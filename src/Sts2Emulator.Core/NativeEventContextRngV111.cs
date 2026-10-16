namespace Sts2Emulator.Core;

/// <summary>
/// Owns EventModel.Begin-compatible event streams for configured native runs.
/// The stream is persisted on EventState and copied forward after each draw.
/// </summary>
public static class NativeEventContextRngV111
{
    public static RngStreamState? CreateAtEntry(
        RunState state, string? nativeEventEntry)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.NativeRunRng is null || nativeEventEntry is null
            ? null
            : NativeRngV111Contexts.ForEvent(
                state.RunSeed, nativeEventEntry);
    }

    public static (RunState State, int SelectedIndex) ResolveIndex(
        RunState state, EventState eventState, string nativeEventEntry,
        string siteId, string ownerId, int candidateCount,
        int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(eventState);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (candidateCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));

        var nativeStream = eventState.NativeEventRng;
        if (nativeStream is null && state.NativeRunRng is not null)
        {
            if (eventState.NativePageIndex != 0
                || eventState.ChosenChoiceId is not null
                || eventState.NativeEventPotionSlot is not null)
                throw new InvalidOperationException(
                    "Cannot reconstruct a missing native event RNG after event progress.");

            // An older save at the initial page has not sampled this event's
            // result yet, so its context stream still starts at event entry.
            nativeStream = NativeRngV111Contexts.ForEvent(
                state.RunSeed, nativeEventEntry);
        }
        if (nativeStream is null)
        {
            return (state, LegacyRandomChoiceV111.ChooseIndexRange(
                state.Rng, siteId, "event", ownerId,
                candidateCount).SelectedIndex);
        }

        var bundle = new RngBundle([nativeStream.Fork()]);
        var request = new RandomIndexRequestV111(
            siteId,
            nativeStream.StreamId,
            ownerId,
            ordinal ?? 0,
            Enumerable.Range(0, candidateCount).ToArray());
        var decision = new NativeRandomIndexProviderV111().Resolve(
            request, bundle);
        var world = state.World
            ?? throw new InvalidOperationException(
                "A native event RNG requires an active run world.");
        return (state with
        {
            World = world with
            {
                Event = eventState with
                {
                    NativeEventRng = bundle.Streams[0]
                }
            }
        }, decision.SelectedIndex);
    }

    public static (RunState State, int SelectedValue)
        ResolveBoundedInteger(
            RunState state, EventState eventState, string nativeEventEntry,
            string siteId, string ownerId, int boundedCount,
            int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(eventState);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (boundedCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boundedCount));

        var nativeStream = eventState.NativeEventRng;
        if (nativeStream is null && state.NativeRunRng is not null)
        {
            if (eventState.NativePageIndex != 0
                || eventState.ChosenChoiceId is not null
                || eventState.NativeEventPotionSlot is not null)
                throw new InvalidOperationException(
                    "Cannot reconstruct a missing native event RNG after event progress.");
            nativeStream = NativeRngV111Contexts.ForEvent(
                state.RunSeed, nativeEventEntry);
        }
        if (nativeStream is null)
        {
            return (state,
                LegacyRandomChoiceV111.ChooseBoundedInteger(
                    state.Rng, siteId, "event", ownerId,
                    boundedCount).SelectedIndex);
        }

        var bundle = new RngBundle([nativeStream.Fork()]);
        var request = new RandomIndexRequestV111(
            siteId,
            nativeStream.StreamId,
            ownerId,
            ordinal ?? 0,
            Array.Empty<int>(),
            OperationKind: "bounded_integer",
            BoundedCount: boundedCount);
        var decision = new NativeRandomIndexProviderV111().Resolve(
            request, bundle);
        var world = state.World
            ?? throw new InvalidOperationException(
                "A native event RNG requires an active run world.");
        return (state with
        {
            World = world with
            {
                Event = eventState with
                {
                    NativeEventRng = bundle.Streams[0]
                }
            }
        }, decision.SelectedIndex);
    }

    public static RunState ShuffleInPlace<T>(
        RunState state, EventState eventState, string nativeEventEntry,
        IList<T> values, string siteId, string ownerId,
        int? ordinalOffset = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(eventState);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventEntry);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinalOffset is < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinalOffset));

        var nativeStream = eventState.NativeEventRng;
        if (nativeStream is null && state.NativeRunRng is not null)
        {
            if (eventState.NativePageIndex != 0
                || eventState.ChosenChoiceId is not null
                || eventState.NativeEventPotionSlot is not null)
                throw new InvalidOperationException(
                    "Cannot reconstruct a missing native event RNG after event progress.");
            nativeStream = NativeRngV111Contexts.ForEvent(
                state.RunSeed, nativeEventEntry);
        }
        if (nativeStream is null)
        {
            _ = LegacyFisherYatesV111.ShuffleInPlace(
                values, state.Rng, "event", siteId, ownerId,
                ordinalOffset);
            return state;
        }

        var bundle = new RngBundle([nativeStream.Fork()]);
        _ = NativeFisherYatesV111.ShuffleInPlace(
            values, bundle, new NativeRandomIndexProviderV111(),
            nativeStream.StreamId, ownerId, siteId,
            ordinalOffset ?? 0);
        var world = state.World
            ?? throw new InvalidOperationException(
                "A native event RNG requires an active run world.");
        return state with
        {
            World = world with
            {
                Event = eventState with
                {
                    NativeEventRng = bundle.Streams[0]
                }
            }
        };
    }

    public static void Validate(
        RunState state, string prototypeEventId, string nativeEventEntry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventEntry);
        if (state.World?.Event is not { } eventState
            || eventState.EventId != prototypeEventId)
            return;

        if (state.NativeRunRng is null)
        {
            if (eventState.NativeEventRng is not null)
                throw new InvalidOperationException(
                    "Legacy event state cannot own a native event RNG.");
            return;
        }

        // Null is accepted for a pre-migration save. ResolveIndex can rebuild
        // the unadvanced entry stream only while the event is still on page 0.
        if (eventState.NativeEventRng is not { } stream)
            return;
        if (eventState.NativeEventRngProviderMode is not null)
            throw new InvalidOperationException(
                "Native event state cannot use a counterfactual RNG provider.");

        var initial = NativeRngV111Contexts.ForEvent(
            state.RunSeed, nativeEventEntry);
        if (stream.StreamId != initial.StreamId)
            throw new InvalidOperationException(
                "Event RNG has the wrong native context.");
    }
}
