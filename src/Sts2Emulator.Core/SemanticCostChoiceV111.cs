namespace Sts2Emulator.Core;

/// <summary>One opt-in source-timed random card-cost event.</summary>
public sealed record SemanticCostEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, RandomIndexRequestV111 Request,
    int[] CandidateCosts, string ProbabilityStatus = "UNVERIFIED",
    long? TargetCardInstanceId = null, string? TargetCardModelId = null)
    : ISemanticChanceEventV111
{
    public const string EventSchemaId = "sts2-v111-semantic-cost-choice-v1";
    public string SiteId => Request.SiteId;
    public string OperationKind => Request.OperationKind;
    public string StreamId => Request.StreamId;
    public string OwnerId => Request.OwnerId;
    public int Ordinal => Request.Ordinal;

    public SemanticCostEventV111 Fork() => this with
    {
        Request = Request with
        {
            OrderedCandidates = (int[])Request.OrderedCandidates.Clone()
        },
        CandidateCosts = (int[])CandidateCosts.Clone()
    };

    public void Validate()
    {
        Request.Validate();
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            Request.OperationKind != "bounded_integer" ||
            CandidateCosts is null || CandidateCosts.Length == 0 ||
            CandidateCosts.Distinct().Count() != CandidateCosts.Length ||
            !CandidateCosts.SequenceEqual(
                Enumerable.Range(0, CandidateCosts.Length)) ||
            Request.SupportCount != CandidateCosts.Length ||
            ProbabilityStatus != "UNVERIFIED" ||
            (TargetCardInstanceId is null) != (TargetCardModelId is null) ||
            (TargetCardInstanceId is not null &&
             (TargetCardInstanceId <= 0 ||
              string.IsNullOrWhiteSpace(TargetCardModelId) ||
              !((Request.SiteId ==
                     "combat.random_hand_card.energy_cost" &&
                 Request.StreamId == "prototype.combat") ||
                ((Request.SiteId is
                     "combat.card_draw.relic_energy_cost" or
                     "combat.card_draw.slither_energy_cost") &&
                 Request.StreamId == "semantic.combat")))))
            throw new InvalidDataException("Invalid semantic cost event.");
        var expectedSupportFingerprint = TargetCardInstanceId is null
            ? CanonicalJson.Sha256(new
                { request = Request, costs = CandidateCosts })
            : CanonicalJson.Sha256(new
            {
                request = Request,
                costs = CandidateCosts,
                targetCardInstanceId = TargetCardInstanceId,
                targetCardModelId = TargetCardModelId
            });
        if (SupportFingerprint != expectedSupportFingerprint)
            throw new InvalidDataException(
                "Semantic cost support fingerprint is inconsistent.");
    }
}

public sealed record SemanticCostDecisionV111(
    SemanticCostEventV111 Event, int SelectedIndex, int SelectedCost)
{
    public SemanticCostDecisionV111 Fork() => this with
    {
        Event = Event.Fork()
    };

    public void Validate()
    {
        Event.Validate();
        if (SelectedIndex < 0 || SelectedIndex >= Event.CandidateCosts.Length ||
            Event.CandidateCosts[SelectedIndex] != SelectedCost)
            throw new InvalidDataException(
                "Semantic cost decision is outside its event support.");
    }
}
