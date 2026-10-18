namespace Sts2Emulator.Core;

public enum PrototypeAutomaticHandDrawProgressKindV111
{
    ControlledDraw,
    ControlledDrawCost,
    SemanticBeforeHandDraw,
    SemanticDraw,
    ControlledRecycleStratagem,
    ControlledDrawHookChoice,
    ControlledBeforeHandDrawHookChoice
}

/// <summary>One active DrawPlayerHand progress mode in the ordinary pipeline.
/// The legacy flattened counters are projections of this discriminated state.
/// </summary>
public sealed record PrototypeAutomaticHandDrawProgressV111(
    PrototypeAutomaticHandDrawProgressKindV111 Kind,
    int? RequestedCount = null,
    int? RemainingCount = null)
{
    public static PrototypeAutomaticHandDrawProgressV111 ControlledDraw(
        int remainingCount) => Create(
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw,
            requestedCount: null, remainingCount: remainingCount);

    public static PrototypeAutomaticHandDrawProgressV111
        ControlledDrawHookChoice(int remainingCount) => Create(
            PrototypeAutomaticHandDrawProgressKindV111
                .ControlledDrawHookChoice,
            requestedCount: null, remainingCount: remainingCount);

    public static PrototypeAutomaticHandDrawProgressV111 ControlledDrawCost(
        int remainingCount) => Create(
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost,
            requestedCount: null, remainingCount: remainingCount);

    public static PrototypeAutomaticHandDrawProgressV111 SemanticBeforeHandDraw() =>
        Create(
            PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw,
            requestedCount: null, remainingCount: null);

    public static PrototypeAutomaticHandDrawProgressV111
        ControlledBeforeHandDrawHookChoice() => Create(
            PrototypeAutomaticHandDrawProgressKindV111
                .ControlledBeforeHandDrawHookChoice,
            requestedCount: null, remainingCount: null);

    public static PrototypeAutomaticHandDrawProgressV111 SemanticDraw(
        int requestedCount, int remainingCount) => Create(
            PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw,
            requestedCount, remainingCount);

    public static PrototypeAutomaticHandDrawProgressV111
        ControlledRecycleStratagem(int remainingCount) => Create(
            PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem,
            requestedCount: null, remainingCount: remainingCount);

    public void Validate()
    {
        if (!Enum.IsDefined(Kind))
            throw new InvalidOperationException(
                "Automatic hand-draw progress has an unknown mode.");
        switch (Kind)
        {
            case PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw:
            case PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice:
            case PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost:
                if (RequestedCount is not null || RemainingCount is not >= 0)
                    throw new InvalidOperationException(
                        "Controlled automatic hand-draw progress is malformed.");
                break;
            case PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw:
            case PrototypeAutomaticHandDrawProgressKindV111.ControlledBeforeHandDrawHookChoice:
                if (RequestedCount is not null || RemainingCount is not null)
                    throw new InvalidOperationException(
                        "BeforeHandDraw hook progress cannot contain draw counts.");
                break;
            case PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw:
                if (RequestedCount is not > 0 || RemainingCount is not >= 0)
                    throw new InvalidOperationException(
                        "Semantic automatic hand-draw progress is malformed.");
                break;
            case PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem:
                if (RequestedCount is not null || RemainingCount is not > 0)
                    throw new InvalidOperationException(
                        "Controlled recycle Stratagem progress is malformed.");
                break;
            default:
                throw new InvalidOperationException(
                    "Automatic hand-draw progress has an unsupported mode.");
        }
    }

    private static PrototypeAutomaticHandDrawProgressV111 Create(
        PrototypeAutomaticHandDrawProgressKindV111 kind,
        int? requestedCount, int? remainingCount)
    {
        var progress = new PrototypeAutomaticHandDrawProgressV111(
            kind, requestedCount, remainingCount);
        progress.Validate();
        return progress;
    }
}

/// <summary>
/// Saved stochastic cursors and provider decisions for one automatic combat
/// pipeline. The continuation's legacy properties remain the snapshot wire
/// projection during migration.
/// </summary>
public sealed record PrototypeAutomaticPipelineStochasticStateV111(
    PrototypeAutomaticHandDrawProgressV111? HandDrawProgress = null,
    PrototypeControlledEnemyActionContinuationV111? ControlledEnemyAction = null,
    ControlledTurnIntentsV111? ControlledTurnIntents = null)
{
    public int? ControlledDrawRemaining
    {
        get => HandDrawProgress?.Kind is
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice or
            PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem
                ? HandDrawProgress.RemainingCount : null;
        init => HandDrawProgress = SetControlledDrawRemaining(
            HandDrawProgress, value);
    }

    public bool ControlledDrawHookChoicePending
    {
        get => HandDrawProgress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice;
        init => HandDrawProgress = SetControlledDrawHookChoicePending(
            HandDrawProgress, value);
    }

    public bool ControlledBeforeHandDrawHookChoicePending
    {
        get => HandDrawProgress?.Kind == PrototypeAutomaticHandDrawProgressKindV111
            .ControlledBeforeHandDrawHookChoice;
        init => HandDrawProgress = SetControlledBeforeHandDrawHookChoicePending(
            HandDrawProgress, value);
    }

    public int? ControlledDrawCostRemaining
    {
        get => HandDrawProgress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost
                ? HandDrawProgress.RemainingCount : null;
        init => HandDrawProgress = SetControlledDrawCostRemaining(
            HandDrawProgress, value);
    }

    public int? ControlledSemanticDrawRemaining
    {
        get => HandDrawProgress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw
                ? HandDrawProgress.RemainingCount : null;
        init => HandDrawProgress = SetSemanticDrawRemaining(
            HandDrawProgress, value);
    }

    public int? ControlledSemanticDrawRequested
    {
        get => HandDrawProgress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw
                ? HandDrawProgress.RequestedCount : null;
        init => HandDrawProgress = SetSemanticDrawRequested(
            HandDrawProgress, value);
    }

    public bool ControlledSemanticBeforeHandDrawPending
    {
        get => HandDrawProgress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw;
        init => HandDrawProgress = SetSemanticBeforeHandDraw(
            HandDrawProgress, value);
    }

    public bool ControlledRecycleStratagemPending
    {
        get => HandDrawProgress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem;
        init => HandDrawProgress = SetControlledRecycleStratagem(
            HandDrawProgress, value);
    }

    public bool TryGetSemanticHandDrawProgress(
        PrototypeAutomaticPipelineMechanicsStateV111 mechanics,
        out int requested, out int remaining)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        requested = 0;
        remaining = 0;
        if (ControlledSemanticBeforeHandDrawPending ||
            ControlledSemanticDrawRequested is not > 0 ||
            ControlledSemanticDrawRemaining is not { } savedRemaining ||
            savedRemaining < 0 ||
            !mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            ControlledDrawRemaining is not null ||
            ControlledDrawCostRemaining is not null ||
            mechanics.OpeningStage is not null ||
            mechanics.PendingPostDispatchEventKind is not null)
            return false;
        requested = ControlledSemanticDrawRequested.Value;
        remaining = savedRemaining;
        return true;
    }

    public PrototypeAutomaticPipelineStochasticStateV111 Fork() => this with
    {
        HandDrawProgress = HandDrawProgress is null ? null :
            HandDrawProgress with { },
        ControlledEnemyAction = ControlledEnemyAction?.Fork(),
        ControlledTurnIntents = ControlledTurnIntents?.Fork()
    };

    public static PrototypeAutomaticPipelineStochasticStateV111
        FromFlattenedProgress(
            int? controlledDrawRemaining,
            int? controlledDrawCostRemaining,
            int? controlledSemanticDrawRemaining,
            int? controlledSemanticDrawRequested,
            bool controlledSemanticBeforeHandDrawPending,
            bool controlledRecycleStratagemPending,
            bool controlledDrawHookChoicePending,
            bool controlledBeforeHandDrawHookChoicePending,
            PrototypeControlledEnemyActionContinuationV111? controlledEnemyAction,
            ControlledTurnIntentsV111? controlledTurnIntents)
    {
        ValidateFlattenedProgress(
            controlledDrawRemaining,
            controlledDrawCostRemaining,
            controlledSemanticDrawRemaining,
            controlledSemanticDrawRequested,
            controlledSemanticBeforeHandDrawPending,
            controlledRecycleStratagemPending,
            controlledDrawHookChoicePending,
            controlledBeforeHandDrawHookChoicePending);

        PrototypeAutomaticHandDrawProgressV111? progress = null;
        if (controlledDrawRemaining is { } drawRemaining)
        {
            progress = new(
                controlledDrawHookChoicePending
                    ? PrototypeAutomaticHandDrawProgressKindV111
                        .ControlledDrawHookChoice
                    : controlledRecycleStratagemPending
                    ? PrototypeAutomaticHandDrawProgressKindV111
                        .ControlledRecycleStratagem
                    : PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw,
                RemainingCount: drawRemaining);
        }
        else if (controlledDrawCostRemaining is { } drawCostRemaining)
        {
            progress = new(
                PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost,
                RemainingCount: drawCostRemaining);
        }
        else if (controlledSemanticBeforeHandDrawPending)
        {
            progress = new(
                PrototypeAutomaticHandDrawProgressKindV111
                    .SemanticBeforeHandDraw);
        }
        else if (controlledBeforeHandDrawHookChoicePending)
        {
            progress = new(
                PrototypeAutomaticHandDrawProgressKindV111
                    .ControlledBeforeHandDrawHookChoice);
        }
        else if (controlledSemanticDrawRemaining is not null ||
                 controlledSemanticDrawRequested is not null)
        {
            progress = new(
                PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw,
                controlledSemanticDrawRequested,
                controlledSemanticDrawRemaining);
        }
        else if (controlledRecycleStratagemPending)
        {
            progress = new(
                PrototypeAutomaticHandDrawProgressKindV111
                    .ControlledRecycleStratagem);
        }

        return new(progress, controlledEnemyAction, controlledTurnIntents);
    }

    private static void ValidateFlattenedProgress(
        int? controlledDrawRemaining,
        int? controlledDrawCostRemaining,
        int? controlledSemanticDrawRemaining,
        int? controlledSemanticDrawRequested,
        bool controlledSemanticBeforeHandDrawPending,
        bool controlledRecycleStratagemPending,
        bool controlledDrawHookChoicePending,
        bool controlledBeforeHandDrawHookChoicePending)
    {
        int activeModes = (controlledDrawRemaining is not null ? 1 : 0) +
            (controlledDrawCostRemaining is not null ? 1 : 0) +
            (controlledSemanticDrawRemaining is not null ||
             controlledSemanticDrawRequested is not null ? 1 : 0) +
            (controlledSemanticBeforeHandDrawPending ? 1 : 0) +
            (controlledBeforeHandDrawHookChoicePending ? 1 : 0);
        if (activeModes > 1 ||
            (controlledRecycleStratagemPending &&
             controlledDrawCostRemaining is not null) ||
            (controlledRecycleStratagemPending &&
             (controlledSemanticDrawRemaining is not null ||
              controlledSemanticDrawRequested is not null ||
              controlledSemanticBeforeHandDrawPending)) ||
            (controlledDrawHookChoicePending &&
             (controlledRecycleStratagemPending ||
              controlledDrawRemaining is null)) ||
            (controlledBeforeHandDrawHookChoicePending &&
             controlledSemanticBeforeHandDrawPending))
            throw new InvalidOperationException(
                "Flattened automatic hand-draw progress contains conflicting modes.");
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetControlledDrawRemaining(
            PrototypeAutomaticHandDrawProgressV111? progress, int? value)
    {
        if (value is null)
        {
            if (progress?.Kind is
                PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
                PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice)
                return null;
            if (progress?.Kind ==
                PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem)
                return progress with { RemainingCount = null };
            return progress;
        }
        if (progress is null)
            return new(PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw,
                RemainingCount: value);
        if (progress.Kind is
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice or
            PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem)
            return progress with { RemainingCount = value };
        return new(PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw,
            RemainingCount: value);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetControlledDrawHookChoicePending(
            PrototypeAutomaticHandDrawProgressV111? progress, bool value)
    {
        if (!value)
        {
            if (progress?.Kind != PrototypeAutomaticHandDrawProgressKindV111
                    .ControlledDrawHookChoice)
                return progress;
            return progress.RemainingCount is null ? null :
                progress with
                {
                    Kind = PrototypeAutomaticHandDrawProgressKindV111
                        .ControlledDraw
                };
        }
        if (progress is null)
            return new(PrototypeAutomaticHandDrawProgressKindV111
                .ControlledDrawHookChoice);
        if (progress.Kind is
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawHookChoice)
            return progress with
            {
                Kind = PrototypeAutomaticHandDrawProgressKindV111
                    .ControlledDrawHookChoice
            };
        return new(PrototypeAutomaticHandDrawProgressKindV111
            .ControlledDrawHookChoice);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetControlledBeforeHandDrawHookChoicePending(
            PrototypeAutomaticHandDrawProgressV111? progress, bool value)
    {
        if (!value)
            return progress?.Kind == PrototypeAutomaticHandDrawProgressKindV111
                .ControlledBeforeHandDrawHookChoice ? null : progress;
        return new(PrototypeAutomaticHandDrawProgressKindV111
            .ControlledBeforeHandDrawHookChoice);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetControlledDrawCostRemaining(
            PrototypeAutomaticHandDrawProgressV111? progress, int? value)
    {
        if (value is null)
            return progress?.Kind ==
                PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost
                    ? null : progress;
        return new(PrototypeAutomaticHandDrawProgressKindV111.ControlledDrawCost,
            RemainingCount: value);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetSemanticDrawRemaining(
            PrototypeAutomaticHandDrawProgressV111? progress, int? value)
    {
        if (value is null)
            return ClearSemanticComponent(progress,
                requested: false);
        return new(PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw,
            progress?.Kind is
                PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw or
                PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw
                    ? progress.RequestedCount : null,
            value);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetSemanticDrawRequested(
            PrototypeAutomaticHandDrawProgressV111? progress, int? value)
    {
        if (value is null)
            return ClearSemanticComponent(progress,
                requested: true);
        return new(PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw,
            value,
            progress?.Kind is
                PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw or
                PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw
                    ? progress.RemainingCount : null);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        ClearSemanticComponent(
            PrototypeAutomaticHandDrawProgressV111? progress, bool requested)
    {
        if (progress?.Kind ==
            PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw)
            return null;
        if (progress?.Kind !=
            PrototypeAutomaticHandDrawProgressKindV111.SemanticDraw)
            return progress;
        var updated = requested
            ? progress with { RequestedCount = null }
            : progress with { RemainingCount = null };
        return updated.RequestedCount is null && updated.RemainingCount is null
            ? null : updated;
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetSemanticBeforeHandDraw(
            PrototypeAutomaticHandDrawProgressV111? progress, bool value)
    {
        if (!value)
            return progress?.Kind ==
                PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw
                    ? null : progress;
        return new(
            PrototypeAutomaticHandDrawProgressKindV111.SemanticBeforeHandDraw);
    }

    private static PrototypeAutomaticHandDrawProgressV111?
        SetControlledRecycleStratagem(
            PrototypeAutomaticHandDrawProgressV111? progress, bool value)
    {
        if (!value)
        {
            if (progress?.Kind !=
                PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem)
                return progress;
            return progress.RemainingCount is null ? null :
                progress with
                {
                    Kind = PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw
                };
        }
        if (progress is null)
            return new(
                PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem);
        if (progress.Kind is
            PrototypeAutomaticHandDrawProgressKindV111.ControlledDraw or
            PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem)
            return progress with
            {
                Kind = PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem
            };
        return new(
            PrototypeAutomaticHandDrawProgressKindV111.ControlledRecycleStratagem);
    }

    public void Validate(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(continuation);

        ValidateHandDrawProgress();

        if (continuation.OpeningStage ==
                PrototypeOpeningCombatStage.InitialEnemyIntents &&
            ControlledTurnIntents is null)
            throw new InvalidOperationException(
                "Invalid suspended initial-enemy-intent stage.");

        if (ControlledRecycleStratagemPending &&
            (!continuation.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand) ||
             ControlledDrawRemaining is not > 0 ||
             ControlledDrawCostRemaining is not null ||
             combat.PendingChoice is not
                { ChoiceId: "select_cards",
                  Selection.SourceZone: PrototypeCardZone.DrawPile } ||
             combat.SemanticDeck is not null))
            throw new InvalidOperationException(
                "Controlled recycle Stratagem selection is malformed.");

        if (ControlledDrawHookChoicePending &&
            (!continuation.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand) ||
             ControlledDrawRemaining is not >= 0 ||
             ControlledDrawCostRemaining is not null ||
             ControlledSemanticDrawRemaining is not null ||
             ControlledSemanticDrawRequested is not null ||
             ControlledSemanticBeforeHandDrawPending ||
             ControlledRecycleStratagemPending ||
             ControlledEnemyAction is not null ||
             !combat.IsPlayerTurn || combat.PendingChoice is null ||
             combat.PendingKnowledgeDemonChoice is not null ||
             combat.SemanticDeck is not null))
            throw new InvalidOperationException(
                "Controlled automatic CardDrawn hook choice is malformed.");

        if (ControlledBeforeHandDrawHookChoicePending &&
            (!continuation.Mechanics.IsAtStep(
                 PrototypeAutomaticStepKind.DrawPlayerHand) ||
             ControlledDrawRemaining is not null ||
             ControlledDrawCostRemaining is not null ||
             ControlledSemanticDrawRemaining is not null ||
             ControlledSemanticDrawRequested is not null ||
             ControlledSemanticBeforeHandDrawPending ||
             ControlledRecycleStratagemPending ||
             ControlledDrawHookChoicePending ||
             ControlledEnemyAction is not null ||
             !combat.IsPlayerTurn || combat.PendingChoice is null ||
             combat.PendingKnowledgeDemonChoice is not null ||
             combat.SemanticDeck is not null))
            throw new InvalidOperationException(
                "Controlled BeforeHandDraw hook choice is malformed.");

        ValidateSemanticHandDrawCursor(combat, continuation);

        if (ControlledTurnIntents is { } turnIntents)
        {
            turnIntents.Validate();
            bool initialIntentStage = continuation.OpeningStage ==
                PrototypeOpeningCombatStage.InitialEnemyIntents;
            // BaselineCombat is the replay origin for the intent ticket
            // prefix. Combat contains any intents already committed before
            // the next ticket suspended; ControlledTurnIntents.Validate
            // independently proves that prefix from its baseline.
            var intentCheckpoint = turnIntents.Combat with
            {
                AutomaticPipelineContinuation = null
            };
            var checkpoint = combat with
            {
                AutomaticPipelineContinuation = null
            };
            if (turnIntents.SiteId != (initialIntentStage
                    ? NativeCombatRngEntryV111.CommittedIntentSiteId
                    : ControlledTurnIntentEngineV111.SiteId) ||
                (!initialIntentStage &&
                 !continuation.Mechanics.IsAtStep(
                    PrototypeAutomaticStepKind.AdvanceTurn)) ||
                (initialIntentStage && continuation.NextStepIndex !=
                    PrototypeContent.Rules.EndTurnPipeline.Length) ||
                continuation.PendingPostDispatchEventKind is not null ||
                ControlledEnemyAction is not null ||
                ControlledDrawRemaining is not null ||
                ControlledDrawCostRemaining is not null ||
                ControlledSemanticDrawRemaining is not null ||
                ControlledSemanticDrawRequested is not null ||
                ControlledSemanticBeforeHandDrawPending ||
                ControlledRecycleStratagemPending ||
                combat.IsPlayerTurn || combat.PendingChoice is not null ||
                combat.PendingKnowledgeDemonChoice is not null ||
                CanonicalJson.Sha256(intentCheckpoint) !=
                    CanonicalJson.Sha256(checkpoint))
                throw new InvalidOperationException(
                    "Controlled turn-intent checkpoint disagrees with the automatic pipeline.");
        }

        if (ControlledEnemyAction is not { } enemyAction) return;
        enemyAction.Validate(PrototypeContent.Rules.EndTurnPipeline.Length);
        if (continuation.NextStepIndex != enemyAction.StepIndex ||
            combat.IsPlayerTurn || combat.PendingChoice is not null ||
            combat.PendingKnowledgeDemonChoice is not null ||
            CanonicalJson.Sha256(enemyAction.Checkpoint.World?.Combat) !=
                CanonicalJson.Sha256(combat with
                {
                    AutomaticPipelineContinuation = null
                }))
            throw new InvalidOperationException(
                "Controlled enemy-action checkpoint disagrees with combat state.");
    }

    private void ValidateHandDrawProgress()
    {
        HandDrawProgress?.Validate();
    }

    private void ValidateSemanticHandDrawCursor(
        CombatState combat,
        PrototypeAutomaticPipelineContinuationState continuation)
    {
        bool hasCursor = ControlledSemanticDrawRemaining is not null ||
            ControlledSemanticDrawRequested is not null ||
            ControlledSemanticBeforeHandDrawPending;
        if (!hasCursor) return;

        if (!continuation.Mechanics.IsAtStep(
                PrototypeAutomaticStepKind.DrawPlayerHand) ||
            ControlledDrawRemaining is not null ||
            ControlledDrawCostRemaining is not null ||
            ControlledEnemyAction is not null ||
            combat.SemanticDeck is not
                { Mode: SemanticDeckStateV111.NatureMode } ||
            combat.PendingKnowledgeDemonChoice is not null)
            throw new InvalidOperationException(
                "Semantic hand-draw cursor disagrees with the automatic pipeline.");

        if (ControlledSemanticBeforeHandDrawPending)
        {
            bool pendingBeforeDrawChoice =
                combat.PendingChoice?.ControlledRandomPileCard is not null ||
                combat.PendingChoice?.AutomaticSemanticDrawHookChoice == true;
            if (ControlledSemanticDrawRemaining is not null ||
                ControlledSemanticDrawRequested is not null ||
                combat.SemanticDeck?.Pending is not null ||
                !pendingBeforeDrawChoice)
                throw new InvalidOperationException(
                    "Semantic BeforeHandDraw cursor has no matching choice.");
            return;
        }

        if (!TryGetSemanticHandDrawProgress(
                continuation.Mechanics, out _, out _))
            throw new InvalidOperationException(
                "Semantic automatic hand-draw counters are malformed.");
    }

}
