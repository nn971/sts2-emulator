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
/// Shared replay and suspension boundary for run-based mechanics
/// continuations. It owns exact prefix consumption; caller-owned boundary
/// adapters retain their checkpoint selection, supported-site policy, and
/// serialized continuation types.
/// </summary>
internal static class ControlledChanceContinuationExecutorV111
{
    public static ControlledStochasticReplayResultV111 ResumeStochastic(
        RunState checkpoint,
        ControlledStochasticDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        Func<ControlledStochasticRequestV111, bool>? requestFilter = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mechanics);

        using var replay = ControlledStochasticChanceReplayV111.Begin(decisions);
        using var providers = requestFilter is null
            ? StochasticProviderScopeV111.Enter(replay, replay)
            : StochasticProviderScopeV111.EnterFiltered(
                replay, replay, requestFilter);
        try
        {
            var run = mechanics(checkpoint) ?? throw new InvalidDataException(
                "Typed stochastic continuation returned a null run.");
            replay.EnsureConsumed();
            return new ControlledStochasticReplayResultV111(run, null);
        }
        catch (ControlledStochasticChanceRequiredExceptionV111 pending)
        {
            return new ControlledStochasticReplayResultV111(null, pending);
        }
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
