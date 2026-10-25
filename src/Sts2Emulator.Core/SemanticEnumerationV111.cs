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
        Func<string, SemanticPilotCombatV111> resolve;

        if (current.IntentPending is not null)
        {
            var pending =
                SemanticPilotCombatEngineV111.DescribePendingIntentEvent(current);
            eventKind = "enemy_intent";
            eventId = pending.EventId;
            outcomeIds = pending.Support.Candidates
                .Select(candidate => candidate.IntentId).ToArray();
            resolve = outcomeId =>
                SemanticPilotCombatEngineV111.ResolveIntent(
                    current, fingerprint, outcomeId);
        }
        else
        {
            var pending = SemanticPilotCombatEngineV111
                .DescribePendingIndexChanceEvent(current) ??
                throw new InvalidOperationException(
                    "No semantic Nature chance event is pending to enumerate.");
            eventId = pending.Event.EventId;
            outcomeIds = pending.SemanticOutcomeIds;
            eventKind = DescribeEventKind(pending.Event);
            if (outcomeIds.Length != pending.SupportCount)
                throw new InvalidDataException(
                    "Semantic enumeration support differs from the active chance event.");
            resolve = outcomeId => SemanticPilotCombatEngineV111
                .ResolveChanceOutcomeBySemanticId(
                    current, pending, outcomeId);
        }

        if (outcomeIds.Length == 0)
            throw new InvalidDataException(
                "A semantic chance event cannot have empty support.");
        if (outcomeIds.Length > maximumOutcomes)
            throw new InvalidOperationException(
                $"Pending support has {outcomeIds.Length} outcomes, exceeding " +
                $"the requested enumeration cap of {maximumOutcomes}.");
        var outcomes = outcomeIds.Select(outcomeId =>
            new SemanticEnumeratedOutcomeV111(
                outcomeId, resolve(outcomeId))).ToArray();
        return new SemanticEnumerationResultV111(
            SchemaId, eventKind, eventId, outcomes);
    }

    private static string DescribeEventKind(
        ISemanticChanceEventV111 chanceEvent) => chanceEvent switch
    {
        SemanticEndTurnIndexEventV111 => "enemy_action_chance",
        SemanticTargetEventV111 => "random_target",
        SemanticGeneratedCardEventV111 generated => generated.SiteId ==
            "combat.random_draw_attack.card_model"
                ? "random_draw_attack_card_model" : "generated_card",
        SemanticCostEventV111 cost => cost.TargetCardInstanceId is null
            ? "drawn_card_cost" : "random_hand_card_cost",
        SemanticRandomPileCardEventV111 => "random_pile_card",
        SemanticCardTransformEventV111 => "random_card_transform",
        SemanticPotionGenerationEventV111 => "random_potion_generation",
        SemanticHandCostSelectionEventV111 => "random_hand_card_selection",
        SemanticPoolShuffleEventV111 poolShuffle => poolShuffle.SiteId ==
            StochasticSiteRegistryV111.CombatHandReturnShuffleSiteId
                ? "hand_return_shuffle" : "generated_card_pool_shuffle",
        SemanticDrawInsertionEventV111 => "generated_draw_pile_insertion",
        SemanticCardShortlistEventV111 => "random_pile_selection_shortlist",
        SemanticDeckEventV111 deckEvent => deckEvent.Purpose == "draw"
            ? "draw_card" : "deck_reveal",
        _ => throw new NotSupportedException(
            $"UNSUPPORTED_SEMANTIC_CHANCE site={chanceEvent.SiteId}")
    };
}
