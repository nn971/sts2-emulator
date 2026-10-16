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
        bool automatic = IsSemanticAutomaticHandDraw(combat);
        if (combat.PendingChoice is not null ||
            (combat.AutomaticPipelineContinuation is not null && !automatic) ||
            (automatic && (!fromHandDraw ||
                           combat.AutomaticPipelineContinuation!
                               .ControlledSemanticDrawRemaining != count)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=nested_choice_or_automatic_pipeline");
        var result = DrawCards(
            run.Player, combat, count, run.Rng, fromHandDraw,
            controlledSemanticDrawSuspension: true,
            controlledAfterShuffleInsertionSuspension: !automatic);
        if (result.ControlledAfterShuffleInsertions is
            { Length: > 0 } afterShuffleInsertions)
        {
            if (automatic)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle " +
                    "operation=automatic_after_shuffle_insertion");
            var advancedAfterInsertion = run with
            {
                Player = result.Player,
                World = world with { Combat = result.Combat }
            };
            return SuspendSemanticDrawAfterShuffleInsertions(
                advancedAfterInsertion, result, afterShuffleInsertions,
                fromHandDraw, eventDepth: 0);
        }
        if (result.ControlledRecyclePending || result.ShuffleSelectionPending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=unsupported_nested_stage");
        var advanced = run with
        {
            Player = result.Player,
            World = world with { Combat = result.Combat }
        };
        bool pending = result.ControlledSemanticDrawPending ||
            result.ControlledDrawCostPending;
        return automatic
            ? CommitSemanticAutomaticHandDrawProgress(
                advanced, result.RemainingCount, pending)
            : (advanced, pending ? result.RemainingCount : 0);
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
        var pending = combat.PendingChoice;
        var cost = pending?.ControlledDrawCost;
        bool automatic = IsSemanticAutomaticHandDraw(combat);
        if (deck.Pending is not null ||
            (combat.AutomaticPipelineContinuation is not null && !automatic) ||
            (automatic && (
                !cost!.FromHandDraw ||
                combat.AutomaticPipelineContinuation!
                    .ControlledSemanticDrawRemaining != cost.RemainingCount)) ||
            cost is null ||
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
                PendingChoice = pending! with
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
            controlledSemanticDrawSuspension: true,
            controlledAfterShuffleInsertionSuspension: !automatic);
        if (continued.ControlledAfterShuffleInsertions is
            { Length: > 0 } afterShuffleInsertions)
        {
            if (automatic)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle " +
                    "operation=automatic_after_shuffle_insertion");
            var advancedAfterInsertion = run with
            {
                Player = continued.Player,
                World = world with { Combat = continued.Combat }
            };
            return SuspendSemanticDrawAfterShuffleInsertions(
                advancedAfterInsertion, continued, afterShuffleInsertions,
                cost.FromHandDraw, cost.EventDepth);
        }
        if (continued.ControlledRecyclePending ||
            continued.ShuffleSelectionPending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.cost " +
                "operation=unsupported_post_cost_stage");
        var advanced = run with
        {
            Player = continued.Player,
            World = world with { Combat = continued.Combat }
        };
        bool hasPending = continued.ControlledSemanticDrawPending ||
            continued.ControlledDrawCostPending;
        return automatic
            ? CommitSemanticAutomaticHandDrawProgress(
                advanced, continued.RemainingCount, hasPending)
            : (advanced, hasPending ? continued.RemainingCount : 0);
    }

    private static (RunState Run, int RemainingCount)
        SuspendSemanticDrawAfterShuffleInsertions(
            RunState run,
            PrototypeDrawCardsResult drawn,
            PrototypeAfterShuffleRelicInsertionV111[] insertions,
            bool fromHandDraw,
            int eventDepth)
    {
        if (insertions.Length == 0 || drawn.RemainingCount <= 0)
            throw new InvalidDataException(
                "After-shuffle insertion lost its remaining draw cursor.");
        if (drawn.Combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.stratagem" &&
                power.Stacks > 0) && drawn.Combat.Hand.Length < 10)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle " +
                "operation=after_shuffle_insertion_with_stratagem");
        var semanticDraw = new PrototypeControlledSemanticDrawV111(
            drawn.RemainingCount, fromHandDraw, eventDepth,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: false,
            StandaloneSemanticDrawSegment: true);
        var continuation = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DrawPile,
            Operations: [],
            PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [],
            CompletionEvents: []);
        var combat = MakeControlledAfterShuffleInsertionChoice(
            drawn.Combat, insertions[0], insertions.Skip(1).ToArray(),
            [], semanticDraw, continuation, 0);
        return (run with
        {
            Player = drawn.Player,
            World = run.World! with { Combat = combat }
        }, drawn.RemainingCount);
    }
}
