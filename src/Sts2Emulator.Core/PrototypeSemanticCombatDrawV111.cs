namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in initial semantic combat draw bridge. Uses the *real* DrawCards
/// kernel; its isolated fixture envelope is distinct from the legacy
/// opening Fisher-Yates controlled-playable state and transcript.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static (RunState Run, int RemainingCount)
        BeginSemanticCombatDraw(RunState run, int count, bool fromHandDraw)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic combat draw requires a controlled chance guard.");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat to draw from.");
        if (combat.SemanticDeck is null)
            throw new InvalidOperationException("Missing semantic deck provider.");
        if (combat.PendingChoice is not null ||
            combat.AutomaticPipelineContinuation is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=nested_choice_or_automatic_pipeline");
        var result = DrawCards(
            run.Player, combat, count, run.Rng, fromHandDraw,
            controlledSemanticDrawSuspension: true);
        if (result.ControlledRecyclePending || result.ShuffleSelectionPending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=unsupported_nested_stage");
        return (run with
        {
            Player = result.Player,
            World = world with { Combat = result.Combat }
        }, result.ControlledSemanticDrawPending ||
           result.ControlledDrawCostPending
            ? result.RemainingCount : 0);
    }

    /// <summary>
    /// Continue a semantic card draw *after* the physical card entered hand.
    /// Snecko/relic cost runs first; Slither, if present, overrides it. Only
    /// after all cost choices does CardDrawn dispatch, exactly once. The
    /// subsequent draw reuses the same real DrawCards and semantic provider.
    /// </summary>
    internal static (RunState Run, int RemainingCount)
        ResumeSemanticCombatDrawCost(RunState run, int value)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic cost resume requires controlled chance guard.");
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("No semantic cost combat.");
        var deck = combat.SemanticDeck ??
            throw new InvalidOperationException("Semantic provider is absent.");
        SemanticDeckEngineV111.Validate(deck);
        if (deck.Pending is not null ||
            combat.AutomaticPipelineContinuation is not null ||
            combat.PendingChoice is not { ControlledDrawCost: { } cost } pending ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cost.RemainingCount < 0 || cost.EventDepth != 0 ||
            !ValidateControlledDrawCost(
                run.Player, combat, cost with { FromHandDraw = true }))
            throw new InvalidDataException(
                "Semantic cost checkpoint/card identity/stage is inconsistent.");

        int bound = cost.Stage == "relic" ? cost.RelicMaxExclusive : 4;
        if (value < 0 || value >= bound)
            throw new ArgumentOutOfRangeException(nameof(value));
        combat = SetCardTemporaryEnergyCost(
            combat, cost.CardInstanceId,
            new PrototypeTemporaryCardCost(
                value, PrototypeTemporaryCardCostExpiry.None));
        if (cost.Stage == "relic" && cost.HasSlither)
        {
            combat = combat with
            {
                PendingChoice = pending with
                {
                    ControlledDrawCost = cost with { Stage = "slither" }
                }
            };
            return (run with
            {
                World = world with { Combat = combat }
            }, cost.RemainingCount);
        }

        combat = combat with { PendingChoice = null };
        var card = RequireCombatCard(combat, cost.CardInstanceId);
        var dispatched = DispatchCombatEvent(
            run.Player, combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardDrawn,
                SourceCardInstanceId: cost.CardInstanceId,
                CardId: card.CardId,
                FromHandDraw: cost.FromHandDraw),
            run.Rng, cost.EventDepth);
        var player = dispatched.Player;
        combat = dispatched.Combat;
        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                "operation=nested_choice_continuation");

        var continued = DrawCards(
            player, combat, cost.RemainingCount, run.Rng,
            cost.FromHandDraw, eventDepth: cost.EventDepth,
            controlledSemanticDrawSuspension: true);
        if (continued.ControlledRecyclePending ||
            continued.ShuffleSelectionPending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.cost " +
                "operation=unsupported_post_cost_stage");
        return (run with
        {
            Player = continued.Player,
            World = world with { Combat = continued.Combat }
        }, continued.ControlledSemanticDrawPending ||
           continued.ControlledDrawCostPending
            ? continued.RemainingCount : 0);
    }
}
