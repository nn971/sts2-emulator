namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics cursor for one automatic combat pipeline. Provider-private
/// resume values are paired with this record by the simulation envelope.
/// </summary>
public sealed record PrototypeAutomaticPipelineMechanicsStateV111(
    int NextStepIndex,
    PrototypeCombatEventKind? PendingPostDispatchEventKind = null,
    PrototypeOpeningCombatStage? OpeningStage = null)
{
    /// <summary>
    /// The ordinary pipeline step represented by this cursor. Opening and
    /// post-dispatch continuations are boundary stages rather than pipeline
    /// steps, so they return null.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeAutomaticStepKind? CurrentStepKind
    {
        get
        {
            if (TryGetCurrentStepKind(out var stepKind))
                return stepKind;
            if (OpeningStage is not null ||
                PendingPostDispatchEventKind is not null)
                return null;
            throw new InvalidDataException(
                "Automatic pipeline cursor has no current step.");
        }
    }

    public bool IsAtStep(PrototypeAutomaticStepKind expectedStep) =>
        TryGetCurrentStepKind(out var stepKind) &&
        stepKind == expectedStep;

    public bool TryGetCurrentStepKind(
        out PrototypeAutomaticStepKind stepKind)
    {
        stepKind = default;
        if (OpeningStage is not null ||
            PendingPostDispatchEventKind is not null)
            return false;
        var pipeline = PrototypeContent.Rules.EndTurnPipeline;
        if (NextStepIndex < 0 || NextStepIndex >= pipeline.Length)
            return false;
        stepKind = pipeline[NextStepIndex].Kind;
        return true;
    }

    public void Validate(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (OpeningStage == PrototypeOpeningCombatStage.InitialEnemyIntents)
        {
            if (combat.Turn != 1 || combat.IsPlayerTurn ||
                combat.PendingChoice is not null ||
                combat.PendingKnowledgeDemonChoice is not null ||
                NextStepIndex !=
                    PrototypeContent.Rules.EndTurnPipeline.Length ||
                PendingPostDispatchEventKind is not null)
                throw new InvalidOperationException(
                    "Invalid suspended initial-enemy-intent stage.");
        }
        else if (OpeningStage is { } opening &&
            (!Enum.IsDefined(opening)
                || opening <= PrototypeOpeningCombatStage.PlayerSideStart
                || combat.Turn != 1 || !combat.IsPlayerTurn
                || combat.PendingChoice is null
                || NextStepIndex !=
                    PrototypeContent.Rules.EndTurnPipeline.Length
                || PendingPostDispatchEventKind is not null))
            throw new InvalidOperationException(
                "Invalid suspended opening-combat stage.");

        if (OpeningStage is null &&
            (NextStepIndex < 0 ||
             NextStepIndex > PrototypeContent.Rules.EndTurnPipeline.Length ||
             PendingPostDispatchEventKind is { } eventKind &&
                !Enum.IsDefined(eventKind)))
            throw new InvalidOperationException(
                "Invalid suspended automatic-combat pipeline.");
    }
}
