using System.Text.Json;
using Sts2Emulator.Trace;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Source-only run-start to first-combat chronology. Captures native run
/// identity, visited room coordinates, precombat queue request types/outcomes
/// and the first combat trace link without executing commands or RNG.
///
/// Event-choice observations are pre-execution synchronous offers/requests;
/// option effects, other precombat choices, and RNG remain unverified.
/// </summary>
public sealed class NativeRunPreludeRecorder : IAsyncDisposable
{
    public const string Schema = "native-run-prelude-v1";
    private readonly AsyncTraceWriter _writer;
    private readonly Dictionary<object, (long Id, bool Opaque)> _requests =
        new(ReferenceEqualityComparer.Instance);
    private long _ordinal;
    private long _nextRequestId;
    private long _nextEventRequestId;
    private readonly HashSet<(ulong Owner, uint ChoiceId)> _precombatChoices = [];
    private readonly Dictionary<object, long> _eventRequests =
        new(ReferenceEqualityComparer.Instance);
    private bool _closed;
    private bool _incomplete;
    private bool _linked;
    private int _linkedActIndex;
    private int? _linkedMapColumn;
    private int? _linkedMapRow;
    private readonly HashSet<(int ActIndex, int? Column, int? Row)>
        _sourceRoomArrivals = [];

    public string RunId { get; }
    /// <summary>
    /// Source room callbacks can occur after combat initialization has begun.
    /// Do not close before the linked room and pending map postfixes arrive.
    /// </summary>
    public bool HasLinkedCombatRoomAndSettledMapRequests =>
        _linked
        && _sourceRoomArrivals.Contains((
            _linkedActIndex, _linkedMapColumn, _linkedMapRow))
        && _requests.Count == 0
        && _eventRequests.Count == 0;
    public bool IsIncomplete => _incomplete;

    public NativeRunPreludeRecorder(
        string path, string gameBuild, string runId, string seed,
        int ascension, string characterId, string firstActId,
        int playerCount, int queueCapacity = 4096)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameBuild);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstActId);
        ArgumentOutOfRangeException.ThrowIfNegative(ascension);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerCount);
        RunId = runId;
        _writer = new AsyncTraceWriter(path, queueCapacity);
        Write("run_started", new
        {
            schema = Schema, game_build = gameBuild, run_id = runId,
            seed, ascension, character_id = characterId,
            first_act_id = firstActId,
            player_count = playerCount,
            source = "RunManager.RunStarted(RunState)",
            source_seed = "RunState.Rng.StringSeed",
            source_character = "RunState.Players[0].Character.Id",
            source_ascension = "RunState.AscensionLevel",
            source_act = "RunState.Act.Id",
            source_rng_consumed_by_inspector = false,
            source_macro_choices_complete = false,
            emulator_bootstrap = "NOT_TESTED"
        });
    }

    /// <summary>
    /// Read-only snapshots from native RunRngSet.ToSerializable and
    /// PlayerRngSet.ToSerializable. These expose counters/words at three
    /// observed sites, not all RNG calls or a verified emulator alignment.
    /// </summary>
    public void RunRngSnapshot(
        string site, ulong playerSeed,
        IReadOnlyList<NativeRunRngStreamView> streams)
    {
        if (_closed) return;
        if (site is not ("run_started" or "room_entered" or "combat_attach")
            || streams.Count == 0
            || streams.Any(s => s.Scope is not ("run" or "player")
                || string.IsNullOrWhiteSpace(s.StreamId) || s.Counter < 0)
            || streams.Select(s => (s.Scope, s.StreamId)).Distinct().Count()
                != streams.Count
            || !streams.Any(s => s.Scope == "run")
            || !streams.Any(s => s.Scope == "player"))
        {
            ObservationFailure("run_rng_snapshot", "INVALID_SOURCE_RNG_PROJECTION");
            return;
        }
        Write("run_rng_snapshot", new
        {
            site,
            player_rng_seed = playerSeed,
            streams = streams.OrderBy(s => s.Scope, StringComparer.Ordinal)
                .ThenBy(s => s.StreamId, StringComparer.Ordinal)
                .Select(s => new
                {
                    scope = s.Scope,
                    stream_id = s.StreamId,
                    counter = s.Counter,
                    s0 = s.S0, s1 = s.S1, s2 = s.S2, s3 = s.S3
                }).ToArray(),
            source = "RunRngSet.ToSerializable+PlayerRngSet.ToSerializable",
            source_rng_consumed_by_inspector = false,
            native_rng_precombat_history_complete = false,
            emulator_rng_alignment = "NOT_TESTED"
        });
    }

    public void RoomEntered(
        int actIndex, string actId, int actFloor, int totalFloor,
        int? mapColumn, int? mapRow, string roomType,
        string? roomModelId, int playerHp, int playerGold)
    {
        if (_closed) return;
        if (actIndex < 0 || actFloor < 0 || totalFloor < 0
            || (mapColumn.HasValue != mapRow.HasValue)
            || mapColumn < 0 || mapRow < 0
            || playerHp < 0 || playerGold < 0
            || string.IsNullOrWhiteSpace(actId)
            || string.IsNullOrWhiteSpace(roomType))
        {
            ObservationFailure("room_entered", "INVALID_NATIVE_ROOM_PROJECTION");
            return;
        }
        _sourceRoomArrivals.Add((actIndex, mapColumn, mapRow));
        Write("room_entered", new
        {
            act_index = actIndex, act_id = actId,
            act_floor = actFloor, total_floor = totalFloor,
            map_column = mapColumn, map_row = mapRow,
            room_type = roomType, room_model_id = roomModelId,
            player_hp = playerHp, player_gold = playerGold,
            source = "RunManager.RoomEntered",
            visited_destination = mapColumn.HasValue
                ? "SOURCE_CURRENT_MAP_COORD" : "MAP_COORD_UNASSIGNED",
            command_provenance = "UNVERIFIED",
            emulator_parity = "NOT_TESTED"
        });
    }

    public void MapActionRequested(
        object action, string kind, int? sourceAct, int? sourceColumn,
        int? sourceRow, int? targetColumn, int? targetRow,
        int? mapGeneration)
    {
        if (_closed) return;
        if (kind is not ("map_vote" or "map_move")
            || (sourceColumn.HasValue != sourceRow.HasValue)
            || (targetColumn.HasValue != targetRow.HasValue)
            || targetColumn < 0 || targetRow < 0
            || sourceColumn < 0 || sourceRow < 0
            || sourceAct < 0 || mapGeneration < 0
            || (kind == "map_move" && !targetColumn.HasValue))
        {
            ObservationFailure("map_request", "INVALID_NATIVE_MAP_ACTION");
            return;
        }
        if (_requests.ContainsKey(action))
        {
            ObservationFailure("map_request", "DUPLICATE_NATIVE_ACTION_OBJECT");
            return;
        }
        long id = ++_nextRequestId;
        _requests[action] = (id, false);
        Write("map_action_request", new
        {
            request_id = id, kind,
            source_act = sourceAct,
            source_column = sourceColumn, source_row = sourceRow,
            target_column = targetColumn, target_row = targetRow,
            map_generation = mapGeneration,
            source = "ActionQueueSynchronizer.RequestEnqueue.prefix",
            action_identity = "SAME_NATIVE_GAME_ACTION_OBJECT",
            queue_acceptance = "UNVERIFIED",
            human_origin = "UNVERIFIED",
            emulator_action = "NOT_MAPPED"
        });
    }

    /// <summary>
    /// Record every other precombat queue request by CLR type only. This
    /// deliberately avoids ToNetAction, choice inference and extra RNG calls.
    /// </summary>
    public void OpaqueActionRequested(object action, string nativeActionType)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(nativeActionType)
            || _requests.ContainsKey(action))
        {
            ObservationFailure("opaque_action_request",
                "INVALID_OR_DUPLICATE_NATIVE_ACTION");
            return;
        }
        long id = ++_nextRequestId;
        _requests[action] = (id, true);
        Write("opaque_action_request", new
        {
            request_id = id, native_action_type = nativeActionType,
            source = "ActionQueueSynchronizer.RequestEnqueue.prefix",
            action_identity = "SAME_NATIVE_GAME_ACTION_OBJECT",
            queue_acceptance = "UNVERIFIED",
            human_origin = "UNVERIFIED",
            emulator_action = "NOT_MAPPED"
        });
    }

    public void MapActionOutcome(object action, string nativeActionState,
                                 uint? nativeActionId)
    {
        if (_closed || !_requests.Remove(action, out var pending)) return;
        if (pending.Opaque)
        {
            Write("opaque_action_outcome", new
            {
                request_id = pending.Id, native_action_state = nativeActionState,
                native_action_id = nativeActionId,
                source = "ActionQueueSynchronizer.RequestEnqueue.postfix",
                native_executor_completion = "NOT_OBSERVED"
            });
            return;
        }
        Write("map_action_outcome", new
        {
            request_id = pending.Id, native_action_state = nativeActionState,
            native_action_id = nativeActionId,
            source = "ActionQueueSynchronizer.RequestEnqueue.postfix",
            native_executor_completion = "NOT_OBSERVED",
            visited_room_proves_this_request = false
        });
    }

    /// <summary>
    /// A read-only copy of EventSynchronizer's exact local options at its
    /// ChooseLocalOption entry. The native method may begin async effects;
    /// even a successful synchronous return does not certify completion.
    /// </summary>
    public void EventChoiceRequested(
        object token, string eventModelId, ulong ownerId,
        int selectedIndex, IReadOnlyList<NativeEventOptionView> options)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(eventModelId)
            || selectedIndex < 0 || selectedIndex >= options.Count
            || options.Count == 0
            || options.Any(o => string.IsNullOrWhiteSpace(o.TextKey))
            || _eventRequests.ContainsKey(token))
        {
            ObservationFailure("event_choice_request",
                "INVALID_SOURCE_EVENT_SELECTION");
            return;
        }
        long id = ++_nextEventRequestId;
        _eventRequests[token] = id;
        Write("event_choice_request", new
        {
            request_id = id, event_model_id = eventModelId,
            owner_id = ownerId, selected_index = selectedIndex,
            selected_text_key = options[selectedIndex].TextKey,
            options = options.Select((o, index) => new
            {
                index, text_key = o.TextKey,
                is_locked = o.IsLocked, is_proceed = o.IsProceed,
                relic_model_id = o.RelicModelId
            }).ToArray(),
            source = "EventSynchronizer.ChooseLocalOption.prefix",
            selection_origin = "LOCAL_SYNCHRONIZER_CALL",
            option_execution = "NOT_OBSERVED",
            effects_settled = false
        });
    }

    public void EventChoiceReturned(object token)
    {
        if (_closed || !_eventRequests.Remove(token, out long id)) return;
        Write("event_choice_return", new
        {
            request_id = id,
            source = "EventSynchronizer.ChooseLocalOption.postfix",
            handler_returned = true,
            option_effects_settled = false
        });
    }

    /// <summary>
    /// A source-synchronized precombat result, not an offered menu. The
    /// enclosing run is singleplayer; IDs are the original native values.
    /// Mutable-card contents and all post-choice effects remain unobserved.
    /// </summary>
    public void PrecombatPlayerChoice(
        ulong ownerId, uint choiceId, string choiceType,
        IReadOnlyList<long>? combatCardIds, IReadOnlyList<long>? deckIndices,
        IReadOnlyList<string>? canonicalCardIds, IReadOnlyList<int>? indexes,
        ulong? selectedPlayerId, int? mutableCardCount, ulong? mutableOwnerId)
    {
        if (_closed) return;
        bool typeMatches = choiceType switch
        {
            "CombatCard" => combatCardIds is not null,
            "DeckCard" => deckIndices is not null,
            "CanonicalCard" => canonicalCardIds is not null,
            "Index" => indexes is not null,
            "Player" => true,
            "MutableCard" => mutableCardCount is >= 0,
            _ => false
        };
        if (!typeMatches
            || (combatCardIds is not null && combatCardIds.Any(i => i < 0))
            || (deckIndices is not null && deckIndices.Any(i => i < 0))
            || (canonicalCardIds is not null
                && canonicalCardIds.Any(string.IsNullOrWhiteSpace))
            || (indexes is not null && indexes.Any(i => i < 0))
            || !_precombatChoices.Add((ownerId, choiceId)))
        {
            ObservationFailure("precombat_player_choice",
                "INVALID_OR_DUPLICATE_SYNCHRONIZED_CHOICE");
            return;
        }
        Write("player_choice_result", new
        {
            owner_id = ownerId, choice_id = choiceId, choice_type = choiceType,
            combat_card_ids = combatCardIds, deck_indices = deckIndices,
            canonical_card_ids = canonicalCardIds, indexes,
            selected_player_id = selectedPlayerId,
            mutable_card_count = mutableCardCount,
            mutable_owner_id = mutableOwnerId,
            source = "PlayerChoiceSynchronizer.PlayerChoiceReceived",
            synchronized_result_observed = true,
            offered_options = "NOT_CAPTURED",
            effects_settled = false,
            emulator_replay = "NOT_EXECUTED"
        });
    }

    public void CombatLink(string combatRunId, string combatTraceFile,
                           int actIndex, int? mapColumn, int? mapRow)
    {
        if (_closed) return;
        if (_linked || string.IsNullOrWhiteSpace(combatRunId)
            || string.IsNullOrWhiteSpace(combatTraceFile)
            || actIndex < 0
            || (mapColumn.HasValue != mapRow.HasValue))
        {
            ObservationFailure("combat_link", "INVALID_COMBAT_LINK");
            return;
        }
        _linked = true;
        _linkedActIndex = actIndex;
        _linkedMapColumn = mapColumn;
        _linkedMapRow = mapRow;
        Write("combat_link", new
        {
            combat_run_id = combatRunId,
            combat_trace_file = Path.GetFileName(combatTraceFile),
            act_index = actIndex,
            map_column = mapColumn, map_row = mapRow,
            source = "NativeV111PassiveBridge.OpenCombat",
            combat_initial_rng_alignment = "NOT_TESTED",
            full_macro_action_transcript = "NOT_CAPTURED"
        });
    }

    public void ObservationFailure(string stage, string reason)
    {
        if (_closed) return;
        _incomplete = true;
        Write("observation_failure", new { stage, reason });
    }

    public void Finish(string reason)
    {
        if (_closed) return;
        Write("terminal", new
        {
            status = _incomplete ||
                (_linked && !HasLinkedCombatRoomAndSettledMapRequests)
                ? "INCOMPLETE_CAPTURE"
                : _linked ? "SOURCE_PRELUDE_OBSERVED" : "NO_COMBAT_LINK",
            reason, combat_link_recorded = _linked,
            linked_combat_room_arrival_observed =
                _linked && _sourceRoomArrivals.Contains((
                    _linkedActIndex, _linkedMapColumn, _linkedMapRow)),
            map_requests_missing_postfix = _requests.Values.Count(p => !p.Opaque),
            opaque_requests_missing_postfix = _requests.Values.Count(p => p.Opaque),
            event_choice_requests_missing_postfix = _eventRequests.Count,
            source_macro_choices_complete = false,
            native_rng_parity = "NOT_TESTED",
            emulator_bootstrap = "NOT_TESTED"
        });
        _closed = true;
    }

    private void Write<T>(string type, T data)
    {
        if (!_writer.TryEnqueue(new NativeRunPreludeRecord(
            type, ++_ordinal, RunId, JsonSerializer.SerializeToElement(data))))
            _incomplete = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed) Finish("Recorder closed before first combat");
        await _writer.CompleteAsync().ConfigureAwait(false);
    }
}

public sealed record NativeRunRngStreamView(
    string Scope, string StreamId, int Counter,
    ulong S0, ulong S1, ulong S2, ulong S3);

public sealed record NativeRunPreludeRecord(
    string Type, long Ordinal, string RunId, JsonElement Data);

// The source EventModel.CurrentOptions projection is read-only. No option
// callbacks or localization resolvers are invoked by the observer.
public sealed record NativeEventOptionView(
    string TextKey, bool IsLocked, bool IsProceed, string? RelicModelId);
