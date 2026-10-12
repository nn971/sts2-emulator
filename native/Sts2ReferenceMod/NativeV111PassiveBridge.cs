#if STS2_NATIVE
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Capture;
using Sts2ReferenceMod.Trace;
using Sts2ReferenceMod.Ui;
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
    private static NativeCombatAdapter? _adapter;
    private static int _decisionGeneration;
    private static int _lastCapturedGeneration = -1;
    private static bool _awaitingSettlement;
    private static bool _choiceInterrupted;
    private static int _historyIndex;
    private static bool _handlingFrame;
    private static string? _currentFile;

    internal static void Start()
    {
        string? expected = System.Environment.GetEnvironmentVariable("STS2_FIDELITY_EXPECT_SHA256");
        if (string.IsNullOrWhiteSpace(expected) || expected.Length != 64 ||
            !BuildId.EndsWith("sha256=" + expected.ToLowerInvariant(), StringComparison.Ordinal))
        {
            Godot.GD.PushError("[Sts2ReferenceMod] Refusing capture: exact sts2.dll SHA-256 pin " +
                "missing or mismatched. Run tools/native_game_preflight.py and set " +
                "STS2_FIDELITY_EXPECT_SHA256 to that verified hash.");
            return;
        }
        if (Godot.Engine.GetMainLoop() is not SceneTree tree)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] SceneTree unavailable; passive capture disabled.");
            return;
        }
        if (_tree is not null) return;
        _tree = tree;
        tree.ProcessFrame += OnFrame;
        Godot.GD.Print("[Sts2ReferenceMod] Source-audited passive combat hook enabled; "
            + "local build validation and first parity fixture PENDING. build=" + BuildId);
    }

    private static void OnFrame()
    {
        if (_handlingFrame) return;
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
                if (_recording is not null) EndCombat("Combat ended");
                return;
            }

            var combat = manager.DebugOnlyGetState();
            if (combat is null || combat.Players.Count != 1) return;
            if (!ReferenceEquals(_combat, combat))
            {
                if (_recording is not null) EndCombat("Combat replaced without terminal checkpoint");
                OpenCombat(combat);
            }

            if (_recording is null || _adapter is null || _choiceInterrupted) return;
            AttachActionExecutor();
            if (!IsStablePlayerDecision(combat)) return;
            if (_lastCapturedGeneration == _decisionGeneration) return;
            if (_awaitingSettlement) _awaitingSettlement = false;
            _adapter.Generation = _decisionGeneration;
            var recorded = _recording.OnStableDecision(_adapter);
            if (recorded is not null)
                _lastCapturedGeneration = _decisionGeneration;
        }
        catch (Exception error)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] Passive boundary capture failed: " + error);
            if (_recording is not null) EndCombat("INCOMPLETE_CAPTURE: " + error.GetType().Name);
        }
        finally { _handlingFrame = false; }
    }

    private static bool IsStablePlayerDecision(CombatState combat)
    {
        if (combat.CurrentSide != CombatSide.Player) return false;
        if (CombatManager.Instance.PlayerActionsDisabled) return false;
        if (_executor is null || _executor.IsRunning || _executor.CurrentlyRunningAction is not null)
            return false;
        var player = combat.Players[0];
        if (player.PlayerCombatState?.Phase != PlayerTurnPhase.Play || !player.Creature.IsAlive)
            return false;
        // Native input readiness includes the actual hand interaction mode,
        // which distinguishes play from a card-selection prompt.
        var hand = NCombatRoom.Instance?.Ui?.Hand;
        return hand is not null && !hand.InCardPlay &&
            hand.CurrentMode == NPlayerHand.Mode.Play;
    }

    private static void OpenCombat(CombatState combat)
    {
        _combat = combat;
        _decisionGeneration = 0;
        _lastCapturedGeneration = -1;
        _awaitingSettlement = false;
        _choiceInterrupted = false;
        _historyIndex = CombatManager.Instance.History.Entries.Count;
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
        _panel?.UseSession(_recording, _currentFile);
        CombatManager.Instance.History.Changed += OnHistoryChanged;
        AttachActionExecutor();
        Godot.GD.Print("[Sts2ReferenceMod] Recording (passive): " + _currentFile);
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

    private static void BeforeAction(NativeGameAction action)
    {
        if (_recording is null || _choiceInterrupted) return;
        // The async card execution may request an additional player choice. We
        // must report that unsupported boundary rather than merge two decisions.
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
        if (chosen is null) return;
        try
        {
            _recording.OnHumanAction(chosen);
            _awaitingSettlement = true;
            ++_decisionGeneration;
        }
        catch (InvalidOperationException)
        {
            _choiceInterrupted = true;
            EndCombat("INCOMPLETE_CAPTURE: action started without a stable boundary");
        }
    }

    private static void AfterAction(NativeGameAction action)
    {
        action.BeforePausedForPlayerChoice -= OnNestedChoice;
        // A semantic boundary is detected by the next readiness check,
        // after the native action queue and automatic effects are complete.
    }

    private static void OnNestedChoice(NativeGameAction action)
    {
        _choiceInterrupted = true;
        EndCombat("UNSUPPORTED: nested native player choice within " + action.GetType().Name);
    }

    private static void OnHistoryChanged()
    {
        if (_recording is null || _combat is null) return;
        try
        {
            var history = CombatManager.Instance.History.Entries;
            if (_recording.RecordedDecisions.Count == 0)
            {
                // The audited trace starts at the first stable player choice.
                // Setup effects before that point are represented in the
                // snapshot, but their history order is outside trace coverage.
                _historyIndex = history.Count;
                return;
            }
            while (_historyIndex < history.Count)
            {
                var entry = history[_historyIndex++];
                var effect = HistoryEventProjector.Project(entry, _historyIndex - 1,
                    creature => creature is Creature c
                        ? "creature:" + (c.CombatId?.ToString() ?? "UNASSIGNED")
                        : null);
                // Unexpected history shapes are captured as diagnostics. Until
                // required payload fields have been audited, event coverage
                // remains empty and parity does not assert semantic event order.
                _recording.OnSemanticEffect(effect.Kind, effect.Payload,
                    "CombatHistory.Changed", effect.ActorId, effect.TargetId,
                    visibility: "diagnostic");
            }
        }
        catch (Exception error)
        {
            EndCombat("INCOMPLETE_CAPTURE: history collection error: " + error.Message);
        }
    }

    private static void EndCombat(string reason)
    {
        var recording = _recording;
        if (recording is null) return;
        _recording = null;
        JsonElement? terminal = null;
        try
        {
            if (_adapter is not null) terminal = _adapter.CaptureCanonicalState();
        }
        catch (Exception error)
        {
            reason += "; terminal state unavailable: " + error.GetType().Name;
        }
        recording.OnTerminal(terminal is null ? "INCOMPLETE_CAPTURE" : "TERMINAL",
            reason, terminal);
        _ = FinishAsync(recording);
        if (_executor is not null)
        {
            _executor.BeforeActionExecuted -= BeforeAction;
            _executor.AfterActionExecuted -= AfterAction;
            _executor = null;
        }
        CombatManager.Instance.History.Changed -= OnHistoryChanged;
        _combat = null;
        _adapter = null;
        Godot.GD.Print("[Sts2ReferenceMod] Passive combat trace completed: " + _currentFile);
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
            instance_id = (long)c.CombatCardIndex,
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
            card_instance_id = (long)card.CombatCardIndex,
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
