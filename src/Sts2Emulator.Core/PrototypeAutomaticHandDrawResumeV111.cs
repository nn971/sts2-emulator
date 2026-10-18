namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics for resuming the remaining cards in an automatic
/// DrawPlayerHand segment. Provider adapters choose which tagged boundaries
/// they can suspend and interpret the next result.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    // The provider adapters supply their suspension policy and continue the
    // tagged result through their own saved frame.
    private static (RunState Run, PrototypeDrawCardsResult? Drawn)
        ResumeAutomaticHandDrawAfterCost(
            RunState checkpoint,
            PrototypeControlledDrawCostV111 cost,
            int selectedCost,
            PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
            bool allowHookSuspension,
            string unsupportedBoundaryMessage)
    {
        var costProgress = ResolveDrawCostAndDispatchCardDrawn(
            checkpoint, cost, selectedCost, allowHookSuspension);
        if (costProgress.CostStagePending)
            return (costProgress.Run, null);

        var world = RequireWorld(costProgress.Run);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Draw-cost resume removed combat.");
        if (costProgress.DrawnHookSuspended &&
            combat.PendingChoice is not null &&
            combat.PendingKnowledgeDemonChoice is null)
            return (costProgress.Run, new PrototypeDrawCardsResult(
                costProgress.Run.Player, combat, [],
                PrototypeDrawCardsBoundaryV111.NestedCardDrawnChoice(
                    cost.RemainingCount)));
        if (costProgress.DrawnHookSuspended ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(unsupportedBoundaryMessage);

        return (costProgress.Run, ResumeAutomaticHandDrawCards(
            costProgress.Run, cost.RemainingCount, suspensionPolicy,
            eventDepth: cost.EventDepth));
    }

    private static PrototypeDrawCardsResult ResumeAutomaticHandDrawCards(
        RunState checkpoint,
        int remaining,
        PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
        int eventDepth = 0)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Automatic hand-draw resumption requires the RNG guard.");
        if (remaining < 0 || eventDepth < 0)
            throw new ArgumentOutOfRangeException(
                remaining < 0 ? nameof(remaining) : nameof(eventDepth));

        var world = RequireWorld(checkpoint);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Automatic hand-draw resumption has no combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor is null ||
            !cursor.Stochastic.TryGetAutomaticHandDrawRemainder(
                cursor.Mechanics, out int savedRemaining) ||
            savedRemaining != remaining)
            throw new InvalidDataException(
                "Automatic hand-draw resume cursor is inconsistent.");

        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Automatic hand-draw resume boundary is inconsistent.");

        combat = combat with
        {
            AutomaticPipelineContinuation = cursor.WithHandDrawProgress(null)
        };
        return DrawCards(
            checkpoint.Player, combat, remaining, checkpoint.Rng,
            fromHandDraw: true, eventDepth: eventDepth,
            suspensionPolicy: suspensionPolicy);
    }
}
