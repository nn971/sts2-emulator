namespace Sts2Emulator.Core;

/// <summary>
/// Provider decisions accumulated by one semantic combat-draw continuation.
/// The continuation keeps its established flattened properties as
/// projections so its snapshot shape remains compatible.
/// </summary>
public sealed record SemanticCombatDrawStochasticStateV111
{
    private SemanticRandomPileCardDecisionV111[]? _randomPileCardTranscript = [];
    private SemanticDrawInsertionDecisionV111[]? _drawInsertionTranscript = [];
    private SemanticCardSelectionDecisionV111[]? _stratagemSelectionTranscript = [];
    private SemanticDrawHookChoiceDecisionV111[]? _drawHookChoiceTranscript = [];
    private RandomIndexDecisionV111[]? _costTranscript = [];

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
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)!;
        init => _costTranscript = CopyTranscript(value,
            PrototypeControlledEventChoiceContinuationV111.CloneDecision);
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
        CostTranscript = CopyTranscript(_costTranscript,
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)!
    };

    public void Validate()
    {
        var costTranscript = CostTranscript;
        var drawInsertionTranscript = DrawInsertionTranscript;
        var stratagemSelectionTranscript = StratagemSelectionTranscript;
        var drawHookChoiceTranscript = DrawHookChoiceTranscript;
        var randomPileCardTranscript = RandomPileCardTranscript;
        if (costTranscript is null || drawInsertionTranscript is null ||
            stratagemSelectionTranscript is null ||
            drawHookChoiceTranscript is null)
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
    }

    private static T[]? CopyTranscript<T>(T[]? transcript, Func<T, T> clone)
        where T : class => transcript?.Select(decision =>
            decision is null ? null! : clone(decision)).ToArray();
}

/// <summary>Consumes the typed semantic-draw histories during deterministic
/// source replay. Mechanics determines which boundary occurs next; this owner
/// tracks consumption across the separate provider transcript projections.</summary>
internal sealed class SemanticCombatDrawReplayCursorV111
{
    private readonly SemanticDeckChoiceV111[] _deckChoices;
    private readonly RandomIndexDecisionV111[] _costChoices;
    private readonly SemanticRandomPileCardDecisionV111[] _pileCardChoices;
    private readonly SemanticDrawInsertionDecisionV111[] _insertions;
    private readonly SemanticCardSelectionDecisionV111[] _stratagemChoices;
    private readonly SemanticDrawHookChoiceDecisionV111[] _drawHookChoices;
    private int _deckIndex;
    private int _costIndex;
    private int _pileCardIndex;
    private int _insertionIndex;
    private int _stratagemIndex;
    private int _drawHookIndex;

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
        "Semantic draw/cost stage mismatch in provenance replay.");

    internal RandomIndexDecisionV111 NextCostChoice() => Take(
        _costChoices, ref _costIndex,
        "Missing semantic drawn-cost decision in replay.");

    internal SemanticRandomPileCardDecisionV111 NextPileCardChoice() => Take(
        _pileCardChoices, ref _pileCardIndex,
        "Missing semantic random pile-card decision in replay.");

    internal SemanticDrawInsertionDecisionV111 NextInsertion() => Take(
        _insertions, ref _insertionIndex,
        "Missing semantic draw-insertion decision in replay.");

    internal SemanticCardSelectionDecisionV111 NextStratagemChoice() => Take(
        _stratagemChoices, ref _stratagemIndex,
        "Missing standalone Stratagem selection in replay.");

    internal SemanticDrawHookChoiceDecisionV111 NextDrawHookChoice() => Take(
        _drawHookChoices, ref _drawHookIndex,
        "Missing nested CardDrawn hook choice in replay.");

    internal void ValidateComplete()
    {
        if (HasRemaining)
            throw new InvalidDataException(
                "Semantic combat draw replay left transcript decisions unused.");
    }

    private static T Take<T>(T[] transcript, ref int index, string error)
        where T : class
    {
        if (index >= transcript.Length)
            throw new InvalidDataException(error);
        return transcript[index++];
    }
}
