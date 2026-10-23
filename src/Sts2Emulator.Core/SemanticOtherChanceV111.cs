namespace Sts2Emulator.Core;

/// <summary>
/// Private, explicit, engine-owned Native streams for supported non-Nature
/// chance operations in the two-site pilot. The source-shaped site and stream
/// assignment are provisional until audited against the installed game.
/// This must never be attached to fair player observations.
/// </summary>
public sealed record SemanticOtherChanceStateV111
{
    private RngBundle? _streams;
    private RandomIndexDecisionV111[]? _decisions;
    private Dictionary<string, ulong>? _initialStreamCallCounts;
    private Dictionary<string, NativeRngSnapshot>? _initialStreamSnapshots;

    [System.Text.Json.Serialization.JsonConstructor]
    public SemanticOtherChanceStateV111(
        RngBundle Streams,
        RandomIndexDecisionV111[] Decisions,
        string ProviderId = SemanticOtherChanceEngineV111.ProviderId,
        ulong InitialCallCount = 0,
        Dictionary<string, ulong>? InitialStreamCallCounts = null,
        Dictionary<string, NativeRngSnapshot>? InitialStreamSnapshots = null)
    {
        this.Streams = Streams;
        this.Decisions = Decisions;
        this.ProviderId = ProviderId;
        this.InitialCallCount = InitialCallCount;
        this.InitialStreamCallCounts = InitialStreamCallCounts;
        this.InitialStreamSnapshots = InitialStreamSnapshots;
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

    public Dictionary<string, ulong>? InitialStreamCallCounts
    {
        get => _initialStreamCallCounts is null
            ? null
            : new Dictionary<string, ulong>(
                _initialStreamCallCounts, StringComparer.Ordinal);
        init => _initialStreamCallCounts = value is null
            ? null
            : new Dictionary<string, ulong>(value, StringComparer.Ordinal);
    }

    public Dictionary<string, NativeRngSnapshot>? InitialStreamSnapshots
    {
        get => _initialStreamSnapshots is null
            ? null
            : new Dictionary<string, NativeRngSnapshot>(
                _initialStreamSnapshots, StringComparer.Ordinal);
        init => _initialStreamSnapshots = value is null
            ? null
            : new Dictionary<string, NativeRngSnapshot>(
                value, StringComparer.Ordinal);
    }

    public string ProviderId { get; init; }
    public ulong InitialCallCount { get; init; }

    public SemanticOtherChanceStateV111 Fork() => this with
    {
        Streams = _streams?.Fork()!,
        Decisions = CloneDecisions(_decisions)!,
        InitialStreamCallCounts = _initialStreamCallCounts,
        InitialStreamSnapshots = _initialStreamSnapshots
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
        "engine-owned-native-v111-index-streams-unverified-v1";
    public const string LegacyProviderId =
        "engine-owned-native-v111-cost-stream-unverified-v1";
    public const string CostStreamId = "run.combat_energy_costs";

    /// <summary>
    /// Caller supplies the complete source-owned run bundle after entry
    /// setup. The owner keeps all named streams so registered engine-owned
    /// Native index requests can advance their actual source stream without
    /// making a third Nature choice.
    /// </summary>
    public static SemanticOtherChanceStateV111 Begin(
        RngBundle afterOpening, string providerId = ProviderId)
    {
        ArgumentNullException.ThrowIfNull(afterOpening);
        NativeRunRngV111.ValidateBundle(afterOpening);
        if (!IsSupportedProviderId(providerId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE provider=" + providerId);
        var costStream = afterOpening.GetStream(CostStreamId);
        var initialCallCount = costStream.CallCount ??
            throw new InvalidDataException(
                "Native cost stream is missing its call counter.");
        var legacy = providerId == LegacyProviderId;
        var state = new SemanticOtherChanceStateV111(
            legacy
                ? new RngBundle([costStream.Fork()])
                : afterOpening.Fork(), [], providerId,
            InitialCallCount: initialCallCount,
            InitialStreamCallCounts: legacy ? null : afterOpening.Streams.ToDictionary(
                stream => stream.StreamId,
                stream => stream.CallCount ?? throw new InvalidDataException(
                    "Native stream is missing its call counter."),
                StringComparer.Ordinal),
            InitialStreamSnapshots: legacy ? null : afterOpening.Streams.ToDictionary(
                stream => stream.StreamId,
                NativeRngV111.ToSnapshot,
                StringComparer.Ordinal));
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
        return ResolveNativeIndex(previous, sourceRequest);
    }

    /// <summary>
    /// Commit one registered Native index boundary owned by this private
    /// engine stream bundle. Nature does not receive the request or outcome.
    /// The caller remains responsible for applying the selected index to its
    /// saved mechanics continuation.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveNativeIndex(
        SemanticOtherChanceStateV111 previous,
        RandomIndexRequestV111 request)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(request);
        request.ValidateForNativeProvider();
        if (!StochasticSiteRegistryV111.SupportsEngineOwnedNativeBoundary(
                request.SiteId, request.StreamId, request.OperationKind))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + request.SiteId +
                " stream=" + request.StreamId +
                " operation=" + request.OperationKind);
        if (previous.ProviderId == LegacyProviderId &&
            (request.StreamId != CostStreamId ||
             !IsDrawCostSite(request.SiteId)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE provider=" +
                previous.ProviderId + " site=" + request.SiteId +
                " stream=" + request.StreamId);

        var working = previous.Fork();
        var workingStreams = working.Streams;
        var stream = workingStreams.GetStream(request.StreamId);
        _ = stream.CallCount ??
            throw new InvalidDataException(
                "Native engine-owned stream is missing its call counter.");

        var decision = new NativeRandomIndexProviderV111()
            .Resolve(request, workingStreams);
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
        if (!IsSupportedProviderId(state.ProviderId) || state.Decisions is null)
            throw new InvalidDataException(
                "Invalid engine-owned non-Nature Native stream ownership.");
        var ownedStreams = state.Streams;
        if (ownedStreams is null || ownedStreams.Streams is null)
            throw new InvalidDataException(
                "Engine-owned Native index provider has no stream collection.");
        var streams = ownedStreams.Streams;
        if (state.ProviderId == ProviderId)
        {
            NativeRunRngV111.ValidateBundle(ownedStreams);
        }
        else if (streams.Length == 1)
        {
            if (!ownedStreams.TryGetStream(CostStreamId, out var costStream))
                throw new InvalidDataException(
                    "Legacy engine-owned cost state must own its named cost stream.");
            _ = NativeRngV111.ToSnapshot(costStream);
        }
        else
        {
            NativeRunRngV111.ValidateBundle(ownedStreams);
        }

        var initialCallCounts = state.InitialStreamCallCounts;
        var initialStreamSnapshots = state.InitialStreamSnapshots;
        if (state.ProviderId == ProviderId &&
            (initialCallCounts is null || initialStreamSnapshots is null))
            throw new InvalidDataException(
                "Native index-stream state is missing its cursor origins.");
        if (initialCallCounts is not null)
        {
            if (initialCallCounts.Count != streams.Length ||
                streams.Any(stream =>
                    !initialCallCounts.ContainsKey(stream.StreamId)) ||
                initialStreamSnapshots is null ||
                initialStreamSnapshots.Count != streams.Length ||
                streams.Any(stream =>
                    !initialStreamSnapshots.ContainsKey(stream.StreamId)) ||
                initialStreamSnapshots.Values.Any(snapshot => snapshot is null) ||
                !initialCallCounts.TryGetValue(CostStreamId,
                    out var initialCostCallCount) ||
                initialCostCallCount != state.InitialCallCount)
                throw new InvalidDataException(
                    "Engine-owned Native stream cursor origin is malformed.");
        }
        else if (!state.Decisions.All(decision =>
                    decision.Request.StreamId == CostStreamId &&
                    IsDrawCostSite(decision.Request.SiteId)))
            throw new InvalidDataException(
                "Legacy engine-owned cost state contains another Native index site.");
        else if (initialStreamSnapshots is not null)
            throw new InvalidDataException(
                "Legacy engine-owned provider state has no cursor origins for its snapshots.");

        if (state.ProviderId == LegacyProviderId &&
            (!state.Decisions.All(decision =>
                decision.Request.StreamId == CostStreamId &&
                IsDrawCostSite(decision.Request.SiteId)) ||
             initialCallCounts is not null ||
             initialStreamSnapshots is not null))
            throw new InvalidDataException(
                "Legacy engine-owned provider state contains an expanded index-stream transcript.");

        var observedByStream = new Dictionary<string, ulong>(
            StringComparer.Ordinal);
        var currentSnapshots = initialStreamSnapshots is null
            ? null
            : new Dictionary<string, NativeRngSnapshot>(
                initialStreamSnapshots, StringComparer.Ordinal);
        for (int ordinal = 0; ordinal < state.Decisions.Length; ordinal++)
        {
            var decision = state.Decisions[ordinal];
            decision.Validate();
            var request = decision.Request;
            var streamId = request.StreamId;
            var beforeCursor = decision.NativeCursorBefore;
            var afterCursor = decision.NativeCursorAfter;
            var priorCalls = observedByStream.TryGetValue(streamId,
                out var count) ? count : 0;
            if (decision.ProviderMode != "native-v111" ||
                !StochasticSiteRegistryV111.SupportsEngineOwnedNativeBoundary(
                    request.SiteId, request.StreamId, request.OperationKind) ||
                beforeCursor is null || afterCursor is null)
                throw new InvalidDataException(
                    "Private non-Nature Native index transcript has invalid provenance.");
            if (currentSnapshots is not null)
            {
                if (!currentSnapshots.TryGetValue(streamId,
                        out var expectedCursor) ||
                    beforeCursor != expectedCursor)
                    throw new InvalidDataException(
                        "Private Native index transcript has a discontinuous stream cursor.");
                currentSnapshots[streamId] = afterCursor;
            }
            observedByStream[streamId] = checked(priorCalls + 1);
        }

        foreach (var stream in streams)
        {
            if (initialCallCounts is not null)
            {
                var expectedCallCount = checked(
                    initialCallCounts[stream.StreamId] +
                    (observedByStream.TryGetValue(stream.StreamId,
                        out var count) ? count : 0));
                if (stream.CallCount != expectedCallCount ||
                    NativeRngV111.ToSnapshot(stream) !=
                        currentSnapshots![stream.StreamId])
                    throw new InvalidDataException(
                        "Private Native stream state disagrees with its transcript.");
            }
        }
        var expectedCostCallCount = checked(
            state.InitialCallCount + (ulong)state.Decisions.Count(decision =>
                decision.Request.StreamId == CostStreamId));
        if (WorkingCostStreamCallCount(state) != expectedCostCallCount)
            throw new InvalidDataException(
                "Private non-Nature cost stream cursor disagrees with its transcript.");
    }

    private static bool IsDrawCostSite(string siteId) => siteId is
        StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
        StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId;

    public static bool IsSupportedProviderId(string? providerId) =>
        providerId is ProviderId or LegacyProviderId;
}
