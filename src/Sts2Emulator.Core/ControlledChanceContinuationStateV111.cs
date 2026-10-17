namespace Sts2Emulator.Core;

/// <summary>
/// Provider-side payload for a suspended typed random-index request. The
/// enclosing engine continuation retains its boundary-specific mechanics;
/// these fields describe only the saved outcome prefix and pending request.
/// </summary>
public sealed record ControlledChanceContinuationStateV111(
    RandomIndexDecisionV111[] Decisions,
    RandomIndexRequestV111 PendingRequest,
    string[]? PendingCandidateLabels = null,
    string[][]? DecisionCandidateLabels = null)
{
    public ControlledChanceContinuationStateV111 Fork() => this with
    {
        Decisions = Decisions.Select(CloneDecision).ToArray(),
        PendingRequest = CloneRequest(PendingRequest),
        PendingCandidateLabels = PendingCandidateLabels is null
            ? null : (string[])PendingCandidateLabels.Clone(),
        DecisionCandidateLabels = DecisionCandidateLabels?.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = CloneRequest(decision.Request)
    };

    private static RandomIndexRequestV111 CloneRequest(
        RandomIndexRequestV111 request) => request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };
}
