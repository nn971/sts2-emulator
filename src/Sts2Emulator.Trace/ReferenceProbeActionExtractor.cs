using System.Text.Json;

namespace Sts2Emulator.Trace;

public sealed record ReferenceProbeSequenceRef(
    long Sequence,
    string Boundary,
    string StateHash,
    string Evidence);

public sealed record ReferenceProbeActionEvidence(
    long ActionIndex,
    ReferenceNormalizedAction Action,
    long ObservedSequence,
    ReferenceProbeSequenceRef? CandidateBefore,
    ReferenceProbeSequenceRef? LifecycleFinish,
    ReferenceProbeSequenceRef? CandidateAfter,
    string[] Diagnostics);

public sealed record ReferenceProbeActionReport(
    int CardPlayCount,
    int PotionUseCount,
    int EndTurnCount,
    int ActionsWithCandidateBefore,
    int ActionsWithCandidateAfter,
    IReadOnlyList<ReferenceProbeActionEvidence> Actions);

public static class ReferenceProbeActionExtractor
{
    public static ReferenceProbeActionReport Analyze(string path)
    {
        var boundaries = ReadBoundaries(path);
        var actions = new List<ReferenceProbeActionEvidence>();
        var actionIndex = 0L;

        for (var index = 0; index < boundaries.Count; index++)
        {
            var boundary = boundaries[index];
            var historyType = HistoryType(boundary.HistoryEntry);

            if (historyType.EndsWith(
                    ".CardPlayStartedEntry",
                    StringComparison.Ordinal))
            {
                actions.Add(BuildCardPlayEvidence(
                    ++actionIndex,
                    boundaries,
                    index));
                continue;
            }

            if (historyType.EndsWith(
                    ".PotionUsedEntry",
                    StringComparison.Ordinal))
            {
                actions.Add(BuildPotionEvidence(
                    ++actionIndex,
                    boundary));
                continue;
            }

            if (StringComparer.Ordinal.Equals(
                    boundary.Boundary,
                    "combat_manager.PlayerEndedTurn"))
            {
                actions.Add(BuildEndTurnEvidence(
                    ++actionIndex,
                    boundaries,
                    index));
            }
        }

        return new ReferenceProbeActionReport(
            actions.Count(action => action.Action.Kind == "play_card"),
            actions.Count(action => action.Action.Kind == "use_potion"),
            actions.Count(action => action.Action.Kind == "end_turn"),
            actions.Count(action => action.CandidateBefore is not null),
            actions.Count(action => action.CandidateAfter is not null),
            actions);
    }

    private static ReferenceProbeActionEvidence BuildCardPlayEvidence(
        long actionIndex,
        IReadOnlyList<ProbeBoundary> boundaries,
        int startIndex)
    {
        var start = boundaries[startIndex];
        var cardPlay = GetObject(start.HistoryEntry, "card_play");
        var card = GetObject(cardPlay, "card");

        var cardId = CardIdFromPlayPile(start.State);
        var cardRuntimeType =
            GetString(card, "runtime_type")
            ?? CardRuntimeTypeFromNested(card)
            ?? CardRuntimeTypeFromPlayPile(start.State);
        var title = GetString(card, "title");
        var target = GetInt64(GetObject(cardPlay, "target"), "combat_id");
        var energySpent = GetInt64(
            GetObject(cardPlay, "resources"),
            "energy_spent");
        var upgraded = CardUpgradeFromPlayPile(start.State);

        var handIndexCandidates = FindRemovedHandIndices(
            startIndex > 0 ? boundaries[startIndex - 1].State : null,
            start.State);

        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["card_id"] = cardId,
            ["native_card_runtime_type"] = cardRuntimeType,
            ["title"] = title,
            ["target_combat_id"] = target,
            ["energy_spent"] = energySpent,
            ["is_auto_play"] = GetBool(cardPlay, "is_auto_play"),
            ["play_index"] = GetInt64(cardPlay, "play_index"),
            ["play_count"] = GetInt64(cardPlay, "play_count"),
            ["is_upgraded"] = upgraded,
            ["equivalent_hand_indices"] = handIndexCandidates
        });

        var diagnostics = new List<string>();

        var before = FindCardBefore(
            boundaries,
            startIndex,
            energySpent,
            diagnostics);

        var finishIndex = FindMatchingCardFinish(
            boundaries,
            startIndex,
            title,
            target,
            GetBool(cardPlay, "is_auto_play"),
            GetInt64(cardPlay, "play_index"),
            GetInt64(cardPlay, "play_count"));

        ReferenceProbeSequenceRef? finish = null;
        ReferenceProbeSequenceRef? after = null;
        if (finishIndex is not null)
        {
            var finishBoundary = boundaries[finishIndex.Value];
            finish = ToRef(
                finishBoundary,
                "card-play-finished lifecycle marker");
            after = FindSettledPlayerStateAfterCard(
                boundaries,
                finishIndex.Value,
                diagnostics);
        }
        else
        {
            diagnostics.Add("card_play_finish_missing");
        }

        if (handIndexCandidates.Length == 0)
        {
            diagnostics.Add("played_card_hand_index_unresolved");
        }
        else if (handIndexCandidates.Length > 1)
        {
            diagnostics.Add("played_card_hand_index_semantically_ambiguous");
        }

        return new ReferenceProbeActionEvidence(
            actionIndex,
            new ReferenceNormalizedAction("play_card", payload),
            start.Sequence,
            before,
            finish,
            after,
            diagnostics.ToArray());
    }

    private static ReferenceProbeActionEvidence BuildPotionEvidence(
        long actionIndex,
        ProbeBoundary boundary)
    {
        var potion = GetObject(boundary.HistoryEntry, "potion");
        var target = GetObject(boundary.HistoryEntry, "target");

        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["potion_id"] = GetString(GetObject(potion, "id"), "entry"),
            ["target_combat_id"] = GetInt64(target, "combat_id")
        });

        return new ReferenceProbeActionEvidence(
            actionIndex,
            new ReferenceNormalizedAction("use_potion", payload),
            boundary.Sequence,
            CandidateBefore: null,
            LifecycleFinish: ToRef(
                boundary,
                "PotionUsedEntry is an observed potion lifecycle marker"),
            CandidateAfter: ToRef(
                boundary,
                "post-effect player-state candidate; validate against a potion-start signal"),
            Diagnostics:
            [
                "potion_action_start_not_observed",
                "potion_effects_may_precede_potion_used_entry"
            ]);
    }

    private static ReferenceProbeActionEvidence BuildEndTurnEvidence(
        long actionIndex,
        IReadOnlyList<ProbeBoundary> boundaries,
        int index)
    {
        var boundary = boundaries[index];
        var payload = JsonSerializer.SerializeToElement(
            new Dictionary<string, object?>());

        var after = FindNextPlayerTurn(boundaries, index);
        var diagnostics = after is null
            ? new[] { "next_player_turn_not_observed" }
            : Array.Empty<string>();

        return new ReferenceProbeActionEvidence(
            actionIndex,
            new ReferenceNormalizedAction("end_turn", payload),
            boundary.Sequence,
            ToRef(
                boundary,
                "PlayerEndedTurn snapshot is settled before hand/side transition"),
            LifecycleFinish: null,
            CandidateAfter: after,
            diagnostics);
    }

    private static ReferenceProbeSequenceRef? FindCardBefore(
        IReadOnlyList<ProbeBoundary> boundaries,
        int startIndex,
        long? energySpent,
        List<string> diagnostics)
    {
        if (startIndex <= 0)
        {
            diagnostics.Add("card_play_has_no_previous_boundary");
            return null;
        }

        var previous = boundaries[startIndex - 1];
        var previousHistoryType = HistoryType(previous.HistoryEntry);

        if (energySpent is > 0
            && previousHistoryType.EndsWith(
                ".EnergySpentEntry",
                StringComparison.Ordinal)
            && GetInt64(previous.HistoryEntry, "amount") == energySpent
            && IsPlayerReadyShape(previous.State))
        {
            return ToRef(
                previous,
                "EnergySpentEntry snapshot is pre-mutation for this paid card play");
        }

        if (IsPlayerReadyShape(previous.State)
            && HandCount(previous.State) == HandCount(boundaries[startIndex].State) + 1)
        {
            return ToRef(
                previous,
                "previous settled player-state candidate for zero-cost card play");
        }

        diagnostics.Add("pre_card_decision_snapshot_not_observed");
        return null;
    }

    private static ReferenceProbeSequenceRef? FindSettledPlayerStateAfterCard(
        IReadOnlyList<ProbeBoundary> boundaries,
        int finishIndex,
        List<string> diagnostics)
    {
        for (var index = finishIndex + 1; index < boundaries.Count; index++)
        {
            var candidate = boundaries[index];
            var historyType = HistoryType(candidate.HistoryEntry);

            if (historyType.EndsWith(
                    ".CardPlayStartedEntry",
                    StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "next_card_started_before_a_settled_post_card_snapshot_was_observed");
                return null;
            }

            if (StringComparer.Ordinal.Equals(
                    candidate.Boundary,
                    "combat_manager.PlayerEndedTurn"))
            {
                return IsPlayerReadyShape(candidate.State)
                    ? ToRef(
                        candidate,
                        "settled player state immediately before end-turn transition")
                    : null;
            }

            if (!IsPlayerReadyShape(candidate.State))
            {
                continue;
            }

            return ToRef(
                candidate,
                "first observed settled player-state candidate after card finish");
        }

        diagnostics.Add("post_card_settled_snapshot_not_observed");
        return null;
    }

    private static ReferenceProbeSequenceRef? FindNextPlayerTurn(
        IReadOnlyList<ProbeBoundary> boundaries,
        int index)
    {
        for (var scan = index + 1; scan < boundaries.Count; scan++)
        {
            var candidate = boundaries[scan];
            if (!StringComparer.Ordinal.Equals(
                    candidate.Boundary,
                    "combat_manager.TurnStarted"))
            {
                continue;
            }

            if (!StringComparer.Ordinal.Equals(
                    GetString(
                        GetObject(candidate.State, "combat"),
                        "current_side"),
                    "Player"))
            {
                continue;
            }

            return ToRef(
                candidate,
                "next player TurnStarted after automatic enemy-turn resolution");
        }

        return null;
    }

    private static int? FindMatchingCardFinish(
        IReadOnlyList<ProbeBoundary> boundaries,
        int startIndex,
        string? title,
        long? target,
        bool? isAutoPlay,
        long? playIndex,
        long? playCount)
    {
        for (var index = startIndex + 1; index < boundaries.Count; index++)
        {
            var candidate = boundaries[index];
            var historyType = HistoryType(candidate.HistoryEntry);
            if (!historyType.EndsWith(
                    ".CardPlayFinishedEntry",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var cardPlay = GetObject(candidate.HistoryEntry, "card_play");
            var card = GetObject(cardPlay, "card");

            if (!StringComparer.Ordinal.Equals(
                    GetString(card, "title"),
                    title)
                || GetInt64(
                    GetObject(cardPlay, "target"),
                    "combat_id") != target
                || GetBool(cardPlay, "is_auto_play") != isAutoPlay
                || GetInt64(cardPlay, "play_index") != playIndex
                || GetInt64(cardPlay, "play_count") != playCount)
            {
                continue;
            }

            return index;
        }

        return null;
    }

    private static long[] FindRemovedHandIndices(
        JsonElement? beforeState,
        JsonElement startState)
    {
        if (beforeState is null)
        {
            return [];
        }

        var before = HandItems(beforeState.Value);
        var after = HandItems(startState);
        if (before.Length != after.Length + 1)
        {
            return [];
        }

        var candidates = new List<long>();
        for (var removed = 0; removed < before.Length; removed++)
        {
            var matches = true;
            for (var beforeIndex = 0, afterIndex = 0;
                 beforeIndex < before.Length;
                 beforeIndex++)
            {
                if (beforeIndex == removed)
                {
                    continue;
                }

                if (!CardProjectionEquivalent(
                        before[beforeIndex],
                        after[afterIndex++]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                candidates.Add(removed);
            }
        }

        return candidates.Select(index => (long)index).ToArray();
    }

    private static bool CardProjectionEquivalent(
        JsonElement left,
        JsonElement right)
    {
        return StringComparer.Ordinal.Equals(
                GetString(left, "type"),
                GetString(right, "type"))
            && StringComparer.Ordinal.Equals(
                GetString(GetObject(left, "id"), "entry"),
                GetString(GetObject(right, "id"), "entry"))
            && GetBool(left, "is_upgraded") == GetBool(right, "is_upgraded");
    }

    private static string? CardIdFromPlayPile(JsonElement state)
    {
        var items = PlayPileItems(state);
        return items.Length == 1
            ? GetString(GetObject(items[0], "id"), "entry")
            : null;
    }

    private static string? CardRuntimeTypeFromPlayPile(JsonElement state)
    {
        var items = PlayPileItems(state);
        return items.Length == 1
            ? GetString(items[0], "type")
            : null;
    }

    private static bool? CardUpgradeFromPlayPile(JsonElement state)
    {
        var items = PlayPileItems(state);
        return items.Length == 1
            ? GetBool(items[0], "is_upgraded")
            : null;
    }

    private static string? CardRuntimeTypeFromNested(JsonElement card)
    {
        foreach (var name in new[] { "deck_version", "canonical_instance" })
        {
            var nested = GetObject(card, name);
            var type = GetString(nested, "runtime_type")
                ?? GetString(nested, "type");
            if (type?.Contains(
                    ".Models.Cards.",
                    StringComparison.Ordinal) == true)
            {
                return type;
            }
        }

        return null;
    }

    private static bool IsPlayerReadyShape(JsonElement state)
    {
        var manager = GetObject(state, "manager");
        var combat = GetObject(state, "combat");
        var player = FirstPlayer(state);
        var playerCombat = GetObject(player, "combat");

        return GetBool(manager, "is_in_progress") == true
            && GetBool(manager, "is_over_or_ending") == false
            && GetBool(manager, "player_actions_disabled") == false
            && StringComparer.Ordinal.Equals(
                GetString(combat, "current_side"),
                "Player")
            && StringComparer.Ordinal.Equals(
                GetString(playerCombat, "phase"),
                "Play")
            && PlayPileItems(state).Length == 0;
    }

    private static int HandCount(JsonElement state) =>
        HandItems(state).Length;

    private static JsonElement[] HandItems(JsonElement state) =>
        PileItems(FirstPlayer(state), "hand");

    private static JsonElement[] PlayPileItems(JsonElement state) =>
        PileItems(FirstPlayer(state), "play_pile");

    private static JsonElement[] PileItems(
        JsonElement player,
        string pileName)
    {
        var pile = GetObject(player, pileName);
        if (pile.ValueKind != JsonValueKind.Object
            || !pile.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static JsonElement FirstPlayer(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object
            || !state.TryGetProperty("players", out var players)
            || players.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        foreach (var player in players.EnumerateArray())
        {
            return player;
        }

        return default;
    }

    private static ReferenceProbeSequenceRef ToRef(
        ProbeBoundary boundary,
        string evidence) =>
        new(
            boundary.Sequence,
            boundary.Boundary,
            boundary.StateHash,
            evidence);

    private static List<ProbeBoundary> ReadBoundaries(string path)
    {
        var result = new List<ProbeBoundary>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (!StringComparer.Ordinal.Equals(
                        GetString(root, "type"),
                        "boundary")
                    || !root.TryGetProperty("sequence", out var sequenceElement)
                    || !sequenceElement.TryGetInt64(out var sequence))
                {
                    continue;
                }

                var boundary = GetString(root, "boundary") ?? "<missing>";
                var stateHash = GetString(root, "state_hash") ?? "<missing>";
                var state = root.TryGetProperty("state", out var stateElement)
                    ? stateElement.Clone()
                    : JsonSerializer.SerializeToElement(
                        new Dictionary<string, object?>());
                var historyEntry =
                    root.TryGetProperty("history_entry", out var historyElement)
                        ? historyElement.Clone()
                        : default;

                result.Add(new ProbeBoundary(
                    sequence,
                    boundary,
                    stateHash,
                    state,
                    historyEntry));
            }
        }

        result.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
        return result;
    }

    private static string HistoryType(JsonElement historyEntry)
    {
        if (historyEntry.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return GetString(historyEntry, "runtime_type")
            ?? GetString(historyEntry, "type")
            ?? string.Empty;
    }

    private static JsonElement GetObject(
        JsonElement element,
        string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Object
                ? property
                : default;
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static long? GetInt64(
        JsonElement element,
        string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out var value)
                ? value
                : null;
    }

    private static bool? GetBool(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private sealed record ProbeBoundary(
        long Sequence,
        string Boundary,
        string StateHash,
        JsonElement State,
        JsonElement HistoryEntry);
}
