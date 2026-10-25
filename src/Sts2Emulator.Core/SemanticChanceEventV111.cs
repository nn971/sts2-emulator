using System.Globalization;

namespace Sts2Emulator.Core;

public static class SemanticChanceEventContractV111
{
    public const string SchemaId = "sts2-v111-semantic-chance-event-v1";
}

/// <summary>
/// Shared description of a mechanic-level stochastic event. Semantic event
/// schemas carry support and conditioning metadata independently of the
/// source provider's primitive stream/cursor transcript.
/// </summary>
public interface ISemanticChanceEventV111
{
    string SchemaId { get; }
    string EventId { get; }
    string SiteId { get; }
    string OperationKind { get; }
    string StreamId { get; }
    string OwnerId { get; }
    int Ordinal { get; }
    string SupportFingerprint { get; }
    string ProbabilityStatus { get; }
}

/// <summary>
/// The single active index-valued semantic event exposed to providers. The
/// event remains owned by its typed mechanic; this envelope only fixes its
/// dispatch precedence, checked support size, and originating boundary.
/// </summary>
internal sealed record SemanticPendingIndexChanceEventV111(
    ISemanticChanceEventV111 Event, int SupportCount,
    string BoundaryFingerprint)
{
    public static SemanticPendingIndexChanceEventV111 Create(
        string boundaryFingerprint, ISemanticChanceEventV111 chanceEvent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(chanceEvent);
        var supportCount = CreateOrderedOutcomeIds(chanceEvent).Length;
        var pending = new SemanticPendingIndexChanceEventV111(
            chanceEvent, supportCount, boundaryFingerprint);
        pending.Validate();
        return pending;
    }

    public string[] OrderedOutcomeIds => CreateOrderedOutcomeIds(Event);

    public void ValidateSelection(string boundaryFingerprint, int index)
    {
        ValidateBoundary(boundaryFingerprint);
        if (index < 0 || index >= SupportCount)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    public int SelectNativeOutcomeIndex(RandomIndexDecisionV111 decision)
    {
        Validate();
        if (Event.OperationKind is not ("choose_index" or "bounded_integer"))
            throw new NotSupportedException(
                $"UNSUPPORTED_SEMANTIC_CHANCE site={Event.SiteId} " +
                $"operation={Event.OperationKind}");
        return NativeSemanticOutcomeV111.SelectIndex(
            decision, Event.OperationKind, SupportCount);
    }

    public void ValidateBoundary(string boundaryFingerprint)
    {
        Validate();
        if (!StringComparer.Ordinal.Equals(
                BoundaryFingerprint, boundaryFingerprint))
            throw new InvalidOperationException(
                "Semantic chance descriptor is stale for the current boundary.");
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Event);
        if (string.IsNullOrWhiteSpace(Event.EventId) || SupportCount < 1 ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint))
            throw new InvalidDataException(
                "Invalid pending semantic index chance event.");
        if (CreateOrderedOutcomeIds(Event).Length != SupportCount)
            throw new InvalidDataException(
                $"Pending semantic chance support changed " +
                $"site={Event.SiteId}.");
    }

    private static string[] CreateOrderedOutcomeIds(
        ISemanticChanceEventV111 chanceEvent) => chanceEvent switch
    {
        SemanticEndTurnIndexEventV111 endTurn => IndexOutcomeIds(
            "choice_index:", endTurn.Request.SupportCount),
        SemanticTargetEventV111 target =>
            target.CandidateEnemyInstanceIds.Select(instanceId =>
                "enemy_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture)).ToArray(),
        SemanticGeneratedCardEventV111 generatedCard =>
            generatedCard.CandidateCardIds.Select(cardId =>
                "card_model:" + cardId).ToArray(),
        SemanticCostEventV111 cost =>
            cost.CandidateCosts.Select(value =>
                "cost:" + value.ToString(CultureInfo.InvariantCulture))
                .ToArray(),
        SemanticRandomPileCardEventV111 randomPileCard =>
            randomPileCard.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        SemanticCardTransformEventV111 transform =>
            transform.CandidateCardIds.Select(cardId =>
                "card_model:" + cardId).ToArray(),
        SemanticPotionGenerationEventV111 potion =>
            potion.CandidatePotionIds.Select(potionId =>
                "potion_model:" + potionId).ToArray(),
        SemanticHandCostSelectionEventV111 handCost =>
            handCost.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        SemanticPoolShuffleEventV111 poolShuffle => IndexOutcomeIds(
            "swap_index:", poolShuffle.Request.SupportCount),
        SemanticDrawInsertionEventV111 drawInsertion => IndexOutcomeIds(
            "draw_position:", drawInsertion.Request.SupportCount),
        SemanticCardShortlistEventV111 shortlist =>
            shortlist.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        SemanticDeckEventV111 deck =>
            deck.CandidateCardInstanceIds
                .Select(CardInstanceOutcomeId).ToArray(),
        _ => throw new NotSupportedException(
            $"UNSUPPORTED_SEMANTIC_CHANCE site={chanceEvent.SiteId}")
    };

    private static string[] IndexOutcomeIds(string prefix, int supportCount) =>
        Enumerable.Range(0, supportCount).Select(index =>
            prefix + index.ToString(CultureInfo.InvariantCulture)).ToArray();

    private static string CardInstanceOutcomeId(long instanceId) =>
        "card_instance:" + instanceId.ToString(CultureInfo.InvariantCulture);
}
