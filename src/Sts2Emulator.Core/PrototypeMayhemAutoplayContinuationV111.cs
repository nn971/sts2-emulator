namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    internal static bool IsMayhemTopAutoplayPipelineOperation(
        CombatState combat, PrototypeQueuedOperation operation)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(operation);
        var pipeline = combat.AutomaticPipelineContinuation;
        if (operation.Kind !=
                PrototypeCombatEffectKind.AutoPlayTopDrawCards ||
            operation.SourceKind != PrototypeEffectSourceKind.Power ||
            !combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.mayhem" && power.Stacks > 0) ||
            pipeline is null)
            return false;

        bool openingContinuation =
            pipeline.OpeningStage ==
                PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy &&
            pipeline.NextStepIndex ==
                PrototypeContent.Rules.EndTurnPipeline.Length &&
            pipeline.PendingPostDispatchEventKind is null;

        var autoPrePlayIndex = Array.FindIndex(
            PrototypeContent.Rules.EndTurnPipeline,
            step => step.Kind == PrototypeAutomaticStepKind.DispatchCombatEvent &&
                    step.EventKind ==
                        PrototypeCombatEventKind.AutoPrePlayPhaseEntered);
        bool turnContinuation = autoPrePlayIndex >= 0 &&
            pipeline.OpeningStage is null &&
            pipeline.NextStepIndex == autoPrePlayIndex + 1 &&
            pipeline.PendingPostDispatchEventKind ==
                PrototypeCombatEventKind.AutoPrePlayPhaseEntered;

        return openingContinuation || turnContinuation;
    }

    /// <summary>
    /// The only automatic-pipeline suspension supported for a semantic
    /// AutoPlayTopDrawCards operation is Mayhem's AutoPrePlay trigger. Its
    /// saved cursor must point at the post-dispatch policy boundary, where
    /// History Course runs before the next opening or turn stage.
    /// </summary>
    internal static bool IsMayhemTopAutoplayDrawContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        var draw = pending.ControlledSemanticDraw;
        var operation = pending.OuterChoiceContinuation?.Operations
            .FirstOrDefault();
        if (pending.ChoiceId != "controlled_semantic_draw" ||
            draw is not { FromHandDraw: false, RemainingCount: > 0 } ||
            operation is null || operation.Amount != draw.RemainingCount ||
            operation.DeferredDrawnCardInstanceIds is { } staged &&
                (staged.Distinct().Count() != staged.Length ||
                 staged.Any(id => !combat.PlayCardIds.Contains(id))) ||
            !IsMayhemTopAutoplayPipelineOperation(combat, operation) ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode,
                  Pending: { Purpose: "draw", TopOffset: 0 } })
            return false;
        return true;
    }
}
