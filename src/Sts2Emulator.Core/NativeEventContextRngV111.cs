namespace Sts2Emulator.Core;

/// <summary>
/// Owns EventModel.Begin-compatible event streams for configured native runs.
/// The stream is persisted on EventState and copied forward after each draw.
/// Scripted Trash Heap, Tablet of Truth, Trial, Endless Conveyor dish-ticket,
/// Native event-gold, and event-shuffle resolutions retain counterfactual
/// provider markers, with event-specific cursor and result invariants checked
/// at the boundary.
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
        NativeEventRandomProbeV111.CaptureUnitFloat(state, request);
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

        NativeEventRandomProbeV111.CaptureShuffle(siteId, ownerId, values);

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
        var scriptedReflectionsChoiceIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            PrototypeGameEngine.IsValidScriptedEventShuffleState(
                eventState);
        if (prototypeEventId == PrototypeNativeGloryEvents.ReflectionsId &&
            providerMode == "scripted-v111" &&
            scriptedReflectionsChoiceIsExpected)
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
        var scriptedFakeMerchantShuffleIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            PrototypeGameEngine.IsValidScriptedEventShuffleState(
                eventState);
        if (prototypeEventId == PrototypeNativeFakeMerchant.EventId &&
            providerMode == "scripted-v111" &&
            scriptedFakeMerchantShuffleIsExpected &&
            state.Phase == RunPhase.Event &&
            state.World?.Combat is null)
            return;
        if (prototypeEventId == PrototypeNativeEndlessConveyor.EventId &&
            providerMode == "scripted-v111" &&
            PrototypeNativeEndlessConveyor.IsValidScriptedDishState(
                state, eventState))
            return;
        var scriptedInfestedAutomatonOfferIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            PrototypeNativeHiveEvents.IsValidScriptedInfestedAutomatonState(
                state, eventState);
        if (prototypeEventId ==
                PrototypeNativeHiveEvents.InfestedAutomatonId &&
            providerMode == "scripted-v111" &&
            scriptedInfestedAutomatonOfferIsExpected)
            return;
        var scriptedBattlewornShuffleIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            PrototypeGameEngine.IsValidScriptedEventShuffleState(
                eventState);
        if (prototypeEventId ==
                PrototypeNativeGloryEvents.BattlewornDummyId &&
            providerMode == "scripted-v111" &&
            scriptedBattlewornShuffleIsExpected &&
            state.Phase == RunPhase.Event &&
            state.World?.Combat is null &&
            state.World?.EncounterIds.LastOrDefault() ==
                PrototypeNativeGloryEvents.BattlewornV2EncounterId)
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
        var scriptedGoldEntryIsExpected =
            NativeRngV111.ToSnapshot(stream) ==
                NativeRngV111.ToSnapshot(initial) &&
            eventState.NativePageIndex == 0 &&
            eventState.ChosenChoiceId is null &&
            (prototypeEventId switch
            {
                var id when id == PrototypeNativeDenseVegetation.EventId =>
                    eventState.NativeEventGold is >= 61 and <= 99 &&
                    eventState.NativeEventSecondaryGold == 0,
                "proto.native.event.whispering_hollow" =>
                    eventState.NativeEventGold is >= 26 and <= 44 &&
                    eventState.NativeEventSecondaryGold == 0,
                "proto.native.event.sunken_statue" or
                    "proto.native.underdocks.sunken_statue" =>
                    eventState.NativeEventGold is >= 101 and <= 121 &&
                    eventState.NativeEventSecondaryGold == 0,
                "proto.native.event.luminous_choir" =>
                    eventState.NativeEventGold is >= 100 and <= 149 &&
                    eventState.NativeEventSecondaryGold == 0,
                "proto.native.underdocks.sunken_treasury" =>
                    eventState.NativeEventGold is >= 52 and <= 67 &&
                    eventState.NativeEventSecondaryGold is >= 303 and <= 363,
                var id when id == PrototypeNativePunchOff.EventId =>
                    eventState.NativeEventGold is >= 91 and <= 98 &&
                    eventState.NativeEventSecondaryGold == 0,
                var id when id == PrototypeNativeSharedEvents.ThisOrThatId =>
                    eventState.NativeEventGold is >= 41 and <= 68 &&
                    eventState.NativeEventSecondaryGold == 0,
                var id when id == PrototypeNativeHiveEvents.LostWispId =>
                    eventState.NativeEventGold is >= 45 and <= 75 &&
                    eventState.NativeEventSecondaryGold == 0,
                "proto.native.event.jungle_maze_adventure" =>
                    eventState.NativeEventGold is >= 135 and <= 165 &&
                    eventState.NativeEventSecondaryGold is >= 35 and <= 65,
                _ => false
            });
        if (providerMode == "scripted-v111" &&
            scriptedGoldEntryIsExpected)
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
