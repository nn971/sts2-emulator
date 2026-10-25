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
        var supportCount = chanceEvent switch
        {
            SemanticEndTurnIndexEventV111 endTurn =>
                endTurn.Request.SupportCount,
            SemanticTargetEventV111 target =>
                target.CandidateEnemyInstanceIds.Length,
            SemanticGeneratedCardEventV111 generatedCard =>
                generatedCard.CandidateCardIds.Length,
            SemanticCostEventV111 cost => cost.CandidateCosts.Length,
            SemanticRandomPileCardEventV111 randomPileCard =>
                randomPileCard.CandidateCardInstanceIds.Length,
            SemanticCardTransformEventV111 cardTransform =>
                cardTransform.CandidateCardIds.Length,
            SemanticPotionGenerationEventV111 potionGeneration =>
                potionGeneration.CandidatePotionIds.Length,
            SemanticHandCostSelectionEventV111 handCost =>
                handCost.CandidateCardInstanceIds.Length,
            SemanticPoolShuffleEventV111 poolShuffle =>
                poolShuffle.Request.SupportCount,
            SemanticDrawInsertionEventV111 drawInsertion =>
                drawInsertion.Request.SupportCount,
            SemanticCardShortlistEventV111 shortlist =>
                shortlist.CandidateCardInstanceIds.Length,
            SemanticDeckEventV111 deck =>
                deck.CandidateCardInstanceIds.Length,
            _ => throw new NotSupportedException(
                "Unsupported pending semantic chance event type.")
        };
        var pending = new SemanticPendingIndexChanceEventV111(
            chanceEvent, supportCount, boundaryFingerprint);
        pending.Validate();
        return pending;
    }

    public void ValidateSelection(string boundaryFingerprint, int index)
    {
        Validate();
        if (!StringComparer.Ordinal.Equals(
                BoundaryFingerprint, boundaryFingerprint))
            throw new InvalidOperationException(
                "Semantic chance descriptor is stale for the current boundary.");
        if (index < 0 || index >= SupportCount)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Event);
        if (string.IsNullOrWhiteSpace(Event.EventId) || SupportCount < 1 ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint))
            throw new InvalidDataException(
                "Invalid pending semantic index chance event.");
    }
}
