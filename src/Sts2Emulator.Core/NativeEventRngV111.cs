namespace Sts2Emulator.Core;

/// <summary>
/// Native v111 event-local RNG lifetime, shared by Neow and other event models.
/// EventModel.BeginEvent creates a content-local stream from the native event
/// entry; it is independent of the 12 run and 3 player named streams.
/// Only explicit opt-in native-fidelity paths currently use this owner.
/// </summary>
public static class NativeEventRngV111
{
    public static RngBundle Begin(
        string runSeed, string eventEntry, int playerSlot = 0, bool shared = false) =>
        new([NativeRngV111Contexts.ForEvent(runSeed, eventEntry, playerSlot, shared)]);

    /// <summary>
    /// Retain an independently advanced event snapshot using the same native
    /// context identity as Begin. This is serialization/replay ownership only:
    /// the snapshot's source origin and game parity require separate evidence.
    /// </summary>
    public static RngBundle Restore(
        string runSeed, string eventEntry, NativeRngSnapshot snapshot,
        int playerSlot = 0, bool shared = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Counter < 0)
            throw new InvalidDataException("Native event RNG counter cannot be negative.");
        var stream = NativeRngV111Contexts.ForEvent(
            runSeed, eventEntry, playerSlot, shared);
        return new([NativeRngV111.FromSnapshot(stream.StreamId, snapshot)]);
    }

    /// <summary>
    /// An event keeps its RNG across choices and page transitions. Leaving
    /// that event, including entering another event, releases the former owner.
    /// First-time allocation occurs in the event-entering transition.
    /// </summary>
    public static RunState ReconcileAfterStep(RunState before, RunState after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.NativeEventRng is null || after.NativeEventRng is null)
            return after;
        var previousEvent = before.World?.Event?.EventId;
        if (before.Phase != RunPhase.Event || previousEvent is null)
            throw new InvalidDataException(
                "Native event-local RNG is attached without an active event.");
        return after.Phase == RunPhase.Event
            && StringComparer.Ordinal.Equals(previousEvent, after.World?.Event?.EventId)
                ? after
                : after with { NativeEventRng = null };
    }
}
