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

    private sealed record PrototypeDrawCostProgressV111(
        RunState Run,
        bool CostStagePending,
        bool DrawnHookSuspended);

    /// <summary>
    /// Applies one saved draw-cost outcome, advances Relic to Slither when
    /// required, and dispatches CardDrawn exactly once. Callers retain only
    /// the continuation policy for a suspended hook or the next draw.
    /// </summary>
    private static PrototypeDrawCostProgressV111
        ResolveDrawCostAndDispatchCardDrawn(
            RunState run,
            PrototypeControlledDrawCostV111 cost,
            int selectedValue,
            bool allowHookSuspension)
    {
        var world = RequireWorld(run);
        var combat = world.Combat ?? throw new InvalidOperationException(
            "Draw-cost continuation has no combat.");
        if (combat.PendingChoice?.ControlledDrawCost != cost)
            throw new InvalidDataException(
                "Draw-cost outcome no longer matches its pending choice.");
        int bound = cost.Stage == "relic"
            ? cost.RelicMaxExclusive : 4;
        if (selectedValue < 0 || selectedValue >= bound)
            throw new ArgumentOutOfRangeException(nameof(selectedValue));

        combat = SetCardTemporaryEnergyCost(
            combat, cost.CardInstanceId,
            new PrototypeTemporaryCardCost(
                selectedValue, PrototypeTemporaryCardCostExpiry.None));
        if (cost.Stage == "relic" && cost.HasSlither)
        {
            combat = combat with
            {
                PendingChoice = combat.PendingChoice! with
                {
                    ControlledDrawCost = cost with { Stage = "slither" }
                }
            };
            return new PrototypeDrawCostProgressV111(
                run with { World = world with { Combat = combat } },
                CostStagePending: true, DrawnHookSuspended: false);
        }

        combat = combat with { PendingChoice = null };
        var prototypeRng = run.Stochastic.PrototypeRng;
        var card = RequireCombatCard(combat, cost.CardInstanceId);
        var dispatched = DispatchCombatEvent(
            run.Player, combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardDrawn,
                SourceCardInstanceId: cost.CardInstanceId,
                CardId: card.CardId,
                FromHandDraw: cost.FromHandDraw),
            prototypeRng, cost.EventDepth,
            allowSuspension: allowHookSuspension);
        var nextRun = run with
        {
            Player = dispatched.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                run.Stochastic, prototypeRng),
            World = world with { Combat = dispatched.Combat }
        };
        return new PrototypeDrawCostProgressV111(
            nextRun, CostStagePending: false,
            DrawnHookSuspended:
                dispatched.Combat.PendingChoice is not null ||
                dispatched.Combat.PendingKnowledgeDemonChoice is not null);
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
                PendingKnowledgeDemonChoice: null } ||
            (combat.AutomaticPipelineContinuation is not null &&
             (pending is null ||
              !IsSupportedAutomaticPipelineSemanticDrawContinuation(combat, pending))) ||
            pending is not { ChoiceId: "controlled_draw_cost" } ||
            cost is null || draw is null || continuation is null ||
            draw.RemainingCount != cost.RemainingCount ||
            draw.FromHandDraw != cost.FromHandDraw ||
            draw.EventDepth != cost.EventDepth ||
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
            !ValidateControlledDrawCost(state.Player, combat, cost))
            throw new InvalidDataException(
                "Semantic effect-draw cost checkpoint is inconsistent.");

        return ContinueSemanticDrawCost(
            state, cost, draw, value, continuation,
            automaticHandDrawRequested: null,
            resumeKind: SemanticDrawCostResumeKindV111.CardEffect,
            recycleSite: "combat.card_effect.draw",
            recycleOperation: "recycle_requires_composed_continuation").Run;
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
        var pending = combat.PendingChoice ??
            throw new InvalidOperationException("Missing drawn-card cost choice.");
        var cost = pending.ControlledDrawCost ??
            throw new InvalidOperationException("No controlled drawn-card cost.");
        var resumed = ResumeAutomaticHandDrawAfterCost(
            state, cost, value,
            PrototypeDrawCardsSuspensionPolicyV111
                .ControlledLegacyAutomaticHandDraw(combat),
            allowHookSuspension: combat.Stochastic.SemanticDeck is null &&
                !ControlledChanceGuardV111.EngineOwnedNativeProviderActive,
            unsupportedBoundaryMessage:
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw_cost." +
                "drawn_event operation=nested_choice",
            semantic: false,
            semanticRequested: null);
        return resumed.Drawn is null
            ? resumed.Run
            : ContinueControlledHandDraw(resumed.Run, resumed.Drawn);
    }
}
