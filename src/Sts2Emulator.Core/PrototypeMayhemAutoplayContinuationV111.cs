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
            { ChoiceId: "select_cards", OuterChoiceContinuation: null } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;

        if (IsPlayerTurnStartedDiscardSelection(combat, pending) ||
            IsPlayerTurnStartedToolsOfTheTradeSelection(combat, pending))
            return true;
        if (pending.CardPlaySeries is not { IsAutoPlay: true })
            return false;

        return IsMayhemAutoPrePlayPipelineCursor(combat) ||
            combat.IsPlayerTurn &&
            IsAtSupportedAutomaticPipelineTargetBoundary(combat);
    }

    private static bool IsPlayerTurnStartedDiscardSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        if (combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: { } continuation,
                PendingKnowledgeDemonChoice: null } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            pending is not
                { SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { } candidates,
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      RemoveUnselectedFromSource: false },
                  OuterChoiceContinuation: null,
                  CardPlaySeries: null,
                  EventDispatchContinuation: null,
                  SelectedCardPower: null,
                  SelectedCardKeyword: null,
                  SelectedCardTemporaryCost: null,
                  SequentialSelectedCardInstanceIds: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false } ||
            pending.Selection is not
                { SourceZone: PrototypeCardZone.Hand,
                  Resolution: PrototypeCardSelectionResolutionKind.MoveToDiscard,
                  SequentialOptional: false,
                  DrawEqualToSelectionsOnCompletion: false,
                  RemoveUnselectedFromSource: false } ||
            !IsAtSupportedAutomaticPipelineTargetBoundary(combat))
            return false;

        var playerTurnStartedIndex = PlayerTurnStartedPipelineStepIndex();
        if (playerTurnStartedIndex < 0 ||
            continuation.NextStepIndex != playerTurnStartedIndex + 1)
            return false;

        var discardCount = combat.PlayerPowers.Sum(power =>
            PrototypeContent.Power(power.PowerId)
                .DiscardAfterPlayerTurnStartPerStack * power.Stacks);
        var expectedCount = Math.Min(discardCount, combat.Hand.Length);
        return expectedCount > 0 &&
            pending.Selection.MinSelections == expectedCount &&
            pending.Selection.MaxSelections == expectedCount &&
            pending.CandidateCardInstanceIds.SequenceEqual(combat.Hand);
    }

    private static bool IsPlayerTurnStartedToolsOfTheTradeSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        const string toolsOfTheTradePowerId =
            "proto.power.tools_of_the_trade";
        if (combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: { } continuation,
                PendingKnowledgeDemonChoice: null } ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            pending is not
                { ChoiceId: "select_cards",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { } candidates,
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      RemoveUnselectedFromSource: false },
                  OuterChoiceContinuation: null,
                  CardPlaySeries: null,
                  SelectedCardPower: null,
                  SelectedCardKeyword: null,
                  SelectedCardTemporaryCost: null,
                  SequentialSelectedCardInstanceIds: null,
                  EventDispatchContinuation: { } eventDispatch,
                  ControlledTarget: null,
                  ControlledGeneration: null,
                  ControlledPotionGeneration: null,
                  ControlledCardTransform: null,
                  ControlledDrawInsertion: null,
                  ControlledPoolShuffle: null,
                  ControlledRandomCost: null,
                  ControlledDrawCost: null,
                  ControlledCardShortlist: null,
                  ControlledRandomPileCard: null,
                  ControlledSemanticDraw: null,
                  SemanticStratagemDrawSelection: false,
                  AutomaticSemanticStratagemDrawSelection: false,
                  StandaloneSemanticDrawHookChoice: false,
                  AutomaticSemanticDrawHookChoice: false,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false } ||
            eventDispatch is not
                { EventDepth: 0,
                  CombatEvent.Kind: PrototypeCombatEventKind.PlayerTurnStarted } ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.PlayerTurnStarted) ||
            !IsAtPlayerTurnStartedDispatchContinuation(
                combat, continuation))
            return false;

        var subscriber = eventDispatch.CurrentSubscriber;
        if (subscriber.SourcePowerApplicationOrder is not { } powerOrder ||
            subscriber.SourceKind != PrototypeEffectSourceKind.Power ||
            subscriber.SourcePowerEnemyId is not null ||
            subscriber.PowerTriggerIndex is not { } triggerIndex)
            return false;

        var power = combat.PlayerPowers.SingleOrDefault(candidate =>
            candidate.ApplicationOrder == powerOrder);
        if (power is not { PowerId: toolsOfTheTradePowerId, Stacks: > 0 } ||
            subscriber.ApplicationOrder != power.ApplicationOrder)
            return false;

        var definition = PrototypeContent.Power(toolsOfTheTradePowerId);
        if ((uint)triggerIndex >= (uint)definition.Triggers.Length)
            return false;
        var trigger = definition.Triggers[triggerIndex];
        if (trigger.EventKind != PrototypeCombatEventKind.PlayerTurnStarted ||
            trigger.RequiresOwnerTarget || trigger.ExcludeHandDraw ||
            trigger.RequiresPlayerTurn ||
            trigger.RequiredSourceCardType is not null ||
            trigger.Effects.Length != 1)
            return false;
        var expectedSubscriber = new PrototypeEventSubscriberState(
            power.ApplicationOrder, trigger.Effects, power.Stacks,
            PrototypeEffectSourceKind.Power,
            SourcePowerApplicationOrder: power.ApplicationOrder,
            PowerCardPayload: power.CardPayload?.Fork(),
            RemoveSourcePowerAfterTrigger: trigger.RemoveSourcePowerAfterTrigger,
            EveryNth: trigger.EveryNth,
            PowerTriggerIndex: triggerIndex,
            CountdownBeforeTrigger: trigger.CountdownBeforeTrigger,
            SourcePowerStoredValue: power.StoredValue);
        if (CanonicalJson.Sha256(expectedSubscriber) !=
            CanonicalJson.Sha256(subscriber))
            return false;

        var chooseCards = trigger.Effects.SingleOrDefault(effect =>
            effect.Kind == PrototypeCombatEffectKind.ChooseCards);
        if (chooseCards?.Selection is not { } sourceSelection ||
            sourceSelection.SourceZone != PrototypeCardZone.Hand ||
            sourceSelection.Resolution !=
                PrototypeCardSelectionResolutionKind.MoveToDiscard ||
            sourceSelection.RequiredCardType is not null ||
            sourceSelection.RandomCandidateCount != 0 ||
            sourceSelection.RequireAttackOrPower ||
            sourceSelection.RequireEnergyCostingCard)
            return false;

        int sourceMin = sourceSelection.MinSelections +
            sourceSelection.SelectionsPerPowerStack * power.Stacks;
        int sourceMax = sourceSelection.MaxSelections +
            sourceSelection.SelectionsPerPowerStack * power.Stacks;
        int expectedMax = Math.Min(sourceMax, combat.Hand.Length);
        if (expectedMax <= 0)
            return false;
        var expectedSelection = sourceSelection with
        {
            MinSelections = Math.Min(sourceMin, expectedMax),
            MaxSelections = expectedMax
        };
        return pending.Selection == expectedSelection &&
            candidates.SequenceEqual(combat.Hand);
    }

    private static bool IsAtPlayerTurnStartedDispatchContinuation(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        var playerTurnStartedIndex = PlayerTurnStartedPipelineStepIndex();
        return combat.IsPlayerTurn && playerTurnStartedIndex >= 0 &&
            continuation is
                { OpeningStage: null,
                  PendingPostDispatchEventKind:
                    PrototypeCombatEventKind.PlayerTurnStarted,
                  ControlledDrawRemaining: null,
                  ControlledDrawCostRemaining: null,
                  ControlledSemanticDrawRemaining: null,
                  ControlledSemanticDrawRequested: null,
                  ControlledSemanticBeforeHandDrawPending: false,
                  ControlledRecycleStratagemPending: false,
                  ControlledDrawHookChoicePending: false,
                  ControlledBeforeHandDrawHookChoicePending: false,
                  ControlledEnemyAction: null,
                  ControlledTurnIntents: null } &&
            continuation.NextStepIndex == playerTurnStartedIndex + 1;
    }

    private static int PlayerTurnStartedPipelineStepIndex() =>
        Array.FindIndex(PrototypeContent.Rules.EndTurnPipeline, step =>
            step.Kind == PrototypeAutomaticStepKind.DispatchCombatEvent &&
            step.EventKind == PrototypeCombatEventKind.PlayerTurnStarted);

    internal static bool IsAutomaticPipelineSemanticDrawHookChoiceContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (!pending.StandaloneSemanticDrawHookChoice ||
            pending.AutomaticSemanticDrawHookChoice ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            !SemanticDrawHookChoiceEngineV111.HasStandaloneDrawResume(pending) ||
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
