namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical fixture checkpoint and replay history for the controlled
/// playable combat session. Provider cursors and outcomes live beside it in
/// the simulation envelope.
/// </summary>
public sealed record ControlledPlayableCombatMechanicsStateV111(
    string Seed,
    ControlledIntentStateV111 Origin,
    RunState Run,
    string[] PlayerActions,
    string Mode);
