namespace Sts2Emulator.Core;

public sealed record TransitionResult(RunState State, string[] Diagnostics)
{
    private RunState? _state = State.Fork();
    private string[]? _diagnostics = Copy(Diagnostics);

    public RunState State
    {
        get => (_state ?? throw new InvalidDataException(
            "Engine transition result has no run state.")).Fork();
        init => _state = value.Fork();
    }

    public string[] Diagnostics
    {
        get => Copy(_diagnostics)!;
        init => _diagnostics = Copy(value);
    }

    public TransitionResult Fork() => this with
    {
        State = _state ?? throw new InvalidDataException(
            "Engine transition result has no run state."),
        Diagnostics = _diagnostics!
    };

    private static string[]? Copy(string[]? values) => values is null
        ? null
        : (string[])values.Clone();
}

/// <summary>
/// Canonical engine boundary. Implementations should be deterministic because all
/// continuation-relevant hidden state, including RNG, belongs to RunState.
/// </summary>
public interface IDeterministicEngine
{
    IReadOnlyList<GameAction> GetLegalActions(RunState state);

    TransitionResult Step(RunState state, GameAction action);
}
