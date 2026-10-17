namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint for one suspended end-turn continuation. Typed chance
/// decisions and their chronology are paired through the simulation envelope.
/// </summary>
public sealed record SemanticEndTurnMechanicsStateV111
{
    private SemanticEndTurnHandDrawProjectionV111? _handDraw;

    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState Origin
    {
        get => Continuation.Origin!;
        init => Continuation = Continuation with { Origin = value };
    }

    public RunState Run
    {
        get => Continuation.Current;
        init
        {
            if (!Complete && _handDraw is not null &&
                HandDraw is { AtBoundary: true } &&
                CanonicalJson.Sha256(Continuation.Current) !=
                    CanonicalJson.Sha256(value))
                _handDraw = _handDraw with
                {
                    ProjectedRun = Continuation.Current.Fork()
                };
            Continuation = Continuation with { Current = value };
        }
    }

    public string NativeEncounterEntry { get; init; }
    public SemanticCombatDrawV111? HandDraw
    {
        get => _handDraw is { } draw
            ? new SemanticCombatDrawV111(
                draw.Origin, draw.ProjectedRun ?? Continuation.Current,
                draw.RequestedCount,
                draw.RemainingCount, draw.FromHandDraw,
                draw.AutomaticHandDraw)
            {
                Stochastic = draw.Stochastic
            }
            : null;
        init
        {
            if (value is null)
            {
                _handDraw = null;
                return;
            }

            _handDraw = new SemanticEndTurnHandDrawProjectionV111(
                value.Origin.Fork(), value.RequestedCount,
                value.RemainingCount, value.FromHandDraw,
                value.AutomaticHandDraw, value.Stochastic.Fork(),
                Complete ? value.Run.Fork() : null);
            if (!Complete)
                Continuation = Continuation with { Current = value.Run };
        }
    }
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
        var checkpoint = PrototypeGameEngine
            .SetControlledTurnIntentContinuation(Run, Intents);
        if (!Complete && HandDraw is not null &&
            CanonicalJson.Sha256(checkpoint) !=
                CanonicalJson.Sha256(HandDraw.Run))
            throw new InvalidDataException(
                "Semantic end-turn and hand-draw checkpoints disagree.");
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.SemanticEndTurn,
            Origin, checkpoint);
        this.NativeEncounterEntry = NativeEncounterEntry;
        this.Complete = Complete;
        this.PlayerActionSource = PlayerActionSource;
        this.PlayerActionId = PlayerActionId;
        this.HandDraw = HandDraw;
    }
}

/// <summary>
/// Semantic draw facade state that belongs to end-turn mechanics without a
/// second current RunState checkpoint. The ordinary pipeline checkpoint is
/// projected into SemanticCombatDrawV111 when an adapter reads HandDraw.
/// </summary>
internal sealed record SemanticEndTurnHandDrawProjectionV111(
    RunState Origin,
    int RequestedCount,
    int RemainingCount,
    bool FromHandDraw,
    bool AutomaticHandDraw,
    SemanticCombatDrawStochasticStateV111 Stochastic,
    RunState? ProjectedRun = null);
