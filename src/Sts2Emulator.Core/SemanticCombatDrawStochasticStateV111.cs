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
                "stratagem_selection" or "draw_hook_choice") ||
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
    internal const int CurrentBoundaryTranscriptVersion = 1;

    private SemanticRandomPileCardDecisionV111[]? _randomPileCardTranscript = [];
    private SemanticDrawInsertionDecisionV111[]? _drawInsertionTranscript = [];
    private SemanticCardSelectionDecisionV111[]? _stratagemSelectionTranscript = [];
    private SemanticDrawHookChoiceDecisionV111[]? _drawHookChoiceTranscript = [];
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
        StratagemSelectionTranscript = CopyTranscript(_stratagemSelectionTranscript,
            decision => decision.Fork())!,
        DrawHookChoiceTranscript = CopyTranscript(_drawHookChoiceTranscript,
            decision => decision.Fork())!,
        CostTranscript = CopyTranscript(_costTranscript, CloneCostDecision)!,
        BoundaryTranscript = CopyTranscript(_boundaryTranscript,
            decision => decision)!
    };

    public void Validate()
    {
        var costTranscript = CostTranscript;
        var drawInsertionTranscript = DrawInsertionTranscript;
        var stratagemSelectionTranscript = StratagemSelectionTranscript;
        var drawHookChoiceTranscript = DrawHookChoiceTranscript;
        var randomPileCardTranscript = RandomPileCardTranscript;
        var boundaryTranscript = BoundaryTranscript;
        if (costTranscript is null || drawInsertionTranscript is null ||
            stratagemSelectionTranscript is null ||
            drawHookChoiceTranscript is null || boundaryTranscript is null)
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
        foreach (var decision in stratagemSelectionTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic Stratagem transcript contains a null decision."))
                .Validate();
        foreach (var decision in drawHookChoiceTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic draw-hook transcript contains a null decision."))
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
            ["stratagem_selection"] = StratagemSelectionTranscript.Length,
            ["draw_hook_choice"] = DrawHookChoiceTranscript.Length
        };
        if (BoundaryTranscriptVersion == 0 && transcript.Length == 0)
            return; // Legacy snapshots predate the chronology member.
        if (BoundaryTranscriptVersion != CurrentBoundaryTranscriptVersion)
            throw new InvalidDataException(
                "Unsupported semantic combat-draw chronology version.");
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
    private readonly SemanticCardSelectionDecisionV111[] _stratagemChoices;
    private readonly SemanticDrawHookChoiceDecisionV111[] _drawHookChoices;
    private readonly SemanticCombatDrawBoundaryChoiceV111[] _boundaryChoices;
    private readonly bool _hasBoundaryTranscript;
    private int _deckIndex;
    private int _costIndex;
    private int _pileCardIndex;
    private int _insertionIndex;
    private int _stratagemIndex;
    private int _drawHookIndex;
    private int _boundaryIndex;

    internal SemanticCombatDrawReplayCursorV111(
        SemanticCombatDrawV111 state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _deckChoices = state.Transcript;
        _costChoices = state.CostTranscript;
        _pileCardChoices = state.RandomPileCardTranscript;
        _insertions = state.DrawInsertionTranscript;
        _stratagemChoices = state.StratagemSelectionTranscript;
        _drawHookChoices = state.DrawHookChoiceTranscript;
        _boundaryChoices = state.Stochastic.BoundaryTranscript;
        _hasBoundaryTranscript = _boundaryChoices.Length > 0;
    }

    internal bool HasRemaining =>
        _deckIndex < _deckChoices.Length ||
        _costIndex < _costChoices.Length ||
        _pileCardIndex < _pileCardChoices.Length ||
        _insertionIndex < _insertions.Length ||
        _stratagemIndex < _stratagemChoices.Length ||
        _drawHookIndex < _drawHookChoices.Length;

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

    internal SemanticCardSelectionDecisionV111 NextStratagemChoice() => Take(
        _stratagemChoices, ref _stratagemIndex,
        "Missing standalone Stratagem selection in replay.",
        "stratagem_selection");

    internal SemanticDrawHookChoiceDecisionV111 NextDrawHookChoice() => Take(
        _drawHookChoices, ref _drawHookIndex,
        "Missing nested CardDrawn hook choice in replay.",
        "draw_hook_choice");

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
