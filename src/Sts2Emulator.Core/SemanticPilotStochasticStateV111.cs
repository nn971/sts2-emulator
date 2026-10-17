namespace Sts2Emulator.Core;

/// <summary>
/// Provider-private state and provenance owned by the semantic pilot. The
/// public pilot members remain compatibility projections for its current
/// serialized representation.
/// </summary>
public sealed record SemanticPilotStochasticStateV111(
    SemanticOtherChanceStateV111? OtherChance = null,
    SemanticRewardAuditV111? RewardAudit = null,
    SemanticNativePolicyAuditV111? NativePolicyAudit = null,
    SemanticSourceRouteAuditV111? SourceRouteAudit = null,
    SemanticEndTurnStochasticStateV111? PendingEndTurn = null,
    SemanticSeededEntryV111? SeededEntry = null)
{
    public SemanticPilotStochasticStateV111 Fork() => this with
    {
        OtherChance = OtherChance?.Fork(),
        PendingEndTurn = PendingEndTurn?.Fork(),
        NativePolicyAudit = NativePolicyAudit is { } audit
            ? audit with
            {
                Decisions = audit.Decisions.Select(decision => decision with
                {
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                }).ToArray()
            }
            : null,
        SourceRouteAudit = SourceRouteAudit is { } route
            ? route with
            {
                ActionIds = (string[])route.ActionIds.Clone(),
                NativeEntryDecisions = route.NativeEntryDecisions.Select(
                    decision => decision with
                    {
                        Request = decision.Request with
                        {
                            OrderedCandidates = (int[])decision.Request
                                .OrderedCandidates.Clone()
                        }
                    }).ToArray()
            }
            : null
    };

    public void Validate()
    {
        if (OtherChance is not null)
            SemanticOtherChanceEngineV111.Validate(OtherChance);
        if (PendingEndTurn is not null)
            PendingEndTurn.Validate();
    }

    internal static SemanticPilotStochasticStateV111 WithOtherChance(
        SemanticPilotStochasticStateV111? current,
        SemanticOtherChanceStateV111? otherChance) =>
        (current ?? new SemanticPilotStochasticStateV111()) with
        {
            OtherChance = otherChance
        };

    internal static SemanticPilotStochasticStateV111 WithRewardAudit(
        SemanticPilotStochasticStateV111? current,
        SemanticRewardAuditV111? rewardAudit) =>
        (current ?? new SemanticPilotStochasticStateV111()) with
        {
            RewardAudit = rewardAudit
        };

    internal static SemanticPilotStochasticStateV111 WithNativePolicyAudit(
        SemanticPilotStochasticStateV111? current,
        SemanticNativePolicyAuditV111? nativePolicyAudit) =>
        (current ?? new SemanticPilotStochasticStateV111()) with
        {
            NativePolicyAudit = nativePolicyAudit
        };

    internal static SemanticPilotStochasticStateV111 WithSourceRouteAudit(
        SemanticPilotStochasticStateV111? current,
        SemanticSourceRouteAuditV111? sourceRouteAudit) =>
        (current ?? new SemanticPilotStochasticStateV111()) with
        {
            SourceRouteAudit = sourceRouteAudit
        };
}
