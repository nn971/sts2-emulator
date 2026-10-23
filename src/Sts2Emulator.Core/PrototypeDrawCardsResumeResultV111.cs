namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral transient progress returned by a draw resume. Automatic
/// hand-draw stages can also carry their requested count; choice transcripts
/// remain with their existing stochastic owners.
/// </summary>
internal readonly record struct PrototypeDrawCardsResumeResultV111(
    RunState Run, int RemainingCount, int? RequestedCount = null)
{
    internal static PrototypeDrawCardsResumeResultV111 ForHandDraw(
        RunState run, int requestedCount, int remainingCount) =>
        new PrototypeDrawCardsResumeResultV111(
            run, remainingCount, requestedCount).Validate();

    internal PrototypeDrawCardsResumeResultV111 Validate()
    {
        if (Run is null || RemainingCount < 0 || RequestedCount is < 0)
            throw new InvalidDataException(
                "Draw resume progress contains a null run or negative count.");
        return this;
    }

    internal int RequireRequestedCount()
    {
        Validate();
        return RequestedCount ?? throw new InvalidDataException(
            "Draw resume result has no requested hand-draw count.");
    }
}
