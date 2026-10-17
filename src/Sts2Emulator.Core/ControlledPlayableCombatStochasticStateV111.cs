namespace Sts2Emulator.Core;

/// <summary>
/// Provider cursor, unresolved requests and outcome transcripts for the
/// controlled playable combat session. The session keeps its established
/// flattened properties as projections of this owner.
/// </summary>
public sealed record ControlledPlayableCombatStochasticStateV111
{
    public int NextShuffleIndex { get; init; }
    public RandomIndexDecisionV111[] ShuffleTranscript { get; init; } = [];
    public RandomIndexRequestV111? Pending { get; init; }
    public ControlledTurnIntentsV111? TurnIntents { get; init; }
    public RandomIndexDecisionV111[] TurnTranscript { get; init; } = [];
    public ControlledRecycleShuffleV111? Recycle { get; init; }
    public RandomIndexDecisionV111[] RecycleTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] LiveTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] TargetTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] GeneratedCardTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] DrawInsertionTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] PoolShuffleTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] RandomCostTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] DrawCostTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] PotionGenerationTranscript { get; init; } = [];
    public RandomIndexDecisionV111[] CardTransformTranscript { get; init; } = [];

    public ControlledPlayableCombatStochasticStateV111 Fork() => this with
    {
        ShuffleTranscript = DeepCopy(ShuffleTranscript),
        Pending = CloneRequest(Pending),
        TurnIntents = TurnIntents?.Fork(),
        TurnTranscript = DeepCopy(TurnTranscript),
        Recycle = Recycle?.Fork(),
        RecycleTranscript = DeepCopy(RecycleTranscript),
        LiveTranscript = DeepCopy(LiveTranscript),
        TargetTranscript = DeepCopy(TargetTranscript),
        GeneratedCardTranscript = DeepCopy(GeneratedCardTranscript),
        DrawInsertionTranscript = DeepCopy(DrawInsertionTranscript),
        PoolShuffleTranscript = DeepCopy(PoolShuffleTranscript),
        RandomCostTranscript = DeepCopy(RandomCostTranscript),
        DrawCostTranscript = DeepCopy(DrawCostTranscript),
        PotionGenerationTranscript = DeepCopy(PotionGenerationTranscript),
        CardTransformTranscript = DeepCopy(CardTransformTranscript)
    };

    private static RandomIndexRequestV111? CloneRequest(
        RandomIndexRequestV111? request) => request is null ? null : request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };

    private static RandomIndexDecisionV111[] DeepCopy(
        RandomIndexDecisionV111[] decisions) => decisions.Select(decision =>
        decision with
        {
            Request = decision.Request with
            {
                OrderedCandidates = (int[])decision.Request
                    .OrderedCandidates.Clone()
            }
        }).ToArray();
}
