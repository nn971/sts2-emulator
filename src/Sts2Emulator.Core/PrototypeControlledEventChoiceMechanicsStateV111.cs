namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics checkpoint and queued consequences for one suspended controlled
/// event effect sequence.
/// </summary>
public sealed record PrototypeControlledEventChoiceMechanicsStateV111(
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
    string? DeferredCardId);
