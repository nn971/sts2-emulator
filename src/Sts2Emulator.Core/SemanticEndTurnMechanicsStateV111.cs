namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint for one suspended end-turn continuation. Typed chance
/// decisions and their chronology are paired through the simulation envelope.
/// </summary>
public sealed record SemanticEndTurnMechanicsStateV111
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

    public string NativeEncounterEntry { get; init; }
    public ControlledTurnIntentsV111? Intents { get; init; }
    public SemanticCombatDrawV111? HandDraw { get; init; }
    public bool Complete { get; init; }
    public RunState? PlayerActionSource { get; init; }
    public string? PlayerActionId { get; init; }

    public SemanticEndTurnMechanicsStateV111(
        RunState Origin,
        RunState Run,
        string NativeEncounterEntry,
        ControlledTurnIntentsV111? Intents,
        SemanticCombatDrawV111? HandDraw,
        bool Complete = false,
        RunState? PlayerActionSource = null,
        string? PlayerActionId = null)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.SemanticEndTurn,
            Origin, Run);
        this.NativeEncounterEntry = NativeEncounterEntry;
        this.Intents = Intents;
        this.HandDraw = HandDraw;
        this.Complete = Complete;
        this.PlayerActionSource = PlayerActionSource;
        this.PlayerActionId = PlayerActionId;
    }
}
