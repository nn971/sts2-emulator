namespace Sts2Emulator.Core;

/// <summary>
/// Provider-neutral mechanics for advancing and suspending a saved draw
/// segment, including shared draw-progress interpretation. Provider adapters
/// retain their own choice and transcript owners.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static PrototypeAutomaticPipelineResumeResultV111
        ResumeSemanticDrawAfterControlledInsertion(
            RunState state, CombatState combat,
            PrototypeControlledSemanticDrawV111 draw,
            PrototypeChoiceResolutionContinuationState continuation)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(continuation);
        if (draw.RemainingCount <= 0 || draw.EventDepth < 0)
            throw new InvalidDataException(
                "Controlled insertion saved an invalid semantic draw cursor.");

        if (draw.StratagemSelectionPending)
        {
            var stratagemRun = ResumeControlledStratagemSelection(
                state, combat, draw, continuation);
            return draw.StandaloneSemanticDrawSegment
                ? PrototypeAutomaticPipelineResumeResultV111.FromRun(
                    stratagemRun)
                : ResumeControlledInsertionPipeline(stratagemRun);
        }

        var drawRun = state with
        {
            World = state.World! with { Combat = combat }
        };
        if (draw.StandaloneSemanticDrawSegment)
            return PrototypeAutomaticPipelineResumeResultV111.FromRun(
                BeginSemanticCombatDraw(
                    drawRun, draw.RemainingCount,
                    draw.FromHandDraw).Run);

        if (draw.AutomaticSemanticHandDrawSegment)
            return ResumeSemanticAutomaticHandDrawAfterNestedChoice(
                state, drawRun, null, "controlled draw insertion",
                expectedSourceRemaining: draw.RemainingCount)
                    .ToAutomaticPipelineResumeResult();

        var drawnSegment = ExecuteDrawCardsSegment(
            state, prototypeRng => DrawCards(
                state.Player, combat, draw.RemainingCount,
                prototypeRng, draw.FromHandDraw,
                eventDepth: draw.EventDepth,
                suspensionPolicy:
                    PrototypeDrawCardsSuspensionPolicyV111
                        .SemanticDrawCards(effectDraw: true)));
        var drawState = drawnSegment.Run with
        {
            World = state.World! with { Combat = combat }
        };
        var continued = ContinueControlledSemanticEffectDraw(
            drawnSegment with { Run = drawState }, draw, continuation);
        return ResumeControlledInsertionPipeline(continued);
    }

    private static PrototypeQueuedOperation CreateDrawResumeOperation(
        PrototypeControlledSemanticDrawV111 draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        if (draw.RemainingCount < 0 || draw.EventDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(draw),
                "A saved draw cursor requires nonnegative progress values.");
        return new PrototypeQueuedOperation(
            PrototypeCombatEffectKind.DrawCards,
            draw.RemainingCount,
            FromHandDraw: draw.FromHandDraw,
            DrawnCardKeyword: draw.DrawnCardKeyword is null
                ? null : draw.DrawnCardKeyword with { },
            DeferredDrawnCardInstanceIds:
                draw.DeferredKeywordCardInstanceIds,
            DrawEventDepth: draw.EventDepth,
            StandaloneSemanticDrawSegment:
                draw.StandaloneSemanticDrawSegment,
            DrawEqualSelectionCount: draw.DrawEqualSelectionCount);
    }

    private static Queue<PrototypeQueuedOperation>
        CreateHandDrawStratagemResumeOperations(
            CombatState combat, int remainingCount)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (remainingCount <= 0 ||
            ExpectedStratagemSelectionCount(combat) <= 0)
            throw new InvalidDataException(
                "Hand-draw Stratagem resume has no remaining draw or selection.");
        return new Queue<PrototypeQueuedOperation>(
        [
            StratagemChoiceOperation(combat),
            new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.DrawCards,
                remainingCount,
                FromHandDraw: true)
        ]);
    }

    private static (PlayerState Player, CombatState Combat)
        ResumeLegacyHandDrawAfterStratagemSelection(
            PlayerState player, CombatState combat, int remainingCount,
            RngBundle prototypeRng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(prototypeRng);
        var operations = CreateHandDrawStratagemResumeOperations(
            combat, remainingCount);
        return ResolveOperations(
            player, combat, operations, prototypeRng,
            moveSourceCardOnCompletion: false);
    }

    private static CombatState AttachDrawResumeContinuation(
        CombatState combat,
        PrototypeQueuedOperation drawOperation,
        PrototypeChoiceResolutionContinuationState continuation) =>
        AttachOuterChoiceContinuation(
            combat,
            continuation with
            {
                Operations = new[] { drawOperation }
                    .Concat(continuation.Operations).ToArray()
            });

    private static long[] AccumulateDrawnCardInstanceIds(
        IEnumerable<long>? previousCardInstanceIds,
        IEnumerable<long> drawnCardInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(drawnCardInstanceIds);
        return (previousCardInstanceIds ?? [])
            .Concat(drawnCardInstanceIds)
            .Distinct()
            .ToArray();
    }

    private static PrototypeControlledSemanticDrawV111
        CreateAdvancedEffectDrawFrame(
            PrototypeQueuedOperation operation,
            int remainingCount,
            int eventDepth,
            long[] drawnCardInstanceIds,
            bool? stratagemSelectionPending = null)
    {
        if (operation.Kind != PrototypeCombatEffectKind.DrawCards)
            throw new ArgumentException(
                "Effect draw frame needs a DrawCards operation.",
                nameof(operation));
        var draw = new PrototypeControlledSemanticDrawV111(
            remainingCount, operation.FromHandDraw, eventDepth,
            operation.DrawnCardKeyword,
            operation.DeferredDrawnCardInstanceIds,
            StandaloneSemanticDrawSegment:
                operation.StandaloneSemanticDrawSegment)
        {
            DrawEqualSelectionCount = operation.DrawEqualSelectionCount
        };
        return AdvanceControlledSemanticDrawFrame(
            draw, remainingCount, drawnCardInstanceIds,
            accumulateDrawnCardIds: true,
            stratagemSelectionPending: stratagemSelectionPending);
    }

    private static PrototypeControlledSemanticDrawV111
        AdvanceControlledSemanticDrawFrame(
            PrototypeControlledSemanticDrawV111 draw,
            int remainingCount,
            long[] drawnCardInstanceIds,
            bool accumulateDrawnCardIds,
            bool? stratagemSelectionPending = null)
    {
        var deferredKeywordIds = accumulateDrawnCardIds
            ? AccumulateDrawnCardInstanceIds(
                draw.DeferredKeywordCardInstanceIds,
                drawnCardInstanceIds)
            : draw.DeferredKeywordCardInstanceIds;
        return draw with
        {
            RemainingCount = remainingCount,
            DeferredKeywordCardInstanceIds = deferredKeywordIds,
            StratagemSelectionPending =
                stratagemSelectionPending ?? draw.StratagemSelectionPending
        };
    }

    private static (PlayerState Player, CombatState Combat)
        DispatchCardDrawnAfterCost(
            PlayerState player,
            CombatState combat,
            long cardInstanceId,
            bool fromHandDraw,
            RngBundle rng,
            int eventDepth,
            bool allowSuspension)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(rng);
        var card = RequireCombatCard(combat, cardInstanceId);
        return DispatchCombatEvent(
            player,
            combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardDrawn,
                SourceCardInstanceId: cardInstanceId,
                CardId: card.CardId,
                FromHandDraw: fromHandDraw),
            rng,
            eventDepth,
            allowSuspension: allowSuspension);
    }

    private static CombatState AttachDrawCardsAtCardDrawnChoice(
        CombatState combat,
        PrototypeControlledSemanticDrawV111 resumedDraw,
        PrototypeChoiceResolutionContinuationState continuation,
        bool markStandaloneHookChoice)
    {
        var resumeDraw = CreateDrawResumeOperation(resumedDraw);
        combat = AttachDrawResumeContinuation(
            combat, resumeDraw, continuation);
        if (markStandaloneHookChoice)
            combat = combat with
            {
                PendingChoice = combat.PendingChoice! with
                {
                    StandaloneSemanticDrawHookChoice = true
                }
            };
        return combat;
    }

    private static CombatState AttachControlledDrawResumeBoundary(
        CombatState combat,
        PrototypeDrawCardsBoundaryKind boundary,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation,
        PrototypeAfterShuffleRelicInsertionV111[]? insertions = null,
        int insertionOrdinal = 0,
        bool markStandaloneHookChoice = false)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(continuation);
        return boundary switch
        {
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice =>
                AttachDrawCardsAtCardDrawnChoice(
                    combat, draw, continuation, markStandaloneHookChoice),
            PrototypeDrawCardsBoundaryKind.SemanticDraw =>
                MakeControlledSemanticDrawChoice(
                    combat, draw, continuation),
            PrototypeDrawCardsBoundaryKind.ControlledDrawCost =>
                AttachControlledSemanticDrawCost(
                    combat, draw, continuation),
            PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions =>
                AttachAfterShuffleInsertionBoundary(
                    combat, draw, continuation, insertions,
                    insertionOrdinal),
            _ => throw new InvalidDataException(
                "Draw boundary has no saved controlled resume choice.")
        };
    }

    private static CombatState AttachAfterShuffleInsertionBoundary(
        CombatState combat,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation,
        PrototypeAfterShuffleRelicInsertionV111[]? insertions,
        int insertionOrdinal)
    {
        if (insertions is not { Length: > 0 })
            throw new InvalidDataException(
                "After-shuffle draw resume lost its insertion sequence.");
        return MakeControlledAfterShuffleInsertionChoice(
            combat, insertions[0], insertions.Skip(1).ToArray(), [], draw,
            continuation, insertionOrdinal);
    }

    private readonly record struct PrototypeEffectDrawResumeResultV111(
        PlayerState Player,
        CombatState Combat,
        Queue<PrototypeQueuedOperation> Operations,
        bool ReturnFromOperations);

    private static PrototypeEffectDrawResumeResultV111
        ResolveEffectDrawOperation(
            PlayerState player,
            CombatState combat,
            PrototypeQueuedOperation operation,
            Queue<PrototypeQueuedOperation> remainingOperations,
            RngBundle rng,
            int eventDepth,
            EffectContinuationContext continuationContext)
    {
        if (operation.Kind != PrototypeCombatEffectKind.DrawCards)
            throw new ArgumentException(
                "Effect draw continuation needs a DrawCards operation.",
                nameof(operation));
        ArgumentNullException.ThrowIfNull(remainingOperations);
        var drawEventDepth = operation.DrawEventDepth ?? eventDepth + 1;
        var drawn = DrawCards(
            player, combat, operation.Amount, rng,
            fromHandDraw: operation.FromHandDraw,
            eventDepth: drawEventDepth,
            suspensionPolicy:
                PrototypeDrawCardsSuspensionPolicyV111
                    .ControlledEffectDraw(combat));
        player = drawn.Player;
        combat = drawn.Combat;
        var boundary = drawn.BoundaryKind;
        if (boundary == PrototypeDrawCardsBoundaryKind.ControlledRecycle)
        {
            var suspendedDraw = CreateAdvancedEffectDrawFrame(
                operation, drawn.RemainingCount, drawEventDepth,
                drawn.DrawnCardInstanceIds);
            var resumeDraw = CreateDrawResumeOperation(suspendedDraw);
            var recycleContinuation = CreateEffectContinuation(
                continuationContext,
                new[] { resumeDraw }.Concat(remainingOperations));
            if (combat.Stochastic.SemanticDeck is not null ||
                drawn.RemainingCount <= 0 || combat.DrawPile.Length != 0 ||
                combat.DiscardPile.Length == 0)
                throw new InvalidDataException(
                    "Legacy effect-draw recycle boundary is inconsistent.");

            var recycleOperation = new PrototypeQueuedOperation(
                PrototypeCombatEffectKind.RecycleDrawPileForEffectDraw, 0);
            if (combat.DiscardPile.Length > 1)
            {
                if (!TryMakeControlledPoolShuffleChoice(
                        combat, recycleOperation,
                        continuationContext.SourceCardInstanceId,
                        recycleContinuation, out var suspended))
                    throw new NotSupportedException(
                        "UNSUPPORTED_CONTROLLED_CHANCE " +
                        "site=combat.draw.recycle " +
                        "operation=missing_saved_shuffle_continuation");
                return new PrototypeEffectDrawResumeResultV111(
                    player, suspended, remainingOperations,
                    ReturnFromOperations: true);
            }

            var recycled = (long[])combat.DiscardPile.Clone();
            var postHook = NativeShuffleStepReplayV111.CheckPostHook(
                ApplyPerfectFitShuffleOrder(combat, recycled));
            combat = SemanticDrawPileAdapterV111.CommitLegacyRecycle(
                combat, postHook);
            var insertions = ControlledAfterShuffleRelicInsertions(combat);
            if (insertions.Length > 0)
            {
                combat = MakeControlledAfterShuffleInsertionChoice(
                    combat, insertions[0], insertions.Skip(1).ToArray(), [],
                    null, recycleContinuation, ordinal: 0);
                return new PrototypeEffectDrawResumeResultV111(
                    player, combat, remainingOperations,
                    ReturnFromOperations: true);
            }
            var resumed = ResumeChoiceResolutionContinuation(
                player, combat, rng, recycleContinuation);
            return new PrototypeEffectDrawResumeResultV111(
                resumed.Player, resumed.Combat, remainingOperations,
                ReturnFromOperations: true);
        }

        if (boundary == PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice)
        {
            var suspendedDraw = CreateAdvancedEffectDrawFrame(
                operation, drawn.RemainingCount, drawEventDepth,
                drawn.DrawnCardInstanceIds);
            var continuation = CreateEffectContinuation(
                continuationContext, remainingOperations);
            combat = AttachControlledDrawResumeBoundary(
                combat, boundary, suspendedDraw, continuation,
                markStandaloneHookChoice:
                    operation.StandaloneSemanticDrawSegment);
            return new PrototypeEffectDrawResumeResultV111(
                player, combat, remainingOperations,
                ReturnFromOperations: true);
        }

        if (boundary is
            PrototypeDrawCardsBoundaryKind.SemanticDraw or
            PrototypeDrawCardsBoundaryKind.ControlledDrawCost or
            PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions)
        {
            var continuation = CreateEffectContinuation(
                continuationContext, remainingOperations);
            var suspendedDraw = CreateAdvancedEffectDrawFrame(
                operation, drawn.RemainingCount, drawEventDepth,
                drawn.DrawnCardInstanceIds,
                stratagemSelectionPending:
                    boundary ==
                        PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions &&
                    ExpectedStratagemSelectionCount(combat) > 0);
            return new PrototypeEffectDrawResumeResultV111(
                player,
                AttachControlledDrawResumeBoundary(
                    combat, boundary, suspendedDraw, continuation,
                    drawn.ControlledAfterShuffleInsertions),
                remainingOperations,
                ReturnFromOperations: true);
        }

        if (boundary == PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            var suspendedDraw = CreateAdvancedEffectDrawFrame(
                operation, drawn.RemainingCount, drawEventDepth,
                drawn.DrawnCardInstanceIds);
            remainingOperations = new Queue<PrototypeQueuedOperation>(
                new[]
                {
                    StratagemChoiceOperation(
                        combat,
                        suspendedDraw.StandaloneSemanticDrawSegment),
                    operation with
                    {
                        Amount = suspendedDraw.RemainingCount,
                        DeferredDrawnCardInstanceIds =
                            suspendedDraw.DeferredKeywordCardInstanceIds,
                        DrawEventDepth = suspendedDraw.EventDepth
                    }
                }.Concat(remainingOperations));
        }

        if (operation.DrawnCardKeyword is not null &&
            boundary != PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            var completedDraw = CreateAdvancedEffectDrawFrame(
                operation, drawn.RemainingCount, drawEventDepth,
                drawn.DrawnCardInstanceIds);
            combat = ApplyDrawnCardKeywordOverride(
                combat, operation.DrawnCardKeyword,
                completedDraw.DeferredKeywordCardInstanceIds ?? []);
        }

        return new PrototypeEffectDrawResumeResultV111(
            player, combat, remainingOperations, ReturnFromOperations: false);
    }

    private static PrototypeEffectDrawResumeResultV111
        ResolveHandReturnDrawShuffleOperation(
            PlayerState player,
            CombatState combat,
            PrototypeQueuedOperation operation,
            Queue<PrototypeQueuedOperation> remainingOperations,
            RngBundle rng,
            EffectContinuationContext continuationContext)
    {
        if (operation.Kind !=
            PrototypeCombatEffectKind.ReturnHandToDrawAndShuffle)
            throw new ArgumentException(
                "Hand-return continuation needs its shuffle operation.",
                nameof(operation));
        ArgumentNullException.ThrowIfNull(remainingOperations);
        if (combat.Stochastic.SemanticDeck is
                { Mode: SemanticDeckStateV111.NatureMode } &&
            (!ControlledChanceGuardV111.Active ||
             ControlledChanceGuardV111.EngineOwnedNativeProviderActive))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK " +
                "site=combat.hand_return.draw_shuffle " +
                "operation=requires_shuffle_continuation");

        var returnedHandIds = (long[])combat.Hand.Clone();
        var shuffled = combat.DrawPile.Concat(combat.Hand).ToArray();
        combat = ShuffleCombatCardPool(
            combat, rng, shuffled,
            StochasticSiteRegistryV111.CombatHandReturnShuffleSiteId,
            "combat", "run.shuffle",
            "effect:" + operation.Kind + ":source:" +
                (continuationContext.SourceCardInstanceId?.ToString() ?? "none"),
            operation.Kind, shuffled.Length,
            continuationContext.SourceCardInstanceId);
        var shuffledDrawPile = ApplyPerfectFitShuffleOrder(
            combat, shuffled);
        combat = SemanticDrawPileAdapterV111.CommitHandReturnShuffle(
            combat, shuffledDrawPile, returnedHandIds);

        var insertions = ControlledAfterShuffleRelicInsertions(combat);
        if (ControlledChanceGuardV111.Active &&
            !ControlledChanceGuardV111.EngineOwnedNativeProviderActive &&
            insertions.Length > 0)
        {
            var continuation = CreateEffectContinuation(
                continuationContext, remainingOperations);
            combat = MakeControlledAfterShuffleInsertionChoice(
                combat, insertions[0], insertions.Skip(1).ToArray(), [], null,
                continuation, ordinal: 0);
            return new PrototypeEffectDrawResumeResultV111(
                player, combat, remainingOperations,
                ReturnFromOperations: true);
        }

        combat = ApplyRelicAfterShuffleCards(combat, rng);
        return new PrototypeEffectDrawResumeResultV111(
            player, combat, remainingOperations, ReturnFromOperations: false);
    }

    internal static int GetCardsDrawnSince(
        CombatState combat, int drawnBefore)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (drawnBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(drawnBefore));
        var drawn = combat.CounterState.CardsDrawnThisCombat - drawnBefore;
        if (drawn < 0)
            throw new InvalidDataException(
                "Combat draw count moved behind its saved resume checkpoint.");
        return drawn;
    }

    /// <summary>
    /// Resolve the remaining count from the saved mechanics continuation.
    /// Standalone draw segments, nested draws, and automatic hand draws all
    /// project their progress from the same pending choice and pipeline state.
    /// </summary>
    internal static int GetSemanticDrawRemainingCount(
        RunState run, int previousRemaining, int drawnBefore)
    {
        var combat = run.World?.Combat ?? throw new InvalidDataException(
            "Semantic draw continuation removed combat.");
        var pending = combat.PendingChoice;
        if (pending is not null &&
            TryGetStandaloneSemanticDrawOperation(pending,
                out var drawOperation))
            return drawOperation.Amount;
        int? savedCount = pending?.ControlledSemanticDraw?.RemainingCount ??
            pending?.ControlledDrawInsertion?.SemanticDrawContinuation?
                .RemainingCount ??
            pending?.ControlledDrawCost?.RemainingCount;
        if (savedCount is { } pendingRemaining)
            return pendingRemaining;
        if (TryGetSemanticAutomaticHandDrawProgress(
                combat, out _, out int remaining))
            return remaining;
        if (combat.Stochastic.SemanticDeck?.Pending is not null)
            return Math.Max(0, previousRemaining -
                GetCardsDrawnSince(combat, drawnBefore));
        return 0;
    }

    internal static bool TryGetStandaloneSemanticDrawOperation(
        PendingCombatChoiceState choice,
        out PrototypeQueuedOperation operation)
    {
        if (choice.Continuation.FirstOrDefault(item =>
                item.Kind == PrototypeCombatEffectKind.DrawCards &&
                item.StandaloneSemanticDrawSegment) is { } drawOperation)
        {
            operation = drawOperation;
            return true;
        }
        for (PrototypeChoiceResolutionContinuationState? current =
                choice.OuterChoiceContinuation;
             current is not null;
             current = current.Parent)
            if (TryGetStandaloneSemanticDrawOperation(current,
                    out operation))
                return true;
        operation = null!;
        return false;
    }

    internal static bool TryGetStandaloneSemanticDrawOperation(
        PrototypeChoiceResolutionContinuationState continuation,
        out PrototypeQueuedOperation operation)
    {
        for (PrototypeChoiceResolutionContinuationState? current = continuation;
             current is not null;
             current = current.Parent)
            if (current.Operations.FirstOrDefault(item =>
                    item.Kind == PrototypeCombatEffectKind.DrawCards &&
                    item.StandaloneSemanticDrawSegment) is { } drawOperation)
            {
                operation = drawOperation;
                return true;
            }
        operation = null!;
        return false;
    }

    internal static int ExpectedStratagemSelectionCount(
        CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        int stacks = combat.PlayerPowers
            .Where(power => power.PowerId == "proto.power.stratagem")
            .Sum(power => Math.Max(0, power.Stacks));
        return Math.Min(stacks,
            Math.Min(combat.DrawPile.Length,
                PrototypeContent.Rules.RemainingHandCapacity(
                    combat.Hand.Length)));
    }

    private static RunState ResolveOpeningHandDrawResult(
        PrototypeDrawCardsSegmentV111 segment)
    {
        var state = segment.Run;
        var drawn = segment.Drawn;
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(drawn);
        var prototypeRng = state.Stochastic.PrototypeRng;
        var player = drawn.Player;
        var combat = drawn.Combat;
        if (drawn.BoundaryKind ==
                PrototypeDrawCardsBoundaryKind.ShuffleSelection &&
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode })
        {
            var resolved = ResumeLegacyHandDrawAfterStratagemSelection(
                player, combat, drawn.RemainingCount, prototypeRng);
            player = resolved.Player;
            combat = resolved.Combat;
        }
        else if (IsSemanticDrawResumeBoundary(
                     drawn.BoundaryKind,
                     includeSemanticDrawChoiceBoundaries: true))
        {
            if (!ControlledChanceGuardV111.Active ||
                combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode })
                throw new NotSupportedException(
                    "UNSUPPORTED_OPENING_DRAW " +
                    "site=combat.opening_draw operation=" +
                    drawn.BoundaryKind);

            var drawFrame = CreateSemanticDrawResultFrame(
                drawn.RemainingCount, fromHandDraw: true, eventDepth: 0,
                automatic: false, standalone: false);
            bool finalCardHook = drawn.BoundaryKind ==
                    PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice &&
                drawn.RemainingCount == 0;
            if (!finalCardHook)
                return ResolveControlledSemanticDrawResult(
                    segment, drawFrame, EmptyDrawChoiceContinuation(),
                    automaticHandDrawRequested: null,
                    recycleSite: "combat.opening_draw",
                    recycleOperation:
                        drawn.BoundaryKind ==
                            PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions
                            ? "after_shuffle_insertion"
                            : "opening_draw_resume").Run;
        }
        else if (drawn.BoundaryKind !=
            PrototypeDrawCardsBoundaryKind.Complete)
        {
            throw new NotSupportedException(
                "UNSUPPORTED_OPENING_DRAW " +
                "site=combat.opening_draw operation=" +
                drawn.BoundaryKind);
        }

        state = state with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                state.Stochastic, prototypeRng)
        };
        return PublishDrawMechanicsState(state, player, combat);
    }

    /// <summary>
    /// Resume the remaining cards in a saved automatic hand-draw segment.
    /// The shared cursor is checked and cleared before entering the draw
    /// kernel with the caller's prototype RNG projection.
    /// </summary>
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
                cursor.Mechanics, remaining, out var progress))
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

    /// <summary>
    /// Resume a saved automatic hand-draw segment and publish its advanced
    /// prototype RNG with the source checkpoint. Adapters retain ownership of
    /// the returned boundary interpretation and their typed transcripts.
    /// </summary>
    private static PrototypeDrawCardsSegmentV111
        ResumeAutomaticHandDrawSegment(
        RunState checkpoint,
        int remaining,
        PrototypeDrawCardsSuspensionPolicyV111 suspensionPolicy,
        int eventDepth = 0)
    {
        return ExecuteDrawCardsSegment(checkpoint, prototypeRng =>
            ResumeAutomaticHandDrawCards(
                checkpoint, remaining, suspensionPolicy, prototypeRng,
                eventDepth));
    }

    /// <summary>
    /// Run one draw kernel segment with a local prototype RNG projection and
    /// publish the advanced provider state with its source checkpoint.
    /// Callers continue to own mechanics and boundary interpretation.
    /// </summary>
    private static PrototypeDrawCardsSegmentV111
        ExecuteDrawCardsSegment(
        RunState checkpoint,
        Func<RngBundle, PrototypeDrawCardsResult> drawSegment)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(drawSegment);
        return ExecuteDrawCardsSegment(
            checkpoint, checkpoint.Stochastic.PrototypeRng, drawSegment);
    }

    /// <summary>
    /// Use a caller-owned local projection when earlier operations in the same
    /// source transition have already advanced the prototype provider.
    /// </summary>
    private static PrototypeDrawCardsSegmentV111
        ExecuteDrawCardsSegment(
        RunState checkpoint,
        RngBundle prototypeRng,
        Func<RngBundle, PrototypeDrawCardsResult> drawSegment)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(prototypeRng);
        ArgumentNullException.ThrowIfNull(drawSegment);
        var drawn = drawSegment(prototypeRng);
        var run = checkpoint with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                checkpoint.Stochastic, prototypeRng)
        };
        return new PrototypeDrawCardsSegmentV111(run, drawn);
    }
}
