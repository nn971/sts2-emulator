namespace Sts2Emulator.Core;

public sealed partial class PrototypeGameEngine
{
    /// <summary>
    /// The original ordered source pools, with unchanged eligibility tests.
    /// Only these five simple one-at-a-time generation effects support an
    /// explicitly resumable Nature ticket. Other random sites fail closed.
    /// </summary>
    private static string[]? ControlledGeneratedCardPool(
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
        ControlledGeneratedCardPool(choice.Operation) is { } expected &&
        expected.Length > 0 &&
        choice.CandidateCardIds.SequenceEqual(expected);

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
        RunState state, int selectedIndex)
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
        var source = ControlledGeneratedCardPool(generation.Operation);
        if (source is null || candidates.Length == 0 ||
            !candidates.SequenceEqual(source) ||
            generation.Operation.Amount <= 0)
            throw new InvalidDataException(
                "Saved generation pool is not reachable from source rules.");
        if (selectedIndex < 0 || selectedIndex >= candidates.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));

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
                candidates[selectedIndex], upgrade, PrototypeJson.EmptyObject(),
                TemporaryEnergyCost: free), 1);

        var resolved = ResumeChoiceResolutionContinuation(
            state.Player, combat, state.Rng, continuation);
        var updated = state with
        {
            Player = resolved.Player,
            World = world with { Combat = resolved.Combat }
        };
        if (resolved.Combat.PendingChoice is null &&
            resolved.Combat.AutomaticPipelineContinuation is { } automatic)
        {
            updated = updated with
            {
                World = updated.World! with
                {
                    Combat = resolved.Combat with
                    {
                        AutomaticPipelineContinuation = null
                    }
                }
            };
            updated = automatic.OpeningStage is { } openingStage
                ? ResumeOpeningCombat(updated, openingStage)
                : ResumeAutomaticPipeline(updated,
                    automatic.NextStepIndex,
                    automatic.PendingPostDispatchEventKind);
        }
        if (updated.Phase == RunPhase.Combat &&
            updated.World?.Combat is { } live &&
            AllEnemiesDefeated(live))
            return EnterCombatReward(updated);
        return updated;
    }
}
