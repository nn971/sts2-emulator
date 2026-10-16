namespace Sts2Emulator.Core;

/// <summary>
/// A private, explicit, engine-owned stream for supported non-Nature chance
/// operations in the two-site pilot. The source-shaped site and stream
/// assignment are provisional until audited against the installed game.
/// This must never be attached to fair player observations.
/// </summary>
public sealed record SemanticOtherChanceStateV111(
    RngBundle Streams,
    RandomIndexDecisionV111[] Decisions,
    string ProviderId = SemanticOtherChanceEngineV111.ProviderId)
{
    public SemanticOtherChanceStateV111 Fork() => this with
    {
        Streams = Streams.Fork(),
        Decisions = Decisions.Select(d => d with
        {
            Request = d.Request with
            {
                OrderedCandidates = (int[])d.Request.OrderedCandidates.Clone()
            }
        }).ToArray()
    };
}

public static class SemanticOtherChanceEngineV111
{
    public const string ProviderId =
        "engine-owned-native-v111-cost-stream-unverified-v1";
    public const string CostStreamId = "run.combat_energy_costs";

    /// <summary>
    /// Caller supplies the stream state AFTER all already committed opening
    /// RNG. This keeps physical-deck realization disjoint from Nature's
    /// later unresolved draw-card decisions.
    /// </summary>
    public static SemanticOtherChanceStateV111 Begin(RngBundle afterOpening)
    {
        ArgumentNullException.ThrowIfNull(afterOpening);
        var state = new SemanticOtherChanceStateV111(
            afterOpening.Fork(), []);
        Validate(state);
        return state;
    }

    /// <summary>
    /// Sample a source-timed drawn-card cost using an owned named stream,
    /// without requesting a third choice from Nature. The input state is
    /// never mutated; the applied cost remains in the real DrawCards
    /// continuation, not inside this provider.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveDrawCost(
        SemanticOtherChanceStateV111 previous,
        RandomIndexRequestV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (pending.SiteId is not (
                "combat.card_draw.relic_energy_cost" or
                "combat.card_draw.slither_energy_cost") ||
            pending.OperationKind != "bounded_integer" ||
            pending.StreamId != "semantic.combat" ||
            pending.SupportCount is < 1 or > 32 ||
            !pending.OwnerId.StartsWith("card_instance:", StringComparison.Ordinal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var sourceRequest = pending with
        {
            StreamId = CostStreamId,
            Ordinal = previous.Decisions.Length,
            OrderedCandidates = []
        };
        var working = previous.Fork();
        var decision = new NativeRandomIndexProviderV111()
            .Resolve(sourceRequest, working.Streams);
        var next = working with
        {
            Decisions = working.Decisions.Append(decision).ToArray()
        };
        Validate(next);
        return (next, decision);
    }

    public static void Validate(SemanticOtherChanceStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ProviderId != ProviderId || state.Streams is null ||
            state.Decisions is null ||
            state.Streams.Streams.Count(stream =>
                stream.StreamId == CostStreamId) != 1)
            throw new InvalidDataException(
                "Invalid engine-owned non-Nature cost stream ownership.");
        var ids = state.Streams.Streams.Select(x => x.StreamId).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidDataException("Duplicate private RNG stream owners.");
        for (int ordinal = 0; ordinal < state.Decisions.Length; ordinal++)
        {
            var decision = state.Decisions[ordinal];
            decision.Validate();
            if (decision.ProviderMode != "native-v111" ||
                decision.Request.StreamId != CostStreamId ||
                decision.Request.Ordinal != ordinal ||
                decision.Request.SiteId is not (
                    "combat.card_draw.relic_energy_cost" or
                    "combat.card_draw.slither_energy_cost") ||
                decision.NativeCursorBefore is null ||
                decision.NativeCursorAfter is null)
                throw new InvalidDataException(
                    "Private non-Nature chance transcript has invalid provenance.");
        }
    }
}
