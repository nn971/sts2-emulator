namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint and applied player-action history for the semantic
/// playable session. Provider provenance and suspended choices live beside
/// it in the simulation envelope.
/// </summary>
public sealed record SemanticPilotMechanicsStateV111
{
    private ControlledPlayableCombatV111 _openingSource = null!;

    public PrototypeMechanicsTransitionV111 Transition { get; init; }

    public ControlledPlayableCombatV111 OpeningSource
    {
        get => _openingSource;
        init
        {
            _openingSource = value;
            Transition = Transition with { Origin = value.Run };
        }
    }

    public RunState Run
    {
        get => Transition.Current;
        init => Transition = Transition with { Current = value };
    }

    public SemanticPilotTransitionV111[] History { get; init; }
    public string NativeEncounterEntry { get; init; }
    public string Mode { get; init; }

    public SemanticPilotMechanicsStateV111(
        ControlledPlayableCombatV111 OpeningSource,
        RunState Run,
        SemanticPilotTransitionV111[] History,
        string NativeEncounterEntry,
        string Mode)
    {
        Transition = new PrototypeMechanicsTransitionV111(
            PrototypeMechanicsTransitionKindV111.SemanticPilot,
            OpeningSource.Run, Run);
        this.OpeningSource = OpeningSource;
        this.History = History;
        this.NativeEncounterEntry = NativeEncounterEntry;
        this.Mode = Mode;
    }
}
