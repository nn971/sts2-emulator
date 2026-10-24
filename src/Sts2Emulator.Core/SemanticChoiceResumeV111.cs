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
        var resumed = Resume(checkpoint, decision);
        var run = resumed.Run;
        var pipelineBoundary = resumed.PipelineBoundary ??
            PrototypeAutomaticPipelineResumeResultV111.FromRun(run).Boundary;
        if (!pending.AutomaticHandDraw)
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                    run, requested,
                    PrototypeGameEngine.GetSemanticDrawRemainingCount(
                        run, remaining, drawnBefore), pipelineBoundary),
                decision);

        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Automatic draw-hook choice removed combat.");
        if (pending.BeforeHandDraw &&
            combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true)
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, 0, pipelineBoundary), decision);

        if (pending.BeforeHandDraw &&
            combat.PendingChoice?.ControlledTarget is not null)
        {
            SemanticTargetChoiceV111.ValidateBoundary(run);
            return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                run, requested, 0, pipelineBoundary), decision);
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
                run, requested, remaining, pipelineBoundary), decision);
        }

        var resumedDraw = PrototypeGameEngine
            .ResumeSemanticAutomaticHandDraw(run, remaining);
        return (PrototypeDrawCardsResumeResultV111.ForHandDraw(
                resumedDraw.Run, requested, resumedDraw.RemainingCount,
                resumedDraw.PipelineBoundary)
            .EnsurePipelineBoundary(), decision);
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
