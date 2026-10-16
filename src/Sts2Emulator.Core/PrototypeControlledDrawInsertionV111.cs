namespace Sts2Emulator.Core;

/// <summary>
/// The original operation kernel supplies this continuation before consuming
/// the model/position RNG draws. Each branch materializes exactly one card
/// only after both required decisions are known.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static string[] ControlledRandomDrawAttackPool() =>
        PrototypeContent.NativeSilentCardPool.Where(id =>
        {
            var card = PrototypeContent.Card(id);
            return card.Type == PrototypeCardType.Attack
                && card.Rarity is (PrototypeCardRarity.Common or
                    PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare)
                && card.MechanicsImplemented
                && !card.MultiplayerOnly
                && card.CanBeGeneratedInCombat;
        }).ToArray();

    private static CombatState MakeControlledDrawInsertionChoice(
        CombatState combat, PrototypeQueuedOperation operation,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        if (!ControlledChanceGuardV111.Active ||
            combat.PendingChoice is not null || operation.Amount <= 0)
            throw new InvalidOperationException(
                "Controlled draw insertion requires one live source operation.");
        if (operation.Kind is not (
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions
                or PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile))
            throw new ArgumentOutOfRangeException(nameof(operation));
        string[] pool;
        string stage;
        string? selected;
        if (operation.Kind ==
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions)
        {
            if (operation.CardId is null)
                throw new InvalidOperationException(
                    "Generated draw-pile card ID is missing.");
            _ = PrototypeContent.Card(operation.CardId);
            pool = [];
            selected = operation.CardId;
            stage = "position";
        }
        else
        {
            pool = ControlledRandomDrawAttackPool();
            if (pool.Length == 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_CONTROLLED_CHANCE site=combat.random_draw_attack empty_pool");
            selected = null;
            stage = "card_model";
        }

        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_draw_insertion",
                SourceCardInstanceId: continuation.SourceCardInstanceId,
                SourceCardDestination: continuation.SourceCardDestination,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                OuterChoiceContinuation: continuation.Fork(),
                ControlledDrawInsertion: new PrototypeControlledDrawInsertionV111(
                    operation with { }, stage, selected, pool,
                    (long[])combat.DrawPile.Clone()))
        };
    }

    internal static bool ValidateControlledDrawInsertion(
        CombatState combat, PrototypeControlledDrawInsertionV111 choice)
    {
        if (choice.Operation.Amount <= 0 ||
            !combat.DrawPile.SequenceEqual(choice.EligibleDrawPile) ||
            choice.CandidateCardIds is null ||
            choice.EligibleDrawPile is null)
            return false;
        if (choice.Operation.Kind ==
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions)
            return choice.Stage == "position" &&
                choice.Operation.CardId is not null &&
                choice.SelectedCardId == choice.Operation.CardId &&
                choice.CandidateCardIds.Length == 0;
        if (choice.Operation.Kind !=
            PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile ||
            !choice.CandidateCardIds.SequenceEqual(ControlledRandomDrawAttackPool()))
            return false;
        return choice.Stage switch
        {
            "card_model" => choice.SelectedCardId is null,
            "position" => choice.SelectedCardId is not null &&
                choice.CandidateCardIds.Contains(choice.SelectedCardId),
            _ => false
        };
    }

    /// <summary>
    /// Advance one Nature ticket: card model first for Metamorphosis-like
    /// effects, then bounded insertion index. Subsequent generated cards
    /// resume the exact remaining queue, yielding new chance tickets.
    /// </summary>
    internal static RunState ResumeControlledDrawInsertion(
        RunState state, int chosen)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled draw insertion requires an RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Draw-insertion combat missing.");
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("Draw-insertion ticket missing.");
        if (pending.ChoiceId != "controlled_draw_insertion" ||
            pending.ControlledDrawInsertion is not { } choice ||
            pending.OuterChoiceContinuation is not { } continuation ||
            !ValidateControlledDrawInsertion(combat, choice))
            throw new InvalidDataException(
                "Pending draw insertion is not source-reachable.");
        if (choice.Stage == "card_model")
        {
            if (chosen < 0 || chosen >= choice.CandidateCardIds.Length)
                throw new ArgumentOutOfRangeException(nameof(chosen));
            var selected = choice.CandidateCardIds[chosen];
            return state with
            {
                World = world with
                {
                    Combat = combat with
                    {
                        PendingChoice = pending with
                        {
                            ControlledDrawInsertion = choice with
                            {
                                Stage = "position",
                                SelectedCardId = selected
                            }
                        }
                    }
                }
            };
        }

        if (chosen < 0 || chosen > combat.DrawPile.Length ||
            choice.SelectedCardId is not { } cardId)
            throw new ArgumentOutOfRangeException(nameof(chosen));
        var instanceId = combat.NextCardInstanceId;
        var card = new CombatCardInstance(
            instanceId, null, cardId, 0, true,
            PrototypeJson.EmptyObject(),
            TemporaryEnergyCost: choice.Operation.Kind ==
                PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile
                ? new PrototypeTemporaryCardCost(
                    0, PrototypeTemporaryCardCostExpiry.None)
                : null);
        var draw = combat.DrawPile.ToList();
        draw.Insert(chosen, instanceId);
        combat = combat with
        {
            PendingChoice = null,
            NextCardInstanceId = instanceId + 1,
            Cards = combat.Cards.Append(card).ToArray(),
            DrawPile = draw.ToArray()
        };
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

    /// <summary>
    /// Narrow test/diagnostic entry for an effect that is not yet reachable
    /// through a normal Silent fixture card; executes the actual operations
    /// kernel, preserving a real RunState and RNG guard, not fake pile edits.
    /// </summary>
    internal static RunState BeginControlledDrawInsertionFixture(
        RunState state, PrototypeQueuedOperation operation)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException("Controlled test fixture needs RNG guard.");
        if (operation.Kind is not (
                PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions
                or PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile))
            throw new ArgumentException("Expected a random draw-pile insertion effect.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat.");
        var resolved = ResolveOperations(state.Player, combat,
            new Queue<PrototypeQueuedOperation>([operation]), state.Rng,
            moveSourceCardOnCompletion: false);
        return state with
        {
            Player = resolved.Player,
            World = world with { Combat = resolved.Combat }
        };
    }
}
