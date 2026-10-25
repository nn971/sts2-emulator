namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in semantic continuation at the genuine DrawPlayerHand automatic
/// pipeline step. A source checkpoint is required: earlier turn stages
/// (including intents) must already have executed, and they are not replayed.
/// </summary>
public sealed partial class PrototypeGameEngine
{
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
        return ResumeSemanticAutomaticHandDrawStep(
            run,
            combat.CounterState.CardsDrawnThisCombat,
            "unsupported_nested_stage",
            "Hand draw removed combat.",
            missingCombatIsInvalidData: false);
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
        ResumeSemanticAutomaticHandDrawAfterNestedChoice(
            RunState source, RunState resumed,
            PrototypeAutomaticPipelineResumeBoundaryKindV111? boundary,
            string choiceKind,
            int? expectedSourceRequested = null,
            int? expectedSourceRemaining = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resumed);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceKind);
        var sourceCombat = source.World?.Combat ??
            throw new InvalidDataException(
                $"Semantic {choiceKind.ToLowerInvariant()} source lost combat.");
        var sourceCursor = sourceCombat.AutomaticPipelineContinuation ??
            throw new InvalidDataException(
                $"Semantic {choiceKind.ToLowerInvariant()} source lost its pipeline cursor.");
        var combat = resumed.World?.Combat ??
            throw new InvalidDataException(
                $"Semantic {choiceKind.ToLowerInvariant()} choice removed combat.");
        var cursor = combat.AutomaticPipelineContinuation ??
            throw new InvalidDataException(
                $"Semantic {choiceKind.ToLowerInvariant()} continuation lost its pipeline cursor.");
        cursor.Stochastic.ValidateSemanticHandDrawProgressTransitionFrom(
            sourceCursor.Stochastic, sourceCursor.Mechanics,
            cursor.Mechanics, expectedSourceRequested,
            expectedSourceRemaining);

        if (TryGetSemanticAutomaticHandDrawProgress(
                combat, out int requested, out int remaining))
        {
            if (combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.Stochastic.SemanticDeck?.Pending is null)
            {
                var handDraw = ResumeSemanticAutomaticHandDraw(
                    resumed, remaining);
                if (handDraw.RequireRequestedCount() != requested)
                    throw new InvalidDataException(
                        $"Semantic {choiceKind.ToLowerInvariant()} continuation changed the automatic hand-draw request.");
                return handDraw.EnsurePipelineBoundary();
            }
            return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                resumed, requested, remaining, boundary)
                .EnsurePipelineBoundary();
        }

        if (cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics) &&
            cursor.Stochastic.CompletedSemanticHandDrawRequested is null)
        {
            if (combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.Stochastic.SemanticDeck?.Pending is null)
                return ControlledChanceGuardV111.ExecuteGuarded(
                    () => ResumeSemanticBeforeHandDrawActionChoice(resumed))
                    .EnsurePipelineBoundary();
            return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                resumed, 0, 0, boundary).EnsurePipelineBoundary();
        }

        if (cursor.Mechanics.IsImmediatelyAfterStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) &&
            !cursor.Stochastic.HasSavedHandDrawProgress() &&
            cursor.Stochastic.CompletedSemanticHandDrawRequested is
                { } completedRequested)
        {
            return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                resumed, completedRequested, 0, boundary)
                .EnsurePipelineBoundary();
        }

        throw new InvalidDataException(
            $"Semantic {choiceKind.ToLowerInvariant()} continuation lost the automatic hand-draw cursor.");
    }

    internal static PrototypeDrawCardsResumeResultV111
        CommitSemanticAutomaticHandDrawProgress(
            RunState run, int remaining, bool hasPending)
    {
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing semantic hand draw.");
        if (!TryGetSemanticAutomaticHandDrawProgress(
                combat, out int semanticRequested, out _) ||
            (combat.Stochastic.SemanticDeck?.Pending is not null &&
             combat.PendingChoice?.ControlledDrawCost is not null) ||
            remaining < 0)
            throw new InvalidDataException(
                "Semantic automatic draw cursor or pending stage diverged.");
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
        if (TryGetSemanticAutomaticHandDrawProgress(
                after, out int requested, out int remaining))
        {
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
            .GetSavedSemanticHandDrawRequestedCount(
                completed.Mechanics);
        var completedRequested = savedRequested ??
            GetCardsDrawnSince(after, cardsDrawnBefore);
        // The common stage owner already recorded semantic completions that
        // carried a saved request. Only source-committed draw-free completion
        // needs the facade to derive and attach a receipt here.
        if (savedRequested is null)
        {
            after = AttachCompletedSemanticHandDrawReceipt(
                after, completed, completedRequested);
            result = result with
            {
                World = RequireWorld(result) with { Combat = after }
            };
        }
        return PrototypeDrawCardsResumeResultV111.ForHandDraw(
            result, completedRequested, 0);
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
        if (cursor is null ||
            !cursor.Stochastic.IsAtSemanticHandDrawContinuationBoundary(
                cursor.Mechanics) ||
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
        var afterHook = PublishDrawMechanicsAndPrototypeRng(
            run, resumedEvent.Player, resumedEvent.Combat, prototypeRng);
        return ResumeSemanticAutomaticHandDrawAfterNestedChoice(
            run, afterHook, null, "random pile-card",
            activeHandDraw ? savedRequested : null,
            activeHandDraw ? savedRemaining : null);
    }
}
