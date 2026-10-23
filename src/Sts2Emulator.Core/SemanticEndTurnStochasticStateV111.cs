using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Typed provider decisions, source actions, and chronology for one semantic
/// automatic-turn continuation. SemanticEndTurnV111 keeps its established
/// flattened fields.
/// </summary>
public sealed record SemanticEndTurnStochasticStateV111
{
    internal const int CurrentBoundaryTranscriptVersion = 1;
    internal const int CurrentEnemyActionCandidateLabelsVersion = 1;

    private RandomIndexDecisionV111[]? _intentTranscript;
    private SemanticIntentTranscriptEntryV111[]?
        _semanticIntentTranscript = [];
    private RandomIndexDecisionV111[]? _enemyActionTranscript;
    private string[][]? _enemyActionCandidateLabels = [];
    private SemanticTargetDecisionV111[]? _targetTranscript;
    private SemanticEndTurnBoundaryChoiceV111[]? _boundaryTranscript;
    private SemanticEndTurnNestedChoiceV111[]? _nestedChoiceTranscript = [];
    public int BoundaryTranscriptVersion { get; init; }
    public int EnemyActionCandidateLabelsVersion { get; init; }

    [JsonConstructor]
    public SemanticEndTurnStochasticStateV111(
        RandomIndexDecisionV111[] IntentTranscript,
        RandomIndexDecisionV111[] EnemyActionTranscript,
        SemanticTargetDecisionV111[] TargetTranscript,
        SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript,
        SemanticCombatDrawStochasticStateV111? HandDraw = null,
        SemanticIntentTranscriptEntryV111[]? SemanticIntentTranscript = null,
        string[][]? EnemyActionCandidateLabels = null,
        int EnemyActionCandidateLabelsVersion = 0)
    {
        _intentTranscript = CopyTranscript(IntentTranscript,
            CloneIndexDecision);
        _enemyActionTranscript = CopyTranscript(EnemyActionTranscript,
            CloneIndexDecision);
        _enemyActionCandidateLabels = CopyCandidateLabels(
            EnemyActionCandidateLabels) ?? [];
        _targetTranscript = CopyTranscript(TargetTranscript,
            decision => decision.Fork());
        _boundaryTranscript = CopyTranscript(BoundaryTranscript,
            decision => decision);
        this.HandDraw = HandDraw;
        this.EnemyActionCandidateLabelsVersion =
            EnemyActionCandidateLabelsVersion;
        _semanticIntentTranscript = CopyTranscript(SemanticIntentTranscript,
            decision => decision.Fork()) ?? [];
    }

    public RandomIndexDecisionV111[] IntentTranscript
    {
        get => CopyTranscript(_intentTranscript,
            CloneIndexDecision)!;
        init => _intentTranscript = CopyTranscript(value,
            CloneIndexDecision);
    }
    public SemanticIntentTranscriptEntryV111[] SemanticIntentTranscript
    {
        get => CopyTranscript(_semanticIntentTranscript,
            decision => decision.Fork())!;
        init => _semanticIntentTranscript = CopyTranscript(value,
            decision => decision.Fork()) ?? [];
    }
    public RandomIndexDecisionV111[] EnemyActionTranscript
    {
        get => CopyTranscript(_enemyActionTranscript,
            CloneIndexDecision)!;
        init => _enemyActionTranscript = CopyTranscript(value,
            CloneIndexDecision);
    }
    public string[][] EnemyActionCandidateLabels
    {
        get => CopyCandidateLabels(_enemyActionCandidateLabels)!;
        init => _enemyActionCandidateLabels = CopyCandidateLabels(value);
    }
    public SemanticTargetDecisionV111[] TargetTranscript
    {
        get => CopyTranscript(_targetTranscript, decision => decision.Fork())!;
        init => _targetTranscript = CopyTranscript(value, decision => decision.Fork());
    }
    public SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript
    {
        get => CopyTranscript(_boundaryTranscript, decision => decision)!;
        init => _boundaryTranscript = CopyTranscript(value, decision => decision);
    }
    public SemanticCombatDrawStochasticStateV111? HandDraw { get; init; }

    /// <summary>
    /// Typed outcomes nested under the automatic pipeline. Kept as an init
    /// property so older snapshots deserialize with an empty transcript.
    /// </summary>
    public SemanticEndTurnNestedChoiceV111[] NestedChoiceTranscript
    {
        get => CopyTranscript(_nestedChoiceTranscript, choice => choice.Fork())!;
        init => _nestedChoiceTranscript = CopyTranscript(value,
            choice => choice.Fork());
    }

    internal SemanticEndTurnStochasticStateV111 AppendSemanticIntent(
        SemanticIntentTranscriptEntryV111 entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Validate();
        return this with
        {
            SemanticIntentTranscript = SemanticIntentTranscript
                .Append(entry.Fork()).ToArray()
        };
    }

    internal SemanticEndTurnStochasticStateV111 AppendIntentDecision(
        RandomIndexDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (decision.ProviderMode != "controlled-v111" ||
            decision.NativeCursorBefore is not null ||
            decision.NativeCursorAfter is not null ||
            decision.Request.SiteId != ControlledTurnIntentEngineV111.SiteId)
            throw new InvalidDataException(
                "Invalid semantic turn-intent Nature decision.");
        return this with
        {
            IntentTranscript = IntentTranscript
                .Append(CloneIndexDecision(decision)).ToArray()
        };
    }

    internal SemanticEndTurnStochasticStateV111 AppendEnemyAction(
        RandomIndexDecisionV111 decision, string[]? candidateLabels)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (decision.ProviderMode != "controlled-v111" ||
            decision.NativeCursorBefore is not null ||
            decision.NativeCursorAfter is not null ||
            !PrototypeControlledEnemyActionContinuationV111
                .IsEnemyActionSite(decision.Request.SiteId))
            throw new InvalidDataException(
                "Invalid semantic enemy-action Nature decision.");
        var transcript = EnemyActionTranscript;
        var boundaries = BoundaryTranscriptForAppend().Append(
            new SemanticEndTurnBoundaryChoiceV111(
                "enemy_action", transcript.Length)).ToArray();
        var candidateLabelsVersion =
            EnemyActionCandidateLabelsVersion != 0 ||
            candidateLabels is not null
                ? CurrentEnemyActionCandidateLabelsVersion : 0;
        var candidateLabelTranscript = EnemyActionCandidateLabels;
        if (candidateLabelsVersion == 1)
        {
            if (EnemyActionCandidateLabelsVersion == 0)
                candidateLabelTranscript = Enumerable.Repeat<string[]>(
                    null!, transcript.Length).ToArray();
            else if (candidateLabelTranscript.Length != transcript.Length)
                throw new InvalidDataException(
                    "Semantic enemy-action candidate labels are misaligned.");
            candidateLabelTranscript = candidateLabelTranscript.Append(
                candidateLabels is null
                    ? null! : (string[])candidateLabels.Clone()).ToArray();
        }
        return this with
        {
            EnemyActionTranscript = transcript
                .Append(CloneIndexDecision(decision)).ToArray(),
            BoundaryTranscript = boundaries,
            EnemyActionCandidateLabels = candidateLabelTranscript,
            EnemyActionCandidateLabelsVersion = candidateLabelsVersion
        };
    }

    internal SemanticEndTurnStochasticStateV111 AppendTarget(
        SemanticTargetDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = TargetTranscript;
        if (decision.Event.Ordinal != transcript.Length)
            throw new InvalidDataException(
                "Semantic end-turn target ordinal differs from its transcript.");
        var boundaries = BoundaryTranscriptForAppend().Append(
            new SemanticEndTurnBoundaryChoiceV111(
                "random_target", transcript.Length)).ToArray();
        return this with
        {
            TargetTranscript = transcript.Append(decision.Fork()).ToArray(),
            BoundaryTranscript = boundaries
        };
    }

    internal SemanticEndTurnStochasticStateV111 AppendNestedChoice(
        SemanticEndTurnNestedChoiceV111 choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        choice.Validate();
        var transcript = NestedChoiceTranscript;
        var boundaries = BoundaryTranscriptForAppend().Append(
            new SemanticEndTurnBoundaryChoiceV111(
                choice.Kind,
                transcript.Count(item => item.Kind == choice.Kind))).ToArray();
        return this with
        {
            NestedChoiceTranscript = transcript.Append(choice.Fork()).ToArray(),
            BoundaryTranscript = boundaries
        };
    }

    internal SemanticEndTurnStochasticStateV111 CarryBoundaryChronologyTo(
        SemanticEndTurnStochasticStateV111 nextTurn)
    {
        ArgumentNullException.ThrowIfNull(nextTurn);
        return nextTurn with
        {
            EnemyActionTranscript = EnemyActionTranscript,
            EnemyActionCandidateLabels = EnemyActionCandidateLabels,
            EnemyActionCandidateLabelsVersion =
                EnemyActionCandidateLabelsVersion,
            TargetTranscript = TargetTranscript,
            BoundaryTranscript = BoundaryTranscript,
            NestedChoiceTranscript = NestedChoiceTranscript,
            BoundaryTranscriptVersion = BoundaryTranscriptVersion
        };
    }

    public SemanticEndTurnStochasticStateV111 Fork() => this with
    {
        IntentTranscript = CopyTranscript(_intentTranscript,
            CloneIndexDecision)!,
        SemanticIntentTranscript = CopyTranscript(_semanticIntentTranscript,
            decision => decision.Fork())!,
        EnemyActionTranscript = CopyTranscript(_enemyActionTranscript,
            CloneIndexDecision)!,
        EnemyActionCandidateLabels = CopyCandidateLabels(
            _enemyActionCandidateLabels)!,
        TargetTranscript = CopyTranscript(_targetTranscript,
            decision => decision.Fork())!,
        BoundaryTranscript = CopyTranscript(_boundaryTranscript,
            decision => decision)!,
        NestedChoiceTranscript = CopyTranscript(_nestedChoiceTranscript,
            choice => choice.Fork())!,
        HandDraw = HandDraw?.Fork()
    };

    public void Deconstruct(
        out RandomIndexDecisionV111[] IntentTranscript,
        out RandomIndexDecisionV111[] EnemyActionTranscript,
        out SemanticTargetDecisionV111[] TargetTranscript,
        out SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscript,
        out SemanticCombatDrawStochasticStateV111? HandDraw)
    {
        IntentTranscript = this.IntentTranscript;
        EnemyActionTranscript = this.EnemyActionTranscript;
        TargetTranscript = this.TargetTranscript;
        BoundaryTranscript = this.BoundaryTranscript;
        HandDraw = this.HandDraw;
    }

    public void Validate()
    {
        var intentTranscript = this.IntentTranscript;
        var enemyActionTranscript = this.EnemyActionTranscript;
        var targetTranscript = this.TargetTranscript;
        var boundaryTranscript = this.BoundaryTranscript;
        var nestedChoiceTranscript = this.NestedChoiceTranscript;
        var enemyActionCandidateLabels = this.EnemyActionCandidateLabels;
        if (intentTranscript is null || enemyActionTranscript is null ||
            targetTranscript is null || boundaryTranscript is null ||
            nestedChoiceTranscript is null ||
            BoundaryTranscriptVersion is not (0 or
                CurrentBoundaryTranscriptVersion) ||
            EnemyActionCandidateLabelsVersion is not (0 or
                CurrentEnemyActionCandidateLabelsVersion) ||
            EnemyActionCandidateLabelsVersion == 0 &&
                enemyActionCandidateLabels.Length != 0 ||
            EnemyActionCandidateLabelsVersion == 1 &&
                enemyActionCandidateLabels.Length !=
                    enemyActionTranscript.Length)
            throw new InvalidDataException(
                "Invalid semantic end-turn stages.");
        HandDraw?.Validate();

        foreach (var decision in intentTranscript)
        {
            ValidateDecision(decision, "Turn-intent");
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Invalid semantic turn-intent Nature decision.");
        }
        var semanticIntentTranscript = this.SemanticIntentTranscript;
        var previousSemanticIntentIndex = -1;
        foreach (var entry in semanticIntentTranscript)
        {
            (entry ?? throw new InvalidDataException(
                "Semantic intent transcript contains a null entry."))
                .Validate();
            if (entry.TranscriptIndex <= previousSemanticIntentIndex ||
                entry.TranscriptIndex >= intentTranscript.Length)
                throw new InvalidDataException(
                    "Semantic intent transcript indexes are unordered or outside the primitive transcript.");
            previousSemanticIntentIndex = entry.TranscriptIndex;

            var primitive = intentTranscript[entry.TranscriptIndex];
            var semanticEvent = entry.Decision.Event;
            var selectedCandidate = semanticEvent.Support.Candidates
                .SingleOrDefault(candidate =>
                    candidate.IntentId == entry.Decision.IntentId);
            if (selectedCandidate is null ||
                primitive.Request.SiteId != semanticEvent.SiteId ||
                primitive.Request.StreamId != semanticEvent.StreamId ||
                primitive.Request.OwnerId != semanticEvent.OwnerId ||
                primitive.Request.Ordinal != semanticEvent.Ordinal ||
                primitive.Request.SupportCount !=
                    semanticEvent.Support.TotalRawWeight ||
                primitive.SelectedIndex != selectedCandidate.TicketStart)
                throw new InvalidDataException(
                    "Semantic intent outcome differs from its retained primitive ticket.");
        }
        for (var index = 0; index < enemyActionTranscript.Length; index++)
        {
            var decision = enemyActionTranscript[index];
            ValidateDecision(decision, "Enemy-action");
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                !PrototypeControlledEnemyActionContinuationV111
                    .IsEnemyActionSite(decision.Request.SiteId))
                throw new InvalidDataException(
                    "Invalid semantic enemy-action Nature decision.");
            var candidateLabels = EnemyActionCandidateLabelsVersion == 1
                ? enemyActionCandidateLabels[index] : null;
            if (candidateLabels is not null &&
                (candidateLabels.Length != decision.Request.SupportCount ||
                 candidateLabels.Any(string.IsNullOrWhiteSpace) ||
                 candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                     candidateLabels.Length))
                throw new InvalidDataException(
                    "Invalid semantic enemy-action candidate-label transcript.");
        }
        for (int i = 0; i < targetTranscript.Length; i++)
        {
            var decision = targetTranscript[i] ?? throw new InvalidDataException(
                "Semantic end-turn target transcript contains a null decision.");
            decision.Validate();
            if (decision.Event.Ordinal != i)
                throw new InvalidDataException(
                    "Semantic end-turn target transcript ordinal is invalid.");
        }
        foreach (var choice in nestedChoiceTranscript)
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
        int transcriptKnowledgeDemonChoices = 0;
        int transcriptNestedChoices = 0;
        foreach (var boundary in boundaryTranscript)
        {
            (boundary ?? throw new InvalidDataException(
                "Semantic end-turn boundary transcript contains a null entry."))
                .Validate();
            if (boundary.Kind == "enemy_action")
            {
                if (boundary.TranscriptIndex != transcriptEnemyActions++ ||
                    transcriptEnemyActions > enemyActionTranscript.Length)
                    throw new InvalidDataException(
                        "Semantic end-turn enemy-action boundary order is invalid.");
            }
            else if (boundary.Kind == "random_target")
            {
                if (boundary.TranscriptIndex != transcriptTargets++ ||
                    transcriptTargets > targetTranscript.Length)
                    throw new InvalidDataException(
                        "Semantic end-turn target boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_draw")
            {
                if (boundary.TranscriptIndex != transcriptNestedDraws++ ||
                    transcriptNestedDraws > nestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_draw") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_draw")
                    throw new InvalidDataException(
                        "Semantic end-turn nested draw boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_cost")
            {
                if (boundary.TranscriptIndex != transcriptNestedCosts++ ||
                    transcriptNestedCosts > nestedChoiceTranscript.Count(choice =>
                        choice.Kind == "semantic_cost") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_cost")
                    throw new InvalidDataException(
                        "Semantic end-turn nested cost boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_draw_insertion")
            {
                if (boundary.TranscriptIndex != transcriptNestedInsertions++ ||
                    transcriptNestedInsertions > nestedChoiceTranscript.Count(
                        choice => choice.Kind == "semantic_draw_insertion") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_draw_insertion")
                    throw new InvalidDataException(
                        "Semantic end-turn nested draw-insertion boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_generated_card")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedGeneratedCards++ ||
                    transcriptNestedGeneratedCards >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_generated_card") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_generated_card")
                    throw new InvalidDataException(
                        "Semantic end-turn nested generated-card boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_potion_generation")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedPotionGenerations++ ||
                    transcriptNestedPotionGenerations >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_potion_generation") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_potion_generation")
                    throw new InvalidDataException(
                        "Semantic end-turn nested potion-generation boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_card_transform")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedCardTransforms++ ||
                    transcriptNestedCardTransforms >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_card_transform") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_card_transform")
                    throw new InvalidDataException(
                        "Semantic end-turn nested card-transform boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_card_shortlist")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedCardShortlists++ ||
                    transcriptNestedCardShortlists >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_card_shortlist") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_card_shortlist")
                    throw new InvalidDataException(
                        "Semantic end-turn nested card-shortlist boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_pool_shuffle")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedPoolShuffles++ ||
                    transcriptNestedPoolShuffles >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_pool_shuffle") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_pool_shuffle")
                    throw new InvalidDataException(
                        "Semantic end-turn nested pool-shuffle boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_hand_cost_selection")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedHandCostSelections++ ||
                    transcriptNestedHandCostSelections >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_hand_cost_selection") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_hand_cost_selection")
                    throw new InvalidDataException(
                        "Semantic end-turn nested hand-cost-selection boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_card_selection")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedCardSelections++ ||
                    transcriptNestedCardSelections >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_card_selection") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_card_selection")
                    throw new InvalidDataException(
                        "Semantic end-turn nested card-selection boundary order is invalid.");
            }
            else if (boundary.Kind == "semantic_draw_hook_choice")
            {
                if (boundary.TranscriptIndex !=
                        transcriptNestedDrawHookChoices++ ||
                    transcriptNestedDrawHookChoices >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "semantic_draw_hook_choice") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "semantic_draw_hook_choice")
                    throw new InvalidDataException(
                        "Semantic end-turn draw-hook boundary order is invalid.");
            }
            else if (boundary.Kind == "knowledge_demon_choice")
            {
                if (boundary.TranscriptIndex !=
                        transcriptKnowledgeDemonChoices++ ||
                    transcriptKnowledgeDemonChoices >
                        nestedChoiceTranscript.Count(choice =>
                            choice.Kind == "knowledge_demon_choice") ||
                    transcriptNestedChoices >= nestedChoiceTranscript.Length ||
                    nestedChoiceTranscript[transcriptNestedChoices++].Kind !=
                        "knowledge_demon_choice")
                    throw new InvalidDataException(
                        "Semantic end-turn Knowledge Demon boundary order is invalid.");
            }
        }
        if (BoundaryTranscriptVersion ==
                CurrentBoundaryTranscriptVersion &&
            boundaryTranscript.Length != enemyActionTranscript.Length +
                targetTranscript.Length + nestedChoiceTranscript.Length)
            throw new InvalidDataException(
                "Semantic end-turn boundary transcript omits or duplicates decisions.");
        if ((boundaryTranscript.Length > 0 &&
             (transcriptEnemyActions != enemyActionTranscript.Length ||
              transcriptTargets != targetTranscript.Length ||
              transcriptNestedDraws != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_draw") ||
              transcriptNestedCosts != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_cost") ||
              transcriptNestedInsertions != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_draw_insertion") ||
              transcriptNestedGeneratedCards != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_generated_card") ||
              transcriptNestedPotionGenerations != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_potion_generation") ||
              transcriptNestedCardTransforms != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_card_transform") ||
              transcriptNestedCardShortlists != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_card_shortlist") ||
              transcriptNestedPoolShuffles != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_pool_shuffle") ||
              transcriptNestedHandCostSelections != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_hand_cost_selection") ||
              transcriptNestedCardSelections != nestedChoiceTranscript.Count(choice =>
                  choice.Kind == "semantic_card_selection") ||
              transcriptNestedDrawHookChoices !=
                  nestedChoiceTranscript.Count(choice =>
                      choice.Kind == "semantic_draw_hook_choice") ||
              transcriptKnowledgeDemonChoices !=
                  nestedChoiceTranscript.Count(choice =>
                      choice.Kind == "knowledge_demon_choice") ||
              transcriptNestedChoices != nestedChoiceTranscript.Length)) ||
            (boundaryTranscript.Length == 0 &&
             (targetTranscript.Length != 0 ||
              nestedChoiceTranscript.Length != 0)))
            throw new InvalidDataException(
                "Semantic end-turn boundary transcript is incomplete.");

    }

    private static T[]? CopyTranscript<T>(T[]? transcript, Func<T, T> clone)
        where T : class => transcript?.Select(decision =>
            decision is null ? null! : clone(decision)).ToArray();

    private static string[][]? CopyCandidateLabels(string[][]? labels) =>
        labels?.Select(candidateLabels => candidateLabels is null
            ? null! : (string[])candidateLabels.Clone()).ToArray();

    private SemanticEndTurnBoundaryChoiceV111[] BoundaryTranscriptForAppend()
    {
        var boundaries = BoundaryTranscript;
        if (boundaries.Length > 0)
            return boundaries;
        return Enumerable.Range(0, EnemyActionTranscript.Length)
            .Select(index => new SemanticEndTurnBoundaryChoiceV111(
                "enemy_action", index))
            .Concat(Enumerable.Range(0, TargetTranscript.Length)
                .Select(index => new SemanticEndTurnBoundaryChoiceV111(
                    "random_target", index)))
            .ToArray();
    }

    private static RandomIndexDecisionV111 CloneIndexDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = decision.Request with
        {
            OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
        }
    };

    private static void ValidateDecision(
        RandomIndexDecisionV111 decision, string kind)
    {
        if (decision is null)
            throw new InvalidDataException(
                $"Semantic {kind.ToLowerInvariant()} transcript contains a null decision.");
        decision.Validate();
    }
}

/// <summary>One typed decision nested beneath an automatic-pipeline
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
    SemanticDrawHookChoiceDecisionV111? DrawHookChoiceDecision = null,
    SemanticKnowledgeDemonChoiceDecisionV111?
        KnowledgeDemonChoiceDecision = null)
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
        DrawHookChoiceDecision = DrawHookChoiceDecision?.Fork(),
        KnowledgeDemonChoiceDecision = KnowledgeDemonChoiceDecision?.Fork()
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
            (DrawHookChoiceDecision is null ? 0 : 1) +
            (KnowledgeDemonChoiceDecision is null ? 0 : 1);
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
        if (Kind == "knowledge_demon_choice" &&
            KnowledgeDemonChoiceDecision is not null)
        {
            KnowledgeDemonChoiceDecision.Validate();
            return;
        }
        throw new InvalidDataException(
            "Invalid semantic end-turn nested-choice transcript entry.");
    }
}
