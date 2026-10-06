namespace Sts2Emulator.Core;

public sealed record TransitionResult(RunState State, string[] Diagnostics);

/// <summary>
/// Canonical engine boundary. Implementations should be deterministic because all
/// continuation-relevant hidden state, including RNG, belongs to RunState.
/// </summary>
public interface IDeterministicEngine
{
    IReadOnlyList<GameAction> GetLegalActions(RunState state);

    TransitionResult Step(RunState state, GameAction action);
}
