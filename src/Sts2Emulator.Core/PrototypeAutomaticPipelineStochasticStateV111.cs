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
}
