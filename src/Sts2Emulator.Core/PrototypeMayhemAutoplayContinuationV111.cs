namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    internal static bool IsMayhemTopAutoplayPipelineOperation(
        CombatState combat, PrototypeQueuedOperation operation)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Kind !=
                PrototypeCombatEffectKind.AutoPlayTopDrawCards ||
            operation.SourceKind != PrototypeEffectSourceKind.Power ||
            !IsMayhemAutoPrePlayPipelineCursor(combat))
            return false;

        return true;
    }

    /// <summary>
    /// Admit a saved top-autoplay draw or a nested draw/cost frame from one
    /// of its autoplayed cards while the exact Mayhem AutoPrePlay cursor is
    /// suspended. Other pipeline-owned semantic draws remain unsupported.
    /// </summary>
    internal static bool IsMayhemSemanticDrawContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (!IsMayhemAutoPrePlayPipelineCursor(combat) ||
            pending.ControlledSemanticDraw is not { } draw ||
            pending.ChoiceId is not (
                "controlled_semantic_draw" or "controlled_draw_cost") ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;

        var continuation = pending.OuterChoiceContinuation;
        if (continuation is null) return false;
        var operation = continuation.Operations.FirstOrDefault();
        bool topAutoplay = pending.ChoiceId == "controlled_semantic_draw" &&
            draw is { FromHandDraw: false, RemainingCount: > 0 } &&
            operation is not null &&
            operation.Kind == PrototypeCombatEffectKind.AutoPlayTopDrawCards &&
            operation.Amount == draw.RemainingCount &&
            (operation.DeferredDrawnCardInstanceIds is not { } staged ||
             (staged.Distinct().Count() == staged.Length &&
              staged.All(combat.PlayCardIds.Contains))) &&
            IsMayhemTopAutoplayPipelineOperation(combat, operation);
        bool nestedAutoplayCard =
            (pending.ChoiceId != "controlled_draw_cost" ||
             pending.ControlledDrawCost is not null);
        if (nestedAutoplayCard)
        {
            nestedAutoplayCard = false;
            for (var frame = continuation;
                 frame is not null; frame = frame.Parent)
                if (frame.CardPlaySeries is { IsAutoPlay: true })
                {
                    nestedAutoplayCard = true;
                    break;
                }
        }

        if (!topAutoplay && !nestedAutoplayCard) return false;
        if (pending.ChoiceId == "controlled_draw_cost" &&
            pending.ControlledDrawCost is { } cost &&
            (cost.RemainingCount != draw.RemainingCount ||
             cost.FromHandDraw != draw.FromHandDraw ||
             cost.EventDepth != draw.EventDepth))
            return false;
        return true;
    }

    private static bool IsMayhemAutoPrePlayPipelineCursor(
        CombatState combat)
    {
        var pipeline = combat.AutomaticPipelineContinuation;
        if (!combat.PlayerPowers.Any(power =>
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

}
