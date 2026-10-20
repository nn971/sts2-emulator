namespace Sts2Emulator.Core;

/// <summary>
/// Applies a recorded supported nested semantic choice to its run checkpoint.
/// End-turn and playable continuations keep their own journals, while this
/// owner shares the mechanics resume path.
/// </summary>
internal static class SemanticNestedChoiceResumeV111
{
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
                return (SemanticDrawChoiceV111.ResumeControlledEffectDraw(
                    checkpoint.Fork(), choice.DrawDecision!,
                    allowEngineOwnedPrototypeReward),
                    "combat.card_effect.draw");
            case "semantic_cost":
            {
                var decision = choice.CostDecision!;
                return checkpoint.World?.Combat?.PendingChoice?
                        .ControlledRandomCost is not null
                    ? (SemanticRandomHandCostChoiceV111.Resume(
                        checkpoint.Fork(), decision.Event.SiteId,
                        decision.SelectedIndex), decision.Event.SiteId)
                    : (SemanticCostChoiceV111.ResumeEffectDrawCost(
                        checkpoint.Fork(), decision.SelectedCost,
                        allowEngineOwnedPrototypeReward),
                        decision.Event.SiteId);
            }
            case "semantic_draw_insertion":
                return (SemanticDrawInsertionChoiceV111.Resume(
                    checkpoint.Fork(), choice.DrawInsertionDecision!),
                    StochasticSiteRegistryV111
                        .AfterShuffleRelicDrawPileInsertSiteId);
            case "semantic_generated_card":
                return (SemanticGenerationChoiceV111.Resume(
                    checkpoint.Fork(), choice.GeneratedCardDecision!),
                    choice.GeneratedCardDecision!.Event.SiteId);
            case "semantic_potion_generation":
                return (SemanticPotionGenerationChoiceV111.Resume(
                    checkpoint.Fork(), choice.PotionGenerationDecision!),
                    choice.PotionGenerationDecision!.Event.SiteId);
            case "semantic_card_transform":
                return (SemanticCardTransformChoiceV111.Resume(
                    checkpoint.Fork(), choice.CardTransformDecision!),
                    choice.CardTransformDecision!.Event.SiteId);
            case "semantic_card_shortlist":
                return (SemanticCardShortlistChoiceV111.Resume(
                    checkpoint.Fork(), choice.CardShortlistDecision!),
                    choice.CardShortlistDecision!.Event.SiteId);
            case "semantic_pool_shuffle":
                return (SemanticPoolShuffleChoiceV111.Resume(
                    checkpoint.Fork(), choice.PoolShuffleDecision!),
                    choice.PoolShuffleDecision!.Event.SiteId);
            case "semantic_hand_cost_selection":
                return (SemanticHandCostSelectionChoiceV111.Resume(
                    checkpoint.Fork(), choice.HandCostSelectionDecision!),
                    choice.HandCostSelectionDecision!.Event.SiteId);
            case "semantic_card_selection":
            {
                var decision = choice.CardSelectionDecision!;
                var selection = SemanticCardSelectionEngineV111.Step(
                    checkpoint.Fork(), decision.ActionId);
                var replayed = selection.Decision ??
                    throw new InvalidDataException(
                        "Semantic card-selection replay lost its decision.");
                SemanticCardSelectionEngineV111.ValidateDecision(
                    decision.Event, replayed);
                if (CanonicalJson.Sha256(decision) !=
                    CanonicalJson.Sha256(replayed))
                    throw new InvalidDataException(
                        "Semantic card-selection replay chose a different action.");
                return (selection.Run, "combat.card_selection");
            }
            case "semantic_draw_hook_choice":
            {
                var decision = choice.DrawHookChoiceDecision!;
                var (run, replayed) = SemanticDrawHookChoiceEngineV111.Resolve(
                    checkpoint.Fork(), decision.Event, decision.ActionId);
                if (CanonicalJson.Sha256(decision) !=
                    CanonicalJson.Sha256(replayed))
                    throw new InvalidDataException(
                        "Semantic draw-hook replay diverged.");
                return (run, "combat.draw.CardDrawn");
            }
            case "knowledge_demon_choice":
            {
                var decision = choice.KnowledgeDemonChoiceDecision!;
                var (run, replayed) =
                    SemanticKnowledgeDemonChoiceEngineV111.Resolve(
                        checkpoint.Fork(), decision.Event, decision.ActionId);
                if (CanonicalJson.Sha256(decision) !=
                    CanonicalJson.Sha256(replayed))
                    throw new InvalidDataException(
                        "Knowledge Demon choice replay diverged.");
                return (run, "combat.ResolveEnemyActions.KnowledgeDemon");
            }
            default:
                throw new InvalidDataException(
                    "Unsupported automatic semantic choice kind.");
        }
    }
}
