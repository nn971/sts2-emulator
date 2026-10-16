namespace Sts2Emulator.Core;

/// <summary>
/// Draw-cost continuation for the existing v111 playable fixture protocol.
/// It makes cost RNG a Nature ticket after the concrete card has moved to hand,
/// before CardDrawn hooks. No second deck/random-card choice is emitted by
/// this legacy already-shuffled mode. A separate semantic deck schema is
/// required by the revised desired RNG design.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private static CombatState MakeControlledDrawCostChoice(
        CombatState combat, long cardInstanceId, int remaining,
        bool fromHandDraw, int depth, int relicMaxExclusive, bool hasSlither)
    {
        if (!ControlledChanceGuardV111.Active || combat.PendingChoice is not null ||
            remaining < 0 || (relicMaxExclusive <= 0 && !hasSlither) ||
            !combat.Hand.Contains(cardInstanceId))
            throw new InvalidOperationException(
                "Controlled drawn-card cost requires a live drawn instance.");
        return combat with
        {
            PendingChoice = new PendingCombatChoiceState(
                ChoiceId: "controlled_draw_cost",
                SourceCardInstanceId: cardInstanceId,
                SourceCardDestination: PrototypeCardZone.Hand,
                Selection: new PrototypeCardSelectionSpec(
                    PrototypeCardZone.Hand, 0, 0,
                    PrototypeCardSelectionResolutionKind.MoveToDiscard),
                CandidateCardInstanceIds: [],
                Continuation: [],
                CompletionEvents: [],
                ControlledDrawCost: new PrototypeControlledDrawCostV111(
                    cardInstanceId, remaining, fromHandDraw, depth,
                    relicMaxExclusive, hasSlither,
                    relicMaxExclusive > 0 ? "relic" : "slither"))
        };
    }

    internal static bool ValidateControlledDrawCost(
        PlayerState player, CombatState combat,
        PrototypeControlledDrawCostV111 pending)
    {
        if (pending.RemainingCount < 0 || pending.EventDepth < 0 ||
            !combat.Hand.Contains(pending.CardInstanceId) ||
            combat.PendingChoice?.ChoiceId != "controlled_draw_cost" ||
            combat.PendingChoice.SourceCardInstanceId != pending.CardInstanceId ||
            pending.Stage is not ("relic" or "slither"))
            return false;
        var card = combat.Cards.SingleOrDefault(c =>
            c.InstanceId == pending.CardInstanceId);
        if (card is null) return false;
        var max = player.ActiveRelics.Select(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .RandomizeDrawnCardCostMaxExclusive)
            .DefaultIfEmpty(0).Max();
        var definition = PrototypeContent.Card(card.CardId);
        var expectedMax = definition.Cost.Kind == PrototypeCardCostKind.Fixed
            && definition.Cost.Amount >= 0 ? max : 0;
        var slither = card.Enchantment is
            { Kind: PrototypeCardEnchantmentKind.Slither };
        return pending.RelicMaxExclusive == expectedMax &&
            pending.HasSlither == slither &&
            (pending.Stage != "relic" || expectedMax > 0) &&
            (pending.Stage != "slither" || slither);
    }

    /// <summary>
    /// Resume a drawn-card cost nested inside a player card's DrawCards
    /// operation. The source effect queue and the draw's deferred keyword
    /// updates remain attached until CardDrawn and every requested draw finish.
    /// </summary>
    internal static RunState ResumeControlledSemanticEffectDrawCost(
        RunState state, int value)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic effect-draw cost requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidOperationException(
            "Semantic effect-draw cost has no combat.");
        var pending = combat.PendingChoice;
        var cost = pending?.ControlledDrawCost;
        var draw = pending?.ControlledSemanticDraw;
        var continuation = pending?.OuterChoiceContinuation;
        if (state.Phase != RunPhase.Combat ||
            combat is not { IsPlayerTurn: true,
                AutomaticPipelineContinuation: null,
                PendingKnowledgeDemonChoice: null } ||
            pending is not { ChoiceId: "controlled_draw_cost" } ||
            cost is null || draw is null || continuation is null ||
            draw.RemainingCount != cost.RemainingCount ||
            draw.FromHandDraw != cost.FromHandDraw ||
            draw.EventDepth != cost.EventDepth ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            !ValidateControlledDrawCost(state.Player, combat, cost))
            throw new InvalidDataException(
                "Semantic effect-draw cost checkpoint is inconsistent.");

        int bound = cost.Stage == "relic"
            ? cost.RelicMaxExclusive : 4;
        if (value < 0 || value >= bound)
            throw new ArgumentOutOfRangeException(nameof(value));
        combat = SetCardTemporaryEnergyCost(
            combat, cost.CardInstanceId,
            new PrototypeTemporaryCardCost(
                value, PrototypeTemporaryCardCostExpiry.None));
        if (cost.Stage == "relic" && cost.HasSlither)
            return state with
            {
                World = world with
                {
                    Combat = combat with
                    {
                        PendingChoice = pending! with
                        {
                            ControlledDrawCost = cost with
                            {
                                Stage = "slither"
                            }
                        }
                    }
                }
            };

        combat = combat with { PendingChoice = null };
        var card = RequireCombatCard(combat, cost.CardInstanceId);
        var dispatched = DispatchCombatEvent(
            state.Player, combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardDrawn,
                SourceCardInstanceId: cost.CardInstanceId,
                CardId: card.CardId,
                FromHandDraw: cost.FromHandDraw),
            state.Rng, cost.EventDepth);
        if (dispatched.Combat.PendingChoice is not null ||
            dispatched.Combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                "operation=nested_choice_continuation");

        var drawn = DrawCards(
            dispatched.Player, dispatched.Combat, cost.RemainingCount,
            state.Rng, cost.FromHandDraw, eventDepth: cost.EventDepth,
            controlledDrawCostSuspension: true,
            controlledSemanticDrawSuspension: true);
        return ContinueControlledSemanticEffectDraw(
            state, drawn, draw, continuation);
    }

    /// <summary>Advance one cost decision, then dispatch CardDrawn once and
    /// continue the suspended automatic hand draw. Previously drawn cards
    /// and hook side effects remain intact in the saved combat state.</summary>
    internal static RunState ResumeControlledDrawCost(
        RunState state, int value)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Draw-cost continuation requires controlled RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ??
            throw new InvalidOperationException("Draw-cost combat missing.");
        var continuation = combat.AutomaticPipelineContinuation ??
            throw new InvalidOperationException(
                "Missing original automatic hand-draw cursor.");
        int index = continuation.NextStepIndex;
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("Missing drawn-card cost choice.");
        var cost = pending.ControlledDrawCost ??
            throw new InvalidOperationException("No controlled drawn-card cost.");
        if (index < 0 || index >= PrototypeContent.Rules.EndTurnPipeline.Length ||
            PrototypeContent.Rules.EndTurnPipeline[index].Kind !=
                PrototypeAutomaticStepKind.DrawPlayerHand ||
            continuation.ControlledDrawCostRemaining != cost.RemainingCount ||
            continuation.ControlledDrawRemaining is not null ||
            continuation.OpeningStage is not null ||
            continuation.PendingPostDispatchEventKind is not null ||
            !ValidateControlledDrawCost(state.Player, combat, cost))
            throw new InvalidDataException(
                "Saved drawn-card cost does not match the hand-draw checkpoint.");
        int bound = cost.Stage == "relic"
            ? cost.RelicMaxExclusive : 4;
        if (value < 0 || value >= bound)
            throw new ArgumentOutOfRangeException(nameof(value));

        combat = SetCardTemporaryEnergyCost(
            combat, cost.CardInstanceId,
            new PrototypeTemporaryCardCost(
                value, PrototypeTemporaryCardCostExpiry.None));
        if (cost.Stage == "relic" && cost.HasSlither)
        {
            return state with
            {
                World = world with
                {
                    Combat = combat with
                    {
                        PendingChoice = pending with
                        {
                            ControlledDrawCost = cost with
                            {
                                Stage = "slither"
                            }
                        }
                    }
                }
            };
        }

        combat = combat with { PendingChoice = null };
        var card = RequireCombatCard(combat, cost.CardInstanceId);
        var dispatched = DispatchCombatEvent(
            state.Player, combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardDrawn,
                SourceCardInstanceId: cost.CardInstanceId,
                CardId: card.CardId,
                FromHandDraw: cost.FromHandDraw),
            state.Rng, cost.EventDepth);
        var player = dispatched.Player;
        combat = dispatched.Combat;
        // Nested choice/relic chains need their own explicit continuation.
        // Do not allow an unsupported event suspension to be overwritten.
        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw_cost." +
                "drawn_event operation=nested_choice");

        var drawn = DrawCards(player, combat, cost.RemainingCount,
            state.Rng, fromHandDraw: true, eventDepth: cost.EventDepth,
            controlledRecycleSuspension: true,
            controlledDrawCostSuspension: true);
        player = drawn.Player;
        combat = drawn.Combat;
        if (drawn.ShuffleSelectionPending)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw_cost." +
                "stratagem operation=choose_cards");
        if (drawn.ControlledDrawCostPending || drawn.ControlledRecyclePending)
        {
            return state with
            {
                Player = player,
                World = world with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation = continuation with
                        {
                            ControlledDrawCostRemaining =
                                drawn.ControlledDrawCostPending
                                    ? drawn.RemainingCount : null,
                            ControlledDrawRemaining =
                                drawn.ControlledRecyclePending
                                    ? drawn.RemainingCount : null
                        }
                    }
                }
            };
        }

        combat = RemovePlayerPowers(
            combat, definition => definition.RemoveAfterHandDraw);
        combat = DecrementPlayerPowers(
            combat, definition => definition.DecrementAfterHandDraw);
        var resumed = state with
        {
            Player = player,
            World = world with
            {
                Combat = combat with { AutomaticPipelineContinuation = null }
            }
        };
        return ResumeAutomaticPipeline(resumed, index + 1);
    }
}
