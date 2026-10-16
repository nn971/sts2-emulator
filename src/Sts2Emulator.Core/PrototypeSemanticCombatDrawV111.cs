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
            controlledCardDrawnChoiceSuspension: !automatic);
        if (result.ControlledAfterShuffleInsertions is
            { Length: > 0 } afterShuffleInsertions)
        {
            var advancedAfterInsertion = run with
            {
                Player = result.Player,
                World = world with { Combat = result.Combat }
            };
            return SuspendSemanticDrawAfterShuffleInsertions(
                advancedAfterInsertion, result, afterShuffleInsertions,
                fromHandDraw, eventDepth: 0,
                automaticHandDrawRequested: automatic
                    ? combat.AutomaticPipelineContinuation!
                        .ControlledSemanticDrawRequested
                    : null);
        }
        if (result.NestedCardDrawnChoicePending)
        {
            if (automatic)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=automatic_nested_choice");
            var advancedNestedChoice = run with
            {
                Player = result.Player,
                World = world with { Combat = result.Combat }
            };
            return (SuspendStandaloneSemanticDrawForHookChoice(
                advancedNestedChoice, result, fromHandDraw),
                result.RemainingCount);
        }
        if (result.ControlledRecyclePending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw " +
                "operation=unsupported_nested_stage");
        if (result.ShuffleSelectionPending)
        {
            if (automatic)
            {
                var automaticSelectionRun = run with
                {
                    Player = result.Player,
                    World = world with { Combat = result.Combat }
                };
                return (SuspendSemanticAutomaticDrawForStratagemSelection(
                    automaticSelectionRun, result,
                    combat.AutomaticPipelineContinuation!
                        .ControlledSemanticDrawRequested!.Value),
                    result.RemainingCount);
            }
            var semanticDraw = new PrototypeControlledSemanticDrawV111(
                result.RemainingCount, fromHandDraw, 0,
                DrawnCardKeyword: null,
                DeferredKeywordCardInstanceIds: [],
                StratagemSelectionPending: false,
                StandaloneSemanticDrawSegment: true);
            var continuation = new PrototypeChoiceResolutionContinuationState(
                SourceCardInstanceId: null,
                SourceCardDestination: PrototypeCardZone.DrawPile,
                Operations: [], PendingDiscardEvents: [],
                PendingSlyCardInstanceIds: [], CompletionEvents: []);
            var advancedSelection = run with
            {
                Player = result.Player,
                World = world with { Combat = result.Combat }
            };
            var selectionRun = ResumeControlledStratagemSelection(
                advancedSelection, result.Combat, semanticDraw, continuation);
            return (selectionRun, result.RemainingCount);
        }
        var advanced = run with
        {
            Player = result.Player,
            World = world with { Combat = result.Combat }
        };
        bool pending = result.ControlledSemanticDrawPending ||
            result.ControlledDrawCostPending;
        return automatic
            ? CommitSemanticAutomaticHandDrawProgress(
                advanced, result.RemainingCount, pending)
            : (advanced, pending ? result.RemainingCount : 0);
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

        int bound = cost.Stage == "relic" ? cost.RelicMaxExclusive : 4;
        if (value < 0 || value >= bound)
            throw new ArgumentOutOfRangeException(nameof(value));
        combat = SetCardTemporaryEnergyCost(
            combat, cost.CardInstanceId,
            new PrototypeTemporaryCardCost(
                value, PrototypeTemporaryCardCostExpiry.None));
        if (cost.Stage == "relic" && cost.HasSlither)
        {
            combat = combat with
            {
                PendingChoice = pending! with
                {
                    ControlledDrawCost = cost with { Stage = "slither" }
                }
            };
            return (run with
            {
                World = world with { Combat = combat }
            }, cost.RemainingCount);
        }

        combat = combat with { PendingChoice = null };
        var card = RequireCombatCard(combat, cost.CardInstanceId);
        var dispatched = DispatchCombatEvent(
            run.Player, combat,
            new PrototypeCombatEvent(
                PrototypeCombatEventKind.CardDrawn,
                SourceCardInstanceId: cost.CardInstanceId,
                CardId: card.CardId,
                FromHandDraw: cost.FromHandDraw),
            run.Rng, cost.EventDepth,
            allowSuspension: !automatic);
        var player = dispatched.Player;
        combat = dispatched.Combat;
        if (combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null)
        {
            if (combat.PendingChoice is null || automatic)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=nested_choice_continuation");
            var hookRun = SuspendStandaloneSemanticDrawAtHookChoice(
                run, player, combat, cost.RemainingCount,
                cost.FromHandDraw, cost.EventDepth);
            return (hookRun, cost.RemainingCount);
        }

        var continued = DrawCards(
            player, combat, cost.RemainingCount, run.Rng,
            cost.FromHandDraw, eventDepth: cost.EventDepth,
            controlledSemanticDrawSuspension: true,
            controlledAfterShuffleInsertionSuspension: true,
            controlledCardDrawnChoiceSuspension: !automatic);
        if (continued.ControlledAfterShuffleInsertions is
            { Length: > 0 } afterShuffleInsertions)
        {
            var advancedAfterInsertion = run with
            {
                Player = continued.Player,
                World = world with { Combat = continued.Combat }
            };
            return SuspendSemanticDrawAfterShuffleInsertions(
                advancedAfterInsertion, continued, afterShuffleInsertions,
                cost.FromHandDraw, cost.EventDepth,
                automaticHandDrawRequested: automatic
                    ? combat.AutomaticPipelineContinuation!
                        .ControlledSemanticDrawRequested
                    : null);
        }
        if (continued.NestedCardDrawnChoicePending)
        {
            if (automatic)
                throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.CardDrawn " +
                    "operation=automatic_nested_choice");
            var advancedNestedChoice = run with
            {
                Player = continued.Player,
                World = world with { Combat = continued.Combat }
            };
            return (SuspendStandaloneSemanticDrawForHookChoice(
                advancedNestedChoice, continued, cost.FromHandDraw),
                continued.RemainingCount);
        }
        if (continued.ControlledRecyclePending)
            throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_DECK site=combat.draw.cost " +
                "operation=unsupported_post_cost_stage");
        if (continued.ShuffleSelectionPending)
        {
            if (automatic)
            {
                var automaticSelectionRun = run with
                {
                    Player = continued.Player,
                    World = world with { Combat = continued.Combat }
                };
                return (SuspendSemanticAutomaticDrawForStratagemSelection(
                    automaticSelectionRun, continued,
                    combat.AutomaticPipelineContinuation!
                        .ControlledSemanticDrawRequested!.Value),
                    continued.RemainingCount);
            }
            var semanticDraw = new PrototypeControlledSemanticDrawV111(
                continued.RemainingCount, cost.FromHandDraw,
                cost.EventDepth, DrawnCardKeyword: null,
                DeferredKeywordCardInstanceIds: [],
                StratagemSelectionPending: false,
                StandaloneSemanticDrawSegment: true);
            var continuation = new PrototypeChoiceResolutionContinuationState(
                SourceCardInstanceId: null,
                SourceCardDestination: PrototypeCardZone.DrawPile,
                Operations: [], PendingDiscardEvents: [],
                PendingSlyCardInstanceIds: [], CompletionEvents: []);
            var advancedSelection = run with
            {
                Player = continued.Player,
                World = world with { Combat = continued.Combat }
            };
            var selectionRun = ResumeControlledStratagemSelection(
                advancedSelection, continued.Combat, semanticDraw,
                continuation);
            return (selectionRun, continued.RemainingCount);
        }
        var advanced = run with
        {
            Player = continued.Player,
            World = world with { Combat = continued.Combat }
        };
        bool hasPending = continued.ControlledSemanticDrawPending ||
            continued.ControlledDrawCostPending;
        return automatic
            ? CommitSemanticAutomaticHandDrawProgress(
                advanced, continued.RemainingCount, hasPending)
            : (advanced, hasPending ? continued.RemainingCount : 0);
    }

    private static (RunState Run, int RemainingCount)
        SuspendSemanticDrawAfterShuffleInsertions(
            RunState run,
            PrototypeDrawCardsResult drawn,
            PrototypeAfterShuffleRelicInsertionV111[] insertions,
            bool fromHandDraw,
            int eventDepth,
            int? automaticHandDrawRequested = null)
    {
        if (insertions.Length == 0 || drawn.RemainingCount <= 0)
            throw new InvalidDataException(
                "After-shuffle insertion lost its remaining draw cursor.");
        bool stratagemSelectionPending = drawn.Combat.PlayerPowers.Any(power =>
            power.PowerId == "proto.power.stratagem" &&
            power.Stacks > 0) && drawn.Combat.Hand.Length < 10;
        bool automatic = automaticHandDrawRequested is not null;
        if (automatic && (!fromHandDraw || eventDepth != 0 ||
                          automaticHandDrawRequested is not > 0))
            throw new InvalidDataException(
                "Automatic after-shuffle draw cursor is inconsistent.");
        var semanticDraw = new PrototypeControlledSemanticDrawV111(
            drawn.RemainingCount, fromHandDraw, eventDepth,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: stratagemSelectionPending,
            StandaloneSemanticDrawSegment: !automatic,
            AutomaticSemanticHandDrawSegment: automatic);
        var continuation = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DrawPile,
            Operations: [],
            PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [],
            CompletionEvents: []);
        var insertionCombat = drawn.Combat;
        if (automatic)
        {
            int stepIndex = SemanticHandDrawStepIndex;
            if (stepIndex < 0 ||
                (insertionCombat.AutomaticPipelineContinuation is { } cursor &&
                 cursor.NextStepIndex != stepIndex))
                throw new InvalidDataException(
                    "Automatic after-shuffle insertion left DrawPlayerHand.");
            insertionCombat = insertionCombat with
            {
                AutomaticPipelineContinuation =
                    new PrototypeAutomaticPipelineContinuationState(
                        stepIndex,
                        ControlledSemanticDrawRemaining: drawn.RemainingCount,
                        ControlledSemanticDrawRequested:
                            automaticHandDrawRequested)
            };
        }
        var combat = MakeControlledAfterShuffleInsertionChoice(
            insertionCombat, insertions[0], insertions.Skip(1).ToArray(),
            [], semanticDraw, continuation, 0);
        return (run with
        {
            Player = drawn.Player,
            World = run.World! with { Combat = combat }
        }, drawn.RemainingCount);
    }

    private static RunState SuspendSemanticAutomaticDrawForStratagemSelection(
        RunState run, PrototypeDrawCardsResult drawn, int requested)
    {
        int stepIndex = SemanticHandDrawStepIndex;
        if (stepIndex < 0 || requested <= 0 ||
            drawn.RemainingCount <= 0 ||
            !drawn.Combat.PlayerPowers.Any(power =>
                power.PowerId == "proto.power.stratagem" &&
                power.Stacks > 0 && drawn.Combat.Hand.Length < 10))
            throw new InvalidDataException(
                "Automatic Stratagem draw checkpoint is inconsistent.");
        var combat = drawn.Combat with
        {
            AutomaticPipelineContinuation =
                new PrototypeAutomaticPipelineContinuationState(
                    stepIndex,
                    ControlledSemanticDrawRemaining: drawn.RemainingCount,
                    ControlledSemanticDrawRequested: requested)
        };
        var draw = new PrototypeControlledSemanticDrawV111(
            drawn.RemainingCount, FromHandDraw: true, EventDepth: 0,
            DrawnCardKeyword: null,
            DeferredKeywordCardInstanceIds: [],
            StratagemSelectionPending: false,
            AutomaticSemanticHandDrawSegment: true);
        var continuation = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DrawPile,
            Operations: [], PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [], CompletionEvents: []);
        var advanced = run with
        {
            Player = drawn.Player,
            World = run.World! with { Combat = combat }
        };
        return ResumeControlledStratagemSelection(
            advanced, combat, draw, continuation);
    }

    private static RunState SuspendStandaloneSemanticDrawForHookChoice(
        RunState run, PrototypeDrawCardsResult drawn, bool fromHandDraw)
    {
        if (!drawn.NestedCardDrawnChoicePending)
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
        var resumeDraw = CreateDrawResumeOperation(
            remaining, fromHandDraw, eventDepth,
            drawnCardKeyword: null,
            deferredDrawnCardInstanceIds: [],
            standaloneSemanticDrawSegment: true);
        var continuation = new PrototypeChoiceResolutionContinuationState(
            SourceCardInstanceId: null,
            SourceCardDestination: PrototypeCardZone.DrawPile,
            Operations: [resumeDraw], PendingDiscardEvents: [],
            PendingSlyCardInstanceIds: [], CompletionEvents: []);
        combat = AttachDrawResumeContinuation(
            combat, resumeDraw, continuation);
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
}
