namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint for one suspended end-turn continuation. Typed chance
/// decisions and their chronology are paired through the simulation envelope.
/// </summary>
public sealed record SemanticEndTurnMechanicsStateV111(
    RunState Origin,
    RunState Run,
    string NativeEncounterEntry,
    ControlledTurnIntentsV111? Intents,
    SemanticCombatDrawV111? HandDraw,
    bool Complete = false,
    RunState? PlayerActionSource = null,
    string? PlayerActionId = null);
