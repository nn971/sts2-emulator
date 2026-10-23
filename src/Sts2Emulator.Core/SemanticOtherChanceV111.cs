namespace Sts2Emulator.Core;

/// <summary>
/// A private, explicit, engine-owned stream for supported non-Nature chance
/// operations in the two-site pilot. The source-shaped site and stream
/// assignment are provisional until audited against the installed game.
/// This must never be attached to fair player observations.
/// </summary>
public sealed record SemanticOtherChanceStateV111
{
    private RngBundle? _streams;
    private RandomIndexDecisionV111[]? _decisions;

    [System.Text.Json.Serialization.JsonConstructor]
    public SemanticOtherChanceStateV111(
        RngBundle Streams,
        RandomIndexDecisionV111[] Decisions,
        string ProviderId = SemanticOtherChanceEngineV111.ProviderId,
        ulong InitialCallCount = 0)
    {
        this.Streams = Streams;
        this.Decisions = Decisions;
        this.ProviderId = ProviderId;
        this.InitialCallCount = InitialCallCount;
    }

    public RngBundle Streams
    {
        get => _streams?.Fork()!;
        init => _streams = value?.Fork();
    }

    public RandomIndexDecisionV111[] Decisions
    {
        get => CloneDecisions(_decisions)!;
        init => _decisions = CloneDecisions(value);
    }

    public string ProviderId { get; init; }
    public ulong InitialCallCount { get; init; }

    public SemanticOtherChanceStateV111 Fork() => this with
    {
        Streams = _streams?.Fork()!,
        Decisions = CloneDecisions(_decisions)!
    };

    public void Deconstruct(
        out RngBundle Streams,
        out RandomIndexDecisionV111[] Decisions,
        out string ProviderId,
        out ulong InitialCallCount)
    {
        Streams = this.Streams;
        Decisions = this.Decisions;
        ProviderId = this.ProviderId;
        InitialCallCount = this.InitialCallCount;
    }

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? decisions) => decisions?.Select(
            decision => decision is null ? null! : decision with
            {
                Request = decision.Request with
                {
                    OrderedCandidates = (int[])decision.Request
                        .OrderedCandidates.Clone()
                }
            }).ToArray();
}

public static class SemanticOtherChanceEngineV111
{
    public const string ProviderId =
        "engine-owned-native-v111-cost-stream-unverified-v1";
    public const string CostStreamId = "run.combat_energy_costs";

    /// <summary>
    /// Caller supplies the complete source-owned run bundle after entry
    /// setup. This owner retains only the cost stream, keeping cost sampling
    /// private and disjoint from Nature's unresolved draw-card decisions.
    /// </summary>
    public static SemanticOtherChanceStateV111 Begin(RngBundle afterOpening)
    {
        ArgumentNullException.ThrowIfNull(afterOpening);
        NativeRunRngV111.ValidateBundle(afterOpening);
        var costStream = afterOpening.GetStream(CostStreamId);
        var streams = new RngBundle([costStream.Fork()]);
        var initialCallCount = costStream.CallCount ??
            throw new InvalidDataException(
                "Native cost stream is missing its call counter.");
        var state = new SemanticOtherChanceStateV111(
            streams, [], InitialCallCount: initialCallCount);
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
                StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
                StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId) ||
            pending.OperationKind != "bounded_integer" ||
            pending.StreamId != "semantic.combat" ||
            pending.SupportCount is < 1 or > 32 ||
            !pending.OwnerId.StartsWith("card_instance:", StringComparison.Ordinal))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var callCount = WorkingCostStreamCallCount(previous);
        if (callCount > int.MaxValue)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=native_ordinal_overflow");
        var sourceRequest = pending with
        {
            StreamId = CostStreamId,
            Ordinal = (int)callCount,
            OrderedCandidates = []
        };
        var working = previous.Fork();
        var workingStreams = working.Streams;
        if (workingStreams.Streams.Length != 1)
        {
            var costStream = workingStreams.GetStream(CostStreamId);
            workingStreams = new RngBundle([costStream.Fork()]);
        }
        var decision = new NativeRandomIndexProviderV111()
            .Resolve(sourceRequest, workingStreams);
        var next = working with
        {
            Streams = workingStreams,
            Decisions = working.Decisions.Append(decision).ToArray()
        };
        Validate(next);
        return (next, decision);
    }

    private static ulong WorkingCostStreamCallCount(
        SemanticOtherChanceStateV111 state) =>
        state.Streams.GetStream(CostStreamId).CallCount ??
        throw new InvalidDataException(
            "Native cost stream is missing its call counter.");

    public static void Validate(SemanticOtherChanceStateV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ProviderId != ProviderId || state.Streams is null ||
            state.Decisions is null)
            throw new InvalidDataException(
                "Invalid engine-owned non-Nature cost stream ownership.");
        if (state.Streams.Streams is null)
            throw new InvalidDataException(
                "Engine-owned cost provider has no stream collection.");
        if (state.Streams.Streams.Length == 1)
        {
            if (!state.Streams.TryGetStream(
                    CostStreamId, out var costStream))
                throw new InvalidDataException(
                    "Engine-owned cost provider must own only its named cost stream.");
            _ = NativeRngV111.ToSnapshot(costStream);
        }
        else
        {
            // Older snapshots stored a full Native run bundle here. Continue
            // to accept that shape and compact it on the next cost resolution.
            NativeRunRngV111.ValidateBundle(state.Streams);
        }
        var expectedCallCount = checked(
            state.InitialCallCount + (ulong)state.Decisions.Length);
        if (WorkingCostStreamCallCount(state) != expectedCallCount)
            throw new InvalidDataException(
                "Private non-Nature cost stream cursor disagrees with its transcript.");
        for (int ordinal = 0; ordinal < state.Decisions.Length; ordinal++)
        {
            var decision = state.Decisions[ordinal];
            var expectedOrdinal = checked(
                state.InitialCallCount + (ulong)ordinal);
            decision.Validate();
            if (decision.ProviderMode != "native-v111" ||
                decision.Request.StreamId != CostStreamId ||
                expectedOrdinal > int.MaxValue ||
                decision.Request.Ordinal != (int)expectedOrdinal ||
                decision.Request.SiteId is not (
                    StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
                    StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId) ||
                decision.NativeCursorBefore is null ||
                decision.NativeCursorAfter is null)
                throw new InvalidDataException(
                    "Private non-Nature chance transcript has invalid provenance.");
        }
    }
}
