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
    private RandomBoundaryTranscriptEntryV111[]? _boundaryTranscript;

    [System.Text.Json.Serialization.JsonConstructor]
    public SemanticOtherChanceStateV111(
        RngBundle Streams,
        RandomIndexDecisionV111[] Decisions,
        string ProviderId = SemanticOtherChanceEngineV111.ProviderId,
        ulong InitialCallCount = 0,
        Dictionary<string, ulong>? InitialStreamCallCounts = null,
        Dictionary<string, NativeRngSnapshot>? InitialStreamSnapshots = null,
        RandomBoundaryTranscriptEntryV111[]? BoundaryTranscript = null)
    {
        this.Streams = Streams;
        this.Decisions = Decisions;
        this.ProviderId = ProviderId;
        this.InitialCallCount = InitialCallCount;
        this.InitialStreamCallCounts = InitialStreamCallCounts;
        this.InitialStreamSnapshots = InitialStreamSnapshots;
        this.BoundaryTranscript = BoundaryTranscript;
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

    public RandomBoundaryTranscriptEntryV111[]? BoundaryTranscript
    {
        get => CloneTranscript(_boundaryTranscript);
        init => _boundaryTranscript = CloneTranscript(value);
    }

    public string ProviderId { get; init; }
    public ulong InitialCallCount { get; init; }

    public SemanticOtherChanceStateV111 Fork() => this with
    {
        Streams = _streams?.Fork()!,
        Decisions = CloneDecisions(_decisions)!,
        InitialStreamCallCounts = _initialStreamCallCounts,
        InitialStreamSnapshots = _initialStreamSnapshots,
        BoundaryTranscript = _boundaryTranscript
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

    private static RandomBoundaryTranscriptEntryV111[]? CloneTranscript(
        RandomBoundaryTranscriptEntryV111[]? transcript) => transcript?.Select(
            entry => entry is null ? null! : entry.Fork()).ToArray();
}

public static class SemanticOtherChanceEngineV111
{
    public const string ProviderId =
        "engine-owned-native-v111-boundary-streams-unverified-v1";
    public const string IndexOnlyProviderId =
        "engine-owned-native-v111-index-streams-unverified-v1";
    public const string LegacyProviderId =
        "engine-owned-native-v111-cost-stream-unverified-v1";
    public const string CostStreamId = "run.combat_energy_costs";

    /// <summary>
    /// Caller supplies the complete source-owned run bundle after entry
    /// setup. The owner keeps all named streams so registered engine-owned
    /// Native typed boundaries can advance their source stream without
    /// making an additional Nature choice.
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
                StringComparer.Ordinal),
            BoundaryTranscript: providerId == ProviderId ? [] : null);
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
    /// Sample a supported semantic cost request outside the two Nature-owned
    /// sites. The semantic event remains the mechanics continuation; this
    /// method records the corresponding engine-owned Native boundary.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveNonNatureCost(
        SemanticOtherChanceStateV111 previous,
        SemanticCostEventV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        var request = pending.Request;
        if (request.SiteId is
                StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
                StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId)
            return ResolveDrawCost(previous, request);

        if (request.SiteId !=
                StochasticSiteRegistryV111.CombatRandomHandCardEnergyCostSiteId ||
            request.OperationKind != "bounded_integer" ||
            request.SupportCount is < 1 or > 32 ||
            pending.TargetCardInstanceId is null ||
            request.StreamId is not (
                SemanticCostChoiceV111.LegacyHandCostStreamId or
                SemanticCostChoiceV111.NativeHandCostStreamId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + request.SiteId +
                " operation=" + request.OperationKind);

        var callCount = WorkingCostStreamCallCount(previous);
        if (callCount > int.MaxValue)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + request.SiteId +
                " operation=native_ordinal_overflow");
        return ResolveNativeIndex(previous, request with
        {
            StreamId = CostStreamId,
            Ordinal = (int)callCount,
            OrderedCandidates = []
        });
    }

    /// <summary>
    /// Resolve the saved random hand-card target through its registered
    /// Native index boundary while retaining the semantic event for mechanics
    /// application and replay.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveHandCostTarget(
        SemanticOtherChanceStateV111 previous,
        SemanticHandCostSelectionEventV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (pending.SiteId !=
                StochasticSiteRegistryV111.CombatRandomHandCardSelectSiteId ||
            pending.OperationKind !=
                SemanticHandCostSelectionChoiceV111.OperationKind ||
            pending.StreamId is not (
                SemanticHandCostSelectionChoiceV111.StreamId or
                SemanticHandCostSelectionChoiceV111.NativeStreamId))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var streamId = SemanticHandCostSelectionChoiceV111.NativeStreamId;
        var callCount = previous.Streams.GetStream(streamId).CallCount ??
            throw new InvalidDataException(
                "Native hand-cost selection stream has no call counter.");
        if (callCount > int.MaxValue)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=native_ordinal_overflow");
        var request = new RandomIndexRequestV111(
            pending.SiteId, streamId, pending.OwnerId, (int)callCount,
            Enumerable.Range(0, pending.CandidateCardInstanceIds.Length)
                .ToArray(), OperationKind: "choose_index");
        return ResolveNativeIndex(previous, request);
    }

    /// <summary>
    /// Resolve a registered random-enemy target through the private Native
    /// stream owner. The semantic target event remains the mechanics support
    /// and selected enemy identity contract.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveRandomTarget(
        SemanticOtherChanceStateV111 previous,
        SemanticTargetEventV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (pending.StreamId != "run.combat_targets" ||
            pending.OperationKind != "choose_enemy_instance")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var request = new RandomIndexRequestV111(
            pending.SiteId, pending.StreamId, pending.OwnerId,
            pending.Ordinal, pending.CandidateEnemyInstanceIds,
            OperationKind: "choose_index");
        return ResolveNativeIndex(previous, request);
    }

    /// <summary>
    /// Resolve a registered combat generated-card model through the private
    /// Native card-generation stream. The semantic event retains the ordered
    /// model pool and resumes the suspended mechanics operation.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveGeneratedCard(
        SemanticOtherChanceStateV111 previous,
        SemanticGeneratedCardEventV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (pending.SiteId is not (
                StochasticSiteRegistryV111.CombatGeneratedCardSelectSiteId or
                StochasticSiteRegistryV111.CombatRandomDrawAttackCardModelSiteId) ||
            pending.StreamId is not ("prototype.combat" or
                "run.combat_card_generation") ||
            pending.OperationKind != "choose_card_model")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var request = SemanticGenerationChoiceV111.CreateRequest(
            pending.SiteId, pending.OwnerId, pending.Ordinal,
            pending.CandidateCardIds.Length,
            "run.combat_card_generation");
        return ResolveNativeIndex(previous, request);
    }

    /// <summary>
    /// Resolve a registered combat potion model through the private Native
    /// potion-generation stream while retaining the semantic source pool.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolvePotionGeneration(
        SemanticOtherChanceStateV111 previous,
        SemanticPotionGenerationEventV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (pending.SiteId != SemanticPotionGenerationChoiceV111.SiteId ||
            pending.StreamId is not (SemanticPotionGenerationChoiceV111.StreamId or
                SemanticPotionGenerationChoiceV111.NativeStreamId) ||
            pending.OperationKind != "choose_potion_model")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var request = SemanticPotionGenerationChoiceV111.CreateRequest(
            pending.OwnerId, pending.Ordinal,
            pending.CandidatePotionIds.Length,
            SemanticPotionGenerationChoiceV111.NativeStreamId);
        return ResolveNativeIndex(previous, request);
    }

    /// <summary>
    /// Resolve a registered combat card-transform replacement through the
    /// Native card-generation stream while preserving its semantic source
    /// identity and ordered replacement pool.
    /// </summary>
    public static (SemanticOtherChanceStateV111 Next,
        RandomIndexDecisionV111 Decision) ResolveCardTransform(
        SemanticOtherChanceStateV111 previous,
        SemanticCardTransformEventV111 pending)
    {
        Validate(previous);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (pending.SiteId != SemanticCardTransformChoiceV111.SiteId ||
            pending.StreamId is not (SemanticCardTransformChoiceV111.StreamId or
                SemanticCardTransformChoiceV111.NativeStreamId) ||
            pending.OperationKind != "choose_card_model")
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + pending.SiteId +
                " operation=" + pending.OperationKind);

        var request = SemanticCardTransformChoiceV111.CreateRequest(
            pending.OwnerId, pending.Ordinal,
            pending.CandidateCardIds.Length,
            SemanticCardTransformChoiceV111.NativeStreamId);
        return ResolveNativeIndex(previous, request);
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
        var transcript = working.ProviderId != ProviderId
            ? null
            : AppendBoundaryTranscript(working,
                "index",
                new RandomBoundaryEventV111(
                    NextBoundarySequence(working), decision.Request,
                    decision.SelectedIndex, decision.SelectedValue,
                    decision.ProviderMode, decision.NativeCursorBefore,
                    decision.NativeCursorAfter));
        var next = working with
        {
            Streams = workingStreams,
            Decisions = working.Decisions.Append(decision).ToArray(),
            BoundaryTranscript = transcript ?? working.BoundaryTranscript
        };
        Validate(next);
        return (next, decision);
    }

    public static (SemanticOtherChanceStateV111 Next,
        RandomUnitFloatDecisionV111 Decision) ResolveNativeUnitFloat(
        SemanticOtherChanceStateV111 previous,
        RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateNativeBoundaryRequest(
            previous, request.SiteId, request.StreamId, "unit_float");
        var working = previous.Fork();
        var streams = working.Streams;
        var decision = new NativeRandomUnitFloatProviderV111()
            .Resolve(request, streams);
        return PublishNativeBoundary(working, streams, "unit_float",
            new RandomUnitFloatBoundaryEventV111(
                NextBoundarySequence(working), decision.Request,
                decision.SelectedValue, decision.ProviderMode,
                decision.NativeCursorBefore, decision.NativeCursorAfter),
            decision);
    }

    public static (SemanticOtherChanceStateV111 Next,
        RandomRangedFloatDecisionV111 Decision) ResolveNativeRangedFloat(
        SemanticOtherChanceStateV111 previous,
        RandomRangedFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateNativeBoundaryRequest(
            previous, request.SiteId, request.StreamId, "ranged_float");
        var working = previous.Fork();
        var streams = working.Streams;
        var decision = new NativeRandomRangedFloatProviderV111()
            .Resolve(request, streams);
        return PublishNativeBoundary(working, streams, "ranged_float",
            new RandomRangedFloatBoundaryEventV111(
                NextBoundarySequence(working), decision.Request,
                decision.SelectedValue, decision.ProviderMode,
                decision.NativeCursorBefore, decision.NativeCursorAfter),
            decision);
    }

    public static (SemanticOtherChanceStateV111 Next,
        RandomRangedDoubleDecisionV111 Decision) ResolveNativeRangedDouble(
        SemanticOtherChanceStateV111 previous,
        RandomRangedDoubleRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateNativeBoundaryRequest(
            previous, request.SiteId, request.StreamId, "ranged_double");
        var working = previous.Fork();
        var streams = working.Streams;
        var decision = new NativeRandomRangedDoubleProviderV111()
            .Resolve(request, streams);
        return PublishNativeBoundary(working, streams, "ranged_double",
            new RandomRangedDoubleBoundaryEventV111(
                NextBoundarySequence(working), decision.Request,
                decision.SelectedValue, decision.ProviderMode,
                decision.NativeCursorBefore, decision.NativeCursorAfter),
            decision);
    }

    public static (SemanticOtherChanceStateV111 Next,
        RandomGaussianIntDecisionV111 Decision) ResolveNativeGaussianInt(
        SemanticOtherChanceStateV111 previous,
        RandomGaussianIntRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateNativeBoundaryRequest(
            previous, request.SiteId, request.StreamId, "gaussian_int");
        var working = previous.Fork();
        var streams = working.Streams;
        var decision = new NativeRandomGaussianIntProviderV111()
            .Resolve(request, streams);
        return PublishNativeBoundary(working, streams, "gaussian_int",
            new RandomGaussianIntBoundaryEventV111(
                NextBoundarySequence(working), decision.Request,
                decision.SelectedValue, decision.ProviderMode,
                decision.NativeCursorBefore, decision.NativeCursorAfter),
            decision);
    }

    public static (SemanticOtherChanceStateV111 Next,
        RandomGaussianDoubleDecisionV111 Decision) ResolveNativeGaussianDouble(
        SemanticOtherChanceStateV111 previous,
        RandomGaussianDoubleRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateNativeBoundaryRequest(
            previous, request.SiteId, request.StreamId, "gaussian_double");
        var working = previous.Fork();
        var streams = working.Streams;
        var decision = new NativeRandomGaussianDoubleProviderV111()
            .Resolve(request, streams);
        return PublishNativeBoundary(working, streams, "gaussian_double",
            new RandomGaussianDoubleBoundaryEventV111(
                NextBoundarySequence(working), decision.Request,
                decision.SelectedValue, decision.ProviderMode,
                decision.NativeCursorBefore, decision.NativeCursorAfter),
            decision);
    }

    private static (SemanticOtherChanceStateV111 Next, T Decision)
        PublishNativeBoundary<T>(
        SemanticOtherChanceStateV111 previous,
        RngBundle streams,
        string operationKind,
        object boundaryEvent,
        T decision)
    {
        var next = previous with
        {
            Streams = streams,
            BoundaryTranscript = AppendBoundaryTranscript(
                previous, operationKind, boundaryEvent)
        };
        Validate(next);
        return (next, decision);
    }

    private static RandomBoundaryTranscriptEntryV111[]
        AppendBoundaryTranscript(
        SemanticOtherChanceStateV111 state,
        string operationKind,
        object boundaryEvent)
    {
        var transcript = state.BoundaryTranscript ??
            throw new InvalidDataException(
                "Expanded Native provider has no mixed boundary transcript.");
        return transcript.Append(new RandomBoundaryTranscriptEntryV111(
            transcript.Length + 1, operationKind,
            RandomBoundaryTranscriptEntryV111.SerializeEvent(boundaryEvent)))
            .ToArray();
    }

    private static int NextBoundarySequence(
        SemanticOtherChanceStateV111 state) =>
        checked((state.BoundaryTranscript?.Length ?? 0) + 1);

    private static void ValidateNativeBoundaryRequest(
        SemanticOtherChanceStateV111 previous,
        string siteId,
        string streamId,
        string operationKind)
    {
        Validate(previous);
        if (previous.ProviderId != ProviderId ||
            !StochasticSiteRegistryV111.SupportsEngineOwnedNativeBoundary(
                siteId, streamId, operationKind))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OTHER_CHANCE site=" + siteId +
                " stream=" + streamId + " operation=" + operationKind);
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
        if (state.ProviderId is ProviderId or IndexOnlyProviderId)
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
        var expandedProvider = state.ProviderId == ProviderId;
        var indexOnlyProvider = state.ProviderId == IndexOnlyProviderId;
        var boundaryTranscript = state.BoundaryTranscript;
        if ((expandedProvider || indexOnlyProvider) &&
            (initialCallCounts is null || initialStreamSnapshots is null ||
             (expandedProvider && boundaryTranscript is null) ||
             (indexOnlyProvider && boundaryTranscript is not null)))
            throw new InvalidDataException(
                "Native boundary state is missing or has inconsistent cursor origins.");
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
             initialStreamSnapshots is not null ||
             state.BoundaryTranscript is not null))
            throw new InvalidDataException(
                "Legacy engine-owned provider state contains an expanded index-stream transcript.");

        if (expandedProvider)
        {
            ValidateExpandedBoundaryTranscript(
                state, streams, initialCallCounts!,
                initialStreamSnapshots!, boundaryTranscript!);
            return;
        }

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

    private static void ValidateExpandedBoundaryTranscript(
        SemanticOtherChanceStateV111 state,
        RngStreamState[] streams,
        Dictionary<string, ulong> initialCallCounts,
        Dictionary<string, NativeRngSnapshot> initialSnapshots,
        RandomBoundaryTranscriptEntryV111[] transcript)
    {
        if (state.InitialCallCount !=
                SnapshotCallCount(initialSnapshots[CostStreamId]) ||
            initialCallCounts.Any(pair =>
                pair.Value != SnapshotCallCount(initialSnapshots[pair.Key])))
            throw new InvalidDataException(
                "Native stream call-count and cursor origins disagree.");

        var currentSnapshots = new Dictionary<string, NativeRngSnapshot>(
            initialSnapshots, StringComparer.Ordinal);
        var projectedIndexDecisions = new List<RandomIndexDecisionV111>();
        for (int index = 0; index < transcript.Length; index++)
        {
            var entry = transcript[index];
            entry.Validate();
            if (entry.Sequence != index + 1 ||
                entry.ReadProviderMode() != "native-v111")
                throw new InvalidDataException(
                    "Expanded Native boundary transcript order or provider mode is invalid.");

            var boundary = ReadBoundary(entry);
            if (boundary.EventSequence != entry.Sequence ||
                !StochasticSiteRegistryV111.SupportsEngineOwnedNativeBoundary(
                    boundary.SiteId, boundary.StreamId,
                    boundary.OperationKind) ||
                boundary.CursorBefore is null ||
                boundary.CursorAfter is null ||
                !currentSnapshots.TryGetValue(boundary.StreamId,
                    out var expectedCursor) ||
                boundary.CursorBefore != expectedCursor)
                throw new InvalidDataException(
                    "Expanded Native boundary transcript has an invalid site or cursor chain.");
            currentSnapshots[boundary.StreamId] = boundary.CursorAfter;
            if (boundary.IndexDecision is { } indexDecision)
                projectedIndexDecisions.Add(indexDecision);
        }

        if (projectedIndexDecisions.Count != state.Decisions.Length)
            throw new InvalidDataException(
                "Expanded Native index compatibility projection is incomplete.");
        for (int index = 0; index < projectedIndexDecisions.Count; index++)
        {
            if (CanonicalJson.Sha256(projectedIndexDecisions[index]) !=
                CanonicalJson.Sha256(state.Decisions[index]))
                throw new InvalidDataException(
                    "Expanded Native index projection differs from its mixed transcript.");
        }

        foreach (var stream in streams)
        {
            var expected = currentSnapshots[stream.StreamId];
            if (NativeRngV111.ToSnapshot(stream) != expected ||
                stream.CallCount != SnapshotCallCount(expected))
                throw new InvalidDataException(
                    "Expanded Native stream cursor differs from its mixed transcript.");
        }
    }

    private static ulong SnapshotCallCount(NativeRngSnapshot snapshot) =>
        unchecked((uint)snapshot.Counter);

    private static (int EventSequence, string SiteId, string StreamId,
        string OperationKind,
        NativeRngSnapshot? CursorBefore, NativeRngSnapshot? CursorAfter,
        RandomIndexDecisionV111? IndexDecision) ReadBoundary(
        RandomBoundaryTranscriptEntryV111 entry)
    {
        switch (entry.OperationKind)
        {
            case "index":
            {
                var value = entry.DeserializeEvent<RandomBoundaryEventV111>();
                var decision = new RandomIndexDecisionV111(
                    value.Request, value.SelectedIndex, value.ProviderMode,
                    value.NativeCursorBefore, value.NativeCursorAfter);
                return (value.Sequence, value.Request.SiteId, value.Request.StreamId,
                    value.Request.OperationKind, value.NativeCursorBefore,
                    value.NativeCursorAfter, decision);
            }
            case "unit_float":
            {
                var value = entry.DeserializeEvent<RandomUnitFloatBoundaryEventV111>();
                return (value.Sequence, value.Request.SiteId, value.Request.StreamId,
                    "unit_float", value.NativeCursorBefore,
                    value.NativeCursorAfter, null);
            }
            case "ranged_float":
            {
                var value = entry.DeserializeEvent<RandomRangedFloatBoundaryEventV111>();
                return (value.Sequence, value.Request.SiteId, value.Request.StreamId,
                    "ranged_float", value.NativeCursorBefore,
                    value.NativeCursorAfter, null);
            }
            case "ranged_double":
            {
                var value = entry.DeserializeEvent<RandomRangedDoubleBoundaryEventV111>();
                return (value.Sequence, value.Request.SiteId, value.Request.StreamId,
                    "ranged_double", value.NativeCursorBefore,
                    value.NativeCursorAfter, null);
            }
            case "gaussian_int":
            {
                var value = entry.DeserializeEvent<RandomGaussianIntBoundaryEventV111>();
                return (value.Sequence, value.Request.SiteId, value.Request.StreamId,
                    "gaussian_int", value.NativeCursorBefore,
                    value.NativeCursorAfter, null);
            }
            case "gaussian_double":
            {
                var value = entry.DeserializeEvent<RandomGaussianDoubleBoundaryEventV111>();
                return (value.Sequence, value.Request.SiteId, value.Request.StreamId,
                    "gaussian_double", value.NativeCursorBefore,
                    value.NativeCursorAfter, null);
            }
            default:
                throw new InvalidDataException(
                    "Expanded Native transcript has an unknown boundary operation.");
        }
    }

    private static bool IsDrawCostSite(string siteId) => siteId is
        StochasticSiteRegistryV111.CombatDrawRelicEnergyCostSiteId or
        StochasticSiteRegistryV111.CombatDrawSlitherEnergyCostSiteId;

    public static bool IsSupportedProviderId(string? providerId) =>
        providerId is ProviderId or IndexOnlyProviderId or LegacyProviderId;

    public static int DecisionCount(SemanticOtherChanceStateV111? state) =>
        state?.BoundaryTranscript?.Length ?? state?.Decisions.Length ?? 0;
}
