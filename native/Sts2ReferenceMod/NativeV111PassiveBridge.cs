#if STS2_NATIVE
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Capture;
using Sts2ReferenceMod.Trace;
using Sts2ReferenceMod.Ui;
using CombatState = MegaCrit.Sts2.Core.Combat.CombatState;
using NativeGameAction = MegaCrit.Sts2.Core.GameActions.GameAction;
using CanonicalAction = Sts2Emulator.Core.GameAction;

namespace Sts2ReferenceMod;

/// <summary>
/// Opt-in, read-only v0.111 native combat instrumentation. This binding is
/// source-audited but MUST be validated against a pinned locally installed DLL.
/// It captures player action requests and CombatHistory without affecting
/// action queues, player input, or RNG. Unsupported prompts stop the capture.
/// </summary>
internal static class NativeV111PassiveBridge
{
    private static readonly string BuildId = NativeBuildId();
    private static SceneTree? _tree;
    private static FidelityInspectorPanel? _panel;
    private static bool _f8WasDown;
    private static ActionExecutor? _executor;
    private static CombatState? _combat;
    private static NativeRecordingSession? _recording;
    // Independent action observer: remains active after the strict decision
    // trace reaches an unsupported nested choice or an unstable input boundary.
    private static NativeActionEnvelopeRecorder? _actionEnvelopes;
    private static NativeCombatAdapter? _adapter;
    private static int _decisionGeneration;
    private static int _lastCapturedGeneration = -1;
    private static bool _awaitingSettlement;
    private static bool _choiceInterrupted;
    private static int _historyIndex;
    private static bool _handlingFrame;
    private static string? _currentFile;
    private static CombatState? _failedCombat;
    private static string? _lastGateReason;
    private static bool _hookDisabled;

    // Only report changes in gate state: these diagnostics must not spam
    // every Godot frame or affect player input/RNG.
    private static void ReportGate(string reason)
    {
        if (string.Equals(reason, _lastGateReason, StringComparison.Ordinal)) return;
        _lastGateReason = reason;
        Godot.GD.Print("[Sts2ReferenceMod] CAPTURE_STATUS=" + reason);
    }

    internal static bool ReadyForNativeRngHook => _tree is not null && !_hookDisabled;

    // Source hook callbacks are deliberately separate from rendered-frame
    // snapshots and CombatHistory publication. No RNG is sampled here.
    internal static void NativeRngBoundary(NativeRngBoundaryEvidenceV111 evidence) =>
        _actionEnvelopes?.NativeRngBoundary(evidence);

    internal static void NativeRngScalarBoundary(NativeRngScalarEvidenceV111 evidence) =>
        _actionEnvelopes?.NativeRngScalarBoundary(evidence);

    internal static void NativeRngStreamCheckpoint(
        NativeRngStreamCheckpointV111 evidence) =>
        _actionEnvelopes?.NativeRngStreamCheckpoint(evidence);

    internal static bool HasActionSidecar => _actionEnvelopes is not null;

    internal static void NativeShuffleOperation(
        NativeShuffleOperationEvidenceV111 evidence) =>
        _actionEnvelopes?.NativeShuffleOperation(evidence);

    internal static void NativeInitialHpRoll(
        NativeInitialHpRollEvidenceV111 evidence,
        bool bufferedBeforeFirstFrame = false) =>
        _actionEnvelopes?.NativeInitialHpRoll(evidence, bufferedBeforeFirstFrame);

    internal static void NativeRngHookFailure(string exceptionType) =>
        _actionEnvelopes?.ObservationFailure("native_rng_source_hook", exceptionType);

    internal static void Start()
    {
        string? expected = System.Environment.GetEnvironmentVariable("STS2_FIDELITY_EXPECT_SHA256");
        if (string.IsNullOrWhiteSpace(expected) || expected.Length != 64 ||
            !BuildId.EndsWith("sha256=" + expected.ToLowerInvariant(), StringComparison.Ordinal))
        {
            Godot.GD.PushError("[Sts2ReferenceMod] CAPTURE_STATUS=PIN_REJECTED; " +
                "expected_sha=" + (string.IsNullOrWhiteSpace(expected) ? "MISSING" : expected) +
                "; runtime_build=" + BuildId +
                ". Run tools/native_game_preflight.py and set " +
                "STS2_FIDELITY_EXPECT_SHA256 to the local sts2.dll SHA-256.");
            return;
        }
        if (Godot.Engine.GetMainLoop() is not SceneTree tree)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] CAPTURE_STATUS=SCENE_TREE_UNAVAILABLE; " +
                "SceneTree unavailable; passive capture disabled.");
            return;
        }
        if (_tree is not null) return;
        _tree = tree;
        tree.ProcessFrame += OnFrame;
        Godot.GD.Print("[Sts2ReferenceMod] CAPTURE_STATUS=HOOK_ENABLED; " +
            "build=" + BuildId + "; trace_dir=" +
            (System.Environment.GetEnvironmentVariable("STS2_FIDELITY_TRACE_DIR")
                ?? Path.Combine(OS.GetUserDataDir(), "native_fidelity_traces")) +
            "; native parity NOT_TESTED.");
    }

    private static void OnFrame()
    {
        if (_hookDisabled || _handlingFrame) return;
        _handlingFrame = true;
        try
        {
            if (_panel is null && _tree?.Root is not null)
            {
                _panel = new FidelityInspectorPanel();
                _tree.Root.AddChild(_panel);
            }
            bool f8Down = Godot.Input.IsKeyPressed(Godot.Key.F8);
            if (f8Down && !_f8WasDown) _panel?.Toggle();
            _f8WasDown = f8Down;
            _panel?.Tick();

            var manager = CombatManager.Instance;
            if (!manager.IsInProgress)
            {
                _failedCombat = null;
                if (_combat is not null) EndCombat("Combat ended");
                ReportGate("WAITING_FOR_COMBAT");
                return;
            }

            var combat = manager.DebugOnlyGetState();
            if (combat is null)
            {
                ReportGate("COMBAT_STATE_UNAVAILABLE");
                return;
            }
            if (combat.Players.Count != 1)
            {
                ReportGate("UNSUPPORTED_PLAYER_COUNT");
                return;
            }
            // A fatal bridge error should not spawn a fresh incomplete trace
            // every frame in the same encounter. Retry only next combat.
            if (ReferenceEquals(_failedCombat, combat)) return;
            if (!ReferenceEquals(_combat, combat))
            {
                if (_combat is not null) EndCombat("Combat replaced without terminal checkpoint");
                OpenCombat(combat);
            }

            // Executor callbacks must stay attached even when the stricter
            // decision trace has stopped due to an unsupported player prompt.
            AttachActionExecutor();
            if (_actionEnvelopes is not null && _adapter is not null)
            {
                // Changed-state frame samples continue after the strict player
                // decision trace reaches an unsupported nested UI choice.
                var frameState = TryActionSnapshot("frame_state");
                if (frameState.HasValue)
                    _actionEnvelopes.ObserveFrame(frameState.Value);
            }
            if (_recording is null || _adapter is null || _choiceInterrupted) return;
            string? blocker = StableDecisionBlocker(combat);
            if (blocker is not null)
            {
                ReportGate("WAITING_FOR_DECISION:" + blocker);
                return;
            }
            if (_lastCapturedGeneration == _decisionGeneration)
            {
                ReportGate("AWAITING_PLAYER_ACTION");
                return;
            }
            if (_awaitingSettlement) _awaitingSettlement = false;
            _adapter.Generation = _decisionGeneration;
            var recorded = _recording.OnStableDecision(_adapter);
            if (recorded is not null)
            {
                _lastCapturedGeneration = _decisionGeneration;
                _actionEnvelopes?.StableBoundary(
                    recorded.DecisionId, recorded.State);
                ReportGate("DECISION_CAPTURED:" + recorded.DecisionId);
            }
            else ReportGate("DECISION_NOT_RECORDED");
        }
        catch (Exception error)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] CAPTURE_STATUS=CAPTURE_EXCEPTION; " +
                "Passive boundary capture failed: " + error);
            // Fail closed for this encounter, even if OpenCombat failed before
            // creating a recording. Keep the error once rather than per frame.
            _failedCombat ??= _combat;
            if (_recording is not null) EndCombat("INCOMPLETE_CAPTURE: " + error.GetType().Name);
            if (_failedCombat is null)
            {
                // A failure before any combat is established (e.g. overlay
                // construction or the main-loop callback) cannot be isolated
                // to an encounter. Unregister this observer instead of
                // throwing/logging on every subsequent rendered frame.
                _hookDisabled = true;
                if (_tree is not null) _tree.ProcessFrame -= OnFrame;
                Godot.GD.PushError("[Sts2ReferenceMod] CAPTURE_STATUS=HOOK_DISABLED; " +
                    "fatal frame callback error before combat setup.");
            }
        }
        finally { _handlingFrame = false; }
    }

    private static string? StableDecisionBlocker(CombatState combat)
    {
        if (combat.CurrentSide != CombatSide.Player) return "NOT_PLAYER_SIDE";
        if (CombatManager.Instance.PlayerActionsDisabled) return "PLAYER_ACTIONS_DISABLED";
        if (_executor is null) return "ACTION_EXECUTOR_MISSING";
        if (_executor.IsRunning || _executor.CurrentlyRunningAction is not null)
            return "ACTION_EXECUTOR_BUSY";
        var player = combat.Players[0];
        if (!player.Creature.IsAlive) return "PLAYER_DEAD";
        if (player.PlayerCombatState?.Phase != PlayerTurnPhase.Play)
            return "PLAYER_PHASE_NOT_PLAY";
        // A nested overlay can obscure the hand while its mode remains Play.
        // Treat the overlay as an independent unsupported choice boundary.
        if (NOverlayStack.Instance is { ScreenCount: > 0 }) return "OVERLAY_ACTIVE";
        var hand = NCombatRoom.Instance?.Ui?.Hand;
        if (hand is null) return "HAND_UI_MISSING";
        if (hand.InCardPlay) return "CARD_PLAY_ANIMATION";
        if (hand.CurrentMode != NPlayerHand.Mode.Play) return "HAND_MODE_NOT_PLAY";
        return null;
    }

    private static void OpenCombat(CombatState combat)
    {
        _combat = combat;
        _decisionGeneration = 0;
        _lastCapturedGeneration = -1;
        _awaitingSettlement = false;
        _choiceInterrupted = false;
        _historyIndex = CombatManager.Instance.History.Entries.Count();
        _adapter = new NativeCombatAdapter(combat, () => _choiceInterrupted);
        string runId = Guid.NewGuid().ToString("N");
        string root = System.Environment.GetEnvironmentVariable("STS2_FIDELITY_TRACE_DIR")
            ?? Path.Combine(OS.GetUserDataDir(), "native_fidelity_traces");
        Directory.CreateDirectory(root);
        _currentFile = Path.Combine(root, "passive-" +
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + runId + ".jsonl");
        var header = new NativeTraceHeader(
            "header", NativeTraceVersion.Current, BuildId,
            typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unknown",
            null, runId,
            System.Environment.GetEnvironmentVariable("STS2_FIDELITY_RUN_SEED") ?? "UNVERIFIED",
            DateTimeOffset.UtcNow, "passive", "v111_source_audited_local_validation_pending;"
                + "emulator_commit=UNVERIFIED;static_data=UNVERIFIED;single_player_combat");
        _recording = new NativeRecordingSession(_currentFile, header);
        // A separate append-only action timeline survives unsupported player
        // input semantics, and retains automatic actions before the first hand.
        _actionEnvelopes = new NativeActionEnvelopeRecorder(
            _currentFile[..^".jsonl".Length] + ".actions.jsonl",
            BuildId, runId,
            nativeRngHookInstalled: NativeV111RngHook.IsInstalled,
            nativeMonsterAiScopeInstalled: NativeV111RngHook.HasRollMoveScope,
            nativeShuffleScopeInstalled: NativeV111ShuffleHooks.IsInstalled,
            nativeInitialHpScopeInstalled: NativeV111InitialHpHooks.IsInstalled,
            nativeRngDoubleHookInstalled: NativeV111RngHook.HasDoubleHook,
            nativeRngBoolHookInstalled: NativeV111RngHook.HasBoolHook,
            nativeRngStreamCheckpointsEnabled:
                NativeV111RngHook.StreamCheckpointsEnabled);
        // A source-audited initial shuffle executes before the rendered
        // combat frame. Buffered evidence must precede later RNG requests
        // in this sidecar, with its pre-attachment basis clearly marked.
        // Source-local preframe buffers are flushed separately: the exact
        // cross-mechanic chronology is not inferred from this flush order.
        NativeV111InitialHpHooks.FlushBuffered();
        NativeV111ShuffleHooks.FlushBufferedInitial();
        _actionEnvelopes.HistoryBaseline(_historyIndex);
        // All 12+3 native named streams are bound to their identity only
        // after a single-player CombatState exists. Earlier setup draws are
        // independently marked as unobserved by the diagnostic contract.
        try { NativeV111RngHook.BeginCombat(combat); }
        catch (Exception error)
        {
            _actionEnvelopes.ObservationFailure(
                "native_rng_owner_binding", error.GetType().Name);
        }
        _panel?.UseSession(_recording, _currentFile);
        CombatManager.Instance.History.Changed += OnHistoryChanged;
        AttachActionExecutor();
        Godot.GD.Print("[Sts2ReferenceMod] CAPTURE_STATUS=RECORDING_STARTED; " +
            "Recording (passive): " + _currentFile);
    }

    private static void AttachActionExecutor()
    {
        var current = RunManager.Instance.ActionExecutor;
        if (ReferenceEquals(current, _executor)) return;
        if (_executor is not null)
        {
            _executor.BeforeActionExecuted -= BeforeAction;
            _executor.AfterActionExecuted -= AfterAction;
        }
        _executor = current;
        if (_executor is not null)
        {
            _executor.BeforeActionExecuted += BeforeAction;
            _executor.AfterActionExecuted += AfterAction;
        }
    }

    private static JsonElement? TryActionSnapshot(string stage)
    {
        try { return _adapter?.CaptureCanonicalState().Clone(); }
        catch (Exception error)
        {
            _actionEnvelopes?.ObservationFailure(stage, error.GetType().Name);
            return null;
        }
    }

    private static void BeforeAction(NativeGameAction action)
    {
        try { ObserveBeforeAction(action); }
        catch (Exception error) { CaptureCallbackFailure("before_action", error); }
    }

    private static void ObserveBeforeAction(NativeGameAction action)
    {
        // This callback runs for every executor action, including monster and
        // automatic actions which have no matching human legal-menu item.
        // A distinct timeline protects their evidence from strict-trace gaps.
        var snapshot = TryActionSnapshot("before_action");
        action.BeforePausedForPlayerChoice -= OnNestedChoice;
        action.BeforePausedForPlayerChoice += OnNestedChoice;
        CanonicalAction? chosen = action switch
        {
            PlayCardAction play => new CanonicalAction("play_card",
                JsonSerializer.SerializeToElement(new
                {
                    card_instance_id = (long)play.NetCombatCard.CombatCardIndex,
                    target_id = play.TargetId.HasValue ? "creature:" + play.TargetId.Value : null
                })),
            EndPlayerTurnAction => CanonicalAction.Empty("end_turn"),
            _ => null
        };
        _actionEnvelopes?.Start(
            action, action.GetType().FullName ?? action.GetType().Name,
            snapshot, chosen);
        NativeV111RngHook.CaptureStreamCheckpoint("before_action");

        if (_recording is null || _choiceInterrupted || chosen is null) return;
        try
        {
            _recording.OnHumanAction(chosen);
            _awaitingSettlement = true;
            ++_decisionGeneration;
        }
        catch (InvalidOperationException)
        {
            _choiceInterrupted = true;
            StopStrictCapture("INCOMPLETE_CAPTURE: action started without a stable boundary");
        }
    }

    private static void AfterAction(NativeGameAction action)
    {
        try
        {
            _actionEnvelopes?.Complete(action, TryActionSnapshot("after_action"));
            NativeV111RngHook.CaptureStreamCheckpoint("after_action");
        }
        catch (Exception error) { CaptureCallbackFailure("after_action", error); }
        finally
        {
            try { action.BeforePausedForPlayerChoice -= OnNestedChoice; }
            catch (Exception error) { CaptureCallbackFailure("detach_choice", error); }
        }
        // AfterAction is an executor callback, not necessarily a settled
        // semantic input boundary. The sidecar records it as such.
    }

    private static void OnNestedChoice(NativeGameAction action)
    {
        try
        {
            _actionEnvelopes?.ChoicePause(
                action, TryActionSnapshot("nested_choice_pause"));
            _choiceInterrupted = true;
            StopStrictCapture("UNSUPPORTED: nested native player choice within " + action.GetType().Name);
        }
        catch (Exception error) { CaptureCallbackFailure("nested_choice", error); }
        // Keep executor and history subscriptions active for the sidecar.
    }

    private static void CaptureCallbackFailure(string stage, Exception error)
    {
        // Passive event handlers execute inside native game callbacks.
        // Never propagate an Inspector exception into the game's action
        // executor; mark incomplete and leave the underlying action alone.
        try { _actionEnvelopes?.ObservationFailure(stage, error.GetType().Name); }
        catch (Exception) { }
        try
        {
            _choiceInterrupted = true;
            StopStrictCapture("INCOMPLETE_CAPTURE: observer failure at " + stage);
        }
        catch (Exception) { _recording = null; }
        try { Godot.GD.PushError("[Sts2ReferenceMod] Observer callback " + stage +
                                 " failed: " + error.GetType().Name); }
        catch (Exception) { }
    }

    private static void StopStrictCapture(string reason)
    {
        var recording = _recording;
        if (recording is null) return;
        _recording = null;
        JsonElement? state = TryActionSnapshot("strict_trace_terminal");
        try { recording.OnTerminal("INCOMPLETE_CAPTURE", reason, state); }
        catch (Exception error)
        {
            _actionEnvelopes?.ObservationFailure(
                "strict_trace_terminal", error.GetType().Name);
        }
        finally { _ = FinishAsync(recording); }
        ReportGate("STRICT_TRACE_STOPPED_ACTION_SIDECAR_CONTINUES");
    }

    private static void OnHistoryChanged()
    {
        if (_combat is null || (_recording is null && _actionEnvelopes is null)) return;
        try
        {
            // Entries is IEnumerable<CombatHistoryEntry>, not IReadOnlyList.
            // Take a stable ordered snapshot for this Changed callback rather
            // than indexing the enumerable or enumerating it repeatedly.
            var history = CombatManager.Instance.History.Entries.ToArray();
            if (history.Length < _historyIndex)
            {
                // Capture the reset in the sidecar, and continue with a new
                // history epoch. The strict decision trace stops fail-closed.
                _actionEnvelopes?.HistoryReset(_historyIndex, history.Length);
                _historyIndex = 0;
                StopStrictCapture(
                    "INCOMPLETE_CAPTURE: native CombatHistory reset during recording");
            }
            while (_historyIndex < history.Length)
            {
                var entry = history[_historyIndex++];
                var effect = HistoryEventProjector.Project(entry, _historyIndex - 1,
                    creature => creature is Creature c
                        ? "creature:" + (c.CombatId?.ToString() ?? "UNASSIGNED")
                        : null);
                // Preserve all entries in the independent action timeline,
                // including automatic actions and history after a nested choice.
                _actionEnvelopes?.HistoryEntry(_historyIndex - 1,
                    effect.Kind, effect.Payload, effect.ActorId, effect.TargetId,
                    effect.MissingFields);
                // Strict decision events begin only after its first semantic
                // boundary; earlier history remains sidecar-only.
                if (_recording?.RecordedDecisions.Count > 0)
                    _recording.OnSemanticEffect(effect.Kind, effect.Payload,
                        "CombatHistory.Changed", effect.ActorId, effect.TargetId,
                        visibility: "diagnostic");
            }
        }
        catch (Exception error)
        {
            _actionEnvelopes?.ObservationFailure(
                "history_collection", error.GetType().Name);
            StopStrictCapture(
                "INCOMPLETE_CAPTURE: history collection error: " + error.GetType().Name);
        }
    }

    private static void EndCombat(string reason)
    {
        NativeV111RngHook.EndCombat();
        var recording = _recording;
        var actionEnvelopes = _actionEnvelopes;
        if (recording is null && actionEnvelopes is null && _combat is null)
            return;
        _recording = null;
        _actionEnvelopes = null;
        JsonElement? terminal = null;
        try
        {
            if (_adapter is not null) terminal = _adapter.CaptureCanonicalState();
        }
        catch (Exception error)
        {
            reason += "; terminal state unavailable: " + error.GetType().Name;
        }
        if (recording is not null)
        {
            recording.OnTerminal(terminal is null ? "INCOMPLETE_CAPTURE" : "TERMINAL",
                reason, terminal);
            _ = FinishAsync(recording);
        }
        if (actionEnvelopes is not null)
        {
            actionEnvelopes.Finish(reason, terminal);
            _ = FinishActionEnvelopesAsync(actionEnvelopes);
        }
        if (_executor is not null)
        {
            _executor.BeforeActionExecuted -= BeforeAction;
            _executor.AfterActionExecuted -= AfterAction;
            _executor = null;
        }
        CombatManager.Instance.History.Changed -= OnHistoryChanged;
        _combat = null;
        _adapter = null;
        Godot.GD.Print("[Sts2ReferenceMod] Passive combat capture completed: " + _currentFile);
    }

    private static async Task FinishActionEnvelopesAsync(
        NativeActionEnvelopeRecorder observer)
    {
        try { await observer.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] Action sidecar writer failed: " + error);
        }
    }

    private static async Task FinishAsync(NativeRecordingSession session)
    {
        try { await session.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] Trace writer failed: " + error);
        }
    }

    private static string NativeBuildId()
    {
        Assembly assembly = typeof(CombatManager).Assembly;
        string version = assembly.GetName().Version?.ToString() ?? "unknown";
        string mvid = assembly.ManifestModule.ModuleVersionId.ToString("N");
        try
        {
            string path = assembly.Location;
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                return "assembly=" + version + ";mvid=" + mvid + ";sha256=" + hash;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return "assembly=" + version + ";mvid=" + mvid + ";sha256=UNAVAILABLE";
    }
}

internal sealed class NativeCombatAdapter(
    CombatState combat, Func<bool> nestedChoicePending) : INativeCaptureAdapter
{
    private static readonly string[] PrimitivePaths =
    {
        "$.player.hp", "$.player.max_hp", "$.player.block",
        "$.combat.energy", "$.combat.max_energy", "$.combat.turn",
        "$.combat.creatures", "$.combat.hand", "$.combat.draw_pile",
        "$.combat.discard_pile", "$.combat.exhaust_pile"
    };

    public int Generation { get; set; }
    private Player Player => combat.Players[0];
    public string Phase => "Combat";
    public bool IsAtStablePlayerChoice => !nestedChoicePending()
        && Player.PlayerCombatState?.Phase == PlayerTurnPhase.Play;
    public string SemanticBoundaryKey => "combat:" + Generation;
    public string ObservationHash => CanonicalJson.Sha256(CaptureCanonicalState());

    public JsonElement CaptureCanonicalState()
    {
        var player = Player;
        var pcs = player.PlayerCombatState
            ?? throw new InvalidOperationException("PlayerCombatState not initialized.");
        return JsonSerializer.SerializeToElement(new
        {
            player = new
            {
                hp = player.Creature.CurrentHp,
                max_hp = player.Creature.MaxHp,
                block = player.Creature.Block,
                gold = player.Gold,
                powers = Powers(player.Creature)
            },
            combat = new
            {
                energy = pcs.Energy,
                max_energy = pcs.MaxEnergy,
                turn = pcs.TurnNumber,
                creatures = combat.Creatures.Select(creature => new
                {
                    id = CreatureId(creature),
                    model_id = creature.ModelId.Entry,
                    hp = creature.CurrentHp,
                    max_hp = creature.MaxHp,
                    block = creature.Block,
                    alive = creature.IsAlive,
                    powers = Powers(creature)
                }).ToArray(),
                hand = Cards(pcs.Hand),
                draw_pile = Cards(pcs.DrawPile),
                discard_pile = Cards(pcs.DiscardPile),
                exhaust_pile = Cards(pcs.ExhaustPile)
            },
            rng = new { status = "RNG_UNAUDITED" },
            unsupported_continuation = new[]
            {
                "relic_and_potion_mutable_state", "creature_move_history",
                "card_persistent_state", "complete_rng_streams", "pending_effects",
                "nested_selection_prompts", "global_encounter_history"
            }
        });
    }

    private static object[] Powers(Creature creature) => creature.Powers
        .Select(p => (object)new { model_id = p.Id.Entry, amount = p.Amount }).ToArray();

    private static object[] Cards(CardPile pile) => pile.Cards
        .Select(c => (object)new
        {
            instance_id = (long)NetCombatCard.FromModel(c).CombatCardIndex,
            model_id = c.Id.Entry,
            upgrades = c.CurrentUpgradeLevel,
            target_type = c.TargetType.ToString(),
            cost = c.EnergyCost.GetResolved()
        }).ToArray();

    private static string CreatureId(Creature creature) =>
        "creature:" + (creature.CombatId?.ToString() ?? "UNASSIGNED");

    public IReadOnlyList<CanonicalAction> GetCanonicalLegalActions()
    {
        var result = new List<CanonicalAction>();
        var pcs = Player.PlayerCombatState!;
        foreach (var card in pcs.Hand.Cards)
        {
            if (!card.CanPlay(out _, out _)) continue;
            if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyAlly)
            {
                foreach (var target in combat.Creatures)
                {
                    if (!card.IsValidTarget(target)) continue;
                    result.Add(PlayCard(card, CreatureId(target)));
                }
            }
            else result.Add(PlayCard(card, null));
        }
        result.Add(CanonicalAction.Empty("end_turn"));
        return result;
    }

    private static CanonicalAction PlayCard(CardModel card, string? target) =>
        new("play_card", JsonSerializer.SerializeToElement(new
        {
            card_instance_id = (long)NetCombatCard.FromModel(card).CombatCardIndex,
            target_id = target
        }));

    public CaptureCoverage GetCoverage()
    {
        bool potions = Player.PotionSlots.Any(p => p is not null);
        bool exoticTargets = Player.PlayerCombatState!.Hand.Cards.Any(c =>
            c.TargetType is TargetType.AnyAlly);
        var missing = new[] { "$.combat.pending_choice", "$.combat.rng_state",
            "$.inventory.relics", "$.inventory.potions", "$.combat.move_history",
            "$.combat.hidden_card_state" };
        var unsupported = new List<string>
        {
            "NATIVE_V111_PINNED_BUILD_VALIDATION_PENDING",
            "HUMAN_ACTION_ORIGIN_UNVERIFIED",
            "PRE_BOUNDARY_COMBAT_HISTORY_UNAUDITED"
        };
        if (potions) unsupported.Add("POTIONS_NOT_IN_LEGAL_MENU");
        if (exoticTargets) unsupported.Add("ALLY_TARGETING_UNVERIFIED");
        return new CaptureCoverage(
            PrimitivePaths,
            missing, unsupported.ToArray(), "RNG_UNAUDITED",
            Array.Empty<string>(),
            new[] { "$.player.hp", "$.player.block", "$.combat.energy",
                "$.combat.creatures", "$.combat.hand" },
            LegalActionsComplete: !potions && !exoticTargets);
    }
}
#endif
