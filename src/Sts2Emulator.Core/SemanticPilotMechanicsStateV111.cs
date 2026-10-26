namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint and applied player-action history for the semantic
/// playable session. Provider provenance and suspended choices live beside
/// it in the simulation envelope.
/// </summary>
public sealed record SemanticPilotMechanicsStateV111
{
    private ControlledPlayableCombatV111 _openingSource = null!;
    private SemanticPilotContinuationV111[] _history = [];

    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public ControlledPlayableCombatV111 OpeningSource
    {
        get => _openingSource.Fork();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _openingSource = value.Fork();
            Continuation = Continuation with
            { Origin = _openingSource.Run.Fork() };
        }
    }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public SemanticPilotContinuationV111[] History
    {
        get => CopyHistory(_history);
        init => _history = CopyHistory(value);
    }

    internal SemanticPilotMechanicsStateV111 AppendHistory(
        SemanticPilotContinuationV111 transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        transition.Validate();
        return this with
        {
            History = History.Append(transition.Fork()).ToArray()
        };
    }

    public string NativeEncounterEntry { get; init; }
    public string Mode { get; init; }
    internal SemanticPilotEndTurnMechanicsProjectionV111?
        PendingEndTurn { get; init; }

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

    private static SemanticPilotContinuationV111[] CopyHistory(
        SemanticPilotContinuationV111[] history)
    {
        ArgumentNullException.ThrowIfNull(history);
        return history.Select(transition => transition is null
            ? null!
            : transition.Fork()).ToArray();
    }
}

/// <summary>
/// Runless mechanics metadata for the pilot's pending end-turn facade. The
/// pilot mechanics continuation is the sole current-run checkpoint.
/// </summary>
internal sealed record SemanticPilotEndTurnMechanicsProjectionV111(
    RunState Origin,
    string NativeEncounterEntry,
    SemanticEndTurnHandDrawProjectionV111? HandDraw,
    bool Complete,
    RunState? PlayerActionSource,
    string? PlayerActionId,
    ControlledTurnIntentsV111? ControlledTurnIntents)
{
    public static SemanticPilotEndTurnMechanicsProjectionV111 Capture(
        SemanticEndTurnV111 state) => new(
        state.Origin.Fork(), state.NativeEncounterEntry,
        state.Mechanics.HandDrawProjection is { } hand
            ? hand with
            {
                Origin = hand.Origin.Fork(),
                ProjectedRun = hand.ProjectedRun?.Fork()
            }
            : null,
        state.Complete, state.PlayerActionSource?.Fork(),
        state.PlayerActionId, state.Intents?.Fork());

    public SemanticEndTurnV111 Project(
        RunState currentRun,
        SemanticEndTurnStochasticStateV111 stochastic)
    {
        var intents = ControlledTurnIntents?.Fork() ?? currentRun.World?.Combat?
            .AutomaticPipelineContinuation?.ControlledTurnIntents;
        var run = intents is null
            ? currentRun
            : PrototypeGameEngine.SetControlledTurnIntentContinuation(
                currentRun, intents);
        var handDraw = HandDraw is { } hand
            ? new SemanticCombatDrawV111(
                hand.Origin, hand.ProjectedRun ?? run,
                hand.RequestedCount, hand.RemainingCount,
                hand.FromHandDraw, hand.AutomaticHandDraw)
            {
                Stochastic = stochastic.HandDraw ??
                    new SemanticCombatDrawStochasticStateV111()
            }
            : null;
        return new SemanticEndTurnV111(
            Origin, run, NativeEncounterEntry, intents,
            handDraw, [], Complete)
        {
            PlayerActionSource = PlayerActionSource,
            PlayerActionId = PlayerActionId,
            Stochastic = stochastic
        };
    }
}
