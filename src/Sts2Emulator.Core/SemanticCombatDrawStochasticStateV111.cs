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
}
