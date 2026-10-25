namespace Sts2Emulator.Core;

/// <summary>Continuation helpers for the legacy controlled DrawPlayerHand
/// stage. Shuffle tickets, Stratagem's player selection, drawn-card costs and
/// subsequent recycle tickets all resume the same saved pipeline cursor.</summary>
public sealed partial class PrototypeGameEngine
{
    private static PrototypeDrawCardsResumeResultV111
        ContinueInitialControlledHandDrawBoundary(
        PrototypeDrawCardsSegmentV111 segment,
        int? requestedCount)
    {
        var state = segment.Run;
        var drawn = segment.Drawn;
        bool nestedHookChoice = drawn.BoundaryKind ==
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice &&
            drawn.Combat.Stochastic.SemanticDeck is null &&
            drawn.Combat.PendingChoice is not null;
        bool stratagemSelection = drawn.BoundaryKind ==
            PrototypeDrawCardsBoundaryKind.ShuffleSelection &&
            drawn.Combat.Stochastic.SemanticDeck is null &&
            drawn.Combat.PendingChoice is null;
        if (!ControlledChanceGuardV111.Active ||
            requestedCount is not > 0 ||
            (drawn.BoundaryKind is not (
                PrototypeDrawCardsBoundaryKind.ControlledDrawCost or
                PrototypeDrawCardsBoundaryKind.ControlledRecycle) &&
             !nestedHookChoice && !stratagemSelection))
            throw new InvalidOperationException(
                "Initial controlled hand draw is not at a resumable legacy boundary.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Initial controlled hand draw has no combat.");
        if (combat.AutomaticPipelineContinuation is not null ||
            combat.Stochastic.SemanticDeck is not null)
            throw new InvalidDataException(
                "Initial controlled hand draw boundary has an incompatible owner.");
        var checkpoint = CommitAutomaticHandDrawProgress(
            state, drawn.Player, drawn.Combat, progress: null,
            createCheckpoint: true);
        var suspendedCombat = checkpoint.World!.Combat!;
        return ContinueControlledHandDraw(
            segment with
            {
                Run = checkpoint,
                Drawn = drawn with { Combat = suspendedCombat }
            }, requestedCount);
    }

    internal static RunState ResumeControlledAutomaticHandDrawHookChoice(
        RunState state) =>
            ResumeControlledAutomaticHandDrawHookChoiceWithProgress(state)
                .Run;

    internal static PrototypeDrawCardsResumeResultV111
        ResumeControlledAutomaticHandDrawHookChoiceWithProgress(
        RunState state)
    {
        if (!ControlledChanceGuardV111.Active ||
            ControlledChanceGuardV111.EngineOwnedNativeProviderActive)
            throw new InvalidOperationException(
                "Controlled automatic hand-draw hook resumption requires the controlled provider.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Controlled automatic hand-draw hook has no combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (state.Phase != RunPhase.Combat ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.Stochastic.SemanticDeck is not null || cursor is null ||
            !cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                cursor.Mechanics, out var progress) ||
            progress.Kind != PrototypeAutomaticHandDrawProgressKindV111
                .ControlledDrawHookChoice)
            throw new InvalidDataException(
                "Controlled automatic hand-draw hook cursor is inconsistent.");

        int remaining = progress.RemainingCount!.Value;
        var segment = ResumeAutomaticHandDrawSegment(
            state, remaining,
            PrototypeDrawCardsSuspensionPolicyV111
                .ControlledLegacyAutomaticHandDraw(
                    combat, cardDrawnChoice: true));
        return ResumeSavedControlledAutomaticHandDraw(segment);
    }

    private static RunState BeginControlledRecycleStratagemSelection(
        RunState state, PlayerState player, CombatState combat, int remaining,
        int? requestedCount)
    {
        if (combat.Stochastic.SemanticDeck is not null)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.recycle.stratagem " +
                "operation=legacy_recycle_with_provider_deck");
        if (!ControlledChanceGuardV111.Active || remaining <= 0 ||
            combat.AutomaticPipelineContinuation is not { } cursor ||
            cursor.Mechanics.CurrentStepKind !=
                PrototypeAutomaticStepKind.DrawPlayerHand ||
            ExpectedStratagemSelectionCount(combat) <= 0 ||
            combat.PendingChoice is not null)
            throw new InvalidDataException(
                "Controlled recycle Stratagem checkpoint is inconsistent.");
        var world = RequireWorld(state);
        var prototypeRng = state.Stochastic.PrototypeRng;
        var prompt = ResolveOperations(
            player, combat,
            new Queue<PrototypeQueuedOperation>([
                StratagemChoiceOperation(combat)]),
            prototypeRng, moveSourceCardOnCompletion: false);
        if (prompt.Combat.PendingChoice is not
            { ChoiceId: "select_cards", Selection.SourceZone: PrototypeCardZone.DrawPile }
            selection)
            throw new InvalidDataException(
                "Stratagem did not create its expected draw-pile selection.");
        return CommitAutomaticHandDrawStratagemSelection(
            state, prompt.Player, prompt.Combat, remaining,
            semanticRequested: null,
            controlledRequested: requestedCount,
            prototypeRng: prototypeRng);
    }

    internal static RunState ResumeControlledRecycleStratagemSelection(
        RunState state) =>
            ResumeControlledRecycleStratagemSelectionWithProgress(state).Run;

    internal static PrototypeDrawCardsResumeResultV111
        ResumeControlledRecycleStratagemSelectionWithProgress(
        RunState state)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Controlled recycle selection requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidOperationException(
            "Controlled recycle selection has no combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (combat.PendingChoice is not null || cursor is null ||
            !cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                cursor.Mechanics, out var progress) ||
            progress.Kind != PrototypeAutomaticHandDrawProgressKindV111
                .ControlledRecycleStratagem ||
            progress.RemainingCount is not > 0 ||
            combat.Stochastic.SemanticDeck is not null)
            throw new InvalidDataException(
                "Saved recycle Stratagem selection is not source-reachable.");

        var remaining = progress.RemainingCount!.Value;
        var segment = ResumeAutomaticHandDrawSegment(
            state, remaining,
            PrototypeDrawCardsSuspensionPolicyV111
                .ControlledLegacyAutomaticHandDraw(combat));
        return ResumeSavedControlledAutomaticHandDraw(segment);
    }

    private static PrototypeDrawCardsResumeResultV111
        ContinueControlledHandDraw(
        PrototypeDrawCardsSegmentV111 segment,
        int? requestedCount = null)
    {
        var state = segment.Run;
        var drawn = segment.Drawn;
        var combat = drawn.Combat;
        var drawBoundary = drawn.BoundaryKind;
        requestedCount = ResolveAutomaticHandDrawRequestedCount(
            combat, requestedCount);

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            return new PrototypeDrawCardsResumeResultV111(
                BeginControlledRecycleStratagemSelection(
                    state, drawn.Player, combat, drawn.RemainingCount,
                    requestedCount),
                drawn.RemainingCount, requestedCount);
        }

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.ControlledDrawCost)
        {
            return new PrototypeDrawCardsResumeResultV111(
                CommitAutomaticHandDrawProgress(
                    state, drawn.Player, combat,
                    PrototypeAutomaticHandDrawProgressV111
                        .ControlledDrawCost(
                            drawn.RemainingCount, requestedCount)),
                drawn.RemainingCount, requestedCount);
        }

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.ControlledRecycle)
        {
            return new PrototypeDrawCardsResumeResultV111(
                CommitAutomaticHandDrawProgress(
                    state, drawn.Player, combat,
                    PrototypeAutomaticHandDrawProgressV111
                        .ControlledDraw(
                            drawn.RemainingCount, requestedCount)),
                drawn.RemainingCount, requestedCount);
        }

        if (drawBoundary ==
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice)
        {
            return new PrototypeDrawCardsResumeResultV111(
                SuspendAutomaticHandDrawAtCardDrawnChoice(
                    state, drawn.Player, combat, drawn.RemainingCount,
                    semanticRequested: null,
                    controlledRequested: requestedCount),
                drawn.RemainingCount, requestedCount);
        }

        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            drawBoundary is PrototypeDrawCardsBoundaryKind.SemanticDraw or
                PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions)
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=combat.draw.recycle " +
                "operation=nested_draw_continuation");

        var advanced = PublishDrawCardsResult(segment);
        var pipelineResume = ResumeAutomaticPipelineContinuation(
            CompleteAutomaticHandDrawStage(advanced));
        return new PrototypeDrawCardsResumeResultV111(
            pipelineResume.Run, 0, requestedCount,
            PipelineBoundary: pipelineResume.Boundary);
    }

}
