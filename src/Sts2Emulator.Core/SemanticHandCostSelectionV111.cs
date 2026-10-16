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
