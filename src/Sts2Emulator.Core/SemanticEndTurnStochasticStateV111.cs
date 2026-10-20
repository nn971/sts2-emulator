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
    /// Typed outcomes nested under the automatic pipeline. Kept as an init
    /// property so older snapshots deserialize with an empty transcript.
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
        int transcriptNestedInsertions = 0, transcriptNestedGeneratedCards = 0;
        int transcriptNestedPotionGenerations = 0;
        int transcriptNestedCardTransforms = 0;
        int transcriptNestedCardShortlists = 0;
        int transcriptNestedPoolShuffles = 0;
        int transcriptNestedHandCostSelections = 0;
        int transcriptNestedCardSelections = 0;
        int transcriptNestedDrawHookChoices = 0;
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
            else if (boundary.Kind == "semantic_generated_card")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedGeneratedCards++ ||
                    transcriptNestedGeneratedCards >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_generated_card") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_generated_card")
                    throw new InvalidDataException(
                        "Semantic end-turn nested generated-card boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_potion_generation")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedPotionGenerations++ ||
                    transcriptNestedPotionGenerations >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_potion_generation") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_potion_generation")
                    throw new InvalidDataException(
                        "Semantic end-turn nested potion-generation boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_card_transform")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedCardTransforms++ ||
                    transcriptNestedCardTransforms >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_card_transform") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_card_transform")
                    throw new InvalidDataException(
                        "Semantic end-turn nested card-transform boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_card_shortlist")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedCardShortlists++ ||
                    transcriptNestedCardShortlists >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_card_shortlist") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_card_shortlist")
                    throw new InvalidDataException(
                        "Semantic end-turn nested card-shortlist boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_pool_shuffle")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedPoolShuffles++ ||
                    transcriptNestedPoolShuffles >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_pool_shuffle") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_pool_shuffle")
                    throw new InvalidDataException(
                        "Semantic end-turn nested pool-shuffle boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_hand_cost_selection")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedHandCostSelections++ ||
                    transcriptNestedHandCostSelections >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_hand_cost_selection") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_hand_cost_selection")
                    throw new InvalidDataException(
                        "Semantic end-turn nested hand-cost-selection boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_card_selection")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedCardSelections++ ||
                    transcriptNestedCardSelections >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_card_selection") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_card_selection")
                    throw new InvalidDataException(
                        "Semantic end-turn nested card-selection boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_draw_hook_choice")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedDrawHookChoices++ ||
                    transcriptNestedDrawHookChoices >
                        NestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_draw_hook_choice") ||
                    transcriptNestedChoices >= NestedChoiceTranscript.Length ||
                    NestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_draw_hook_choice")
                    throw new InvalidDataException(
                        "Semantic end-turn draw-hook boundary order is invalid.");
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
              transcriptNestedGeneratedCards != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_generated_card") ||
              transcriptNestedPotionGenerations != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_potion_generation") ||
              transcriptNestedCardTransforms != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_card_transform") ||
              transcriptNestedCardShortlists != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_card_shortlist") ||
              transcriptNestedPoolShuffles != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_pool_shuffle") ||
              transcriptNestedHandCostSelections != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_hand_cost_selection") ||
              transcriptNestedCardSelections != NestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_card_selection") ||
              transcriptNestedDrawHookChoices !=
                  NestedChoiceTranscript.Count(choice =>
                      choice.Kind == "semantic_draw_hook_choice") ||
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
    SemanticDrawInsertionDecisionV111? DrawInsertionDecision = null,
    SemanticGeneratedCardDecisionV111? GeneratedCardDecision = null,
    SemanticPotionGenerationDecisionV111? PotionGenerationDecision = null,
    SemanticCardTransformDecisionV111? CardTransformDecision = null,
    SemanticCardShortlistDecisionV111? CardShortlistDecision = null,
    SemanticPoolShuffleDecisionV111? PoolShuffleDecision = null,
    SemanticHandCostSelectionDecisionV111? HandCostSelectionDecision = null,
    SemanticCardSelectionDecisionV111? CardSelectionDecision = null,
    SemanticDrawHookChoiceDecisionV111? DrawHookChoiceDecision = null)
{
    public SemanticEndTurnNestedChoiceV111 Fork() => this with
    {
        DrawDecision = DrawDecision?.Fork(),
        CostDecision = CostDecision?.Fork(),
        DrawInsertionDecision = DrawInsertionDecision?.Fork(),
        GeneratedCardDecision = GeneratedCardDecision is null
            ? null
            : GeneratedCardDecision with
            {
                Event = GeneratedCardDecision.Event.Fork()
            },
        PotionGenerationDecision = PotionGenerationDecision?.Fork(),
        CardTransformDecision = CardTransformDecision is null
            ? null
            : CardTransformDecision with
            {
                Event = CardTransformDecision.Event.Fork()
            },
        CardShortlistDecision = CardShortlistDecision?.Fork(),
        PoolShuffleDecision = PoolShuffleDecision?.Fork(),
        HandCostSelectionDecision = HandCostSelectionDecision?.Fork(),
        CardSelectionDecision = CardSelectionDecision?.Fork(),
        DrawHookChoiceDecision = DrawHookChoiceDecision?.Fork()
    };

    public void Validate()
    {
        int decisionCount = (DrawDecision is null ? 0 : 1) +
            (CostDecision is null ? 0 : 1) +
            (DrawInsertionDecision is null ? 0 : 1) +
            (GeneratedCardDecision is null ? 0 : 1) +
            (PotionGenerationDecision is null ? 0 : 1) +
            (CardTransformDecision is null ? 0 : 1) +
            (CardShortlistDecision is null ? 0 : 1) +
            (PoolShuffleDecision is null ? 0 : 1) +
            (HandCostSelectionDecision is null ? 0 : 1) +
            (CardSelectionDecision is null ? 0 : 1) +
            (DrawHookChoiceDecision is null ? 0 : 1);
        if (decisionCount != 1)
            throw new InvalidDataException(
                "Nested end-turn choice must contain exactly one typed decision.");
        if (Kind == "semantic_draw" && DrawDecision is not null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardTransformDecision is null && CardShortlistDecision is null)
        {
            DrawDecision.Validate();
            return;
        }
        if (Kind == "semantic_cost" && CostDecision is not null &&
            DrawDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardTransformDecision is null && CardShortlistDecision is null)
        {
            CostDecision.Validate();
            return;
        }
        if (Kind == "semantic_draw_insertion" &&
            DrawInsertionDecision is not null && DrawDecision is null &&
            CostDecision is null && GeneratedCardDecision is null &&
            PotionGenerationDecision is null && CardTransformDecision is null &&
            CardShortlistDecision is null)
        {
            DrawInsertionDecision.Validate();
            return;
        }
        if (Kind == "semantic_generated_card" &&
            GeneratedCardDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            PotionGenerationDecision is null && CardTransformDecision is null &&
            CardShortlistDecision is null)
        {
            GeneratedCardDecision.Validate();
            return;
        }
        if (Kind == "semantic_potion_generation" &&
            PotionGenerationDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && CardTransformDecision is null &&
            CardShortlistDecision is null)
        {
            PotionGenerationDecision.Validate();
            return;
        }
        if (Kind == "semantic_card_transform" &&
            CardTransformDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardShortlistDecision is null)
        {
            CardTransformDecision.Validate();
            return;
        }
        if (Kind == "semantic_card_shortlist" &&
            CardShortlistDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardTransformDecision is null)
        {
            CardShortlistDecision.Validate();
            return;
        }
        if (Kind == "semantic_pool_shuffle" &&
            PoolShuffleDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardTransformDecision is null && CardShortlistDecision is null)
        {
            PoolShuffleDecision.Validate();
            return;
        }
        if (Kind == "semantic_hand_cost_selection" &&
            HandCostSelectionDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardTransformDecision is null && CardShortlistDecision is null &&
            PoolShuffleDecision is null)
        {
            HandCostSelectionDecision.Validate();
            return;
        }
        if (Kind == "semantic_card_selection" &&
            CardSelectionDecision is not null && DrawDecision is null &&
            CostDecision is null && DrawInsertionDecision is null &&
            GeneratedCardDecision is null && PotionGenerationDecision is null &&
            CardTransformDecision is null && CardShortlistDecision is null &&
            PoolShuffleDecision is null && HandCostSelectionDecision is null)
        {
            CardSelectionDecision.Validate();
            return;
        }
        if (Kind == "semantic_draw_hook_choice" &&
            DrawHookChoiceDecision is not null)
        {
            DrawHookChoiceDecision.Validate();
            if (!DrawHookChoiceDecision.Event.AutomaticPipelineAutoplay ||
                DrawHookChoiceDecision.Event.AutomaticHandDraw ||
                DrawHookChoiceDecision.Event.BeforeHandDraw)
                throw new InvalidDataException(
                    "Nested draw-hook decision has the wrong source boundary.");
            return;
        }
        throw new InvalidDataException(
            "Invalid semantic end-turn nested-choice transcript entry.");
    }
}
