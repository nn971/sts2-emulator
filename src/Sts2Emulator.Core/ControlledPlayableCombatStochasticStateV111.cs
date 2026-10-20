namespace Sts2Emulator.Core;

/// <summary>
/// Provider cursor, unresolved requests and outcome transcripts for the
/// controlled playable combat session. The session keeps its established
/// flattened properties as projections of this owner.
/// </summary>
public sealed record ControlledPlayableCombatStochasticStateV111
{
    private RandomIndexDecisionV111[] _shuffleTranscript = [];
    private RandomIndexDecisionV111[] _turnTranscript = [];
    private RandomIndexDecisionV111[] _recycleTranscriptPrefix = [];
    private RandomIndexDecisionV111[] _liveTranscript = [];
    private RandomIndexDecisionV111[] _targetTranscript = [];
    private RandomIndexDecisionV111[] _generatedCardTranscript = [];
    private RandomIndexDecisionV111[] _drawInsertionTranscript = [];
    private RandomIndexDecisionV111[] _poolShuffleTranscript = [];
    private RandomIndexDecisionV111[] _randomCostTranscript = [];
    private RandomIndexDecisionV111[] _drawCostTranscript = [];
    private RandomIndexDecisionV111[] _potionGenerationTranscript = [];
    private RandomIndexDecisionV111[] _cardTransformTranscript = [];
    private RandomIndexRequestV111? _pending;
    private ControlledRecycleShuffleV111? _recycle;

    public int NextShuffleIndex { get; init; }
    public RandomIndexDecisionV111[] ShuffleTranscript
    {
        get => DeepCopy(_shuffleTranscript)!;
        init => _shuffleTranscript = DeepCopy(value)!;
    }
    public RandomIndexRequestV111? Pending
    {
        get => CloneRequest(_pending);
        init => _pending = CloneRequest(value);
    }
    public RandomIndexDecisionV111[] TurnTranscript
    {
        get => DeepCopy(_turnTranscript)!;
        init => _turnTranscript = DeepCopy(value)!;
    }
    public ControlledRecycleShuffleV111? Recycle
    {
        get => _recycle?.Fork();
        init => _recycle = value?.Fork();
    }
    /// <summary>
    /// Completed recycle decisions. While a recycle is active, its current
    /// segment remains solely in <see cref="Recycle"/>.
    /// </summary>
    public RandomIndexDecisionV111[] RecycleTranscriptPrefix
    {
        get => DeepCopy(_recycleTranscriptPrefix)!;
        init => _recycleTranscriptPrefix = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] LiveTranscript
    {
        get => DeepCopy(_liveTranscript)!;
        init => _liveTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] TargetTranscript
    {
        get => DeepCopy(_targetTranscript)!;
        init => _targetTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] GeneratedCardTranscript
    {
        get => DeepCopy(_generatedCardTranscript)!;
        init => _generatedCardTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] DrawInsertionTranscript
    {
        get => DeepCopy(_drawInsertionTranscript)!;
        init => _drawInsertionTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] PoolShuffleTranscript
    {
        get => DeepCopy(_poolShuffleTranscript)!;
        init => _poolShuffleTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] RandomCostTranscript
    {
        get => DeepCopy(_randomCostTranscript)!;
        init => _randomCostTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] DrawCostTranscript
    {
        get => DeepCopy(_drawCostTranscript)!;
        init => _drawCostTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] PotionGenerationTranscript
    {
        get => DeepCopy(_potionGenerationTranscript)!;
        init => _potionGenerationTranscript = DeepCopy(value)!;
    }
    public RandomIndexDecisionV111[] CardTransformTranscript
    {
        get => DeepCopy(_cardTransformTranscript)!;
        init => _cardTransformTranscript = DeepCopy(value)!;
    }

    public ControlledPlayableCombatStochasticStateV111 Fork() => this with
    {
        ShuffleTranscript = DeepCopy(_shuffleTranscript)!,
        Pending = CloneRequest(_pending),
        TurnTranscript = DeepCopy(_turnTranscript)!,
        Recycle = _recycle?.Fork(),
        RecycleTranscriptPrefix = DeepCopy(_recycleTranscriptPrefix)!,
        LiveTranscript = DeepCopy(_liveTranscript)!,
        TargetTranscript = DeepCopy(_targetTranscript)!,
        GeneratedCardTranscript = DeepCopy(_generatedCardTranscript)!,
        DrawInsertionTranscript = DeepCopy(_drawInsertionTranscript)!,
        PoolShuffleTranscript = DeepCopy(_poolShuffleTranscript)!,
        RandomCostTranscript = DeepCopy(_randomCostTranscript)!,
        DrawCostTranscript = DeepCopy(_drawCostTranscript)!,
        PotionGenerationTranscript = DeepCopy(_potionGenerationTranscript)!,
        CardTransformTranscript = DeepCopy(_cardTransformTranscript)!
    };

    private static RandomIndexRequestV111? CloneRequest(
        RandomIndexRequestV111? request) => request is null ? null : request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };

    private static RandomIndexDecisionV111[]? DeepCopy(
        RandomIndexDecisionV111[]? decisions) => decisions?.Select(decision =>
        decision is null
            ? null!
            : PrototypeControlledEventChoiceContinuationV111
                .CloneDecision(decision)).ToArray();
}
