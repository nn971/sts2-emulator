namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics for resuming the remaining cards in an automatic
/// DrawPlayerHand segment. Provider adapters choose which tagged boundaries
/// they can suspend and interpret the next result.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static int AutomaticHandDrawStepIndex =>
        Array.FindIndex(PrototypeContent.Rules.EndTurnPipeline,
            step => step.Kind == PrototypeAutomaticStepKind.DrawPlayerHand);

    /// <summary>
    /// Project tagged semantic progress from the owner of the active
    /// DrawPlayerHand cursor. Draw facades use this shared query instead of
    /// re-reading the continuation fields themselves.
    /// </summary>
    internal static bool TryGetSemanticAutomaticHandDrawProgress(
        CombatState combat, out int requested, out int remaining)
    {
        ArgumentNullException.ThrowIfNull(combat);
        requested = 0;
        remaining = 0;
        var cursor = combat.AutomaticPipelineContinuation;
        return cursor is not null &&
            cursor.Stochastic.TryGetSemanticHandDrawProgress(
                cursor.Mechanics, out requested, out remaining);
    }

    /// <summary>Check whether the shared DrawPlayerHand cursor carries
    /// semantic requested/remaining progress.</summary>
    internal static bool HasSemanticAutomaticHandDrawProgress(
        CombatState combat) =>
        TryGetSemanticAutomaticHandDrawProgress(
            combat, out _, out _);

    /// <summary>
    /// Reconcile the caller's request count with active pipeline-owned
    /// hand-draw progress. Saved older snapshots may omit the requested count,
    /// in which case the caller's value remains authoritative.
    /// </summary>
    private static int? ResolveAutomaticHandDrawRequestedCount(
        CombatState combat, int? requestedCount)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor is null)
        {
            if (requestedCount is <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedCount));
            return requestedCount;
        }
        return cursor.Stochastic.ResolveAutomaticHandDrawRequestedCount(
            cursor.Mechanics, requestedCount);
    }

    /// <summary>
    /// Commit provider progress at the ordinary DrawPlayerHand checkpoint.
    /// This is the shared write boundary for legacy and semantic adapters:
    /// the selected progress and the latest player/combat mechanics become
    /// visible together, while the provider keeps ownership of resumption.
    /// </summary>
    internal static RunState CommitAutomaticHandDrawProgress(
        RunState state,
        PlayerState player,
        CombatState combat,
        PrototypeAutomaticHandDrawProgressV111? progress,
        bool createCheckpoint = false,
        RngBundle? prototypeRng = null)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Automatic hand-draw progress requires the RNG guard.");
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combat);
        progress?.Validate();

        int stageIndex = AutomaticHandDrawStepIndex;
        if (stageIndex < 0)
            throw new InvalidDataException(
                "DrawPlayerHand has no automatic pipeline index.");
        var cursor = combat.AutomaticPipelineContinuation;
        if (cursor is null)
        {
            if (!createCheckpoint)
                throw new InvalidDataException(
                    "Automatic hand-draw progress has no pipeline cursor.");
            cursor = new PrototypeAutomaticPipelineContinuationState(
                stageIndex);
        }
        if (!cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand))
            throw new InvalidDataException(
                "Automatic hand-draw progress is outside its pipeline checkpoint.");

        var updatedCombat = AttachValidatedAutomaticPipelineContinuation(
            combat, cursor.WithHandDrawProgress(progress));
        return prototypeRng is null
            ? PublishDrawMechanicsState(state, player, updatedCombat)
            : PublishDrawMechanicsAndPrototypeRng(
                state, player, updatedCombat, prototypeRng);
    }

    /// <summary>
    /// Apply DrawPlayerHand post-stage cleanup and advance its pipeline cursor
    /// for either provider before the common pipeline resumes.
    /// </summary>
    internal static RunState CompleteAutomaticHandDrawStage(
        RunState state, int? completedSemanticDrawRequested = null)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Automatic hand-draw completion requires the RNG guard.");
        var world = RequireWorld(state);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Automatic hand-draw completion has no combat.");
        var cursor = combat.AutomaticPipelineContinuation ??
            throw new InvalidDataException(
                "Automatic hand-draw completion lost its pipeline cursor.");
        if (completedSemanticDrawRequested is < 0 ||
            !cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            cursor.Stochastic.IsAtSemanticBeforeHandDraw(cursor.Mechanics) ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.Stochastic.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Automatic hand-draw completion is not at a clean boundary.");

        var completedMechanics = cursor.Mechanics.AdvancePastStep(
            PrototypeAutomaticStepKind.DrawPlayerHand);
        combat = ApplyAutomaticHandDrawPostStagePowerTiming(combat);
        var completedCursor = cursor.WithHandDrawProgress(null);
        completedCursor = completedCursor with
        {
            Mechanics = completedMechanics
        };
        combat = completedSemanticDrawRequested is { } requested
            ? AttachCompletedSemanticHandDrawReceipt(
                combat, completedCursor, requested)
            : AttachValidatedAutomaticPipelineContinuation(
                combat, completedCursor);
        return PublishDrawMechanicsState(state, state.Player, combat);
    }

    /// <summary>
    /// Publish the completed semantic hand-draw receipt state at the already
    /// advanced DrawPlayerHand cursor for adapters that resume after the
    /// source step has advanced.
    /// </summary>
    internal static CombatState AttachCompletedSemanticHandDrawReceipt(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState cursor,
        int? requestedCount)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic hand-draw receipt publication requires the RNG guard.");
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(cursor);
        var completedCursor = cursor.WithHandDrawProgress(null)
            .WithCompletedSemanticHandDrawReceipt(requestedCount);
        return AttachValidatedAutomaticPipelineContinuation(
            combat, completedCursor);
    }

    private static CombatState ApplyAutomaticHandDrawPostStagePowerTiming(
        CombatState combat)
    {
        combat = RemovePlayerPowers(
            combat, definition => definition.RemoveAfterHandDraw);
        return DecrementPlayerPowers(
            combat, definition => definition.DecrementAfterHandDraw);
    }

    private enum AutomaticHandDrawSourceResultContextV111
    {
        OrdinaryPipeline,
        DeferredSemanticStage,
        ResumedSemanticBeforeHandDraw
    }

    private enum AutomaticHandDrawResumeContextV111
    {
        SourceStageBoundary,
        SavedControlledStage,
        SavedSemanticStage
    }

    private static (RunState Run,
        AutomaticHandDrawSourceResultContextV111 ResultContext,
        bool DispatchBeforeHandDraw) PrepareAutomaticHandDrawStage(
        RunState state, RngBundle prototypeRng,
        AutomaticHandDrawSourceResultContextV111 requestedResultContext)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(requestedResultContext))
            throw new ArgumentOutOfRangeException(
                nameof(requestedResultContext));

        var combat = RequireWorld(state).Combat ??
            throw new InvalidDataException(
                "Automatic hand-draw stage has no combat.");
        var cursor = combat.AutomaticPipelineContinuation;
        bool resumedAfterSemanticHook = cursor is not null &&
            cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics);
        bool resumedAfterControlledHook = cursor is not null &&
            cursor.Stochastic.IsAtControlledBeforeHandDraw(
                cursor.Mechanics);
        if (resumedAfterSemanticHook && requestedResultContext !=
                AutomaticHandDrawSourceResultContextV111
                    .DeferredSemanticStage)
            throw new InvalidDataException(
                "Semantic BeforeHandDraw resume has no deferred stage context.");

        if (!resumedAfterSemanticHook && !resumedAfterControlledHook)
            return (state, requestedResultContext, true);

        var cleared = CommitAutomaticHandDrawProgress(
            state, state.Player, combat, progress: null,
            prototypeRng: prototypeRng);
        return (cleared,
            resumedAfterSemanticHook
                ? AutomaticHandDrawSourceResultContextV111
                    .ResumedSemanticBeforeHandDraw
                : requestedResultContext,
            false);
    }

    private static PrototypeAutomaticPipelineResumeResultV111?
        ResumeAutomaticHandDrawHookContinuation(
        RunState state, bool stopBeforeSemanticHandDraw)
    {
        var combat = state.World?.Combat;
        var cursor = combat?.AutomaticPipelineContinuation;
        if (combat is not
                { PendingChoice: null,
                  PendingKnowledgeDemonChoice: null } || cursor is null)
            return null;

        if (cursor.Stochastic.IsAtControlledBeforeHandDraw(
                cursor.Mechanics))
        {
            if (!ControlledChanceGuardV111.Active)
                throw new InvalidOperationException(
                    "Controlled BeforeHandDraw choice requires the RNG guard.");
            var resumed = ResumeAutomaticPipeline(
                state, cursor.Mechanics.RequireResumeStepIndex());
            return ResumeAutomaticPipelineContinuation(
                resumed,
                stopBeforeSemanticHandDraw: stopBeforeSemanticHandDraw);
        }

        if (cursor.Stochastic.IsAtSemanticBeforeHandDraw(
                cursor.Mechanics))
        {
            if (!ControlledChanceGuardV111.Active)
                throw new InvalidOperationException(
                    "Semantic BeforeHandDraw choice requires the RNG guard.");
            var resumed = ResumeSemanticBeforeHandDrawActionChoice(state).Run;
            return ResumeAutomaticPipelineContinuation(
                resumed,
                stopBeforeSemanticHandDraw: stopBeforeSemanticHandDraw);
        }

        if (cursor.Stochastic.IsAtControlledDrawHookChoice(
                cursor.Mechanics))
            return ResumeControlledAutomaticHandDrawHookChoiceWithProgress(
                state).ToAutomaticPipelineResumeResult();

        return null;
    }

    /// <summary>
    /// Keep the automatic pipeline at its current stage while a hand-draw
    /// continuation owns a pending count or pre-draw hook boundary.
    /// </summary>
    private static bool MustSuspendAutomaticPipelineAfterStep(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.PendingChoice?.ControlledRandomPileCard is not null)
            return true;

        var continuation = combat.AutomaticPipelineContinuation;
        if (continuation is null)
            return false;
        var mechanics = continuation.Mechanics;
        var stochastic = continuation.Stochastic;
        return (ControlledChanceGuardV111.Active &&
                stochastic.TryGetAutomaticHandDrawRemainder(
                    mechanics, out _)) ||
            stochastic.IsAtSemanticBeforeHandDraw(mechanics) ||
            stochastic.IsAtControlledBeforeHandDraw(mechanics);
    }

    private static bool InspectAutomaticHandDrawProgressPosition(
        PrototypeAutomaticPipelineContinuationState? cursor)
    {
        if (cursor is null)
            return false;
        return cursor.Stochastic.ValidateHandDrawProgressPosition(
            cursor.Mechanics);
    }

    /// <summary>
    /// Routes a completed DrawPlayerHand call to the matching continuation
    /// owner. Source-stage calls route only suspended boundaries; saved-stage
    /// resumes also route normal completion through the same provider adapter.
    /// </summary>
    private static PrototypeDrawCardsResumeResultV111?
        ResumeAutomaticHandDrawBoundary(
            PrototypeDrawCardsSegmentV111 segment,
            int requestedCount,
            AutomaticHandDrawResumeContextV111 context,
            PrototypeControlledSemanticDrawV111? semanticDraw = null,
            string recycleSite = "combat.DrawPlayerHand",
            string recycleOperation = "unsupported_nested_stage")
    {
        var rngCheckpoint = segment.Run;
        var drawn = segment.Drawn;
        ArgumentNullException.ThrowIfNull(rngCheckpoint);
        ArgumentNullException.ThrowIfNull(drawn);
        if (requestedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(requestedCount));
        if ((context == AutomaticHandDrawResumeContextV111.SavedSemanticStage) !=
            (semanticDraw is not null))
            throw new InvalidDataException(
                "Automatic hand-draw resume context and provider frame disagree.");

        var combat = drawn.Combat;
        var boundary = drawn.BoundaryKind;
        bool resumeSavedStage = context is
            AutomaticHandDrawResumeContextV111.SavedControlledStage or
            AutomaticHandDrawResumeContextV111.SavedSemanticStage;
        bool semanticChoiceBoundary = context ==
                AutomaticHandDrawResumeContextV111.SourceStageBoundary &&
            ControlledChanceGuardV111.Active &&
            combat.Stochastic.SemanticDeck is
                { Mode: SemanticDeckStateV111.NatureMode } &&
            boundary is
                (PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice or
                 PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions or
                 PrototypeDrawCardsBoundaryKind.ShuffleSelection);
        bool semanticDrawBoundary = context ==
                AutomaticHandDrawResumeContextV111.SourceStageBoundary &&
            combat.Stochastic.SemanticDeck is not null &&
            boundary is
                (PrototypeDrawCardsBoundaryKind.SemanticDraw or
                 PrototypeDrawCardsBoundaryKind.ControlledDrawCost);
        bool semanticSavedStage = context ==
            AutomaticHandDrawResumeContextV111.SavedSemanticStage;
        if (semanticChoiceBoundary || semanticDrawBoundary ||
            semanticSavedStage)
        {
            var world = RequireWorld(rngCheckpoint);
            var advanced = rngCheckpoint with
            {
                Player = drawn.Player,
                World = world with { Combat = combat }
            };
            var drawFrame = semanticDraw ?? CreateSemanticDrawResultFrame(
                drawn.RemainingCount, fromHandDraw: true,
                eventDepth: 0, automatic: true);
            return ResolveControlledSemanticDrawResult(
                advanced, drawn, drawFrame,
                continuation: null,
                automaticHandDrawRequested: requestedCount,
                recycleSite: recycleSite,
                recycleOperation: recycleOperation);
        }

        if (ControlledChanceGuardV111.Active &&
            combat.Stochastic.SemanticDeck is null &&
            (resumeSavedStage || boundary is
                (PrototypeDrawCardsBoundaryKind.ControlledDrawCost or
                 PrototypeDrawCardsBoundaryKind.ControlledRecycle or
                 PrototypeDrawCardsBoundaryKind.ShuffleSelection or
                 PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice)))
        {
            // The saved controlled-stage adapter uses zero to mean that the
            // original count is carried by progress rather than this boundary.
            int? controlledRequested = requestedCount > 0
                ? requestedCount : null;
            controlledRequested = ResolveAutomaticHandDrawRequestedCount(
                combat, controlledRequested);
            return combat.AutomaticPipelineContinuation is null
                ? ContinueInitialControlledHandDrawBoundary(
                    rngCheckpoint, drawn, controlledRequested)
                : ContinueControlledHandDraw(
                    rngCheckpoint, drawn, controlledRequested);
        }

        return null;
    }

    /// <summary>
    /// Own every result from the source DrawPlayerHand operation: provider
    /// suspension routing, ordinary Stratagem resolution, deferred stage
    /// completion, and post-draw power timing.
    /// </summary>
    private static RunState ResumeAutomaticHandDrawSourceResult(
        PrototypeDrawCardsSegmentV111 segment,
        int requestedCount,
        AutomaticHandDrawSourceResultContextV111 context)
    {
        var checkpoint = segment.Run;
        var drawn = segment.Drawn;
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(drawn);
        if (requestedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(requestedCount));
        if (!Enum.IsDefined(context))
            throw new ArgumentOutOfRangeException(nameof(context));
        if (context !=
                AutomaticHandDrawSourceResultContextV111.OrdinaryPipeline &&
            (!ControlledChanceGuardV111.Active ||
             drawn.Combat.Stochastic.SemanticDeck is null))
            throw new InvalidDataException(
                "Semantic hand-draw result context has no active provider.");

        var providerResume = ResumeAutomaticHandDrawBoundary(
            segment, requestedCount,
            AutomaticHandDrawResumeContextV111.SourceStageBoundary);
        if (providerResume is { } resumed)
            return resumed.Run;

        var result = PublishDrawCardsResult(checkpoint, drawn);
        if (drawn.BoundaryKind ==
            PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            var world = RequireWorld(result);
            var combat = world.Combat ?? throw new InvalidDataException(
                "Automatic Stratagem resume removed combat.");
            var pendingDraw =
                CreateHandDrawStratagemResumeOperations(
                    combat, drawn.RemainingCount);
            var prototypeRng = result.Stochastic.PrototypeRng;
            var continued = ResolveOperations(
                result.Player, combat, pendingDraw, prototypeRng,
                moveSourceCardOnCompletion: false);
            result = PublishDrawMechanicsAndPrototypeRng(
                result, continued.Player, continued.Combat, prototypeRng);
        }

        var resultWorld = RequireWorld(result);
        var resultCombat = resultWorld.Combat ?? throw new InvalidDataException(
            "Automatic hand draw removed combat.");
        bool drawPending = resultCombat.PendingChoice is not null ||
            resultCombat.PendingKnowledgeDemonChoice is not null ||
            resultCombat.Stochastic.SemanticDeck?.Pending is not null;
        if (context ==
            AutomaticHandDrawSourceResultContextV111.DeferredSemanticStage)
        {
            if (drawPending)
                throw new InvalidDataException(
                    "Deferred semantic draw escaped its boundary router.");
            return CompleteAutomaticHandDrawStage(result, requestedCount);
        }

        if (context ==
            AutomaticHandDrawSourceResultContextV111
                .ResumedSemanticBeforeHandDraw)
            return CompleteAutomaticHandDrawStage(result, requestedCount);

        if (!drawPending && ControlledChanceGuardV111.Active &&
            resultCombat.AutomaticPipelineContinuation is { } cursor &&
            cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand))
            return CompleteAutomaticHandDrawStage(result);

        return PublishDrawMechanicsState(
            result, result.Player,
            ApplyAutomaticHandDrawPostStagePowerTiming(resultCombat));
    }

    /// <summary>
    /// Resume a saved legacy automatic draw through the common boundary
    /// router. The controlled adapter remains the fallback for result kinds
    /// outside that router's legacy automatic contract.
    /// </summary>
    private static PrototypeDrawCardsResumeResultV111
        ResumeSavedControlledAutomaticHandDraw(
        PrototypeDrawCardsSegmentV111 segment)
    {
        var resumed = ResumeAutomaticHandDrawBoundary(
            segment, requestedCount: 0,
            context: AutomaticHandDrawResumeContextV111.SavedControlledStage);
        return resumed ?? ContinueControlledHandDraw(
            segment.Run, segment.Drawn);
    }

    private sealed record PrototypeAutomaticHandDrawCostResumeV111(
        PrototypeDrawCardsResumeResultV111 Progress,
        PrototypeDrawCardsSegmentV111? Segment);

    private static RunState CommitAutomaticHandDrawStratagemSelection(
        RunState state,
        PlayerState player,
        CombatState combat,
        int remaining,
        int? semanticRequested,
        int? controlledRequested = null,
        bool createCheckpoint = false,
        RngBundle? prototypeRng = null)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        var choice = combat.PendingChoice;
        int expectedSelections = ExpectedStratagemSelectionCount(combat);
        if (!ControlledChanceGuardV111.Active || remaining <= 0 ||
            combat.PendingKnowledgeDemonChoice is not null ||
            choice is not
                { ChoiceId: "select_cards",
                  Selection.SourceZone: PrototypeCardZone.DrawPile } ||
            expectedSelections <= 0 ||
            (cursor is not null &&
             !cursor.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand)))
            throw new InvalidDataException(
                "Automatic Stratagem selection lost its hand-draw boundary.");
        bool isBeforeHandDraw =
            InspectAutomaticHandDrawProgressPosition(cursor);
        if (choice!.Selection.MinSelections != expectedSelections ||
            choice.Selection.MaxSelections != expectedSelections)
            throw new InvalidDataException(
                "Automatic Stratagem selection count differs from its power stacks.");

        PrototypeAutomaticHandDrawProgressV111 progress;
        if (semanticRequested is { } requested)
        {
            int? savedSemanticRequested = null;
            bool hasSavedProgress = false;
            bool hasSavedSemanticDrawProgress = false;
            if (cursor is not null)
            {
                savedSemanticRequested = cursor.Stochastic
                    .GetSavedSemanticHandDrawRequestedCount(
                        cursor.Mechanics);
                hasSavedProgress =
                    cursor.Stochastic.HasSavedHandDrawProgress();
                hasSavedSemanticDrawProgress =
                    cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                        cursor.Mechanics, out var priorProgress) &&
                    priorProgress.Kind ==
                        PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw;
            }
            if (requested <= 0 || combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                choice!.SemanticStratagemDrawSelection ||
                !choice.AutomaticSemanticStratagemDrawSelection ||
                (savedSemanticRequested is { } saved && saved != requested) ||
                (hasSavedProgress && !hasSavedSemanticDrawProgress))
                throw new InvalidDataException(
                    "Semantic automatic Stratagem cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                requested, remaining);
        }
        else
        {
            var priorProgress = cursor is not null &&
                cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                    cursor.Mechanics, out var activeProgress)
                ? activeProgress : null;
            bool hasSemanticDrawProgress = priorProgress?.Kind ==
                    PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw;
            if (combat.Stochastic.SemanticDeck is not null ||
                choice!.AutomaticSemanticStratagemDrawSelection ||
                choice.SemanticStratagemDrawSelection ||
                cursor?.Stochastic.CompletedSemanticHandDrawRequested is not null ||
                hasSemanticDrawProgress || isBeforeHandDraw ||
                (controlledRequested is { } controlledRequestInput &&
                 priorProgress?.RequestedCount is { } savedRequested &&
                 controlledRequestInput != savedRequested))
                throw new InvalidDataException(
                    "Controlled automatic Stratagem cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111
                .ControlledRecycleStratagem(
                    remaining,
                    controlledRequested ?? priorProgress?.RequestedCount);
        }

        var suspended = CommitAutomaticHandDrawProgress(
            state, player, combat, progress, createCheckpoint,
            prototypeRng);
        PrototypeStateInvariants.Validate(suspended);
        return suspended;
    }

    private static RunState SuspendAutomaticHandDrawAtCardDrawnChoice(
        RunState state,
        PlayerState player,
        CombatState combat,
        int remaining,
        int? semanticRequested,
        int? controlledRequested = null)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        bool isBeforeHandDraw =
            InspectAutomaticHandDrawProgressPosition(cursor);
        if (!ControlledChanceGuardV111.Active || remaining < 0 ||
            combat.PendingChoice is null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cursor is null || !cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            isBeforeHandDraw)
            throw new InvalidDataException(
                "Automatic CardDrawn choice lost its hand-draw cursor.");

        PrototypeAutomaticHandDrawProgressV111 progress;
        if (semanticRequested is { } requested)
        {
            int? savedSemanticRequested = cursor.Stochastic
                .GetSavedSemanticHandDrawRequestedCount(
                    cursor.Mechanics);
            bool hasControlledProgress =
                cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                    cursor.Mechanics, out var priorProgress) &&
                priorProgress.Kind is (
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost);
            if (requested <= 0 || combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                (savedSemanticRequested is { } saved && saved != requested) ||
                hasControlledProgress)
                throw new InvalidDataException(
                    "Semantic automatic CardDrawn choice cursor is inconsistent.");
            combat = combat with
            {
                PendingChoice = combat.PendingChoice with
                {
                    AutomaticSemanticDrawHookChoice = true
                }
            };
            progress = PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                requested, remaining);
        }
        else
        {
            var priorProgress =
                cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                    cursor.Mechanics, out var activeProgress)
                    ? activeProgress : null;
            bool hasSemanticDrawProgress = priorProgress?.Kind ==
                PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw;
            if (ControlledChanceGuardV111.EngineOwnedNativeProviderActive ||
                combat.Stochastic.SemanticDeck is not null ||
                cursor.Stochastic.CompletedSemanticHandDrawRequested is not null ||
                hasSemanticDrawProgress ||
                (controlledRequested is { } controlledRequestInput &&
                 priorProgress?.RequestedCount is { } savedRequested &&
                 controlledRequestInput != savedRequested))
                throw new InvalidDataException(
                    "Controlled automatic CardDrawn choice cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111
                .ControlledDrawHookChoice(
                    remaining,
                    controlledRequested ?? priorProgress?.RequestedCount);
        }

        var suspended = CommitAutomaticHandDrawProgress(
            state, player, combat, progress);
        PrototypeStateInvariants.Validate(suspended);
        return suspended;
    }

    // The provider adapters supply their suspension policy and continue the
    // tagged result through their own saved frame.
    private static PrototypeAutomaticHandDrawCostResumeV111
        ResumeAutomaticHandDrawAfterCost(
            RunState checkpoint,
            PrototypeControlledDrawCostV111 cost,
            int selectedCost,
            PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
            bool allowHookSuspension,
            string unsupportedBoundaryMessage,
            bool semantic)
    {
        var combat = RequireWorld(checkpoint).Combat ??
            throw new InvalidDataException(
                "Automatic draw-cost resume has no combat.");
        int? savedRequested = ValidateAutomaticHandDrawCostCheckpoint(
            combat, cost, semantic);
        if (!ValidateControlledDrawCost(
                checkpoint.Player, combat, cost))
            throw new InvalidDataException(
                "Automatic draw-cost resume does not match its saved choice.");

        var costProgress = ResolveDrawCostAndDispatchCardDrawn(
            checkpoint, cost, selectedCost, allowHookSuspension);
        if (costProgress.CostStagePending)
            return new PrototypeAutomaticHandDrawCostResumeV111(
                new PrototypeDrawCardsResumeResultV111(
                    costProgress.Run, cost.RemainingCount, savedRequested),
                null);

        var world = RequireWorld(costProgress.Run);
        var costCombat = world.Combat ?? throw new InvalidDataException(
            "Draw-cost resume removed combat.");
        if (costProgress.DrawnHookSuspended &&
            costCombat.PendingChoice is not null &&
            costCombat.PendingKnowledgeDemonChoice is null)
        {
            var suspendedSegment = new PrototypeDrawCardsSegmentV111(
                costProgress.Run,
                CreateDrawCardsResult(
                    costProgress.Run.Player, costCombat, [],
                    PrototypeDrawCardsBoundaryV111.NestedCardDrawnChoice(
                        cost.RemainingCount),
                    automaticHandDrawStage: true));
            return new PrototypeAutomaticHandDrawCostResumeV111(
                suspendedSegment.ToResumeProgress(savedRequested),
                suspendedSegment);
        }
        if (costProgress.DrawnHookSuspended ||
            costCombat.PendingChoice is not null ||
            costCombat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(unsupportedBoundaryMessage);

        var segment = ResumeAutomaticHandDrawSegment(
            costProgress.Run, cost.RemainingCount, suspensionPolicy,
            eventDepth: cost.EventDepth);
        return new PrototypeAutomaticHandDrawCostResumeV111(
            segment.ToResumeProgress(savedRequested), segment);
    }

    /// <summary>
    /// Own the post-cost entry and routing for an automatic DrawPlayerHand
    /// segment. Provider adapters supply their suspension policy and semantic
    /// frame; this owner routes the tagged result through shared dispatch.
    /// </summary>
    private static PrototypeDrawCardsResumeResultV111
        ResumeAutomaticHandDrawCost(
            RunState checkpoint,
            PrototypeControlledDrawCostV111 cost,
            int selectedCost,
            PrototypeControlledSemanticDrawV111? semanticDraw,
            PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
            bool allowHookSuspension,
            string unsupportedBoundaryMessage,
            string? recycleSite = null,
            string? recycleOperation = null)
    {
        bool semantic = semanticDraw is not null;
        if (semantic != (recycleSite is not null) ||
            semantic != (recycleOperation is not null))
            throw new InvalidDataException(
                "Automatic draw-cost provider frame is inconsistent.");

        var resumed = ResumeAutomaticHandDrawAfterCost(
            checkpoint, cost, selectedCost, suspensionPolicy,
            allowHookSuspension, unsupportedBoundaryMessage, semantic);
        resumed.Progress.Validate();
        if (resumed.Segment is not { } segment)
            return resumed.Progress;

        int requestedCount = resumed.Progress.RequestedCount ?? 0;
        if (semantic && requestedCount <= 0)
            throw new InvalidDataException(
                "Semantic automatic draw resume lost its request.");
        var progress = ResumeAutomaticHandDrawBoundary(
                segment, requestedCount,
                context: semantic
                    ? AutomaticHandDrawResumeContextV111.SavedSemanticStage
                    : AutomaticHandDrawResumeContextV111.SavedControlledStage,
                semanticDraw: semanticDraw,
                recycleSite: recycleSite ?? "combat.DrawPlayerHand",
                recycleOperation: recycleOperation ??
                    "unsupported_nested_stage") ??
            throw new InvalidDataException(
                "Automatic hand-draw result lost its continuation owner.");
        if (progress.RequestedCount is { } reportedRequested &&
            reportedRequested != requestedCount)
            throw new InvalidDataException(
                "Automatic hand-draw resume changed its requested count.");
        return semantic
            ? PrototypeDrawCardsResumeResultV111.ForHandDraw(
                progress.Run, requestedCount, progress.RemainingCount,
                progress.PipelineBoundary)
            : progress;
    }

    private static int? ValidateAutomaticHandDrawCostCheckpoint(
        CombatState combat,
        PrototypeControlledDrawCostV111 cost,
        bool semantic)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        if (!ControlledChanceGuardV111.Active || cost.RemainingCount < 0 ||
            !cost.FromHandDraw || cost.EventDepth != 0 ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cursor is null ||
            !cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                cursor.Mechanics, cost.RemainingCount, out var progress))
            throw new InvalidDataException(
                "Automatic draw-cost cursor does not match its saved count.");

        if (semantic)
        {
            if (combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                progress.Kind != PrototypeAutomaticHandDrawProgressKindV111
                    .SemanticDraw ||
                progress.RequestedCount is not > 0)
                throw new InvalidDataException(
                    "Semantic automatic draw-cost cursor is inconsistent.");
            return progress.RequestedCount.Value;
        }

        if (combat.Stochastic.SemanticDeck is not null ||
            progress.Kind != PrototypeAutomaticHandDrawProgressKindV111
                .ControlledDrawCost ||
            cursor.Stochastic.TryGetControlledHandDrawRemainder(
                cursor.Mechanics, out _))
            throw new InvalidDataException(
                "Controlled automatic draw-cost cursor is inconsistent.");
        return progress.RequestedCount;
    }
}
