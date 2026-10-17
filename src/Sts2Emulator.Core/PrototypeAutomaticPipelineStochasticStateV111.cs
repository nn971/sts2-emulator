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
    PrototypeControlledEnemyActionContinuationV111? ControlledEnemyAction = null)
{
    public PrototypeAutomaticPipelineStochasticStateV111 Fork() => this with
    {
        ControlledEnemyAction = ControlledEnemyAction?.Fork()
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
