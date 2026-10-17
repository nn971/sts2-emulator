namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical checkpoint and draw-stage cursor for one semantic draw
/// segment. Typed draw outcomes are paired through the simulation envelope.
/// </summary>
public sealed record SemanticCombatDrawMechanicsStateV111
{
    public PrototypeMechanicsTransitionV111 Transition { get; init; }

    public RunState Origin
    {
        get => Transition.Origin;
        init => Transition = Transition with { Origin = value };
    }

    public RunState Run
    {
        get => Transition.Current;
        init => Transition = Transition with { Current = value };
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
        Transition = new PrototypeMechanicsTransitionV111(
            PrototypeMechanicsTransitionKindV111.SemanticCombatDraw,
            Origin, Run);
        this.RequestedCount = RequestedCount;
        this.RemainingCount = RemainingCount;
        this.FromHandDraw = FromHandDraw;
        this.AutomaticHandDraw = AutomaticHandDraw;
    }
}
