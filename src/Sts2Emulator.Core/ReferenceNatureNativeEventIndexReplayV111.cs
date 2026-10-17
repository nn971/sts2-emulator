namespace Sts2Emulator.Core;

/// <summary>Replays one atomic Native event index decision through its
/// ordinary player action while preserving the event stream cursor.</summary>
internal static class ReferenceNatureNativeEventIndexReplayV111
{
    public static RunState Replay(
        RunState origin, RandomIndexRequestV111 request,
        int selectedIndex, GameAction action, string nativeEventRngEventId,
        string eventName)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventRngEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var expectedStream = origin.World?.Event?.NativeEventRng ??
            NativeRngV111Contexts.ForEvent(
                origin.RunSeed, nativeEventRngEventId);
        var scripted = new ScriptedRandomIndexProviderV111(
        [
            new RandomIndexDecisionV111(
                request, selectedIndex, "scripted-v111")
        ]);
        using var providerScope = StochasticProviderScopeV111.Enter(
            scripted, overrideExplicitProviders: true);
        var run = new PrototypeGameEngine().Step(
            origin.Fork(), action).State;
        scripted.AssertComplete();
        if (CanonicalJson.Sha256(run.World?.Event?.NativeEventRng) !=
            CanonicalJson.Sha256(expectedStream))
            throw new InvalidDataException(
                $"Scripted {eventName} resolution advanced its Native event cursor.");
        var world = run.World ?? throw new InvalidDataException(
            $"Scripted {eventName} resolution lost its event world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            $"Scripted {eventName} resolution lost its event state.");
        return run with
        {
            World = world with
            {
                Event = eventState with
                {
                    NativeEventRngProviderMode = "scripted-v111"
                }
            }
        };
    }
}
