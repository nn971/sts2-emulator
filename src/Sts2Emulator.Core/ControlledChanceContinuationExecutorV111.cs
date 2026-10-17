namespace Sts2Emulator.Core;

/// <summary>
/// Result of resuming one mechanics checkpoint under a saved controlled
/// outcome prefix. Boundary owners decide how a new request is persisted.
/// </summary>
internal sealed record ControlledChanceReplayResultV111(
    RunState? Run,
    ControlledChanceReplaySuspensionV111? PendingChance)
{
    public bool Suspended => PendingChance is not null;
}

internal sealed record ControlledChanceReplaySuspensionV111(
    RandomIndexRequestV111 Request,
    string[]? CandidateLabels);

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
    public static ControlledChanceReplayResultV111 Resume(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts = null)
        => ResumeCore(
            checkpoint,
            decisions,
            mechanics,
            () => ControlledEventChoiceReplayV111.Begin(
                decisions, priorOrdinalCounts),
            exception => exception is
                ControlledEventChoiceChanceRequiredException pending
                    ? new ControlledChanceReplaySuspensionV111(
                        pending.Request, pending.CandidateLabels)
                    : null);

    public static ControlledChanceReplayResultV111 ResumeEnemyAction(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics)
        => ResumeCore(
            checkpoint,
            decisions,
            mechanics,
            () => ControlledEnemyActionReplayV111.Begin(decisions),
            exception => exception is
                ControlledEnemyActionChanceRequiredException pending
                    ? new ControlledChanceReplaySuspensionV111(
                        pending.Request, null)
                    : null);

    private static ControlledChanceReplayResultV111 ResumeCore(
        RunState checkpoint,
        RandomIndexDecisionV111[] decisions,
        Func<RunState, RunState> mechanics,
        Func<IControlledChanceReplayScopeV111> beginReplay,
        Func<Exception, ControlledChanceReplaySuspensionV111?> captureSuspension)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mechanics);
        ArgumentNullException.ThrowIfNull(beginReplay);
        ArgumentNullException.ThrowIfNull(captureSuspension);

        using var replay = beginReplay();
        try
        {
            var run = mechanics(checkpoint) ?? throw new InvalidDataException(
                "Controlled mechanics continuation returned a null run.");
            replay.EnsureConsumed();
            return new ControlledChanceReplayResultV111(run, null);
        }
        catch (Exception exception)
        {
            var suspension = captureSuspension(exception);
            if (suspension is null) throw;
            return new ControlledChanceReplayResultV111(null, suspension);
        }
    }
}
