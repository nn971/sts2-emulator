namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint and queued consequences for one suspended controlled
/// event effect sequence.
/// </summary>
public sealed record PrototypeControlledEventChoiceMechanicsStateV111
{
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState PartialState
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public string EventId { get; init; }
    public string ChoiceId { get; init; }
    public int NextEffectIndex { get; init; }
    public int EffectDecisionStartIndex { get; init; }
    public string[] QueuedPotionIds { get; init; }
    public PrototypePendingEventDeckChoiceState[] QueuedDeckChoices { get; init; }
    public RewardState? DeferredReward { get; init; }
    public int DeferredHpLoss { get; init; }
    public int DeferredGoldLoss { get; init; }
    public string? DeferredCardId { get; init; }

    public PrototypeControlledEventChoiceMechanicsStateV111(
        RunState PartialState,
        string EventId,
        string ChoiceId,
        int NextEffectIndex,
        int EffectDecisionStartIndex,
        string[] QueuedPotionIds,
        PrototypePendingEventDeckChoiceState[] QueuedDeckChoices,
        RewardState? DeferredReward,
        int DeferredHpLoss,
        int DeferredGoldLoss,
        string? DeferredCardId)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ControlledEventChoice,
            null, PartialState);
        this.EventId = EventId;
        this.ChoiceId = ChoiceId;
        this.NextEffectIndex = NextEffectIndex;
        this.EffectDecisionStartIndex = EffectDecisionStartIndex;
        this.QueuedPotionIds = QueuedPotionIds;
        this.QueuedDeckChoices = QueuedDeckChoices;
        this.DeferredReward = DeferredReward;
        this.DeferredHpLoss = DeferredHpLoss;
        this.DeferredGoldLoss = DeferredGoldLoss;
        this.DeferredCardId = DeferredCardId;
    }
}
