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
