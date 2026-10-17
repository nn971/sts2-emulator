namespace Sts2Emulator.Core;

/// <summary>
/// Provider-private state for an active event: its context stream, selected
/// provider mode, and any suspended controlled random-choice continuation.
/// </summary>
public sealed record EventStochasticStateV111(
    RngStreamState? NativeRng = null,
    string? ProviderMode = null,
    PrototypeControlledEventChoiceContinuationV111? ControlledChoice = null)
{
    public EventStochasticStateV111 Fork() => this with
    {
        NativeRng = NativeRng?.Fork(),
        ControlledChoice = ControlledChoice?.Fork()
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
}
