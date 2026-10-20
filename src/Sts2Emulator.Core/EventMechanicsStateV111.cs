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
    private string[]? _queuedPotionIds = Clone(QueuedPotionIds);
    private PrototypePendingEventDeckChoiceState[]? _queuedDeckChoices =
        Clone(QueuedDeckChoices);
    private string[]? _offeredChoiceIds = Clone(OfferedChoiceIds);

    public string[]? QueuedPotionIds
    {
        get => Clone(_queuedPotionIds);
        init => _queuedPotionIds = Clone(value);
    }

    public PrototypePendingEventDeckChoiceState[]? QueuedDeckChoices
    {
        get => Clone(_queuedDeckChoices);
        init => _queuedDeckChoices = Clone(value);
    }

    public string[]? OfferedChoiceIds
    {
        get => Clone(_offeredChoiceIds);
        init => _offeredChoiceIds = Clone(value);
    }

    public EventMechanicsStateV111 Fork() => this with
    {
        PendingDeckChoice = PendingDeckChoice?.Fork(),
        PendingPotionReplacement = PendingPotionReplacement?.Fork(),
        QueuedPotionIds = _queuedPotionIds,
        QueuedDeckChoices = _queuedDeckChoices,
        OfferedChoiceIds = _offeredChoiceIds,
        PendingReward = PendingReward?.Fork()
    };

    private static string[]? Clone(string[]? values) =>
        values is null ? null : (string[])values.Clone();

    private static PrototypePendingEventDeckChoiceState[]? Clone(
        PrototypePendingEventDeckChoiceState[]? values) =>
        values?.Select(choice => choice.Fork()).ToArray();
}
