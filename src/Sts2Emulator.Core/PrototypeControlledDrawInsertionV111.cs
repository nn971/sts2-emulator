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
        PrototypeChoiceResolutionContinuationState continuation,
        PrototypeControlledSemanticDrawV111? semanticDrawContinuation = null,
        PrototypeAfterShuffleRelicInsertionV111? afterShuffleRelic = null,
        PrototypeAfterShuffleRelicInsertionV111[]? completedAfterShuffleRelics = null,
        PrototypeAfterShuffleRelicInsertionV111[]? remainingAfterShuffleRelics = null,
        int requestOrdinal = 0)
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
                    (long[])combat.DrawPile.Clone(),
                    SemanticDrawContinuation: semanticDrawContinuation?.Fork(),
                    AfterShuffleRelic: afterShuffleRelic is null
                        ? null : afterShuffleRelic with { },
                    CompletedAfterShuffleRelics:
                        completedAfterShuffleRelics is null
                            ? null : completedAfterShuffleRelics
                                .Select(item => item with { }).ToArray(),
                    RemainingAfterShuffleRelics: remainingAfterShuffleRelics is null
                        ? null : remainingAfterShuffleRelics
                            .Select(item => item with { }).ToArray(),
                    RequestSiteId: afterShuffleRelic is null
                        ? "combat.draw_pile.random_insert_index"
                        : "combat.relic.after_shuffle.generated_card.random_insert_index",
                    RequestOwnerId: afterShuffleRelic is null
                        ? "prototype.combat"
                        : "relic:" + afterShuffleRelic.RelicId +
                            ":application:" + afterShuffleRelic.RelicApplicationOrder,
                    RequestOrdinal: requestOrdinal))
        };
    }

    private static bool ValidateAfterShuffleRelicInsertion(
        CombatState combat, PrototypeAfterShuffleRelicInsertionV111 insertion)
    {
        var relic = combat.RelicStates.SingleOrDefault(state =>
            state.ApplicationOrder == insertion.RelicApplicationOrder &&
            state.RelicId == insertion.RelicId && !state.IsMelted);
        return relic is not null &&
            PrototypeContent.Relic(relic.RelicId)
                .AfterShuffleGeneratedCardId == insertion.CardId;
    }

    private static PrototypeAfterShuffleRelicInsertionV111[]
        ControlledAfterShuffleRelicInsertions(CombatState combat) =>
        combat.RelicStates.Where(relic => !relic.IsMelted)
            .OrderBy(relic => relic.ApplicationOrder)
            .Select(relic => new
            {
                Relic = relic,
                CardId = PrototypeContent.Relic(relic.RelicId)
                    .AfterShuffleGeneratedCardId
            })
            .Where(item => item.CardId is not null)
            .Select(item => new PrototypeAfterShuffleRelicInsertionV111(
                item.Relic.RelicId, item.Relic.ApplicationOrder,
                item.CardId!))
            .ToArray();

    internal static bool ValidateControlledDrawInsertion(
        CombatState combat, PrototypeControlledDrawInsertionV111 choice)
    {
        if (choice.CandidateCardIds is null ||
            choice.EligibleDrawPile is null || choice.Operation.Amount <= 0 ||
            !combat.DrawPile.SequenceEqual(choice.EligibleDrawPile))
            return false;
        if (choice.Operation.Kind ==
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions)
        {
            var basicSupport = choice.Stage == "position" &&
                choice.Operation.CardId is not null &&
                choice.SelectedCardId == choice.Operation.CardId &&
                choice.CandidateCardIds.Length == 0;
            if (!basicSupport) return false;
            if (choice.SemanticDrawContinuation is null)
                return choice.AfterShuffleRelic is null &&
                    choice.CompletedAfterShuffleRelics is null &&
                    choice.RemainingAfterShuffleRelics is null &&
                    choice.RequestSiteId ==
                        "combat.draw_pile.random_insert_index" &&
                    choice.RequestOwnerId == "prototype.combat" &&
                    choice.RequestOrdinal == 0;
            var semanticDraw = choice.SemanticDrawContinuation;
            var afterShuffle = choice.AfterShuffleRelic;
            var completed = choice.CompletedAfterShuffleRelics ?? [];
            var remaining = choice.RemainingAfterShuffleRelics ?? [];
            var expectedInsertions = ControlledAfterShuffleRelicInsertions(
                combat);
            var savedInsertions = afterShuffle is null
                ? Array.Empty<PrototypeAfterShuffleRelicInsertionV111>()
                : completed.Concat(new[] { afterShuffle })
                    .Concat(remaining).ToArray();
            return semanticDraw.RemainingCount > 0 &&
                semanticDraw.EventDepth >= 0 &&
                semanticDraw.DeferredKeywordCardInstanceIds?.Distinct().Count() ==
                    semanticDraw.DeferredKeywordCardInstanceIds?.Length &&
                (!semanticDraw.StratagemSelectionPending ||
                 (combat.Hand.Length < 10 && combat.PlayerPowers.Any(power =>
                     power.PowerId == "proto.power.stratagem" &&
                     power.Stacks > 0))) &&
                afterShuffle is not null &&
                choice.Operation.Amount == 1 &&
                choice.Operation.CardId == afterShuffle.CardId &&
                completed.All(item =>
                    ValidateAfterShuffleRelicInsertion(combat, item)) &&
                ValidateAfterShuffleRelicInsertion(combat, afterShuffle) &&
                remaining.All(item =>
                    ValidateAfterShuffleRelicInsertion(combat, item)) &&
                savedInsertions.SequenceEqual(expectedInsertions) &&
                choice.RequestSiteId ==
                    "combat.relic.after_shuffle.generated_card.random_insert_index" &&
                choice.RequestOwnerId == "relic:" + afterShuffle.RelicId +
                    ":application:" + afterShuffle.RelicApplicationOrder &&
                choice.RequestOrdinal >= 0;
        }
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
        if (choice.AfterShuffleRelic is not null)
        {
            combat = AddGeneratedCardsToDiscard(combat, cardId, 1);
            if (combat.NextCardInstanceId != instanceId + 1)
                throw new InvalidDataException(
                    "After-shuffle relic insertion changed its card identity.");
        }
        else
        {
            var card = new CombatCardInstance(
                instanceId, null, cardId, 0, true,
                PrototypeJson.EmptyObject(),
                TemporaryEnergyCost: choice.Operation.Kind ==
                    PrototypeCombatEffectKind.CreateRandomFreeAttacksInDrawPile
                    ? new PrototypeTemporaryCardCost(
                        0, PrototypeTemporaryCardCostExpiry.None)
                    : null);
            combat = combat with
            {
                NextCardInstanceId = instanceId + 1,
                Cards = combat.Cards.Append(card).ToArray()
            };
        }
        var draw = combat.DrawPile.ToList();
        draw.Insert(chosen, instanceId);
        var semanticDeck = choice.SemanticDrawContinuation is null
            ? combat.SemanticDeck
            : SynchronizeSemanticDrawPileAfterInsertion(
                combat, chosen, instanceId);
        combat = combat with
        {
            PendingChoice = null,
            DrawPile = draw.ToArray(),
            SemanticDeck = semanticDeck,
            DiscardPile = choice.AfterShuffleRelic is null
                ? combat.DiscardPile
                : combat.DiscardPile.Where(id => id != instanceId).ToArray()
        };

        if (choice.SemanticDrawContinuation is { } semanticDraw)
        {
            var remainingInsertions =
                choice.RemainingAfterShuffleRelics ?? [];
            if (remainingInsertions.Length > 0)
            {
                var nextCombat = MakeControlledAfterShuffleInsertionChoice(
                    combat, remainingInsertions[0],
                    remainingInsertions.Skip(1).ToArray(),
                    (choice.CompletedAfterShuffleRelics ?? [])
                        .Append(choice.AfterShuffleRelic!).ToArray(),
                    semanticDraw, continuation, choice.RequestOrdinal + 1);
                return state with
                {
                    World = world with { Combat = nextCombat }
                };
            }

            if (semanticDraw.StratagemSelectionPending)
                return ResumeControlledInsertionPipeline(
                    ResumeControlledStratagemSelection(
                        state, combat, semanticDraw, continuation));

            var resumedDraw = DrawCards(
                state.Player, combat, semanticDraw.RemainingCount, state.Rng,
                semanticDraw.FromHandDraw, eventDepth: semanticDraw.EventDepth,
                controlledDrawCostSuspension: true,
                controlledSemanticDrawSuspension: true,
                controlledAfterShuffleInsertionSuspension: true);
            var drawState = state with
            {
                World = world with { Combat = combat }
            };
            return ResumeControlledInsertionPipeline(
                ContinueControlledSemanticEffectDraw(
                    drawState, resumedDraw, semanticDraw, continuation));
        }

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

    private static CombatState MakeControlledAfterShuffleInsertionChoice(
        CombatState combat,
        PrototypeAfterShuffleRelicInsertionV111 insertion,
        PrototypeAfterShuffleRelicInsertionV111[] remaining,
        PrototypeAfterShuffleRelicInsertionV111[] completed,
        PrototypeControlledSemanticDrawV111 semanticDraw,
        PrototypeChoiceResolutionContinuationState continuation,
        int ordinal)
    {
        var operation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.CreateGeneratedCardsAtRandomDrawPositions,
            1, CardId: insertion.CardId);
        return MakeControlledDrawInsertionChoice(
            combat, operation, continuation, semanticDraw, insertion,
            completed, remaining, ordinal);
    }

    private static RunState ResumeControlledStratagemSelection(
        RunState state, CombatState combat,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation)
    {
        var drawOperation = new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.DrawCards, draw.RemainingCount,
            FromHandDraw: draw.FromHandDraw,
            DrawnCardKeyword: draw.DrawnCardKeyword is null
                ? null : draw.DrawnCardKeyword with { },
            DeferredDrawnCardInstanceIds:
                (draw.DeferredKeywordCardInstanceIds ?? []).ToArray(),
            DrawEventDepth: draw.EventDepth);
        var resumedContinuation = continuation with
        {
            Operations = new[]
            {
                StratagemChoiceOperation(combat), drawOperation
            }.Concat(continuation.Operations).ToArray()
        };
        var resolved = ResumeChoiceResolutionContinuation(
            state.Player, combat, state.Rng, resumedContinuation);
        return state with
        {
            Player = resolved.Player,
            World = state.World! with { Combat = resolved.Combat }
        };
    }

    private static RunState ResumeControlledInsertionPipeline(RunState state)
    {
        if (state.Phase == RunPhase.Combat &&
            state.World?.Combat is { PendingChoice: null,
                AutomaticPipelineContinuation: { } automatic } combat)
        {
            state = state with
            {
                World = state.World with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation = null
                    }
                }
            };
            state = automatic.OpeningStage is { } openingStage
                ? ResumeOpeningCombat(state, openingStage)
                : ResumeAutomaticPipeline(state, automatic.NextStepIndex,
                    automatic.PendingPostDispatchEventKind);
        }
        if (state.Phase == RunPhase.Combat &&
            state.World?.Combat is { } live && AllEnemiesDefeated(live))
            return EnterCombatReward(state);
        return state;
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
