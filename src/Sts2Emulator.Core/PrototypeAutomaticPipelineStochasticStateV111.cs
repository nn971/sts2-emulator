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

        if (continuation.OpeningStage ==
                PrototypeOpeningCombatStage.InitialEnemyIntents &&
            ControlledTurnIntents is null)
            throw new InvalidOperationException(
                "Invalid suspended initial-enemy-intent stage.");

        if (ControlledRecycleStratagemPending &&
            (!continuation.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand) ||
             ControlledDrawRemaining is not > 0 ||
             ControlledDrawCostRemaining is not null ||
             combat.PendingChoice is not
                { ChoiceId: "select_cards",
                  Selection.SourceZone: PrototypeCardZone.DrawPile } ||
             combat.SemanticDeck is not null))
            throw new InvalidOperationException(
                "Controlled recycle Stratagem selection is malformed.");

        ValidateSemanticHandDrawCursor(combat, continuation);

        if (ControlledTurnIntents is { } turnIntents)
        {
            turnIntents.Validate();
            bool initialIntentStage = continuation.OpeningStage ==
                PrototypeOpeningCombatStage.InitialEnemyIntents;
            var baseline = turnIntents.BaselineCombat with
            {
                AutomaticPipelineContinuation = null
            };
            var checkpoint = combat with
            {
                AutomaticPipelineContinuation = null
            };
            if (turnIntents.SiteId != (initialIntentStage
                    ? NativeCombatRngEntryV111.CommittedIntentSiteId
                    : ControlledTurnIntentEngineV111.SiteId) ||
                (!initialIntentStage &&
                 !continuation.Mechanics.IsAtStep(
                    PrototypeAutomaticStepKind.AdvanceTurn)) ||
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

    private void ValidateSemanticHandDrawCursor(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        bool hasCursor = ControlledSemanticDrawRemaining is not null ||
            ControlledSemanticDrawRequested is not null ||
            ControlledSemanticBeforeHandDrawPending;
        if (!hasCursor) return;

        if (!continuation.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            ControlledDrawRemaining is not null ||
            ControlledDrawCostRemaining is not null ||
            ControlledEnemyAction is not null ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidOperationException(
                "Semantic hand-draw cursor disagrees with the automatic pipeline.");

        if (ControlledSemanticBeforeHandDrawPending)
        {
            bool pendingBeforeDrawChoice =
                combat.PendingChoice?.ControlledRandomPileCard is not null ||
                combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true;
            if (ControlledSemanticDrawRemaining is not null ||
                ControlledSemanticDrawRequested is not null ||
                combat.SemanticDeck?.Pending is not null ||
                !pendingBeforeDrawChoice)
                throw new InvalidOperationException(
                    "Semantic BeforeHandDraw cursor has no matching choice.");
            return;
        }

        if (ControlledSemanticDrawRemaining is not { } remaining ||
            remaining < 0 || ControlledSemanticDrawRequested is not > 0 ||
            !PrototypeGameEngine.IsSemanticAutomaticHandDraw(combat))
            throw new InvalidOperationException(
                "Semantic automatic hand-draw counters are malformed.");
    }

}
