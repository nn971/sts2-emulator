namespace Sts2Emulator.Core;

/// <summary>
/// Typed provider decisions and chronology for one semantic automatic-turn
/// continuation. SemanticEndTurnV111 keeps its established flattened fields.
/// </summary>
public sealed record SemanticEndTurnStochasticStateV111(
    RandomIndexDecisionV111[] IntentTranscript,
    RandomIndexDecisionV111[] EnemyActionTranscript,
    SemanticTargetDecisionV111[] TargetTranscript,
    SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript,
    SemanticCombatDrawStochasticStateV111? HandDraw = null)
{
    /// <summary>
    /// Semantic draw, draw-cost, and insertion outcomes nested under the
    /// automatic pipeline. Kept as an init property so older snapshots
    /// deserialize with an empty transcript.
    /// </summary>
    public SemanticEndTurnNestedChoiceV111[] NestedChoiceTranscript
        { get; init; } = [];

    public SemanticEndTurnStochasticStateV111 Fork() => this with
    {
        IntentTranscript = IntentTranscript.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        EnemyActionTranscript = EnemyActionTranscript.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        TargetTranscript = TargetTranscript.Select(decision =>
            decision.Fork()).ToArray(),
        BoundaryTranscript =
            (SemanticEndTurnBoundaryChoiceV111[])BoundaryTranscript.Clone(),
        NestedChoiceTranscript = NestedChoiceTranscript.Select(
            choice => choice.Fork()).ToArray(),
        HandDraw = HandDraw?.Fork()
    };

    public void Validate()
    {
        if (IntentTranscript is null || EnemyActionTranscript is null ||
            TargetTranscript is null || BoundaryTranscript is null ||
            NestedChoiceTranscript is null)
            throw new InvalidDataException(
                "Invalid semantic end-turn stages.");
        HandDraw?.Validate();

        foreach (var decision in IntentTranscript)
        {
            ValidateDecision(decision, "Turn-intent");
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Invalid semantic turn-intent Nature decision.");
        }
        foreach (var decision in EnemyActionTranscript)
        {
            ValidateDecision(decision, "Enemy-action");
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                !PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(decision.Request.SiteId))
                throw new InvalidDataException(
                    "Invalid semantic enemy-action Nature decision.");
        }
        for (int i = 0; i < TargetTranscript.Length; i++)
        {
            var decision = TargetTranscript[i] ?? throw new InvalidDataException(
                "Semantic end-turn target transcript contains a null decision.");
            decision.Validate();
            if (decision.Event.Ordinal != i)
                throw new InvalidDataException(
                    "Semantic end-turn target transcript ordinal is invalid.");
        }
        foreach (var choice in NestedChoiceTranscript)
            (choice ?? throw new InvalidDataException(
                "Semantic end-turn nested-choice transcript contains a null entry."))
                .Validate();

        int transcriptEnemyActions = 0, transcriptTargets = 0;
        int transcriptNestedDraws = 0, transcriptNestedCosts = 0;
        int transcriptNestedInsertions = 0;
        int transcriptNestedChoices = 0;
        foreach (var boundary in BoundaryTranscript)
        {
            (boundary ?? throw new InvalidDataException(
                "Semantic end-turn boundary transcript contains a null entry."))
                .Validate();
            if (boundary.Kind == "enemy_action")
            {
                if (boundary.TranscriptIndex != transcriptEnemyActions++ ||
                    transcriptEnemyActions > EnemyActionTranscript.Length)
                    throw new InvalidDataException(
                        "Semantic end-turn enemy-action boundary order is invalid.");
            }
            else if (boundary.Kind == "random_target")
            {
                if (boundary.TranscriptIndex != transcriptTargets++ ||
                    transcriptTargets > TargetTranscript.Length)
                    throw new InvalidDataException(
                        "Semantic end-turn target boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_draw")
            {
                if (boundary.TranscriptIndex != transcriptNestedDraws++ ||
                    transcriptNestedDraws > NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_draw") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_draw")
                    throw new InvalidDataException(
                        "Semantic end-turn nested draw boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_cost")
            {
                if (boundary.TranscriptIndex != transcriptNestedCosts++ ||
                    transcriptNestedCosts > NestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_cost") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_cost")
                    throw new InvalidDataException(
                        "Semantic end-turn nested cost boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_draw_insertion")
            {
                if (boundary.TranscriptIndex != transcriptNestedInsertions++ ||
                    transcriptNestedInsertions > NestedChoiceTranscript.Count(
                        choice => choice.Kind == "semantic_draw_insertion") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_draw_insertion")
                    throw new InvalidDataException(
                        "Semantic end-turn nested draw-insertion boundary order is invalid.");
            }
        }
        if ((BoundaryTranscript.Length > 0 &&
             (transcriptEnemyActions != EnemyActionTranscript.Length ||
              transcriptTargets != TargetTranscript.Length ||
              transcriptNestedDraws != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_draw") ||
              transcriptNestedCosts != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_cost") ||
              transcriptNestedInsertions != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_draw_insertion") ||
              transcriptNestedChoices != NestedChoiceTranscript.Length)) ||
            (BoundaryTranscript.Length == 0 &&
             (TargetTranscript.Length != 0 ||
              NestedChoiceTranscript.Length != 0)))
            throw new InvalidDataException(
                "Semantic end-turn boundary transcript is incomplete.");

    }

    private static void ValidateDecision(
        RandomIndexDecisionV111 decision, string kind)
    {
        if (decision is null)
            throw new InvalidDataException(
                $"Semantic {kind.ToLowerInvariant()} transcript contains a null decision.");
        decision.Validate();
    }
}

/// <summary>One typed chance outcome nested beneath an automatic-pipeline
/// continuation.</summary>
public sealed record SemanticEndTurnNestedChoiceV111(
    string Kind,
    SemanticDrawDecisionV111? DrawDecision = null,
    SemanticCostDecisionV111? CostDecision = null,
    SemanticDrawInsertionDecisionV111? DrawInsertionDecision = null)
{
    public SemanticEndTurnNestedChoiceV111 Fork() => this with
    {
        DrawDecision = DrawDecision?.Fork(),
        CostDecision = CostDecision?.Fork(),
        DrawInsertionDecision = DrawInsertionDecision?.Fork()
    };

    public void Validate()
    {
        if (Kind == "semantic_draw" && DrawDecision is not null &&
            CostDecision is null && DrawInsertionDecision is null)
        {
            DrawDecision.Validate();
            return;
        }
        if (Kind == "semantic_cost" && CostDecision is not null &&
            DrawDecision is null && DrawInsertionDecision is null)
        {
            CostDecision.Validate();
            return;
        }
        if (Kind == "semantic_draw_insertion" &&
            DrawInsertionDecision is not null && DrawDecision is null &&
            CostDecision is null)
        {
            DrawInsertionDecision.Validate();
            return;
        }
        throw new InvalidDataException(
            "Invalid semantic end-turn nested-choice transcript entry.");
    }
}
