namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// The original ordered source pools, with unchanged eligibility tests.
    /// Only these five simple one-at-a-time generation effects support an
    /// explicitly resumable Nature ticket. Other random sites fail closed.
    /// </summary>
    internal static string[]? GeneratedCardModelPool(
        PrototypeQueuedOperation operation) => operation.Kind switch
    {
        PrototypeCombatEffectKind.CreateRandomFreeCharacterCardInHand =>
            PrototypeContent.RewardCardPool.Where(id =>
            {
                var card = PrototypeContent.Card(id);
                return card.MechanicsImplemented && card.CanBeGeneratedInCombat
                    && !card.MultiplayerOnly
                    && card.Type is (PrototypeCardType.Attack
                        or PrototypeCardType.Skill or PrototypeCardType.Power);
            }).ToArray(),

        PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand =>
            PrototypeContent.RewardCardPool.Where(id =>
            {
                var card = PrototypeContent.Card(id);
                return card.MechanicsImplemented && card.CanBeGeneratedInCombat
                    && !card.MultiplayerOnly
                    && card.Cost.Kind == PrototypeCardCostKind.Fixed
                    && card.Cost.Amount == 0;
            }).ToArray(),

        PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand or
        PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand or
        PrototypeCombatEffectKind.CreateRandomCharacterPowerCardsInHand =>
            ControlledTypedGenerationPool(operation.Kind),

        _ => null
    };

    private static string[] ControlledTypedGenerationPool(
        PrototypeCombatEffectKind kind)
    {
        var type = kind switch
        {
            PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand =>
                PrototypeCardType.Attack,
            PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand =>
                PrototypeCardType.Skill,
            PrototypeCombatEffectKind.CreateRandomCharacterPowerCardsInHand =>
                PrototypeCardType.Power,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var pool = type == PrototypeCardType.Attack
            ? PrototypeContent.RewardCardPool
            : PrototypeContent.NativeSilentCardPool;
        return pool.Where(id =>
        {
            var card = PrototypeContent.Card(id);
            return card.Type == type
                && card.MechanicsImplemented
                && card.CanBeGeneratedInCombat
                && card.Rarity is (PrototypeCardRarity.Common or
                    PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare)
                && !card.MultiplayerOnly;
        }).ToArray();
    }

    internal static bool ValidateControlledGenerationSupport(
        PrototypeControlledGeneratedCardV111 choice) =>
        choice.Operation.Amount > 0 &&
        GeneratedCardModelPool(choice.Operation) is { } expected &&
        expected.Length > 0 &&
        choice.CandidateCardIds.SequenceEqual(expected);

    internal static bool IsCrossbowTurnStartGenerationContinuation(
        CombatState combat, PendingCombatChoiceState pending)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(pending);
        if (combat is not
                { Turn: >= 1, IsPlayerTurn: true,
                  IsPlayerSideActive: true,
                  PendingKnowledgeDemonChoice: null,
                  SemanticDeck:
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: null } } ||
            pending.ControlledGeneration is not { } generation ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledGenerationSupport(generation))
            return false;

        var pipeline = combat.AutomaticPipelineContinuation;
        if (pipeline is null ||
            !(combat.Turn == 1
                ? pipeline.Mechanics.IsAtOpeningBoundary(
                    PrototypeOpeningCombatStage.BeforeHandDraw)
                : combat.Turn >= 2 && pipeline.Mechanics.IsAtStep(
                    PrototypeAutomaticStepKind.DrawPlayerHand)) ||
            pipeline.Stochastic is not
                { ControlledEnemyAction: null,
                  ControlledTurnIntents: null,
                  CompletedSemanticHandDrawRequested: null } ||
            pipeline.Stochastic.HasSavedHandDrawProgress() ||
            continuation.EventDispatchContinuation is not
                { EventDepth: 0,
                  RemainingSubscribers: { } remainingSubscribers }
                eventDispatch ||
            eventDispatch.CombatEvent != new PrototypeCombatEvent(
                PrototypeCombatEventKind.PlayerSideTurnStarted) ||
            eventDispatch.CurrentSubscriber is not
                { SourceKind: PrototypeEffectSourceKind.Relic,
                  RelicStateIndex: { } relicIndex,
                  RelicTriggerIndex: 0 } subscriber ||
            (uint)relicIndex >= (uint)combat.RelicStates.Length)
            return false;

        var relic = combat.RelicStates[relicIndex];
        if (relic is not
                { RelicId: NativeGloryAncientsV111.CrossbowId,
                  IsMelted: false } ||
            relic.PersistentIndex != relicIndex ||
            relic.ApplicationOrder != subscriber.ApplicationOrder)
            return false;

        var triggers = PrototypeContent.Relic(relic.RelicId).Triggers ?? [];
        var expectedTrigger = new PrototypeRelicTriggerSpec(
            PrototypeCombatEventKind.PlayerSideTurnStarted,
            [new PrototypeCombatEffectSpec(
                PrototypeCombatEffectKind
                    .CreateRandomCharacterAttackCardsInHand,
                1, GeneratedCharacterCardsFreeThisTurn: true)]);
        if (triggers.Length != 1 ||
            CanonicalJson.Sha256(triggers[0]) !=
                CanonicalJson.Sha256(expectedTrigger))
            return false;

        var expectedSubscriber = new PrototypeEventSubscriberState(
            relic.ApplicationOrder, triggers[0].Effects, PowerStacks: 0,
            PrototypeEffectSourceKind.Relic,
            RelicStateIndex: relicIndex,
            RelicTriggerIndex: 0,
            EveryNth: triggers[0].EveryNth,
            MaxTriggersPerCounterWindow:
                triggers[0].MaxTriggersPerCounterWindow);
        if (CanonicalJson.Sha256(expectedSubscriber) !=
                CanonicalJson.Sha256(subscriber) ||
            !AreSupportedCrossbowRemainderSubscribers(
                combat, remainingSubscribers))
            return false;

        var expectedOperation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand,
            1, SourceKind: PrototypeEffectSourceKind.Relic,
            GeneratedCharacterCardsFreeThisTurn: true);
        if (CanonicalJson.Sha256(generation.Operation) !=
            CanonicalJson.Sha256(expectedOperation))
            return false;

        var expectedContinuation =
            new PrototypeChoiceResolutionContinuationState(
                SourceCardInstanceId: null,
                SourceCardDestination: PrototypeCardZone.DiscardPile,
                Operations: [],
                PendingDiscardEvents: [],
                PendingSlyCardInstanceIds: [],
                CompletionEvents: [],
                EventDispatchContinuation: eventDispatch.Fork());
        var expectedPending = new PendingCombatChoiceState(
            ChoiceId: "controlled_generated_card",
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DiscardPile,
            Selection: new PrototypeCardSelectionSpec(
                PrototypeCardZone.Hand, 0, 0,
                PrototypeCardSelectionResolutionKind.MoveToDiscard),
            CandidateCardInstanceIds: [],
            Continuation: [],
            CompletionEvents: [],
            OuterChoiceContinuation: expectedContinuation,
            ControlledGeneration:
                new PrototypeControlledGeneratedCardV111(
                    generation.Operation with { },
                    (string[])generation.CandidateCardIds.Clone()));
        return CanonicalJson.Sha256(expectedPending) ==
            CanonicalJson.Sha256(pending);
    }

    private static bool AreSupportedCrossbowRemainderSubscribers(
        CombatState combat,
        PrototypeEventSubscriberState[] subscribers) =>
        subscribers.All(subscriber =>
        {
            if (subscriber is not
                    { SourceKind: PrototypeEffectSourceKind.Relic,
                      RelicStateIndex: { } relicIndex,
                      RelicTriggerIndex: { } triggerIndex } ||
                (uint)relicIndex >= (uint)combat.RelicStates.Length)
                return false;
            var relic = combat.RelicStates[relicIndex];
            if (relic.IsMelted || relic.PersistentIndex != relicIndex ||
                relic.ApplicationOrder != subscriber.ApplicationOrder)
                return false;
            var triggers = PrototypeContent.Relic(relic.RelicId).Triggers ?? [];
            if ((uint)triggerIndex >= (uint)triggers.Length)
                return false;
            var trigger = triggers[triggerIndex];
            if (trigger.EventKind !=
                    PrototypeCombatEventKind.PlayerSideTurnStarted ||
                trigger.Effects.Length == 0 ||
                !trigger.Effects.All(
                    IsSupportedCrossbowRemainderEffect))
                return false;
            var expectedSubscriber = new PrototypeEventSubscriberState(
                relic.ApplicationOrder, trigger.Effects, PowerStacks: 0,
                PrototypeEffectSourceKind.Relic,
                RelicStateIndex: relicIndex,
                RelicTriggerIndex: triggerIndex,
                EveryNth: trigger.EveryNth,
                MaxTriggersPerCounterWindow:
                    trigger.MaxTriggersPerCounterWindow);
            return CanonicalJson.Sha256(expectedSubscriber) ==
                CanonicalJson.Sha256(subscriber);
        });

    private static bool IsSupportedCrossbowRemainderEffect(
        PrototypeCombatEffectSpec effect)
    {
        PrototypeCombatEffectSpec? expected = effect.Kind switch
        {
            PrototypeCombatEffectKind.GainEnergy =>
                new(effect.Kind, effect.Amount),
            PrototypeCombatEffectKind.GainEnergyForGold =>
                new(effect.Kind, effect.Amount,
                    PlayerGoldCost: effect.PlayerGoldCost),
            PrototypeCombatEffectKind.GainPlayerBlock =>
                new(effect.Kind, effect.Amount),
            PrototypeCombatEffectKind.ApplyPlayerPower
                when !string.IsNullOrWhiteSpace(effect.PowerId) =>
                new(effect.Kind, effect.Amount, PowerId: effect.PowerId),
            _ => null
        };
        return expected is not null &&
            CanonicalJson.Sha256(expected) == CanonicalJson.Sha256(effect);
    }

    private static CombatState MakeControlledGeneratedCardChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        string[] candidates,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        if (!ControlledChanceGuardV111.Active ||
            operation.Amount <= 0 || combat.PendingChoice is not null ||
            candidates.Length == 0 ||
            candidates.Distinct(StringComparer.Ordinal).Count() != candidates.Length)
            throw new InvalidOperationException(
                "Controlled generation requires one unique, live card pool.");
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_generated_card",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledGeneration: new PrototypeControlledGeneratedCardV111(
                    operation with { }, (string[])candidates.Clone()))
        };
    }

    /// <summary>
    /// Apply only the one selected generated card, then execute the saved
    /// remainder. Neither prior card effects nor the choice's parent action
    /// is repeated. Repeated generation exposes a new ticket each time.
    /// </summary>
    internal static RunState ResumeControlledGeneratedCard(
        RunState state, string selectedCardId) =>
        ResumeControlledGeneratedCardWithBoundary(
            state, selectedCardId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledGeneratedCardWithBoundary(
            RunState state, string selectedCardId) =>
        ResumeControlledGeneratedCardCore(
            state, selectedCardId, resumeAutomaticPipeline: true);

    internal static RunState ResumeControlledGeneratedCardForSemanticHandoff(
        RunState state, string selectedCardId) =>
        ResumeControlledGeneratedCardForSemanticHandoffWithBoundary(
            state, selectedCardId).Run;

    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledGeneratedCardForSemanticHandoffWithBoundary(
            RunState state, string selectedCardId) =>
        ResumeControlledGeneratedCardCore(
            state, selectedCardId, resumeAutomaticPipeline: false);

    private static PrototypeAutomaticPipelineResumeResultV111
        ResumeControlledGeneratedCardCore(
        RunState state, string selectedCardId,
        bool resumeAutomaticPipeline)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled generation requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing generating combat.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("No generated-card chance pending.");
        if (pending.ChoiceId != "controlled_generated_card" ||
            pending.ControlledGeneration is not { } generation ||
            pending.OuterChoiceContinuation is not { } continuation)
            throw new InvalidOperationException(
                "Pending combat choice is not a Nature card generation.");
        var candidates = generation.CandidateCardIds;
        var source = GeneratedCardModelPool(generation.Operation);
        if (source is null || candidates.Length == 0 ||
            !candidates.SequenceEqual(source) ||
            generation.Operation.Amount <= 0)
            throw new InvalidDataException(
                "Saved generation pool is not reachable from source rules.");
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedCardId);
        if (!candidates.Contains(selectedCardId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Selected generated-card identity is outside the saved source pool.");

        var op = generation.Operation;
        PrototypeTemporaryCardCost? free = op.Kind switch
        {
            PrototypeCombatEffectKind.CreateRandomFreeCharacterCardInHand =>
                new PrototypeTemporaryCardCost(0,
                    PrototypeTemporaryCardCostExpiry.EndOfTurn |
                    PrototypeTemporaryCardCostExpiry.WhenPlayed),
            PrototypeCombatEffectKind.CreateRandomCharacterSkillCardsInHand =>
                new PrototypeTemporaryCardCost(0,
                    PrototypeTemporaryCardCostExpiry.EndOfTurn),
            PrototypeCombatEffectKind.CreateRandomCharacterAttackCardsInHand or
            PrototypeCombatEffectKind.CreateRandomCharacterPowerCardsInHand
                when op.GeneratedCharacterCardsFreeThisTurn =>
                new PrototypeTemporaryCardCost(0,
                    PrototypeTemporaryCardCostExpiry.EndOfTurn),
            _ => null
        };
        int upgrade = op.Kind ==
            PrototypeCombatEffectKind.AddRandomZeroCostCardsToHand
            ? op.GeneratedCardUpgradeLevel : 0;

        combat = AddGeneratedCardCopies(
            combat with { PendingChoice = null },
            new PrototypeCombatCardSnapshot(
                selectedCardId, upgrade, PrototypeJson.EmptyObject(),
                TemporaryEnergyCost: free), 1);

        var prototypeRng = state.Stochastic.PrototypeRng;
        var resolved = ResumeChoiceResolutionContinuation(
            state.Player, combat, prototypeRng, continuation);
        var updated = state with
        {
            Player = resolved.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng),
            World = world with { Combat = resolved.Combat }
        };
        return resumeAutomaticPipeline
            ? ResumeAutomaticPipelineContinuation(updated)
            : PrototypeAutomaticPipelineResumeResultV111.FromRun(updated);
    }
}
