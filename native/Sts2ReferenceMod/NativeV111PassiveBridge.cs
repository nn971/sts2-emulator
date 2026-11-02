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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Capture;
using Sts2ReferenceMod.Trace;
using Sts2ReferenceMod.Transport;
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
    private static PlayerChoiceSynchronizer? _choiceSync;
    private static CombatState? _combat;
    private static NativeRecordingSession? _recording;
    // Independent action observer: remains active after the strict decision
    // trace reaches an unsupported nested choice or an unstable input boundary.
    private static NativeActionEnvelopeRecorder? _actionEnvelopes;
    // Independent source-run prelude, begun at RunManager.RunStarted.
    // Its first-combat link is retained even before a combat sidecar exists.
    private static NativeRunPreludeRecorder? _runPrelude;
    private static MegaCrit.Sts2.Core.Runs.RunState? _preludeRunState;
    // Generated ActModel queues are read after RunManager.GenerateRooms;
    // reflection is read-only and never asks a bag to generate or pull.
    private static readonly FieldInfo? NativeActRoomSetField =
        typeof(ActModel).GetField("_rooms",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NativeStandardMapRngField =
        typeof(StandardActMap).GetField("_rng",
            BindingFlags.Instance | BindingFlags.NonPublic);
    // Source: Flaaax/STS2-source@706ef1c9, pinned v0.111.0.
    // Play-phase requests may be deferred outside the ActionQueueSet. A
    // drained ActionQueueSet alone must never conceal those pending requests.
    private static readonly FieldInfo? NativeDeferredPlayerActionsField =
        typeof(ActionQueueSynchronizer).GetField(
            "_requestedActionsWaitingForPlayerTurn",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static bool _initialMapProjectionAttempted;
    private static bool _missingMapAtFirstInputReported;
    private static NativeCombatAdapter? _adapter;
    private static int _decisionGeneration;
    private static int _lastCapturedGeneration = -1;
    private static bool _awaitingSettlement;
    // Independent turn-start candidates continue even after strict input
    // capture reaches its decision-9 gap. No candidate is a parity certificate.
    private static readonly HashSet<int> ObservedPlayerInputTurns = [];
    private static readonly HashSet<int> ObservedPlayerStartTurns = [];
    private static readonly HashSet<int> ObservedEnemyStartTurns = [];
    private static bool _choiceInterrupted;
    private static int _historyIndex;
    private static bool _handlingFrame;
    private static string? _currentFile;
    private static CombatState? _failedCombat;
    private static string? _lastGateReason;
    private static bool _hookDisabled;
    private static NativeAgentBoundaryFeed? _agentFeed;
    private static LocalBridgeServer? _agentBridge;
    private static NativeLiveEventHub? _liveEvents;

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

    internal static void NativeInputHookFailure(string exceptionType)
    {
        _actionEnvelopes?.ObservationFailure("native_input_source_hook", exceptionType);
        _runPrelude?.ObservationFailure("native_input_source_hook", exceptionType);
    }

    // Called by a source method Harmony PREFIX, while the original native
    // request is still pending. Acceptance and human provenance are unknown.
    internal static void NativeInputRequested(NativeGameAction action)
    {
        if (_combat is null && _runPrelude is not null)
        {
            try
            {
                TryCaptureInitialNativeMap();
                if ((action is MoveToMapCoordAction or VoteForMapCoordAction)
                    && !_runPrelude.HasInitialMapSnapshot
                    && !_missingMapAtFirstInputReported)
                {
                    _missingMapAtFirstInputReported = true;
                    _runPrelude.ObservationFailure("initial_map_snapshot",
                        "MAP_UNAVAILABLE_BEFORE_FIRST_MAP_REQUEST");
                }
                ObservePrecombatMapRequest(action);
            }
            catch (Exception error)
            {
                _runPrelude.ObservationFailure("precombat_map_request",
                    error.GetType().Name);
            }
        }
        if (_actionEnvelopes is null || _combat is null) return;
        _actionEnvelopes.NativeInputRequest(action,
            action.GetType().FullName ?? action.GetType().Name,
            TryActionSnapshot("native_input_request"),
            ProjectCanonicalAction(action));
    }

    internal static void NativeInputRequestOutcome(
        NativeGameAction action, string nativeState, uint? nativeActionId)
    {
        _actionEnvelopes?.NativeInputOutcome(action, nativeState, nativeActionId);
        _runPrelude?.MapActionOutcome(action, nativeState, nativeActionId);
        TryFinishRunPrelude();
    }

    internal static bool HasRunPrelude => _runPrelude is not null && _combat is null;

    internal static void NativeEventChoiceRequested(
        object token, string eventModelId, ulong ownerId, int selectedIndex,
        IReadOnlyList<NativeEventOptionView> options,
        NativeEventRngView eventRng) =>
        _runPrelude?.EventChoiceRequested(
            token, eventModelId, ownerId, selectedIndex, options, eventRng);

    internal static void NativeEventChoiceReturned(object token)
    {
        _runPrelude?.EventChoiceReturned(token);
        TryFinishRunPrelude();
    }

    /// <summary>Read-only capture of the original UI-filtered hand options.</summary>
    internal static void NativeHandMenu(
        string mode, string? sourceModelId,
        int minSelect, int maxSelect, bool requireConfirmation,
        int holderCount, int missingCardIds,
        IReadOnlyList<NativeHandMenuOption> options,
        ulong? choiceOwnerId, uint? choiceId, string bindingStatus,
        object nativeHand) =>
        _actionEnvelopes?.NativeHandMenu(mode, sourceModelId, minSelect,
            maxSelect, requireConfirmation, holderCount, missingCardIds,
            options, choiceOwnerId, choiceId, bindingStatus, nativeHand);

    /// <summary>Read completed native visibility updates, without rerunning a filter.</summary>
    internal static void NativeHandMenuRefresh(
        object nativeHand, string mode, bool peekMode, int holderCount,
        int missingCardIds, IReadOnlyList<NativeHandMenuOption> options) =>
        _actionEnvelopes?.NativeHandMenuRefresh(nativeHand, mode, peekMode,
            holderCount, missingCardIds, options);

    /// <summary>Close the original UI menu lifetime, preserving native reason.</summary>
    internal static void NativeHandMenuClosed(object nativeHand, string reason) =>
        _actionEnvelopes?.NativeHandMenuClosed(nativeHand, reason);

    /// <summary>Read the original native hand-click/shortcut UI gate result.</summary>
    internal static void NativeUiHandGate(
        string sourceMethod, bool allowed, string nativeMode,
        bool existingCardPlay) =>
        _actionEnvelopes?.NativeUiHandGate(
            sourceMethod, allowed, nativeMode, existingCardPlay);

    /// <summary>Original native UI card interaction before drag/targeting.</summary>
    internal static void NativeUiPlayBegin(
        long? combatCardId, string cardModelId, bool viaShortcut)
    {
        // Hooked at original NPlayerHand.StartCardPlay.prefix. Capture here
        // before the game processes targeting and queueing; the preceding
        // frame can be stale while the prior card executor remains active.
        // No native legal-menu predicates or RNG are evaluated by this hook.
        if (_actionEnvelopes is null) return;
        _actionEnvelopes.NativeUiPlayBegin(
            combatCardId, cardModelId, viaShortcut,
            TryOptionalUiBeginSnapshot());
    }

    /// <summary>Result of native CanPlayTargeting/manual action request.</summary>
    internal static void NativeManualPlayReturn(
        long? combatCardId, string cardModelId,
        string? targetId, bool success) =>
        _actionEnvelopes?.NativeManualPlayResult(
            combatCardId, cardModelId, targetId, success);

    private static CanonicalAction? ProjectCanonicalAction(NativeGameAction action) =>
        action switch
        {
            PlayCardAction play => new CanonicalAction("play_card",
                JsonSerializer.SerializeToElement(new
                {
                    card_instance_id = (long)play.NetCombatCard.CombatCardIndex,
                    target_id = play.TargetId.HasValue
                        ? "creature:" + play.TargetId.Value : null
                })),
            EndPlayerTurnAction => CanonicalAction.Empty("end_turn"),
            _ => null
        };

    private static void OnNativePlayerChoice(
        Player player, uint choiceId, NetPlayerChoiceResult result)
    {
        try
        {
            // The game's synchronizer publishes the selected *result*.
            // CombatCard choices carry original combat-card indexes, including
            // Survivor's selected discard. This event has no legal menu.
            if (_runPrelude is not null && _combat is null)
                _runPrelude.PrecombatPlayerChoice(
                    player.NetId, choiceId, result.type.ToString(),
                    result.combatCards?.Select(c =>
                        (long)c.CombatCardIndex).ToArray(),
                    result.deckCards?.Select(c =>
                        (long)c.DeckIndex).ToArray(),
                    result.canonicalCards?.Select(c =>
                        c.Id.Entry).ToArray(),
                    result.indexes?.ToArray(), result.playerId,
                    result.mutableCards?.Count, result.mutableCardOwner);
            _actionEnvelopes?.NativePlayerChoice(
                player.NetId, choiceId, result.type.ToString(),
                result.combatCards?.Select(card =>
                    (long)card.CombatCardIndex).ToArray(),
                result.indexes?.ToArray(),
                result.canonicalCards?.Select(card =>
                    card.Id.Entry).ToArray(),
                result.playerId);
        }
        catch (Exception error)
        {
            CaptureCallbackFailure("native_player_choice", error);
        }
    }

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
        // RunStarted provides the only source-verifiable initial run identity;
        // subscribing after a run began deliberately does not invent it.
        RunManager.Instance.RunStarted += OnRunStarted;
        RunManager.Instance.RoomEntered += OnRunRoomEntered;
        StartSpectatorIfRequested();
        tree.ProcessFrame += OnFrame;
        Godot.GD.Print("[Sts2ReferenceMod] CAPTURE_STATUS=HOOK_ENABLED; " +
            "build=" + BuildId + "; trace_dir=" +
            (System.Environment.GetEnvironmentVariable("STS2_FIDELITY_TRACE_DIR")
                ?? Path.Combine(OS.GetUserDataDir(), "native_fidelity_traces")) +
            "; native parity NOT_TESTED.");
    }

    private static void OnRunStarted(MegaCrit.Sts2.Core.Runs.RunState state)
    {
        try
        {
            if (_runPrelude is not null)
            {
                if (!_runPrelude.HasLinkedCombatRoomAndSettledMapRequests)
                    _runPrelude.ObservationFailure("run_replaced",
                        "PREVIOUS_PRELUDE_UNSETTLED");
                _runPrelude.Finish("A new RunStarted replaced the previous prelude");
                _ = FinishRunPreludeAsync(_runPrelude);
                _runPrelude = null;
            }
            if (_choiceSync is not null)
            {
                _choiceSync.PlayerChoiceReceived -= OnNativePlayerChoice;
                _choiceSync = null;
            }
            _preludeRunState = state;
            _initialMapProjectionAttempted = false;
            _missingMapAtFirstInputReported = false;
            if (state.Players.Count != 1) return;
            string seed = state.Rng.StringSeed;
            if (string.IsNullOrWhiteSpace(seed)) return;
            string runId = Guid.NewGuid().ToString("N");
            string root = System.Environment.GetEnvironmentVariable(
                "STS2_FIDELITY_TRACE_DIR")
                ?? Path.Combine(OS.GetUserDataDir(), "native_fidelity_traces");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root,
                "run-prelude-" +
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") +
                "-" + runId + ".jsonl");
            _runPrelude = new NativeRunPreludeRecorder(
                path, BuildId, runId, seed, state.AscensionLevel,
                state.Players[0].Character.Id.Entry, state.Act.Id.Entry,
                state.Players.Count);
            CapturePreludeRng("run_started", state);
            CapturePreludeRoomQueues(state);
            // Launch() raises RunStarted after synchronizer construction in
            // pinned v111. Subscribe now to capture Neow-triggered nested
            // card/deck choices before the first combat observer exists.
            _choiceSync = RunManager.Instance.PlayerChoiceSynchronizer;
            if (_choiceSync is null)
                _runPrelude.ObservationFailure("precombat_player_choice",
                    "SOURCE_SYNCHRONIZER_UNAVAILABLE");
            else
                _choiceSync.PlayerChoiceReceived += OnNativePlayerChoice;
            GD.Print("[Sts2ReferenceMod] RUN_PRELUDE_STATUS=RUN_STARTED; " +
                "source=RunManager.RunStarted; path=" + path);
        }
        catch (Exception error)
        {
            _runPrelude?.ObservationFailure("run_started", error.GetType().Name);
            GD.PushError("[Sts2ReferenceMod] RUN_PRELUDE_STATUS=FAILED;" +
                " reason=" + error.GetType().Name);
        }
    }

    private static void CapturePreludeRoomQueues(
        MegaCrit.Sts2.Core.Runs.RunState state)
    {
        if (_runPrelude is null) return;
        try
        {
            if (NativeActRoomSetField is null)
                throw new MissingFieldException("ActModel._rooms");
            var queued = state.Acts.Select((act, index) =>
            {
                if (NativeActRoomSetField.GetValue(act) is not RoomSet rooms)
                    throw new InvalidDataException(
                        "Native ActModel has no generated RoomSet.");
                return new NativeStartupActRoomQueueView(
                    index, act.Id.Entry,
                    rooms.events.Select(e => e.Id.Entry).ToArray(),
                    rooms.normalEncounters.Select(e => e.Id.Entry).ToArray(),
                    rooms.eliteEncounters.Select(e => e.Id.Entry).ToArray(),
                    rooms.Boss.Id.Entry,
                    rooms.SecondBoss?.Id.Entry,
                    rooms.Ancient.Id.Entry,
                    rooms.eventsVisited, rooms.normalEncountersVisited,
                    rooms.eliteEncountersVisited, rooms.bossEncountersVisited);
            }).ToArray();
            _runPrelude.StartupRoomQueues(queued);
        }
        catch (Exception error)
        {
            _runPrelude.ObservationFailure("startup_room_queues",
                error.GetType().Name);
        }
    }

    private static void OnRunRoomEntered()
    {
        if (_runPrelude is null || _preludeRunState is not { } state
            || state.Players.Count != 1) return;
        try
        {
            TryCaptureInitialNativeMap();
            var coord = state.CurrentMapCoord;
            var room = state.CurrentRoom;
            _runPrelude.RoomEntered(
                state.CurrentActIndex, state.Act.Id.Entry,
                state.ActFloor, state.TotalFloor,
                coord?.col, coord?.row,
                room?.RoomType.ToString() ?? "UNAVAILABLE",
                room?.ModelId?.Entry,
                state.Players[0].Creature.CurrentHp,
                state.Players[0].Gold);
            CapturePreludeRng("room_entered", state);
            TryFinishRunPrelude();
        }
        catch (Exception error)
        {
            _runPrelude.ObservationFailure("room_entered",
                error.GetType().Name);
        }
    }

    private static void ObservePrecombatMapRequest(NativeGameAction action)
    {
        if (_runPrelude is null || _preludeRunState is null) return;
        // Pinned source ToNetAction() for these two actions only constructs
        // a value-type envelope from existing private fields; no RNG/queue.
        if (action is MoveToMapCoordAction move
            && move.ToNetAction() is NetMoveToMapCoordAction netMove)
        {
            var prior = _preludeRunState.CurrentMapCoord;
            _runPrelude.MapActionRequested(
                action, "map_move", _preludeRunState.CurrentActIndex,
                prior?.col, prior?.row,
                netMove.destination.col, netMove.destination.row, null);
        }
        else if (action is VoteForMapCoordAction vote
            && vote.ToNetAction() is NetVoteForMapCoordAction netVote)
        {
            var from = netVote.source.coord;
            var to = netVote.destination;
            _runPrelude.MapActionRequested(
                action, "map_vote", netVote.source.actIndex,
                from?.col, from?.row,
                to?.coord.col, to?.coord.row, to?.mapGenerationCount);
        }
        else
        {
            // Non-map source requests remain opaque: observing their CLR
            // class and queue return is safe, but does not capture an
            // executable Neow/reward/event choice.
            _runPrelude.OpaqueActionRequested(action,
                action.GetType().FullName ?? action.GetType().Name);
        }
    }

    /// <summary>
    /// RunManager.GenerateMap installs the finalized ActMap after native
    /// post-generation hooks. Its StandardActMap._rng is an independent,
    /// content-local generator; ToSerializable only copies its cursor.
    /// A one-shot projection avoids sampling the map every rendered frame.
    /// </summary>
    private static void TryCaptureInitialNativeMap()
    {
        var recorder = _runPrelude;
        var state = _preludeRunState;
        if (recorder is null || recorder.HasInitialMapSnapshot
            || _initialMapProjectionAttempted || state is null
            || state.Players.Count != 1 || state.CurrentActIndex != 0
            || state.Map is not StandardActMap map)
            return;
        _initialMapProjectionAttempted = true;
        try
        {
            if (NativeStandardMapRngField?.GetValue(map)
                    is not MegaCrit.Sts2.Core.Random.Rng mapRng)
                throw new InvalidOperationException("SOURCE_STANDARD_MAP_RNG_UNAVAILABLE");
            // Begin with precisely the finalized native objects. The
            // starting ancient and boss are outside ActMap.Grid and must
            // be included explicitly for a complete topology comparison.
            var nativePoints = map.GetAllMapPoints()
                .Append(map.StartingMapPoint)
                .Append(map.BossMapPoint)
                .Concat(map.SecondBossMapPoint is { } second
                    ? new[] { second } : Array.Empty<MapPoint>())
                .OrderBy(p => p.coord.row).ThenBy(p => p.coord.col)
                .Select(p => new NativeMapPointView(
                    p.coord.col, p.coord.row, p.PointType.ToString(),
                    p.CanBeModified,
                    p.Children.OrderBy(c => c.coord.row)
                        .ThenBy(c => c.coord.col)
                        .Select(c => new NativeMapCoordView(
                            c.coord.col, c.coord.row))
                        .ToArray()))
                .ToArray();
            var snapshot = mapRng.ToSerializable();
            recorder.InitialMapSnapshot(
                state.CurrentActIndex, state.Act.Id.Entry,
                nativePoints,
                new NativeMapRngView(snapshot.counter, snapshot.state0,
                    snapshot.state1, snapshot.state2, snapshot.state3));
            if (recorder.HasInitialMapSnapshot)
                GD.Print("[Sts2ReferenceMod] NATIVE_MAP_STATUS=SOURCE_CAPTURED;" +
                    " nodes=" + nativePoints.Length +
                    "; rng_counter=" + snapshot.counter +
                    "; emulator_parity=NOT_TESTED");
        }
        catch (Exception error)
        {
            recorder.ObservationFailure("initial_map_snapshot",
                error.GetType().Name);
        }
    }

    private static void CapturePreludeRng(
        string site, MegaCrit.Sts2.Core.Runs.RunState state)
    {
        if (_runPrelude is null || state.Players.Count != 1) return;
        try
        {
            // Both ToSerializable calls inspect native RNG words/counters
            // without advancing or reconstructing any source RNG stream.
            var run = state.Rng.ToSerializable();
            var player = state.Players[0].PlayerRng.ToSerializable();
            var streams = run.Rngs.Select(pair =>
                new NativeRunRngStreamView(
                    "run", pair.Key.ToString(), pair.Value.counter,
                    pair.Value.state0, pair.Value.state1,
                    pair.Value.state2, pair.Value.state3))
                .Concat(player.Rngs.Select(pair =>
                    new NativeRunRngStreamView(
                        "player", pair.Key.ToString(), pair.Value.counter,
                        pair.Value.state0, pair.Value.state1,
                        pair.Value.state2, pair.Value.state3)))
                .ToArray();
            _runPrelude.RunRngSnapshot(site, player.Seed, streams);
        }
        catch (Exception error)
        {
            _runPrelude.ObservationFailure("source_rng_snapshot",
                error.GetType().Name);
        }
    }

    private static void TryFinishRunPrelude()
    {
        var prelude = _runPrelude;
        if (prelude is null
            || !prelude.HasLinkedCombatRoomAndSettledMapRequests)
            return;
        TryCaptureInitialNativeMap();
        if (!prelude.HasInitialMapSnapshot)
            prelude.ObservationFailure("initial_map_snapshot",
                "MISSING_INITIAL_NATIVE_MAP_AT_COMBAT");
        prelude.Finish("Linked first combat room and map returns observed");
        _runPrelude = null;
        _preludeRunState = null;
        _ = FinishRunPreludeAsync(prelude);
    }

    private static async Task FinishRunPreludeAsync(
        NativeRunPreludeRecorder recorder)
    {
        try { await recorder.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error)
        {
            GD.PushError("[Sts2ReferenceMod] RUN_PRELUDE_STATUS=WRITE_FAILED;" +
                " reason=" + error.GetType().Name);
        }
    }

    private static void StartSpectatorIfRequested()
    {
        if (System.Environment.GetEnvironmentVariable("STS2_INSPECTOR_AGENT_BRIDGE") != "1")
            return;
        string? token = System.Environment.GetEnvironmentVariable(
            "STS2_INSPECTOR_AGENT_TOKEN");
        if (string.IsNullOrWhiteSpace(token) || token.Length < 24)
        {
            Godot.GD.PushError("[Sts2ReferenceMod] AGENT_BRIDGE_STATUS=TOKEN_REQUIRED;" +
                " supply a unique token of at least 24 characters.");
            return;
        }
        int port = 39221;
        var configured = System.Environment.GetEnvironmentVariable(
            "STS2_INSPECTOR_AGENT_PORT");
        if (configured is not null &&
            (!int.TryParse(configured, out port) || port is < 1024 or > 65535))
        {
            Godot.GD.PushError("[Sts2ReferenceMod] AGENT_BRIDGE_STATUS=INVALID_PORT");
            return;
        }
        try
        {
            var feed = new NativeAgentBoundaryFeed();
            string root = System.Environment.GetEnvironmentVariable(
                "STS2_FIDELITY_TRACE_DIR")
                ?? Path.Combine(OS.GetUserDataDir(), "native_fidelity_traces");
            var prefix = Path.Combine(root, "live-" +
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" +
                Guid.NewGuid().ToString("N"));
            var events = new NativeLiveEventHub(prefix);
            string? fidelityToken = System.Environment.GetEnvironmentVariable(
                "STS2_INSPECTOR_FIDELITY_TOKEN");
            var server = new LocalBridgeServer(feed, token, port,
                liveEvents: events, fidelityToken: fidelityToken);
            server.Start();
            _agentFeed = feed;
            _liveEvents = events;
            _agentBridge = server;
            Godot.GD.Print("[Sts2ReferenceMod] AGENT_BRIDGE_STATUS=SPECTATOR_READY;" +
                " schema=" + NativeAgentBoundaryFeed.SchemaId +
                "; address=127.0.0.1:" + port +
                "; actions=DISABLED; read_only=true");
        }
        catch (Exception error)
        {
            _agentFeed = null;
            _liveEvents = null;
            _agentBridge = null;
            Godot.GD.PushError("[Sts2ReferenceMod] AGENT_BRIDGE_STATUS=START_FAILED;" +
                " reason=" + error.GetType().Name);
        }
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
                // Native mod DLLs can be loaded without Godot registering
                // this managed CanvasLayer as a script. Do not depend on the
                // virtual _Ready callback to build the overlay controls.
                _panel.Initialize();
                bool showOnStart = System.Environment.GetEnvironmentVariable(
                    "STS2_INSPECTOR_PANEL_SHOW_ON_START") == "1";
                _panel.SetShown(showOnStart);
                Godot.GD.Print("[Sts2ReferenceMod] INSPECTOR_PANEL_STATUS=CREATED;" +
                    " visible=" + _panel.Visible +
                    "; controls_created=" + (_panel.GetChildCount() > 0) +
                    "; in_tree=" + _panel.IsInsideTree() +
                    "; opt_in_show_on_start=" + showOnStart);
            }
            bool f8Down = Godot.Input.IsKeyPressed(Godot.Key.F8);
            if (f8Down && !_f8WasDown)
            {
                _panel?.Toggle();
                Godot.GD.Print("[Sts2ReferenceMod] INSPECTOR_PANEL_STATUS=F8_TOGGLED;" +
                    " visible=" + (_panel?.Visible.ToString() ?? "UNAVAILABLE"));
            }
            _f8WasDown = f8Down;
            _panel?.SetFidelityResult(_liveEvents?.LatestFidelityResult,
                _liveEvents?.Failure);
            _panel?.Tick();
            TryCaptureInitialNativeMap();

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
            JsonElement? frameState = null;
            if (_actionEnvelopes is not null && _adapter is not null)
            {
                // Reuse the single source frame snapshot for a provisional
                // player-turn candidate, keeping its chronology and cost
                // identical to the existing diagnostic frame observation.
                frameState = TryActionSnapshot("frame_state");
                if (frameState.HasValue)
                    _actionEnvelopes.ObserveFrame(frameState.Value);
            }
            // First idle playable frame in a turn, before any observed
            // player action. This is a candidate, not a certified queue
            // settlement: future native source hooks must prove that all
            // automatic/queued effects have finished.
            if (_actionEnvelopes is not null && _adapter is not null
                && combat.CurrentSide == CombatSide.Player
                && combat.Players[0].PlayerCombatState is { } playerState
                && playerState.TurnNumber > 0
                && !ObservedPlayerInputTurns.Contains(playerState.TurnNumber)
                && !ObservedPlayerStartTurns.Contains(playerState.TurnNumber)
                && frameState.HasValue
                && StableDecisionBlocker(combat) is null)
            {
                _actionEnvelopes.TurnBoundaryCandidate(
                    "PLAYER_TURN_START", playerState.TurnNumber,
                    frameState.Value, combat.CurrentSide.ToString(),
                    playerState.Phase.ToString(),
                    CaptureNativeQueueQuiescence(),
                    CaptureNativeAutomaticEffectEvidence(combat));
                ObservedPlayerStartTurns.Add(playerState.TurnNumber);
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
                try
                {
                    _agentFeed?.Publish(recorded);
                    var visible = _agentFeed?.Current();
                    if (visible is not null
                        && _liveEvents?.PublishPublicBoundary(visible) == false)
                        _agentFeed?.Withhold("LIVE_EVENT_JOURNAL_FAILED");
                }
                catch (Exception error)
                {
                    _agentFeed?.Withhold("OBSERVATION_PROJECTION_UNAVAILABLE");
                    Godot.GD.PushError("[Sts2ReferenceMod] AGENT_BRIDGE_STATUS=PROJECTION_FAILED;" +
                        " reason=" + error.GetType().Name);
                }
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
            _agentFeed?.Withhold("INCOMPLETE_CAPTURE");
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

    /// <summary>
    /// Passive source-pinned observation on the Godot frame thread. Neither
    /// task is awaited and no ActionQueueSet.GetReadyAction is called (that
    /// method can remove canceled queued actions). A failure to inspect the
    /// private deferred-request list results in missing evidence, not a
    /// claim that the list was empty.
    /// </summary>
    /// <summary>
    /// Read only the native CombatManager's explicit card/potion effect-body
    /// depth and phase flags. It is possible for detached delayed hooks to
    /// remain in flight when all three flags are true.
    /// </summary>
    private static NativeAutomaticEffectEvidence? CaptureNativeAutomaticEffectEvidence(
        CombatState combat)
    {
        try
        {
            if (combat.Players.Count != 1) return null;
            var manager = CombatManager.Instance;
            return new NativeAutomaticEffectEvidence(
                NoActiveCardOrPotionEffectBody:
                    !manager.IsExecutingCardOrPotionEffect(combat.Players[0]),
                NoPlayerTurnEndTransition:
                    !manager.EndingPlayerTurnPhaseOne &&
                    !manager.EndingPlayerTurnPhaseTwo,
                EnemyTurnNotStarted: !manager.IsEnemyTurnStarted);
        }
        catch (Exception error)
        {
            Godot.GD.PrintErr(
                "[Sts2ReferenceMod] AUTOMATIC_EFFECT_STATUS=UNAVAILABLE; reason=" +
                error.GetType().Name);
            return null;
        }
    }

    private static NativeQueueQuiescenceEvidence? CaptureNativeQueueQuiescence()
    {
        try
        {
            if (_executor is null || NativeDeferredPlayerActionsField is null)
                return null;
            var queue = RunManager.Instance.ActionQueueSet;
            var synchronizer = RunManager.Instance.ActionQueueSynchronizer;
            if (queue is null || synchronizer is null)
                return null;
            var deferred = NativeDeferredPlayerActionsField.GetValue(
                synchronizer) as System.Collections.ICollection;
            if (deferred is null) return null;
            return new NativeQueueQuiescenceEvidence(
                QueueIsEmpty: queue.IsEmpty,
                QueueCompletionSucceeded: queue.BecameEmpty().IsCompletedSuccessfully,
                ExecutorNotRunning: !_executor.IsRunning,
                ExecutorCompletionSucceeded: _executor.FinishedExecutingActions()
                    .IsCompletedSuccessfully,
                NoCurrentlyRunningAction: _executor.CurrentlyRunningAction is null,
                ExecutorNotPaused: !_executor.IsPaused,
                SynchronizerPlayPhase: synchronizer.CombatState ==
                    ActionSynchronizerCombatState.PlayPhase,
                NoDeferredPlayerRequests: deferred.Count == 0);
        }
        catch (Exception error)
        {
            Godot.GD.PrintErr(
                "[Sts2ReferenceMod] QUEUE_QUIESCENCE_STATUS=UNAVAILABLE; reason=" +
                error.GetType().Name);
            return null;
        }
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
        ObservedPlayerInputTurns.Clear();
        ObservedPlayerStartTurns.Clear();
        ObservedEnemyStartTurns.Clear();
        _lastCapturedGeneration = -1;
        _awaitingSettlement = false;
        _choiceInterrupted = false;
        _historyIndex = CombatManager.Instance.History.Entries.Count();
        _adapter = new NativeCombatAdapter(combat, () => _choiceInterrupted);
        string runId = Guid.NewGuid().ToString("N");
        _agentFeed?.OpenCombat(runId);
        if (_liveEvents?.OpenSession(runId) == false)
            _agentFeed?.Withhold("LIVE_EVENT_JOURNAL_FAILED");
        string root = System.Environment.GetEnvironmentVariable("STS2_FIDELITY_TRACE_DIR")
            ?? Path.Combine(OS.GetUserDataDir(), "native_fidelity_traces");
        Directory.CreateDirectory(root);
        _currentFile = Path.Combine(root, "passive-" +
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + runId + ".jsonl");
        // Source-v111 real run seed and ascension; unlike the old env-provided
        // STS2_FIDELITY_RUN_SEED this is taken directly from RunState.
        string actualSeed = combat.Players[0].RunState.Rng.StringSeed;
        int actualAscension = combat.Players[0].RunState.AscensionLevel;
        var header = new NativeTraceHeader(
            "header", NativeTraceVersion.Current, BuildId,
            typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unknown",
            null, runId,
            string.IsNullOrWhiteSpace(actualSeed) ? "UNVERIFIED" : actualSeed,
            DateTimeOffset.UtcNow, "passive", "v111_source_audited_local_validation_pending;"
                + "emulator_commit=UNVERIFIED;static_data=UNVERIFIED;single_player_combat");
        _recording = new NativeRecordingSession(_currentFile, header);
        // A separate append-only action timeline survives unsupported player
        // input semantics, and retains automatic actions before the first hand.
        // Link the independently captured source run prelude to precisely
        // this combat trace, then close the precombat journal. A run that
        // started before the Inspector attached has no such claim.
        if (_runPrelude is { } prelude)
        {
            try
            {
                var coord = _preludeRunState?.CurrentMapCoord;
                if (_preludeRunState is not null)
                    CapturePreludeRng("combat_attach", _preludeRunState);
                prelude.CombatLink(runId, _currentFile,
                    _preludeRunState?.CurrentActIndex ?? 0,
                    coord?.col, coord?.row);
                // RoomEntered fires after an awaited room.Enter. It may
                // arrive after combat frame polling has already begun.
                TryFinishRunPrelude();
            }
            catch (Exception error)
            {
                prelude.ObservationFailure("combat_link", error.GetType().Name);
            }
        }
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
                NativeV111RngHook.StreamCheckpointsEnabled,
            nativeRngFloatHookInstalled: NativeV111RngHook.HasFloatHook,
            nativeInputHookInstalled: NativeV111InputHooks.IsInstalled,
            nativeHandMenuHookInstalled: NativeV111InputHooks.HasHandMenuHook,
            nativeHandChoiceFlowHookInstalled:
                NativeV111InputHooks.HasHandChoiceFlowHook,
            nativeManualPlayHookInstalled: NativeV111InputHooks.HasManualPlayHook,
            nativeUiPlayBeginHookInstalled: NativeV111InputHooks.HasUiPlayBeginHook,
            nativeUiGateHookInstalled: NativeV111InputHooks.HasUiGateHook,
            eventSink: record => _liveEvents?.PublishFidelityRecord(record) ?? true);
        if (!string.IsNullOrWhiteSpace(actualSeed))
            _actionEnvelopes.NativeRunIdentity(actualSeed, actualAscension);
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
        _panel?.SetCaptureCapabilities(
            NativeV111RngHook.IsInstalled,
            NativeV111RngHook.HasFloatHook,
            _agentBridge is not null);
        CombatManager.Instance.History.Changed += OnHistoryChanged;
        CombatManager.Instance.AboutToSwitchToEnemyTurn += OnAboutToSwitchToEnemyTurn;
        CombatManager.Instance.TurnEnded += OnNativeTurnEnded;
        CombatManager.Instance.TurnStarted += OnNativeTurnStarted;
        AttachActionExecutor();
        var currentChoiceSync = RunManager.Instance.PlayerChoiceSynchronizer;
        if (!ReferenceEquals(_choiceSync, currentChoiceSync))
        {
            if (_choiceSync is not null)
                _choiceSync.PlayerChoiceReceived -= OnNativePlayerChoice;
            _choiceSync = currentChoiceSync;
            if (_choiceSync is not null)
                _choiceSync.PlayerChoiceReceived += OnNativePlayerChoice;
        }
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

    // Pinned v0.111 CombatManager invokes these events from the original
    // turn loop. Side switch is observed from TurnEnded (after CurrentSide
    // changes); TurnStarted can occur during a paused start-turn hook choice.
    // In that case the player phase is Start/AutoPrePlay, not Play.
    // No gameplay method, RNG provider or task is called by the observer.
    private static void OnAboutToSwitchToEnemyTurn(CombatState state) =>
        ObserveNativePhaseHandoff("ABOUT_TO_SWITCH_TO_ENEMY_TURN", state);

    private static void OnNativeTurnEnded(CombatState state) =>
        ObserveNativePhaseHandoff("SIDE_SWITCHED", state);

    private static void OnNativeTurnStarted(CombatState state) =>
        ObserveNativePhaseHandoff("TURN_STARTED", state);

    private static void ObserveNativePhaseHandoff(string sourceEvent, CombatState state)
    {
        if (_actionEnvelopes is null) return;
        try
        {
            if (!ReferenceEquals(state, _combat) || state.Players.Count != 1
                || state.Players[0].PlayerCombatState is not { } playerState)
            {
                _actionEnvelopes.ObservationFailure("native_phase_handoff",
                    "SOURCE_COMBAT_IDENTITY_UNAVAILABLE");
                return;
            }
            var synchronizer = RunManager.Instance.ActionQueueSynchronizer;
            if (synchronizer is null)
            {
                _actionEnvelopes.ObservationFailure("native_phase_handoff",
                    "SOURCE_SYNCHRONIZER_UNAVAILABLE");
                return;
            }
            var manager = CombatManager.Instance;
            _actionEnvelopes.NativePhaseHandoff(
                sourceEvent, playerState.TurnNumber,
                state.CurrentSide.ToString(), playerState.Phase.ToString(),
                synchronizer.CombatState.ToString(),
                manager.EndingPlayerTurnPhaseOne,
                manager.EndingPlayerTurnPhaseTwo,
                manager.IsEnemyTurnStarted);
        }
        catch (Exception error)
        {
            _actionEnvelopes.ObservationFailure("native_phase_handoff",
                error.GetType().Name);
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

    // UI-begin state is supplementary evidence. Failure to take this
    // extra snapshot is explicitly encoded as UNOBSERVED in the sidecar;
    // all existing strict executor and decision checks remain authoritative.
    private static JsonElement? TryOptionalUiBeginSnapshot()
    {
        try { return _adapter?.CaptureCanonicalState().Clone(); }
        catch (Exception error)
        {
            Godot.GD.PrintErr("[Sts2ReferenceMod] UI_BEGIN_STATE=UNOBSERVED; " +
                              "reason=" + error.GetType().Name);
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
        // A previous stable menu is no longer safe as soon as ANY executor
        // action (including automatic enemy or hook action) starts.
        _agentFeed?.Withhold("ACTION_RESOLVING");
        _liveEvents?.PublishPublicStatus("ACTION_RESOLVING");
        // This callback runs for every executor action, including monster and
        // automatic actions which have no matching human legal-menu item.
        // A distinct timeline protects their evidence from strict-trace gaps.
        var snapshot = TryActionSnapshot("before_action");
        action.BeforePausedForPlayerChoice -= OnNestedChoice;
        action.BeforePausedForPlayerChoice += OnNestedChoice;
        CanonicalAction? chosen = ProjectCanonicalAction(action);
        _actionEnvelopes?.Start(
            action, action.GetType().FullName ?? action.GetType().Name,
            snapshot, chosen);

        // Native automatic enemy-turn setup follows player cleanup in
        // observed source traces. Capture its pre-execution state without
        // claiming that this hook is an atomically settled enemy-side gate.
        if (action is ReadyToBeginEnemyTurnAction
            && _combat is { Players.Count: 1 } observedCombat
            && observedCombat.Players[0].PlayerCombatState is { } pcs
            && pcs.TurnNumber > 0
            && snapshot.HasValue
            && ObservedEnemyStartTurns.Add(pcs.TurnNumber))
        {
            _actionEnvelopes?.TurnBoundaryCandidate(
                "ENEMY_TURN_START", pcs.TurnNumber, snapshot.Value,
                observedCombat.CurrentSide.ToString(), pcs.Phase.ToString());
        }
        if (chosen is not null
            && _combat is { Players.Count: 1 } sourceCombat
            && sourceCombat.Players[0].PlayerCombatState is { } current
            && current.TurnNumber > 0)
        {
            ObservedPlayerInputTurns.Add(current.TurnNumber);
        }
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
            // Preserve the *last reported frame gate*, which is more useful
            // than querying StableDecisionBlocker inside an already-running
            // ActionExecutor callback. This is diagnostic only.
            _actionEnvelopes?.StrictInputGap(
                _lastGateReason, _decisionGeneration, _lastCapturedGeneration);
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
        _agentFeed?.Withhold("UNSUPPORTED_NESTED_CHOICE");
        _liveEvents?.PublishPublicStatus("UNSUPPORTED_NESTED_CHOICE");
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
        _agentFeed?.Withhold("STRICT_TRACE_INCOMPLETE");
        _liveEvents?.PublishPublicStatus("STRICT_TRACE_INCOMPLETE");
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
        // If the native room callback or a pending map request postfix was
        // never observed, preserve the incomplete run-prelude evidence.
        if (_runPrelude is { } prelude)
        {
            prelude.ObservationFailure("linked_combat_room_settlement",
                "FIRST_COMBAT_FINISHED_BEFORE_PRELUDE_SETTLED");
            prelude.Finish("First combat ended before native prelude completed");
            _runPrelude = null;
            _preludeRunState = null;
            _ = FinishRunPreludeAsync(prelude);
        }
        _agentFeed?.EndCombat(reason);
        _liveEvents?.PublishPublicStatus("COMBAT_COMPLETE");
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
        if (_choiceSync is not null)
        {
            _choiceSync.PlayerChoiceReceived -= OnNativePlayerChoice;
            _choiceSync = null;
        }
        CombatManager.Instance.History.Changed -= OnHistoryChanged;
        CombatManager.Instance.AboutToSwitchToEnemyTurn -= OnAboutToSwitchToEnemyTurn;
        CombatManager.Instance.TurnEnded -= OnNativeTurnEnded;
        CombatManager.Instance.TurnStarted -= OnNativeTurnStarted;
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
