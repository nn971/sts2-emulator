namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint and applied player-action history for the semantic
/// playable session. Provider provenance and suspended choices live beside
/// it in the simulation envelope.
/// </summary>
public sealed record SemanticPilotMechanicsStateV111(
    ControlledPlayableCombatV111 OpeningSource,
    RunState Run,
    SemanticPilotTransitionV111[] History,
    string NativeEncounterEntry,
    string Mode);
