namespace Sts2Emulator.Core;

/// <summary>
/// Mechanical event progress and queued consequences. EventState retains its
/// flattened properties as snapshot and source-call compatibility projections.
/// </summary>
public sealed record EventMechanicsStateV111(
    string EventId,
    string? ChosenChoiceId = null,
    PrototypePendingEventDeckChoiceState? PendingDeckChoice = null,
    PrototypePendingEventPotionReplacementState? PendingPotionReplacement = null,
    string[]? QueuedPotionIds = null,
    PrototypePendingEventDeckChoiceState[]? QueuedDeckChoices = null,
    string[]? OfferedChoiceIds = null,
    RewardState? PendingReward = null,
    int DeferredHpLoss = 0,
    int NativePageIndex = 0,
    int DeferredGoldLoss = 0,
    string? RepyFirstChoiceId = null,
    int RepyChestGroupsResolved = 0,
    bool? NativeTanxTriEligible = null,
    bool? NativeNonupeipeBraceletEligible = null)
{
    public EventMechanicsStateV111 Fork() => this with
    {
        PendingDeckChoice = PendingDeckChoice?.Fork(),
        PendingPotionReplacement = PendingPotionReplacement?.Fork(),
        QueuedPotionIds = QueuedPotionIds is null
            ? null : (string[])QueuedPotionIds.Clone(),
        QueuedDeckChoices = QueuedDeckChoices is null
            ? null : QueuedDeckChoices.Select(choice => choice.Fork()).ToArray(),
        OfferedChoiceIds = OfferedChoiceIds is null
            ? null : (string[])OfferedChoiceIds.Clone(),
        PendingReward = PendingReward?.Fork()
    };
}
