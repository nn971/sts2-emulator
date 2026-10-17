namespace Sts2Emulator.Core;

/// <summary>
/// Event-scoped outcomes and draw-relevant continuation values already
/// committed by a provider. They remain available to mechanics through the
/// flattened EventState compatibility properties.
/// </summary>
public sealed record EventCommittedRandomStateV111(
    string? DeferredCardId = null,
    int NativeEventGold = 0,
    int NativeEventSecondaryGold = 0,
    int? NativeEventPotionSlot = null,
    string? NativeDishId = null,
    string? NativeLastDishId = null,
    int NativeDishCount = 0,
    bool NativeDishRollPending = false,
    string[]? RepyChestPotionIds = null,
    string[]? RepyChestRelicIds = null,
    string? NativeTinkerType = null,
    PrototypeFuturePotionOffer[]? FuturePotionOffers = null)
{
    public EventCommittedRandomStateV111 Fork() => this with
    {
        RepyChestPotionIds = RepyChestPotionIds is null
            ? null : (string[])RepyChestPotionIds.Clone(),
        RepyChestRelicIds = RepyChestRelicIds is null
            ? null : (string[])RepyChestRelicIds.Clone(),
        FuturePotionOffers = FuturePotionOffers is null
            ? null : (PrototypeFuturePotionOffer[])FuturePotionOffers.Clone()
    };
}

/// <summary>
/// Provider-private state for an active event: its context stream, selected
/// provider mode, suspended controlled random-choice continuation, and
/// committed event-local random outcomes.
/// </summary>
public sealed record EventStochasticStateV111(
    RngStreamState? NativeRng = null,
    string? ProviderMode = null,
    PrototypeControlledEventChoiceContinuationV111? ControlledChoice = null,
    EventCommittedRandomStateV111? CommittedRandom = null)
{
    public EventCommittedRandomStateV111 RandomOutcomes =>
        CommittedRandom ?? new EventCommittedRandomStateV111();

    public EventStochasticStateV111 Fork() => this with
    {
        NativeRng = NativeRng?.Fork(),
        ControlledChoice = ControlledChoice?.Fork(),
        CommittedRandom = CommittedRandom?.Fork()
    };

    internal static EventStochasticStateV111 WithNativeRng(
        EventStochasticStateV111? current, RngStreamState? nativeRng) =>
        (current ?? new EventStochasticStateV111()) with
        {
            NativeRng = nativeRng
        };

    internal static EventStochasticStateV111 WithProviderMode(
        EventStochasticStateV111? current, string? providerMode) =>
        (current ?? new EventStochasticStateV111()) with
        {
            ProviderMode = providerMode
        };

    internal static EventStochasticStateV111 WithControlledChoice(
        EventStochasticStateV111? current,
        PrototypeControlledEventChoiceContinuationV111? controlledChoice) =>
        (current ?? new EventStochasticStateV111()) with
        {
            ControlledChoice = controlledChoice
        };

    internal static EventStochasticStateV111 WithCommittedRandom(
        EventStochasticStateV111? current,
        EventCommittedRandomStateV111 committedRandom) =>
        (current ?? new EventStochasticStateV111()) with
        {
            CommittedRandom = committedRandom
        };
}
