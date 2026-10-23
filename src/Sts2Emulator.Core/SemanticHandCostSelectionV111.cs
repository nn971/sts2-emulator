namespace Sts2Emulator.Core;

/// <summary>One source-timed choice of a positive-cost hand card whose cost
/// is changed by a fixed-value effect.</summary>
public sealed record SemanticHandCostSelectionEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string OperationKind,
    long[] CandidateCardInstanceIds, string[] CandidateCardModelIds,
    int AppliedCost, string ProbabilityStatus = "UNVERIFIED")
    : ISemanticChanceEventV111
{
    private long[]? _candidateCardInstanceIds =
        CloneOptional(CandidateCardInstanceIds);
    private string[]? _candidateCardModelIds =
        CloneOptional(CandidateCardModelIds);

    public long[] CandidateCardInstanceIds
    {
        get => CloneOptional(_candidateCardInstanceIds)!;
        init => _candidateCardInstanceIds = CloneOptional(value);
    }
    public string[] CandidateCardModelIds
    {
        get => CloneOptional(_candidateCardModelIds)!;
        init => _candidateCardModelIds = CloneOptional(value);
    }

    public const string EventSchemaId =
        SemanticHandCostSelectionChoiceV111.SchemaId;

    public SemanticHandCostSelectionEventV111 Fork() => this with
    {
        CandidateCardInstanceIds = _candidateCardInstanceIds!,
        CandidateCardModelIds = _candidateCardModelIds!
    };

    public void Validate()
    {
        var candidateCardInstanceIds = _candidateCardInstanceIds;
        var candidateCardModelIds = _candidateCardModelIds;
        var expectedEventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint,
            SupportFingerprint
        });
        if (SchemaId != EventSchemaId ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            SiteId != SemanticHandCostSelectionChoiceV111.SiteId ||
            StreamId is not (SemanticHandCostSelectionChoiceV111.StreamId or
                SemanticHandCostSelectionChoiceV111.NativeStreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != SemanticHandCostSelectionChoiceV111.OperationKind ||
            candidateCardInstanceIds is not { Length: > 0 } ||
            candidateCardModelIds is null ||
            candidateCardModelIds.Length != candidateCardInstanceIds.Length ||
            candidateCardInstanceIds.Any(id => id <= 0) ||
            candidateCardInstanceIds.Distinct().Count() !=
                candidateCardInstanceIds.Length ||
            candidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            AppliedCost < 0 || ProbabilityStatus != "UNVERIFIED" ||
            EventId != expectedEventId ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                candidateCardInstanceIds,
                candidateCardModelIds,
                appliedCost = AppliedCost
            }))
            throw new InvalidDataException(
                "Invalid semantic hand-cost selection event.");
    }

    private static long[]? CloneOptional(long[]? values) => values is null
        ? null
        : (long[])values.Clone();

    private static string[]? CloneOptional(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

public sealed record SemanticHandCostSelectionDecisionV111(
    SemanticHandCostSelectionEventV111 Event, int SelectedIndex,
    long SelectedCardInstanceId, string SelectedCardModelId)
{
    public SemanticHandCostSelectionDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 ||
            SelectedIndex >= Event.CandidateCardInstanceIds.Length ||
            Event.CandidateCardInstanceIds[SelectedIndex] !=
                SelectedCardInstanceId ||
            Event.CandidateCardModelIds[SelectedIndex] != SelectedCardModelId)
            throw new InvalidDataException(
                "Hand-cost selection is outside its ordered source support.");
    }
}

public static class SemanticHandCostSelectionChoiceV111
{
    public const string SchemaId =
        "sts2-v111-semantic-hand-cost-selection-v1";
    public const string SiteId =
        StochasticSiteRegistryV111.CombatRandomHandCardSelectSiteId;
    internal const string StreamId = "prototype.combat";
    internal const string NativeStreamId = "run.combat_card_selection";
    internal const string OperationKind = "choose_hand_card_instance";

    internal static SemanticHandCostSelectionEventV111 CreateEvent(
        string boundaryFingerprint,
        PrototypeControlledRandomCostV111 randomCost,
        string[] candidateCardModelIds, int ordinal,
        string streamId = StreamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryFingerprint);
        ArgumentNullException.ThrowIfNull(randomCost);
        ArgumentNullException.ThrowIfNull(candidateCardModelIds);
        if (randomCost.Operation.Kind != PrototypeCombatEffectKind
                .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed ||
            randomCost.EligibleCardInstanceIds is not { Length: > 0 } ||
            candidateCardModelIds.Length !=
                randomCost.EligibleCardInstanceIds.Length)
            throw new InvalidDataException(
                "Hand-cost selection source pool is invalid.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (streamId is not (StreamId or NativeStreamId))
            throw new NotSupportedException(
                "UNSUPPORTED_RANDOM_SITE site=" + SiteId +
                " operation=hand_cost_selection_stream.");

        var cardIds = (long[])randomCost.EligibleCardInstanceIds.Clone();
        var modelIds = (string[])candidateCardModelIds.Clone();
        var appliedCost = Math.Max(0, randomCost.Operation.Amount);
        var supportFingerprint = CanonicalJson.Sha256(new
        {
            candidateCardInstanceIds = cardIds,
            candidateCardModelIds = modelIds,
            appliedCost
        });
        var ownerId = "effect:" + randomCost.Operation.Kind + ":hand:" +
            CanonicalJson.Sha256(cardIds);
        var eventId = CanonicalJson.Sha256(new
        {
            SchemaId,
            BoundaryFingerprint = boundaryFingerprint,
            SupportFingerprint = supportFingerprint
        });
        var semanticEvent = new SemanticHandCostSelectionEventV111(
            SchemaId, eventId, boundaryFingerprint, supportFingerprint,
            SiteId, streamId, ownerId, ordinal, OperationKind, cardIds,
            modelIds, appliedCost);
        semanticEvent.Validate();
        return semanticEvent;
    }

    public static SemanticHandCostSelectionDecisionV111 Select(
        SemanticHandCostSelectionEventV111 pending, string eventId,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (pending.EventId != eventId)
            throw new InvalidOperationException(
                "Hand-cost selection event ID is stale or mismatched.");
        var index = Array.IndexOf(
            pending.CandidateCardInstanceIds, cardInstanceId);
        if (index < 0)
            throw new InvalidOperationException(
                "Card is outside the pending hand-cost selection support.");
        var decision = new SemanticHandCostSelectionDecisionV111(
            pending.Fork(), index, cardInstanceId,
            pending.CandidateCardModelIds[index]);
        decision.Validate();
        return decision;
    }

    internal static void ValidateDecision(
        SemanticHandCostSelectionEventV111? pending,
        SemanticHandCostSelectionDecisionV111 decision,
        long cardInstanceId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        pending?.Validate();
        decision.Validate();
        bool legacyPrototypeStream = pending is not null &&
            pending.StreamId == NativeStreamId &&
            decision.Event.StreamId == StreamId &&
            CanonicalJson.Sha256(pending with
            {
                StreamId = decision.Event.StreamId
            }) == CanonicalJson.Sha256(decision.Event);
        if (pending is null || pending.EventId != decision.Event.EventId ||
            (CanonicalJson.Sha256(pending) !=
                CanonicalJson.Sha256(decision.Event) &&
             !legacyPrototypeStream) ||
            decision.SelectedCardInstanceId != cardInstanceId)
            throw new InvalidDataException(
                "Hand-cost decision differs from its live semantic event.");
    }

    /// <summary>Revalidate the live hand-card support and resume its ordinary
    /// random-cost continuation.</summary>
    internal static RunState Resume(
        RunState checkpoint,
        SemanticHandCostSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var combat = checkpoint.World?.Combat ??
            throw new InvalidDataException(
                "Hand-cost selection lost its source combat.");
        if (decision.Event.StreamId == NativeStreamId &&
            combat.Stochastic.NativeRng is null)
            throw new InvalidDataException(
                "Native hand-cost selection stream attribution has no Native combat RNG owner.");
        var randomCost = combat.PendingChoice?.ControlledRandomCost;
        if (randomCost is null || randomCost.Operation.Kind !=
                PrototypeCombatEffectKind
                    .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed)
            throw new InvalidDataException(
                "Hand-cost selection does not match its source operation.");
        SemanticRandomHandCostChoiceV111.ValidateBoundary(
            checkpoint);
        var eventState = decision.Event;
        var candidateIds = randomCost.EligibleCardInstanceIds;
        var candidateModels = candidateIds.Select(id => combat.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        if (eventState.SiteId != SiteId ||
            eventState.OperationKind != OperationKind ||
            eventState.AppliedCost != Math.Max(
                0, randomCost.Operation.Amount) ||
            !candidateIds.SequenceEqual(
                eventState.CandidateCardInstanceIds) ||
            !candidateModels.SequenceEqual(
                eventState.CandidateCardModelIds))
            throw new InvalidDataException(
                "Hand-cost decision no longer matches its live support.");
        return SemanticRandomHandCostChoiceV111.ResumeSelectedHandCard(
            checkpoint, decision.SelectedCardInstanceId);
    }
}
