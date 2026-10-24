namespace Sts2Emulator.Core;

internal readonly record struct SemanticChoiceResumeResultV111(
    RunState Run, string SiteId,
    PrototypeAutomaticPipelineResumeBoundaryKindV111? PipelineBoundary = null)
{
    internal PrototypeAutomaticPipelineResumeResultV111
        EnsurePipelineBoundary() =>
            PrototypeAutomaticPipelineResumeResultV111
                .FromPipelineProgress(Run, PipelineBoundary);
}

/// <summary>
/// Applies a recorded supported semantic choice to its run checkpoint,
/// including targets whose journals remain in their existing facades.
/// End-turn and playable continuations keep their own journals, while this
/// owner shares the guarded mechanics resume paths.
/// </summary>
internal static class SemanticChoiceResumeV111
{
    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeTarget(
            RunState checkpoint, SemanticTargetDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return SemanticTargetChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
    }

    internal static SemanticChoiceResumeResultV111 ResumeEffectDraw(
        RunState checkpoint, SemanticDrawDecisionV111 decision,
        bool allowEngineOwnedPrototypeReward = true)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticDrawChoiceV111
            .ResumeControlledEffectDrawWithBoundary(
                checkpoint.Fork(), ownedDecision,
                allowEngineOwnedPrototypeReward);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static (PrototypeDrawCardsResumeResultV111 Progress,
        string SiteId)
        ResumeDrawSegment(
            RunState checkpoint, SemanticDrawDecisionV111 decision,
            int remainingCount, bool fromHandDraw)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        if (remainingCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingCount));
        decision.Validate();
        var ownedDecision = decision.Fork();
        var run = checkpoint.Fork();
        var pending = SemanticDrawChoiceV111.ValidateDrawBoundary(run);
        SemanticDrawChoiceV111.ValidateDecision(pending, ownedDecision);
        var world = run.World!;
        var combat = world.Combat!;
        var resolvedCombat = SemanticDrawChoiceV111.Commit(
            combat, ownedDecision);
        run = run with
        {
            World = world with { Combat = resolvedCombat }
        };

        PrototypeDrawCardsResumeResultV111 progress;
        if (fromHandDraw && run.World?.Combat is { } automaticCombat &&
            PrototypeGameEngine.IsSemanticAutomaticHandDraw(
                automaticCombat))
            progress = PrototypeGameEngine
                .ResumeSemanticAutomaticHandDraw(run, remainingCount);
        else
            progress = ControlledChanceGuardV111.ExecuteGuarded(
                () => PrototypeGameEngine.BeginSemanticCombatDraw(
                    run, remainingCount, fromHandDraw));
        SemanticDrawChoiceV111.ValidateRecordedChoice(
            progress.Run, ownedDecision.Event,
            ownedDecision.SelectedIndex);
        return (
            progress.EnsurePipelineBoundary(),
            ownedDecision.Event.SiteId);
    }

    internal static (PrototypeDrawCardsResumeResultV111 Progress,
        SemanticDrawHookChoiceDecisionV111 Decision)
        ResumeDrawHookChoice(
            RunState checkpoint,
            SemanticDrawHookChoiceEventV111 pending,
            string actionId,
            int requested,
            int remaining,
            int drawnBefore)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        if (drawnBefore < 0 || requested < 0 || remaining < 0)
            throw new InvalidDataException(
                "Semantic draw-hook resume progress is invalid.");

        var decision = SemanticDrawHookChoiceEngineV111.Select(
            checkpoint, pending, actionId);
        var resumed = Resume(checkpoint, decision)
            .EnsurePipelineBoundary();
        var run = resumed.Run;
        var pipelineBoundary = resumed.Boundary;
        if (!pending.AutomaticHandDraw)
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, requested,
                    PrototypeGameEngine.GetSemanticDrawRemainingCount(
                        run, remaining, drawnBefore), pipelineBoundary)
                .EnsurePipelineBoundary(),
                decision);

        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Automatic draw-hook choice removed combat.");
        if (pending.BeforeHandDraw &&
            combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true)
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);

        if (pending.BeforeHandDraw &&
            combat.PendingChoice?.ControlledTarget is not null)
        {
            SemanticTargetChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (combat.PendingChoice?.AutomaticSemanticDrawTargetChoice == true)
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested)
                throw new InvalidDataException(
                    "Automatic draw-hook target lost its tagged hand-draw progress.");
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw &&
            combat.PendingChoice?.ControlledRandomPileCard is not null)
        {
            _ = SemanticRandomPileCardEngineV111.DescribePending(run) ??
                throw new InvalidDataException(
                    "BeforeHandDraw random pile-card child lost its event.");
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw &&
            combat.PendingChoice is { } beforeHandDrawEffectDraw &&
            PrototypeGameEngine
                .IsAutomaticBeforeHandDrawHookSemanticDrawContinuation(
                    combat, beforeHandDrawEffectDraw))
        {
            if (beforeHandDrawEffectDraw.ChoiceId == "controlled_draw_cost")
                SemanticCostChoiceV111.ValidateDrawBoundary(run);
            else
                SemanticDrawChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (!pending.BeforeHandDraw &&
            combat.PendingChoice?.ControlledRandomPileCard is not null)
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested ||
                savedRemaining != remaining)
                throw new InvalidDataException(
                    "Automatic draw-hook pile-card child lost its tagged hand-draw progress.");
            _ = SemanticRandomPileCardEngineV111.DescribePending(run) ??
                throw new InvalidDataException(
                    "Automatic draw-hook pile-card child lost its event.");
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (!pending.BeforeHandDraw &&
            combat.PendingChoice is { } nestedEffectDraw &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookSemanticDrawContinuation(
                    combat, nestedEffectDraw))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested || savedRemaining != remaining)
                throw new InvalidDataException(
                    "Automatic draw-hook effect draw lost its tagged hand-draw progress.");
            if (nestedEffectDraw.ChoiceId == "controlled_draw_cost")
                SemanticCostChoiceV111.ValidateDrawBoundary(run);
            else
                SemanticDrawChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (!pending.BeforeHandDraw &&
            combat.PendingChoice is { } nestedDrawInsertion &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookDrawInsertionContinuation(
                    combat, nestedDrawInsertion))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested || savedRemaining != remaining)
                throw new InvalidDataException(
                    "Automatic draw-hook insertion lost its tagged hand-draw progress.");
            SemanticDrawInsertionChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw &&
            combat.PendingChoice is { } beforeHandDrawInsertion &&
            PrototypeGameEngine
                .IsAutomaticBeforeHandDrawHookDrawInsertionContinuation(
                    combat, beforeHandDrawInsertion))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (requested != 0 || remaining != 0 || cursor is null ||
                !cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                    cursor.Mechanics))
                throw new InvalidDataException(
                    "BeforeHandDraw insertion lost its pre-draw cursor.");
            SemanticDrawInsertionChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, 0, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw &&
            combat.PendingChoice is { } beforeHandDrawGeneratedCard &&
            PrototypeGameEngine
                .IsAutomaticHandDrawGeneratedCardContinuation(
                    combat, beforeHandDrawGeneratedCard))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (requested != 0 || remaining != 0 || cursor is null ||
                !cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                    cursor.Mechanics))
                throw new InvalidDataException(
                    "BeforeHandDraw generated-card choice lost its pre-draw cursor.");
            SemanticGenerationChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, 0, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (!pending.BeforeHandDraw &&
            combat.PendingChoice is { } activeHandDrawGeneratedCard &&
            PrototypeGameEngine
                .IsAutomaticHandDrawGeneratedCardContinuation(
                    combat, activeHandDrawGeneratedCard))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested || savedRemaining != remaining)
                throw new InvalidDataException(
                    "Automatic draw-hook generated-card choice lost its tagged hand-draw progress.");
            SemanticGenerationChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw &&
            combat.PendingChoice is { } beforeHandDrawPotionGeneration &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPotionGenerationContinuation(
                    combat, beforeHandDrawPotionGeneration))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (requested != 0 || remaining != 0 || cursor is null ||
                !cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                    cursor.Mechanics))
                throw new InvalidDataException(
                    "BeforeHandDraw potion-generation choice lost its pre-draw cursor.");
            SemanticPotionGenerationChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, 0, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (!pending.BeforeHandDraw &&
            combat.PendingChoice is { } activeHandDrawPotionGeneration &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPotionGenerationContinuation(
                    combat, activeHandDrawPotionGeneration))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested || savedRemaining != remaining)
                throw new InvalidDataException(
                    "Automatic draw-hook potion generation lost its tagged hand-draw progress.");
            SemanticPotionGenerationChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw &&
            combat.PendingChoice is { } beforeHandDrawPoolShuffle &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPoolShuffleContinuation(
                    combat, beforeHandDrawPoolShuffle))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (requested != 0 || remaining != 0 || cursor is null ||
                !cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                    cursor.Mechanics))
                throw new InvalidDataException(
                    "BeforeHandDraw pool shuffle lost its pre-draw cursor.");
            SemanticPoolShuffleChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, 0, 0, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (!pending.BeforeHandDraw &&
            combat.PendingChoice is { } activeHandDrawPoolShuffle &&
            PrototypeGameEngine
                .IsAutomaticHandDrawPoolShuffleContinuation(
                    combat, activeHandDrawPoolShuffle))
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested,
                    out int savedRemaining) ||
                savedRequested != requested || savedRemaining != remaining)
                throw new InvalidDataException(
                    "Automatic draw-hook pool shuffle lost its tagged hand-draw progress.");
            SemanticPoolShuffleChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, savedRemaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        if (pending.BeforeHandDraw)
        {
            PrototypeDrawCardsResumeResultV111 progress;
            progress = ControlledChanceGuardV111.ExecuteGuarded(
                () => PrototypeGameEngine
                    .ResumeSemanticBeforeHandDrawActionChoice(run));
            return (progress.EnsurePipelineBoundary(), decision);
        }

        if (combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true)
        {
            var cursor = combat.AutomaticPipelineContinuation;
            if (cursor is null ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out _, out int savedRemaining))
                throw new InvalidDataException(
                    "Automatic draw-hook choice lost its tagged cursor.");
            remaining = savedRemaining;
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, remaining, pipelineBoundary)
                .EnsurePipelineBoundary(), decision);
        }

        var resumedDraw = PrototypeGameEngine
            .ResumeSemanticAutomaticHandDraw(run, remaining);
        if (resumedDraw.RequireRequestedCount() != requested)
            throw new InvalidDataException(
                "Automatic draw-hook resume changed its requested count.");
        return (resumedDraw.EnsurePipelineBoundary(), decision);
    }

    internal static (
        PrototypeDrawCardsResumeResultV111 Progress,
        SemanticCardSelectionDecisionV111 Decision)
        ResumeStratagemSelection(
            RunState checkpoint,
            SemanticCardSelectionEventV111 pending,
            string actionId,
            int remaining,
            bool automaticHandDraw,
            int drawnBefore)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(pending);
        pending.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (remaining < 0 || drawnBefore < 0)
            throw new InvalidDataException(
                "Semantic Stratagem draw progress is invalid.");
        var decision = SemanticCardSelectionEngineV111.Select(
            pending, pending.EventId, actionId);
        var selected = Resume(checkpoint, decision);
        var selectedRun = selected.Run;

        if (!automaticHandDraw)
            return (new PrototypeDrawCardsResumeResultV111(
                    selectedRun,
                    PrototypeGameEngine.GetSemanticDrawRemainingCount(
                        selectedRun, remaining, drawnBefore),
                    PipelineBoundary: selected.PipelineBoundary)
                .EnsurePipelineBoundary(), decision);

        if (!PrototypeGameEngine.IsSemanticAutomaticHandDraw(
                selectedRun.World?.Combat ?? throw new InvalidDataException(
                    "Automatic Stratagem selection removed combat.")))
            throw new InvalidDataException(
                "Automatic Stratagem selection lost its pipeline cursor.");
        var resumed =
            PrototypeGameEngine.ResumeSemanticAutomaticHandDraw(
                selectedRun, remaining);
        return (resumed.EnsurePipelineBoundary(), decision);
    }

    internal static RunState ResumeDeckReveal(
        RunState checkpoint, SemanticDeckRevealDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return SemanticDeckRevealChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision);
    }

    internal static PrototypeDrawCardsResumeResultV111
        ResumeCombatDrawCost(
            RunState checkpoint, int selectedCost, int remainingCount,
            int cardsDrawnBefore)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (remainingCount < 0 || cardsDrawnBefore < 0)
            throw new InvalidDataException(
                "Semantic combat-draw cost progress is invalid.");
        var run = checkpoint.Fork();
        var pending = run.World?.Combat?.PendingChoice;
        if (pending is not null &&
            run.World?.Combat is { } activeHandDrawCombat &&
            PrototypeGameEngine
                .IsAutomaticHandDrawHookSemanticDrawContinuation(
                    activeHandDrawCombat, pending))
        {
            var cursor = activeHandDrawCombat.AutomaticPipelineContinuation!;
            if (!cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int requested, out _))
                throw new InvalidDataException(
                    "Nested effect-draw cost lost its active hand-draw cursor.");
            var resumedEffectCost = SemanticCostChoiceV111
                .ResumeEffectDrawCostWithBoundary(
                    run, selectedCost,
                    allowEngineOwnedPrototypeReward: false);
            run = resumedEffectCost.Run;
            var resumedCombat = run.World?.Combat ??
                throw new InvalidDataException(
                    "Nested effect-draw cost removed combat.");
            if (resumedCombat.PendingChoice is null &&
                resumedCombat.PendingKnowledgeDemonChoice is null &&
                resumedCombat.Stochastic.SemanticDeck?.Pending is null &&
                SemanticCombatDrawV111.TryGetAutomaticSemanticHandDrawProgress(
                    resumedCombat, out _, out int savedRemaining))
            {
                var handDraw = PrototypeGameEngine
                    .ResumeSemanticAutomaticHandDraw(run, savedRemaining);
                if (handDraw.RequireRequestedCount() != requested)
                    throw new InvalidDataException(
                        "Nested effect-draw cost changed the active hand-draw request.");
                return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    handDraw.Run, requested, handDraw.RemainingCount,
                    handDraw.PipelineBoundary);
            }

            if (SemanticCombatDrawV111.TryGetAutomaticSemanticHandDrawProgress(
                    resumedCombat, out int resumedRequested,
                    out int resumedRemaining))
            {
                if (resumedRequested != requested)
                    throw new InvalidDataException(
                        "Nested effect-draw cost changed the active hand-draw request.");
                return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, requested, resumedRemaining,
                    resumedEffectCost.Boundary);
            }
            var completedCursor = resumedCombat.AutomaticPipelineContinuation;
            if (completedCursor is not null &&
                (completedCursor.Stochastic.IsAtSemanticBeforeHandDraw(
                     completedCursor.Mechanics) ||
                 completedCursor.Mechanics.IsImmediatelyAfterStep(
                     PrototypeAutomaticStepKind.DrawPlayerHand)) &&
                !completedCursor.Stochastic.HasSavedHandDrawProgress())
                return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, requested, 0, resumedEffectCost.Boundary);
            throw new InvalidDataException(
                "Nested effect-draw cost lost its active hand-draw cursor.");
        }

        if (pending is not null &&
            run.World?.Combat is { } beforeHandDrawCombat &&
            PrototypeGameEngine
                .IsAutomaticBeforeHandDrawHookSemanticDrawContinuation(
                    beforeHandDrawCombat, pending))
        {
            var resumedEffectCost = SemanticCostChoiceV111
                .ResumeEffectDrawCostWithBoundary(
                    run, selectedCost,
                    allowEngineOwnedPrototypeReward: false);
            run = resumedEffectCost.Run;
            var resumedCombat = run.World?.Combat ??
                throw new InvalidDataException(
                    "BeforeHandDraw effect-draw cost removed combat.");
            if (SemanticCombatDrawV111.TryGetAutomaticSemanticHandDrawProgress(
                    resumedCombat, out int requested, out int savedRemaining))
            {
                if (resumedCombat.PendingChoice is null &&
                    resumedCombat.PendingKnowledgeDemonChoice is null &&
                    resumedCombat.Stochastic.SemanticDeck?.Pending is null)
                {
                    var handDraw = ControlledChanceGuardV111.ExecuteGuarded(
                        () => PrototypeGameEngine
                            .ResumeSemanticAutomaticHandDraw(
                                run, savedRemaining));
                    if (handDraw.RequireRequestedCount() != requested)
                        throw new InvalidDataException(
                            "BeforeHandDraw effect-draw cost changed the hand-draw request.");
                    return handDraw.EnsurePipelineBoundary();
                }
                return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, requested, savedRemaining,
                    resumedEffectCost.Boundary)
                    .EnsurePipelineBoundary();
            }

            var cursor = resumedCombat.AutomaticPipelineContinuation;
            if (cursor is not null &&
                cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                    cursor.Mechanics) &&
                cursor.Stochastic.CompletedSemanticHandDrawRequested is null &&
                resumedCombat.PendingChoice is null &&
                resumedCombat.PendingKnowledgeDemonChoice is null &&
                resumedCombat.Stochastic.SemanticDeck?.Pending is null)
                return ControlledChanceGuardV111.ExecuteGuarded(
                    () => PrototypeGameEngine
                        .ResumeSemanticBeforeHandDrawActionChoice(run))
                    .EnsurePipelineBoundary();

            if (cursor is not null &&
                cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                    cursor.Mechanics) &&
                cursor.Stochastic.CompletedSemanticHandDrawRequested is null)
                return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, 0, 0, resumedEffectCost.Boundary)
                    .EnsurePipelineBoundary();

            if (cursor is not null &&
                cursor.Mechanics.IsImmediatelyAfterStep(
                    PrototypeAutomaticStepKind.DrawPlayerHand) &&
                !cursor.Stochastic.HasSavedHandDrawProgress() &&
                cursor.Stochastic.CompletedSemanticHandDrawRequested is
                    { } completedRequested)
                return PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, completedRequested, 0,
                    resumedEffectCost.Boundary)
                    .EnsurePipelineBoundary();

            if (resumedCombat.PendingChoice is not null ||
                resumedCombat.PendingKnowledgeDemonChoice is not null)
                throw new InvalidDataException(
                    "BeforeHandDraw effect-draw cost lost its saved cursor.");

            if (cursor is null)
                throw new InvalidDataException(
                    "BeforeHandDraw effect-draw cost lost its saved cursor.");
            throw new InvalidDataException(
                "BeforeHandDraw effect-draw cost resumed at an unsupported cursor.");
        }

        if (pending?.ControlledSemanticDraw?
                .StandaloneSemanticDrawSegment == true)
        {
            var resumed = SemanticCostChoiceV111
                .ResumeEffectDrawCostWithBoundary(
                    run, selectedCost,
                    allowEngineOwnedPrototypeReward: false);
            return new PrototypeDrawCardsResumeResultV111(
                resumed.Run,
                PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    resumed.Run, remainingCount, cardsDrawnBefore),
                PipelineBoundary: resumed.Boundary);
        }

        return ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeSemanticCombatDrawCost(
                run, selectedCost));
    }

    internal static (PrototypeDrawCardsResumeResultV111 Progress,
        string SiteId) Resume(
        RunState checkpoint,
        SemanticRandomPileCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        PrototypeDrawCardsResumeResultV111 progress;
        progress = ControlledChanceGuardV111.ExecuteGuarded(
            () => PrototypeGameEngine.ResumeSemanticRandomPileCardChoice(
                checkpoint.Fork(), ownedDecision));
        return (progress.EnsurePipelineBoundary(),
            ownedDecision.Event.SiteId);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticDrawInsertionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticDrawInsertionChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticCardSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var (selection, pipelineResume) =
            SemanticCardSelectionEngineV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        var replayed = selection.Decision ??
            throw new InvalidDataException(
                "Semantic card-selection replay lost its decision.");
        SemanticCardSelectionEngineV111.ValidateDecision(
            ownedDecision.Event, replayed);
        if (CanonicalJson.Sha256(ownedDecision) !=
            CanonicalJson.Sha256(replayed))
            throw new InvalidDataException(
                "Semantic card-selection replay chose a different action.");
        return new SemanticChoiceResumeResultV111(
            selection.Run, "combat.card_selection",
            pipelineResume.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticCostDecisionV111 decision,
        bool allowEngineOwnedPrototypeReward = true)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var siteId = ownedDecision.Event.SiteId;
        if (ownedDecision.Event.Request.StreamId ==
                SemanticCostChoiceV111.NativeHandCostStreamId &&
            checkpoint.World?.Combat?.Stochastic.NativeRng is null)
            throw new InvalidDataException(
                "Native random-hand-cost stream attribution has no Native combat RNG owner.");
        RunState run;
        PrototypeAutomaticPipelineResumeBoundaryKindV111?
            pipelineBoundary = null;
        if (checkpoint.World?.Combat?.PendingChoice?
                .ControlledRandomCost is not null)
        {
            var resumed = SemanticRandomHandCostChoiceV111
                .ResumeWithBoundary(
                    checkpoint.Fork(), siteId,
                    ownedDecision.SelectedIndex);
            run = resumed.Run;
            pipelineBoundary = resumed.Boundary;
        }
        else
        {
            var resumed = SemanticCostChoiceV111
                .ResumeEffectDrawCostWithBoundary(
                    checkpoint.Fork(), ownedDecision.SelectedCost,
                    allowEngineOwnedPrototypeReward);
            run = resumed.Run;
            pipelineBoundary = resumed.Boundary;
        }
        return new SemanticChoiceResumeResultV111(
            run, siteId, pipelineBoundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticGeneratedCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticGenerationChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111
        ResumeGeneratedCardForHandDraw(
            RunState checkpoint, SemanticGeneratedCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticGenerationChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision,
            deferAutomaticHandDrawContinuation: true);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticPotionGenerationDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticPotionGenerationChoiceV111
            .ResumeWithBoundary(checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111
        ResumePotionGenerationForHandDraw(
            RunState checkpoint,
            SemanticPotionGenerationDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticPotionGenerationChoiceV111
            .ResumeWithBoundary(checkpoint.Fork(), ownedDecision,
                deferSemanticHandDrawContinuation: true);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticCardTransformDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticCardTransformChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticCardShortlistDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticCardShortlistChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticPoolShuffleDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticPoolShuffleChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111
        ResumePoolShuffleForHandDraw(
            RunState checkpoint, SemanticPoolShuffleDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticPoolShuffleChoiceV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision,
            deferAutomaticHandDrawContinuation: true);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint,
        SemanticHandCostSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticHandCostSelectionChoiceV111
            .ResumeWithBoundary(checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, ownedDecision.Event.SiteId, resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticDrawHookChoiceDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var (resumed, replayed) =
            SemanticDrawHookChoiceEngineV111.ResumeWithBoundary(
                checkpoint.Fork(), ownedDecision);
        if (CanonicalJson.Sha256(ownedDecision) !=
            CanonicalJson.Sha256(replayed))
            throw new InvalidDataException(
                "Semantic draw-hook replay diverged.");
        return new SemanticChoiceResumeResultV111(
            resumed.Run, "combat.draw.CardDrawn", resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint,
        SemanticKnowledgeDemonChoiceDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var resumed = SemanticKnowledgeDemonChoiceEngineV111.ResumeWithBoundary(
            checkpoint.Fork(), ownedDecision);
        return new SemanticChoiceResumeResultV111(
            resumed.Run, "combat.ResolveEnemyActions.KnowledgeDemon",
            resumed.Boundary);
    }

    internal static SemanticChoiceResumeResultV111 Resume(
        RunState checkpoint, SemanticEndTurnNestedChoiceV111 choice,
        bool allowEngineOwnedPrototypeReward = true)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(choice);
        choice.Validate();

        switch (choice.Kind)
        {
            case "semantic_draw":
                return ResumeEffectDraw(
                    checkpoint, choice.DrawDecision!,
                    allowEngineOwnedPrototypeReward);
            case "semantic_cost":
                return Resume(checkpoint, choice.CostDecision!,
                    allowEngineOwnedPrototypeReward);
            case "semantic_draw_insertion":
                return Resume(checkpoint, choice.DrawInsertionDecision!);
            case "semantic_generated_card":
                return Resume(checkpoint, choice.GeneratedCardDecision!);
            case "semantic_potion_generation":
                return Resume(checkpoint, choice.PotionGenerationDecision!);
            case "semantic_card_transform":
                return Resume(checkpoint, choice.CardTransformDecision!);
            case "semantic_card_shortlist":
                return Resume(checkpoint, choice.CardShortlistDecision!);
            case "semantic_pool_shuffle":
                return Resume(checkpoint, choice.PoolShuffleDecision!);
            case "semantic_hand_cost_selection":
                return Resume(checkpoint,
                    choice.HandCostSelectionDecision!);
            case "semantic_card_selection":
                return Resume(checkpoint, choice.CardSelectionDecision!);
            case "semantic_draw_hook_choice":
                return Resume(checkpoint,
                    choice.DrawHookChoiceDecision!);
            case "knowledge_demon_choice":
                return Resume(checkpoint,
                    choice.KnowledgeDemonChoiceDecision!);
            default:
                throw new InvalidDataException(
                    "Unsupported automatic semantic choice kind.");
        }
    }

    internal static (
        PrototypeAutomaticPipelineResumeResultV111 Resume,
        string SiteId)
        ResumeAutomaticPipelineChoice(
            RunState checkpoint, SemanticEndTurnNestedChoiceV111 choice,
            bool allowEngineOwnedPrototypeReward = true)
    {
        var resumed = Resume(
            checkpoint, choice, allowEngineOwnedPrototypeReward);
        return (
            resumed.EnsurePipelineBoundary(),
            resumed.SiteId);
    }

}
