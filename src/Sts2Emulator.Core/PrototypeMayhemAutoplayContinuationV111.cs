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
            combat.Stochastic.SemanticDeck is not
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
              ControlledDrawInsertion: { } insertion,
              OuterChoiceContinuation: { } continuation } ||
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;

        if (IsOpeningBlessedAntlerDrawInsertionContinuation(
                combat, pending, insertion, continuation))
            return true;

        if (IsOpeningAfterShuffleDrawInsertionContinuation(
                combat, pending, insertion, continuation))
            return true;

        if (insertion is not
            { Stage: "position", SelectedCardId: not null,
              SemanticDrawContinuation: not null,
              AfterShuffleRelic: not null })
            return false;

        if (insertion.SemanticDrawContinuation is
                { AutomaticSemanticHandDrawSegment: true,
                  StandaloneSemanticDrawSegment: false,
                  FromHandDraw: true,
                  EventDepth: 0,
                  RemainingCount: > 0 } automaticDraw &&
            combat.IsPlayerTurn &&
            combat.AutomaticPipelineContinuation is { } pipeline &&
            pipeline.Stochastic.TryGetSemanticHandDrawProgress(
                pipeline.Mechanics, out _, out int savedRemaining) &&
            savedRemaining == automaticDraw.RemainingCount)
            return true;

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

    private static bool IsOpeningAfterShuffleDrawInsertionContinuation(
        CombatState combat, PendingCombatChoiceState pending,
        PrototypeControlledDrawInsertionV111 insertion,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var pipeline = combat.AutomaticPipelineContinuation;
        if (combat is not
                { IsPlayerTurn: true, Turn: 1,
                  PendingKnowledgeDemonChoice: null } ||
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            pipeline is null ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.AfterHandDraw) ||
            pending is not
                { ChoiceId: "controlled_draw_insertion",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DrawPile,
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 } } ||
            continuation is not
                { SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DrawPile,
                  Operations: { Length: 0 },
                  PendingDiscardEvents: { Length: 0 },
                  PendingSlyCardInstanceIds: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  CardPlaySeries: null,
                  EventDispatchContinuation: null,
                  Parent: null } ||
            insertion is not
                { Stage: "position", SelectedCardId: not null,
                  AfterShuffleRelic: not null,
                  SemanticDrawContinuation:
                    { FromHandDraw: true, EventDepth: 0,
                      RemainingCount: > 0,
                      StandaloneSemanticDrawSegment: false,
                      AutomaticSemanticHandDrawSegment: false,
                      DrawnCardKeyword: null } })
            return false;

        return ValidateControlledDrawInsertion(combat, insertion);
    }

    private static bool IsOpeningBlessedAntlerDrawInsertionContinuation(
        CombatState combat, PendingCombatChoiceState pending,
        PrototypeControlledDrawInsertionV111 insertion,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        const string dazedCardId = "proto.status.dazed";
        if (combat is not { IsPlayerTurn: true, Turn: 1,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null },
                AutomaticPipelineContinuation: { } pipeline } ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, pipeline, PrototypeOpeningCombatStage.DrawHand) ||
            pending is not
                { SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { Length: 0 },
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SourceZone: PrototypeCardZone.Hand,
                      MinSelections: 0,
                      MaxSelections: 0,
                      Resolution:
                        PrototypeCardSelectionResolutionKind.MoveToDiscard,
                      RequiredCardType: null,
                      SelectionsPerPowerStack: 0,
                      RandomCandidateCount: 0,
                      RequireAttackOrPower: false,
                      RequireEnergyCostingCard: false,
                      SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      RemoveUnselectedFromSource: false,
                      CopiesPerSelection: 0,
                      CopiesPerSelectionUpgradeDelta: 0 },
                  EventDispatchContinuation: null,
                  CardPlaySeries: null,
                  SelectedCardPower: null,
                  SelectedCardKeyword: null,
                  SelectedCardTemporaryCost: null,
                  ControlledTarget: null,
                  ControlledGeneration: null,
                  ControlledPotionGeneration: null,
                  ControlledCardTransform: null,
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
            insertion is not
                { Stage: "position",
                  Operation: { Kind:
                      PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                    Amount: >= 1 and <= 3,
                    CardId: dazedCardId,
                    SourceKind: PrototypeEffectSourceKind.Relic },
                  SelectedCardId: dazedCardId,
                  CandidateCardIds: { Length: 0 },
                  SemanticDrawContinuation: null,
                  AfterShuffleRelic: null,
                  CompletedAfterShuffleRelics: null,
                  RemainingAfterShuffleRelics: null,
                  RequestSiteId:
                    StochasticSiteRegistryV111.DrawPileRandomInsertSiteId,
                  RequestOwnerId: "prototype.combat",
                  RequestOrdinal: 0,
                  PersonalHiveDamageContinuation: null,
                  AutomaticHandDrawRemaining: null } ||
            continuation is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  PendingDiscardEvents: { Length: 0 },
                  PendingSlyCardInstanceIds: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } eventDispatch,
                  CompletePlayerHandCleanup: false })
            return false;

        var expectedOperation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
            insertion.Operation.Amount, CardId: dazedCardId,
            SourceKind: PrototypeEffectSourceKind.Relic);
        if (CanonicalJson.Sha256(insertion.Operation) !=
                CanonicalJson.Sha256(expectedOperation) ||
            eventDispatch.EventDepth != 0 ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.BeforeHandDraw) ||
            continuation.Operations.Length !=
                (insertion.Operation.Amount > 1 ? 1 : 0))
            return false;

        if (insertion.Operation.Amount > 1)
        {
            var expectedRemainingOperation = expectedOperation with
            {
                Amount = insertion.Operation.Amount - 1
            };
            if (CanonicalJson.Sha256(continuation.Operations[0]) !=
                CanonicalJson.Sha256(expectedRemainingOperation))
                return false;
        }

        if (eventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Relic,
                  SourcePowerApplicationOrder: null,
                  SourcePowerEnemyId: null,
                  RelicStateIndex: { } relicIndex,
                  RelicTriggerIndex: 0 } subscriber ||
            (uint)relicIndex >= (uint)combat.RelicStates.Length)
            return false;

        var relic = combat.RelicStates[relicIndex];
        if (relic is not
                { RelicId: NativeGloryAncientsV111.BlessedAntlerId,
                  IsMelted: false } ||
            relic.PersistentIndex != relicIndex ||
            relic.ApplicationOrder != subscriber.ApplicationOrder)
            return false;

        var definition = PrototypeContent.Relic(relic.RelicId);
        var triggers = definition.Triggers ?? [];
        var expectedTrigger = new PrototypeRelicTriggerSpec(
            PrototypeCombatEventKind.BeforeHandDraw,
            [new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
                3, CardId: dazedCardId)],
            TurnEquals: 1);
        if (triggers.Length != 1 ||
            CanonicalJson.Sha256(triggers[0]) !=
                CanonicalJson.Sha256(expectedTrigger) ||
            subscriber.RelicTriggerIndex != 0)
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            relic.ApplicationOrder, triggers[0].Effects, PowerStacks: 0,
            PrototypeEffectSourceKind.Relic,
            RelicStateIndex: relicIndex,
            RelicTriggerIndex: 0,
            EveryNth: triggers[0].EveryNth,
            MaxTriggersPerCounterWindow:
                triggers[0].MaxTriggersPerCounterWindow);
        return CanonicalJson.Sha256(expectedSubscriber) ==
            CanonicalJson.Sha256(subscriber);
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
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            !ValidateControlledGenerationSupport(generation))
            return false;

        return IsSupportedAutomaticPipelineAutoplayContinuation(
                combat, continuation) ||
            IsCrossbowTurnStartGenerationContinuation(combat, pending);
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
            combat.Stochastic.SemanticDeck is not
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
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            !ValidateControlledCardTransform(combat, transform))
            return false;

        if (transform.OriginalChoice.OuterChoiceContinuation is
            { } continuation)
            return IsSupportedAutomaticPipelineAutoplayContinuation(
                combat, continuation);

        var originalChoice = transform.OriginalChoice;
        return IsPlayerTurnStartedEntropySelection(
            combat with { PendingChoice = originalChoice }, originalChoice);
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
            combat.Stochastic.SemanticDeck is not
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
        if (combat.Stochastic.SemanticDeck is not
            { Mode: SemanticDeckStateV111.NatureMode })
            return false;
        return IsSupportedAutomaticPipelineAutoplayContinuation(
                combat, continuation) ||
            IsOpeningGeneratedPoolShuffleContinuation(
                combat, pending, shuffle, continuation) ||
            IsHelloWorldAutomaticBeforeHandDrawPoolShuffle(
                combat, pending, shuffle, continuation) ||
            IsToolboxAutomaticBeforeHandDrawPoolShuffle(
                combat, pending, shuffle, continuation);
    }

    internal static bool IsHelloWorldAutomaticBeforeHandDrawPoolShuffle(
        CombatState combat, PendingCombatChoiceState pending,
        PrototypeControlledPoolShuffleV111 shuffle,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        if (combat.Turn < 2 ||
            !IsAutomaticSemanticBeforeHandDrawPoolShuffleFrame(
                combat, pending, continuation,
                PrototypeEffectSourceKind.Power, out var eventDispatch) ||
            !IsHelloWorldBeforeHandDrawDispatch(
                combat, eventDispatch, out var power) ||
            shuffle.Operation.Kind !=
                PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand ||
            shuffle.Operation.SourceKind != PrototypeEffectSourceKind.Power ||
            shuffle.Operation.SourcePowerApplicationOrder is not
                { } powerOrder ||
            powerOrder != power.ApplicationOrder)
            return false;

        if (shuffle.Operation.Amount != power.Stacks)
            return false;

        var triggers = PrototypeContent.Power(power.PowerId).Triggers;
        var expectedOperation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand,
            power.Stacks,
            SourceKind: PrototypeEffectSourceKind.Power,
            SourcePowerApplicationOrder: power.ApplicationOrder);
        var sourcePool = SourcePoolForShuffle(
            combat, expectedOperation, sourceCardInstanceId: null);
        if (sourcePool is null ||
            CanonicalJson.Sha256(shuffle.Operation) !=
                CanonicalJson.Sha256(expectedOperation) ||
            shuffle.SelectedCount != Math.Min(
                power.StoredValue, sourcePool.Length) ||
            !shuffle.OriginalCardIds.SequenceEqual(sourcePool))
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            power.ApplicationOrder, triggers[0].Effects, power.Stacks,
            PrototypeEffectSourceKind.Power,
            SourcePowerApplicationOrder: power.ApplicationOrder,
            PowerCardPayload: power.CardPayload?.Fork(),
            RemoveSourcePowerAfterTrigger:
                triggers[0].RemoveSourcePowerAfterTrigger,
            EveryNth: triggers[0].EveryNth,
            PowerTriggerIndex: 0,
            CountdownBeforeTrigger: triggers[0].CountdownBeforeTrigger,
            SourcePowerStoredValue: power.StoredValue);
        return CanonicalJson.Sha256(expectedSubscriber) ==
            CanonicalJson.Sha256(eventDispatch.CurrentSubscriber);
    }

    private static bool IsHelloWorldBeforeHandDrawDispatch(
        CombatState combat,
        PrototypeEventDispatchContinuationState eventDispatch,
        out PrototypePowerInstanceState power)
    {
        power = null!;
        const string helloWorldPowerId = "proto.power.hello_world";
        if (combat.Turn < 2 || eventDispatch.EventDepth != 0 ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.BeforeHandDraw) ||
            eventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Power,
                  SourcePowerApplicationOrder: { } powerOrder,
                  PowerTriggerIndex: 0 } ||
            combat.PlayerPowers.SingleOrDefault(candidate =>
                candidate.ApplicationOrder == powerOrder) is not
                { PowerId: helloWorldPowerId, Stacks: > 0,
                  StoredValue: > 0 } sourcePower)
            return false;

        var definition = PrototypeContent.Power(sourcePower.PowerId);
        var triggers = definition.Triggers;
        var expectedTrigger = new PrototypePowerTriggerSpec(
            PrototypeCombatEventKind.BeforeHandDraw,
            [new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.CreateDistinctCharacterCommonCardsInHand,
                0, AmountPerPowerStack: 1)]);
        if (!definition.SnapshotGenerationStacksAtTurnStart ||
            triggers.Length != 1 ||
            CanonicalJson.Sha256(triggers[0]) !=
                CanonicalJson.Sha256(expectedTrigger))
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            sourcePower.ApplicationOrder, triggers[0].Effects,
            sourcePower.Stacks, PrototypeEffectSourceKind.Power,
            SourcePowerApplicationOrder: sourcePower.ApplicationOrder,
            PowerCardPayload: sourcePower.CardPayload?.Fork(),
            RemoveSourcePowerAfterTrigger:
                triggers[0].RemoveSourcePowerAfterTrigger,
            EveryNth: triggers[0].EveryNth,
            PowerTriggerIndex: 0,
            CountdownBeforeTrigger: triggers[0].CountdownBeforeTrigger,
            SourcePowerStoredValue: sourcePower.StoredValue);
        if (CanonicalJson.Sha256(expectedSubscriber) !=
            CanonicalJson.Sha256(eventDispatch.CurrentSubscriber))
            return false;

        power = sourcePower;
        return true;
    }

    internal static bool IsToolboxAutomaticBeforeHandDrawPoolShuffle(
        CombatState combat, PendingCombatChoiceState pending,
        PrototypeControlledPoolShuffleV111 shuffle,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(shuffle);
        ArgumentNullException.ThrowIfNull(continuation);
        if (combat.Turn != 1 ||
            !IsAutomaticSemanticBeforeHandDrawPoolShuffleFrame(
                combat, pending, continuation,
                PrototypeEffectSourceKind.Relic, out var eventDispatch) ||
            !IsToolboxBeforeHandDrawDispatch(
                combat, eventDispatch, out var expectedOperation))
            return false;

        var sourcePool = SourcePoolForShuffle(
            combat, expectedOperation, sourceCardInstanceId: null);
        if (sourcePool is null ||
            CanonicalJson.Sha256(shuffle.Operation) !=
                CanonicalJson.Sha256(expectedOperation) ||
            shuffle.SelectedCount != Math.Min(3, sourcePool.Length) ||
            !shuffle.OriginalCardIds.SequenceEqual(sourcePool))
            return false;

        return true;
    }

    private static bool IsToolboxBeforeHandDrawDispatch(
        CombatState combat,
        PrototypeEventDispatchContinuationState eventDispatch,
        out PrototypeQueuedOperation expectedOperation)
    {
        return IsSupportedGeneratedChoiceSubscriber(
                combat, eventDispatch, operation: null,
                out expectedOperation, requireOpeningCursor: false) &&
            eventDispatch.CombatEvent == new PrototypeCombatEvent(
                PrototypeCombatEventKind.BeforeHandDraw);
    }

    private static bool IsAutomaticSemanticBeforeHandDrawPoolShuffleFrame(
        CombatState combat, PendingCombatChoiceState pending,
        PrototypeChoiceResolutionContinuationState continuation,
        PrototypeEffectSourceKind expectedSourceKind,
        out PrototypeEventDispatchContinuationState eventDispatch)
    {
        eventDispatch = null!;
        if (combat is not
                { IsPlayerTurn: true,
                  SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !IsAtAutomaticSemanticBeforeHandDrawHookCursor(
                pipeline, combat, pending) ||
            pending is not
                { SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { Length: 0 },
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SourceZone: PrototypeCardZone.Hand,
                      MinSelections: 0,
                      MaxSelections: 0,
                      Resolution:
                        PrototypeCardSelectionResolutionKind.MoveToDiscard,
                      RequiredCardType: null,
                      SelectionsPerPowerStack: 0,
                      RandomCandidateCount: 0,
                      RequireAttackOrPower: false,
                      RequireEnergyCostingCard: false,
                      SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      RemoveUnselectedFromSource: false,
                      CopiesPerSelection: 0,
                      CopiesPerSelectionUpgradeDelta: 0 },
                  EventDispatchContinuation: null,
                  CardPlaySeries: null,
                  SelectedCardPower: null,
                  SelectedCardKeyword: null,
                  SelectedCardTemporaryCost: null,
                  ControlledTarget: null,
                  ControlledGeneration: null,
                  ControlledPotionGeneration: null,
                  ControlledCardTransform: null,
                  ControlledDrawInsertion: null,
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
            continuation is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: { Length: 0 },
                  PendingDiscardEvents: { Length: 0 },
                  PendingSlyCardInstanceIds: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } dispatch,
                  CompletePlayerHandCleanup: false } ||
            dispatch.EventDepth != 0 ||
            dispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.BeforeHandDraw) ||
            dispatch.CurrentSubscriber.SourceKind != expectedSourceKind)
            return false;

        eventDispatch = dispatch;
        return true;
    }

    private static bool IsAtAutomaticSemanticBeforeHandDrawHookCursor(
        PrototypeAutomaticPipelineContinuationState continuation,
        CombatState combat, PendingCombatChoiceState pending)
    {
        bool hasSavedHandDrawProgress =
            continuation.Stochastic.HasSavedHandDrawProgress();
        bool semanticBeforeHandDrawPending =
            continuation.Stochastic.IsAtSemanticBeforeHandDraw(
                continuation.Mechanics);
        if (!continuation.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            (hasSavedHandDrawProgress && !semanticBeforeHandDrawPending) ||
            continuation.Stochastic.ControlledEnemyAction is not null ||
            continuation.Stochastic.ControlledTurnIntents is not null ||
            continuation.Stochastic.CompletedSemanticHandDrawRequested is not null ||
            pending.AutomaticSemanticDrawHookChoice !=
                semanticBeforeHandDrawPending ||
            combat.PendingChoice?.ChoiceId != "controlled_pool_shuffle")
            return false;

        return true;
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
            combat.Stochastic.SemanticDeck is not
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
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
            return false;

        if (IsPlayerTurnStartedDiscardSelection(combat, pending) ||
            IsPlayerTurnStartedToolsOfTheTradeSelection(combat, pending) ||
            IsPlayerTurnStartedEntropySelection(combat, pending) ||
            IsToastyMittensTurnStartSelection(combat, pending) ||
            IsOpeningGeneratedCardSelection(combat, pending) ||
            IsGamblingChipOpeningSelection(combat, pending) ||
            IsAutomaticSemanticBeforeHandDrawGeneratedSelection(
                combat, pending))
            return true;
        if (pending.CardPlaySeries is not { IsAutoPlay: true })
            return false;

        return IsMayhemAutoPrePlayPipelineCursor(combat) ||
            combat.IsPlayerTurn &&
            IsAtSupportedAutomaticPipelineTargetBoundary(combat);
    }

    internal static bool IsAutomaticSemanticBeforeHandDrawGeneratedSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        var cursor = combat.AutomaticPipelineContinuation;
        if (combat is not
                { IsPlayerTurn: true,
                  SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null } } ||
            cursor is null ||
            !cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics) ||
            pending is not
                { ChoiceId: "select_cards",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { Length: > 0 },
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SourceZone: PrototypeCardZone.ChoicePool,
                      MinSelections: 0,
                      MaxSelections: 1,
                      Resolution:
                        PrototypeCardSelectionResolutionKind.MoveToHand,
                      RequiredCardType: null,
                      SelectionsPerPowerStack: 0,
                      RandomCandidateCount: 0,
                      RequireAttackOrPower: false,
                      RequireEnergyCostingCard: false,
                      SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      RemoveUnselectedFromSource: true,
                      CopiesPerSelection: 0,
                      CopiesPerSelectionUpgradeDelta: 0 },
                  OuterChoiceContinuation: null,
                  CardPlaySeries: null,
                  EventDispatchContinuation: { } eventDispatch,
                  ControlledTarget: null,
                  ControlledGeneration: null,
                  ControlledPotionGeneration: null,
                  ControlledCardTransform: null,
                  ControlledPoolShuffle: null,
                  ControlledDrawInsertion: null,
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
            pending.SequentialSelectedCardInstanceIds is not null ||
            eventDispatch.EventDepth != 0 ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.BeforeHandDraw))
            return false;

        var candidates = pending.CandidateCardInstanceIds;
        if (!combat.ChoiceCardIds.SequenceEqual(candidates) ||
            candidates.Distinct().Count() != candidates.Length)
            return false;
        return IsToolboxAutomaticBeforeHandDrawSelection(
            combat, candidates, eventDispatch);
    }

    private static bool IsToolboxAutomaticBeforeHandDrawSelection(
        CombatState combat, long[] candidates,
        PrototypeEventDispatchContinuationState eventDispatch)
    {
        if (combat.Turn != 1 ||
            !IsToolboxBeforeHandDrawDispatch(
                combat, eventDispatch, out var expectedOperation))
            return false;
        var sourcePool = SourcePoolForShuffle(
            combat, expectedOperation, sourceCardInstanceId: null);
        if (sourcePool is null ||
            candidates.Length != Math.Min(3, sourcePool.Length) ||
            !HasGeneratedChoiceInstances(
                combat, candidates, expectedOperation, sourcePool))
            return false;
        return true;
    }

    private static bool HasGeneratedChoiceInstances(
        CombatState combat, long[] candidates,
        PrototypeQueuedOperation operation, string[] sourcePool)
    {
        if (candidates.Distinct().Count() != candidates.Length)
            return false;
        var byInstance = combat.Cards.ToDictionary(card => card.InstanceId);
        if (candidates.Any(instanceId =>
                !byInstance.ContainsKey(instanceId)))
            return false;

        var offered = candidates.Select(id => byInstance[id]).ToArray();
        if (offered.Select(card => card.CardId).Distinct(
                StringComparer.Ordinal).Count() != offered.Length)
            return false;

        PrototypeCardKeywordOverride[]? expectedKeywords =
            operation.GeneratedChoiceCardsRetain
                ? [new PrototypeCardKeywordOverride(
                    PrototypeCardKeyword.Retain, true,
                    PrototypeCardKeywordOverrideExpiry.None)]
                : null;
        var expectedTemporaryCost =
            operation.GeneratedChoiceCardsFreeThisTurn
                ? new PrototypeTemporaryCardCost(0,
                    PrototypeTemporaryCardCostExpiry.EndOfTurn |
                    PrototypeTemporaryCardCostExpiry.WhenPlayed)
                : null;
        return offered.All(card =>
        {
            var expected = new CombatCardInstance(
                InstanceId: card.InstanceId,
                PersistentCardInstanceId: null,
                CardId: card.CardId,
                UpgradeLevel: operation.GeneratedChoiceCardsUpgraded ? 1 : 0,
                IsTemporary: true,
                State: PrototypeJson.EmptyObject(),
                TemporaryEnergyCost: expectedTemporaryCost,
                KeywordOverrides: expectedKeywords);
            expected = ApplyActiveSourceBoundAfflictionToCard(
                combat, expected);
            return sourcePool.Contains(card.CardId, StringComparer.Ordinal) &&
                CanonicalJson.Sha256(expected) ==
                    CanonicalJson.Sha256(card);
        });
    }

    internal static bool IsPlayerTurnStartedEntropySelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        const string entropyPowerId = "proto.power.entropy";
        if (combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: { } continuation,
                PendingKnowledgeDemonChoice: null } ||
            combat.Stochastic.SemanticDeck is not
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
            pending.Selection is not
                { SourceZone: PrototypeCardZone.Hand,
                  Resolution:
                    PrototypeCardSelectionResolutionKind.TransformRandom,
                  RequiredCardType: null,
                  SelectionsPerPowerStack: 1,
                  RandomCandidateCount: 0,
                  RequireAttackOrPower: false,
                  RequireEnergyCostingCard: false } ||
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
        if (power is not { PowerId: entropyPowerId, Stacks: > 0 } ||
            subscriber.ApplicationOrder != power.ApplicationOrder)
            return false;

        var definition = PrototypeContent.Power(entropyPowerId);
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
        var sourceSelection = chooseCards?.Selection;
        if (sourceSelection is not
                { SourceZone: PrototypeCardZone.Hand,
                  MinSelections: 0,
                  MaxSelections: 0,
                  Resolution:
                    PrototypeCardSelectionResolutionKind.TransformRandom,
                  RequiredCardType: null,
                  SelectionsPerPowerStack: 1,
                  RemoveUnselectedFromSource: false,
                  RequireEnergyCostingCard: false,
                  SequentialOptional: false,
                  DrawEqualToSelectionsOnCompletion: false,
                  MaxSelectionsUpgradeDelta: 0,
                  RandomCandidateCount: 0,
                  RequireAttackOrPower: false,
                  CopiesPerSelection: 0,
                  CopiesPerSelectionUpgradeDelta: 0 })
            return false;

        var expectedCandidates = combat.Hand.Where(id =>
            !PrototypeCardRules.IsEternal(combat.Cards.Single(card =>
                card.InstanceId == id))).ToArray();
        int expectedMax = Math.Min(power.Stacks, expectedCandidates.Length);
        if (expectedMax <= 0 ||
            !candidates.SequenceEqual(expectedCandidates) ||
            expectedCandidates.Any(id =>
                EligibleCombatTransformPool(combat.Cards.Single(card =>
                    card.InstanceId == id).CardId, combat).Length == 0))
            return false;

        var expectedSelection = sourceSelection with
        {
            MinSelections = expectedMax,
            MaxSelections = expectedMax
        };
        return pending.Selection == expectedSelection;
    }

    private static bool IsToastyMittensTurnStartSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        if (combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: { } continuation,
                PendingKnowledgeDemonChoice: null } ||
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            pending is not
                { ChoiceId: "select_cards",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { } candidates,
                  Continuation: { Length: 1 } queuedOperations,
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
            pending.Selection is not
                { SourceZone: PrototypeCardZone.Hand,
                  MinSelections: 1,
                  MaxSelections: 1,
                  Resolution:
                    PrototypeCardSelectionResolutionKind.MoveToExhaust,
                  RequiredCardType: null,
                  SelectionsPerPowerStack: 0,
                  RemoveUnselectedFromSource: false,
                  RequireEnergyCostingCard: false,
                  SequentialOptional: false,
                  DrawEqualToSelectionsOnCompletion: false,
                  MaxSelectionsUpgradeDelta: 0,
                  RandomCandidateCount: 0,
                  RequireAttackOrPower: false,
                  CopiesPerSelection: 0,
                  CopiesPerSelectionUpgradeDelta: 0 } ||
            eventDispatch.EventDepth != 0 ||
            eventDispatch.CombatEvent.SourceCardInstanceId is not null ||
            eventDispatch.CombatEvent.CardId is not null ||
            eventDispatch.CombatEvent.TargetEnemyId is not null ||
            eventDispatch.CombatEvent.Amount != 0 ||
            eventDispatch.CombatEvent.PowerApplicationOrderCeiling is not null ||
            eventDispatch.CombatEvent.FromHandDraw ||
            eventDispatch.CombatEvent.IsAutoPlay ||
            eventDispatch.CombatEvent.IsPoweredAttack)
            return false;

        var eventKind = eventDispatch.CombatEvent.Kind;
        int triggerIndex;
        if (eventKind == PrototypeCombatEventKind.OpeningPlayerTurnStarted)
        {
            if (!IsAtOpeningAutomaticPipelineCursor(
                    combat, continuation,
                    PrototypeOpeningCombatStage.BoneTeaAfterTurnStart))
                return false;
            triggerIndex = 0;
        }
        else if (eventKind == PrototypeCombatEventKind.PlayerTurnStarted)
        {
            if (combat.Turn < 2 ||
                !IsAtPlayerTurnStartedDispatchContinuation(
                    combat, continuation))
                return false;
            triggerIndex = 1;
        }
        else
            return false;

        var subscriber = eventDispatch.CurrentSubscriber;
        if (subscriber.SourceKind != PrototypeEffectSourceKind.Relic ||
            subscriber.SourcePowerApplicationOrder is not null ||
            subscriber.SourcePowerEnemyId is not null ||
            subscriber.RelicStateIndex is not { } relicIndex ||
            subscriber.RelicTriggerIndex != triggerIndex ||
            (uint)relicIndex >= (uint)combat.RelicStates.Length)
            return false;

        var relic = combat.RelicStates[relicIndex];
        if (relic is not
                { RelicId: NativeAncientRewardsV111.ToastyMittensId,
                  IsMelted: false } ||
            relic.PersistentIndex != relicIndex ||
            relic.ApplicationOrder != subscriber.ApplicationOrder)
            return false;

        var definition = PrototypeContent.Relic(relic.RelicId);
        var triggers = definition.Triggers ?? [];
        if ((uint)triggerIndex >= (uint)triggers.Length)
            return false;
        var trigger = triggers[triggerIndex];
        int? expectedMinimumTurn = triggerIndex == 0 ? null : 2;
        if (trigger.EventKind != eventKind || trigger.EveryNth != 1 ||
            trigger.MaxPlayerHpPercent is not null ||
            trigger.MinPlayerHpPercent is not null ||
            trigger.TurnEquals is not null ||
            trigger.MinTurn != expectedMinimumTurn ||
            trigger.RequiresZeroPlayerBlock ||
            trigger.RequiredSourceCardType is not null ||
            trigger.ResetCounterEachTurn ||
            trigger.MaxTriggersPerCounterWindow is not null ||
            trigger.MaxAttacksPlayedLastTurn is not null ||
            trigger.RequiresEmptyHand ||
            trigger.RequiredSourceCardTag is not null ||
            trigger.RequiresPlayerSide)
            return false;

        var expectedEffects = new[]
        {
            new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.ChooseCards, 0,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 1, 1,
                    PrototypeCardSelectionResolutionKind.MoveToExhaust)),
            new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.ApplyPlayerPower, 1,
                PowerId: "proto.power.strength")
        };
        if (CanonicalJson.Sha256(trigger.Effects) !=
                CanonicalJson.Sha256(expectedEffects) ||
            CanonicalJson.Sha256(new PrototypeEventSubscriberState(
                relic.ApplicationOrder, trigger.Effects, PowerStacks: 0,
                PrototypeEffectSourceKind.Relic,
                RelicStateIndex: relicIndex,
                RelicTriggerIndex: triggerIndex,
                EveryNth: trigger.EveryNth,
                MaxTriggersPerCounterWindow:
                    trigger.MaxTriggersPerCounterWindow)) !=
            CanonicalJson.Sha256(subscriber))
            return false;

        var expectedSelection = expectedEffects[0].Selection!;
        if (pending.Selection != expectedSelection ||
            !candidates.SequenceEqual(combat.Hand))
            return false;

        var expectedContinuation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.ApplyPlayerPower, 1,
            PowerId: "proto.power.strength",
            TargetMode: expectedEffects[1].Target,
            SourceKind: PrototypeEffectSourceKind.Relic);
        return CanonicalJson.Sha256(queuedOperations) ==
            CanonicalJson.Sha256(new[] { expectedContinuation });
    }

    private static bool IsOpeningGeneratedPoolShuffleContinuation(
        CombatState combat, PendingCombatChoiceState pending,
        PrototypeControlledPoolShuffleV111 shuffle,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        if (combat is not { IsPlayerTurn: true, Turn: 1,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null },
                AutomaticPipelineContinuation: not null } ||
            pending is not
                { ChoiceId: "controlled_pool_shuffle",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Selection: { SourceZone: PrototypeCardZone.Hand,
                      MinSelections: 0,
                      MaxSelections: 0,
                      Resolution:
                        PrototypeCardSelectionResolutionKind.MoveToDiscard,
                      RequiredCardType: null,
                      SelectionsPerPowerStack: 0,
                      RemoveUnselectedFromSource: false,
                      RequireEnergyCostingCard: false,
                      SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      MaxSelectionsUpgradeDelta: 0,
                      RandomCandidateCount: 0,
                      RequireAttackOrPower: false,
                      CopiesPerSelection: 0,
                      CopiesPerSelectionUpgradeDelta: 0 },
                  CandidateCardInstanceIds: { Length: 0 },
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  OuterChoiceContinuation: { } pendingContinuation,
                  EventDispatchContinuation: null,
                  ControlledTarget: null,
                  ControlledGeneration: null,
                  ControlledPotionGeneration: null,
                  ControlledCardTransform: null,
                  ControlledDrawInsertion: null,
                  ControlledPoolShuffle: { } pendingShuffle,
                  ControlledRandomCost: null,
                  ControlledDrawCost: null,
                  ControlledCardShortlist: null,
                  ControlledRandomPileCard: null,
                  ControlledSemanticDraw: null } ||
            CanonicalJson.Sha256(pendingShuffle) !=
                CanonicalJson.Sha256(shuffle) ||
            CanonicalJson.Sha256(pendingContinuation) !=
                CanonicalJson.Sha256(continuation) ||
            continuation is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: { Length: 0 },
                  PendingDiscardEvents: { Length: 0 },
                  PendingSlyCardInstanceIds: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } eventDispatch,
                  CompletePlayerHandCleanup: false })
            return false;

        return IsSupportedGeneratedChoiceSubscriber(
                combat, eventDispatch, shuffle.Operation,
                out var expectedOperation, requireOpeningCursor: true) &&
            CanonicalJson.Sha256(shuffle.Operation) ==
                CanonicalJson.Sha256(expectedOperation);
    }

    private static bool IsOpeningGeneratedCardSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        if (combat is not { IsPlayerTurn: true, Turn: 1,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null },
                AutomaticPipelineContinuation: not null } ||
            pending is not
                { ChoiceId: "select_cards",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { } candidates,
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SourceZone: PrototypeCardZone.ChoicePool,
                      MaxSelections: 1,
                      Resolution:
                        PrototypeCardSelectionResolutionKind.MoveToHand,
                      RequiredCardType: null,
                      SelectionsPerPowerStack: 0,
                      RemoveUnselectedFromSource: true,
                      RequireEnergyCostingCard: false,
                      SequentialOptional: false,
                      DrawEqualToSelectionsOnCompletion: false,
                      MaxSelectionsUpgradeDelta: 0,
                      RandomCandidateCount: 0,
                      RequireAttackOrPower: false,
                      CopiesPerSelection: 0,
                      CopiesPerSelectionUpgradeDelta: 0 } selection,
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
            !IsSupportedGeneratedChoiceSubscriber(
                combat, eventDispatch, operation: null, out var operation,
                requireOpeningCursor: true) ||
            selection.MinSelections !=
                (operation.GeneratedChoiceMustPick ? 1 : 0) ||
            candidates.Length != operation.Amount ||
            !candidates.SequenceEqual(combat.ChoiceCardIds))
            return false;

        var eligible = SourcePoolForShuffle(
            combat, operation, sourceCardInstanceId: null);
        return eligible is not null &&
            HasGeneratedChoiceInstances(
                combat, candidates, operation, eligible);
    }

    private static bool IsSupportedGeneratedChoiceSubscriber(
        CombatState combat,
        PrototypeEventDispatchContinuationState eventDispatch,
        PrototypeQueuedOperation? operation,
        out PrototypeQueuedOperation expectedOperation,
        bool requireOpeningCursor)
    {
        expectedOperation = null!;
        if (combat.Turn != 1 || eventDispatch.EventDepth != 0 ||
            eventDispatch.CombatEvent.SourceCardInstanceId is not null ||
            eventDispatch.CombatEvent.CardId is not null ||
            eventDispatch.CombatEvent.TargetEnemyId is not null ||
            eventDispatch.CombatEvent.Amount != 0 ||
            eventDispatch.CombatEvent.PowerApplicationOrderCeiling is not null ||
            eventDispatch.CombatEvent.FromHandDraw ||
            eventDispatch.CombatEvent.IsAutoPlay ||
            eventDispatch.CombatEvent.IsPoweredAttack)
            return false;

        string relicId;
        PrototypeCombatEventKind eventKind;
        PrototypeOpeningCombatStage openingStage;
        PrototypeRelicTriggerSpec expectedTrigger;
        if (eventDispatch.CombatEvent == new PrototypeCombatEvent(
                PrototypeCombatEventKind.BeforeHandDraw))
        {
            relicId = "proto.relic.toolbox";
            eventKind = PrototypeCombatEventKind.BeforeHandDraw;
            openingStage = PrototypeOpeningCombatStage.DrawHand;
            expectedOperation = new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.ChooseGeneratedCards, 3,
                SourceKind: PrototypeEffectSourceKind.Relic,
                GeneratedChoiceCardsFreeThisTurn: false);
            expectedTrigger = new PrototypeRelicTriggerSpec(
                eventKind,
                [new PrototypeCombatEffectSpec(
                    PrototypeCombatEffectKind.ChooseGeneratedCards, 3,
                    GeneratedChoiceCardsFreeThisTurn: false)],
                TurnEquals: 1);
        }
        else if (eventDispatch.CombatEvent == new PrototypeCombatEvent(
                     PrototypeCombatEventKind.OpeningPlayerTurnStarted))
        {
            relicId = NativeGloryAncientsV111.ChoicesParadoxId;
            eventKind = PrototypeCombatEventKind.OpeningPlayerTurnStarted;
            openingStage = PrototypeOpeningCombatStage.BoneTeaAfterTurnStart;
            expectedOperation = new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.ChooseGeneratedCards, 5,
                SourceKind: PrototypeEffectSourceKind.Relic,
                GeneratedChoiceMustPick: true,
                GeneratedChoiceCharacterCards: true,
                GeneratedChoiceCardsRetain: true,
                GeneratedChoiceUsesNativeCardGenerationRng: true);
            expectedTrigger = new PrototypeRelicTriggerSpec(
                eventKind,
                [new PrototypeCombatEffectSpec(
                    PrototypeCombatEffectKind.ChooseGeneratedCards, 5,
                    GeneratedChoiceMustPick: true,
                    GeneratedChoiceCharacterCards: true,
                    GeneratedChoiceCardsRetain: true,
                    GeneratedChoiceUsesNativeCardGenerationRng: true)],
                TurnEquals: 1);
        }
        else
            return false;

        if (eventDispatch.CombatEvent.Kind != eventKind ||
            eventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Relic,
                  RelicStateIndex: { } relicIndex,
                  RelicTriggerIndex: 0 } subscriber ||
            (uint)relicIndex >= (uint)combat.RelicStates.Length)
            return false;

        if (requireOpeningCursor &&
            (combat.AutomaticPipelineContinuation is not { } pipeline ||
             !IsAtOpeningAutomaticPipelineCursor(
                 combat, pipeline, openingStage)))
            return false;

        var relic = combat.RelicStates[relicIndex];
        if (relic is not
                { IsMelted: false } ||
            relic.RelicId != relicId ||
            relic.PersistentIndex != relicIndex ||
            relic.ApplicationOrder != subscriber.ApplicationOrder)
            return false;

        var definition = PrototypeContent.Relic(relic.RelicId);
        var triggers = definition.Triggers ?? [];
        if (triggers.Length != 1 ||
            CanonicalJson.Sha256(triggers[0]) !=
                CanonicalJson.Sha256(expectedTrigger) ||
            subscriber.RelicTriggerIndex != 0 ||
            operation is not null && CanonicalJson.Sha256(operation) !=
                CanonicalJson.Sha256(expectedOperation))
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            relic.ApplicationOrder, triggers[0].Effects, PowerStacks: 0,
            PrototypeEffectSourceKind.Relic,
            RelicStateIndex: relicIndex,
            RelicTriggerIndex: 0,
            EveryNth: triggers[0].EveryNth,
            MaxTriggersPerCounterWindow:
                triggers[0].MaxTriggersPerCounterWindow);
        return CanonicalJson.Sha256(expectedSubscriber) ==
            CanonicalJson.Sha256(subscriber);
    }

    private static bool IsGamblingChipOpeningSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        if (combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null },
                AutomaticPipelineContinuation: { } continuation } ||
            pending is not
                { ChoiceId: "select_cards",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CandidateCardInstanceIds: { } candidates,
                  Continuation: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  Selection: { SequentialOptional: true,
                      DrawEqualToSelectionsOnCompletion: true,
                      RemoveUnselectedFromSource: false,
                      RequiredCardType: null,
                      SelectionsPerPowerStack: 0,
                      RequireEnergyCostingCard: false,
                      MaxSelectionsUpgradeDelta: 0,
                      RandomCandidateCount: 0,
                      RequireAttackOrPower: false,
                      CopiesPerSelection: 0,
                      CopiesPerSelectionUpgradeDelta: 0 },
                  OuterChoiceContinuation: null,
                  CardPlaySeries: null,
                  EventDispatchContinuation: { } eventDispatch,
                  SelectedCardPower: null,
                  SelectedCardKeyword: null,
                  SelectedCardTemporaryCost: null,
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
            pending.Selection is not
                { SourceZone: PrototypeCardZone.Hand,
                  Resolution:
                    PrototypeCardSelectionResolutionKind.MoveToDiscard } ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, continuation,
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !TryGetGamblingChipOpeningSubscriber(
                combat, eventDispatch, out var sourceSelection))
            return false;

        var selected = pending.SequentialSelectedCardInstanceIds ?? [];
        var hand = combat.Hand;
        if (selected.Length > hand.Length ||
            selected.Any(id => !hand.Contains(id)) ||
            selected.Any(candidates.Contains) ||
            !candidates.SequenceEqual(hand.Where(id =>
                !selected.Contains(id))))
            return false;

        int maxSelections = Math.Min(sourceSelection.MaxSelections, hand.Length);
        var expectedSelection = sourceSelection with
        {
            MinSelections = Math.Min(sourceSelection.MinSelections,
                maxSelections),
            MaxSelections = maxSelections
        };
        return maxSelections > 0 &&
            selected.Length < maxSelections &&
            pending.Selection == expectedSelection;
    }

    private static bool IsGamblingChipOpeningSemanticDrawContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        if (combat is not { IsPlayerTurn: true,
                PendingKnowledgeDemonChoice: null,
                SemanticDeck: { Mode: SemanticDeckStateV111.NatureMode },
                AutomaticPipelineContinuation: { } cursor } ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, cursor,
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            pending.ControlledSemanticDraw is not { } draw ||
            pending.OuterChoiceContinuation is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: { Length: 0 },
                  PendingDiscardEvents: { Length: 0 },
                  PendingSlyCardInstanceIds: { Length: 0 },
                  CompletionEvents: { Length: 0 },
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } eventDispatch } ||
            !TryGetGamblingChipOpeningSubscriber(
                combat, eventDispatch, out _) ||
            draw is not { FromHandDraw: false, EventDepth: 1,
                DrawnCardKeyword: null,
                StratagemSelectionPending: false,
                StandaloneSemanticDrawSegment: false,
                AutomaticSemanticHandDrawSegment: false,
                DrawEqualSelectionCount: { } selectedCount })
            return false;

        var alreadyDrawn = draw.DeferredKeywordCardInstanceIds ?? [];
        if (selectedCount <= 0 || alreadyDrawn.Distinct().Count() !=
                alreadyDrawn.Length ||
            selectedCount != alreadyDrawn.Length + draw.RemainingCount)
            return false;

        bool semanticDraw = pending is
            { ChoiceId: "controlled_semantic_draw",
              SourceCardInstanceId: null,
              SourceCardDestination: PrototypeCardZone.DiscardPile,
              ControlledDrawCost: null } &&
            draw.RemainingCount > 0 &&
            combat.Stochastic.SemanticDeck!.Pending is
                { Purpose: "draw", TopOffset: 0 };
        bool drawCost = pending is
            { ChoiceId: "controlled_draw_cost",
              SourceCardInstanceId: { } cardInstanceId,
              SourceCardDestination: PrototypeCardZone.Hand,
              ControlledDrawCost: { } cost } &&
            cost.CardInstanceId == cardInstanceId &&
            cost.RemainingCount == draw.RemainingCount &&
            cost.FromHandDraw == draw.FromHandDraw &&
            cost.EventDepth == draw.EventDepth &&
            combat.Stochastic.SemanticDeck!.Pending is null;
        if (!semanticDraw && !drawCost)
            return false;

        bool commonChoiceShape =
            pending.CandidateCardInstanceIds.Length == 0 &&
            pending.Continuation.Length == 0 &&
            pending.CompletionEvents.Length == 0 &&
            pending.ControlledTarget is null &&
            pending.ControlledGeneration is null &&
            pending.ControlledPotionGeneration is null &&
            pending.ControlledCardTransform is null &&
            pending.ControlledDrawInsertion is null &&
            pending.ControlledPoolShuffle is null &&
            pending.ControlledRandomCost is null &&
            pending.ControlledCardShortlist is null &&
            pending.ControlledRandomPileCard is null &&
            pending.SemanticStratagemDrawSelection == false &&
            pending.AutomaticSemanticStratagemDrawSelection == false &&
            pending.StandaloneSemanticDrawHookChoice == false &&
            pending.AutomaticSemanticDrawHookChoice == false &&
            pending.SelectedCardPower is null &&
            pending.SelectedCardKeyword is null &&
            pending.SelectedCardTemporaryCost is null &&
            pending.SequentialSelectedCardInstanceIds is null;
        return commonChoiceShape && (semanticDraw
            ? pending.Selection is
                { SourceZone: PrototypeCardZone.Hand,
                  MinSelections: 0,
                  MaxSelections: 0,
                  SequentialOptional: false,
                  DrawEqualToSelectionsOnCompletion: false,
                  RemoveUnselectedFromSource: false,
                  Resolution: PrototypeCardSelectionResolutionKind.Preserve }
            : drawCost && pending.Selection is
                { SourceZone: PrototypeCardZone.Hand,
                  MinSelections: 0,
                  MaxSelections: 0,
                  SequentialOptional: false,
                  DrawEqualToSelectionsOnCompletion: false,
                  RemoveUnselectedFromSource: false,
                  Resolution: PrototypeCardSelectionResolutionKind.MoveToDiscard });
    }

    private static bool IsAtOpeningAutomaticPipelineCursor(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation,
        PrototypeOpeningCombatStage expectedStage) =>
        continuation.Mechanics.IsAtOpeningBoundary(expectedStage) &&
        !continuation.Stochastic.HasSavedHandDrawProgress() &&
        continuation.Stochastic is
            { ControlledEnemyAction: null,
              ControlledTurnIntents: null,
              CompletedSemanticHandDrawRequested: null } &&
        combat.Stochastic.SemanticDeck is
            { Mode: SemanticDeckStateV111.NatureMode };

    private static bool TryGetGamblingChipOpeningSubscriber(
        CombatState combat,
        PrototypeEventDispatchContinuationState eventDispatch,
        out PrototypeCardSelectionSpec selection)
    {
        selection = null!;
        if (eventDispatch is not
                { EventDepth: 0,
                  CombatEvent.Kind:
                    PrototypeCombatEventKind.OpeningPlayerTurnStarted } ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.OpeningPlayerTurnStarted) ||
            eventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Relic,
                  RelicStateIndex: { } relicIndex,
                  RelicTriggerIndex: { } triggerIndex } subscriber ||
            (uint)relicIndex >= (uint)combat.RelicStates.Length)
            return false;

        var relic = combat.RelicStates[relicIndex];
        if (relic is not
                { RelicId: "proto.relic.gambling_chip",
                  IsMelted: false } ||
            relic.PersistentIndex != relicIndex ||
            relic.ApplicationOrder != subscriber.ApplicationOrder)
            return false;

        var definition = PrototypeContent.Relic(relic.RelicId);
        var triggers = definition.Triggers ?? [];
        if ((uint)triggerIndex >= (uint)triggers.Length)
            return false;
        var trigger = triggers[triggerIndex];
        if (trigger is not
                { EventKind:
                    PrototypeCombatEventKind.OpeningPlayerTurnStarted,
                  EveryNth: 1,
                  MaxPlayerHpPercent: null,
                  MinPlayerHpPercent: null,
                  TurnEquals: null,
                  MinTurn: null,
                  RequiresZeroPlayerBlock: false,
                  RequiredSourceCardType: null,
                  ResetCounterEachTurn: false,
                  MaxTriggersPerCounterWindow: null,
                  MaxAttacksPlayedLastTurn: null,
                  RequiresEmptyHand: false,
                  RequiredSourceCardTag: null,
                  RequiresPlayerSide: false,
                  Effects: { Length: 1 } } ||
            trigger.Effects[0] is not
                { Kind: PrototypeCombatEffectKind.ChooseCards,
                  Amount: 0,
                  Selection: { } sourceSelection } ||
            sourceSelection is not
                { SourceZone: PrototypeCardZone.Hand,
                  MinSelections: 0,
                  MaxSelections: 999999999,
                  Resolution:
                    PrototypeCardSelectionResolutionKind.MoveToDiscard,
                  RequiredCardType: null,
                  SelectionsPerPowerStack: 0,
                  RemoveUnselectedFromSource: false,
                  RequireEnergyCostingCard: false,
                  SequentialOptional: true,
                  DrawEqualToSelectionsOnCompletion: true,
                  MaxSelectionsUpgradeDelta: 0,
                  RandomCandidateCount: 0,
                  RequireAttackOrPower: false,
                  CopiesPerSelection: 0,
                  CopiesPerSelectionUpgradeDelta: 0 })
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            relic.ApplicationOrder, trigger.Effects, PowerStacks: 0,
            PrototypeEffectSourceKind.Relic,
            RelicStateIndex: relicIndex,
            RelicTriggerIndex: triggerIndex,
            EveryNth: trigger.EveryNth,
            MaxTriggersPerCounterWindow:
                trigger.MaxTriggersPerCounterWindow);
        if (CanonicalJson.Sha256(expectedSubscriber) !=
            CanonicalJson.Sha256(subscriber))
            return false;
        selection = sourceSelection;
        return true;
    }

    private static bool IsPlayerTurnStartedDiscardSelection(
        CombatState combat, PendingCombatChoiceState pending)
    {
        if (combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: { } continuation,
                PendingKnowledgeDemonChoice: null } ||
            combat.Stochastic.SemanticDeck is not
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

        if (!continuation.Mechanics.IsImmediatelyAfterDispatch(
                PrototypeCombatEventKind.PlayerTurnStarted))
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
            combat.Stochastic.SemanticDeck is not
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

    internal static bool
        IsPlayerTurnStartedToolsOfTheTradeTingshaTargetBoundary(
            CombatState combat)
    {
        if (combat is not
                { IsPlayerTurn: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice: { } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !IsAtSupportedAutomaticPipelineTargetBoundary(combat) ||
            !IsAtPlayerTurnStartedDispatchContinuation(combat, pipeline) ||
            !HasSupportedTingshaDiscardTargetFrame(
                combat, pending, pending.ControlledTarget,
                out var continuation, out _) ||
            continuation.Parent is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: [],
                  PendingDiscardEvents: { },
                  PendingSlyCardInstanceIds: { },
                  CompletionEvents: [],
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } eventDispatch,
                  CompletePlayerHandCleanup: false } ||
            !IsPlayerTurnStartedToolsOfTheTradeSubscriber(
                combat, eventDispatch, out var maxSelections) ||
            !HasSupportedTingshaDiscardEventRemainder(
                combat, continuation, maxSelections))
            return false;

        return true;
    }

    private static bool IsPlayerTurnStartedToolsOfTheTradeSubscriber(
        CombatState combat,
        PrototypeEventDispatchContinuationState eventDispatch,
        out int maxSelections)
    {
        maxSelections = 0;
        const string toolsOfTheTradePowerId =
            "proto.power.tools_of_the_trade";
        if (eventDispatch is not
                { EventDepth: 0,
                  CombatEvent.Kind: PrototypeCombatEventKind.PlayerTurnStarted } ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.PlayerTurnStarted) ||
            eventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Power,
                  SourcePowerApplicationOrder: { } powerOrder,
                  SourcePowerEnemyId: null,
                  PowerTriggerIndex: 0 } subscriber)
            return false;

        var power = combat.PlayerPowers.SingleOrDefault(candidate =>
            candidate.ApplicationOrder == powerOrder);
        var definition = PrototypeContent.Power(toolsOfTheTradePowerId);
        var expectedSelection = new PrototypeCardSelectionSpec(
            PrototypeCardZone.Hand,
            0,
            0,
            PrototypeCardSelectionResolutionKind.MoveToDiscard,
            SelectionsPerPowerStack: 1);
        var expectedTrigger = new PrototypePowerTriggerSpec(
            PrototypeCombatEventKind.PlayerTurnStarted,
            [new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.ChooseCards,
                0,
                Selection: expectedSelection)]);
        if (power is not
                { PowerId: toolsOfTheTradePowerId, Stacks: > 0 } ||
            definition.Triggers is not [{ } trigger] ||
            CanonicalJson.Sha256(trigger) !=
                CanonicalJson.Sha256(expectedTrigger))
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            power.ApplicationOrder, trigger.Effects, power.Stacks,
            PrototypeEffectSourceKind.Power,
            SourcePowerApplicationOrder: power.ApplicationOrder,
            PowerCardPayload: power.CardPayload?.Fork(),
            RemoveSourcePowerAfterTrigger: trigger.RemoveSourcePowerAfterTrigger,
            EveryNth: trigger.EveryNth,
            PowerTriggerIndex: 0,
            CountdownBeforeTrigger: trigger.CountdownBeforeTrigger,
            SourcePowerStoredValue: power.StoredValue);
        if (CanonicalJson.Sha256(expectedSubscriber) !=
            CanonicalJson.Sha256(subscriber))
            return false;

        maxSelections = power.Stacks;
        return true;
    }

    private static bool IsAtPlayerTurnStartedDispatchContinuation(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        return combat.IsPlayerTurn &&
            continuation.Mechanics.IsImmediatelyAfterDispatch(
                PrototypeCombatEventKind.PlayerTurnStarted) &&
            !continuation.Stochastic.HasSavedHandDrawProgress() &&
            continuation.Stochastic is
                { ControlledEnemyAction: null,
                  ControlledTurnIntents: null } &&
            continuation.Mechanics.IsImmediatelyAfterDispatch(
                PrototypeCombatEventKind.PlayerTurnStarted);
    }

    internal static bool IsAutomaticPipelineSemanticDrawHookChoiceContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (!pending.StandaloneSemanticDrawHookChoice ||
            pending.AutomaticSemanticDrawHookChoice ||
            combat.Stochastic.SemanticDeck is not
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

    internal static bool IsSupportedOpeningAutoplayTargetBoundary(
        CombatState combat)
    {
        return IsWhisperingEarringOpeningAutoplayBoundary(combat) ||
            IsImbuedOpeningAutoplayContinuation(combat) ||
            IsGamblingChipOpeningNestedEffectTargetBoundary(combat) ||
            IsGamblingChipOpeningTingshaTargetBoundary(combat) ||
            IsGamblingChipOpeningSlyTargetBoundary(combat) ||
            IsMayhemOpeningAutoplayTargetBoundary(combat);
    }

    internal static bool HasSupportedTingshaDiscardTargetFrame(
        CombatState? combat,
        PendingCombatChoiceState? choice,
        PrototypeControlledTargetChoiceV111? target,
        out PrototypeChoiceResolutionContinuationState continuation,
        out PrototypeEventDispatchContinuationState eventDispatch)
    {
        continuation = null!;
        eventDispatch = null!;
        if (combat is not { IsPlayerTurn: true,
                IsPlayerSideActive: true,
                PendingKnowledgeDemonChoice: null } ||
            target is not { Kind: "effect_random_enemy",
                SlyCardInstanceId: null, AutoPlayCardInstanceId: null } ||
            choice is not
                { ChoiceId: "controlled_random_target",
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  CompletionEvents: [],
                  OuterChoiceContinuation: { } savedContinuation } pending ||
            savedContinuation is not
                { SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: [{ } targetOperation],
                  PendingDiscardEvents: [],
                  PendingSlyCardInstanceIds: [],
                  CompletionEvents: [],
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } savedEventDispatch } ||
            savedEventDispatch is not
                { EventDepth: 0,
                  CombatEvent:
                    { Kind: PrototypeCombatEventKind.CardDiscarded,
                      SourceCardInstanceId: { } discardedCardInstanceId,
                      CardId: { } discardedCardId,
                      TargetEnemyId: null,
                      Amount: 0,
                      PowerApplicationOrderCeiling: null,
                      FromHandDraw: false,
                      IsAutoPlay: false,
                      IsPoweredAttack: false } } ||
            !combat.DiscardPile.Contains(discardedCardInstanceId) ||
            combat.Cards.SingleOrDefault(card =>
                card.InstanceId == discardedCardInstanceId)?.CardId !=
                    discardedCardId ||
            CanonicalJson.Sha256(savedEventDispatch.CombatEvent) !=
                CanonicalJson.Sha256(new PrototypeCombatEvent(
                    PrototypeCombatEventKind.CardDiscarded,
                    SourceCardInstanceId: discardedCardInstanceId,
                    CardId: discardedCardId)) ||
            CanonicalJson.Sha256(targetOperation) !=
                CanonicalJson.Sha256(new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.DamageEnemy, 3,
                    TargetMode: PrototypeEffectTarget.ActionTargetEnemy,
                    SourceKind: PrototypeEffectSourceKind.Relic)) ||
            !SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending))
            return false;

        var relicTriggers = PrototypeContent.Relic("proto.relic.tingsha")
            .Triggers;
        var expectedTrigger = new PrototypeRelicTriggerSpec(
            PrototypeCombatEventKind.CardDiscarded,
            [new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind.DamageEnemy, 3,
                Target: PrototypeEffectTarget.RandomEnemy)],
            RequiresPlayerSide: true);
        if (relicTriggers is not [{ } trigger] ||
            CanonicalJson.Sha256(trigger) !=
                CanonicalJson.Sha256(expectedTrigger) ||
            savedEventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Relic,
                  RelicStateIndex: { } relicIndex,
                  RelicTriggerIndex: 0 } subscriber ||
            (uint)relicIndex >= (uint)combat.RelicStates.Length ||
            combat.RelicStates[relicIndex] is not
                { RelicId: "proto.relic.tingsha", IsMelted: false } relic ||
            relic.PersistentIndex != relicIndex ||
            CanonicalJson.Sha256(subscriber) != CanonicalJson.Sha256(
                new PrototypeEventSubscriberState(
                    relic.ApplicationOrder, trigger.Effects, 0,
                    PrototypeEffectSourceKind.Relic,
                    RelicStateIndex: relicIndex,
                    RelicTriggerIndex: 0,
                    EveryNth: trigger.EveryNth,
                    MaxTriggersPerCounterWindow:
                        trigger.MaxTriggersPerCounterWindow)))
            return false;

        continuation = savedContinuation;
        eventDispatch = savedEventDispatch;
        return true;
    }

    internal static bool HasSupportedTingshaDiscardEventRemainder(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState continuation,
        int? selectedCount)
    {
        if (selectedCount is <= 0 ||
            continuation.EventDispatchContinuation?.CombatEvent
                .SourceCardInstanceId is not { } currentDiscardedCardId ||
            continuation.Parent is not
                { PendingDiscardEvents: { } pendingDiscardEvents,
                  PendingSlyCardInstanceIds: { } pendingSlyCardInstanceIds } ||
            selectedCount is { } boundedSelectionCount &&
                (pendingDiscardEvents.Length >= boundedSelectionCount ||
                 pendingSlyCardInstanceIds.Length > boundedSelectionCount))
            return false;

        if (!HasSupportedLivePendingSlyAutoplayQueue(
                combat, pendingSlyCardInstanceIds))
            return false;

        var seenDiscardedCards = new HashSet<long>
        {
            currentDiscardedCardId
        };
        foreach (var discardEvent in pendingDiscardEvents)
        {
            if (discardEvent is not
                    { Kind: PrototypeCombatEventKind.CardDiscarded,
                      SourceCardInstanceId: { } discardedCardId,
                      CardId: { } cardId } ||
                !seenDiscardedCards.Add(discardedCardId) ||
                !combat.DiscardPile.Contains(discardedCardId) ||
                combat.Cards.SingleOrDefault(card =>
                    card.InstanceId == discardedCardId)?.CardId != cardId ||
                CanonicalJson.Sha256(discardEvent) !=
                    CanonicalJson.Sha256(new PrototypeCombatEvent(
                        PrototypeCombatEventKind.CardDiscarded,
                        SourceCardInstanceId: discardedCardId,
                        CardId: cardId)))
                return false;
        }

        return true;
    }

    private static bool HasSupportedLivePendingSlyAutoplayQueue(
        CombatState combat, long[] cardInstanceIds)
    {
        return cardInstanceIds.Distinct().Count() == cardInstanceIds.Length &&
            cardInstanceIds.All(cardInstanceId =>
            {
                var card = combat.Cards.SingleOrDefault(item =>
                    item.InstanceId == cardInstanceId);
                return card is not null &&
                    combat.DiscardPile.Contains(cardInstanceId) &&
                    CardHasKeyword(combat, cardInstanceId,
                        PrototypeCardKeyword.Sly);
            });
    }

    internal static bool IsDistilledChaosTingshaTargetBoundary(
        CombatState? combat,
        PendingCombatChoiceState? choice,
        PrototypeControlledTargetChoiceV111? target)
    {
        if (combat is not
                { IsPlayerTurn: true,
                  AutomaticPipelineContinuation: null,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null } } ||
            !HasSupportedTingshaDiscardTargetFrame(
                combat, choice, target, out var continuation, out _) ||
            !HasActiveAutoplayCardPlaySeries(combat, continuation))
            return false;

        var root = continuation;
        while (root.Parent is { } parent)
            root = parent;

        if (root is not
                { SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: { Length: <= 2 } operations,
                  PendingDiscardEvents: [],
                  PendingSlyCardInstanceIds: [],
                  CompletionEvents: [{ } completion],
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: null,
                  Parent: null,
                  CompletePlayerHandCleanup: false } ||
            CanonicalJson.Sha256(completion) !=
                CanonicalJson.Sha256(new PrototypeCombatEvent(
                    PrototypeCombatEventKind.PotionUsed)) ||
            !operations.All(operation =>
                operation.CardInstanceId is { } cardInstanceId &&
                CanonicalJson.Sha256(operation) ==
                    CanonicalJson.Sha256(new PrototypeQueuedOperation(
                        PrototypeCombatEffectKind.AutoPlayCombatCard,
                        0,
                        SourceKind: PrototypeEffectSourceKind.Potion,
                        CardInstanceId: cardInstanceId)) &&
                combat.PlayCardIds.Contains(cardInstanceId)) ||
            operations.Select(operation => operation.CardInstanceId)
                .Distinct().Count() != operations.Length)
            return false;

        var expectedPotionEffect = new PrototypeCombatEffectSpec(
            PrototypeCombatEffectKind.AutoPlayTopDrawCards, 3);
        return CanonicalJson.Sha256(
                PrototypeContent.Potion("proto.potion.distilled_chaos").Effects) ==
            CanonicalJson.Sha256(new[] { expectedPotionEffect });
    }

    internal static bool IsGamblingChipOpeningTingshaTargetBoundary(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { ControlledTarget: { } target } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, pipeline,
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !HasSupportedTingshaDiscardTargetFrame(
                combat, pending, target, out var continuation, out _) ||
            continuation.Parent is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: [{ Kind: PrototypeCombatEffectKind.DrawCards,
                      Amount: > 0,
                      DrawEqualSelectionCount: { } selectedCount }],
                  PendingDiscardEvents: { },
                  PendingSlyCardInstanceIds: { },
                  CompletionEvents: [],
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } openingEventDispatch,
                  CompletePlayerHandCleanup: false } parent ||
            parent.Operations[0].Amount != selectedCount ||
            !HasSupportedTingshaDiscardEventRemainder(
                combat, continuation, selectedCount) ||
            CanonicalJson.Sha256(parent.Operations[0]) !=
                CanonicalJson.Sha256(new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.DrawCards, selectedCount,
                    DrawEqualSelectionCount: selectedCount)) ||
            !TryGetGamblingChipOpeningSubscriber(
                combat, openingEventDispatch, out _))
            return false;

        return true;
    }

    private static bool IsGamblingChipOpeningSlyTargetBoundary(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { ChoiceId: "controlled_random_target",
                      ControlledTarget:
                        { Kind: "sly_auto_play",
                          SlyCardInstanceId: { } slyCardInstanceId,
                          AutoPlayCardInstanceId: null },
                      OuterChoiceContinuation: { } continuation } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, pipeline,
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !IsLiveSlyAutoplaySource(combat, slyCardInstanceId) ||
            !SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending) ||
            continuation is not
                { Parent: null,
                  SourceCardInstanceId: null,
                  SourceCardDestination: PrototypeCardZone.DiscardPile,
                  Operations: [{ Kind: PrototypeCombatEffectKind.DrawCards,
                      Amount: > 0,
                      DrawEqualSelectionCount: { } selectedCount }],
                  PendingDiscardEvents: [],
                  PendingSlyCardInstanceIds: { } pendingSlyCardInstanceIds,
                  CompletionEvents: [],
                  CardPlaySeries: null,
                  MoveSourceCardOnCompletion: true,
                  RemoveSourceCardOnCompletion: false,
                  SourceCardAlreadyMoved: false,
                  EventDispatchContinuation: { } openingEventDispatch,
                  CompletePlayerHandCleanup: false } ||
            pendingSlyCardInstanceIds.Length >= selectedCount ||
            !HasSupportedLivePendingSlyAutoplayQueue(
                combat, pendingSlyCardInstanceIds) ||
            continuation.Operations[0].Amount != selectedCount ||
            CanonicalJson.Sha256(continuation.Operations[0]) !=
                CanonicalJson.Sha256(new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.DrawCards, selectedCount,
                    DrawEqualSelectionCount: selectedCount)) ||
            !TryGetGamblingChipOpeningSubscriber(
                combat, openingEventDispatch, out _))
            return false;

        return true;
    }

    private static bool IsGamblingChipOpeningNestedEffectTargetBoundary(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { ChoiceId: "controlled_random_target",
                      ControlledTarget:
                        { Kind: "effect_random_enemy",
                          SlyCardInstanceId: null,
                          AutoPlayCardInstanceId: null },
                      OuterChoiceContinuation: { } continuation } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, pipeline,
                PrototypeOpeningCombatStage.BoneTeaAfterTurnStart) ||
            !SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending) ||
            !HasActiveAutoplayCardPlaySeries(combat, continuation))
            return false;

        for (var frame = continuation; frame is not null;
             frame = frame.Parent)
        {
            if (frame.EventDispatchContinuation is { } eventDispatch &&
                TryGetGamblingChipOpeningSubscriber(
                    combat, eventDispatch, out _))
                return true;
        }

        return false;
    }

    private static bool IsMayhemOpeningAutoplayTargetBoundary(
        CombatState combat)
    {
        if (IsMayhemOpeningNestedEffectTargetBoundary(combat))
            return true;

        if (IsMayhemOpeningSlyAutoplayTargetBoundary(combat))
            return true;

        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { ChoiceId: "controlled_random_target",
                      ControlledTarget:
                        { Kind: "autoplay_random_enemy",
                          SlyCardInstanceId: null,
                          AutoPlayCardInstanceId: { } cardInstanceId },
                      OuterChoiceContinuation: { } continuation } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy) ||
            !IsMayhemAutoPrePlayPipelineCursor(combat) ||
            continuation.Operations is not { Length: > 0 } operations ||
            operations[0] is not
                { Kind: PrototypeCombatEffectKind.AutoPlayCombatCard,
                  Amount: 0, TargetEnemyId: null,
                  SourceKind: PrototypeEffectSourceKind.Power,
                  CardInstanceId: { } operationCardInstanceId } ||
            operationCardInstanceId != cardInstanceId ||
            !SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending))
            return false;

        return true;
    }

    private static bool IsMayhemOpeningNestedEffectTargetBoundary(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { ChoiceId: "controlled_random_target",
                      ControlledTarget:
                        { Kind: "effect_random_enemy",
                          SlyCardInstanceId: null,
                          AutoPlayCardInstanceId: null },
                      OuterChoiceContinuation: { } continuation } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy) ||
            !IsMayhemAutoPrePlayPipelineCursor(combat) ||
            !SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending))
            return false;

        return HasActiveAutoplayCardPlaySeries(combat, continuation);
    }

    private static bool IsMayhemOpeningSlyAutoplayTargetBoundary(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { ChoiceId: "controlled_random_target",
                      ControlledTarget:
                        { Kind: "sly_auto_play",
                          SlyCardInstanceId: { } slyCardInstanceId,
                          AutoPlayCardInstanceId: null },
                      OuterChoiceContinuation: { } continuation } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !pipeline.Mechanics.IsAtOpeningBoundary(
                PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy) ||
            !IsMayhemAutoPrePlayPipelineCursor(combat) ||
            !IsLiveSlyAutoplaySource(combat, slyCardInstanceId) ||
            !SemanticTargetChoiceV111.HasValidLiveSupport(combat, pending))
            return false;

        for (var frame = continuation; frame is not null;
             frame = frame.Parent)
        {
            if (frame.CardPlaySeries is
                    { IsAutoPlay: true } series &&
                combat.Cards.Any(card =>
                    card.InstanceId == series.SourceCardInstanceId) &&
                !combat.PlayCardIds.Contains(series.SourceCardInstanceId))
                return true;
        }

        return false;
    }

    private static bool HasActiveAutoplayCardPlaySeries(
        CombatState combat,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        for (var frame = continuation; frame is not null;
             frame = frame.Parent)
        {
            if (frame.CardPlaySeries is
                    { IsAutoPlay: true } series &&
                combat.Cards.Any(card =>
                    card.InstanceId == series.SourceCardInstanceId) &&
                !combat.PlayCardIds.Contains(series.SourceCardInstanceId))
                return true;
        }

        return false;
    }

    private static bool IsWhisperingEarringOpeningAutoplayBoundary(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { OuterChoiceContinuation: { } root } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, pipeline,
                PrototypeOpeningCombatStage.Complete))
            return false;

        bool hasWhisperingEarring = false;
        for (var index = 0; index < combat.RelicStates.Length; index++)
        {
            var relic = combat.RelicStates[index];
            if (relic is
                    { RelicId: NativeGloryAncientsV111.WhisperingEarringId,
                      IsMelted: false } &&
                relic.PersistentIndex == index)
            {
                hasWhisperingEarring = true;
                break;
            }
        }
        if (!hasWhisperingEarring)
            return false;

        bool hasWhisperingEarringDriver = false;
        bool hasAutoplaySeries = false;
        bool hasPreplayTarget = false;
        var target = pending.ControlledTarget;
        for (var frame = root; frame is not null; frame = frame.Parent)
        {
            hasAutoplaySeries |= frame.CardPlaySeries is
                { IsAutoPlay: true };
            if (frame.Operations is { Length: 1 } &&
                IsWhisperingEarringDriverFrame(
                    frame, frame.Operations[0]))
                hasWhisperingEarringDriver = true;

            if (target is
                    { Kind: "autoplay_random_enemy",
                      AutoPlayCardInstanceId: { } autoPlayCardId } &&
                IsWhisperingEarringPreplayTargetFrame(
                    frame, autoPlayCardId))
            {
                hasWhisperingEarringDriver = true;
                hasPreplayTarget = true;
            }
        }

        return hasWhisperingEarringDriver &&
            (hasAutoplaySeries || hasPreplayTarget);
    }

    private static bool IsImbuedOpeningAutoplayContinuation(
        CombatState combat)
    {
        if (combat is not
                { Turn: 1, IsPlayerTurn: true, IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  PendingChoice:
                    { OuterChoiceContinuation: { } root } pending,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null },
                  AutomaticPipelineContinuation: { } pipeline } ||
            !IsAtOpeningAutomaticPipelineCursor(
                combat, pipeline,
                PrototypeOpeningCombatStage.LateRelicAutoPlay))
            return false;

        var triggeredImbuedCards = combat.Cards.Where(card =>
                card.Enchantment is
                    { Kind: PrototypeCardEnchantmentKind.Imbued } &&
                card.EnchantmentTriggeredThisCombat &&
                combat.PlayCardIds.Contains(card.InstanceId))
            .OrderBy(card => card.PersistentCardInstanceId).ToArray();
        var driverOperations = triggeredImbuedCards.Select(card =>
            new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.AutoPlayCombatCard, 0,
                SourceKind: PrototypeEffectSourceKind.Card,
                CardInstanceId: card.InstanceId)).ToArray();
        bool suspendedBeforeTarget = driverOperations.Length > 0 &&
            pending.ControlledTarget is
                { Kind: "autoplay_random_enemy",
                  AutoPlayCardInstanceId: { } targetCardId } &&
            targetCardId == triggeredImbuedCards[0].InstanceId &&
            IsImbuedDriverContinuation(root, driverOperations);

        bool suspendedWithinAutoplay = false;
        for (var frame = root; frame is not null; frame = frame.Parent)
        {
            if (frame.CardPlaySeries is
                    { IsAutoPlay: true } series &&
                IsActiveImbuedAutoplaySource(
                    combat, series.SourceCardInstanceId,
                    triggeredImbuedCards))
            {
                for (var parent = frame.Parent; parent is not null;
                     parent = parent.Parent)
                {
                    if (!IsImbuedDriverContinuation(
                            parent, driverOperations))
                        continue;
                    suspendedWithinAutoplay = true;
                    break;
                }
                if (suspendedWithinAutoplay)
                    break;
            }
        }

        return suspendedBeforeTarget || suspendedWithinAutoplay;
    }

    private static bool IsActiveImbuedAutoplaySource(
        CombatState combat, long sourceCardInstanceId,
        CombatCardInstance[] queuedImbuedCards)
    {
        var sourceCard = combat.Cards.FirstOrDefault(card =>
            card.InstanceId == sourceCardInstanceId);
        if (sourceCard is not
                { Enchantment:
                    { Kind: PrototypeCardEnchantmentKind.Imbued },
                  EnchantmentTriggeredThisCombat: true,
                  PersistentCardInstanceId: { } sourcePersistentId } ||
            combat.PlayCardIds.Contains(sourceCardInstanceId))
            return false;

        return queuedImbuedCards.All(card =>
            card.PersistentCardInstanceId is { } queuedPersistentId &&
            queuedPersistentId > sourcePersistentId);
    }

    private static bool IsImbuedDriverContinuation(
        PrototypeChoiceResolutionContinuationState root,
        PrototypeQueuedOperation[] operations)
    {
        var expected = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DiscardPile,
            Operations: operations,
            PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [],
            CompletionEvents: [],
            MoveSourceCardOnCompletion: false);
        return CanonicalJson.Sha256(root) == CanonicalJson.Sha256(expected);
    }

    private static bool IsWhisperingEarringDriverOperation(
        PrototypeQueuedOperation operation) =>
        operation.Amount is >= 0 and
            < NativeGloryAncientsV111.WhisperingEarringMaxCards &&
        CanonicalJson.Sha256(new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.AutoPlayPlayableCardsFromHand,
            operation.Amount,
            SourceKind: PrototypeEffectSourceKind.Relic)) ==
        CanonicalJson.Sha256(operation);

    private static bool IsWhisperingEarringDriverFrame(
        PrototypeChoiceResolutionContinuationState frame,
        PrototypeQueuedOperation operation)
    {
        if (!IsWhisperingEarringDriverOperation(operation))
            return false;
        var expected = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DiscardPile,
            Operations: [new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.AutoPlayPlayableCardsFromHand,
                operation.Amount,
                SourceKind: PrototypeEffectSourceKind.Relic)],
            PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [],
            CompletionEvents: []);
        return CanonicalJson.Sha256(expected) ==
            CanonicalJson.Sha256(frame);
    }

    private static bool IsWhisperingEarringPreplayTargetFrame(
        PrototypeChoiceResolutionContinuationState frame,
        long autoPlayCardId)
    {
        if (frame.Operations is not { Length: 2 } operations ||
            operations[0] is not
                { Kind: PrototypeCombatEffectKind.AutoPlayCombatCard,
                  Amount: 0, TargetEnemyId: null,
                  SourceKind: PrototypeEffectSourceKind.Relic,
                  EnergySpent: { } energySpent,
                  CardInstanceId: { } operationCardId } ||
            operationCardId != autoPlayCardId ||
            !IsWhisperingEarringDriverOperation(operations[1]))
            return false;
        var expected = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DiscardPile,
            Operations:
            [
                new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.AutoPlayCombatCard, 0,
                    SourceKind: PrototypeEffectSourceKind.Relic,
                    CardInstanceId: autoPlayCardId,
                    EnergySpent: energySpent),
                new PrototypeQueuedOperation(
                    PrototypeCombatEffectKind.AutoPlayPlayableCardsFromHand,
                    operations[1].Amount,
                    SourceKind: PrototypeEffectSourceKind.Relic)
            ],
            PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [],
            CompletionEvents: []);
        return CanonicalJson.Sha256(expected) ==
            CanonicalJson.Sha256(frame);
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
            combat.Stochastic.SemanticDeck is not
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
            combat.Stochastic.SemanticDeck is not null ||
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

        bool openingContinuation = pipeline.Mechanics.IsAtOpeningBoundary(
            PrototypeOpeningCombatStage.AfterAutoPrePlayPolicy);

        bool turnContinuation = pipeline.Mechanics.IsImmediatelyAfterDispatch(
            PrototypeCombatEventKind.AutoPrePlayPhaseEntered);

        return openingContinuation || turnContinuation;
    }

}
