namespace Sts2Emulator.Core;

/// <summary>
/// Result of resuming one mechanics checkpoint under a saved controlled
/// outcome prefix. Boundary owners decide how a new request is persisted.
/// </summary>
internal sealed record ControlledChanceReplayResultV111(
    RunState? Run,
    ControlledChanceRequiredExceptionV111? PendingChance)
{
    public bool Suspended => PendingChance is not null;
}

internal sealed record ControlledStochasticTypedContinuationResultV111<TValue>(
    TValue? Value,
    ControlledStochasticContinuationStateV111? Continuation)
    where TValue : class
{
    public bool Suspended => Continuation is not null;
}

internal abstract class ControlledChanceRequiredExceptionV111(
    RandomIndexRequestV111 request,
    string[]? candidateLabels,
    string message) : Exception(message)
{
    private readonly string[]? _candidateLabels = candidateLabels is null
        ? null : (string[])candidateLabels.Clone();

    public RandomIndexRequestV111 Request { get; } = request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };
    public string[]? CandidateLabels => _candidateLabels is null
        ? null : (string[])_candidateLabels.Clone();
}

internal interface IControlledChanceReplayScopeV111 : IDisposable
{
    void EnsureConsumed();
}

/// <summary>
/// Shared replay and suspension boundary for mechanics continuations. Its
/// generic entry points accept any immutable checkpoint/result pair, while
/// caller-owned adapters retain checkpoint selection, supported-site policy,
/// and serialized continuation types.
/// </summary>
internal static class ControlledChanceContinuationExecutorV111
{
    private sealed record SelectedValueV111<TValue>(TValue Value)
        where TValue : struct;

    internal static bool RequestsMatch<TRequest>(
        TRequest? left, TRequest? right) where TRequest : class =>
        ControlledStochasticChanceReplayV111.RequestsMatch(left, right);

    internal static bool CandidateLabelsMatch(
        string[]? left, string[]? right) =>
        ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
            left, right);

    internal static int IndexOfCandidateLabel(
        string[]? candidateLabels, int supportCount, string candidateLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateLabel);
        if (supportCount < 0)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        var labels = candidateLabels ??
            throw new InvalidOperationException(
                "This controlled request has no semantic candidate labels.");
        if (labels.Length != supportCount ||
            labels.Any(string.IsNullOrWhiteSpace) ||
            labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
            throw new InvalidDataException(
                "Controlled candidate labels do not match their source support.");
        var index = Array.FindIndex(labels, label =>
            StringComparer.Ordinal.Equals(label, candidateLabel));
        return index >= 0 ? index : throw new ArgumentOutOfRangeException(
            nameof(candidateLabel),
            "The selected outcome is outside the current support.");
    }

    /// <summary>
    /// Runs mechanics from its checkpoint with a saved outcome prefix and
    /// returns either the completed run or a versioned continuation envelope
    /// for the next unresolved provider request.
    /// </summary>
    public static ControlledStochasticContinuationResultV111 CaptureStochastic(
        RunState checkpoint,
        string schemaId,
        ControlledStochasticDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null,
        IRandomIndexProviderV111? indexFallbackProvider = null)
    {
        var result = CaptureTyped(
            checkpoint, schemaId, decisions, mechanics, requestFilter,
            indexFallbackProvider);
        return new ControlledStochasticContinuationResultV111(
            result.Value, result.Continuation);
    }

    /// <summary>
    /// Runs any immutable mechanics checkpoint with a saved typed outcome
    /// prefix. Run-based sessions use the compatibility wrapper above;
    /// mechanics such as standalone map generation can share the same
    /// chronological request capture without manufacturing a RunState. A
    /// filtered index fallback can resolve later matching index calls after
    /// the saved prefix, for source-owned tail capture.
    /// </summary>
    public static ControlledStochasticTypedContinuationResultV111<TValue>
        CaptureTyped<TCheckpoint, TValue>(
            TCheckpoint checkpoint,
            string schemaId,
            ControlledStochasticDecisionV111[] decisions,
            Func<TCheckpoint, TValue> mechanics,
            Func<ControlledStochasticRequestV111, bool>? requestFilter = null,
            IRandomIndexProviderV111? indexFallbackProvider = null,
            bool allowMissingCandidateLabels = false)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mechanics);
        if (schemaId is not (
                ControlledStochasticContinuationStateV111.CurrentSchemaId or
                ControlledStochasticContinuationStateV111.MixedPrimitiveSchemaId))
            throw new InvalidDataException(
                "Unsupported controlled stochastic continuation schema.");
        foreach (var decision in decisions)
        {
            ArgumentNullException.ThrowIfNull(decision);
            decision.Validate();
            if (schemaId ==
                    ControlledStochasticContinuationStateV111.CurrentSchemaId &&
                !decision.IsIndexOrUnitFloat())
                throw new InvalidDataException(
                    "Version 1 stochastic continuations contain only index and unit-float outcomes.");
        }
        if (indexFallbackProvider is not null &&
            (requestFilter is null || decisions.Any(decision =>
                decision.IndexDecision is null)))
            throw new InvalidDataException(
                "Index fallback requires a filtered index-only decision prefix.");

        using var replay = ControlledStochasticChanceReplayV111.Begin(
            decisions, allowMissingCandidateLabels);
        using var providers = indexFallbackProvider is not null
            ? StochasticProviderScopeV111.EnterFiltered(
                new ControlledStochasticIndexFallbackProviderV111(
                    replay, indexFallbackProvider), null, requestFilter!)
            : requestFilter is null
                ? StochasticProviderScopeV111.EnterBoundary(replay)
                : StochasticProviderScopeV111.EnterFilteredBoundary(
                    replay, requestFilter);
        try
        {
            var value = mechanics(checkpoint) ?? throw new InvalidDataException(
                "Typed stochastic continuation returned a null mechanics value.");
            replay.EnsureConsumed();
            return new ControlledStochasticTypedContinuationResultV111<TValue>(
                value, null);
        }
        catch (ControlledStochasticChanceRequiredExceptionV111 pending)
        {
            var continuation = new ControlledStochasticContinuationStateV111(
                schemaId,
                decisions.Select(decision => decision.Fork()).ToArray(),
                pending.Request,
                pending.CandidateLabels);
            continuation.Validate();
            return new ControlledStochasticTypedContinuationResultV111<TValue>(
                null, continuation);
        }
    }

    /// <summary>
    /// Resolves the exact pending request in a saved continuation, replays the
    /// mechanics from its checkpoint, and captures the next request if one
    /// remains. The state schema governs which primitive types are accepted.
    /// </summary>
    public static ControlledStochasticContinuationResultV111 ResolveStochastic(
        RunState checkpoint,
        ControlledStochasticContinuationStateV111 continuation,
        ControlledStochasticDecisionV111 selectedOutcome,
        Func<RunState, RunState> mechanics,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
    {
        var result = ResolveTyped(
            checkpoint, continuation, selectedOutcome, mechanics,
            requestFilter);
        return new ControlledStochasticContinuationResultV111(
            result.Value, result.Continuation);
    }

    /// <summary>
    /// Resolves the exact pending request in a typed continuation and replays
    /// any mechanics checkpoint through its next typed request, independent
    /// of whether that mechanics value is a RunState.
    /// </summary>
    public static ControlledStochasticTypedContinuationResultV111<TValue>
        ResolveTyped<TCheckpoint, TValue>(
            TCheckpoint checkpoint,
            ControlledStochasticContinuationStateV111 continuation,
            ControlledStochasticDecisionV111 selectedOutcome,
            Func<TCheckpoint, TValue> mechanics,
            Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(selectedOutcome);
        ArgumentNullException.ThrowIfNull(mechanics);
        continuation.Validate();
        selectedOutcome.Validate();
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                continuation.PendingRequest, selectedOutcome.ToRequest()) ||
            (continuation.PendingCandidateLabels is null) !=
                (selectedOutcome.CandidateLabels is null) ||
            continuation.PendingCandidateLabels is { } expectedLabels &&
                !expectedLabels.SequenceEqual(
                    selectedOutcome.CandidateLabels!, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Selected stochastic outcome does not match the saved pending request.");

        var decisions = continuation.Decisions
            .Append(selectedOutcome.Fork()).ToArray();
        return CaptureTyped(
            checkpoint, continuation.SchemaId, decisions, mechanics,
            requestFilter);
    }

    /// <summary>
    /// Resolves a boundary-selected index against an existing typed prefix.
    /// It records the selected outcome once for the caller-owned transition
    /// journal, then replays the complete saved prefix through the same scope.
    /// </summary>
    public static ControlledStochasticTypedContinuationResultV111<TValue>
        ResolveTypedSelectedIndex<TCheckpoint, TValue>(
            TCheckpoint checkpoint,
            ControlledStochasticContinuationStateV111 continuation,
            RandomIndexRequestV111 request,
            int selectedIndex,
            Func<TCheckpoint, RandomIndexDecisionV111, TValue> mechanics,
            string[]? candidateLabels = null,
            Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mechanics);
        continuation.Validate();
        request.Validate();
        if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                continuation.PendingRequest,
                ControlledStochasticRequestV111.FromIndex(request)))
            throw new InvalidDataException(
                "Selected index does not match the saved typed request.");

        var selected = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selectedIndex, "controlled-v111");
        selected.Validate();
        RandomBoundaryJournalV111.Record(selected);
        var outcome = ControlledStochasticDecisionV111.FromIndex(
            selected, candidateLabels);
        return ResolveTyped<TCheckpoint, TValue>(
            checkpoint, continuation, outcome,
            savedCheckpoint => mechanics(savedCheckpoint, selected),
            requestFilter);
    }

    /// <summary>
    /// Replays one selected index through the typed executor when a boundary
    /// owns the request's logical ordinal. The mechanics callback receives
    /// the consumed selection only after the exact request has matched.
    /// </summary>
    public static TValue ResolveSelectedIndex<TCheckpoint, TValue>(
        TCheckpoint checkpoint,
        RandomIndexRequestV111 request,
        int selectedIndex,
        Func<TCheckpoint, int, TValue> mechanics)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mechanics);
        request.Validate();

        var typedRequest = ControlledStochasticRequestV111.FromIndex(request);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            [], typedRequest);
        var selectedOutcome = ControlledStochasticDecisionV111.FromIndex(
            new RandomIndexDecisionV111(
                request, selectedIndex, "controlled-v111"));
        var replay = ResolveTyped<TCheckpoint, TValue>(
            checkpoint, continuation, selectedOutcome,
            (savedCheckpoint) =>
            {
                int resolvedIndex = StochasticProviderScopeV111
                    .ResolveIndexAtDeclaredOrdinal(
                        null, request, RngBundle.Empty).SelectedIndex;
                return mechanics(savedCheckpoint, resolvedIndex);
            },
            candidate => ControlledStochasticChanceReplayV111.RequestsMatch(
                typedRequest, candidate));
        return replay.Value ?? throw new InvalidDataException(
            "Selected index replay suspended outside its exact request.");
    }

    /// <summary>
    /// Value-result counterpart to <see cref="ResolveSelectedIndex"/>.
    /// It uses the same exact request replay and boxes only the mechanics
    /// result for the reference-type continuation envelope.
    /// </summary>
    public static TValue ResolveSelectedIndexValue<TCheckpoint, TValue>(
        TCheckpoint checkpoint,
        RandomIndexRequestV111 request,
        int selectedIndex,
        Func<TCheckpoint, int, TValue> mechanics)
        where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        var replay = ResolveSelectedIndex<TCheckpoint,
            SelectedValueV111<TValue>>(
                checkpoint, request, selectedIndex,
                (savedCheckpoint, replayedIndex) =>
                    new SelectedValueV111<TValue>(
                        mechanics(savedCheckpoint, replayedIndex)));
        return replay.Value;
    }

    public static ControlledChanceReplayResultV111 Resume(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts = null,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null,
        string[][]? decisionCandidateLabels = null)
        => ResumeCore(
            checkpoint,
            decisions,
            mechanics,
            () => ControlledEventChoiceReplayV111.Begin(
                decisions, priorOrdinalCounts, decisionCandidateLabels),
            new ControlledEventChoiceRandomIndexProviderV111(
                requestOrdinalIncludesCurrentOperationPrefix: false),
            requestFilter);

    public static ControlledChanceReplayResultV111 ResumeEnemyAction(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        string[][]? decisionCandidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        if (decisionCandidateLabels is not null &&
            decisionCandidateLabels.Length != decisions.Length)
            throw new InvalidDataException(
                "Enemy-action candidate-label prefix has the wrong length.");
        var typedDecisions = decisions.Select((decision, index) =>
            ControlledStochasticDecisionV111.FromIndex(
                decision,
                decisionCandidateLabels is { } labels &&
                    labels[index] is { } candidateLabels
                    ? candidateLabels : null)).ToArray();
        using var replayJournal = RandomBoundaryJournalV111.Begin();
        var replay = CaptureTyped(
            checkpoint,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            typedDecisions,
            mechanics,
            requestFilter: request =>
            {
                if (request.IndexRequest is { } indexRequest &&
                    PrototypeControlledEnemyActionContinuationV111
                        .IsEnemyActionSite(indexRequest.SiteId))
                    return true;
                throw new NotSupportedException(
                    "UNSUPPORTED_CONTROLLED_CHANCE site=" + request.SiteId +
                    " operation=" + OperationLabel(request) +
                    " requires a saved enemy-action continuation.");
            },
            allowMissingCandidateLabels: true);
        if (replay.Continuation is { } pending)
        {
            var request = pending.PendingRequest.IndexRequest ??
                throw new NotSupportedException(
                    "UNSUPPORTED_CONTROLLED_CHANCE site=" +
                    pending.PendingRequest.SiteId +
                    " operation=non_index requires a mixed enemy-action continuation.");
            return new ControlledChanceReplayResultV111(
                null, new ControlledEnemyActionChanceRequiredException(
                    request, pending.PendingCandidateLabels));
        }
        return new ControlledChanceReplayResultV111(
            replay.Value ?? throw new InvalidDataException(
                "Enemy-action replay returned neither a run nor a pending request."),
            null);
    }

    private static string OperationLabel(
        ControlledStochasticRequestV111 request) =>
        request.IndexRequest?.OperationKind ??
        (request.UnitFloatRequest is not null ? "unit_float" :
        request.RangedFloatRequest is not null ? "ranged_float" :
        request.RangedDoubleRequest is not null ? "ranged_double" :
        request.GaussianIntRequest is not null ? "gaussian_int" :
        request.GaussianDoubleRequest is not null ? "gaussian_double" :
        "unknown");

    private static ControlledChanceReplayResultV111 ResumeCore(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        Func<IControlledChanceReplayScopeV111> beginReplay,
        IRandomIndexProviderV111 replayProvider,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentNullException.ThrowIfNull(beginReplay);
        ArgumentNullException.ThrowIfNull(replayProvider);

        using var replay = beginReplay();
        using var providers = requestFilter is null
            ? StochasticProviderScopeV111.Enter(
                replayProvider, new FailClosedControlledUnitFloatProviderV111())
            : StochasticProviderScopeV111.EnterFiltered(
                replayProvider, new FailClosedControlledUnitFloatProviderV111(),
                requestFilter);
        try
        {
            var run = mechanics(checkpoint) ?? throw new InvalidDataException(
                "Controlled mechanics continuation returned a null run.");
            replay.EnsureConsumed();
            return new ControlledChanceReplayResultV111(run, null);
        }
        catch (ControlledChanceRequiredExceptionV111 pending)
        {
            return new ControlledChanceReplayResultV111(null, pending);
        }
    }

    private sealed class FailClosedControlledUnitFloatProviderV111
        : IRandomUnitFloatProviderV111
    {
        public string Mode => "controlled-v111";

        public RandomUnitFloatDecisionV111 Resolve(
            RandomUnitFloatRequestV111 request, RngBundle streams)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(streams);
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + request.SiteId +
                " operation=unit_float requires a mixed typed continuation.");
        }
    }

    private sealed class ControlledStochasticIndexFallbackProviderV111(
        ControlledStochasticChanceReplayV111 prefix,
        IRandomIndexProviderV111 fallback) : IRandomIndexProviderV111
    {
        public string Mode => prefix.Mode;

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams) =>
            prefix.Remaining > 0
                ? prefix.Resolve(request, streams)
                : fallback.Resolve(request, streams);
    }
}
