namespace Sts2Emulator.Core;

/// <summary>
/// Provider decisions accumulated by one semantic combat-draw continuation.
/// The continuation keeps its established flattened properties as
/// projections so its snapshot shape remains compatible.
/// </summary>
public sealed record SemanticCombatDrawStochasticStateV111
{
    public SemanticRandomPileCardDecisionV111[] RandomPileCardTranscript
        { get; init; } = [];
    public SemanticDrawInsertionDecisionV111[] DrawInsertionTranscript
        { get; init; } = [];
    public SemanticCardSelectionDecisionV111[] StratagemSelectionTranscript
        { get; init; } = [];
    public SemanticDrawHookChoiceDecisionV111[] DrawHookChoiceTranscript
        { get; init; } = [];
    public RandomIndexDecisionV111[] CostTranscript { get; init; } = [];

    public SemanticCombatDrawStochasticStateV111 Fork() => this with
    {
        RandomPileCardTranscript = RandomPileCardTranscript
            .Select(decision => decision.Fork()).ToArray(),
        DrawInsertionTranscript = DrawInsertionTranscript
            .Select(decision => decision.Fork()).ToArray(),
        StratagemSelectionTranscript = StratagemSelectionTranscript
            .Select(decision => decision.Fork()).ToArray(),
        DrawHookChoiceTranscript = DrawHookChoiceTranscript
            .Select(decision => decision.Fork()).ToArray(),
        CostTranscript = CostTranscript.Select(decision => decision with
        {
            Request = decision.Request with
            {
                OrderedCandidates = (int[])decision.Request
                    .OrderedCandidates.Clone()
            }
        }).ToArray()
    };

    public void Validate()
    {
        if (CostTranscript is null || DrawInsertionTranscript is null ||
            StratagemSelectionTranscript is null ||
            DrawHookChoiceTranscript is null)
            throw new InvalidDataException(
                "Invalid semantic combat draw stage.");
        if (RandomPileCardTranscript is null)
            throw new InvalidDataException(
                "Missing semantic random pile-card transcript.");

        foreach (var decision in CostTranscript)
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
        foreach (var decision in RandomPileCardTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic random pile-card transcript contains a null decision."))
                .Validate();
        foreach (var decision in DrawInsertionTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic draw-insertion transcript contains a null decision."))
                .Validate();
        foreach (var decision in StratagemSelectionTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic Stratagem transcript contains a null decision."))
                .Validate();
        foreach (var decision in DrawHookChoiceTranscript)
            (decision ?? throw new InvalidDataException(
                "Semantic draw-hook transcript contains a null decision."))
                .Validate();
    }
}
