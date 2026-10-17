namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical fixture checkpoint and replay history for the controlled
/// playable combat session. Provider cursors and outcomes live beside it in
/// the simulation envelope.
/// </summary>
public sealed record ControlledPlayableCombatMechanicsStateV111
{
    public string Seed { get; init; }
    public ControlledIntentStateV111 Origin { get; init; }
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public string[] PlayerActions { get; init; }
    public string Mode { get; init; }

    public ControlledPlayableCombatMechanicsStateV111(
        string Seed,
        ControlledIntentStateV111 Origin,
        RunState Run,
        string[] PlayerActions,
        string Mode)
    {
        this.Seed = Seed;
        this.Origin = Origin;
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ControlledPlayableCombat,
            null, Run);
        this.PlayerActions = PlayerActions;
        this.Mode = Mode;
    }
}
