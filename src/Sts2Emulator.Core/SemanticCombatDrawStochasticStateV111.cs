namespace Sts2Emulator.Core;

/// <summary>Chronological reference into the typed decision transcripts
/// owned by one semantic combat-draw continuation.</summary>
public sealed record SemanticCombatDrawBoundaryChoiceV111(
    string Kind, int TranscriptIndex)
{
    public void Validate()
    {
        if (Kind is not ("semantic_draw" or "semantic_cost" or
                "random_pile_card" or "semantic_draw_insertion" or
                "semantic_generated_card" or "stratagem_selection" or "draw_hook_choice" or
                "random_target" or "semantic_potion_generation" or
                "semantic_card_selection" or "semantic_pool_shuffle" or
                "semantic_card_transform" or "semantic_card_shortlist") ||
            TranscriptIndex < 0)
            throw new InvalidDataException(
                "Invalid semantic combat-draw boundary transcript entry.");
    }
}

/// <summary>
/// Provider decisions accumulated by one semantic combat-draw continuation.
/// The continuation keeps its established flattened properties as
/// projections so its snapshot shape remains compatible.
/// </summary>
public sealed record SemanticCombatDrawStochasticStateV111
{
    internal const int LegacyBoundaryTranscriptVersion = 1;
    internal const int GeneratedCardBoundaryTranscriptVersion = 2;
    internal const int PotionGenerationBoundaryTranscriptVersion = 3;
    internal const int PoolShuffleBoundaryTranscriptVersion = 4;
    internal const int CardSelectionBoundaryTranscriptVersion = 5;
    internal const int CardTransformBoundaryTranscriptVersion = 6;
    internal const int CurrentBoundaryTranscriptVersion = 7;

    private SemanticRandomPileCardDecisionV111[]? _randomPileCardTranscript = [];
    private SemanticDrawInsertionDecisionV111[]? _drawInsertionTranscript = [];
    private SemanticGeneratedCardDecisionV111[]? _generatedCardTranscript = [];
    private SemanticPotionGenerationDecisionV111[]? _potionGenerationTranscript = [];
    private SemanticPoolShuffleDecisionV111[]? _poolShuffleTranscript = [];
    private SemanticCardSelectionDecisionV111[]? _cardSelectionTranscript = [];
    private SemanticCardTransformDecisionV111[]? _cardTransformTranscript = [];
    private SemanticCardShortlistDecisionV111[]? _cardShortlistTranscript = [];
    private SemanticCardSelectionDecisionV111[]? _stratagemSelectionTranscript = [];
    private SemanticDrawHookChoiceDecisionV111[]? _drawHookChoiceTranscript = [];
    private SemanticTargetDecisionV111[]? _targetTranscript = [];
    private RandomIndexDecisionV111[]? _costTranscript = [];
    private SemanticCombatDrawBoundaryChoiceV111[]? _boundaryTranscript = [];
    public int BoundaryTranscriptVersion { get; init; }

    public SemanticRandomPileCardDecisionV111[] RandomPileCardTranscript
    {
        get => CopyTranscript(_randomPileCardTranscript,
            decision => decision.Fork())!;
        init => _randomPileCardTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticDrawInsertionDecisionV111[] DrawInsertionTranscript
    {
        get => CopyTranscript(_drawInsertionTranscript,
            decision => decision.Fork())!;
        init => _drawInsertionTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticGeneratedCardDecisionV111[] GeneratedCardTranscript
    {
        get => CopyTranscript(_generatedCardTranscript,
            decision => decision.Fork())!;
        init => _generatedCardTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticPotionGenerationDecisionV111[] PotionGenerationTranscript
    {
        get => CopyTranscript(_potionGenerationTranscript,
            decision => decision.Fork())!;
        init => _potionGenerationTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticPoolShuffleDecisionV111[] PoolShuffleTranscript
    {
        get => CopyTranscript(_poolShuffleTranscript,
            decision => decision.Fork())!;
        init => _poolShuffleTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticCardSelectionDecisionV111[] CardSelectionTranscript
    {
        get => CopyTranscript(_cardSelectionTranscript,
            decision => decision.Fork())!;
        init => _cardSelectionTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticCardTransformDecisionV111[] CardTransformTranscript
    {
        get => CopyTranscript(_cardTransformTranscript,
            decision => decision.Fork())!;
        init => _cardTransformTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticCardShortlistDecisionV111[] CardShortlistTranscript
    {
        get => CopyTranscript(_cardShortlistTranscript,
            decision => decision.Fork())!;
        init => _cardShortlistTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticCardSelectionDecisionV111[] StratagemSelectionTranscript
    {
        get => CopyTranscript(_stratagemSelectionTranscript,
            decision => decision.Fork())!;
        init => _stratagemSelectionTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticDrawHookChoiceDecisionV111[] DrawHookChoiceTranscript
    {
        get => CopyTranscript(_drawHookChoiceTranscript,
            decision => decision.Fork())!;
        init => _drawHookChoiceTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public SemanticTargetDecisionV111[] TargetTranscript
    {
        get => CopyTranscript(_targetTranscript,
            decision => decision.Fork())!;
        init => _targetTranscript = CopyTranscript(value,
            decision => decision.Fork());
    }
    public RandomIndexDecisionV111[] CostTranscript
    {
        get => CopyTranscript(_costTranscript,
            CloneCostDecision)!;
        init => _costTranscript = CopyTranscript(value,
            CloneCostDecision);
    }
    public SemanticCombatDrawBoundaryChoiceV111[] BoundaryTranscript
    {
        get => CopyTranscript(_boundaryTranscript, decision => decision)!;
        init => _boundaryTranscript = CopyTranscript(value,
            decision => decision);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendRandomPileCard(
        SemanticRandomPileCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = RandomPileCardTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            RandomPileCardTranscript = transcript
        }).AppendBoundary("random_pile_card", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendDrawInsertion(
        SemanticDrawInsertionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = DrawInsertionTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            DrawInsertionTranscript = transcript
        }).AppendBoundary("semantic_draw_insertion", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendGeneratedCard(
        SemanticGeneratedCardDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = GeneratedCardTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            GeneratedCardTranscript = transcript
        }).AppendBoundary("semantic_generated_card", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendPotionGeneration(
        SemanticPotionGenerationDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = PotionGenerationTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            PotionGenerationTranscript = transcript
        }).AppendBoundary("semantic_potion_generation", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendPoolShuffle(
        SemanticPoolShuffleDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = PoolShuffleTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            PoolShuffleTranscript = transcript
        }).AppendBoundary("semantic_pool_shuffle", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendCardSelection(
        SemanticCardSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = CardSelectionTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            CardSelectionTranscript = transcript
        }).AppendBoundary("semantic_card_selection", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendCardTransform(
        SemanticCardTransformDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = CardTransformTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            CardTransformTranscript = transcript
        }).AppendBoundary("semantic_card_transform", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendCardShortlist(
        SemanticCardShortlistDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = CardShortlistTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            CardShortlistTranscript = transcript
        }).AppendBoundary("semantic_card_shortlist", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendStratagemSelection(
        SemanticCardSelectionDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = StratagemSelectionTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            StratagemSelectionTranscript = transcript
        }).AppendBoundary("stratagem_selection", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendDrawHookChoice(
        SemanticDrawHookChoiceDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = DrawHookChoiceTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            DrawHookChoiceTranscript = transcript
        }).AppendBoundary("draw_hook_choice", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendTarget(
        SemanticTargetDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = TargetTranscript
            .Append(decision.Fork()).ToArray();
        return (this with
        {
            TargetTranscript = transcript
        }).AppendBoundary("random_target", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendCost(
        RandomIndexDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        if (decision.ProviderMode != "semantic-controlled-v1" ||
            decision.ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Unexpected semantic drawn-cost transcript provenance.");
        var transcript = CostTranscript
            .Append(CloneCostDecision(decision)).ToArray();
        return (this with
        {
            CostTranscript = transcript
        }).AppendBoundary("semantic_cost", transcript.Length - 1);
    }

    internal SemanticCombatDrawStochasticStateV111 AppendDeckChoice(
        int transcriptIndex) => AppendBoundary(
            "semantic_draw", transcriptIndex);

    private SemanticCombatDrawStochasticStateV111 AppendBoundary(
        string kind, int transcriptIndex)
    {
        var choice = new SemanticCombatDrawBoundaryChoiceV111(
            kind, transcriptIndex);
        choice.Validate();
        return this with
        {
            BoundaryTranscript = BoundaryTranscript.Append(choice).ToArray(),
            BoundaryTranscriptVersion = CurrentBoundaryTranscriptVersion
        };
    }

    public SemanticCombatDrawStochasticStateV111 Fork() => this with
    {
        RandomPileCardTranscript = CopyTranscript(_randomPileCardTranscript,
            decision => decision.Fork())!,
        DrawInsertionTranscript = CopyTranscript(_drawInsertionTranscript,
            decision => decision.Fork())!,
        GeneratedCardTranscript = CopyTranscript(_generatedCardTranscript,
            decision => decision.Fork())!,
        PotionGenerationTranscript = CopyTranscript(
            _potionGenerationTranscript, decision => decision.Fork())!,
        PoolShuffleTranscript = CopyTranscript(
            _poolShuffleTranscript, decision => decision.Fork())!,
        CardSelectionTranscript = CopyTranscript(
            _cardSelectionTranscript, decision => decision.Fork())!,
        CardTransformTranscript = CopyTranscript(
            _cardTransformTranscript, decision => decision.Fork())!,
        CardShortlistTranscript = CopyTranscript(
            _cardShortlistTranscript, decision => decision.Fork())!,
        StratagemSelectionTranscript = CopyTranscript(_stratagemSelectionTranscript,
            decision => decision.Fork())!,
        DrawHookChoiceTranscript = CopyTranscript(_drawHookChoiceTranscript,
            decision => decision.Fork())!,
        TargetTranscript = CopyTranscript(_targetTranscript,
            decision => decision.Fork())!,
        CostTranscript = CopyTranscript(_costTranscript, CloneCostDecision)!,
        BoundaryTranscript = CopyTranscript(_boundaryTranscript,
            decision => decision)!
    };

    public void Validate()
    {
        var costTranscript = CostTranscript;
        var drawInsertionTranscript = DrawInsertionTranscript;
        var generatedCardTranscript = GeneratedCardTranscript;
        var potionGenerationTranscript = PotionGenerationTranscript;
        var poolShuffleTranscript = PoolShuffleTranscript;
        var cardSelectionTranscript = CardSelectionTranscript;
        var cardTransformTranscript = CardTransformTranscript;
        var cardShortlistTranscript = CardShortlistTranscript;
        var stratagemSelectionTranscript = StratagemSelectionTranscript;
        var drawHookChoiceTranscript = DrawHookChoiceTranscript;
        var targetTranscript = TargetTranscript;
        var randomPileCardTranscript = RandomPileCardTranscript;
        var boundaryTranscript = BoundaryTranscript;
        if (costTranscript is null || drawInsertionTranscript is null ||
            generatedCardTranscript is null ||
            potionGenerationTranscript is null ||
            poolShuffleTranscript is null ||
            cardSelectionTranscript is null ||
            cardTransformTranscript is null ||
            cardShortlistTranscript is null ||
            stratagemSelectionTranscript is null ||
            drawHookChoiceTranscript is null || targetTranscript is null ||
            boundaryTranscript is null)
            throw new InvalidDataException(
                "Invalid semantic combat draw stage.");
        if (randomPileCardTranscript is null)
            throw new InvalidDataException(
                "Missing semantic random pile-card transcript.");

        foreach (var decision in costTranscript)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Semantic drawn-cost transcript contains a null decision.");
            decision.Validate();
            if (decision.ProviderMode != "semantic-controlled-v1" ||
                decision.ProbabilityStatus != "UNVERIFIED")
                throw new InvalidDataException(
                    "Unexpected semantic drawn-cost transcript provenance.");
        }
        foreach (var decision in randomPileCardTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic random pile-card transcript contains a null decision."))
                .Validate();
        foreach (var decision in drawInsertionTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic draw-insertion transcript contains a null decision."))
                .Validate();
        foreach (var decision in generatedCardTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic generated-card transcript contains a null decision."))
                .Validate();
        foreach (var decision in potionGenerationTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic potion-generation transcript contains a null decision."))
                .Validate();
        foreach (var decision in poolShuffleTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic pool-shuffle transcript contains a null decision."))
                .Validate();
        foreach (var decision in cardSelectionTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic card-selection transcript contains a null decision."))
                .Validate();
        foreach (var decision in cardTransformTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic card-transform transcript contains a null decision."))
                .Validate();
        foreach (var decision in cardShortlistTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic card-shortlist transcript contains a null decision."))
                .Validate();
        foreach (var decision in stratagemSelectionTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic Stratagem transcript contains a null decision."))
                .Validate();
        foreach (var decision in drawHookChoiceTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic draw-hook transcript contains a null decision."))
                .Validate();
        foreach (var decision in targetTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic draw target transcript contains a null decision."))
                .Validate();
        foreach (var decision in boundaryTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic combat-draw boundary transcript contains a null entry."))
                .Validate();
    }

    internal void ValidateBoundaryTranscript(int semanticDrawCount)
    {
        if (semanticDrawCount < 0)
            throw new ArgumentOutOfRangeException(nameof(semanticDrawCount));
        var transcript = BoundaryTranscript;
        var typedCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["semantic_draw"] = semanticDrawCount,
            ["semantic_cost"] = CostTranscript.Length,
            ["random_pile_card"] = RandomPileCardTranscript.Length,
            ["semantic_draw_insertion"] = DrawInsertionTranscript.Length,
            ["semantic_generated_card"] = GeneratedCardTranscript.Length,
            ["semantic_potion_generation"] =
                PotionGenerationTranscript.Length,
            ["semantic_pool_shuffle"] = PoolShuffleTranscript.Length,
            ["semantic_card_selection"] = CardSelectionTranscript.Length,
            ["semantic_card_transform"] = CardTransformTranscript.Length,
            ["semantic_card_shortlist"] = CardShortlistTranscript.Length,
            ["stratagem_selection"] = StratagemSelectionTranscript.Length,
            ["draw_hook_choice"] = DrawHookChoiceTranscript.Length,
            ["random_target"] = TargetTranscript.Length
        };
        if (BoundaryTranscriptVersion == 0 && transcript.Length == 0 &&
            TargetTranscript.Length == 0 &&
            GeneratedCardTranscript.Length == 0 &&
            PotionGenerationTranscript.Length == 0 &&
            PoolShuffleTranscript.Length == 0 &&
            CardSelectionTranscript.Length == 0 &&
            CardTransformTranscript.Length == 0 &&
            CardShortlistTranscript.Length == 0)
            return; // Legacy snapshots predate the chronology member.
        if (BoundaryTranscriptVersion == LegacyBoundaryTranscriptVersion &&
            GeneratedCardTranscript.Length != 0)
            throw new InvalidDataException(
                "Legacy semantic combat-draw chronology cannot contain generated-card choices.");
        if (BoundaryTranscriptVersion is not (
                LegacyBoundaryTranscriptVersion or
                GeneratedCardBoundaryTranscriptVersion or
                PotionGenerationBoundaryTranscriptVersion or
                PoolShuffleBoundaryTranscriptVersion or
                CardSelectionBoundaryTranscriptVersion or
                CardTransformBoundaryTranscriptVersion or
                CurrentBoundaryTranscriptVersion))
            throw new InvalidDataException(
                "Unsupported semantic combat-draw chronology version.");
        if (BoundaryTranscriptVersion <
                PotionGenerationBoundaryTranscriptVersion &&
            PotionGenerationTranscript.Length != 0)
            throw new InvalidDataException(
                "Legacy semantic combat-draw chronology cannot contain potion-generation choices.");
        if (BoundaryTranscriptVersion <
                PoolShuffleBoundaryTranscriptVersion &&
            PoolShuffleTranscript.Length != 0)
            throw new InvalidDataException(
                "Legacy semantic combat-draw chronology cannot contain pool-shuffle choices.");
        if (BoundaryTranscriptVersion <
                CardSelectionBoundaryTranscriptVersion &&
            CardSelectionTranscript.Length != 0)
            throw new InvalidDataException(
                "Legacy semantic combat-draw chronology cannot contain card-selection choices.");
        if (BoundaryTranscriptVersion < CardTransformBoundaryTranscriptVersion &&
            CardTransformTranscript.Length != 0)
            throw new InvalidDataException(
                "Legacy semantic combat-draw chronology cannot contain card-transform choices.");
        if (BoundaryTranscriptVersion < CurrentBoundaryTranscriptVersion &&
            CardShortlistTranscript.Length != 0)
            throw new InvalidDataException(
                "Legacy semantic combat-draw chronology cannot contain card-shortlist choices.");
        var nextIndexes = typedCounts.Keys.ToDictionary(
            kind => kind, _ => 0, StringComparer.Ordinal);
        if (transcript.Length != typedCounts.Values.Sum())
            throw new InvalidDataException(
                "Semantic combat-draw chronology omits or duplicates decisions.");
        foreach (var entry in transcript)
        {
            entry.Validate();
            if (!typedCounts.TryGetValue(entry.Kind, out var count) ||
                entry.TranscriptIndex != nextIndexes[entry.Kind] ||
                entry.TranscriptIndex >= count)
                throw new InvalidDataException(
                    "Semantic combat-draw chronology differs from its typed transcripts.");
            nextIndexes[entry.Kind]++;
        }
        if (typedCounts.Any(pair => nextIndexes[pair.Key] != pair.Value))
            throw new InvalidDataException(
                "Semantic combat-draw chronology is incomplete.");
    }

    private static T[]? CopyTranscript<T>(T[]? transcript, Func<T, T> clone)
        where T : class => transcript?.Select(decision =>
            decision is null ? null! : clone(decision)).ToArray();

    private static RandomIndexDecisionV111 CloneCostDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = decision.Request with
        {
            OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
        }
    };
}

/// <summary>Consumes typed semantic-draw histories during deterministic
/// source replay and verifies their recorded cross-type chronology against
/// each live mechanics boundary.</summary>
internal sealed class SemanticCombatDrawReplayCursorV111
{
    private readonly SemanticDeckChoiceV111[] _deckChoices;
    private readonly RandomIndexDecisionV111[] _costChoices;
    private readonly SemanticRandomPileCardDecisionV111[] _pileCardChoices;
    private readonly SemanticDrawInsertionDecisionV111[] _insertions;
    private readonly SemanticGeneratedCardDecisionV111[] _generatedCardChoices;
    private readonly SemanticPotionGenerationDecisionV111[] _potionGenerationChoices;
    private readonly SemanticPoolShuffleDecisionV111[] _poolShuffleChoices;
    private readonly SemanticCardSelectionDecisionV111[] _cardSelectionChoices;
    private readonly SemanticCardTransformDecisionV111[] _cardTransformChoices;
    private readonly SemanticCardShortlistDecisionV111[] _cardShortlistChoices;
    private readonly SemanticCardSelectionDecisionV111[] _stratagemChoices;
    private readonly SemanticDrawHookChoiceDecisionV111[] _drawHookChoices;
    private readonly SemanticTargetDecisionV111[] _targetChoices;
    private readonly SemanticCombatDrawBoundaryChoiceV111[] _boundaryChoices;
    private readonly bool _hasBoundaryTranscript;
    private int _deckIndex;
    private int _costIndex;
    private int _pileCardIndex;
    private int _insertionIndex;
    private int _generatedCardIndex;
    private int _potionGenerationIndex;
    private int _poolShuffleIndex;
    private int _cardSelectionIndex;
    private int _cardTransformIndex;
    private int _cardShortlistIndex;
    private int _stratagemIndex;
    private int _drawHookIndex;
    private int _targetIndex;
    private int _boundaryIndex;

    internal SemanticCombatDrawReplayCursorV111(
        SemanticCombatDrawV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _deckChoices = state.Transcript;
        _costChoices = state.CostTranscript;
        _pileCardChoices = state.RandomPileCardTranscript;
        _insertions = state.DrawInsertionTranscript;
        _generatedCardChoices = state.GeneratedCardTranscript;
        _potionGenerationChoices = state.PotionGenerationTranscript;
        _poolShuffleChoices = state.PoolShuffleTranscript;
        _cardSelectionChoices = state.CardSelectionTranscript;
        _cardTransformChoices = state.CardTransformTranscript;
        _cardShortlistChoices = state.CardShortlistTranscript;
        _stratagemChoices = state.StratagemSelectionTranscript;
        _drawHookChoices = state.DrawHookChoiceTranscript;
        _targetChoices = state.TargetTranscript;
        _boundaryChoices = state.Stochastic.BoundaryTranscript;
        _hasBoundaryTranscript = _boundaryChoices.Length > 0;
    }

    internal bool HasRemaining =>
        _deckIndex < _deckChoices.Length ||
        _costIndex < _costChoices.Length ||
        _pileCardIndex < _pileCardChoices.Length ||
        _insertionIndex < _insertions.Length ||
        _generatedCardIndex < _generatedCardChoices.Length ||
        _potionGenerationIndex < _potionGenerationChoices.Length ||
        _poolShuffleIndex < _poolShuffleChoices.Length ||
        _cardSelectionIndex < _cardSelectionChoices.Length ||
        _cardTransformIndex < _cardTransformChoices.Length ||
        _cardShortlistIndex < _cardShortlistChoices.Length ||
        _stratagemIndex < _stratagemChoices.Length ||
        _drawHookIndex < _drawHookChoices.Length ||
        _targetIndex < _targetChoices.Length;

    internal SemanticDeckChoiceV111 NextDeckChoice() => Take(
        _deckChoices, ref _deckIndex,
        "Semantic draw/cost stage mismatch in provenance replay.",
        "semantic_draw");

    internal RandomIndexDecisionV111 NextCostChoice() => Take(
        _costChoices, ref _costIndex,
        "Missing semantic drawn-cost decision in replay.",
        "semantic_cost");

    internal SemanticRandomPileCardDecisionV111 NextPileCardChoice() => Take(
        _pileCardChoices, ref _pileCardIndex,
        "Missing semantic random pile-card decision in replay.",
        "random_pile_card");

    internal SemanticDrawInsertionDecisionV111 NextInsertion() => Take(
        _insertions, ref _insertionIndex,
        "Missing semantic draw-insertion decision in replay.",
        "semantic_draw_insertion");

    internal SemanticGeneratedCardDecisionV111 NextGeneratedCardChoice() => Take(
        _generatedCardChoices, ref _generatedCardIndex,
        "Missing semantic generated-card decision in draw replay.",
        "semantic_generated_card");

    internal SemanticPotionGenerationDecisionV111
        NextPotionGenerationChoice() => Take(
            _potionGenerationChoices, ref _potionGenerationIndex,
            "Missing semantic potion-generation decision in draw replay.",
            "semantic_potion_generation");

    internal SemanticPoolShuffleDecisionV111 NextPoolShuffleChoice() => Take(
        _poolShuffleChoices, ref _poolShuffleIndex,
        "Missing semantic pool-shuffle decision in draw replay.",
        "semantic_pool_shuffle");

    internal SemanticCardSelectionDecisionV111 NextCardSelectionChoice() =>
        Take(_cardSelectionChoices, ref _cardSelectionIndex,
            "Missing semantic card-selection decision in draw replay.",
            "semantic_card_selection");

    internal SemanticCardTransformDecisionV111 NextCardTransformChoice() =>
        Take(_cardTransformChoices, ref _cardTransformIndex,
            "Missing semantic card-transform decision in draw replay.",
            "semantic_card_transform");

    internal SemanticCardShortlistDecisionV111 NextCardShortlistChoice() =>
        Take(_cardShortlistChoices, ref _cardShortlistIndex,
            "Missing semantic card-shortlist decision in draw replay.",
            "semantic_card_shortlist");

    internal SemanticCardSelectionDecisionV111 NextStratagemChoice() => Take(
        _stratagemChoices, ref _stratagemIndex,
        "Missing standalone Stratagem selection in replay.",
        "stratagem_selection");

    internal SemanticDrawHookChoiceDecisionV111 NextDrawHookChoice() => Take(
        _drawHookChoices, ref _drawHookIndex,
        "Missing nested CardDrawn hook choice in replay.",
        "draw_hook_choice");

    internal SemanticTargetDecisionV111 NextTargetChoice() => Take(
        _targetChoices, ref _targetIndex,
        "Missing automatic hand-draw target decision in replay.",
        "random_target");

    internal void ValidateComplete()
    {
        if (HasRemaining)
            throw new InvalidDataException(
                "Semantic combat draw replay left transcript decisions unused.");
        if (_hasBoundaryTranscript &&
            _boundaryIndex != _boundaryChoices.Length)
            throw new InvalidDataException(
                "Semantic combat-draw replay left chronology entries unused.");
    }

    private T Take<T>(T[] transcript, ref int index, string error, string kind)
        where T : class
    {
        if (index >= transcript.Length)
            throw new InvalidDataException(error);
        if (_hasBoundaryTranscript)
        {
            if (_boundaryIndex >= _boundaryChoices.Length)
                throw new InvalidDataException(
                    "Semantic combat-draw chronology ended before its typed transcript.");
            var boundary = _boundaryChoices[_boundaryIndex++];
            if (boundary.Kind != kind ||
                boundary.TranscriptIndex != index)
                throw new InvalidDataException(
                    "Semantic combat-draw chronology diverged from source replay.");
        }
        return transcript[index++];
    }
}
