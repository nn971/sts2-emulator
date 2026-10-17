namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in initial semantic combat draw bridge. Uses the *real* DrawCards
/// kernel; its isolated fixture envelope is distinct from the legacy
/// opening Fisher-Yates controlled-playable state and transcript.
/// </summary>
public sealed partial class PrototypeGameEngine
{
    internal static (RunState Run, int RemainingCount)
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
        if (combat.SemanticDeck is null)
            throw new InvalidOperationException("Missing semantic deck provider.");
        bool automatic = IsSemanticAutomaticHandDraw(combat);
        if (combat.PendingChoice is not null ||
            (combat.AutomaticPipelineContinuation is not null && !automatic) ||
            (automatic && (!fromHandDraw ||
                           combat.AutomaticPipelineContinuation!
                               .ControlledSemanticDrawRemaining != count)))
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=nested_choice_or_automatic_pipeline");
        var result = DrawCards(
            run.Player, combat, count, run.Rng, fromHandDraw,
            controlledSemanticDrawSuspension: true,
            controlledAfterShuffleInsertionSuspension: true,
            controlledCardDrawnChoiceSuspension: true);
        return ResumeSemanticCombatDrawResult(
            run, result, fromHandDraw, eventDepth: 0,
            automaticHandDrawRequested: automatic
                ? combat.AutomaticPipelineContinuation!
                    .ControlledSemanticDrawRequested
                : null,
            recycleSite: "combat.draw",
            recycleOperation: "unsupported_nested_stage");
    }

    /// <summary>
    /// Continue a semantic card draw *after* the physical card entered hand.
    /// Snecko/relic cost runs first; Slither, if present, overrides it. Only
    /// after all cost choices does CardDrawn dispatch, exactly once. The
    /// subsequent draw reuses the same real DrawCards and semantic provider.
    /// </summary>
    internal static (RunState Run, int RemainingCount)
        ResumeSemanticCombatDrawCost(RunState run, int value)
    {
        if (!ControlledChanceGuardV111.Active)
            throw new InvalidOperationException(
                "Semantic cost resume requires controlled chance guard.");
        var world = RequireWorld(run);
        var combat = world.Combat ??
            throw new InvalidOperationException("No semantic cost combat.");
        var deck = combat.SemanticDeck ??
            throw new InvalidOperationException("Semantic provider is absent.");
        SemanticDeckEngineV111.Validate(deck);
        var pending = combat.PendingChoice;
        var cost = pending?.ControlledDrawCost;
        bool automatic = IsSemanticAutomaticHandDraw(combat);
        if (deck.Pending is not null ||
            (combat.AutomaticPipelineContinuation is not null && !automatic) ||
            (automatic && (
                !cost!.FromHandDraw ||
                combat.AutomaticPipelineContinuation!
                    .ControlledSemanticDrawRemaining != cost.RemainingCount)) ||
            cost is null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cost.RemainingCount < 0 || cost.EventDepth != 0 ||
            !ValidateControlledDrawCost(
                run.Player, combat, cost with { FromHandDraw = true }))
            throw new InvalidDataException(
                "Semantic cost checkpoint/card identity/stage is inconsistent.");

        // Cost selection and its CardDrawn mechanics are shared with nested
        // effect draws; the facade below only chooses the saved resume frame.
        var progress = ResolveDrawCostAndDispatchCardDrawn(
            run, cost, value, allowHookSuspension: !automatic);
        if (progress.CostStagePending)
            return (progress.Run, cost.RemainingCount);
        run = progress.Run;
        world = RequireWorld(run);
        combat = world.Combat ?? throw new InvalidDataException(
            "Semantic cost resume removed combat.");
        if (progress.DrawnHookSuspended)
        {
            if (combat.PendingChoice is null)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=nested_choice_continuation");
            if (automatic)
                return (SuspendSemanticAutomaticDrawAtHookChoice(
                    run, run.Player, combat, cost.RemainingCount,
                    combat.AutomaticPipelineContinuation!
                        .ControlledSemanticDrawRequested!.Value),
                    cost.RemainingCount);
            var hookRun = SuspendStandaloneSemanticDrawAtHookChoice(
                run, run.Player, combat, cost.RemainingCount,
                cost.FromHandDraw, cost.EventDepth);
            return (hookRun, cost.RemainingCount);
        }

        var continued = DrawCards(
            run.Player, combat, cost.RemainingCount, run.Rng,
            cost.FromHandDraw, eventDepth: cost.EventDepth,
            controlledSemanticDrawSuspension: true,
            controlledAfterShuffleInsertionSuspension: true,
            controlledCardDrawnChoiceSuspension: true);
        return ResumeSemanticCombatDrawResult(
            run, continued, cost.FromHandDraw, cost.EventDepth,
            automaticHandDrawRequested: automatic
                ? combat.AutomaticPipelineContinuation!
                    .ControlledSemanticDrawRequested
                : null,
            recycleSite: "combat.draw.cost",
            recycleOperation: "unsupported_post_cost_stage");
    }

    private static (RunState Run, int RemainingCount)
        ResumeSemanticCombatDrawResult(
            RunState run,
            PrototypeDrawCardsResult drawn,
            bool fromHandDraw,
            int eventDepth,
            int? automaticHandDrawRequested,
            string recycleSite,
            string recycleOperation)
    {
        var world = RequireWorld(run);
        var advanced = run with
        {
            Player = drawn.Player,
            World = world with { Combat = drawn.Combat }
        };
        var boundary = drawn.BoundaryKind;
        bool automatic = automaticHandDrawRequested is not null;
        if (boundary == PrototypeDrawCardsBoundaryKind.AfterShuffleInsertions)
            return SuspendSemanticDrawAfterShuffleInsertions(
                advanced, drawn, fromHandDraw,
                eventDepth, automaticHandDrawRequested);

        if (boundary ==
            PrototypeDrawCardsBoundaryKind.NestedCardDrawnChoice)
        {
            if (automatic)
                return (SuspendSemanticAutomaticDrawAtHookChoice(
                    advanced, drawn.Player, drawn.Combat,
                    drawn.RemainingCount, automaticHandDrawRequested!.Value),
                    drawn.RemainingCount);
            return (SuspendStandaloneSemanticDrawForHookChoice(
                advanced, drawn, fromHandDraw), drawn.RemainingCount);
        }

        if (boundary == PrototypeDrawCardsBoundaryKind.ControlledRecycle)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=" + recycleSite +
                " operation=" + recycleOperation);

        if (boundary == PrototypeDrawCardsBoundaryKind.ShuffleSelection)
        {
            if (automatic)
                return (SuspendSemanticAutomaticDrawForStratagemSelection(
                    advanced, drawn, automaticHandDrawRequested!.Value),
                    drawn.RemainingCount);
            var semanticDraw = new PrototypeControlledSemanticDrawV111(
                drawn.RemainingCount, fromHandDraw, eventDepth,
                DrawnCardKeyword: null,
                DeferredKeywordCardInstanceIds: [],
                StratagemSelectionPending: false,
                StandaloneSemanticDrawSegment: true);
            return (SuspendDrawCardsForStratagemSelection(
                advanced, drawn, semanticDraw,
                EmptyDrawChoiceContinuation(),
                automaticHandDrawRequested: null,
                accumulateDrawnCardIds: false), drawn.RemainingCount);
        }

        bool pending = boundary is
            PrototypeDrawCardsBoundaryKind.SemanticDraw or
                PrototypeDrawCardsBoundaryKind.ControlledDrawCost;
        return automatic
            ? CommitSemanticAutomaticHandDrawProgress(
                advanced, drawn.RemainingCount, pending)
            : (advanced, pending ? drawn.RemainingCount : 0);
    }

    private static (RunState Run, int RemainingCount)
        SuspendSemanticDrawAfterShuffleInsertions(
            RunState run,
            PrototypeDrawCardsResult drawn,
            bool fromHandDraw,
            int eventDepth,
            int? automaticHandDrawRequested = null)
    {
        bool automatic = automaticHandDrawRequested is not null;
        if (automatic && (!fromHandDraw || eventDepth != 0 ||
                          automaticHandDrawRequested is not > 0))
            throw new InvalidDataException(
                "Automatic after-shuffle draw cursor is inconsistent.");
        var semanticDraw = new PrototypeControlledSemanticDrawV111(
            drawn.RemainingCount, fromHandDraw, eventDepth,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: false,
            StandaloneSemanticDrawSegment: !automatic,
            AutomaticSemanticHandDrawSegment: automatic);
        return SuspendDrawCardsAfterShuffleInsertion(
            run, drawn, semanticDraw, EmptyDrawChoiceContinuation(),
            automaticHandDrawRequested,
            accumulateDrawnCardIds: false);
    }

    private static (RunState Run, int RemainingCount)
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

        var deferredKeywordIds = accumulateDrawnCardIds
            ? (semanticDraw.DeferredKeywordCardInstanceIds ?? [])
                .Concat(drawn.DrawnCardInstanceIds).Distinct().ToArray()
            : semanticDraw.DeferredKeywordCardInstanceIds;
        semanticDraw = semanticDraw with
        {
            RemainingCount = drawn.RemainingCount,
            DeferredKeywordCardInstanceIds = deferredKeywordIds,
            StratagemSelectionPending =
                semanticDraw.StratagemSelectionPending ||
                (drawn.Combat.PlayerPowers.Any(power =>
                    power.PowerId == "proto.power.stratagem" &&
                    power.Stacks > 0) && drawn.Combat.Hand.Length < 10)
        };

        var insertionCombat = drawn.Combat;
        if (automaticHandDrawRequested is { } requested)
        {
            int stepIndex = SemanticHandDrawStepIndex;
            if (!semanticDraw.AutomaticSemanticHandDrawSegment ||
                !semanticDraw.FromHandDraw || semanticDraw.EventDepth != 0 ||
                requested <= 0 || stepIndex < 0 ||
                (insertionCombat.AutomaticPipelineContinuation is { } cursor &&
                 cursor.NextStepIndex != stepIndex))
                throw new InvalidDataException(
                    "Automatic after-shuffle draw cursor is inconsistent.");
            insertionCombat = insertionCombat with
            {
                AutomaticPipelineContinuation =
                    new PrototypeAutomaticPipelineContinuationState(
                        stepIndex,
                        ControlledSemanticDrawRemaining:
                            drawn.RemainingCount,
                        ControlledSemanticDrawRequested: requested)
            };
        }

        var combat = MakeControlledAfterShuffleInsertionChoice(
            insertionCombat, insertions[0], insertions.Skip(1).ToArray(),
            [], semanticDraw, continuation, 0);
        var world = RequireWorld(run);
        return (run with
        {
            Player = drawn.Player,
            World = world with { Combat = combat }
        }, drawn.RemainingCount);
    }

    private static RunState SuspendSemanticAutomaticDrawForStratagemSelection(
        RunState run, PrototypeDrawCardsResult drawn, int requested)
    {
        var draw = new PrototypeControlledSemanticDrawV111(
            drawn.RemainingCount, FromHandDraw: true, EventDepth: 0,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: false,
            AutomaticSemanticHandDrawSegment: true);
        var continuation = EmptyDrawChoiceContinuation();
        return SuspendDrawCardsForStratagemSelection(
            run, drawn, draw, continuation, requested,
            accumulateDrawnCardIds: false);
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
            !drawn.Combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.stratagem" &&
                power.Stacks > 0 && drawn.Combat.Hand.Length < 10))
            throw new InvalidDataException(
                "Stratagem draw checkpoint is inconsistent.");

        var deferredKeywordIds = accumulateDrawnCardIds
            ? (semanticDraw.DeferredKeywordCardInstanceIds ?? [])
                .Concat(drawn.DrawnCardInstanceIds).Distinct().ToArray()
            : semanticDraw.DeferredKeywordCardInstanceIds;
        semanticDraw = semanticDraw with
        {
            RemainingCount = drawn.RemainingCount,
            DeferredKeywordCardInstanceIds = deferredKeywordIds,
            StratagemSelectionPending = false
        };

        var combat = drawn.Combat;
        if (automaticHandDrawRequested is { } requested)
        {
            int stepIndex = SemanticHandDrawStepIndex;
            if (!semanticDraw.AutomaticSemanticHandDrawSegment ||
                !semanticDraw.FromHandDraw || semanticDraw.EventDepth != 0 ||
                requested <= 0 || stepIndex < 0 ||
                (combat.AutomaticPipelineContinuation is { } cursor &&
                 cursor.NextStepIndex != stepIndex))
                throw new InvalidDataException(
                    "Automatic Stratagem draw cursor is inconsistent.");
            combat = combat with
            {
                AutomaticPipelineContinuation =
                    new PrototypeAutomaticPipelineContinuationState(
                        stepIndex,
                        ControlledSemanticDrawRemaining:
                            drawn.RemainingCount,
                        ControlledSemanticDrawRequested: requested)
            };
        }

        var advanced = run with
        {
            Player = drawn.Player,
            World = RequireWorld(run) with { Combat = combat }
        };
        return ResumeControlledStratagemSelection(
            advanced, combat, semanticDraw, continuation);
    }

    private static PrototypeChoiceResolutionContinuationState
        EmptyDrawChoiceContinuation() =>
            new(
                SourceCardInstanceId: null,
                SourceCardDestination: PrototypeCardZone.DrawPile,
                Operations: [], PendingDiscardEvents: [],
                PendingSlyCardInstanceIds: [], CompletionEvents: []);

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
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null })
            throw new InvalidDataException(
                "Standalone CardDrawn choice lost its source continuation.");
        var draw = new PrototypeControlledSemanticDrawV111(
            remaining, fromHandDraw, eventDepth,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: false,
            StandaloneSemanticDrawSegment: true);
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
        var deferredKeywordIds = accumulateDrawnCardIds
            ? (draw.DeferredKeywordCardInstanceIds ?? [])
                .Concat(drawnCardInstanceIds).Distinct().ToArray()
            : draw.DeferredKeywordCardInstanceIds;
        var resumeDraw = CreateDrawResumeOperation(
            remaining, draw.FromHandDraw, draw.EventDepth,
            draw.DrawnCardKeyword, deferredKeywordIds,
            draw.StandaloneSemanticDrawSegment);
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
        return run with
        {
            Player = player,
            World = run.World! with { Combat = combat }
        };
    }

    internal static RunState SuspendSemanticAutomaticDrawAtHookChoice(
        RunState run, PlayerState player, CombatState combat,
        int remaining, int requested)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        if (!ControlledChanceGuardV111.Active || remaining < 0 ||
            requested <= 0 || combat.PendingChoice is null ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode,
                    Pending: null } ||
            cursor?.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) != true ||
            (cursor.ControlledSemanticDrawRequested is { } savedRequested &&
             savedRequested != requested) ||
            cursor.ControlledSemanticBeforeHandDrawPending ||
            cursor.ControlledDrawRemaining is not null ||
            cursor.ControlledDrawCostRemaining is not null ||
            cursor.OpeningStage is not null ||
            cursor.PendingPostDispatchEventKind is not null)
            throw new InvalidDataException(
                "Automatic CardDrawn choice lost its hand-draw cursor.");
        combat = combat with
        {
            AutomaticPipelineContinuation = cursor with
            {
                ControlledSemanticDrawRemaining = remaining,
                ControlledSemanticDrawRequested = requested
            },
            PendingChoice = combat.PendingChoice with
            {
                AutomaticSemanticDrawHookChoice = true
            }
        };
        var suspended = run with
        {
            Player = player,
            World = run.World! with { Combat = combat }
        };
        PrototypeStateInvariants.Validate(suspended);
        return suspended;
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
            return combat.AutomaticPipelineContinuation!
                .ControlledSemanticDrawRemaining!.Value;
        if (combat.SemanticDeck?.Pending is not null)
            return Math.Max(0, previousRemaining -
                (combat.CounterState.CardsDrawnThisCombat - drawnBefore));
        return 0;
    }

    internal static int ExpectedSemanticStratagemSelectionCount(
        CombatState combat)
    {
        int stacks = combat.PlayerPowers
            .Where(power => power.PowerId == "proto.power.stratagem")
            .Sum(power => Math.Max(0, power.Stacks));
        return Math.Min(stacks,
            Math.Min(combat.DrawPile.Length, Math.Max(0, 10 - combat.Hand.Length)));
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
