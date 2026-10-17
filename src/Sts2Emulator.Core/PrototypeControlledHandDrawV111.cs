namespace Sts2Emulator.Core;

/// <summary>Continuation helpers for the legacy controlled DrawPlayerHand
/// stage. Shuffle tickets, Stratagem's player selection, drawn-card costs and
/// subsequent recycle tickets all resume the same saved pipeline cursor.</summary>
public sealed partial class PrototypeGameEngine
{
    private static RunState BeginControlledRecycleStratagemSelection(
        RunState state, PlayerState player, CombatState combat, int remaining)
    {
        if (combat.SemanticDeck is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle.stratagem " +
                "operation=legacy_recycle_with_provider_deck");
        if (!ControlledChanceGuardV111.Active || remaining <= 0 ||
            combat.AutomaticPipelineContinuation is not { } cursor ||
            cursor.Mechanics.CurrentStepKind !=
                PrototypeAutomaticStepKind.DrawPlayerHand ||
            !combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.stratagem" && power.Stacks > 0) ||
            combat.Hand.Length >= 10 || combat.DrawPile.Length == 0 ||
            combat.PendingChoice is not null)
            throw new InvalidDataException(
                "Controlled recycle Stratagem checkpoint is inconsistent.");
        var world = RequireWorld(state);
        var prompt = ResolveOperations(
            player, combat,
            new Queue<PrototypeQueuedOperation>([
                StratagemChoiceOperation(combat)]),
            state.Rng, moveSourceCardOnCompletion: false);
        if (prompt.Combat.PendingChoice is not
            { ChoiceId: "select_cards", Selection.SourceZone: PrototypeCardZone.DrawPile }
            selection)
            throw new InvalidDataException(
                "Stratagem did not create its expected draw-pile selection.");
        var suspendedCombat = prompt.Combat with
        {
            AutomaticPipelineContinuation = cursor with
            {
                ControlledDrawRemaining = remaining,
                ControlledDrawCostRemaining = null,
                ControlledRecycleStratagemPending = true
            }
        };
        var suspended = state with
        {
            Player = prompt.Player,
            World = world with { Combat = suspendedCombat }
        };
        PrototypeStateInvariants.Validate(suspended);
        return suspended;
    }

    internal static RunState ResumeControlledRecycleStratagemSelection(
        RunState state)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled recycle selection requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidOperationException(
            "Controlled recycle selection has no combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (combat.PendingChoice is not null || cursor is not
            { ControlledRecycleStratagemPending: true,
              ControlledDrawRemaining: > 0 } ||
            cursor.Mechanics.CurrentStepKind !=
                PrototypeAutomaticStepKind.DrawPlayerHand ||
            combat.SemanticDeck is not null)
            throw new InvalidDataException(
                "Saved recycle Stratagem selection is not source-reachable.");

        var remaining = cursor.ControlledDrawRemaining!.Value;
        combat = combat with
        {
            AutomaticPipelineContinuation = cursor with
            {
                ControlledDrawRemaining = null,
                ControlledRecycleStratagemPending = false
            }
        };
        var drawn = DrawCards(
            state.Player, combat, remaining, state.Rng,
            fromHandDraw: true,
            controlledRecycleSuspension: true,
            controlledDrawCostSuspension: true);
        return ContinueControlledHandDraw(state, drawn);
    }

    private static RunState ContinueControlledHandDraw(
        RunState state, PrototypeDrawCardsResult drawn)
    {
        var world = RequireWorld(state);
        var combat = drawn.Combat;
        var cursor = combat.AutomaticPipelineContinuation ??
            throw new InvalidDataException(
                "Controlled hand draw lost its automatic pipeline cursor.");
        if (cursor.Mechanics.CurrentStepKind !=
                PrototypeAutomaticStepKind.DrawPlayerHand ||
            drawn.RemainingCount < 0)
            throw new InvalidDataException(
                "Controlled hand draw has an invalid stage or remaining count.");
        var drawBoundary = drawn.BoundaryKind;

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            if (drawn.RemainingCount <= 0 ||
                combat.PendingChoice is not null ||
                !combat.PlayerPowers.Any(power =>
                    power.PowerId == "proto.power.stratagem" &&
                    power.Stacks > 0) || combat.Hand.Length >= 10)
                throw new InvalidDataException(
                    "Controlled hand draw has a malformed Stratagem boundary.");
            return BeginControlledRecycleStratagemSelection(
                state, drawn.Player, combat, drawn.RemainingCount);
        }

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.ControlledDrawCost)
        {
            if (combat.PendingChoice?.ControlledDrawCost is null)
                throw new InvalidDataException(
                    "Controlled hand draw lost its pending cost ticket.");
            return state with
            {
                Player = drawn.Player,
                World = world with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation = cursor with
                        {
                            ControlledDrawRemaining = null,
                            ControlledDrawCostRemaining = drawn.RemainingCount,
                            ControlledRecycleStratagemPending = false
                        }
                    }
                }
            };
        }

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.ControlledRecycle)
        {
            if (drawn.RemainingCount <= 0 ||
                combat.DrawPile.Length != 0 || combat.DiscardPile.Length == 0)
                throw new InvalidDataException(
                    "Controlled hand draw lost its recycle support.");
            return state with
            {
                Player = drawn.Player,
                World = world with
                {
                    Combat = combat with
                    {
                        AutomaticPipelineContinuation = cursor with
                        {
                            ControlledDrawRemaining = drawn.RemainingCount,
                            ControlledDrawCostRemaining = null,
                            ControlledRecycleStratagemPending = false
                        }
                    }
                }
            };
        }

        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            drawBoundary is PrototypeDrawCardsBoundaryKind.SemanticDraw or
                PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw.recycle " +
                "operation=nested_draw_continuation");

        var advanced = state with
        {
            Player = drawn.Player,
            World = world with { Combat = combat }
        };
        return ResumeAutomaticPipelineContinuation(
            CompleteAutomaticHandDrawStage(advanced));
    }

    /// <summary>
    /// Apply the source DrawPlayerHand post-stage cleanup and advance its
    /// ordinary pipeline cursor. Controlled and semantic providers share
    /// this mechanics boundary before the common pipeline resumes.
    /// </summary>
    internal static RunState CompleteAutomaticHandDrawStage(RunState state)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled hand-draw completion requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Automatic hand-draw completion has no combat.");
        var cursor = combat.AutomaticPipelineContinuation ??
            throw new InvalidDataException(
                "Automatic hand-draw completion lost its pipeline cursor.");
        if (!cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null ||
            cursor.ControlledSemanticBeforeHandDrawPending ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Automatic hand-draw completion is not at a clean boundary.");

        combat = RemovePlayerPowers(
            combat, definition => definition.RemoveAfterHandDraw);
        combat = DecrementPlayerPowers(
            combat, definition => definition.DecrementAfterHandDraw);
        combat = combat with
        {
            AutomaticPipelineContinuation = cursor with
            {
                NextStepIndex = cursor.NextStepIndex + 1,
                ControlledDrawRemaining = null,
                ControlledDrawCostRemaining = null,
                ControlledSemanticDrawRemaining = null,
                ControlledSemanticDrawRequested = null,
                ControlledSemanticBeforeHandDrawPending = false,
                ControlledRecycleStratagemPending = false
            }
        };
        return state with { World = world with { Combat = combat } };
    }
}
