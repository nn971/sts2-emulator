namespace Sts2Emulator.Core;

/// <summary>
/// Owns EventModel.Begin-compatible event streams for configured native runs.
/// The stream is persisted on EventState and copied forward after each draw.
/// Scripted Trash Heap, Tablet of Truth, Trial, and event-shuffle resolutions retain
/// counterfactual provider markers, with event-specific cursor and result
/// invariants checked at the event boundary.
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
            ordinal ?? ResolveRequestOrdinal(nativeStream, bundle),
            Enumerable.Range(0, candidateCount).ToArray());
        var decision = StochasticProviderScopeV111.ResolveIndex(
            new NativeRandomIndexProviderV111(), request, bundle);
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
            ordinal ?? ResolveRequestOrdinal(nativeStream, bundle),
            Array.Empty<int>(),
            OperationKind: "bounded_integer",
            BoundedCount: boundedCount);
        var decision = StochasticProviderScopeV111.ResolveIndex(
            new NativeRandomIndexProviderV111(), request, bundle);
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

    public static (RunState State, float SelectedValue) ResolveUnitFloat(
        RunState state, EventState eventState, string nativeEventEntry,
        string siteId, string ownerId, int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(eventState);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEventEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

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
            throw new InvalidOperationException(
                "A native event unit-float draw requires its event RNG context.");

        var bundle = new RngBundle([nativeStream.Fork()]);
        var request = new RandomUnitFloatRequestV111(
            siteId,
            nativeStream.StreamId,
            ownerId,
            ordinal ?? ResolveRequestOrdinal(nativeStream, bundle));
        var decision = StochasticProviderScopeV111.ResolveUnitFloat(
            new NativeRandomUnitFloatProviderV111(), request, bundle);
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
        }, decision.SelectedValue);
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
            if (eventState.NativeEventRng is not null ||
                eventState.NativeEventRngProviderMode is not null)
                throw new InvalidOperationException(
                    "Legacy event state cannot own a native event RNG.");
            return;
        }

        // Null is accepted for a pre-migration save. ResolveIndex can rebuild
        // the unadvanced entry stream only while the event is still on page 0.
        if (eventState.NativeEventRng is not { } stream)
        {
            if (eventState.NativeEventRngProviderMode is not null)
                throw new InvalidOperationException(
                    "Native event provider mode is present without its context stream.");
            return;
        }
        var initial = NativeRngV111Contexts.ForEvent(
            state.RunSeed, nativeEventEntry);
        if (stream.StreamId != initial.StreamId)
            throw new InvalidOperationException(
                "Event RNG has the wrong native context.");
        if (eventState.NativeEventRngProviderMode is not { } providerMode)
            return;
        var scriptedTrashHeapRewardPresent =
            eventState.ChosenChoiceId switch
            {
                "grab" => state.World!.NextCardInstanceId > 0 &&
                    state.Player.Deck.Any(card =>
                        card.InstanceId == state.World!.NextCardInstanceId - 1 &&
                        PrototypeNativeUnderdocksTrashHeap.CardIds.Contains(
                            card.CardId, StringComparer.Ordinal)),
                "dive" => state.Player.Relics.Any(relic =>
                    PrototypeNativeUnderdocksTrashHeap.RelicIds.Contains(
                        relic.RelicId, StringComparer.Ordinal)),
                _ => false
            };
        if (prototypeEventId == PrototypeNativeUnderdocksTrashHeap.EventId &&
            providerMode == "scripted-v111" &&
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            eventState.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    prototypeEventId) &&
            PrototypeNativeEventCompletionPages.HasTerminalChoice(
                prototypeEventId, eventState.ChosenChoiceId) &&
            scriptedTrashHeapRewardPresent)
            return;
        var tabletPageIsExpected =
            eventState.NativePageIndex is >= 1 and <= 4 &&
                eventState.ChosenChoiceId is null ||
            eventState.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    PrototypeNativeTabletOfTruth.EventId) &&
                PrototypeNativeEventCompletionPages.HasTerminalChoice(
                    PrototypeNativeTabletOfTruth.EventId,
                    eventState.ChosenChoiceId);
        if (prototypeEventId == PrototypeNativeTabletOfTruth.EventId &&
            providerMode == "scripted-v111" && tabletPageIsExpected)
            return;
        var trialChoiceIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            (eventState.NativePageIndex is >= 2 and <= 4 &&
                eventState.ChosenChoiceId is null ||
             eventState.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    PrototypeNativeGloryEvents.TrialId) &&
             PrototypeNativeEventCompletionPages.HasTerminalChoice(
                PrototypeNativeGloryEvents.TrialId,
                eventState.ChosenChoiceId));
        if (prototypeEventId == PrototypeNativeGloryEvents.TrialId &&
            providerMode == "scripted-v111" && trialChoiceIsExpected)
            return;
        var scriptedFragrantMushroomShuffleIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            PrototypeGameEngine.IsValidScriptedEventShuffleState(
                eventState) &&
            state.Player.Relics.Any(relic => relic.RelicId ==
                PrototypeNativeGloryEvents.FragrantMushroomId);
        if (prototypeEventId ==
                PrototypeNativeGloryEvents.HungryForMushroomsId &&
            providerMode == "scripted-v111" &&
            scriptedFragrantMushroomShuffleIsExpected)
            return;
        var scriptedTinkerShuffleIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            PrototypeGameEngine.IsValidScriptedEventShuffleState(
                eventState);
        if (prototypeEventId == PrototypeNativeGloryEvents.TinkerTimeId &&
            providerMode == "scripted-v111" &&
            scriptedTinkerShuffleIsExpected)
            return;
        var doorsLightChoiceIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            eventState.NativePageIndex ==
                PrototypeNativeEventCompletionPages.TerminalChoicePageIndex(
                    PrototypeNativeUnderdocksDoors.EventId) &&
            eventState.ChosenChoiceId == "light";
        if (prototypeEventId == PrototypeNativeUnderdocksDoors.EventId &&
            providerMode == "scripted-v111" && doorsLightChoiceIsExpected)
            return;
        throw new InvalidOperationException(
            "Native event state cannot use this counterfactual RNG provider.");
    }

    private static int ResolveRequestOrdinal(
        RngStreamState stream, RngBundle bundle)
    {
        if (StochasticProviderScopeV111.GetNextOrdinal(
                bundle, stream.StreamId) is { } scopedOrdinal)
            return scopedOrdinal;
        var nativeCallCount = stream.CallCount ?? 0;
        if (nativeCallCount > int.MaxValue)
            throw new NotSupportedException(
                "Native event request ordinal exceeds the supported range.");
        return (int)nativeCallCount;
    }
}
