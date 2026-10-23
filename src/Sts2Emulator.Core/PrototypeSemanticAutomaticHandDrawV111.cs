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

    internal static SemanticDrawProgressResumeResultV111
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
        if (requested == 0 && remaining == 0)
        {
            var after = RequireWorld(result).Combat ??
                throw new InvalidOperationException(
                    "Hand draw removed combat.");
            bool awaitingPreDrawChoice =
                after.AutomaticPipelineContinuation?.Stochastic
                    .HandDrawProgress?.Kind ==
                        PrototypeAutomaticHandDrawProgressKindV111
                            .SemanticBeforeHandDraw &&
                (after.PendingChoice?.ControlledRandomPileCard is not null ||
                 after.PendingChoice?.AutomaticSemanticDrawHookChoice == true);
            bool completedWithoutPendingDraw =
                after.AutomaticPipelineContinuation is { } completed &&
                completed.Mechanics.IsImmediatelyAfterStep(
                    PrototypeAutomaticStepKind.DrawPlayerHand) &&
                completed.Stochastic.HandDrawProgress is null &&
                completed.Stochastic.CompletedSemanticHandDrawRequested is null &&
                after.PendingChoice is null &&
                after.PendingKnowledgeDemonChoice is null &&
                after.Stochastic.SemanticDeck?.Pending is null;
            // The ordinary stage may finish without a Nature ticket when
            // the hand has no remaining capacity or a committed order drew
            // the cards. A suspended BeforeHandDraw choice remains pending.
            if (!awaitingPreDrawChoice && !completedWithoutPendingDraw)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                    "operation=zero_card_uninterrupted_stage");
        }
        if (requested == 0 && remaining > 0)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                "operation=unsupported_nested_stage");
        return new SemanticDrawProgressResumeResultV111(
            result, requested, remaining);
    }

    internal static SemanticDrawProgressResumeResultV111
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
            cursor.Stochastic.HandDrawProgress?.Kind !=
                PrototypeAutomaticHandDrawProgressKindV111
                    .SemanticBeforeHandDraw ||
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

    internal static SemanticCombatDrawResumeResultV111
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
            return new SemanticCombatDrawResumeResultV111(
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
        return new SemanticCombatDrawResumeResultV111(
            CompleteAutomaticHandDrawStage(run, semanticRequested), 0);
    }

    private static SemanticDrawProgressResumeResultV111
        ResumeSemanticAutomaticHandDrawStep(
            RunState checkpoint,
            int cardsDrawnBefore,
            string unsupportedOperation,
            string missingCombatMessage,
            bool missingCombatIsInvalidData)
    {
        var result = ResolveAutomaticStep(
            checkpoint,
            PrototypeContent.Rules.EndTurnPipeline[SemanticHandDrawStepIndex],
            deferAutomaticHandDrawCompletion: true);
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
            return new SemanticDrawProgressResumeResultV111(
                result, requested, remaining);
        }

        if (after.AutomaticPipelineContinuation?
                .Stochastic.HandDrawProgress?.Kind ==
                    PrototypeAutomaticHandDrawProgressKindV111
                        .SemanticBeforeHandDraw &&
            (after.PendingChoice?.ControlledRandomPileCard is not null ||
             after.PendingChoice?.AutomaticSemanticDrawHookChoice == true))
            return new SemanticDrawProgressResumeResultV111(
                result, 0, 0);

        var completed = after.AutomaticPipelineContinuation;
        if (completed?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) == true &&
            completed.Stochastic.HandDrawProgress is null &&
            after.PendingChoice is null &&
            after.PendingKnowledgeDemonChoice is null &&
            after.Stochastic.SemanticDeck is { Pending: null })
        {
            // A source-committed opening order or an empty-capacity draw
            // can complete this stage without a semantic card ticket.
            // Interpret either result through the shared stage owner.
            int drawn = after.CounterState.CardsDrawnThisCombat -
                cardsDrawnBefore;
            if (drawn < 0)
                throw new InvalidDataException(
                    "Automatic hand draw reduced the combat draw count.");
            result = CompleteAutomaticHandDrawStage(
                result, drawn > 0 ? drawn : null);
            return new SemanticDrawProgressResumeResultV111(
                result, drawn, 0);
        }
        if (after.PendingChoice is not null ||
            after.PendingKnowledgeDemonChoice is not null ||
            completed is null ||
            !completed.Mechanics.IsImmediatelyAfterStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            completed.Stochastic.HandDrawProgress is not null ||
            after.Stochastic.SemanticDeck is not { Pending: null })
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.BeforeHandDraw " +
                "operation=" + unsupportedOperation);

        var savedRequested = completed.Stochastic
            .CompletedSemanticHandDrawRequested;
        var requested = savedRequested ??
            Math.Max(0,
                after.CounterState.CardsDrawnThisCombat - cardsDrawnBefore);
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
        return new SemanticDrawProgressResumeResultV111(
            result, requested, 0);
    }

    internal static SemanticDrawProgressResumeResultV111
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
            cursor.Stochastic.HandDrawProgress?.Kind !=
                PrototypeAutomaticHandDrawProgressKindV111
                    .SemanticBeforeHandDraw ||
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
                "UNSUPPORTED_SEMANTIC_DECK site=combat.BeforeHandDraw " +
                "operation=nested_choice_after_random_pile_card");
        var afterHook = run with
        {
            Player = resumedEvent.Player,
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                run.Stochastic, prototypeRng),
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
