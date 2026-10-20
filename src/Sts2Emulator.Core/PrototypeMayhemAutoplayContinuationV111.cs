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
    /// suspended. Late player-turn autoplay is admitted separately by the
    /// shared automatic-pipeline continuation predicate.
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

    internal static bool IsAutomaticPipelineSemanticDrawInsertionContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_draw_insertion",
              ControlledDrawInsertion: { Stage: "position",
                  SelectedCardId: not null,
                  SemanticDrawContinuation: not null,
                  AfterShuffleRelic: not null },
              OuterChoiceContinuation: { } continuation } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;

        bool nestedAutoplay = false;
        for (var frame = continuation; frame is not null;
             frame = frame.Parent)
            if (frame.CardPlaySeries is { IsAutoPlay: true })
            {
                nestedAutoplay = true;
                break;
            }
        if (!nestedAutoplay) return false;

        return IsMayhemAutoPrePlayPipelineCursor(combat) ||
            combat.IsPlayerTurn &&
            IsAtSupportedAutomaticPipelineTargetBoundary(combat);
    }

    internal static bool IsAutomaticPipelineSemanticGenerationContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_generated_card",
              ControlledGeneration: { } generation,
              OuterChoiceContinuation: { } continuation } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            !ValidateControlledGenerationSupport(generation))
            return false;

        return IsSupportedAutomaticPipelineAutoplayContinuation(
            combat, continuation);
    }

    internal static bool IsAutomaticPipelineSemanticPotionGenerationContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_potion_generation",
              ControlledPotionGeneration: { } generation,
              OuterChoiceContinuation: { } continuation } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            !ValidateControlledPotionGeneration(
                combat, generation, pending.SourceCardInstanceId))
            return false;

        return IsSupportedAutomaticPipelineAutoplayContinuation(
            combat, continuation);
    }

    internal static bool IsAutomaticPipelineSemanticCardTransformContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_card_transform",
              ControlledCardTransform: { } transform } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            transform.OriginalChoice.OuterChoiceContinuation is not
                { } continuation ||
            !ValidateControlledCardTransform(combat, transform))
            return false;

        return IsSupportedAutomaticPipelineAutoplayContinuation(
            combat, continuation);
    }

    internal static bool IsAutomaticPipelineSemanticCardShortlistContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_card_shortlist",
              ControlledCardShortlist: { } shortlist,
              OuterChoiceContinuation: { } continuation } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            shortlist.Operation.Kind is not (
                PrototypeCombatEffectKind.AutoPlayRandomCardsFromZone or
                PrototypeCombatEffectKind.EmpowerRandomDrawCardReplay) ||
            !ValidateControlledCardShortlist(combat, shortlist))
            return false;

        return IsSupportedAutomaticPipelineAutoplayContinuation(
            combat, continuation);
    }

    internal static bool IsAutomaticPipelineSemanticPoolShuffleContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_pool_shuffle",
              ControlledPoolShuffle: { } shuffle,
              OuterChoiceContinuation: { } continuation } ||
            shuffle.Operation.Kind == PrototypeCombatEffectKind.ChooseCards ||
            !ValidateControlledPoolShuffle(
                combat, shuffle, pending.SourceCardInstanceId))
            return false;

        if (shuffle.Operation.Kind ==
            PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw)
            return IsMayhemLegacyRecycleContinuation(combat, pending);
        if (combat.SemanticDeck is not
            { Mode: SemanticDeckStateV111.NatureMode })
            return false;
        return IsSupportedAutomaticPipelineAutoplayContinuation(
            combat, continuation);
    }

    internal static bool IsAutomaticPipelineSemanticRandomHandCostContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "controlled_random_hand_cost",
              ControlledRandomCost: { } randomCost,
              OuterChoiceContinuation: { } continuation } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            !ValidateControlledRandomCost(combat, randomCost))
            return false;

        return IsSupportedAutomaticPipelineAutoplayContinuation(
            combat, continuation);
    }

    internal static bool IsAutomaticPipelineSemanticCardSelectionContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (pending is not
            { ChoiceId: "select_cards",
              CardPlaySeries: { IsAutoPlay: true },
              OuterChoiceContinuation: null } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;

        return IsMayhemAutoPrePlayPipelineCursor(combat) ||
            combat.IsPlayerTurn &&
            IsAtSupportedAutomaticPipelineTargetBoundary(combat);
    }

    private static bool IsSupportedAutomaticPipelineAutoplayContinuation(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        for (var frame = continuation; frame is not null;
             frame = frame.Parent)
            if (frame.CardPlaySeries is { IsAutoPlay: true })
                return IsMayhemAutoPrePlayPipelineCursor(combat) ||
                    combat.IsPlayerTurn &&
                    IsAtSupportedAutomaticPipelineTargetBoundary(combat);
        return false;
    }

    private static bool IsLateAutoplaySemanticDrawContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (!combat.IsPlayerTurn ||
            !IsAtSupportedAutomaticPipelineTargetBoundary(combat) ||
            pending.ControlledSemanticDraw is not { } draw ||
            pending.ChoiceId is not (
                "controlled_semantic_draw" or "controlled_draw_cost") ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            pending.OuterChoiceContinuation is not { } continuation)
            return false;

        bool nestedAutoplay = false;
        for (var frame = continuation; frame is not null;
             frame = frame.Parent)
            if (frame.CardPlaySeries is { IsAutoPlay: true })
            {
                nestedAutoplay = true;
                break;
            }
        if (!nestedAutoplay) return false;

        if (pending.ChoiceId == "controlled_semantic_draw")
            return draw.RemainingCount > 0;

        return pending.ControlledDrawCost is { } cost &&
            cost.RemainingCount == draw.RemainingCount &&
            cost.FromHandDraw == draw.FromHandDraw &&
            cost.EventDepth == draw.EventDepth;
    }

    internal static bool IsMayhemLegacyRecycleContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (!IsMayhemAutoPrePlayPipelineCursor(combat) ||
            combat.SemanticDeck is not null ||
            pending.ChoiceId != "controlled_pool_shuffle" ||
            pending.ControlledPoolShuffle?.Operation.Kind !=
                PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw ||
            pending.OuterChoiceContinuation is not { } continuation ||
            continuation.Operations.FirstOrDefault() is not
                { Kind: PrototypeCombatEffectKind.DrawCards, Amount: > 0 })
            return false;

        for (var frame = continuation;
             frame is not null; frame = frame.Parent)
            if (frame.CardPlaySeries is { IsAutoPlay: true })
                return true;
        return false;
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
