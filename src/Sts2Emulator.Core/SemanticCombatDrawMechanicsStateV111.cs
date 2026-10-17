namespace Sts2Emulator.Core;

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

    public int RequestedCount { get; init; }
    public int RemainingCount { get; init; }
    public bool FromHandDraw { get; init; }
    public bool AutomaticHandDraw { get; init; }

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
