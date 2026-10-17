namespace Sts2Emulator.Core;

/// <summary>
/// Result of resuming one mechanics checkpoint under a saved controlled
/// outcome prefix. Boundary owners decide how a new request is persisted.
/// </summary>
internal sealed record ControlledChanceReplayResultV111(
    RunState? Run,
    ControlledEventChoiceChanceRequiredException? PendingChance)
{
    public bool Suspended => PendingChance is not null;
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
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(mechanics);

        using var replay = ControlledEventChoiceReplayV111.Begin(
            decisions, priorOrdinalCounts);
        try
        {
            var run = mechanics(checkpoint) ?? throw new InvalidDataException(
                "Controlled mechanics continuation returned a null run.");
            replay.EnsureConsumed();
            return new ControlledChanceReplayResultV111(run, null);
        }
        catch (ControlledEventChoiceChanceRequiredException pending)
        {
            return new ControlledChanceReplayResultV111(null, pending);
        }
    }
}
