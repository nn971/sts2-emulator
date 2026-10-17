namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint and applied player-action history for the semantic
/// playable session. Provider provenance and suspended choices live beside
/// it in the simulation envelope.
/// </summary>
public sealed record SemanticPilotMechanicsStateV111
{
    private ControlledPlayableCombatV111 _openingSource = null!;

    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public ControlledPlayableCombatV111 OpeningSource
    {
        get => _openingSource;
        init
        {
            _openingSource = value;
            Continuation = Continuation with { Origin = value.Run };
        }
    }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public SemanticPilotContinuationV111[] History { get; init; }
    public string NativeEncounterEntry { get; init; }
    public string Mode { get; init; }

    public SemanticPilotMechanicsStateV111(
        ControlledPlayableCombatV111 OpeningSource,
        RunState Run,
        SemanticPilotContinuationV111[] History,
        string NativeEncounterEntry,
        string Mode)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.SemanticPilot,
            OpeningSource.Run, Run);
        this.OpeningSource = OpeningSource;
        this.History = History;
        this.NativeEncounterEntry = NativeEncounterEntry;
        this.Mode = Mode;
    }
}
