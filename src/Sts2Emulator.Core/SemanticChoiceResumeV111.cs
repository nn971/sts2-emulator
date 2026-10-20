namespace Sts2Emulator.Core;

/// <summary>
/// Applies a recorded supported semantic choice to its run checkpoint,
/// including targets whose journals remain in their existing facades.
/// End-turn and playable continuations keep their own journals, while this
/// owner shares the guarded mechanics resume paths.
/// </summary>
internal static class SemanticChoiceResumeV111
{
    internal static RunState ResumeTarget(
        RunState checkpoint, SemanticTargetDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return SemanticTargetChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision);
    }

    internal static (RunState Run, string SiteId) ResumeEffectDraw(
        RunState checkpoint, SemanticDrawDecisionV111 decision,
        bool allowEngineOwnedPrototypeReward = true)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticDrawChoiceV111.ResumeControlledEffectDraw(
            checkpoint.Fork(), ownedDecision,
            allowEngineOwnedPrototypeReward),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, int RemainingCount, string SiteId)
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

        RunState advanced;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (advanced, remaining) = PrototypeGameEngine
                .BeginSemanticCombatDraw(
                    run, remainingCount, fromHandDraw);
        SemanticDrawChoiceV111.ValidateRecordedChoice(
            advanced, ownedDecision.Event, ownedDecision.SelectedIndex);
        return (advanced, remaining, ownedDecision.Event.SiteId);
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

    internal static (RunState Run, int RemainingCount)
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
            var advanced = SemanticCostChoiceV111.ResumeEffectDrawCost(
                run, selectedCost,
                allowEngineOwnedPrototypeReward: false);
            return (advanced,
                PrototypeGameEngine.GetSemanticDrawRemainingCount(
                    advanced, remainingCount, cardsDrawnBefore));
        }

        using (ControlledChanceGuardV111.Enter())
            return PrototypeGameEngine.ResumeSemanticCombatDrawCost(
                run, selectedCost);
    }

    internal static (RunState Run, int Requested, int Remaining,
        string SiteId) Resume(
        RunState checkpoint,
        SemanticRandomPileCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        RunState run;
        int requested;
        int remaining;
        using (ControlledChanceGuardV111.Enter())
            (run, requested, remaining) =
                PrototypeGameEngine.ResumeSemanticRandomPileCardChoice(
                    checkpoint.Fork(), ownedDecision);
        return (run, requested, remaining, ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticDrawInsertionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticDrawInsertionChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision), ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticCardSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var selection = SemanticCardSelectionEngineV111.Resume(
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
        return (selection.Run, "combat.card_selection");
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticCostDecisionV111 decision,
        bool allowEngineOwnedPrototypeReward = true)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var siteId = ownedDecision.Event.SiteId;
        var run = checkpoint.World?.Combat?.PendingChoice?
                .ControlledRandomCost is not null
            ? SemanticRandomHandCostChoiceV111.Resume(
                checkpoint.Fork(), siteId, ownedDecision.SelectedIndex)
            : SemanticCostChoiceV111.ResumeEffectDrawCost(
                checkpoint.Fork(), ownedDecision.SelectedCost,
                allowEngineOwnedPrototypeReward);
        return (run, siteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticGeneratedCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticGenerationChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticPotionGenerationDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticPotionGenerationChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticCardTransformDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticCardTransformChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticCardShortlistDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticCardShortlistChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticPoolShuffleDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticPoolShuffleChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint,
        SemanticHandCostSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        return (SemanticHandCostSelectionChoiceV111.Resume(
            checkpoint.Fork(), ownedDecision),
            ownedDecision.Event.SiteId);
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint, SemanticDrawHookChoiceDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var (run, replayed) = SemanticDrawHookChoiceEngineV111.Resume(
            checkpoint.Fork(), ownedDecision);
        if (CanonicalJson.Sha256(ownedDecision) !=
            CanonicalJson.Sha256(replayed))
            throw new InvalidDataException(
                "Semantic draw-hook replay diverged.");
        return (run, "combat.draw.CardDrawn");
    }

    internal static (RunState Run, string SiteId) Resume(
        RunState checkpoint,
        SemanticKnowledgeDemonChoiceDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var ownedDecision = decision.Fork();
        var run = SemanticKnowledgeDemonChoiceEngineV111.Resume(
            checkpoint.Fork(), ownedDecision);
        return (run, "combat.ResolveEnemyActions.KnowledgeDemon");
    }

    internal static (RunState Run, string SiteId) Resume(
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

}
