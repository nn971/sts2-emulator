namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical fixture checkpoint and replay history for the controlled
/// playable combat session. Provider cursors and outcomes live beside it in
/// the simulation envelope.
/// </summary>
public sealed record ControlledPlayableCombatMechanicsStateV111
{
    private ControlledIntentStateV111 _origin = null!;
    private string[] _playerActions = [];

    public string Seed { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? FixtureEncounterId { get; init; }
    public ControlledIntentStateV111 Origin
    {
        get => _origin.Fork();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _origin = value.Fork();
        }
    }
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public string[] PlayerActions
    {
        get => (string[])_playerActions.Clone();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _playerActions = (string[])value.Clone();
        }
    }

    internal ControlledPlayableCombatMechanicsStateV111 AppendPlayerAction(
        string actionId, RunState run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(run);
        return this with
        {
            Run = run,
            PlayerActions = PlayerActions.Append(actionId).ToArray()
        };
    }

    public string Mode { get; init; }

    public ControlledPlayableCombatMechanicsStateV111(
        string Seed,
        ControlledIntentStateV111 Origin,
        RunState Run,
        string[] PlayerActions,
        string Mode,
        string? FixtureEncounterId = null)
    {
        this.Seed = Seed;
        this.FixtureEncounterId = FixtureEncounterId;
        this.Origin = Origin;
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ControlledPlayableCombat,
            null, Run);
        this.PlayerActions = PlayerActions;
        this.Mode = Mode;
    }
}
