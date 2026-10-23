namespace Sts2Emulator.Core;

/// <summary>
/// Shared mechanics for resuming the remaining cards in an automatic
/// DrawPlayerHand segment. Provider adapters choose which tagged boundaries
/// they can suspend and interpret the next result.
/// </summary>
public sealed partial class PrototypeGameEngine
{
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
            if (requested <= 0 || combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                choice!.SemanticStratagemDrawSelection ||
                !choice.AutomaticSemanticStratagemDrawSelection ||
                (cursor?.Stochastic.ControlledSemanticDrawRequested is { } savedRequested &&
                 savedRequested != requested) ||
                cursor?.Stochastic.ControlledDrawRemaining is not null ||
                cursor?.Stochastic.ControlledDrawCostRemaining is not null ||
                cursor?.Stochastic.ControlledSemanticBeforeHandDrawPending == true ||
                cursor?.Stochastic.ControlledBeforeHandDrawHookChoicePending == true)
                throw new InvalidDataException(
                    "Semantic automatic Stratagem cursor is inconsistent.");
            progress = PrototypeAutomaticHandDrawProgressV111.SemanticDraw(
                requested, remaining);
        }
        else
        {
            if (combat.Stochastic.SemanticDeck is not null ||
                choice!.AutomaticSemanticStratagemDrawSelection ||
                choice.SemanticStratagemDrawSelection ||
                cursor?.Stochastic.ControlledSemanticDrawRequested is not null ||
                cursor?.Stochastic.ControlledSemanticDrawRemaining is not null ||
                cursor?.Stochastic.ControlledSemanticBeforeHandDrawPending == true ||
                cursor?.Stochastic.ControlledBeforeHandDrawHookChoicePending == true)
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
            cursor.Stochastic.ControlledSemanticBeforeHandDrawPending ||
            cursor.Stochastic.ControlledBeforeHandDrawHookChoicePending)
            throw new InvalidDataException(
                "Automatic CardDrawn choice lost its hand-draw cursor.");

        PrototypeAutomaticHandDrawProgressV111 progress;
        if (semanticRequested is { } requested)
        {
            if (requested <= 0 || combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                (cursor.Stochastic.ControlledSemanticDrawRequested is { } savedRequested &&
                 savedRequested != requested) ||
                cursor.Stochastic.ControlledDrawRemaining is not null ||
                cursor.Stochastic.ControlledDrawCostRemaining is not null)
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
                cursor.Stochastic.ControlledSemanticDrawRequested is not null ||
                cursor.Stochastic.ControlledSemanticDrawRemaining is not null)
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

    private static int? ValidateAutomaticHandDrawCostCheckpoint(
        CombatState combat,
        PrototypeControlledDrawCostV111 cost,
        bool semantic)
    {
        var cursor = combat.AutomaticPipelineContinuation;
        if (!ControlledChanceGuardV111.Active || cost.RemainingCount < 0 ||
            !cost.FromHandDraw || cost.EventDepth != 0 ||
            combat.PendingKnowledgeDemonChoice is not null ||
            cursor is null || !cursor.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            !cursor.Stochastic.TryGetAutomaticHandDrawRemainder(
                cursor.Mechanics, out int remaining) ||
            remaining != cost.RemainingCount)
            throw new InvalidDataException(
                "Automatic draw-cost cursor does not match its saved count.");

        if (semantic)
        {
            if (combat.Stochastic.SemanticDeck is not
                    { Mode: SemanticDeckStateV111.NatureMode, Pending: null } ||
                !cursor.Stochastic.TryGetSemanticHandDrawProgress(
                    cursor.Mechanics, out int requested,
                    out int semanticRemaining) ||
                semanticRemaining != cost.RemainingCount)
                throw new InvalidDataException(
                    "Semantic automatic draw-cost cursor is inconsistent.");
            return requested;
        }

        if (combat.Stochastic.SemanticDeck is not null ||
            cursor.Stochastic.ControlledDrawCostRemaining !=
                cost.RemainingCount ||
            cursor.Stochastic.ControlledDrawRemaining is not null)
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
            !cursor.Stochastic.TryGetAutomaticHandDrawRemainder(
                cursor.Mechanics, out int savedRemaining) ||
            savedRemaining != remaining)
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
