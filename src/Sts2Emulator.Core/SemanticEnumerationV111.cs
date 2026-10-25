using System.Globalization;

namespace Sts2Emulator.Core;

public sealed record SemanticEnumeratedOutcomeV111(
    string OutcomeId, SemanticPilotCombatV111 Child)
{
    public SemanticEnumeratedOutcomeV111 Fork() => this with
    {
        Child = Child.Fork()
    };
}

public sealed record SemanticEnumerationResultV111(
    string SchemaId, string EventKind, string EventId,
    SemanticEnumeratedOutcomeV111[] Outcomes)
{
    private SemanticEnumeratedOutcomeV111[]? _outcomes =
        CloneOutcomes(Outcomes);

    public SemanticEnumeratedOutcomeV111[] Outcomes
    {
        get => CloneOutcomes(_outcomes)!;
        init => _outcomes = CloneOutcomes(value);
    }

    public SemanticEnumerationResultV111 Fork() => this with
    {
        Outcomes = _outcomes?.Select(outcome => outcome is null
            ? null!
            : outcome.Fork()).ToArray()!
    };

    private static SemanticEnumeratedOutcomeV111[]? CloneOutcomes(
        SemanticEnumeratedOutcomeV111[]? outcomes) => outcomes?.Select(
            outcome => outcome is null ? null! : outcome with { }).ToArray();
}

/// <summary>
/// Enumerates one small semantic chance support by resolving each legal
/// outcome against an independent child session. It never enumerates player
/// actions or silently truncates a support.
/// </summary>
public static class SemanticNatureEnumeratorV111
{
    public const string SchemaId = "sts2-v111-semantic-enumeration-v1";
    public const int MaximumSupportedOutcomes = 256;

    public static SemanticEnumerationResultV111 Enumerate(
        SemanticPilotCombatV111 current, string fingerprint,
        int maximumOutcomes = 32)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (maximumOutcomes is < 1 or > MaximumSupportedOutcomes)
            throw new ArgumentOutOfRangeException(nameof(maximumOutcomes),
                $"Enumeration cap must be between 1 and {MaximumSupportedOutcomes}.");
        current.Validate();
        if (current.Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale semantic enumeration fingerprint.");
        string eventKind;
        string eventId;
        string[] outcomeIds;
        Func<int, SemanticPilotCombatV111> resolve;

        if (current.IntentPending is not null)
        {
            var pending =
                SemanticPilotCombatEngineV111.DescribePendingIntentEvent(current);
            eventKind = "enemy_intent";
            eventId = pending.EventId;
            outcomeIds = pending.Support.Candidates
                .Select(candidate => candidate.IntentId).ToArray();
            resolve = outcomeIndex =>
                SemanticPilotCombatEngineV111.ResolveIntent(
                    current, fingerprint,
                    pending.Support.Candidates[outcomeIndex].IntentId);
        }
        else
        {
            var pending = SemanticPilotCombatEngineV111
                .DescribePendingIndexChanceEvent(current) ??
                throw new InvalidOperationException(
                    "No semantic Nature chance event is pending to enumerate.");
            eventId = pending.Event.EventId;
            outcomeIds = DescribeOutcomeIds(pending.Event,
                out eventKind);
            if (outcomeIds.Length != pending.SupportCount)
                throw new InvalidDataException(
                    "Semantic enumeration support differs from the active chance event.");
            resolve = outcomeIndex => SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeByIndex(current, pending, outcomeIndex);
        }

        if (outcomeIds.Length == 0)
            throw new InvalidDataException(
                "A semantic chance event cannot have empty support.");
        if (outcomeIds.Length > maximumOutcomes)
            throw new InvalidOperationException(
                $"Pending support has {outcomeIds.Length} outcomes, exceeding " +
                $"the requested enumeration cap of {maximumOutcomes}.");
        var outcomes = outcomeIds.Select((outcomeId, outcomeIndex) =>
            new SemanticEnumeratedOutcomeV111(
                outcomeId, resolve(outcomeIndex))).ToArray();
        return new SemanticEnumerationResultV111(
            SchemaId, eventKind, eventId, outcomes);
    }

    private static string[] DescribeOutcomeIds(
        ISemanticChanceEventV111 chanceEvent, out string eventKind)
    {
        switch (chanceEvent)
        {
            case SemanticEndTurnIndexEventV111 endTurn:
                eventKind = "enemy_action_chance";
                return endTurn.CandidateLabels ?? Enumerable.Range(
                    0, endTurn.Request.SupportCount).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticTargetEventV111 target:
                eventKind = "random_target";
                return target.CandidateEnemyInstanceIds.Select(id =>
                    id.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticGeneratedCardEventV111 generated:
                eventKind = generated.SiteId ==
                    "combat.random_draw_attack.card_model"
                    ? "random_draw_attack_card_model" : "generated_card";
                return generated.CandidateCardIds;
            case SemanticCostEventV111 cost:
                eventKind = cost.TargetCardInstanceId is null
                    ? "drawn_card_cost" : "random_hand_card_cost";
                return cost.CandidateCosts.Select(value =>
                    value.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticRandomPileCardEventV111 randomPileCard:
                eventKind = "random_pile_card";
                return randomPileCard.CandidateCardInstanceIds.Select(id =>
                    id.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticCardTransformEventV111 transform:
                eventKind = "random_card_transform";
                return transform.CandidateCardIds;
            case SemanticPotionGenerationEventV111 potion:
                eventKind = "random_potion_generation";
                return potion.CandidatePotionIds;
            case SemanticHandCostSelectionEventV111 handCost:
                eventKind = "random_hand_card_selection";
                return handCost.CandidateCardInstanceIds.Select(id =>
                    id.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticPoolShuffleEventV111 poolShuffle:
                eventKind = poolShuffle.SiteId ==
                    StochasticSiteRegistryV111.CombatHandReturnShuffleSiteId
                    ? "hand_return_shuffle"
                    : "generated_card_pool_shuffle";
                return Enumerable.Range(0,
                    poolShuffle.Request.SupportCount).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticDrawInsertionEventV111 drawInsertion:
                eventKind = "generated_draw_pile_insertion";
                return Enumerable.Range(0,
                    drawInsertion.Request.SupportCount).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticCardShortlistEventV111 shortlist:
                eventKind = "random_pile_selection_shortlist";
                return shortlist.CandidateCardInstanceIds.Select(id =>
                    id.ToString(CultureInfo.InvariantCulture)).ToArray();
            case SemanticDeckEventV111 deckEvent:
                eventKind = deckEvent.Purpose == "draw"
                    ? "draw_card" : "deck_reveal";
                return deckEvent.CandidateCardInstanceIds.Select(id =>
                    id.ToString(CultureInfo.InvariantCulture)).ToArray();
            default:
                throw new NotSupportedException(
                    $"UNSUPPORTED_SEMANTIC_CHANCE site={chanceEvent.SiteId}");
        }
    }
}
