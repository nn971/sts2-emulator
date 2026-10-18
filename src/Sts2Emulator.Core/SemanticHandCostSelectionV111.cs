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
    public const string EventSchemaId =
        "sts2-v111-semantic-hand-cost-selection-v1";

    public SemanticHandCostSelectionEventV111 Fork() => this with
    {
        CandidateCardInstanceIds =
            (long[])CandidateCardInstanceIds.Clone(),
        CandidateCardModelIds = (string[])CandidateCardModelIds.Clone()
    };

    public void Validate()
    {
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
            SiteId != "combat.random_hand_card.select" ||
            StreamId != "prototype.combat" ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            OperationKind != "choose_hand_card_instance" ||
            CandidateCardInstanceIds is not { Length: > 0 } ||
            CandidateCardModelIds is null ||
            CandidateCardModelIds.Length != CandidateCardInstanceIds.Length ||
            CandidateCardInstanceIds.Any(id => id <= 0) ||
            CandidateCardInstanceIds.Distinct().Count() !=
                CandidateCardInstanceIds.Length ||
            CandidateCardModelIds.Any(string.IsNullOrWhiteSpace) ||
            AppliedCost < 0 || ProbabilityStatus != "UNVERIFIED" ||
            EventId != expectedEventId ||
            SupportFingerprint != CanonicalJson.Sha256(new
            {
                candidateCardInstanceIds = CandidateCardInstanceIds,
                candidateCardModelIds = CandidateCardModelIds,
                appliedCost = AppliedCost
            }))
            throw new InvalidDataException(
                "Invalid semantic hand-cost selection event.");
    }
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
        var randomCost = combat.PendingChoice?.ControlledRandomCost;
        if (randomCost is null || randomCost.Operation.Kind !=
                PrototypeCombatEffectKind
                    .SetRandomHandCardEnergyCostUntilTurnEndOrPlayed)
            throw new InvalidDataException(
                "Hand-cost selection does not match its source operation.");
        SemanticPilotCombatEngineV111.ValidateRandomHandCostBoundary(
            checkpoint);
        var eventState = decision.Event;
        var candidateIds = randomCost.EligibleCardInstanceIds;
        var candidateModels = candidateIds.Select(id => combat.Cards.Single(
            card => card.InstanceId == id).CardId).ToArray();
        if (eventState.SiteId != "combat.random_hand_card.select" ||
            eventState.StreamId != "prototype.combat" ||
            eventState.OperationKind != "choose_hand_card_instance" ||
            eventState.AppliedCost != Math.Max(
                0, randomCost.Operation.Amount) ||
            !candidateIds.SequenceEqual(
                eventState.CandidateCardInstanceIds) ||
            !candidateModels.SequenceEqual(
                eventState.CandidateCardModelIds))
            throw new InvalidDataException(
                "Hand-cost decision no longer matches its live support.");
        return SemanticRandomHandCostChoiceV111.Resume(
            checkpoint, eventState.SiteId, decision.SelectedIndex);
    }
}
