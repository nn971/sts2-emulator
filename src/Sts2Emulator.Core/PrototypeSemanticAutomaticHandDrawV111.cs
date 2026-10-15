namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in semantic continuation at the genuine DrawPlayerHand automatic
/// pipeline step. A source checkpoint is required: earlier turn stages
/// (including intents) must already have executed, and they are not replayed.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static int SemanticHandDrawStepIndex =>
        Array.FindIndex(PrototypeContent.Rules.EndTurnPipeline,
            step => step.Kind == PrototypeAutomaticStepKind.DrawPlayerHand);

    internal static bool IsSemanticAutomaticHandDraw(CombatState combat)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        return cursor?.ControlledSemanticDrawRemaining is not null &&
            cursor.ControlledSemanticDrawRequested is > 0 &&
            cursor.NextStepIndex == SemanticHandDrawStepIndex &&
            cursor.ControlledDrawRemaining is null &&
            cursor.ControlledDrawCostRemaining is null &&
            cursor.OpeningStage is null &&
            cursor.PendingPostDispatchEventKind is null;
    }

    internal static (RunState Run, int Requested, int Remaining)
        BeginSemanticAutomaticHandDraw(RunState run)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic hand draw requires controlled chance guard.");
        var combat = RequireWorld(run).Combat ??
            throw new InvalidOperationException("Missing automatic hand-draw combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (combat.SemanticDeck is null ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cursor?.NextStepIndex != SemanticHandDrawStepIndex ||
            cursor.ControlledSemanticDrawRemaining is not null ||
            cursor.ControlledDrawRemaining is not null ||
            cursor.ControlledDrawCostRemaining is not null ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null)
            throw new InvalidOperationException(
                "Expected an existing DrawPlayerHand pipeline checkpoint.");
        var result = ResolveAutomaticStep(
            run, PrototypeContent.Rules.EndTurnPipeline[SemanticHandDrawStepIndex]);
        var after = RequireWorld(result).Combat ??
            throw new InvalidOperationException("Hand draw removed combat.");
        // A source-committed opening order may supply the complete
        // requested hand without a Nature ticket. This is a valid,
        // genuinely deterministic automatic stage, not an unsupported
        // interruption. Preserve original hooks and advance the cursor
        // exactly once, as the surrounding ResumeAutomaticPipeline would.
        if (!IsSemanticAutomaticHandDraw(after) &&
            after.AutomaticPipelineContinuation?.NextStepIndex ==
                SemanticHandDrawStepIndex &&
            after.SemanticDeck is { Pending: null } &&
            after.PendingChoice is null &&
            after.PendingKnowledgeDemonChoice is null)
        {
            var drawn = after.CounterState.CardsDrawnThisCombat -
                combat.CounterState.CardsDrawnThisCombat;
            if (drawn <= 0)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                    "operation=zero_card_uninterrupted_stage");
            after = after with
            {
                AutomaticPipelineContinuation =
                    after.AutomaticPipelineContinuation with
                    {
                        NextStepIndex = SemanticHandDrawStepIndex + 1
                    }
            };
            return (result with
            {
                World = RequireWorld(result) with { Combat = after }
            }, drawn, 0);
        }
        if (!IsSemanticAutomaticHandDraw(after) ||
            (after.SemanticDeck?.Pending is null &&
             after.PendingChoice?.ControlledDrawCost is null))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.DrawPlayerHand " +
                "operation=unsupported_nested_stage");
        var saved = after.AutomaticPipelineContinuation!;
        return (result, saved.ControlledSemanticDrawRequested!.Value,
            saved.ControlledSemanticDrawRemaining!.Value);
    }

    internal static (RunState Run, int Remaining)
        CommitSemanticAutomaticHandDrawProgress(
            RunState run, int remaining, bool hasPending)
    {
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing semantic hand draw.");
        if (!IsSemanticAutomaticHandDraw(combat) ||
            (combat.SemanticDeck?.Pending is not null &&
             combat.PendingChoice?.ControlledDrawCost is not null) ||
            remaining < 0)
            throw new InvalidDataException(
                "Semantic automatic draw cursor or pending stage diverged.");
        var cursor = combat.AutomaticPipelineContinuation!;
        if (hasPending)
        {
            if (combat.SemanticDeck?.Pending is null &&
                combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException(
                    "Missing next semantic hand-draw choice.");
            return (run with
            {
                World = world with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation = cursor with
                        {
                            ControlledSemanticDrawRemaining = remaining
                        }
                    }
                }
            }, remaining);
        }

        if (combat.SemanticDeck?.Pending is not null ||
            combat.PendingChoice is not null)
            throw new InvalidDataException(
                "Cannot finish hand draw with an unresolved choice.");
        // Exact post-DrawPlayerHand cleanup from ResolveAutomaticStep.
        combat = RemovePlayerPowers(
            combat, definition => definition.RemoveAfterHandDraw);
        combat = DecrementPlayerPowers(
            combat, definition => definition.DecrementAfterHandDraw);
        combat = combat with
        {
            AutomaticPipelineContinuation = cursor with
            {
                NextStepIndex = SemanticHandDrawStepIndex + 1,
                ControlledSemanticDrawRemaining = null,
                ControlledSemanticDrawRequested = null
            }
        };
        // The remaining automatic steps can be resumed by the future
        // full-action Nature controller. This segment ends after the
        // authentic hand-draw stage, not at a fabricated player turn.
        return (run with { World = world with { Combat = combat } }, 0);
    }

    /// <summary>Advance the remaining authentic automatic stages after
    /// semantic DrawPlayerHand cleanup. This yields an actual next-player
    /// boundary when no subsequent unsupported stochastic site fires.</summary>
    internal static RunState ResumeSemanticAutomaticAfterHandDraw(RunState run)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic post-hand stage requires controlled chance guard.");
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("Missing post-hand combat.");
        var cursor = combat.AutomaticPipelineContinuation ??
            throw new InvalidOperationException("Missing saved post-hand cursor.");
        if (cursor.NextStepIndex != SemanticHandDrawStepIndex + 1 ||
            cursor.ControlledSemanticDrawRequested is not null ||
            cursor.ControlledSemanticDrawRemaining is not null ||
            cursor.ControlledDrawRemaining is not null ||
            cursor.ControlledDrawCostRemaining is not null ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null ||
            combat.SemanticDeck is not { Pending: null } ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidDataException(
                "Semantic post-hand continuation is not at a clean boundary.");

        var continued = ResumeAutomaticPipeline(run, cursor.NextStepIndex);
        if (continued.Phase == RunPhase.Combat)
        {
            var result = RequireWorld(continued).Combat ??
                throw new InvalidDataException("Post-hand combat disappeared.");
            if (!result.IsPlayerTurn ||
                result.AutomaticPipelineContinuation is not null ||
                result.PendingChoice is not null ||
                result.PendingKnowledgeDemonChoice is not null ||
                result.SemanticDeck is not { Pending: null })
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.post_hand_draw " +
                    "operation=nested_automatic_choice");
        }
        return continued;
    }
}
