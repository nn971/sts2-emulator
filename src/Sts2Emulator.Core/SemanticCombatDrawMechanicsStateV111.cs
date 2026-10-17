namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical checkpoint and draw-stage cursor for one semantic draw
/// segment. Typed draw outcomes are paired through the simulation envelope.
/// </summary>
public sealed record SemanticCombatDrawMechanicsStateV111(
    RunState Origin,
    RunState Run,
    int RequestedCount,
    int RemainingCount,
    bool FromHandDraw,
    bool AutomaticHandDraw);
