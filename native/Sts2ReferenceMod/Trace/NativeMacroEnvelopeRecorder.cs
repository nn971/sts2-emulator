using System.Text.Json;
using Sts2Emulator.Trace;

namespace Sts2ReferenceMod.Trace;

/// <summary>
/// Versioned, source-only macro chronology independent of the precombat
/// prelude and the per-encounter combat sidecars. Native callbacks remain
/// human-operated; this recorder never requests an action or consumes RNG.
/// </summary>
public sealed class NativeMacroEnvelopeRecorder : IAsyncDisposable
{
    public const string Schema = "native-macro-envelope-v1";
    private readonly AsyncTraceWriter _writer;
    private readonly Dictionary<object, long> _requests =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, long> _eventRequests =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, (long OfferId, int Index)> _rewards =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<int> _capturedMapActs = [];
    private long _ordinal;
    private long _nextRequestId;
    private long _nextEventRequestId;
    private long _nextRewardOfferId;
    private bool _closed;
    private bool _incomplete;
    public string RunId { get; }

    public NativeMacroEnvelopeRecorder(
        string path, string gameBuild, string runId, string seed,
        int ascension, string character, int queueCapacity = 8192)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameBuild);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        RunId = runId;
        _writer = new AsyncTraceWriter(path, queueCapacity);
        Write("header", new
        {
            schema = Schema, game_build = gameBuild, run_id = runId,
            seed, ascension, character,
            started_at_utc = DateTimeOffset.UtcNow,
            source = "RunManager.RunStarted",
            observation_only = true, native_gameplay_mutated = false,
            native_rng_consumed_by_inspector = false,
            native_emulator_parity = "NOT_TESTED"
        });
    }

    public bool NeedsMap(int actIndex) =>
        !_closed && !_capturedMapActs.Contains(actIndex);

    public void MapSnapshot(int actIndex, string actId,
        IReadOnlyList<NativeMapPointView> points, NativeMapRngView rng)
    {
        if (!NeedsMap(actIndex)) return;
        if (actIndex < 0 || string.IsNullOrWhiteSpace(actId)
            || points.Count == 0 || rng.Counter < 0
            || points.Select(p => (p.Column, p.Row)).Distinct().Count()
                != points.Count)
        {
            Failure("map_snapshot", "INVALID_SOURCE_MAP");
            return;
        }
        var coordinates = points.Select(p => (p.Column, p.Row)).ToHashSet();
        if (points.Any(p => p.Children.Any(c =>
                !coordinates.Contains((c.Column, c.Row)))))
        {
            Failure("map_snapshot", "SOURCE_EDGE_TARGET_MISSING");
            return;
        }
        Write("map_snapshot", new
        {
            act_index = actIndex, act_id = actId,
            points = points.Select(p => new {
                column = p.Column, row = p.Row, point_type = p.PointType,
                can_be_modified = p.CanBeModified,
                children = p.Children.Select(c => new {
                    column = c.Column, row = c.Row
                }).ToArray()
            }).ToArray(),
            map_rng = new {
                counter = rng.Counter, s0 = rng.S0, s1 = rng.S1,
                s2 = rng.S2, s3 = rng.S3
            },
            source = "RunState.Map; StandardActMap._rng.ToSerializable",
            legal_destination_basis = "SOURCE_MAP_EDGES",
            map_ui_eligibility = "NOT_VERIFIED",
            emulator_parity = "NOT_TESTED"
        });
        _capturedMapActs.Add(actIndex);
    }

    public void RoomEntered(int actIndex, int floor, int? column, int? row,
        string roomType, string? modelId, int hp, int gold,
        IReadOnlyList<NativeMapCoordView> outgoing)
    {
        if (_closed) return;
        if (actIndex < 0 || floor < 0 || hp < 0 || gold < 0
            || column.HasValue != row.HasValue || column < 0 || row < 0
            || string.IsNullOrWhiteSpace(roomType))
        {
            Failure("room_entered", "INVALID_ROOM_STATE");
            return;
        }
        Write("room_entered", new {
            act_index = actIndex, total_floor = floor,
            column, row, room_type = roomType, room_model_id = modelId,
            player_hp = hp, player_gold = gold,
            outgoing_map_edges = outgoing.Select(c => new {
                column = c.Column, row = c.Row
            }).ToArray(),
            source = "RunManager.RoomEntered",
            legal_destination_basis = "SOURCE_MAP_EDGES_ONLY",
            actual_map_screen_actions = "NOT_CAPTURED",
            emulator_parity = "NOT_TESTED"
        });
    }

    public void MapRequest(object action, string kind, int? fromColumn,
        int? fromRow, int? toColumn, int? toRow)
    {
        if (_closed) return;
        if (kind is not ("map_move" or "map_vote")
            || (toColumn.HasValue != toRow.HasValue)
            || (fromColumn.HasValue != fromRow.HasValue)
            || toColumn < 0 || toRow < 0 || fromColumn < 0 || fromRow < 0
            || (kind == "map_move" && !toColumn.HasValue)
            || _requests.ContainsKey(action))
        {
            Failure("map_request", "INVALID_OR_DUPLICATE_NATIVE_REQUEST");
            return;
        }
        long id = ++_nextRequestId;
        _requests.Add(action, id);
        Write("map_request", new {
            request_id = id, kind,
            from_column = fromColumn, from_row = fromRow,
            to_column = toColumn, to_row = toRow,
            source = "ActionQueueSynchronizer.RequestEnqueue.prefix",
            object_identity = "SOURCE_NATIVE_ACTION_OBJECT",
            queue_acceptance = "UNVERIFIED",
            actual_legal_menu = "NOT_CAPTURED"
        });
    }

    public void MapOutcome(object action, string state, uint? nativeActionId)
    {
        if (_closed || !_requests.Remove(action, out long id)) return;
        Write("map_request_outcome", new {
            request_id = id, native_action_state = state,
            native_action_id = nativeActionId,
            source = "ActionQueueSynchronizer.RequestEnqueue.postfix",
            execution_completed = false,
            emulator_parity = "NOT_TESTED"
        });
    }

    public void ShopSnapshot(int actIndex, int floor, int gold,
        IReadOnlyList<NativeShopOfferView> entries, string cause)
    {
        if (_closed) return;
        if (actIndex < 0 || floor < 0 || gold < 0
            || entries.Any(e => e.Slot < 0 || e.BaseCost < 0
                || string.IsNullOrWhiteSpace(e.Kind)))
        {
            Failure("shop_snapshot", "INVALID_SHOP_PROJECTION");
            return;
        }
        Write("shop_snapshot", new {
            act_index = actIndex, total_floor = floor, player_gold = gold,
            cause,
            offers = entries.Select(e => new {
                slot = e.Slot, kind = e.Kind, model_id = e.ModelId,
                is_stocked = e.IsStocked, base_cost = e.BaseCost,
                on_sale = e.OnSale, removal_used = e.RemovalUsed,
                displayed_price = (int?)null,
                displayed_price_coverage = "NOT_CAPTURED_FROM_NATIVE_UI",
                purchase_eligible = "NOT_VERIFIED"
            }).ToArray(),
            source = "MerchantRoom.Inventories; MerchantEntry._cost",
            price_modifiers_evaluated_by_inspector = false,
            native_rng_consumed_by_inspector = false,
            emulator_parity = "NOT_TESTED"
        });
    }

    public void RewardOffer(IReadOnlyList<NativeRewardOfferView> rewards,
        int setId, ulong playerId, bool canSkip)
    {
        if (_closed) return;
        if (setId < 0 || rewards.Any(r => string.IsNullOrWhiteSpace(r.Type)))
        {
            Failure("reward_offer", "INVALID_REWARD_SET");
            return;
        }
        long id = ++_nextRewardOfferId;
        for (int i = 0; i < rewards.Count; i++)
            _rewards[rewards[i].SourceReward] = (id, i);
        Write("reward_offer", new {
            offer_id = id, native_set_id = setId, player_id = playerId,
            can_skip = canSkip,
            rewards = rewards.Select((r, i) => new {
                index = i, native_type = r.Type, populated = r.Populated,
                model_ids = r.ModelIds, gold_amount = r.GoldAmount,
                content_coverage = r.ContentCoverage
            }).ToArray(),
            source = "RewardsSetSynchronizer.BeginRewardsSet.postfix",
            native_choice_contents_complete = false,
            emulator_parity = "NOT_TESTED"
        });
    }

    public void RewardSelected(object reward)
    {
        if (_closed) return;
        bool linked = _rewards.TryGetValue(reward, out var offer);
        Write("reward_selection_requested", new {
            offer_id = linked ? (long?)offer.OfferId : null,
            selected_index = linked ? (int?)offer.Index : null,
            source = "RewardsSetSynchronizer.SelectLocalReward.prefix",
            choice_link = linked ? "SAME_NATIVE_REWARD_OBJECT" : "UNVERIFIED",
            effect_completed = false
        });
    }

    public void RewardsSkipped()
    {
        if (_closed) return;
        Write("reward_skip_requested", new {
            source = "RewardsSetSynchronizer.SkipLocalRewardsSet.prefix",
            skipped_set_identity = "UNVERIFIED",
            effect_completed = false
        });
    }

    public void EventChoiceRequested(object token, string eventId,
        int selectedIndex, IReadOnlyList<NativeEventOptionView> options)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(eventId) || selectedIndex < 0
            || selectedIndex >= options.Count || _eventRequests.ContainsKey(token))
        {
            Failure("event_choice", "INVALID_EVENT_REQUEST");
            return;
        }
        long id = ++_nextEventRequestId;
        _eventRequests.Add(token, id);
        Write("event_choice_request", new {
            request_id = id, event_model_id = eventId, selected_index = selectedIndex,
            options = options.Select((o, i) => new {
                index = i, text_key = o.TextKey, is_locked = o.IsLocked,
                is_proceed = o.IsProceed, relic_model_id = o.RelicModelId
            }).ToArray(),
            source = "EventSynchronizer.ChooseLocalOption.prefix",
            source_menu_coverage = "SOURCE_CURRENT_OPTIONS",
            effect_completed = false
        });
    }

    public void EventChoiceReturned(object token)
    {
        if (_closed || !_eventRequests.Remove(token, out long id)) return;
        Write("event_choice_return", new {
            request_id = id,
            source = "EventSynchronizer.ChooseLocalOption.postfix",
            effect_completed = false
        });
    }

    public void CombatLinked(string combatRunId)
    {
        if (_closed) return;
        Write("combat_link", new {
            combat_run_id = combatRunId,
            source = "NativeV111PassiveBridge.OpenCombat",
            emulator_parity = "NOT_TESTED"
        });
    }

    public void Failure(string stage, string reason)
    {
        if (_closed) return;
        _incomplete = true;
        Write("observation_failure", new { stage, reason });
    }

    public void Finish(string reason)
    {
        if (_closed) return;
        Write("terminal", new {
            reason, status = _incomplete ? "INCOMPLETE_CAPTURE" : "SOURCE_OBSERVED",
            open_map_requests = _requests.Count,
            open_event_requests = _eventRequests.Count,
            native_game_parity = "NOT_TESTED",
            whole_run_certified = false
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
        if (!_closed) Finish("Macro recorder disposed");
        await _writer.CompleteAsync().ConfigureAwait(false);
    }
}

public sealed record NativeShopOfferView(int Slot, string Kind, string? ModelId,
    bool IsStocked, int BaseCost, bool OnSale, bool RemovalUsed);
public sealed record NativeRewardOfferView(object SourceReward, string Type,
    bool Populated);
