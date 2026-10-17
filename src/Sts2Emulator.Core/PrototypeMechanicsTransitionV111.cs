namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics checkpoint for semantic continuations that advance one
/// source-shaped run transition. Boundary-specific cursors and stochastic
/// transcripts remain with their owning continuation.
/// </summary>
public sealed record PrototypeMechanicsTransitionV111(
    PrototypeMechanicsTransitionKindV111 Kind,
    RunState Origin,
    RunState Current)
{
    public PrototypeMechanicsTransitionV111 Fork() => this with
    {
        Origin = Origin.Fork(),
        Current = Current.Fork()
    };

    public void Validate(PrototypeMechanicsTransitionKindV111 expectedKind)
    {
        ArgumentNullException.ThrowIfNull(Origin);
        ArgumentNullException.ThrowIfNull(Current);
        if (!Enum.IsDefined(Kind) || Kind != expectedKind ||
            Origin.RunId != Current.RunId ||
            Origin.RunSeed != Current.RunSeed ||
            Origin.GameBuild != Current.GameBuild ||
            Origin.EmulatorSchema != Current.EmulatorSchema)
            throw new InvalidDataException(
                "Mechanics transition checkpoint has mismatched run identity.");
    }
}

public enum PrototypeMechanicsTransitionKindV111
{
    SemanticEndTurn,
    SemanticCombatDraw,
    SemanticPilot
}
