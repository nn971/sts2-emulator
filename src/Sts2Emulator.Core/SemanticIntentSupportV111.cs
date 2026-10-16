namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped candidate at one actual enemy-intent random ticket.
/// TicketStart maps a semantic move selection to the first legal bounded
/// primitive ticket in its raw-weight interval. Do not interpret these
/// weights as independently audited native probabilities.
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

/// <summary>
/// A Nature-facing weighted intent choice. This semantic event is distinct
/// from the bounded integer ticket used internally by the source-shaped AI
/// selector and by native RNG replay.
/// </summary>
public sealed record SemanticIntentDecisionV111(
    string SchemaId, string EventId, string BoundaryFingerprint,
    string SupportFingerprint, string SiteId, string StreamId,
    string OwnerId, int Ordinal, string IntentId,
    int RawWeight, int TotalRawWeight,
    string ProbabilityStatus = "UNVERIFIED",
    string ProbabilityProvenance = "source-shaped-unverified",
    bool WeightsVerified = false)
{
    public const string EventSchemaId = "sts2-v111-semantic-intent-event-v1";

    public void Validate()
    {
        if (SchemaId != EventSchemaId || string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(BoundaryFingerprint) ||
            string.IsNullOrWhiteSpace(SupportFingerprint) ||
            string.IsNullOrWhiteSpace(SiteId) ||
            string.IsNullOrWhiteSpace(StreamId) ||
            string.IsNullOrWhiteSpace(OwnerId) || Ordinal < 0 ||
            string.IsNullOrWhiteSpace(IntentId) || RawWeight <= 0 ||
            TotalRawWeight < RawWeight || ProbabilityStatus != "UNVERIFIED" ||
            ProbabilityProvenance != "source-shaped-unverified" ||
            WeightsVerified)
            throw new InvalidDataException("Invalid semantic intent decision event.");
    }
}
