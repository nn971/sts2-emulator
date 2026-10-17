namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped candidate at one actual enemy-intent random ticket.
/// TicketStart maps a semantic move selection to the first legal bounded
/// primitive ticket in its source raw-weight interval. Chance Law queries
/// map exact Native NextInt grid preimages through these intervals; that does
/// not establish source eligibility completeness or player-visible
/// conditional probabilities.
/// </summary>
public sealed record SemanticIntentCandidateV111(
    string IntentId, string MoveId, int MoveIndex,
    int RawWeight, int TicketStart);

/// <summary>
/// Candidate list after the same phase/history/formation eligibility filters
/// used by the original combat selector, at the current suspended RNG call.
/// </summary>
public sealed record SemanticIntentSupportV111(
    string OwnerId, SemanticIntentCandidateV111[] Candidates,
    int TotalRawWeight)
{
    public const string SchemaId = "sts2-v111-semantic-weighted-intent-v1";
}

/// <summary>A Nature-facing weighted intent support event.</summary>
public sealed record SemanticIntentEventV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, SemanticIntentSupportV111 Support,
    string ProbabilityStatus = "UNVERIFIED",
    string ProbabilityProvenance = "source-shaped-unverified",
    bool WeightsVerified = false) : ISemanticChanceEventV111
{
    public const string EventSchemaId = "sts2-v111-semantic-intent-event-v1";
    public string OperationKind => "weighted_choice";

    public void Validate()
    {
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(StreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            Support is null || Support.OwnerId != OwnerId ||
            Support.Candidates is not { Length: > 0 } ||
            Support.TotalRawWeight <= 0 ||
            SupportFingerprint != CanonicalJson.Sha256(Support) ||
            ProbabilityStatus != "UNVERIFIED" ||
            ProbabilityProvenance != "source-shaped-unverified" ||
            WeightsVerified)
            throw new InvalidDataException("Invalid semantic intent event.");
        var intentIds = new HashSet<string>(StringComparer.Ordinal);
        int ticketStart = 0;
        foreach (var candidate in Support.Candidates)
        {
            if (candidate is null ||
                string.IsNullOrWhiteSpace(candidate.IntentId) ||
                string.IsNullOrWhiteSpace(candidate.MoveId) ||
                candidate.RawWeight <= 0 ||
                candidate.TicketStart != ticketStart ||
                !intentIds.Add(candidate.IntentId))
                throw new InvalidDataException(
                    "Semantic intent candidates are malformed or unordered.");
            ticketStart = checked(ticketStart + candidate.RawWeight);
        }
        if (ticketStart != Support.TotalRawWeight)
            throw new InvalidDataException(
                "Semantic intent weights do not match their total.");
    }
}

/// <summary>
/// Selected semantic branch and its event support. The source selector's
/// bounded ticket realization is retained separately for mechanics replay.
/// </summary>
public sealed record SemanticIntentDecisionV111(
    SemanticIntentEventV111 Event, string IntentId)
{
    public void Validate()
    {
        Event.Validate();
        if (string.IsNullOrWhiteSpace(IntentId) ||
            !Event.Support.Candidates.Any(candidate =>
                candidate.IntentId == IntentId))
            throw new InvalidDataException(
                "Semantic intent outcome is outside its event support.");
    }
}
