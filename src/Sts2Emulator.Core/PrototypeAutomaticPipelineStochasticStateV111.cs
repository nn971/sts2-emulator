namespace Sts2Emulator.Core;

/// <summary>
/// Saved stochastic cursors and provider decisions for one automatic combat
/// pipeline. The continuation's legacy properties remain the snapshot wire
/// projection during migration.
/// </summary>
public sealed record PrototypeAutomaticPipelineStochasticStateV111(
    int? ControlledDrawRemaining = null,
    int? ControlledDrawCostRemaining = null,
    int? ControlledSemanticDrawRemaining = null,
    int? ControlledSemanticDrawRequested = null,
    bool ControlledSemanticBeforeHandDrawPending = false,
    bool ControlledRecycleStratagemPending = false,
    PrototypeControlledEnemyActionContinuationV111? ControlledEnemyAction = null,
    ControlledTurnIntentsV111? ControlledTurnIntents = null)
{
    public PrototypeAutomaticPipelineStochasticStateV111 Fork() => this with
    {
        ControlledEnemyAction = ControlledEnemyAction?.Fork(),
        ControlledTurnIntents = ControlledTurnIntents?.Fork()
    };

    public void Validate(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(continuation);

        if (ControlledRecycleStratagemPending &&
            (continuation.NextStepIndex < 0 ||
             continuation.NextStepIndex >=
                PrototypeContent.Rules.EndTurnPipeline.Length ||
             PrototypeContent.Rules.EndTurnPipeline[
                    continuation.NextStepIndex].Kind !=
                PrototypeAutomaticStepKind.DrawPlayerHand ||
             ControlledDrawRemaining is not > 0 ||
             ControlledDrawCostRemaining is not null ||
             combat.PendingChoice is not
                { ChoiceId: "select_cards",
                  Selection.SourceZone: PrototypeCardZone.DrawPile } ||
             combat.SemanticDeck is not null))
            throw new InvalidOperationException(
                "Controlled recycle Stratagem selection is malformed.");

        if (ControlledTurnIntents is { } turnIntents)
        {
            turnIntents.Validate();
            bool initialIntentStage = continuation.OpeningStage ==
                PrototypeOpeningCombatStage.InitialEnemyIntents;
            int advanceTurnIndex = Array.FindIndex(
                PrototypeContent.Rules.EndTurnPipeline,
                step => step.Kind == PrototypeAutomaticStepKind.AdvanceTurn);
            var baseline = turnIntents.BaselineCombat with
            {
                AutomaticPipelineContinuation = null
            };
            var checkpoint = combat with
            {
                AutomaticPipelineContinuation = null
            };
            if (advanceTurnIndex < 0 ||
                turnIntents.SiteId != (initialIntentStage
                    ? NativeCombatRngEntryV111.CommittedIntentSiteId
                    : ControlledTurnIntentEngineV111.SiteId) ||
                (!initialIntentStage &&
                 (continuation.NextStepIndex != advanceTurnIndex ||
                  continuation.OpeningStage is not null)) ||
                (initialIntentStage && continuation.NextStepIndex !=
                    PrototypeContent.Rules.EndTurnPipeline.Length) ||
                continuation.PendingPostDispatchEventKind is not null ||
                ControlledEnemyAction is not null ||
                ControlledDrawRemaining is not null ||
                ControlledDrawCostRemaining is not null ||
                ControlledSemanticDrawRemaining is not null ||
                ControlledSemanticDrawRequested is not null ||
                ControlledSemanticBeforeHandDrawPending ||
                ControlledRecycleStratagemPending ||
                combat.IsPlayerTurn || combat.PendingChoice is not null ||
                combat.PendingKnowledgeDemonChoice is not null ||
                CanonicalJson.Sha256(baseline) !=
                    CanonicalJson.Sha256(checkpoint))
                throw new InvalidOperationException(
                    "Controlled turn-intent checkpoint disagrees with the automatic pipeline.");
        }

        if (ControlledEnemyAction is not { } enemyAction) return;
        enemyAction.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
        if (continuation.NextStepIndex != enemyAction.StepIndex ||
            combat.IsPlayerTurn || combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            CanonicalJson.Sha256(enemyAction.Checkpoint.World?.Combat) !=
                CanonicalJson.Sha256(combat with
                {
                    AutomaticPipelineContinuation = null
                }))
            throw new InvalidOperationException(
                "Controlled enemy-action checkpoint disagrees with combat state.");
    }

}
