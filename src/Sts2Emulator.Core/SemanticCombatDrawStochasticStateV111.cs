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
        get => Clone(_randomPileCardTranscript,
            decision => decision.Fork())!;
        init => _randomPileCardTranscript = Clone(value,
            decision => decision.Fork());
    }
    public SemanticDrawInsertionDecisionV111[] DrawInsertionTranscript
    {
        get => Clone(_drawInsertionTranscript,
            decision => decision.Fork())!;
        init => _drawInsertionTranscript = Clone(value,
            decision => decision.Fork());
    }
    public SemanticCardSelectionDecisionV111[] StratagemSelectionTranscript
    {
        get => Clone(_stratagemSelectionTranscript,
            decision => decision.Fork())!;
        init => _stratagemSelectionTranscript = Clone(value,
            decision => decision.Fork());
    }
    public SemanticDrawHookChoiceDecisionV111[] DrawHookChoiceTranscript
    {
        get => Clone(_drawHookChoiceTranscript,
            decision => decision.Fork())!;
        init => _drawHookChoiceTranscript = Clone(value,
            decision => decision.Fork());
    }
    public RandomIndexDecisionV111[] CostTranscript
    {
        get => Clone(_costTranscript,
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)!;
        init => _costTranscript = Clone(value,
            PrototypeControlledEventChoiceContinuationV111.CloneDecision);
    }

    public SemanticCombatDrawStochasticStateV111 Fork() => this with
    {
        RandomPileCardTranscript = Clone(_randomPileCardTranscript,
            decision => decision.Fork())!,
        DrawInsertionTranscript = Clone(_drawInsertionTranscript,
            decision => decision.Fork())!,
        StratagemSelectionTranscript = Clone(_stratagemSelectionTranscript,
            decision => decision.Fork())!,
        DrawHookChoiceTranscript = Clone(_drawHookChoiceTranscript,
            decision => decision.Fork())!,
        CostTranscript = Clone(_costTranscript,
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

    private static T[]? Clone<T>(T[]? transcript, Func<T, T> clone)
        where T : class => transcript?.Select(decision =>
            decision is null ? null! : clone(decision)).ToArray();
}
