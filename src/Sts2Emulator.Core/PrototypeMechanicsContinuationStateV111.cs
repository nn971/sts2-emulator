namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics checkpoint for a suspended source-shaped run action.
/// ReferenceNature and semantic transitions retain their origin as well as
/// the current run; controlled continuations that only need a resume
/// checkpoint omit origin.
/// Boundary-specific cursors and stochastic transcripts remain with their
/// owning continuation.
/// </summary>
public sealed record PrototypeMechanicsContinuationStateV111(
    PrototypeMechanicsContinuationKindV111 Kind,
    RunState? Origin,
    RunState Current)
{
    public PrototypeMechanicsContinuationStateV111 Fork() => this with
    {
        Origin = Origin?.Fork(),
        Current = Current.Fork()
    };

    public void Validate(PrototypeMechanicsContinuationKindV111 expectedKind)
    {
        ArgumentNullException.ThrowIfNull(Current);
        bool requiresOrigin = expectedKind is
            PrototypeMechanicsContinuationKindV111.SemanticEndTurn or
            PrototypeMechanicsContinuationKindV111.SemanticCombatDraw or
            PrototypeMechanicsContinuationKindV111.SemanticPilot or
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession or
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction;
        if (!Enum.IsDefined(Kind) || Kind != expectedKind ||
            requiresOrigin != (Origin is not null) ||
            string.IsNullOrWhiteSpace(Current.RunId) ||
            string.IsNullOrWhiteSpace(Current.RunSeed) ||
            (Origin is not null &&
             (string.IsNullOrWhiteSpace(Origin.RunId) ||
              string.IsNullOrWhiteSpace(Origin.RunSeed) ||
              Origin.RunId != Current.RunId ||
              Origin.RunSeed != Current.RunSeed ||
              Origin.GameBuild != Current.GameBuild ||
              Origin.EmulatorSchema != Current.EmulatorSchema)))
            throw new InvalidDataException(
                "Mechanics continuation checkpoint has mismatched run identity.");
    }
}

public enum PrototypeMechanicsContinuationKindV111
{
    SemanticEndTurn,
    SemanticCombatDraw,
    SemanticPilot,
    ControlledPlayableCombat,
    ControlledEnemyAction,
    ControlledRunAction,
    ControlledEventChoice,
    ControlledEventRewardChoice,
    ReferenceNatureRunAction,
    ReferenceNatureRunSession
}
