namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics cursor for one automatic combat pipeline. Provider-private
/// resume values are paired with this record by the simulation envelope.
/// </summary>
public sealed record PrototypeAutomaticPipelineMechanicsStateV111(
    int NextStepIndex,
    PrototypeCombatEventKind? PendingPostDispatchEventKind = null,
    PrototypeOpeningCombatStage? OpeningStage = null);
