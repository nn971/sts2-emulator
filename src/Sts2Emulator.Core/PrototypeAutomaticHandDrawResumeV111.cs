namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics for resuming the remaining cards in an automatic
/// DrawPlayerHand segment. Provider adapters choose which tagged boundaries
/// they can suspend and interpret the next result.
/// </summary>
public sealed partial class PrototypeGameEngine
{
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
        RunState state,
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
            PublishPrototypeRng(state), state.Player, combat,
            progress: null);
        return (cleared,
            resumedAfterSemanticHook
                ? AutomaticHandDrawSourceResultContextV111
                    .ResumedSemanticBeforeHandDraw
                : requestedResultContext,
            false);
    }

    /// <summary>
    /// Routes a completed DrawPlayerHand call to the matching continuation
    /// owner. Source-stage calls route only suspended boundaries; saved-stage
    /// resumes also route normal completion through the same provider adapter.
    /// </summary>
    private static PrototypeDrawCardsResumeResultV111?
        ResumeAutomaticHandDrawBoundary(
        RunState rngCheckpoint, PrototypeDrawCardsResult drawn,
        int requestedCount,
        AutomaticHandDrawResumeContextV111 context,
        PrototypeControlledSemanticDrawV111? semanticDraw = null,
        string recycleSite = "combat.DrawPlayerHand",
        string recycleOperation = "unsupported_nested_stage")
    {
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
            var resumed = combat.AutomaticPipelineContinuation is null
                ? ContinueInitialControlledHandDrawBoundary(
                    rngCheckpoint, drawn)
                : ContinueControlledHandDraw(rngCheckpoint, drawn);
            return new PrototypeDrawCardsResumeResultV111(
                resumed, drawn.RemainingCount);
        }

        return null;
    }

    /// <summary>
    /// Own every result from the source DrawPlayerHand operation: provider
    /// suspension routing, ordinary Stratagem resolution, deferred stage
    /// completion, and post-draw power timing.
    /// </summary>
    private static RunState ResumeAutomaticHandDrawSourceResult(
        RunState checkpoint,
        PrototypeDrawCardsResult drawn,
        int requestedCount,
        RngBundle prototypeRng,
        AutomaticHandDrawSourceResultContextV111 context)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(drawn);
        ArgumentNullException.ThrowIfNull(prototypeRng);
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

        var rngCheckpoint = checkpoint with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                checkpoint.Stochastic, prototypeRng)
        };
        var providerResume = ResumeAutomaticHandDrawBoundary(
            rngCheckpoint, drawn, requestedCount,
            AutomaticHandDrawResumeContextV111.SourceStageBoundary);
        if (providerResume is { } resumed)
            return resumed.Run;

        var result = PublishDrawCardsResult(rngCheckpoint, drawn);
        if (drawn.BoundaryKind ==
            PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            var world = RequireWorld(result);
            var combat = world.Combat ?? throw new InvalidDataException(
                "Automatic Stratagem resume removed combat.");
            var pendingDraw = new Queue<PrototypeQueuedOperation>(
                new[]
                {
                    StratagemChoiceOperation(combat),
                    new PrototypeQueuedOperation(
                        PrototypeCombatEffectKind.DrawCards,
                        drawn.RemainingCount, FromHandDraw: true)
                });
            var continued = ResolveOperations(
                result.Player, combat, pendingDraw, prototypeRng,
                moveSourceCardOnCompletion: false);
            result = PublishDrawMechanicsState(
                result, continued.Player, continued.Combat);
            result = result with
            {
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    result.Stochastic, prototypeRng)
            };
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

        return PublishDrawMechanicsState(
            result, result.Player,
            ApplyAutomaticHandDrawPostStagePowerTiming(resultCombat));
    }

    /// <summary>
    /// Resume a saved legacy automatic draw through the common boundary
    /// router. The controlled adapter remains the fallback for result kinds
    /// outside that router's legacy automatic contract.
    /// </summary>
    private static RunState ResumeSavedControlledAutomaticHandDraw(
        RunState state, PrototypeDrawCardsResult drawn)
    {
        var resumed = ResumeAutomaticHandDrawBoundary(
            state, drawn, requestedCount: 0,
            context: AutomaticHandDrawResumeContextV111.SavedControlledStage);
        return resumed?.Run ?? ContinueControlledHandDraw(state, drawn);
    }

    private sealed record PrototypeAutomaticHandDrawCostResumeV111(
        RunState Run,
        PrototypeDrawCardsResult? Drawn,
        int? SemanticRequested);

    private static void ValidateDrawCardsResult(
        PrototypeDrawCardsResult drawn, bool automaticHandDraw)
    {
        ArgumentNullException.ThrowIfNull(drawn);
        var combat = drawn.Combat;
        if (drawn.RemainingCount < 0)
            throw new InvalidDataException(
                "DrawCards result has a negative remaining count.");
        if (automaticHandDraw &&
            (combat.AutomaticPipelineContinuation is not { } cursor ||
             !cursor.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand)))
            throw new InvalidDataException(
                "Automatic hand-draw result lost its DrawPlayerHand checkpoint.");

        bool validBoundary = drawn.BoundaryKind switch
        {
            PrototypeDrawCardsBoundaryKind.Complete =>
                drawn.RemainingCount == 0 &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.Stochastic.SemanticDeck?.Pending is null,
            PrototypeDrawCardsBoundaryKind.ShuffleSelection =>
                drawn.RemainingCount > 0 &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                ExpectedStratagemSelectionCount(combat) > 0,
            PrototypeDrawCardsBoundaryKind.ControlledRecycle =>
                drawn.RemainingCount > 0 &&
                combat.Stochastic.SemanticDeck is null &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.DrawPile.Length == 0 &&
                combat.DiscardPile.Length > 0,
            PrototypeDrawCardsBoundaryKind.ControlledDrawCost =>
                combat.PendingChoice?.ControlledDrawCost is not null &&
                combat.PendingKnowledgeDemonChoice is null,
            PrototypeDrawCardsBoundaryKind.SemanticDraw =>
                drawn.RemainingCount > 0 &&
                combat.Stochastic.SemanticDeck is
                    { Mode: SemanticDeckStateV111.NatureMode,
                      Pending: not null } &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null,
            PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions =>
                drawn.RemainingCount > 0 &&
                combat.PendingChoice is null &&
                combat.PendingKnowledgeDemonChoice is null &&
                drawn.ControlledAfterShuffleInsertions is
                    { Length: > 0 },
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice =>
                combat.PendingChoice is not null &&
                combat.PendingKnowledgeDemonChoice is null &&
                combat.Stochastic.SemanticDeck?.Pending is null,
            _ => false
        };
        if (!validBoundary)
            throw new InvalidDataException(
                "DrawCards result does not match its saved mechanical boundary.");
    }

    private static PrototypeDrawCardsResult CreateDrawCardsResult(
        PlayerState player, CombatState combat,
        long[] drawnCardInstanceIds,
        PrototypeDrawCardsBoundaryV111 boundary,
        bool automaticHandDrawStage)
    {
        var result = new PrototypeDrawCardsResult(
            player, combat, drawnCardInstanceIds, boundary);
        ValidateDrawCardsResult(result, automaticHandDrawStage);
        return result;
    }

    private static RunState PublishDrawCardsResult(
        RunState checkpoint, PrototypeDrawCardsResult drawn)
    {
        ArgumentNullException.ThrowIfNull(drawn);
        return PublishDrawMechanicsState(
            checkpoint, drawn.Player, drawn.Combat);
    }

    /// <summary>Publish the coupled player/combat mechanics produced by draw
    /// resolution, draw-progress commits, or hand-draw completion.</summary>
    private static RunState PublishDrawMechanicsState(
        RunState checkpoint, PlayerState player, CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combat);
        var world = RequireWorld(checkpoint);
        return checkpoint with
        {
            Player = player,
            World = world with { Combat = combat }
        };
    }

    private static RunState CommitAutomaticHandDrawStratagemSelection(
        RunState state,
        PlayerState player,
        CombatState combat,
        int remaining,
        int? semanticRequested,
        bool createCheckpoint = false)
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
        if (choice!.Selection.MinSelections != expectedSelections ||
            choice.Selection.MaxSelections != expectedSelections)
            throw new InvalidDataException(
                "Automatic Stratagem selection count differs from its power stacks.");

        PrototypeAutomaticHandDrawProgressV111 progress;
        if (semanticRequested is { } requested)
        {
            int? savedSemanticRequested = null;
            if (cursor is not null)
            {
                savedSemanticRequested =
                    cursor.Stochastic.TryGetSemanticHandDrawProgress(
                        cursor.Mechanics, out int savedRequested, out _)
                        ? savedRequested
                        : cursor.Stochastic.CompletedSemanticHandDrawRequested;
            }
            var priorProgress = cursor?.Stochastic.HandDrawProgress;
            if (requested <= 0 || combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                choice!.SemanticStratagemDrawSelection ||
                !choice.AutomaticSemanticStratagemDrawSelection ||
                (savedSemanticRequested is { } saved && saved != requested) ||
                (priorProgress is not null && priorProgress.Kind !=
                    PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw))
                throw new InvalidDataException(
                    "Semantic automatic Stratagem cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                requested, remaining);
        }
        else
        {
            var priorProgress = cursor?.Stochastic.HandDrawProgress;
            if (combat.Stochastic.SemanticDeck is not null ||
                choice!.AutomaticSemanticStratagemDrawSelection ||
                choice.SemanticStratagemDrawSelection ||
                cursor?.Stochastic.CompletedSemanticHandDrawRequested is not null ||
                priorProgress?.Kind is
                    PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw or
                    PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledBeforeHandDrawHookChoice)
                throw new InvalidDataException(
                    "Controlled automatic Stratagem cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111
                .ControlledRecycleStratagem(remaining);
        }

        var suspended = CommitAutomaticHandDrawProgress(
            state, player, combat, progress, createCheckpoint);
        PrototypeStateInvariants.Validate(suspended);
        return suspended;
    }

    private static RunState SuspendAutomaticHandDrawAtCardDrawnChoice(
        RunState state,
        PlayerState player,
        CombatState combat,
        int remaining,
        int? semanticRequested)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        if (!ControlledChanceGuardV111.Active || remaining < 0 ||
            combat.PendingChoice is null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cursor is null || !cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            cursor.Stochastic.HandDrawProgress?.Kind is
                PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw or
                PrototypeAutomaticHandDrawProgressKindV111.ControlledBeforeHandDrawHookChoice)
            throw new InvalidDataException(
                "Automatic CardDrawn choice lost its hand-draw cursor.");

        PrototypeAutomaticHandDrawProgressV111 progress;
        if (semanticRequested is { } requested)
        {
            int? savedSemanticRequested =
                cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int savedRequested, out _)
                    ? savedRequested
                    : cursor.Stochastic.CompletedSemanticHandDrawRequested;
            var priorProgress = cursor.Stochastic.HandDrawProgress;
            if (requested <= 0 || combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                (savedSemanticRequested is { } saved && saved != requested) ||
                priorProgress?.Kind is
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem or
                    PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost)
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
            if (ControlledChanceGuardV111.EngineOwnedNativeProviderActive ||
                combat.Stochastic.SemanticDeck is not null ||
                cursor.Stochastic.CompletedSemanticHandDrawRequested is not null ||
                cursor.Stochastic.HandDrawProgress?.Kind ==
                    PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw)
                throw new InvalidDataException(
                    "Controlled automatic CardDrawn choice cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111
                .ControlledDrawHookChoice(remaining);
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
                costProgress.Run, null, savedRequested);

        var world = RequireWorld(costProgress.Run);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Draw-cost resume removed combat.");
        if (costProgress.DrawnHookSuspended &&
            combat.PendingChoice is not null &&
            combat.PendingKnowledgeDemonChoice is null)
            return new PrototypeAutomaticHandDrawCostResumeV111(
                costProgress.Run, CreateDrawCardsResult(
                    costProgress.Run.Player, combat, [],
                    PrototypeDrawCardsBoundaryV111.NestedCardDrawnChoice(
                        cost.RemainingCount),
                    automaticHandDrawStage: true), savedRequested);
        if (costProgress.DrawnHookSuspended ||
            combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new NotSupportedException(unsupportedBoundaryMessage);

        var prototypeRng = costProgress.Run.Stochastic.PrototypeRng;
        var drawn = ResumeAutomaticHandDrawCards(
            costProgress.Run, cost.RemainingCount, suspensionPolicy,
            prototypeRng, eventDepth: cost.EventDepth);
        var resumedRun = costProgress.Run with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                costProgress.Run.Stochastic, prototypeRng)
        };
        return new PrototypeAutomaticHandDrawCostResumeV111(
            resumedRun, drawn, savedRequested);
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
        if (resumed.Drawn is not { } drawn)
            return new PrototypeDrawCardsResumeResultV111(
                resumed.Run, cost.RemainingCount);

        int requestedCount = resumed.SemanticRequested ?? 0;
        if (semantic && requestedCount <= 0)
            throw new InvalidDataException(
                "Semantic automatic draw resume lost its request.");
        if (!semantic && resumed.SemanticRequested is not null)
            throw new InvalidDataException(
                "Controlled automatic draw acquired semantic progress.");
        return ResumeAutomaticHandDrawBoundary(
                resumed.Run, drawn, requestedCount,
                context: semantic
                    ? AutomaticHandDrawResumeContextV111.SavedSemanticStage
                    : AutomaticHandDrawResumeContextV111.SavedControlledStage,
                semanticDraw: semanticDraw,
                recycleSite: recycleSite ?? "combat.DrawPlayerHand",
                recycleOperation: recycleOperation ??
                    "unsupported_nested_stage") ??
            throw new InvalidDataException(
                "Automatic hand-draw result lost its continuation owner.");
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
                cursor.Mechanics, out var progress) ||
            progress.RemainingCount != cost.RemainingCount)
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
            cursor.Stochastic.TryGetControlledHandDrawRemainder(out _))
            throw new InvalidDataException(
                "Controlled automatic draw-cost cursor is inconsistent.");
        return null;
    }

    private static PrototypeDrawCardsResult ResumeAutomaticHandDrawCards(
        RunState checkpoint,
        int remaining,
        PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
        RngBundle prototypeRng,
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
            !cursor.Stochastic.TryGetActiveAutomaticHandDrawProgress(
                cursor.Mechanics, out var progress) ||
            progress.RemainingCount != remaining)
            throw new InvalidDataException(
                "Automatic hand-draw resume cursor is inconsistent.");

        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            combat.Stochastic.SemanticDeck?.Pending is not null)
            throw new InvalidDataException(
                "Automatic hand-draw resume boundary is inconsistent.");

        var cleared = CommitAutomaticHandDrawProgress(
            checkpoint, checkpoint.Player, combat, progress: null);
        return DrawCards(
            cleared.Player, RequireWorld(cleared).Combat!, remaining,
            prototypeRng,
            fromHandDraw: true, eventDepth: eventDepth,
            suspensionPolicy: suspensionPolicy,
            automaticHandDrawStage: true);
    }
}
