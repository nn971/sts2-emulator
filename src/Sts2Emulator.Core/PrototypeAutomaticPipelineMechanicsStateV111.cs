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

    /// <summary>
    /// True when this is a well-formed ordinary pipeline cursor, including
    /// the completed boundary after the final step. Opening and
    /// post-dispatch continuations are separate cursor forms.
    /// </summary>
    public bool IsValidOrdinaryCursor =>
        OpeningStage is null && PendingPostDispatchEventKind is null &&
        HasValidIndex;

    public bool IsAtStepIndex(int expectedIndex) =>
        IsValidOrdinaryCursor && NextStepIndex == expectedIndex;

    public int RequireResumeStepIndex()
    {
        if (OpeningStage is not null || !HasValidIndex)
            throw new InvalidDataException(
                "Automatic pipeline cursor has no resumable step index.");
        return NextStepIndex;
    }

    public bool HasValidIndex
    {
        get
        {
            var pipeline = PrototypeContent.Rules.EndTurnPipeline;
            return NextStepIndex >= 0 && NextStepIndex <= pipeline.Length;
        }
    }

    public bool HasRemainingSteps =>
        IsValidOrdinaryCursor &&
        NextStepIndex < PrototypeContent.Rules.EndTurnPipeline.Length;

    public bool IsAtOpeningBoundary(PrototypeOpeningCombatStage stage) =>
        OpeningStage == stage && PendingPostDispatchEventKind is null &&
        NextStepIndex == PrototypeContent.Rules.EndTurnPipeline.Length;

    /// <summary>
    /// True when the ordinary cursor has passed the specified event dispatch.
    /// Opening and post-dispatch continuations remain separate boundaries.
    /// </summary>
    public bool HasPassedDispatch(PrototypeCombatEventKind eventKind)
    {
        if (!IsValidOrdinaryCursor)
            return false;
        var pipeline = PrototypeContent.Rules.EndTurnPipeline;
        for (var index = 0; index < pipeline.Length; index++)
        {
            if (pipeline[index] is
                { Kind: PrototypeAutomaticStepKind.DispatchCombatEvent,
                  EventKind: { } dispatchedEvent } &&
                dispatchedEvent == eventKind)
                return NextStepIndex > index;
        }
        return false;
    }

    /// <summary>
    /// True when the cursor points immediately after the requested ordinary
    /// pipeline step, with no opening or post-dispatch boundary active.
    /// </summary>
    public bool IsImmediatelyAfterStep(
        PrototypeAutomaticStepKind completedStep)
    {
        if (!IsValidOrdinaryCursor)
            return false;
        var pipeline = PrototypeContent.Rules.EndTurnPipeline;
        return NextStepIndex > 0 && NextStepIndex <= pipeline.Length &&
            pipeline[NextStepIndex - 1].Kind == completedStep;
    }

    public bool IsImmediatelyAfterDispatch(
        PrototypeCombatEventKind eventKind)
    {
        if (OpeningStage is not null ||
            PendingPostDispatchEventKind != eventKind || !HasValidIndex)
            return false;
        var pipeline = PrototypeContent.Rules.EndTurnPipeline;
        return NextStepIndex > 0 && NextStepIndex <= pipeline.Length &&
            pipeline[NextStepIndex - 1] is
                { Kind: PrototypeAutomaticStepKind.DispatchCombatEvent,
                  EventKind: { } previousEvent } &&
            previousEvent == eventKind;
    }

    public PrototypeAutomaticPipelineMechanicsStateV111 AdvancePastStep(
        PrototypeAutomaticStepKind completedStep)
    {
        if (!IsAtStep(completedStep))
            throw new InvalidDataException(
                "Automatic pipeline cannot advance past a different step.");
        return this with { NextStepIndex = NextStepIndex + 1 };
    }

    public bool TryGetCurrentStepKind(
        out PrototypeAutomaticStepKind stepKind)
    {
        stepKind = default;
        if (!IsValidOrdinaryCursor)
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
        if (OpeningStage == PrototypeOpeningCombatStage.PlayerSideStart)
        {
            if (combat.Turn != 1 ||
                combat.PendingChoice is not null ||
                combat.PendingKnowledgeDemonChoice is not null ||
                !IsAtOpeningBoundary(
                    PrototypeOpeningCombatStage.PlayerSideStart))
                throw new InvalidOperationException(
                    "Invalid initial player-side opening stage.");
        }
        else if (OpeningStage == PrototypeOpeningCombatStage.InitialEnemyIntents)
        {
            if (combat.Turn != 1 || combat.IsPlayerTurn ||
                combat.PendingChoice is not null ||
                combat.PendingKnowledgeDemonChoice is not null ||
                !IsAtOpeningBoundary(
                    PrototypeOpeningCombatStage.InitialEnemyIntents))
                throw new InvalidOperationException(
                    "Invalid suspended initial-enemy-intent stage.");
        }
        else if (OpeningStage is { } opening &&
            (!Enum.IsDefined(opening)
                || opening <= PrototypeOpeningCombatStage.PlayerSideStart
                || combat.Turn != 1 || !combat.IsPlayerTurn
                || combat.PendingKnowledgeDemonChoice is not null
                || !IsAtOpeningBoundary(opening)))
            throw new InvalidOperationException(
                "Invalid suspended opening-combat stage.");

        if (OpeningStage is null &&
            (!HasValidIndex ||
             PendingPostDispatchEventKind is { } eventKind &&
                (!Enum.IsDefined(eventKind) ||
                 !IsImmediatelyAfterDispatch(eventKind))))
            throw new InvalidOperationException(
                "Invalid suspended automatic-combat pipeline.");
    }
}
