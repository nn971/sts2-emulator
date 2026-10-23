namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in initial semantic combat draw bridge. Uses the *real* DrawCards
/// kernel; its isolated fixture envelope is distinct from the legacy
/// opening Fisher-Yates controlled-playable state and transcript.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    private enum SemanticDrawCostResumeKindV111
    {
        Standalone,
        AutomaticHandDraw,
        CardEffect
    }

    internal static SemanticCombatDrawResumeResultV111
        BeginSemanticCombatDraw(RunState run, int count, bool fromHandDraw)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic combat draw requires a controlled chance guard.");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("No combat to draw from.");
        if (combat.Stochastic.SemanticDeck is null)
            throw new InvalidOperationException("Missing semantic deck provider.");
        var automaticCursor = combat.AutomaticPipelineContinuation;
        bool automatic = IsSemanticAutomaticHandDraw(combat);
        int? automaticRequested = null;
        int? automaticRemaining = null;
        if (automatic && automaticCursor is not null &&
            automaticCursor.Stochastic.TryGetSemanticHandDrawProgress(
                automaticCursor.Mechanics, out int requested,
                out int remaining))
        {
            automaticRequested = requested;
            automaticRemaining = remaining;
        }
        if (combat.PendingChoice is not null ||
            (automaticCursor is not null && !automatic) ||
            (automatic && (!fromHandDraw || automaticRemaining != count)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=nested_choice_or_automatic_pipeline");
        var suspensionPolicy =
            PrototypeDrawCardsSuspensionPolicyV111.SemanticDrawCards();
        var prototypeRng = run.Stochastic.PrototypeRng;
        var result = automatic
            ? ResumeAutomaticHandDrawCards(
                run, count, suspensionPolicy, prototypeRng)
            : DrawCards(
                run.Player, combat, count, prototypeRng, fromHandDraw,
                suspensionPolicy: suspensionPolicy);
        run = run with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                run.Stochastic, prototypeRng)
        };
        return ResumeSemanticCombatDrawResult(
            run, result, fromHandDraw, eventDepth: 0,
            automaticHandDrawRequested: automaticRequested,
            recycleSite: "combat.draw",
            recycleOperation: "unsupported_nested_stage");
    }

    internal static SemanticCombatDrawResumeResultV111
        ResumeSemanticAutomaticHandDraw(
            RunState checkpoint, int remainingCount)
    {
        using (ControlledChanceGuardV111.Enter())
            return BeginSemanticCombatDraw(
                checkpoint, remainingCount, fromHandDraw: true);
    }

    /// <summary>
    /// Continue a semantic card draw *after* the physical card entered hand.
    /// Snecko/relic cost runs first; Slither, if present, overrides it. Only
    /// after all cost choices does CardDrawn dispatch, exactly once. The
    /// subsequent draw reuses the same real DrawCards and semantic provider.
    /// </summary>
    internal static SemanticCombatDrawResumeResultV111
        ResumeSemanticCombatDrawCost(RunState run, int value)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic cost resume requires controlled chance guard.");
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("No semantic cost combat.");
        var deck = combat.Stochastic.SemanticDeck ??
            throw new InvalidOperationException("Semantic provider is absent.");
        SemanticDeckEngineV111.Validate(deck);
        var pending = combat.PendingChoice;
        var cost = pending?.ControlledDrawCost;
        bool automatic = IsSemanticAutomaticHandDraw(combat);
        if (cost is null || deck.Pending is not null ||
            (combat.AutomaticPipelineContinuation is not null && !automatic) ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cost.RemainingCount < 0 || cost.EventDepth != 0 ||
            (!automatic && !ValidateControlledDrawCost(
                run.Player, combat, cost with { FromHandDraw = true })))
            throw new InvalidDataException(
                "Semantic cost checkpoint/card identity/stage is inconsistent.");

        var draw = CreateSemanticDrawResultFrame(
            cost.RemainingCount, cost.FromHandDraw, cost.EventDepth,
            automatic);
        return ContinueSemanticDrawCost(
            run, cost, draw, value, continuation: null,
            resumeKind: automatic
                ? SemanticDrawCostResumeKindV111.AutomaticHandDraw
                : SemanticDrawCostResumeKindV111.Standalone,
            recycleSite: "combat.draw.cost",
            recycleOperation: "unsupported_post_cost_stage");
    }

    /// <summary>
    /// Shared post-cost continuation for automatic/standalone semantic draws
    /// and nested card-effect draws. The frame and caller policy determine
    /// which saved continuation owns any later hook or pipeline boundary.
    /// </summary>
    private static SemanticCombatDrawResumeResultV111
        ContinueSemanticDrawCost(
            RunState run,
            PrototypeControlledDrawCostV111 cost,
            PrototypeControlledSemanticDrawV111 draw,
            int selectedCost,
            PrototypeChoiceResolutionContinuationState? continuation,
            SemanticDrawCostResumeKindV111 resumeKind,
            string recycleSite,
            string recycleOperation)
    {
        bool automaticHandDraw =
            resumeKind == SemanticDrawCostResumeKindV111.AutomaticHandDraw;
        bool effectDraw =
            resumeKind == SemanticDrawCostResumeKindV111.CardEffect;
        if (!ControlledChanceGuardV111.Active ||
            cost.RemainingCount != draw.RemainingCount ||
            cost.FromHandDraw != draw.FromHandDraw ||
            cost.EventDepth != draw.EventDepth ||
            effectDraw != (continuation is not null) ||
            !Enum.IsDefined(resumeKind))
            throw new InvalidDataException(
                "Semantic draw-cost continuation frame is inconsistent.");

        if (automaticHandDraw)
        {
            var resumed = ResumeAutomaticHandDrawAfterCost(
                run, cost, selectedCost,
                PrototypeDrawCardsSuspensionPolicyV111.SemanticDrawCards(),
                allowHookSuspension: false,
                unsupportedBoundaryMessage:
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=nested_choice_continuation",
                semantic: true);
            if (resumed.Drawn is null)
                return new SemanticCombatDrawResumeResultV111(
                    resumed.Run, cost.RemainingCount);
            int automaticRequested = resumed.SemanticRequested ??
                throw new InvalidDataException(
                    "Semantic automatic draw resume lost its request.");

            return ResolveControlledSemanticDrawResult(
                resumed.Run, resumed.Drawn, draw, continuation,
                automaticRequested, recycleSite, recycleOperation);
        }

        var progress = ResolveDrawCostAndDispatchCardDrawn(
            run, cost, selectedCost,
            allowHookSuspension: true);
        if (progress.CostStagePending)
            return new SemanticCombatDrawResumeResultV111(
                progress.Run, cost.RemainingCount);

        run = progress.Run;
        var world = RequireWorld(run);
        var combat = world.Combat ?? throw new InvalidDataException(
            "Semantic cost resume removed combat.");
        if (progress.DrawnHookSuspended)
        {
            if (combat.PendingChoice is null)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=nested_choice_continuation");
            if (effectDraw)
            {
                var outerContinuation = continuation!;
                var resumeDraw = CreateDrawResumeOperation(
                    cost.RemainingCount,
                    draw.FromHandDraw,
                    draw.EventDepth,
                    draw.DrawnCardKeyword,
                    draw.DeferredKeywordCardInstanceIds,
                    drawEqualSelectionCount:
                        draw.DrawEqualSelectionCount);
                var combatWithDraw = AttachDrawResumeContinuation(
                    combat, resumeDraw, outerContinuation);
                return new SemanticCombatDrawResumeResultV111(
                    PublishDrawMechanicsState(
                        run, run.Player, combatWithDraw),
                    cost.RemainingCount);
            }
            return new SemanticCombatDrawResumeResultV111(
                SuspendStandaloneSemanticDrawAtHookChoice(
                    run, run.Player, combat, cost.RemainingCount,
                    cost.FromHandDraw, cost.EventDepth),
                cost.RemainingCount);
        }

        var prototypeRng = run.Stochastic.PrototypeRng;
        var drawn = DrawCards(
            run.Player, combat, cost.RemainingCount, prototypeRng,
            cost.FromHandDraw, eventDepth: cost.EventDepth,
            suspensionPolicy:
                PrototypeDrawCardsSuspensionPolicyV111.SemanticDrawCards(
                    effectDraw: effectDraw));
        run = run with
        {
            Stochastic = RunStochasticStateV111.WithPrototypeRng(
                run.Stochastic, prototypeRng)
        };
        var continued = ResolveControlledSemanticDrawResult(
            run, drawn, draw, continuation, automaticHandDrawRequested,
            recycleSite, recycleOperation);
        var result = effectDraw
            ? ResumeAutomaticPipelineContinuation(continued.Run)
            : continued.Run;
        return new SemanticCombatDrawResumeResultV111(
            result, continued.RemainingCount);
    }

    private static SemanticCombatDrawResumeResultV111
        ResumeSemanticCombatDrawResult(
            RunState run,
            PrototypeDrawCardsResult drawn,
            bool fromHandDraw,
            int eventDepth,
            int? automaticHandDrawRequested,
            string recycleSite,
            string recycleOperation)
    {
        var draw = CreateSemanticDrawResultFrame(
            drawn.RemainingCount, fromHandDraw, eventDepth,
            automaticHandDrawRequested is not null);
        return ResolveControlledSemanticDrawResult(
            run, drawn, draw, continuation: null,
            automaticHandDrawRequested, recycleSite, recycleOperation);
    }

    internal static (SemanticDrawProgressResumeResultV111 Progress,
        SemanticDrawHookChoiceDecisionV111 Decision)
        ResumeSemanticDrawHookChoice(
            RunState checkpoint,
            SemanticDrawHookChoiceEventV111 pending,
            string actionId,
            int requested,
            int remaining,
            int drawnBefore)
        => SemanticChoiceResumeV111.ResumeDrawHookChoice(
            checkpoint, pending, actionId, requested, remaining, drawnBefore);

    internal static (SemanticCombatDrawResumeResultV111 Progress,
        SemanticCardSelectionDecisionV111 Decision)
        ResumeSemanticStratagemSelection(
            RunState checkpoint,
            SemanticCardSelectionEventV111 pending,
            string actionId,
            int remaining,
            bool automaticHandDraw,
            int drawnBefore)
        => SemanticChoiceResumeV111.ResumeStratagemSelection(
            checkpoint, pending, actionId, remaining, automaticHandDraw,
            drawnBefore);

    private static PrototypeControlledSemanticDrawV111
        CreateSemanticDrawResultFrame(
            int remainingCount, bool fromHandDraw, int eventDepth,
            bool automatic)
    {
        return new PrototypeControlledSemanticDrawV111(
            remainingCount, fromHandDraw, eventDepth,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: false,
            StandaloneSemanticDrawSegment: !automatic,
            AutomaticSemanticHandDrawSegment: automatic);
    }

    private static SemanticCombatDrawResumeResultV111
        ResolveControlledSemanticDrawResult(
            RunState run,
            PrototypeDrawCardsResult drawn,
            PrototypeControlledSemanticDrawV111 draw,
            PrototypeChoiceResolutionContinuationState? continuation,
            int? automaticHandDrawRequested,
            string recycleSite,
            string recycleOperation)
    {
        var advanced = PublishDrawCardsResult(run, drawn);
        var boundary = drawn.BoundaryKind;
        bool automatic = automaticHandDrawRequested is not null;
        bool nestedEffect = continuation is not null;
        var outerContinuation = continuation ??
            EmptyDrawChoiceContinuation();
        if (boundary == PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions)
            return SuspendDrawCardsAfterShuffleInsertion(
                advanced, drawn, draw, outerContinuation,
                automaticHandDrawRequested,
                accumulateDrawnCardIds: nestedEffect);

        if (boundary ==
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice)
        {
            if (automatic)
                return new SemanticCombatDrawResumeResultV111(
                    SuspendAutomaticHandDrawAtCardDrawnChoice(
                        advanced, drawn.Player, drawn.Combat,
                        drawn.RemainingCount, automaticHandDrawRequested),
                    drawn.RemainingCount);
            if (nestedEffect)
                return new SemanticCombatDrawResumeResultV111(
                    SuspendDrawCardsAtCardDrawnChoice(
                        advanced, drawn.Player, drawn.Combat,
                        drawn.RemainingCount, draw, outerContinuation,
                        drawn.DrawnCardInstanceIds,
                        accumulateDrawnCardIds: true,
                        markStandaloneHookChoice: false),
                    drawn.RemainingCount);
            return new SemanticCombatDrawResumeResultV111(
                SuspendStandaloneSemanticDrawForHookChoice(
                    advanced, drawn, draw.FromHandDraw),
                drawn.RemainingCount);
        }

        if (boundary == PrototypeDrawCardsBoundaryKind.ControlledRecycle)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=" + recycleSite +
                " operation=" + recycleOperation);

        if (boundary == PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            return new SemanticCombatDrawResumeResultV111(
                SuspendDrawCardsForStratagemSelection(
                    advanced, drawn, draw, outerContinuation,
                    automaticHandDrawRequested,
                    accumulateDrawnCardIds: nestedEffect),
                drawn.RemainingCount);
        }

        if (nestedEffect)
        {
            var nextDraw = AdvanceControlledSemanticDrawFrame(
                draw, drawn.RemainingCount, drawn.DrawnCardInstanceIds,
                accumulateDrawnCardIds: true);
            var deferredKeywordIds =
                nextDraw.DeferredKeywordCardInstanceIds ?? [];
            var combat = drawn.Combat;
            if (boundary ==
                PrototypeDrawCardsBoundaryKind.ControlledDrawCost)
            {
                combat = AttachControlledSemanticDrawCost(
                    combat, nextDraw, outerContinuation);
                return new SemanticCombatDrawResumeResultV111(
                    PublishDrawMechanicsState(
                        advanced, advanced.Player, combat),
                    drawn.RemainingCount);
            }
            if (boundary == PrototypeDrawCardsBoundaryKind.SemanticDraw)
            {
                combat = MakeControlledSemanticDrawChoice(
                    combat, nextDraw, outerContinuation);
                return new SemanticCombatDrawResumeResultV111(
                    PublishDrawMechanicsState(
                        advanced, advanced.Player, combat),
                    drawn.RemainingCount);
            }
            if (drawn.RemainingCount != 0)
                throw new InvalidDataException(
                    "Semantic card-effect draw stopped without a pending boundary.");

            combat = ApplyDrawnCardKeywordOverride(
                combat, draw.DrawnCardKeyword, deferredKeywordIds);
            var prototypeRng = run.Stochastic.PrototypeRng;
            var resumed = ResumeChoiceResolutionContinuation(
                drawn.Player, combat, prototypeRng, outerContinuation);
            var resumedRun = run with
            {
                Stochastic = RunStochasticStateV111.WithPrototypeRng(
                    run.Stochastic, prototypeRng)
            };
            return new SemanticCombatDrawResumeResultV111(
                PublishDrawMechanicsState(
                    resumedRun, resumed.Player, resumed.Combat),
                0);
        }

        bool pending = boundary is
            PrototypeDrawCardsBoundaryKind.SemanticDraw or
                PrototypeDrawCardsBoundaryKind.ControlledDrawCost;
        if (automatic && pending &&
            !IsSemanticAutomaticHandDraw(drawn.Combat))
            return new SemanticCombatDrawResumeResultV111(
                SaveAutomaticSemanticHandDrawBoundary(
                    advanced, drawn, automaticHandDrawRequested!.Value),
                drawn.RemainingCount);
        return automatic
            ? CommitSemanticAutomaticHandDrawProgress(
                advanced, drawn.RemainingCount, pending)
            : new SemanticCombatDrawResumeResultV111(
                advanced, pending ? drawn.RemainingCount : 0);
    }

    private static RunState SaveAutomaticSemanticHandDrawBoundary(
        RunState run, PrototypeDrawCardsResult drawn, int requested)
    {
        var combat = drawn.Combat;
        var boundary = drawn.BoundaryKind;
        if (drawn.RemainingCount < 0 || requested <= 0 ||
            (boundary == PrototypeDrawCardsBoundaryKind.SemanticDraw &&
             combat.Stochastic.SemanticDeck?.Pending is null) ||
            (boundary == PrototypeDrawCardsBoundaryKind.ControlledDrawCost &&
             combat.PendingChoice?.ControlledDrawCost is null))
            throw new InvalidDataException(
                "Semantic hand-draw stage lost its suspended request.");
        return CommitAutomaticHandDrawProgress(
            run, drawn.Player, combat,
            PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                requested, drawn.RemainingCount),
            createCheckpoint: true);
    }

    private static SemanticCombatDrawResumeResultV111
        SuspendDrawCardsAfterShuffleInsertion(
            RunState run,
            PrototypeDrawCardsResult drawn,
            PrototypeControlledSemanticDrawV111 semanticDraw,
            PrototypeChoiceResolutionContinuationState continuation,
            int? automaticHandDrawRequested = null,
            bool accumulateDrawnCardIds = true)
    {
        if (drawn.BoundaryKind !=
                PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions ||
            drawn.RemainingCount <= 0 ||
            drawn.ControlledAfterShuffleInsertions is not
                { Length: > 0 } insertions)
            throw new InvalidDataException(
                "After-shuffle insertion lost its remaining draw boundary.");

        bool stratagemSelectionPending =
            semanticDraw.StratagemSelectionPending ||
            ExpectedStratagemSelectionCount(drawn.Combat) > 0;
        semanticDraw = AdvanceControlledSemanticDrawFrame(
            semanticDraw, drawn.RemainingCount,
            drawn.DrawnCardInstanceIds, accumulateDrawnCardIds,
            stratagemSelectionPending);

        var advanced = PublishDrawCardsResult(run, drawn);
        var insertionCombat = drawn.Combat;
        if (automaticHandDrawRequested is { } requested)
        {
            if (!semanticDraw.AutomaticSemanticHandDrawSegment ||
                !semanticDraw.FromHandDraw || semanticDraw.EventDepth != 0 ||
                requested <= 0)
                throw new InvalidDataException(
                    "Automatic after-shuffle draw cursor is inconsistent.");
            advanced = CommitAutomaticHandDrawProgress(
                advanced, drawn.Player, insertionCombat,
                PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                    requested, drawn.RemainingCount),
                createCheckpoint: true);
            insertionCombat = advanced.World!.Combat!;
        }

        var combat = MakeControlledAfterShuffleInsertionChoice(
            insertionCombat, insertions[0], insertions.Skip(1).ToArray(),
            [], semanticDraw, continuation, 0);
        return new SemanticCombatDrawResumeResultV111(
            PublishDrawMechanicsState(
                advanced, advanced.Player, combat),
            drawn.RemainingCount);
    }

    private static RunState SuspendDrawCardsForStratagemSelection(
        RunState run,
        PrototypeDrawCardsResult drawn,
        PrototypeControlledSemanticDrawV111 semanticDraw,
        PrototypeChoiceResolutionContinuationState continuation,
        int? automaticHandDrawRequested,
        bool accumulateDrawnCardIds)
    {
        if (drawn.BoundaryKind !=
                PrototypeDrawCardsBoundaryKind.ShuffleSelection ||
            drawn.RemainingCount <= 0 ||
            ExpectedStratagemSelectionCount(drawn.Combat) <= 0)
            throw new InvalidDataException(
                "Stratagem draw checkpoint is inconsistent.");

        semanticDraw = AdvanceControlledSemanticDrawFrame(
            semanticDraw, drawn.RemainingCount,
            drawn.DrawnCardInstanceIds, accumulateDrawnCardIds,
            stratagemSelectionPending: false);

        var combat = drawn.Combat;
        var advanced = PublishDrawCardsResult(run, drawn);
        var selected = ResumeControlledStratagemSelection(
            advanced, combat, semanticDraw, continuation);
        if (automaticHandDrawRequested is { } requested)
        {
            if (!semanticDraw.AutomaticSemanticHandDrawSegment ||
                !semanticDraw.FromHandDraw || semanticDraw.EventDepth != 0 ||
                requested <= 0)
                throw new InvalidDataException(
                    "Automatic Stratagem draw cursor is inconsistent.");
            var selectionWorld = RequireWorld(selected);
            var selectionCombat = selectionWorld.Combat ??
                throw new InvalidDataException(
                    "Automatic Stratagem selection removed combat.");
            selected = CommitAutomaticHandDrawStratagemSelection(
                selected, selected.Player, selectionCombat,
                drawn.RemainingCount, requested, createCheckpoint: true);
        }
        return selected;
    }

    private static PrototypeChoiceResolutionContinuationState
        EmptyDrawChoiceContinuation() =>
            new(
                SourceCardInstanceId: null,
                SourceCardDestination: PrototypeCardZone.DrawPile,
                Operations: [], PendingDiscardEvents: [],
                PendingSlyCardInstanceIds: [], CompletionEvents: []);

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

    private static RunState SuspendStandaloneSemanticDrawForHookChoice(
        RunState run, PrototypeDrawCardsResult drawn, bool fromHandDraw)
    {
        if (drawn.BoundaryKind !=
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice)
            throw new InvalidDataException(
                "Expected a nested CardDrawn choice result.");
        return SuspendStandaloneSemanticDrawAtHookChoice(
            run, drawn.Player, drawn.Combat, drawn.RemainingCount,
            fromHandDraw, eventDepth: 0);
    }

    private static RunState SuspendStandaloneSemanticDrawAtHookChoice(
        RunState run, PlayerState player, CombatState combat, int remaining,
        bool fromHandDraw, int eventDepth)
    {
        if (!ControlledChanceGuardV111.Active || remaining < 0 ||
            eventDepth < 0 || combat.PendingChoice is null ||
            combat.Stochastic.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null })
            throw new InvalidDataException(
                "Standalone CardDrawn choice lost its source continuation.");
        var draw = CreateSemanticDrawResultFrame(
            remaining, fromHandDraw, eventDepth, automatic: false);
        return SuspendDrawCardsAtCardDrawnChoice(
            run, player, combat, remaining, draw,
            EmptyDrawChoiceContinuation(), [],
            accumulateDrawnCardIds: false,
            markStandaloneHookChoice: true);
    }

    private static RunState SuspendDrawCardsAtCardDrawnChoice(
        RunState run,
        PlayerState player,
        CombatState combat,
        int remaining,
        PrototypeControlledSemanticDrawV111 draw,
        PrototypeChoiceResolutionContinuationState continuation,
        long[] drawnCardInstanceIds,
        bool accumulateDrawnCardIds,
        bool markStandaloneHookChoice)
    {
        var resumedDraw = AdvanceControlledSemanticDrawFrame(
            draw, remaining, drawnCardInstanceIds,
            accumulateDrawnCardIds);
        var resumeDraw = CreateDrawResumeOperation(
            resumedDraw.RemainingCount, resumedDraw.FromHandDraw,
            resumedDraw.EventDepth, resumedDraw.DrawnCardKeyword,
            resumedDraw.DeferredKeywordCardInstanceIds,
            resumedDraw.StandaloneSemanticDrawSegment,
            resumedDraw.DrawEqualSelectionCount);
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
        return PublishDrawMechanicsState(run, player, combat);
    }

    /// <summary>
    /// Resolve the draw cursor from the ordinary mechanics continuation.
    /// Standalone draw segments, suspended nested draws, and the automatic
    /// hand-draw pipeline all use the same saved mechanics state.
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
        if (savedCount is { } remaining)
            return remaining;
        if (IsSemanticAutomaticHandDraw(combat))
        {
            var cursor = combat.AutomaticPipelineContinuation!;
            if (!cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out _, out int remaining))
                throw new InvalidDataException(
                    "Semantic automatic draw lost its tagged progress.");
            return remaining;
        }
        if (combat.Stochastic.SemanticDeck?.Pending is not null)
            return Math.Max(0, previousRemaining -
                (combat.CounterState.CardsDrawnThisCombat - drawnBefore));
        return 0;
    }

    internal static int ExpectedStratagemSelectionCount(
        CombatState combat)
    {
        int stacks = combat.PlayerPowers
            .Where(power => power.PowerId == "proto.power.stratagem")
            .Sum(power => Math.Max(0, power.Stacks));
        return Math.Min(stacks,
            Math.Min(combat.DrawPile.Length,
                PrototypeContent.Rules.RemainingHandCapacity(
                    combat.Hand.Length)));
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
}
