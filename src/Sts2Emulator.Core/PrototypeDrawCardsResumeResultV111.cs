namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral transient progress returned by a draw resume. Automatic
/// hand-draw stages can also carry their requested count; choice transcripts
/// remain with their existing stochastic owners.
/// </summary>
internal readonly record struct PrototypeDrawCardsResumeResultV111
{
    public RunState Run { get; }
    public int RemainingCount { get; }
    public int? RequestedCount { get; }
    public PrototypeAutomaticPipelineResumeBoundaryKindV111?
        PipelineBoundary { get; }

    public PrototypeDrawCardsResumeResultV111(
        RunState Run, int RemainingCount, int? RequestedCount = null,
        PrototypeAutomaticPipelineResumeBoundaryKindV111?
            PipelineBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(Run);
        if (RemainingCount < 0 || RequestedCount is < 0 ||
            (PipelineBoundary is { } boundary && !Enum.IsDefined(boundary)))
            throw new InvalidDataException(
                "Draw resume progress contains an invalid count or pipeline boundary.");

        this.Run = Run;
        this.RemainingCount = RemainingCount;
        this.RequestedCount = RequestedCount;
        this.PipelineBoundary = PipelineBoundary;
    }

    internal static PrototypeDrawCardsResumeResultV111 ForHandDraw(
        RunState run, int requestedCount, int remainingCount,
        PrototypeAutomaticPipelineResumeBoundaryKindV111?
            pipelineBoundary = null) =>
        new PrototypeDrawCardsResumeResultV111(
            run, remainingCount, requestedCount, pipelineBoundary)
            .EnsurePipelineBoundary();

    internal PrototypeDrawCardsResumeResultV111 Validate()
    {
        if (Run is null || RemainingCount < 0 || RequestedCount is < 0 ||
            (PipelineBoundary is { } boundary && !Enum.IsDefined(boundary)))
            throw new InvalidDataException(
                "Draw resume progress contains an invalid run, count, or pipeline boundary.");
        return this;
    }

    internal PrototypeDrawCardsResumeResultV111 EnsurePipelineBoundary()
    {
        Validate();
        var resume = PrototypeAutomaticPipelineResumeResultV111
            .FromPipelineProgress(Run, PipelineBoundary);
        return PipelineBoundary == resume.Boundary
            ? this
            : new PrototypeDrawCardsResumeResultV111(
                Run, RemainingCount, RequestedCount, resume.Boundary);
    }

    internal PrototypeAutomaticPipelineResumeResultV111
        ToAutomaticPipelineResumeResult()
    {
        Validate();
        return PrototypeAutomaticPipelineResumeResultV111
            .FromPipelineProgress(Run, PipelineBoundary);
    }

    internal int RequireRequestedCount()
    {
        Validate();
        return RequestedCount ?? throw new InvalidDataException(
            "Draw resume result has no requested hand-draw count.");
    }

    public readonly void Deconstruct(
        out RunState Run, out int RemainingCount, out int? RequestedCount)
    {
        Run = this.Run;
        RemainingCount = this.RemainingCount;
        RequestedCount = this.RequestedCount;
    }
}
