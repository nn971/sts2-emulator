namespace Sts2Emulator.Core;

internal readonly record struct SemanticCombatDrawResumeResultV111(
    RunState Run, int RemainingCount);

/// <summary>
/// Mechanical checkpoint and draw-stage cursor for one semantic draw
/// segment. Typed draw outcomes are paired through the simulation envelope.
/// </summary>
public sealed record SemanticCombatDrawMechanicsStateV111
{
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState Origin
    {
        get => Continuation.Origin!;
        init => Continuation = Continuation with { Origin = value };
    }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    // Fallback projection for standalone draws and completed automatic-draw
    // facades. While DrawPlayerHand is suspended, the pipeline cursor owns
    // these counts and SemanticCombatDrawV111 reads them there.
    public int RequestedCount { get; init; }
    public int RemainingCount { get; init; }
    public bool FromHandDraw { get; init; }
    public bool AutomaticHandDraw { get; init; }

    internal int ProjectRequestedCount() =>
        ReadAutomaticPipelineDrawProgress(Run, AutomaticHandDraw)?.Requested ??
        RequestedCount;

    internal SemanticCombatDrawMechanicsStateV111 SetRequestedCount(
        int value) => this with
        {
            RequestedCount = ReadAutomaticPipelineDrawProgress(
                Run, AutomaticHandDraw) is null ? value : 0
        };

    internal int ProjectRemainingCount() =>
        ReadAutomaticPipelineDrawProgress(Run, AutomaticHandDraw)?.Remaining ??
        RemainingCount;

    internal SemanticCombatDrawMechanicsStateV111 SetRemainingCount(
        int value) => this with
        {
            RemainingCount = ReadAutomaticPipelineDrawProgress(
                Run, AutomaticHandDraw) is null ? value : 0
        };

    internal SemanticCombatDrawMechanicsStateV111 SetRun(RunState value)
    {
        var priorProgress = ReadAutomaticPipelineDrawProgress(
            Run, AutomaticHandDraw);
        var nextProgress = ReadAutomaticPipelineDrawProgress(
            value, AutomaticHandDraw);
        int requested = RequestedCount;
        int remaining = RemainingCount;
        if (nextProgress is not null)
        {
            requested = 0;
            remaining = 0;
        }
        else if (priorProgress is { } progress)
        {
            var nextCursor = value.World?.Combat?.AutomaticPipelineContinuation;
            int? nextRequested = nextCursor is not null &&
                nextCursor.Stochastic.TryGetSemanticHandDrawProgress(
                    nextCursor.Mechanics, out int savedRequested, out _)
                    ? savedRequested
                    : nextCursor?.Stochastic
                        .CompletedSemanticHandDrawRequested;
            requested = nextRequested ??
                (progress.Requested > 0 ? progress.Requested : requested);
            remaining = 0;
        }
        return this with
        {
            Continuation = Continuation with { Current = value },
            RequestedCount = requested,
            RemainingCount = remaining
        };
    }

    internal SemanticCombatDrawMechanicsStateV111 SetAutomaticHandDraw(
        bool value)
    {
        var pipelineProgress = ReadAutomaticPipelineDrawProgress(Run, value);
        return this with
        {
            AutomaticHandDraw = value,
            RequestedCount = pipelineProgress is null ? RequestedCount : 0,
            RemainingCount = pipelineProgress is null ? RemainingCount : 0
        };
    }

    internal bool HasAutomaticPipelineDrawProgress =>
        ReadAutomaticPipelineDrawProgress(Run, AutomaticHandDraw) is not null;

    internal static bool HasAutomaticPipelineDrawCursor(
        RunState run, bool automaticHandDraw) =>
        ReadAutomaticPipelineDrawProgress(run, automaticHandDraw) is not null;

    private static (int Requested, int Remaining)?
        ReadAutomaticPipelineDrawProgress(
            RunState run, bool automaticHandDraw)
    {
        if (!automaticHandDraw ||
            run.World?.Combat?.AutomaticPipelineContinuation is not
                { } cursor)
            return null;
        if (!cursor.Stochastic.TryGetSemanticHandDrawProjection(
                cursor.Mechanics, out var requested, out var remaining))
            return null;
        return (requested, remaining);
    }

    public SemanticCombatDrawMechanicsStateV111(
        RunState Origin,
        RunState Run,
        int RequestedCount,
        int RemainingCount,
        bool FromHandDraw,
        bool AutomaticHandDraw)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.SemanticCombatDraw,
            Origin, Run);
        this.RequestedCount = RequestedCount;
        this.RemainingCount = RemainingCount;
        this.FromHandDraw = FromHandDraw;
        this.AutomaticHandDraw = AutomaticHandDraw;
    }
}
