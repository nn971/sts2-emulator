namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in semantic continuation at the genuine DrawPlayerHand automatic
/// pipeline step. A source checkpoint is required: earlier turn stages
/// (including intents) must already have executed, and they are not replayed.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static bool IsSemanticAutomaticHandDraw(CombatState combat)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        return cursor is not null &&
            cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out _, out _);
    }

    internal static PrototypeDrawCardsResumeResultV111
        BeginSemanticAutomaticHandDraw(RunState run)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic hand draw requires controlled chance guard.");
        var combat = RequireWorld(run).Combat ??
            throw new InvalidOperationException("Missing automatic hand-draw combat.");
        if (combat.Stochastic.SemanticDeck is null ||
            ClassifyAutomaticPipelineResumeBoundary(run) !=
            PrototypeAutomaticPipelineResumeBoundaryKindV111.DrawPlayerHand)
            throw new InvalidOperationException(
                "Expected an existing DrawPlayerHand pipeline checkpoint.");
        var (result, requested, remaining) =
            ResumeSemanticAutomaticHandDrawStep(
                run,
                combat.CounterState.CardsDrawnThisCombat,
                "unsupported_nested_stage",
                "Hand draw removed combat.",
                missingCombatIsInvalidData: false);
        return PrototypeDrawCardsResumeResultV111.ForHandDraw(
            result, requested, remaining);
    }

    internal static PrototypeDrawCardsResumeResultV111
        ResumeSemanticBeforeHandDrawActionChoice(RunState run)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic pre-draw resumption requires the RNG guard.");
        var combat = RequireWorld(run).Combat ??
            throw new InvalidOperationException(
                "Missing combat for semantic pre-draw resumption.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor is null ||
            !cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics) ||
            cursor.Stochastic.CompletedSemanticHandDrawRequested is not null ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.Stochastic.SemanticDeck is not
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

    internal static PrototypeDrawCardsResumeResultV111
        CommitSemanticAutomaticHandDrawProgress(
            RunState run, int remaining, bool hasPending)
    {
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing semantic hand draw.");
        if (!IsSemanticAutomaticHandDraw(combat) ||
            (combat.Stochastic.SemanticDeck?.Pending is not null &&
             combat.PendingChoice?.ControlledDrawCost is not null) ||
            remaining < 0)
            throw new InvalidDataException(
                "Semantic automatic draw cursor or pending stage diverged.");
        var cursor = combat.AutomaticPipelineContinuation!;
        if (!cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out int semanticRequested, out _))
            throw new InvalidDataException(
                "Semantic automatic draw lost its tagged progress.");
        if (hasPending)
        {
            if (combat.Stochastic.SemanticDeck?.Pending is null &&
                combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException(
                    "Missing next semantic hand-draw choice.");
            return new PrototypeDrawCardsResumeResultV111(
                CommitAutomaticHandDrawProgress(
                    run, run.Player, combat,
                    PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                        semanticRequested, remaining)),
                remaining);
        }

        if (combat.Stochastic.SemanticDeck?.Pending is not null ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidDataException(
                "Cannot finish hand draw with an unresolved choice.");
        // The ordinary engine owns the shared source cleanup and cursor
        // advance and completion receipt for both provider continuations.
        return new PrototypeDrawCardsResumeResultV111(
            CompleteAutomaticHandDrawStage(run, semanticRequested), 0);
    }

    private static PrototypeDrawCardsResumeResultV111
        ResumeSemanticAutomaticHandDrawStep(
            RunState checkpoint,
            int cardsDrawnBefore,
            string unsupportedOperation,
            string missingCombatMessage,
            bool missingCombatIsInvalidData)
    {
        var result = ResolveAutomaticStep(
            checkpoint,
            PrototypeContent.Rules.EndTurnPipeline[AutomaticHandDrawStepIndex],
            handDrawResultContext:
                AutomaticHandDrawSourceResultContextV111
                    .DeferredSemanticStage);
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
            if (!cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int requested, out int remaining))
                throw new InvalidDataException(
                    "Semantic automatic hand draw lost its tagged progress.");
            return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                result, requested, remaining);
        }

        if (after.AutomaticPipelineContinuation is { } beforeHandDraw &&
            beforeHandDraw.Stochastic.IsAtSemanticBeforeHandDraw(
                beforeHandDraw.Mechanics) &&
            (after.PendingChoice?.ControlledRandomPileCard is not null ||
             after.PendingChoice?.AutomaticSemanticDrawHookChoice == true))
            return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                result, 0, 0);

        var completed = after.AutomaticPipelineContinuation;
        if (completed?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) == true &&
            !completed.Stochastic.HasSavedHandDrawProgress() &&
            after.PendingChoice is null &&
            after.PendingKnowledgeDemonChoice is null &&
            after.Stochastic.SemanticDeck is { Pending: null })
        {
            // A source-committed opening order or an empty-capacity draw
            // can complete this stage without a semantic card ticket.
            // Interpret either result through the shared stage owner.
            int drawn = GetCardsDrawnSince(after, cardsDrawnBefore);
            result = CompleteAutomaticHandDrawStage(
                result, drawn > 0 ? drawn : null);
            return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                result, drawn, 0);
        }
        if (after.PendingChoice is not null ||
            after.PendingKnowledgeDemonChoice is not null ||
            completed is null ||
            !completed.Mechanics.IsImmediatelyAfterStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            completed.Stochastic.HasSavedHandDrawProgress() ||
            after.Stochastic.SemanticDeck is not { Pending: null })
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.BeforeHandDraw " +
                "operation=" + unsupportedOperation);

        var savedRequested = completed.Stochastic
            .CompletedSemanticHandDrawRequested;
        var requested = savedRequested ??
            GetCardsDrawnSince(after, cardsDrawnBefore);
        // The common stage owner already recorded semantic completions that
        // carried a saved request. Only source-committed draw-free completion
        // needs the facade to derive and attach a receipt here.
        if (savedRequested is null)
        {
            after = AttachCompletedSemanticHandDrawReceipt(
                after, completed, requested);
            result = result with
            {
                World = RequireWorld(result) with { Combat = after }
            };
        }
        return PrototypeDrawCardsResumeResultV111.ForHandDraw(
            result, requested, 0);
    }

    internal static PrototypeDrawCardsResumeResultV111
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
        int savedRequested = 0;
        int savedRemaining = 0;
        bool activeHandDraw = cursor is not null &&
            cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out savedRequested, out savedRemaining);
        bool beforeHandDraw = cursor is not null &&
            cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics);
        var currentEvent = SemanticRandomPileCardEngineV111
            .DescribePending(run);
        if ((!beforeHandDraw && !activeHandDraw) ||
            pending is null || pendingChoice?.ChoiceId !=
                "controlled_random_pile_card" ||
            pendingChoice.OuterChoiceContinuation is not { } continuation ||
            currentEvent is null ||
            CanonicalJson.Sha256(currentEvent) !=
                CanonicalJson.Sha256(decision.Event) ||
            !pending.CandidateCardInstanceIds.Contains(
                decision.SelectedCardInstanceId))
            throw new InvalidDataException(
                "Stale or malformed semantic automatic-hook choice.");
        var choiceSite = activeHandDraw
            ? "combat.draw.CardDrawn" : "combat.BeforeHandDraw";

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
        if (!candidates.Contains(decision.SelectedCardInstanceId))
            throw new InvalidDataException(
                "Jeweled Mask selected identity is outside current support.");
        var selectedId = decision.SelectedCardInstanceId;
        if (combat.Stochastic.SemanticDeck is null)
            throw new InvalidDataException(
                "Random pile-card choice lost its semantic deck.");
        var handHasSpace = PrototypeContent.Rules.HasHandCapacity(
            combat.Hand.Length);
        combat = SetCardTemporaryEnergyCost(
            combat, selectedId,
            new PrototypeTemporaryCardCost(
                0, PrototypeTemporaryCardCostExpiry.EndOfTurn |
                    PrototypeTemporaryCardCostExpiry.WhenPlayed));
        var zoneChanges = new Dictionary<PrototypeCardZone, long[]>
        {
            [PrototypeCardZone.DrawPile] = combat.DrawPile
                .Where(id => id != selectedId).ToArray(),
            [handHasSpace
                ? PrototypeCardZone.Hand
                : PrototypeCardZone.DiscardPile] = handHasSpace
                    ? combat.Hand.Append(selectedId).ToArray()
                    : combat.DiscardPile.Append(selectedId).ToArray()
        };
        combat = SemanticDrawPileAdapterV111.ApplyDrawPileRemoval(
            combat,
            [selectedId],
            combat.Mechanics.CardZones.WithZoneChanges(zoneChanges)) with
        {
            PendingChoice = null
        };
        var prototypeRng = run.Stochastic.PrototypeRng;
        var resumedEvent = ResumeChoiceResolutionContinuation(
            run.Player, combat, prototypeRng, continuation);
        if (resumedEvent.Combat.PendingChoice is not null ||
            resumedEvent.Combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=" + choiceSite +
                " operation=nested_choice_after_random_pile_card");
        var afterHook = run with
        {
            Player = resumedEvent.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                run.Stochastic, prototypeRng),
            World = world with { Combat = resumedEvent.Combat }
        };
        if (activeHandDraw)
        {
            var resumedCombat = resumedEvent.Combat;
            var resumedCursor = resumedCombat.AutomaticPipelineContinuation;
            if (resumedCombat.PendingChoice is not null ||
                resumedCombat.PendingKnowledgeDemonChoice is not null ||
                resumedCursor is null ||
                !resumedCursor.Stochastic.TryGetSemanticHandDrawProgress(
                    resumedCursor.Mechanics, out int requestedAfter,
                    out int remainingAfter) ||
                requestedAfter != savedRequested ||
                remainingAfter != savedRemaining)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=nested_choice_after_random_pile_card");
            var continued = BeginSemanticCombatDraw(
                afterHook, remainingAfter, fromHandDraw: true);
            if (continued.RequireRequestedCount() != requestedAfter)
                throw new InvalidDataException(
                    "Random pile-card child changed the hand-draw request.");
            return continued;
        }

        var beforeDrawCount = resumedEvent.Combat.CounterState
            .CardsDrawnThisCombat;
        return ResumeSemanticAutomaticHandDrawStep(
            afterHook, beforeDrawCount,
            "unsupported_post_hook_draw_state",
            "Semantic hand draw removed combat.",
            missingCombatIsInvalidData: false);
    }
}
