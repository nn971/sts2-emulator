namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in semantic continuation at the genuine DrawPlayerHand automatic
/// pipeline step. A source checkpoint is required: earlier turn stages
/// (including intents) must already have executed, and they are not replayed.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static int SemanticHandDrawStepIndex =>
        Array.FindIndex(PrototypeContent.Rules.EndTurnPipeline,
            step => step.Kind == PrototypeAutomaticStepKind.DrawPlayerHand);

    internal static bool IsSemanticAutomaticHandDraw(CombatState combat)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        return cursor is not null &&
            cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out _, out _);
    }

    internal static (RunState Run, int Requested, int Remaining)
        BeginSemanticAutomaticHandDraw(RunState run)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic hand draw requires controlled chance guard.");
        var combat = RequireWorld(run).Combat ??
            throw new InvalidOperationException("Missing automatic hand-draw combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (combat.SemanticDeck is null ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cursor?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) != true ||
            cursor.ControlledSemanticDrawRemaining is not null ||
            cursor.ControlledSemanticBeforeHandDrawPending ||
            cursor.ControlledDrawRemaining is not null ||
            cursor.ControlledDrawCostRemaining is not null ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null)
            throw new InvalidOperationException(
                "Expected an existing DrawPlayerHand pipeline checkpoint.");
        var result = ResolveAutomaticStep(
            run, PrototypeContent.Rules.EndTurnPipeline[SemanticHandDrawStepIndex]);
        var after = RequireWorld(result).Combat ??
            throw new InvalidOperationException("Hand draw removed combat.");
        if (after.PendingChoice?.ControlledRandomPileCard is not null &&
            after.AutomaticPipelineContinuation?
                .ControlledSemanticBeforeHandDrawPending == true)
            return (result, 0, 0);
        if (after.PendingChoice?.AutomaticSemanticDrawHookChoice == true &&
            after.AutomaticPipelineContinuation?
                .ControlledSemanticBeforeHandDrawPending == true)
            return (result, 0, 0);
        // A source-committed opening order may supply the complete
        // requested hand without a Nature ticket. This is a valid,
        // genuinely deterministic automatic stage, not an unsupported
        // interruption. Preserve original hooks and advance the cursor
        // exactly once, as the surrounding ResumeAutomaticPipeline would.
        if (!IsSemanticAutomaticHandDraw(after) &&
            after.AutomaticPipelineContinuation?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) == true &&
            after.SemanticDeck is { Pending: null } &&
            after.PendingChoice is null &&
            after.PendingKnowledgeDemonChoice is null)
        {
            var drawn = after.CounterState.CardsDrawnThisCombat -
                combat.CounterState.CardsDrawnThisCombat;
            if (drawn <= 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                    "operation=zero_card_uninterrupted_stage");
            after = after with
            {
                AutomaticPipelineContinuation =
                    after.AutomaticPipelineContinuation with
                    {
                        NextStepIndex = SemanticHandDrawStepIndex + 1
                    }
            };
            return (result with
            {
                World = RequireWorld(result) with { Combat = after }
            }, drawn, 0);
        }
        bool awaitingInsertion = after.PendingChoice?.ControlledDrawInsertion?
            .SemanticDrawContinuation?.AutomaticSemanticHandDrawSegment == true;
        bool awaitingStratagem = after.PendingChoice?
            .AutomaticSemanticStratagemDrawSelection == true;
        bool awaitingHookChoice = after.PendingChoice?
            .AutomaticSemanticDrawHookChoice == true;
        if (!IsSemanticAutomaticHandDraw(after) ||
            (after.SemanticDeck?.Pending is null &&
             after.PendingChoice?.ControlledDrawCost is null &&
             !awaitingInsertion && !awaitingStratagem &&
             !awaitingHookChoice))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                "operation=unsupported_nested_stage");
        var saved = after.AutomaticPipelineContinuation!;
        return (result, saved.ControlledSemanticDrawRequested!.Value,
            saved.ControlledSemanticDrawRemaining!.Value);
    }

    internal static (RunState Run, int Requested, int Remaining)
        ResumeSemanticBeforeHandDrawActionChoice(RunState run)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic pre-draw resumption requires the RNG guard.");
        var combat = RequireWorld(run).Combat ??
            throw new InvalidOperationException(
                "Missing combat for semantic pre-draw resumption.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) != true ||
            !cursor.ControlledSemanticBeforeHandDrawPending ||
            cursor.ControlledSemanticDrawRemaining is not null ||
            cursor.ControlledSemanticDrawRequested is not null ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode, Pending: null })
            throw new InvalidDataException(
                "Semantic BeforeHandDraw choice is not ready to resume.");

        return ResumeSemanticAutomaticHandDrawStep(
            run,
            combat.CounterState.CardsDrawnThisCombat,
            "unsupported_resumed_hand_draw_state",
            "Semantic BeforeHandDraw resumption removed combat.",
            missingCombatIsInvalidData: true);
    }

    internal static (RunState Run, int Remaining)
        CommitSemanticAutomaticHandDrawProgress(
            RunState run, int remaining, bool hasPending)
    {
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing semantic hand draw.");
        if (!IsSemanticAutomaticHandDraw(combat) ||
            (combat.SemanticDeck?.Pending is not null &&
             combat.PendingChoice?.ControlledDrawCost is not null) ||
            remaining < 0)
            throw new InvalidDataException(
                "Semantic automatic draw cursor or pending stage diverged.");
        var cursor = combat.AutomaticPipelineContinuation!;
        if (hasPending)
        {
            if (combat.SemanticDeck?.Pending is null &&
                combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException(
                    "Missing next semantic hand-draw choice.");
            return (run with
            {
                World = world with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation = cursor with
                        {
                            ControlledSemanticDrawRemaining = remaining
                        }
                    }
                }
            }, remaining);
        }

        if (combat.SemanticDeck?.Pending is not null ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidDataException(
                "Cannot finish hand draw with an unresolved choice.");
        // The ordinary engine owns the shared source cleanup and cursor
        // advance for controlled and semantic hand-draw continuations.
        return (CompleteAutomaticHandDrawStage(run), 0);
    }

    private static (RunState Run, int Requested, int Remaining)
        ResumeSemanticAutomaticHandDrawStep(
            RunState checkpoint,
            int cardsDrawnBefore,
            string unsupportedOperation,
            string missingCombatMessage,
            bool missingCombatIsInvalidData)
    {
        var result = ResolveAutomaticStep(
            checkpoint,
            PrototypeContent.Rules.EndTurnPipeline[SemanticHandDrawStepIndex]);
        var after = RequireWorld(result).Combat;
        if (after is null)
        {
            if (missingCombatIsInvalidData)
                throw new InvalidDataException(missingCombatMessage);
            throw new InvalidOperationException(missingCombatMessage);
        }
        if (IsSemanticAutomaticHandDraw(after))
        {
            var cursor = after.AutomaticPipelineContinuation!;
            return (result, cursor.ControlledSemanticDrawRequested!.Value,
                cursor.ControlledSemanticDrawRemaining!.Value);
        }

        var completed = after.AutomaticPipelineContinuation;
        if (after.PendingChoice is not null ||
            after.PendingKnowledgeDemonChoice is not null ||
            completed is null ||
            completed.NextStepIndex != SemanticHandDrawStepIndex + 1 ||
            completed.ControlledSemanticBeforeHandDrawPending ||
            completed.ControlledSemanticDrawRemaining is not null ||
            after.SemanticDeck is not { Pending: null })
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.BeforeHandDraw " +
                "operation=" + unsupportedOperation);

        var requested = completed.ControlledSemanticDrawRequested ??
            Math.Max(0,
                after.CounterState.CardsDrawnThisCombat - cardsDrawnBefore);
        after = after with
        {
            AutomaticPipelineContinuation = completed with
            {
                ControlledSemanticDrawRequested = null
            }
        };
        result = result with
        {
            World = RequireWorld(result) with { Combat = after }
        };
        return (result, requested, 0);
    }

    internal static (RunState Run, int Requested, int Remaining)
        ResumeSemanticRandomPileCardChoice(
            RunState run, SemanticRandomPileCardDecisionV111 decision)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic automatic-hook resumption requires the RNG guard.");
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException(
                "Missing combat for random pile-card choice.");
        var cursor = combat.AutomaticPipelineContinuation;
        var pendingChoice = combat.PendingChoice;
        var pending = pendingChoice?.ControlledRandomPileCard;
        var currentEvent = SemanticRandomPileCardEngineV111
            .DescribePending(run);
        if (cursor?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) != true ||
            !cursor.ControlledSemanticBeforeHandDrawPending ||
            pending is null || pendingChoice?.ChoiceId !=
                "controlled_random_pile_card" ||
            pendingChoice.OuterChoiceContinuation is not { } continuation ||
            currentEvent is null ||
            CanonicalJson.Sha256(currentEvent) !=
                CanonicalJson.Sha256(decision.Event) ||
            decision.SelectedIndex < 0 ||
            decision.SelectedIndex >= pending.CandidateCardInstanceIds.Length)
            throw new InvalidDataException(
                "Stale or malformed semantic automatic-hook choice.");

        var eligible = combat.DrawPile.Where(id =>
        {
            var card = RequireCombatCard(combat, id);
            return RuntimeCardDefinition(card).Type == PrototypeCardType.Power;
        }).ToArray();
        var nonInnate = eligible.Where(id =>
            !IsInnate(RequireCombatCard(combat, id))).ToArray();
        var candidates = nonInnate.Length > 0 ? nonInnate : eligible;
        if (!candidates.SequenceEqual(pending.CandidateCardInstanceIds) ||
            candidates.Length == 0)
            throw new InvalidDataException(
                "Jeweled Mask support changed before the Nature outcome.");
        var selectedId = candidates[decision.SelectedIndex];
        var deck = combat.SemanticDeck ??
            throw new InvalidDataException(
                "Random pile-card choice lost its semantic deck.");
        deck = SemanticDeckEngineV111.RemoveCardInstance(deck, selectedId);
        var handHasSpace = combat.Hand.Length < 10;
        combat = SetCardTemporaryEnergyCost(
            combat, selectedId,
            new PrototypeTemporaryCardCost(
                0, PrototypeTemporaryCardCostExpiry.EndOfTurn |
                    PrototypeTemporaryCardCostExpiry.WhenPlayed));
        combat = SemanticDrawPileAdapterV111
            .ApplyCommittedPhysicalMutation(
                combat,
                combat.DrawPile.Where(id => id != selectedId).ToArray(),
                deck) with
        {
            Hand = handHasSpace
                ? combat.Hand.Append(selectedId).ToArray()
                : combat.Hand,
            DiscardPile = handHasSpace
                ? combat.DiscardPile
                : combat.DiscardPile.Append(selectedId).ToArray(),
            PendingChoice = null
        };
        var resumedEvent = ResumeChoiceResolutionContinuation(
            run.Player, combat, run.Rng, continuation);
        if (resumedEvent.Combat.PendingChoice is not null ||
            resumedEvent.Combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.BeforeHandDraw " +
                "operation=nested_choice_after_random_pile_card");
        var afterHook = run with
        {
            Player = resumedEvent.Player,
            World = world with { Combat = resumedEvent.Combat }
        };
        var beforeDrawCount = resumedEvent.Combat.CounterState
            .CardsDrawnThisCombat;
        return ResumeSemanticAutomaticHandDrawStep(
            afterHook, beforeDrawCount,
            "unsupported_post_hook_draw_state",
            "Semantic hand draw removed combat.",
            missingCombatIsInvalidData: false);
    }
}
