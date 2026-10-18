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
    public RandomIndexRequestV111 Request { get; } = request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };
    public string[]? CandidateLabels { get; } = candidateLabels is null
        ? null : (string[])candidateLabels.Clone();
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
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
    {
        var result = CaptureTyped(
            checkpoint, schemaId, decisions, mechanics, requestFilter);
        return new ControlledStochasticContinuationResultV111(
            result.Value, result.Continuation);
    }

    /// <summary>
    /// Runs any immutable mechanics checkpoint with a saved typed outcome
    /// prefix. Run-based sessions use the compatibility wrapper above;
    /// mechanics such as standalone map generation can share the same
    /// chronological request capture without manufacturing a RunState.
    /// </summary>
    public static ControlledStochasticTypedContinuationResultV111<TValue>
        CaptureTyped<TCheckpoint, TValue>(
            TCheckpoint checkpoint,
            string schemaId,
            ControlledStochasticDecisionV111[] decisions,
            Func<TCheckpoint, TValue> mechanics,
            Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
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

        using var replay = ControlledStochasticChanceReplayV111.Begin(decisions);
        using var providers = requestFilter is null
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
        if (!ControlledStochasticChanceReplayV111.SameRequest(
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

    public static ControlledChanceReplayResultV111 Resume(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts = null,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
        => ResumeCore(
            checkpoint,
            decisions,
            mechanics,
            () => ControlledEventChoiceReplayV111.Begin(
                decisions, priorOrdinalCounts),
            new ControlledEventChoiceRandomIndexProviderV111(
                requestOrdinalIncludesCurrentOperationPrefix: false),
            requestFilter);

    public static ControlledChanceReplayResultV111 ResumeEnemyAction(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics)
        => ResumeCore(
            checkpoint,
            decisions,
            mechanics,
            () => ControlledEnemyActionReplayV111.Begin(decisions),
            new ControlledEnemyActionRandomIndexProviderV111());

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
}
