using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed record NativeEventShufflePlanV111(
    string EventId,
    string NativeRngEventId,
    string SiteId,
    string ActionId,
    GameAction ReplayAction,
    string[] OrderedCandidateIds,
    int CandidateCount,
    string StreamId,
    string OwnerId,
    string ConditioningFingerprint,
    NativeRngSnapshot? NativeCursorBefore = null,
    bool PreserveEventRng = true,
    string? ExpectedUnscriptedStreamId = null,
    int ExpectedUnscriptedDrawCount = 0)
{
    public bool IsFishingRodUpgrade =>
        SiteId == PrototypeGameEngine.FishingRodUpgradeSiteId;
    public bool IsImprovementUpgrade =>
        SiteId == PrototypeGameEngine.ImprovementCardSiteId;
    public bool IsPaelsToothReturn =>
        SiteId == PrototypeGameEngine.PaelsToothCardReturnSiteId;
    public bool IsCombatEndIndex => IsFishingRodUpgrade ||
        IsImprovementUpgrade || IsPaelsToothReturn;
    public int SwapCount => CandidateCount - 1;
    public bool IsRepyChestOffer => SiteId == PrototypeNativeHiveEvents
        .WarHistorianRepyRelicShuffleSiteId;
    public int TotalChoiceCount => SwapCount + (IsRepyChestOffer ? 2 : 0);

    public RandomIndexRequestV111 Request(int ordinal)
    {
        if (ordinal < 0 || ordinal >= SwapCount)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        return new RandomIndexRequestV111(
            SiteId, StreamId, OwnerId, ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: CandidateCount - ordinal);
    }

    public RandomIndexRequestV111 RequestForChoice(int ordinal)
    {
        if (ordinal < SwapCount)
            return Request(ordinal);
        if (!IsRepyChestOffer || ordinal >= TotalChoiceCount ||
            NativeCursorBefore is null)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var potionOrdinal = ordinal - SwapCount;
        return new RandomIndexRequestV111(
            "event.war_historian.repy.potion_offer.index_draw",
            NativePlayerRngV111.RewardsStreamId,
            $"event:war_historian_repy:potion_offer:{potionOrdinal}",
            checked(NativeCursorBefore.Counter + SwapCount + potionOrdinal),
            Enumerable.Range(0, PrototypeContent.PotionPool.Length).ToArray(),
            OperationKind: "choose_index");
    }
}

internal static class NativeEventShufflePlanBuilderV111
{
    public static NativeEventShufflePlanV111 Build(
        RunState origin, string? siteId, string? actionId,
        string[]? plannedCandidateIds = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var fakeMerchantEntryAction =
            origin.Phase == RunPhase.MapChoice &&
            siteId == PrototypeNativeFakeMerchant
                .RelicOfferShuffleSiteId &&
            origin.World?.Event is null;
        var warHammerEliteVictoryAction =
            origin.Phase == RunPhase.Combat &&
            (siteId is null || siteId ==
                "relic.war_hammer.elite_upgrade.shuffle_swap_index") &&
            origin.World is { ActiveRoom: PrototypeRoomType.Elite,
                Event: null, Combat: not null } &&
            origin.Player.ActiveRelics.Any(relic =>
                relic.RelicId == NativeGloryAncientsV111.WarHammerId);
        var fishingRodVictoryAction =
            origin.Phase == RunPhase.Combat &&
            (siteId is null || siteId ==
                PrototypeGameEngine.FishingRodUpgradeSiteId) &&
            origin.World is { ActiveRoom: PrototypeRoomType.Combat,
                Event: null, Combat: not null } &&
            origin.Player.Relics.Any(relic =>
                relic.RelicId == PrototypeNativeOvergrowthEvents
                    .NeowRelicId("FishingRod") && !relic.IsMelted);
        var improvementVictoryAction =
            origin.Phase == RunPhase.Combat &&
            (siteId is null || siteId ==
                PrototypeGameEngine.ImprovementCardSiteId) &&
            origin.World is { Event: null, Combat: not null } &&
            origin.World.Combat.PlayerPowers.Any(power =>
                power.PowerId == PrototypeNativeGloryEvents.ImprovementId &&
                power.Stacks > 0);
        var paelsToothVictoryAction =
            origin.Phase == RunPhase.Combat &&
            (siteId is null || siteId ==
                PrototypeGameEngine.PaelsToothCardReturnSiteId) &&
            origin.World is { Event: null, Combat: not null } &&
            origin.Player.Relics.Any(relic => !relic.IsMelted &&
                relic.RelicId == NativePaelRewardsV111.ToothId &&
                NativePaelRewardsV111.ToothCards(relic).Length > 0);
        var world = origin.World;
        var activeEvent = world?.Event;
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null && !improvementVictoryAction &&
                !paelsToothVictoryAction ||
            world is null ||
            activeEvent is null && !fakeMerchantEntryAction &&
                !warHammerEliteVictoryAction &&
                !fishingRodVictoryAction &&
                !improvementVictoryAction &&
                !paelsToothVictoryAction ||
            activeEvent?.ControlledChoice is not null ||
            activeEvent?.NativeEventPotionSlot is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Native shuffle Nature choices require an active event, an admitted combat-victory hook, or Fake Merchant entry.");
        var evt = activeEvent ?? new EventState(
            fakeMerchantEntryAction
                ? PrototypeNativeFakeMerchant.EventId
                : fishingRodVictoryAction
                    ? "run.combat.fishing_rod"
                    : improvementVictoryAction
                        ? "run.combat.improvement_upgrade"
                        : paelsToothVictoryAction
                            ? "run.combat.paels_tooth"
                            : "run.combat.elite_war_hammer");
        siteId ??= evt.EventId switch
        {
            PrototypeNativeUnderdocksDoors.EventId =>
                PrototypeNativeUnderdocksDoors.ShuffleSiteId,
            PrototypeNativeGloryEvents.HungryForMushroomsId when
                evt.NativePageIndex == 0 =>
                    PrototypeNativeGloryEvents
                        .HungryForMushroomsShuffleSiteId,
            PrototypeNativeGloryEvents.TinkerTimeId when
                evt.NativePageIndex == 0 =>
                    PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId,
            PrototypeNativeGloryEvents.TinkerTimeId when
                evt.NativePageIndex == 1 =>
                    PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId,
            PrototypeNativeHiveEvents.WarHistorianRepyId when
                evt.NativePageIndex == 0 =>
                    PrototypeNativeHiveEvents
                        .WarHistorianRepyRelicShuffleSiteId,
            _ when warHammerEliteVictoryAction =>
                "relic.war_hammer.elite_upgrade.shuffle_swap_index",
            _ when fishingRodVictoryAction =>
                PrototypeGameEngine.FishingRodUpgradeSiteId,
            _ when improvementVictoryAction =>
                PrototypeGameEngine.ImprovementCardSiteId,
            _ when paelsToothVictoryAction =>
                PrototypeGameEngine.PaelsToothCardReturnSiteId,
            _ => throw new InvalidOperationException(
                "No native event shuffle is pending at this event page.")
        };
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (evt.EventId == PrototypeNativeHiveEvents.WarHistorianRepyId &&
            siteId == "event.war_historian.repy.potion_offer.index_draw")
            siteId = PrototypeNativeHiveEvents
                .WarHistorianRepyRelicShuffleSiteId;

        var battlewornVictoryAction =
            origin.Phase == RunPhase.Combat &&
            evt.EventId == PrototypeNativeGloryEvents.BattlewornDummyId &&
            evt.NativePageIndex == 2 &&
            evt.ChosenChoiceId == "setting_2";
        if (origin.Phase != RunPhase.Event && !battlewornVictoryAction &&
            !warHammerEliteVictoryAction &&
            !fishingRodVictoryAction &&
            !improvementVictoryAction &&
            !paelsToothVictoryAction &&
            !fakeMerchantEntryAction)
            throw new InvalidOperationException(
                "Native shuffle Nature choices require a pending event action, an admitted combat-victory hook, or Fake Merchant entry.");
        if (origin.Phase == RunPhase.Event && evt.ChosenChoiceId is not null)
            throw new InvalidOperationException(
                "Native event shuffle Nature choices require an uncommitted event action.");

        string nativeRngEventId;
        string resolvedActionId;
        GameAction? replayAction = null;
        string[] orderedCandidateIds;
        int candidateCount;
        string ownerId;
        NativeRngSnapshot? nativeCursorBefore = null;
        if (fishingRodVictoryAction && siteId ==
            PrototypeGameEngine.FishingRodUpgradeSiteId)
        {
            resolvedActionId = actionId ?? throw new InvalidOperationException(
                "Fishing Rod upgrade requires the stable ID of its victory action.");
            replayAction = new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(action =>
                    PrototypeAiEnvironment.StableActionId(action) ==
                    resolvedActionId);
            if (replayAction is null)
                throw new InvalidOperationException(
                    "Fishing Rod upgrade action is not legal in the current combat state.");
            var capture = CaptureFishingRodRequests(
                origin, replayAction, [], allowNativeTail: true);
            var requests = capture.SiteRequests;
            if (capture.Run.Phase != RunPhase.Reward ||
                capture.Run.World?.Combat is not null || requests.Length == 0)
                throw new InvalidOperationException(
                    "Fishing Rod ReferenceNature requires a combat action that immediately wins and triggers at least one upgrade draw.");
            var firstRequest = requests[0].Request;
            orderedCandidateIds = firstRequest.OrderedCandidates
                .Select(index => index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            candidateCount = firstRequest.SupportCount;
            ownerId = firstRequest.OwnerId;
            nativeRngEventId = string.Empty;
            var nativeStreams = origin.World?.Combat?.NativeRng?.RunStreams
                ?? origin.NativeRunRng;
            var niche = nativeStreams?.Streams.SingleOrDefault(stream =>
                stream.StreamId == "run.niche")
                ?? throw new InvalidOperationException(
                    "Fishing Rod upgrade requires the Native run.niche stream.");
            nativeCursorBefore = NativeRngV111.ToSnapshot(niche);
        }
        else if (improvementVictoryAction && siteId ==
            PrototypeGameEngine.ImprovementCardSiteId)
        {
            resolvedActionId = actionId ?? throw new InvalidOperationException(
                "Improvement upgrade requires the stable ID of its victory action.");
            replayAction = new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(action =>
                    PrototypeAiEnvironment.StableActionId(action) ==
                    resolvedActionId);
            if (replayAction is null)
                throw new InvalidOperationException(
                    "Improvement upgrade action is not legal in the current combat state.");
            var capture = CaptureImprovementRequests(
                origin, replayAction, [], allowLegacyTail: true);
            var requests = capture.SiteRequests;
            if (capture.Run.Phase != RunPhase.Reward ||
                capture.Run.World?.Combat is not null || requests.Length == 0)
                throw new InvalidOperationException(
                    "Improvement ReferenceNature requires a combat action that immediately wins and triggers at least one upgrade draw.");
            var firstRequest = requests[0].Request;
            orderedCandidateIds = firstRequest.OrderedCandidates
                .Select(index => index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            candidateCount = firstRequest.SupportCount;
            ownerId = firstRequest.OwnerId;
            nativeRngEventId = string.Empty;
        }
        else if (paelsToothVictoryAction && siteId ==
            PrototypeGameEngine.PaelsToothCardReturnSiteId)
        {
            resolvedActionId = actionId ?? throw new InvalidOperationException(
                "Pael's Tooth card return requires the stable ID of its victory action.");
            replayAction = new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(action =>
                    PrototypeAiEnvironment.StableActionId(action) ==
                    resolvedActionId);
            if (replayAction is null)
                throw new InvalidOperationException(
                    "Pael's Tooth card return action is not legal in the current combat state.");
            var capture = CapturePaelsToothRequests(
                origin, replayAction, [], allowLegacyTail: true);
            var requests = capture.SiteRequests;
            if (capture.Run.Phase != RunPhase.Reward ||
                capture.Run.World?.Combat is not null || requests.Length == 0)
                throw new InvalidOperationException(
                    "Pael's Tooth ReferenceNature requires a combat action that immediately wins and returns at least one saved card.");
            var firstRequest = requests[0].Request;
            orderedCandidateIds = firstRequest.OrderedCandidates
                .Select(index => index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            candidateCount = firstRequest.SupportCount;
            ownerId = firstRequest.OwnerId;
            nativeRngEventId = string.Empty;
        }
        else if (fakeMerchantEntryAction)
        {
            resolvedActionId = actionId ?? throw new InvalidOperationException(
                "Fake Merchant shuffle requires the stable ID of its legal Event-node action.");
            replayAction = new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(action =>
                    PrototypeAiEnvironment.StableActionId(action) ==
                    resolvedActionId);
            var mapNodeId = replayAction?.Kind == "choose_map_node"
                ? replayAction.ReadPayload<ChooseMapNodePayload>().NodeId
                : null;
            if (mapNodeId is null ||
                !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                    .Any(node => node.NodeId == mapNodeId &&
                        node.RoomType == PrototypeRoomType.Event))
                throw new InvalidOperationException(
                    "Fake Merchant shuffle requires a current legal Event-node action.");
            orderedCandidateIds = plannedCandidateIds is null
                ? [] : (string[])plannedCandidateIds.Clone();
            ownerId = "event:" + PrototypeNativeFakeMerchant.EventId +
                ":stock_relics";
            candidateCount = orderedCandidateIds.Length;
            nativeRngEventId = PrototypeNativeFakeMerchant.NativeRngEventId;
        }
        else if (warHammerEliteVictoryAction && siteId ==
            "relic.war_hammer.elite_upgrade.shuffle_swap_index")
        {
            if (world.ActiveRoom != PrototypeRoomType.Elite ||
                world.Combat is null ||
                origin.Player.ActiveRelics.Count(relic =>
                    relic.RelicId == NativeGloryAncientsV111.WarHammerId) != 1)
                throw new InvalidOperationException(
                    "War Hammer shuffle requires one active War Hammer during an Elite combat.");
            resolvedActionId = actionId ?? throw new InvalidOperationException(
                "War Hammer shuffle requires the stable ID of its victory action.");
            replayAction = new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(action =>
                    PrototypeAiEnvironment.StableActionId(action) ==
                    resolvedActionId);
            if (replayAction is null)
                throw new InvalidOperationException(
                    "War Hammer shuffle action is not legal in the current combat state.");
            using var shuffleProbe = NativeEventRandomProbeV111.Begin();
            using var providerScope = StochasticProviderScopeV111.Enter(
                new NativeRandomIndexProviderV111(),
                overrideExplicitProviders: false);
            var projected = new PrototypeGameEngine().Step(
                origin.Fork(), replayAction).State;
            var captures = shuffleProbe.ShuffleCaptures.Where(capture =>
                capture.SiteId == siteId).ToArray();
            if (projected.Phase != RunPhase.Reward ||
                projected.World?.Combat is not null || captures.Length != 1 ||
                captures[0].NativeStreamId != "run.niche" ||
                captures[0].NativeCursorBefore is null)
                throw new InvalidOperationException(
                    "War Hammer shuffle requires an immediate Elite victory with exactly one run.niche shuffle.");
            orderedCandidateIds = captures[0].OrderedCandidateIds;
            candidateCount = orderedCandidateIds.Length;
            ownerId = captures[0].OwnerId;
            nativeCursorBefore = captures[0].NativeCursorBefore;
            var capturedCursor = nativeCursorBefore ?? throw new InvalidOperationException(
                "War Hammer shuffle capture is missing its Native cursor.");
            var finalNicheCursor = projected.NativeRunRng?.Streams
                .SingleOrDefault(stream => stream.StreamId == "run.niche");
            if (plannedCandidateIds is not null &&
                !orderedCandidateIds.SequenceEqual(
                    plannedCandidateIds, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "War Hammer Nature candidate order changed since session entry.");
            if (finalNicheCursor is null ||
                unchecked((uint)(NativeRngV111.ToSnapshot(finalNicheCursor).Counter -
                    capturedCursor.Counter)) !=
                    unchecked((uint)Math.Max(0, candidateCount - 1)))
                throw new InvalidOperationException(
                    "War Hammer victory has additional run.niche draws after its shuffle and is outside this atomic boundary.");
            nativeRngEventId = string.Empty;
        }
        else if (evt.EventId ==
            PrototypeNativeHiveEvents.WarHistorianRepyId &&
            siteId == PrototypeNativeHiveEvents
                .WarHistorianRepyRelicShuffleSiteId)
        {
            resolvedActionId = actionId ?? "unlock_chest";
            if (!PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
                evt.NativePageIndex != 0 ||
                evt.ChosenChoiceId is not null ||
                evt.RepyFirstChoiceId is not null ||
                evt.RepyChestPotionIds is not null ||
                evt.RepyChestRelicIds is not null ||
                evt.RepyChestGroupsResolved != 0 ||
                evt.NativeEventRng is not null ||
                evt.NativeEventRngProviderMode is not null ||
                resolvedActionId != "unlock_chest")
                throw new InvalidOperationException(
                    "War Historian Repy shuffle requires its initial native chest action before reward generation.");
            replayAction = new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(action => action.Kind == "event_choice" &&
                    action.ReadPayload<EventChoicePayload>().ChoiceId ==
                    resolvedActionId);
            if (replayAction is null)
                throw new InvalidOperationException(
                    "War Historian Repy chest action is not legal in the current event state.");

            using var shuffleProbe = NativeEventRandomProbeV111.Begin();
            using var journal = RandomBoundaryJournalV111.Begin();
            using var eventJournal = NativeEngineEventJournalV111.Begin();
            var projected = new PrototypeGameEngine().Step(
                origin.Fork(), replayAction).State;
            var captures = shuffleProbe.ShuffleCaptures.Where(capture =>
                capture.SiteId == siteId).ToArray();
            var candidates =
                PrototypeNativeHiveEvents.WarHistorianRepyRelicOfferCandidates(
                    origin.Player);
            var rewardStreamBefore = origin.NativeRunRng?.Streams
                .SingleOrDefault(stream => stream.StreamId ==
                    NativePlayerRngV111.RewardsStreamId);
            var rewardStreamAfter = projected.NativeRunRng?.Streams
                .SingleOrDefault(stream => stream.StreamId ==
                    NativePlayerRngV111.RewardsStreamId);
            var rewardRequests = journal.Entries.Where(entry =>
                entry.Request.StreamId ==
                    NativePlayerRngV111.RewardsStreamId).ToArray();
            var expectedSwapCount = Math.Max(0, candidates.Length - 1);
            if (captures.Length != 1 || rewardStreamBefore is null ||
                rewardStreamAfter is null ||
                captures[0].NativeStreamId !=
                    NativePlayerRngV111.RewardsStreamId ||
                captures[0].NativeCursorBefore is null ||
                captures[0].OwnerId !=
                    "event:war_historian_repy:relic_offer" ||
                !captures[0].OrderedCandidateIds.SequenceEqual(
                    candidates, StringComparer.Ordinal) ||
                NativeRngV111.ToSnapshot(rewardStreamBefore) !=
                    captures[0].NativeCursorBefore ||
                projected.Phase != RunPhase.Reward ||
                projected.World?.Event is not
                    {
                        EventId: PrototypeNativeHiveEvents.WarHistorianRepyId,
                        NativePageIndex: 2,
                        ChosenChoiceId: "unlock_chest",
                        NativeEventRng: null,
                        NativeEventRngProviderMode: null,
                        RepyChestPotionIds.Length: 2,
                        RepyChestRelicIds.Length: 2,
                        RepyChestGroupsResolved: 1
                    } ||
                rewardRequests.Length != expectedSwapCount + 2 ||
                rewardRequests.Take(expectedSwapCount).Where((entry, index) =>
                    entry.Request.SiteId != siteId ||
                    entry.Request.OwnerId !=
                        "event:war_historian_repy:relic_offer" ||
                    entry.Request.Ordinal != index ||
                    entry.Request.OperationKind != "bounded_integer" ||
                    entry.Request.BoundedCount != candidates.Length - index ||
                    entry.ProviderMode != "native-v111").Any() ||
                rewardRequests.Skip(expectedSwapCount).Where((entry, index) =>
                    entry.Request.SiteId !=
                        "event.war_historian_repy.potion_offer.index_draw" ||
                    entry.Request.OwnerId !=
                        $"event:war_historian_repy:potion_offer:{index}" ||
                    entry.Request.OperationKind != "choose_index" ||
                    entry.Request.SupportCount !=
                        PrototypeContent.PotionPool.Length ||
                    entry.ProviderMode != "native-v111").Any() ||
                unchecked((uint)(NativeRngV111.ToSnapshot(
                    rewardStreamAfter).Counter -
                    NativeRngV111.ToSnapshot(rewardStreamBefore).Counter)) !=
                    unchecked((uint)(expectedSwapCount + 2)))
                throw new InvalidOperationException(
                    "War Historian Repy chest action must perform exactly one relic shuffle and two player.rewards potion draws.");
            orderedCandidateIds = captures[0].OrderedCandidateIds;
            candidateCount = orderedCandidateIds.Length;
            ownerId = captures[0].OwnerId;
            nativeCursorBefore = captures[0].NativeCursorBefore;
            if (plannedCandidateIds is not null &&
                !orderedCandidateIds.SequenceEqual(
                    plannedCandidateIds, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "War Historian Repy Nature candidate order changed since session entry.");
            nativeRngEventId = string.Empty;
        }
        else if (evt.EventId == PrototypeNativeGloryEvents.BattlewornDummyId &&
            siteId == PrototypeNativeGloryEvents
                .BattlewornUpgradeShuffleSiteId)
        {
            resolvedActionId = actionId ?? throw new InvalidOperationException(
                "Battleworn upgrade shuffle requires the stable ID of its victory action.");
            if (!battlewornVictoryAction || origin.World?.Act != 3 ||
                evt.NativeEventRngProviderMode is not null ||
                origin.World.Combat is not { } combat ||
                combat.Enemies.Any(enemy => enemy.Escaped))
                throw new InvalidOperationException(
                    "Battleworn upgrade shuffle requires a live setting 2 combat action before victory.");
            var legalActions = new PrototypeGameEngine()
                .GetLegalActions(origin);
            replayAction = legalActions.SingleOrDefault(action =>
                PrototypeAiEnvironment.StableActionId(action) ==
                resolvedActionId);
            if (replayAction is null)
                throw new InvalidOperationException(
                    "Battleworn upgrade shuffle action is not legal in the current combat state.");
            orderedCandidateIds = plannedCandidateIds is null
                ? [] : (string[])plannedCandidateIds.Clone();
            candidateCount = orderedCandidateIds.Length;
            ownerId = "event:battleworn:setting:2:eligible_cards";
            nativeRngEventId = PrototypeNativeGloryEvents
                .BattlewornDummyNativeRngEventId;
        }
        else if (evt.EventId == PrototypeNativeUnderdocksDoors.EventId &&
            siteId == PrototypeNativeUnderdocksDoors.ShuffleSiteId)
        {
            resolvedActionId = actionId ?? "light";
            if (evt.NativePageIndex != 0 || resolvedActionId != "light" ||
                evt.NativeEventRngProviderMode is not null)
                throw new InvalidOperationException(
                    "Doors of Light and Dark shuffle requires its pending Light action.");
            var candidateCardInstanceIds = origin.Player.Deck
                .Where(card => PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Upgrade,
                    null, null, false))
                .Select(card => card.InstanceId)
                .ToArray();
            candidateCount = candidateCardInstanceIds.Length;
            orderedCandidateIds = candidateCardInstanceIds.Select(id =>
                id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            ownerId = "event:doors_light:eligible_cards:" +
                CanonicalJson.Sha256(candidateCardInstanceIds);
            nativeRngEventId =
                PrototypeNativeUnderdocksDoors.NativeRngEventId;
        }
        else if (evt.EventId ==
            PrototypeNativeGloryEvents.HungryForMushroomsId &&
            siteId == PrototypeNativeGloryEvents
                .HungryForMushroomsShuffleSiteId)
        {
            resolvedActionId = actionId ?? "fragrant_mushroom";
            if (origin.World?.Act != 3 || evt.NativePageIndex != 0 ||
                resolvedActionId != "fragrant_mushroom" ||
                evt.NativeEventRngProviderMode is not null ||
                origin.Player.Hp <= 15)
                throw new InvalidOperationException(
                    "Fragrant Mushroom shuffle requires its survivable initial action.");
            orderedCandidateIds = origin.Player.Deck
                .Where(card => PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Upgrade,
                    null, null, false))
                .Select(card => card.InstanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            candidateCount = orderedCandidateIds.Length;
            ownerId = $"event:{evt.EventId}:eligible_cards";
            nativeRngEventId = PrototypeNativeGloryEvents
                .HungryForMushroomsNativeRngEventId;
        }
        else if (evt.EventId == PrototypeNativeGloryEvents.TinkerTimeId &&
            siteId is PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId
                or PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId)
        {
            if (origin.World?.Act != 3)
                throw new InvalidOperationException(
                    "Tinker Time shuffle requires its Act 3 event state.");
            nativeRngEventId =
                PrototypeNativeGloryEvents.TinkerTimeNativeRngEventId;
            if (siteId ==
                PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId)
            {
                resolvedActionId = actionId ?? "choose_card_type";
                if (evt.NativePageIndex != 0 ||
                    resolvedActionId != "choose_card_type" ||
                    evt.NativeEventRngProviderMode is not null)
                    throw new InvalidOperationException(
                        "Tinker Time type shuffle requires its initial action.");
                orderedCandidateIds =
                    PrototypeGameEngine.TinkerTypeOfferCandidates();
                candidateCount = orderedCandidateIds.Length;
                ownerId = "event:glory_tinker:types";
            }
            else
            {
                resolvedActionId = actionId ?? "";
                if (evt.NativePageIndex != 1 ||
                    resolvedActionId is not ("attack" or "skill" or "power") ||
                    evt.OfferedChoiceIds?.Contains(
                        resolvedActionId, StringComparer.Ordinal) != true ||
                    evt.NativeTinkerType is not null ||
                    evt.NativeEventRngProviderMode != "scripted-v111")
                    throw new InvalidOperationException(
                        "Tinker Time rider shuffle requires a type offered by the prior scripted Nature shuffle.");
                orderedCandidateIds =
                    PrototypeGameEngine.TinkerRiderOfferCandidates(
                        resolvedActionId);
                candidateCount = orderedCandidateIds.Length;
                ownerId = "event:glory_tinker:type:" + resolvedActionId;
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"No native event shuffle resolver is registered for '{siteId}'.");
        }

        replayAction ??= GameAction.Create("event_choice",
            new EventChoicePayload(resolvedActionId));
        var stableActionId = PrototypeAiEnvironment.StableActionId(
            replayAction);
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(action =>
                PrototypeAiEnvironment.StableActionId(action) == stableActionId))
            throw new InvalidOperationException(
                "Native event shuffle action is not legal in the current state.");

        var isFishingRodUpgrade = siteId ==
            PrototypeGameEngine.FishingRodUpgradeSiteId;
        var isImprovementUpgrade = siteId ==
            PrototypeGameEngine.ImprovementCardSiteId;
        var isPaelsToothReturn = siteId ==
            PrototypeGameEngine.PaelsToothCardReturnSiteId;
        var isCombatEndIndex = isFishingRodUpgrade || isImprovementUpgrade ||
            isPaelsToothReturn;
        var isWarHammerShuffle = siteId ==
            "relic.war_hammer.elite_upgrade.shuffle_swap_index";
        var isRepyShuffle = siteId == PrototypeNativeHiveEvents
            .WarHistorianRepyRelicShuffleSiteId;
        NativeRngSnapshot? streamSnapshot;
        string streamId;
        if (isWarHammerShuffle || isFishingRodUpgrade)
        {
            if (nativeCursorBefore is null)
                throw new InvalidOperationException(
                    "War Hammer shuffle plan is missing its pre-draw Native cursor.");
            streamSnapshot = nativeCursorBefore;
            streamId = "run.niche";
        }
        else if (isImprovementUpgrade)
        {
            streamSnapshot = null;
            streamId = "combat";
        }
        else if (isPaelsToothReturn)
        {
            streamSnapshot = null;
            streamId = "reward";
        }
        else if (isRepyShuffle)
        {
            if (nativeCursorBefore is null)
                throw new InvalidOperationException(
                    "War Historian Repy shuffle plan is missing its player.rewards pre-draw cursor.");
            streamSnapshot = nativeCursorBefore;
            streamId = NativePlayerRngV111.RewardsStreamId;
        }
        else
        {
            var expectedStream = NativeRngV111Contexts.ForEvent(
                origin.RunSeed, nativeRngEventId);
            if (evt.NativeEventRng is { } savedStream &&
                savedStream.StreamId != expectedStream.StreamId)
                throw new InvalidOperationException(
                    "Native event shuffle has the wrong Native event stream context.");
            if (evt.NativeEventRng is null && evt.NativePageIndex != 0)
                throw new InvalidOperationException(
                    "Native event shuffle cannot reconstruct a missing event stream after page progress.");
            var stream = evt.NativeEventRng ?? expectedStream;
            if (NativeRngV111.ToSnapshot(stream) !=
                NativeRngV111.ToSnapshot(expectedStream))
                throw new InvalidOperationException(
                    "Native event shuffle is not at the event cursor owned by its Nature prefix.");
            streamSnapshot = NativeRngV111.ToSnapshot(stream);
            streamId = stream.StreamId;
        }

        if ((battlewornVictoryAction || fakeMerchantEntryAction) &&
            plannedCandidateIds is null)
        {
            RunState projected;
            NativeEventShuffleCaptureV111[] captures;
            using (var shuffleProbe = NativeEventRandomProbeV111.Begin())
            using (RandomBoundaryJournalV111.Begin())
            using (NativeEngineEventJournalV111.Begin())
            {
                projected = new PrototypeGameEngine().Step(
                    origin.Fork(), replayAction).State;
                captures = shuffleProbe.ShuffleCaptures.ToArray();
            }
            var projectedEvent = projected.World?.Event;
            if (battlewornVictoryAction &&
                (projected.Phase != RunPhase.Event ||
                 projected.World?.Combat is not null ||
                 projectedEvent is null ||
                 projectedEvent.NativePageIndex !=
                    PrototypeNativeEventCompletionPages
                        .TerminalChoicePageIndex(projectedEvent.EventId) ||
                 projectedEvent.ChosenChoiceId != "victory"))
                throw new InvalidOperationException(
                    "Battleworn shuffle action must complete setting 2 combat in victory.");
            if (fakeMerchantEntryAction &&
                (projected.Phase != RunPhase.Event ||
                 projectedEvent?.EventId != PrototypeNativeFakeMerchant.EventId))
                throw new InvalidOperationException(
                    "Fake Merchant shuffle action must enter Fake Merchant.");
            var shuffleCaptures = captures.Where(capture =>
                capture.SiteId == siteId && capture.OwnerId == ownerId)
                .ToArray();
            if (shuffleCaptures.Length != 1)
                throw new InvalidOperationException(
                    fakeMerchantEntryAction
                        ? "Fake Merchant entry must perform exactly one stock relic shuffle."
                        : "Battleworn victory must perform exactly one eligible-card shuffle.");
            orderedCandidateIds = shuffleCaptures[0].OrderedCandidateIds;
            candidateCount = orderedCandidateIds.Length;
            if (fakeMerchantEntryAction &&
                !orderedCandidateIds.SequenceEqual(
                    PrototypeNativeFakeMerchant.StockRelicIds,
                    StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Fake Merchant shuffle candidate order differs from its stock definition.");
        }
        if (fakeMerchantEntryAction && plannedCandidateIds is not null &&
            !orderedCandidateIds.SequenceEqual(
                PrototypeNativeFakeMerchant.StockRelicIds,
                StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Fake Merchant planned candidates differ from its stock definition.");

        if (candidateCount <= 1 && !isCombatEndIndex)
            throw new InvalidOperationException(
                "Native event shuffle has fewer than two candidates and performs no draw.");

        var fingerprint = CanonicalJson.Sha256(new
        {
            boundary = isFishingRodUpgrade
                ? "run.persistent_combat_end.index_choice"
                : isImprovementUpgrade
                    ? "combat.improvement.power.index_choice"
                    : isPaelsToothReturn
                        ? "relic.paels_tooth.card_return.index_choice"
                        : "native_event.fisher_yates_shuffle",
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            actionId = resolvedActionId,
            offeredChoiceIds = evt.OfferedChoiceIds,
            selectedTinkerType = evt.NativeTinkerType,
            orderedCandidateIds,
            stream = streamSnapshot,
            siteId,
            ownerId,
            policy = isFishingRodUpgrade
                ? ChanceLawEngineV111.NativeRandomIndexConditioningPolicyId
                : isImprovementUpgrade
                    ? ChanceLawEngineV111.RandomIndexConditioningPolicyId
                    : isPaelsToothReturn
                        ? ChanceLawEngineV111.RandomIndexConditioningPolicyId
                        : ChanceLawEngineV111.NativeEventShuffleConditioningPolicyId
        });
        return new(isWarHammerShuffle ? "run.combat.elite_war_hammer" :
                isFishingRodUpgrade ? "run.combat.fishing_rod" :
                isImprovementUpgrade ? "run.combat.improvement_upgrade" :
                isPaelsToothReturn ? "run.combat.paels_tooth" :
                evt.EventId,
            nativeRngEventId, siteId,
            resolvedActionId, replayAction, orderedCandidateIds, candidateCount,
            streamId, ownerId, fingerprint,
            isWarHammerShuffle || isFishingRodUpgrade || isRepyShuffle
                ? nativeCursorBefore : null,
            PreserveEventRng: !isRepyShuffle,
            ExpectedUnscriptedStreamId: isRepyShuffle
                ? NativePlayerRngV111.RewardsStreamId : null,
            ExpectedUnscriptedDrawCount: 0);
    }

    internal sealed record CombatEndIndexCaptureV111(
        RunState Run, RandomBoundaryEventV111[] SiteRequests);

    internal static CombatEndIndexCaptureV111 CaptureFishingRodRequests(
        RunState origin, GameAction action,
        IReadOnlyList<RandomIndexDecisionV111> scriptedPrefix,
        bool allowNativeTail)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(scriptedPrefix);
        var beforeStreams = origin.World?.Combat?.NativeRng?.RunStreams
            ?? origin.NativeRunRng;
        var beforeNiche = beforeStreams?.Streams.SingleOrDefault(stream =>
            stream.StreamId == "run.niche")
            ?? throw new InvalidOperationException(
                "Fishing Rod replay requires the Native run.niche stream.");
        var beforeCursor = NativeRngV111.ToSnapshot(beforeNiche);
        var provider = new CombatEndIndexPrefixProviderV111(
            scriptedPrefix, allowNativeTail,
            new NativeRandomIndexProviderV111());
        RunState run;
        RandomBoundaryEventV111[] entries;
        using (var journal = RandomBoundaryJournalV111.Begin())
        using (StochasticProviderScopeV111.EnterFiltered(
            provider, null,
            request => request.IndexRequest?.SiteId ==
                PrototypeGameEngine.FishingRodUpgradeSiteId,
            overrideExplicitProviders: true))
        {
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
            entries = journal.Entries.ToArray();
        }
        provider.AssertComplete();
        var siteRequests = entries.Where(entry => entry.Request.SiteId ==
            PrototypeGameEngine.FishingRodUpgradeSiteId).ToArray();
        var runNicheEvents = entries.Where(entry =>
            entry.Request.StreamId == "run.niche").ToArray();
        var rodId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "FishingRod");
        var validSiteRequests = siteRequests.All(entry =>
        {
            var request = entry.Request;
            const string ownerPrefix = "relic:";
            var slotMarker = $":slot:";
            var markerIndex = request.OwnerId.IndexOf(
                slotMarker, StringComparison.Ordinal);
            var slotText = markerIndex >= 0
                ? request.OwnerId[(markerIndex + slotMarker.Length)..]
                : string.Empty;
            return request.StreamId == "run.niche" &&
                request.OperationKind == "choose_index" &&
                request.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, request.SupportCount)) &&
                request.OwnerId.StartsWith(
                    ownerPrefix + rodId + slotMarker,
                    StringComparison.Ordinal) &&
                int.TryParse(slotText,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var slot) &&
                slot < origin.Player.Relics.Length &&
                origin.Player.Relics[slot].RelicId == rodId &&
                !origin.Player.Relics[slot].IsMelted;
        });
        var expectedNativeTail = siteRequests.Length -
            scriptedPrefix.Count;
        var niche = run.NativeRunRng?.Streams.SingleOrDefault(stream =>
            stream.StreamId == "run.niche");
        var actualAdvance = niche is null ? ulong.MaxValue : unchecked(
            (uint)(NativeRngV111.ToSnapshot(niche).Counter -
                beforeCursor.Counter));
        var expectedModes = siteRequests.Select((entry, index) =>
            entry.ProviderMode == (index < scriptedPrefix.Count
                ? "scripted-v111" : "native-v111")).All(matches => matches);
        if (siteRequests.Length < scriptedPrefix.Count ||
            runNicheEvents.Length != siteRequests.Length ||
            runNicheEvents.Any(entry => entry.Request.SiteId !=
                PrototypeGameEngine.FishingRodUpgradeSiteId) ||
            !validSiteRequests || !expectedModes ||
            actualAdvance != (ulong)expectedNativeTail ||
            run.Phase != RunPhase.Reward || run.World?.Combat is not null ||
            !allowNativeTail && siteRequests.Length != scriptedPrefix.Count)
            throw new InvalidDataException(
                "Fishing Rod replay differs from the captured immediate-victory index prefix or run.niche cursor.");
        return new CombatEndIndexCaptureV111(run, siteRequests);
    }

    internal static CombatEndIndexCaptureV111 CaptureImprovementRequests(
        RunState origin, GameAction action,
        IReadOnlyList<RandomIndexDecisionV111> scriptedPrefix,
        bool allowLegacyTail)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(scriptedPrefix);
        var beforeStream = origin.Rng.Streams.SingleOrDefault(stream =>
            stream.StreamId == "combat")
            ?? throw new InvalidOperationException(
                "Improvement replay requires the legacy combat stream.");
        var beforeCallCount = beforeStream.CallCount ?? 0;
        var legacyProvider = new LegacyPrototypeRandomIndexProviderV111();
        var provider = new CombatEndIndexPrefixProviderV111(
            scriptedPrefix, allowLegacyTail, legacyProvider);
        RunState run;
        RandomBoundaryEventV111[] entries;
        using (var journal = RandomBoundaryJournalV111.Begin())
        using (StochasticProviderScopeV111.EnterFiltered(
            provider, null,
            request => request.IndexRequest?.SiteId ==
                PrototypeGameEngine.ImprovementCardSiteId,
            overrideExplicitProviders: true))
        {
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
            entries = journal.Entries.ToArray();
        }
        provider.AssertComplete();
        var siteRequests = entries.Where(entry => entry.Request.SiteId ==
            PrototypeGameEngine.ImprovementCardSiteId).ToArray();
        var combatEvents = entries.Where(entry =>
            entry.Request.StreamId == "combat").ToArray();
        var validSiteRequests = siteRequests.Select((entry, index) =>
        {
            var request = entry.Request;
            var ownerPrefix = $"mad_science_improvement:pick:{index}:pool:";
            var ownerHash = request.OwnerId.StartsWith(
                ownerPrefix, StringComparison.Ordinal)
                ? request.OwnerId[ownerPrefix.Length..] : string.Empty;
            return request.StreamId == "combat" &&
                request.Ordinal == checked((int)(beforeCallCount +
                    (ulong)index)) &&
                request.OperationKind == "choose_index" &&
                request.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, request.SupportCount)) &&
                request.SupportCount <= origin.Player.Deck.Length &&
                ownerHash.Length == 64 && ownerHash.All(Uri.IsHexDigit);
        }).All(valid => valid);
        var expectedLegacyTail = siteRequests.Length -
            scriptedPrefix.Count;
        var afterStream = run.Rng.Streams.SingleOrDefault(stream =>
            stream.StreamId == "combat");
        var actualAdvance = afterStream is null ? ulong.MaxValue :
            unchecked((afterStream.CallCount ?? 0) - beforeCallCount);
        var expectedModes = siteRequests.Select((entry, index) =>
            entry.ProviderMode == (index < scriptedPrefix.Count
                ? "scripted-v111" : legacyProvider.Mode)).All(matches => matches);
        if (siteRequests.Length < scriptedPrefix.Count ||
            combatEvents.Length != siteRequests.Length ||
            combatEvents.Any(entry => entry.Request.SiteId !=
                PrototypeGameEngine.ImprovementCardSiteId) ||
            !validSiteRequests || !expectedModes ||
            actualAdvance != (ulong)expectedLegacyTail ||
            run.Phase != RunPhase.Reward || run.World?.Combat is not null ||
            !allowLegacyTail &&
                siteRequests.Length != scriptedPrefix.Count)
            throw new InvalidDataException(
                "Improvement replay differs from the captured immediate-victory index prefix or combat cursor.");
        return new CombatEndIndexCaptureV111(run, siteRequests);
    }

    internal static CombatEndIndexCaptureV111 CapturePaelsToothRequests(
        RunState origin, GameAction action,
        IReadOnlyList<RandomIndexDecisionV111> scriptedPrefix,
        bool allowLegacyTail)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(scriptedPrefix);
        var beforeStream = origin.Rng.Streams.SingleOrDefault(stream =>
            stream.StreamId == "reward")
            ?? throw new InvalidOperationException(
                "Pael's Tooth replay requires the legacy reward stream.");
        var beforeCallCount = beforeStream.CallCount ?? 0;
        var toothId = NativePaelRewardsV111.ToothId;
        var expectedReturns = origin.Player.Relics
            .Select((relic, index) => (Relic: relic, Index: index))
            .Where(item => !item.Relic.IsMelted &&
                item.Relic.RelicId == toothId)
            .Select(item => (item.Index,
                Saved: NativePaelRewardsV111.ToothCards(item.Relic)))
            .Where(item => item.Saved.Length > 0)
            .ToArray();
        var legacyProvider = new LegacyPrototypeRandomIndexProviderV111();
        var provider = new CombatEndIndexPrefixProviderV111(
            scriptedPrefix, allowLegacyTail, legacyProvider);
        RunState run;
        RandomBoundaryEventV111[] entries;
        using (var journal = RandomBoundaryJournalV111.Begin())
        using (StochasticProviderScopeV111.EnterFiltered(
            provider, null,
            request => request.IndexRequest?.SiteId ==
                PrototypeGameEngine.PaelsToothCardReturnSiteId,
            overrideExplicitProviders: true))
        {
            run = new PrototypeGameEngine().Step(
                origin.Fork(), action).State;
            entries = journal.Entries.ToArray();
        }
        provider.AssertComplete();
        var siteRequests = entries.Where(entry => entry.Request.SiteId ==
            PrototypeGameEngine.PaelsToothCardReturnSiteId).ToArray();
        var rewardEvents = entries.Where(entry =>
            entry.Request.StreamId == "reward").ToArray();
        var targetRequestsAreLeading = siteRequests.Length <=
            rewardEvents.Length && rewardEvents.Take(siteRequests.Length)
                .All(entry => entry.Request.SiteId ==
                    PrototypeGameEngine.PaelsToothCardReturnSiteId);
        var validSiteRequests = siteRequests.Select((entry, index) =>
        {
            if (index >= expectedReturns.Length)
                return false;
            var expected = expectedReturns[index];
            var request = entry.Request;
            var expectedOwner = $"player:0:relic:{expected.Index}:saved_cards:"
                + CanonicalJson.Sha256(expected.Saved);
            return request.StreamId == "reward" &&
                request.Ordinal == checked((int)(beforeCallCount +
                    (ulong)index)) &&
                request.OperationKind == "choose_index" &&
                request.OwnerId == expectedOwner &&
                request.SupportCount == expected.Saved.Length &&
                request.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, expected.Saved.Length));
        }).All(valid => valid);
        var rewardOrdinalsMatch = rewardEvents.Select((entry, index) =>
            beforeCallCount + (ulong)index <= int.MaxValue &&
            entry.Request.Ordinal == checked((int)(beforeCallCount +
                (ulong)index))).All(matches => matches);
        var scriptedCount = Math.Min(scriptedPrefix.Count,
            siteRequests.Length);
        var expectedLegacyDraws = rewardEvents.Length - scriptedCount;
        var afterStream = run.Rng.Streams.SingleOrDefault(stream =>
            stream.StreamId == "reward");
        var actualAdvance = afterStream is null ? ulong.MaxValue :
            unchecked((afterStream.CallCount ?? 0) - beforeCallCount);
        var expectedModes = siteRequests.Select((entry, index) =>
            entry.ProviderMode == (index < scriptedPrefix.Count
                ? "scripted-v111" : legacyProvider.Mode)).All(matches => matches);
        if (siteRequests.Length < scriptedPrefix.Count ||
            siteRequests.Length != expectedReturns.Length ||
            !targetRequestsAreLeading || !validSiteRequests ||
            !rewardOrdinalsMatch || !expectedModes ||
            actualAdvance != (ulong)expectedLegacyDraws ||
            run.Phase != RunPhase.Reward || run.World?.Combat is not null ||
            !allowLegacyTail && siteRequests.Length != scriptedPrefix.Count)
            throw new InvalidDataException(
                "Pael's Tooth replay differs from the captured immediate-victory saved-card prefix or reward cursor.");
        return new CombatEndIndexCaptureV111(run, siteRequests);
    }

    internal static CombatEndIndexCaptureV111
        CaptureCombatEndIndexRequests(
            RunState origin, NativeEventShufflePlanV111 plan,
            IReadOnlyList<RandomIndexDecisionV111> scriptedPrefix,
            bool allowTail) =>
        plan.IsFishingRodUpgrade
            ? CaptureFishingRodRequests(
                origin, plan.ReplayAction, scriptedPrefix, allowTail)
            : plan.IsImprovementUpgrade
                ? CaptureImprovementRequests(
                    origin, plan.ReplayAction, scriptedPrefix, allowTail)
                : plan.IsPaelsToothReturn
                    ? CapturePaelsToothRequests(
                        origin, plan.ReplayAction, scriptedPrefix, allowTail)
                : throw new InvalidOperationException(
                    "Combat-end capture requested for another stochastic site.");

    private sealed class CombatEndIndexPrefixProviderV111(
        IReadOnlyList<RandomIndexDecisionV111> scriptedPrefix,
        bool allowTail,
        IRandomIndexProviderV111 tailProvider) : IRandomIndexProviderV111
    {
        private readonly ScriptedRandomIndexProviderV111 _script =
            new(scriptedPrefix);

        public string Mode => "reference-nature-combat-end-prefix-v111";

        public RandomIndexDecisionV111 Resolve(
            RandomIndexRequestV111 request, RngBundle streams)
        {
            if (_script.Remaining > 0)
                return _script.Resolve(request, streams);
            if (!allowTail)
                throw new InvalidDataException(
                    "Combat-end replay reached an unscripted index draw.");
            return tailProvider.Resolve(request, streams);
        }

        public void AssertComplete() => _script.AssertComplete();
    }
}

public sealed record ReferenceNatureNativeEventShuffleSessionSnapshotV111(
    string SchemaId,
    string? SiteId,
    string? ActionId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>Atomic ReferenceNature replay of admitted Native event shuffles,
/// combat-victory hooks, and their ordinary mechanics actions.</summary>
public sealed record ReferenceNatureNativeEventShuffleSessionV111(
    string SchemaId,
    string SiteId,
    string ActionId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-native-event-shuffle-session-v1";

    internal string[] PlannedCandidateIds { get; init; } = [];

    private bool IsFishingRodUpgrade =>
        SiteId == PrototypeGameEngine.FishingRodUpgradeSiteId;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(new
    {
        session = this,
        plannedCandidateIds = PlannedCandidateIds
    });

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete
    {
        get
        {
            var plan = NativeEventShufflePlanBuilderV111.Build(
                Origin, SiteId, ActionId, PlannedCandidateIds);
            return plan.IsCombatEndIndex
                ? BuildCombatEndIndexPrefix().Capture.SiteRequests.Length ==
                    DecisionTranscript.Length
                : DecisionTranscript.Length == plan.TotalChoiceCount;
        }
    }

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var plan = NativeEventShufflePlanBuilderV111.Build(
                Origin, SiteId, ActionId, PlannedCandidateIds);
            if (plan.IsCombatEndIndex)
            {
                var prefix = BuildCombatEndIndexPrefix();
                if (prefix.Capture.SiteRequests.Length ==
                    prefix.Decisions.Length)
                    return null;
                return QueryCombatEndLaw(plan,
                    prefix.Capture.SiteRequests[
                        prefix.Decisions.Length].Request);
            }
            return QueryBoundaryLaw(
                plan, Origin, NextOrdinal,
                DecisionTranscript.Select(decision => decision.OutcomeIndex)
                    .ToArray(), ActionId, PlannedCandidateIds);
        }
    }

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, ulong natureSeed) => Begin(origin, null, null,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Native event shuffle"));

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, string? actionId, ulong natureSeed) => Begin(
            origin, null, actionId,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Native event shuffle"));

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin) => Begin(origin, null, null,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Native event shuffle"));

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, string? actionId) => Begin(origin, null, actionId,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Native event shuffle"));

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, string siteId, string actionId, ulong natureSeed) =>
        Begin(origin, siteId, actionId,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Native event shuffle"));

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, string siteId, string actionId) =>
        Begin(origin, siteId, actionId,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Native event shuffle"));

    private static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, string? siteId, string? actionId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        var plan = NativeEventShufflePlanBuilderV111.Build(
            origin, siteId, actionId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Native event shuffle");
        var session = new ReferenceNatureNativeEventShuffleSessionV111(
            CurrentSchemaId, plan.SiteId, plan.ActionId,
            origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), [])
        {
            PlannedCandidateIds =
                (string[])plan.OrderedCandidateIds.Clone()
        };
        session.Validate();
        return session;
    }

    public ReferenceNatureNativeEventShuffleSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            PlannedCandidateIds = (string[])PlannedCandidateIds.Clone(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureNativeEventShuffleSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature native event shuffle session is already complete.");
        var plan = NativeEventShufflePlanBuilderV111.Build(
            Origin, SiteId, ActionId, PlannedCandidateIds);
        if (plan.IsCombatEndIndex)
            return ResolveCombatEndIndexNext();
        var priorIndices = DecisionTranscript.Select(
            decision => decision.OutcomeIndex).ToArray();
        var law = QueryBoundaryLaw(
            plan, Origin, NextOrdinal, priorIndices, ActionId,
            PlannedCandidateIds);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNature = selection.NextState;
        var nextRun = Run;
        if (transcript.Length == plan.TotalChoiceCount)
        {
            var decisions = transcript.Select((decision, ordinal) =>
                new RandomIndexDecisionV111(
                    plan.RequestForChoice(ordinal), decision.OutcomeIndex,
                    "scripted-v111")).ToArray();
            var replay = ReferenceNatureNativeEventIndexReplayV111.Replay(
                Origin, decisions, plan.ReplayAction,
                plan.NativeRngEventId, plan.EventId,
                SelectiveReplaySite(plan), plan.OrderedCandidateIds,
                plan.SiteId ==
                    "relic.war_hammer.elite_upgrade.shuffle_swap_index"
                    ? plan.NativeCursorBefore : null,
                plan.PreserveEventRng,
                plan.ExpectedUnscriptedStreamId,
                plan.ExpectedUnscriptedStreamId is null
                    ? null : plan.NativeCursorBefore,
                plan.ExpectedUnscriptedDrawCount);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                replay, nextNature);
        }

        var next = this with
        {
            Run = nextRun,
            NatureState = nextNature,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature native event shuffle session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        var plan = NativeEventShufflePlanBuilderV111.Build(
            Origin, SiteId, ActionId, PlannedCandidateIds);
        if (plan.IsCombatEndIndex)
        {
            ValidateCombatEndIndex(plan);
            return;
        }
        if (DecisionTranscript.Length > plan.TotalChoiceCount ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
        {
            var law = QueryBoundaryLaw(
                plan, Origin, ordinal, priorIndices.ToArray(), ActionId,
                PlannedCandidateIds);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            DecisionTranscript[ordinal].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[ordinal]))
                throw new InvalidDataException(
                    "ReferenceNature native event shuffle transcript does not replay.");
            expectedNatureState = selection.NextState;
            priorIndices.Add(selection.OutcomeIndex);
        }

        var expectedRun = Origin.Fork();
        if (Complete)
        {
            var decisions = DecisionTranscript.Select((decision, ordinal) =>
                new RandomIndexDecisionV111(
                    plan.RequestForChoice(ordinal), decision.OutcomeIndex,
                    "scripted-v111")).ToArray();
            expectedRun = ReferenceNatureNativeEventIndexReplayV111.Replay(
                Origin, decisions, plan.ReplayAction,
                plan.NativeRngEventId, plan.EventId,
                SelectiveReplaySite(plan), plan.OrderedCandidateIds,
                plan.SiteId ==
                    "relic.war_hammer.elite_upgrade.shuffle_swap_index"
                    ? plan.NativeCursorBefore : null,
                plan.PreserveEventRng,
                plan.ExpectedUnscriptedStreamId,
                plan.ExpectedUnscriptedStreamId is null
                    ? null : plan.NativeCursorBefore,
                plan.ExpectedUnscriptedDrawCount);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNatureState);
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle replay differs from its session envelope.");
    }

    private ReferenceNatureNativeEventShuffleSessionV111
        ResolveCombatEndIndexNext()
    {
        var plan = NativeEventShufflePlanBuilderV111.Build(
            Origin, SiteId, ActionId, PlannedCandidateIds);
        var prefix = BuildCombatEndIndexPrefix(plan);
        if (prefix.Capture.SiteRequests.Length == prefix.Decisions.Length)
            throw new InvalidOperationException(
                "ReferenceNature combat-end index session is already complete.");
        var request = prefix.Capture.SiteRequests[
            prefix.Decisions.Length].Request;
        var law = QueryCombatEndLaw(plan, request);
        var selection = ReferenceNatureV111.SampleValidated(
            law, prefix.NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var decisions = prefix.Decisions.Append(
            new RandomIndexDecisionV111(
                request, selection.OutcomeIndex,
                "scripted-v111")).ToArray();
        var projected = NativeEventShufflePlanBuilderV111
            .CaptureCombatEndIndexRequests(
                Origin, plan, decisions, allowTail: true);
        var nextRun = projected.SiteRequests.Length == decisions.Length
            ? ReferenceNatureRunCursorV111.Commit(
                NativeEventShufflePlanBuilderV111
                    .CaptureCombatEndIndexRequests(
                        Origin, plan, decisions, allowTail: false).Run,
                selection.NextState)
            : Origin.Fork();
        var next = this with
        {
            Run = nextRun,
            NatureState = selection.NextState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    private sealed record CombatEndIndexPrefixV111(
        RandomIndexDecisionV111[] Decisions,
        ReferenceNatureStateV111 NatureState,
        NativeEventShufflePlanBuilderV111.CombatEndIndexCaptureV111 Capture);

    private CombatEndIndexPrefixV111 BuildCombatEndIndexPrefix() =>
        BuildCombatEndIndexPrefix(NativeEventShufflePlanBuilderV111.Build(
            Origin, SiteId, ActionId, PlannedCandidateIds));

    private CombatEndIndexPrefixV111 BuildCombatEndIndexPrefix(
        NativeEventShufflePlanV111 plan)
    {
        if (!plan.IsCombatEndIndex)
            throw new InvalidOperationException(
                "Combat-end prefix requested for another stochastic site.");
        var decisions = new List<RandomIndexDecisionV111>();
        var nature = InitialNatureState.Fork();
        for (var ordinal = 0;
             ordinal < DecisionTranscript.Length; ordinal++)
        {
            var capture = NativeEventShufflePlanBuilderV111
                .CaptureCombatEndIndexRequests(
                    Origin, plan, decisions, allowTail: true);
            if (capture.SiteRequests.Length <= ordinal)
                throw new InvalidDataException(
                    "Combat-end transcript contains more choices than the replay reaches.");
            var request = capture.SiteRequests[ordinal].Request;
            var law = QueryCombatEndLaw(plan, request);
            var selection = ReferenceNatureV111.SampleValidated(law, nature);
            var expected = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            DecisionTranscript[ordinal].Validate();
            if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(
                    DecisionTranscript[ordinal]))
                throw new InvalidDataException(
                    "ReferenceNature combat-end index transcript does not replay.");
            decisions.Add(new RandomIndexDecisionV111(
                request, selection.OutcomeIndex, "scripted-v111"));
            nature = selection.NextState;
        }
        var finalCapture = NativeEventShufflePlanBuilderV111
            .CaptureCombatEndIndexRequests(
                Origin, plan, decisions, allowTail: true);
        return new CombatEndIndexPrefixV111(
            decisions.ToArray(), nature, finalCapture);
    }

    private static ChanceLawV111 QueryCombatEndLaw(
        NativeEventShufflePlanV111 plan, RandomIndexRequestV111 request) =>
        plan.IsFishingRodUpgrade
            ? ChanceLawEngineV111
                .QueryNativePersistentCombatEndUpgrade(request)
            : plan.IsImprovementUpgrade
                ? ChanceLawEngineV111.QueryImprovementCombatEndUpgrade(
                    plan.ActionId, request)
                : ChanceLawEngineV111.QueryPaelsToothCombatEndReturn(
                    plan.ActionId, request);

    private void ValidateCombatEndIndex(NativeEventShufflePlanV111 plan)
    {
        var prefix = BuildCombatEndIndexPrefix(plan);
        var complete = prefix.Capture.SiteRequests.Length ==
            prefix.Decisions.Length;
        if (prefix.Capture.SiteRequests.Length < prefix.Decisions.Length ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, complete))
            throw new InvalidDataException(
                "ReferenceNature combat-end index cursor differs from its session envelope.");
        var expectedRun = Origin.Fork();
        if (complete)
        {
            var strictReplay =
                NativeEventShufflePlanBuilderV111
                    .CaptureCombatEndIndexRequests(
                        Origin, plan, prefix.Decisions, allowTail: false);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                strictReplay.Run, prefix.NatureState);
        }
        if (CanonicalJson.Sha256(prefix.NatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature combat-end index replay differs from its session envelope.");
    }

    private static ChanceLawV111 QueryBoundaryLaw(
        NativeEventShufflePlanV111 plan, RunState origin, int ordinal,
        int[] priorIndices, string actionId, string[] plannedCandidateIds) =>
        ordinal < plan.SwapCount
            ? ChanceLawEngineV111.QueryNativeEventShuffle(
                origin, plan.SiteId, ordinal, priorIndices, actionId,
                plannedCandidateIds)
            : ChanceLawEngineV111.QueryNativeRepyPotionOffer(
                origin, ordinal - plan.SwapCount, priorIndices, actionId,
                plannedCandidateIds);

    private static string? SelectiveReplaySite(
        NativeEventShufflePlanV111 plan) =>
        plan.SiteId is "relic.war_hammer.elite_upgrade.shuffle_swap_index" or
            PrototypeNativeGloryEvents
            .BattlewornUpgradeShuffleSiteId or
            PrototypeNativeFakeMerchant.RelicOfferShuffleSiteId or
            PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId
            ? plan.SiteId : null;

    public static string Save(
        ReferenceNatureNativeEventShuffleSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new
            ReferenceNatureNativeEventShuffleSessionSnapshotV111(
                CurrentSchemaId, session.SiteId, session.ActionId,
                RunSnapshot.Save(session.Origin),
                RunSnapshot.Save(session.Run), session.InitialNatureState,
                session.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureNativeEventShuffleSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNativeEventShuffleSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature native event shuffle snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature native event shuffle snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        // The original v1 envelope covered only Doors Light and did not save
        // the site/action fields; keep those snapshots restorable.
        var siteId = snapshot.SiteId ??
            PrototypeNativeUnderdocksDoors.ShuffleSiteId;
        var actionId = snapshot.ActionId ?? "light";
        if (siteId == PrototypeNativeHiveEvents
                .WarHistorianRepyRelicShuffleSiteId &&
            snapshot.DecisionTranscript.Length ==
                NativeEventShufflePlanBuilderV111.Build(
                    origin, siteId, actionId).SwapCount &&
            savedRun.Phase == RunPhase.Reward &&
            savedRun.World?.Event is
                {
                    EventId: PrototypeNativeHiveEvents.WarHistorianRepyId,
                    RepyChestPotionIds.Length: 2,
                    RepyChestRelicIds.Length: 2,
                    RepyChestGroupsResolved: 1
                })
        {
            // Earlier v1 Repy sessions committed after the relic swaps and
            // left potion offers Native. Rewind their derived run so the new
            // session can sample both potion indices from the saved prefix.
            savedRun = origin.Fork();
        }
        var replay = Begin(origin, siteId, actionId,
            snapshot.InitialNatureState.Fork());
        for (var index = 0;
             index < snapshot.DecisionTranscript.Length; index++)
            replay = replay.ResolveNext();
        replay.Validate();
        if (CanonicalJson.Sha256(replay.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !replay.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle snapshot does not replay from its origin.");
        return replay;
    }
}

public static partial class ChanceLawEngineV111
{
    public const string NativeEventShuffleConditioningPolicyId =
        "native-event-fisher-yates-prefix-v1";
    public const string NativeRepyChestOfferConditioningPolicyId =
        "native-event-repy-chest-offer-prefix-v1";
    public const string NativeEndlessConveyorDishConditioningPolicyId =
        "native-event-weighted-dish-ticket-v1";
    public const string NativeEndlessConveyorUpgradeConditioningPolicyId =
        "native-event-endless-conveyor-card-upgrade-v1";
    public const string NativeEndlessConveyorColorlessConditioningPolicyId =
        "native-event-endless-conveyor-colorless-card-v1";

    public static string[] NativeEventShuffleSessionSiteIds =>
    [
        "relic.persistent_combat_end.random_upgrade.index_draw",
        "combat.glory_tinker.improvement_card.index_draw",
        "relic.paels_tooth.card_return.index_draw",
        "relic.war_hammer.elite_upgrade.shuffle_swap_index",
        PrototypeNativeUnderdocksDoors.ShuffleSiteId,
        PrototypeNativeGloryEvents.HungryForMushroomsShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId,
        PrototypeNativeGloryEvents.BattlewornUpgradeShuffleSiteId,
        PrototypeNativeFakeMerchant.RelicOfferShuffleSiteId,
        PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId,
        "event.war_historian.repy.potion_offer.index_draw"
    ];

    public static ChanceLawV111 QueryNativeEventShuffle(
        RunState origin, string siteId, int ordinal,
        int[]? priorIndices = null, string? actionId = null,
        string[]? plannedCandidateIds = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = NativeEventShufflePlanBuilderV111.Build(
            origin, siteId, actionId, plannedCandidateIds);
        priorIndices ??= [];
        if (plan.IsCombatEndIndex)
            return QueryCombatEndIndex(
                origin, plan, ordinal, priorIndices);
        if (ordinal < 0 || ordinal >= plan.SwapCount ||
            priorIndices.Length != ordinal)
            throw new InvalidOperationException(
                "Native event shuffle law requires the complete preceding swap-index prefix.");
        for (var index = 0; index < priorIndices.Length; index++)
            if (priorIndices[index] < 0 ||
                priorIndices[index] >= plan.CandidateCount - index)
                throw new InvalidOperationException(
                    "Native event shuffle prefix contains an unsupported bounded index.");

        var request = plan.Request(ordinal);
        var weights = NativeNextIntGridWeights(request.BoundedCount!.Value);
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            plan.ConditioningFingerprint,
            priorIndices,
            request
        });
        var outcomes = Enumerable.Range(0, request.BoundedCount.Value)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                weights[index]))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.fisher_yates_shuffle_swap",
            conditioningFingerprint
        });
        return Create(eventId, request.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            NativeEventShuffleConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryCombatEndIndex(
        RunState origin, NativeEventShufflePlanV111 plan, int ordinal,
        int[] priorIndices)
    {
        if (ordinal < 0 || priorIndices.Length != ordinal)
            throw new InvalidOperationException(
                "Combat-end index law requires the complete preceding index prefix.");
        var decisions = new List<RandomIndexDecisionV111>();
        for (var index = 0; index < priorIndices.Length; index++)
        {
            var capture = NativeEventShufflePlanBuilderV111
                .CaptureCombatEndIndexRequests(
                    origin, plan, decisions, allowTail: true);
            if (capture.SiteRequests.Length <= index)
                throw new InvalidOperationException(
                    "Combat-end index prefix exceeds the requests reached by the victory action.");
            var request = capture.SiteRequests[index].Request;
            if (priorIndices[index] < 0 ||
                priorIndices[index] >= request.SupportCount)
                throw new InvalidOperationException(
                    "Combat-end index prefix contains an unsupported index.");
            decisions.Add(new RandomIndexDecisionV111(
                request, priorIndices[index], "scripted-v111"));
        }
        var finalCapture = NativeEventShufflePlanBuilderV111
            .CaptureCombatEndIndexRequests(
                origin, plan, decisions, allowTail: true);
        if (finalCapture.SiteRequests.Length <= ordinal)
            throw new InvalidOperationException(
                "Combat-end index law ordinal exceeds the requests reached by the victory action.");
        var selectedRequest = finalCapture.SiteRequests[ordinal].Request;
        return plan.IsFishingRodUpgrade
            ? ChanceLawEngineV111
                .QueryNativePersistentCombatEndUpgrade(selectedRequest)
            : plan.IsImprovementUpgrade
                ? ChanceLawEngineV111.QueryImprovementCombatEndUpgrade(
                    plan.ActionId, selectedRequest)
                : ChanceLawEngineV111.QueryPaelsToothCombatEndReturn(
                    plan.ActionId, selectedRequest);
    }

    public static ChanceLawV111 QueryNativeRepyPotionOffer(
        RunState origin, int offerOrdinal, int[]? priorIndices = null,
        string? actionId = null, string[]? plannedCandidateIds = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        priorIndices ??= [];
        var plan = NativeEventShufflePlanBuilderV111.Build(
            origin,
            PrototypeNativeHiveEvents.WarHistorianRepyRelicShuffleSiteId,
            actionId ?? "unlock_chest", plannedCandidateIds);
        var potionPool = PrototypeContent.PotionPool;
        if (!plan.IsRepyChestOffer || offerOrdinal is < 0 or >= 2 ||
            priorIndices.Length != plan.SwapCount + offerOrdinal ||
            potionPool.Length == 0)
            throw new InvalidOperationException(
                "Repy potion laws require the complete relic-shuffle and prior-potion prefix.");
        for (var index = 0; index < plan.SwapCount; index++)
            if (priorIndices[index] < 0 ||
                priorIndices[index] >= plan.CandidateCount - index)
                throw new InvalidOperationException(
                    "Repy potion law prefix contains an unsupported relic swap index.");
        for (var index = plan.SwapCount; index < priorIndices.Length; index++)
            if (priorIndices[index] < 0 ||
                priorIndices[index] >= potionPool.Length)
                throw new InvalidOperationException(
                    "Repy potion law prefix contains an unsupported potion index.");

        var request = plan.RequestForChoice(
            plan.SwapCount + offerOrdinal);
        var conditioning = CanonicalJson.Sha256(new
        {
            plan.ConditioningFingerprint,
            priorIndices,
            potionPool,
            request
        });
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.repy_potion_offer_index",
            conditioning
        });
        var weights = NativeNextIntGridWeights(potionPool.Length);
        var outcomes = potionPool.Select((potionId, index) =>
            new ChanceLawOutcomeV111(
                $"potion:{potionId}:index:{index}", weights[index])).ToArray();
        return Create(
            eventId, request.SiteId, request.OperationKind,
            request.OwnerId, request.Ordinal,
            NativeRepyChestOfferConditioningPolicyId, conditioning,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    public static ChanceLawV111 QueryNativeEndlessConveyorDishEntry(
        RunState origin, string actionId, int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (origin.Phase == RunPhase.Event &&
            origin.World?.Event is { EventId:
                PrototypeNativeEndlessConveyor.EventId,
                NativeDishCount: 1 } &&
            new PrototypeGameEngine().GetLegalActions(origin)
                .SingleOrDefault(candidate =>
                    PrototypeAiEnvironment.StableActionId(candidate) == actionId)
                is { Kind: "event_choice" } observeAction &&
            observeAction.ReadPayload<EventChoicePayload>().ChoiceId == "observe")
            return QueryNativeEndlessConveyorUpgrade(
                origin, actionId, ordinal);

        if (origin.Phase == RunPhase.Event &&
            origin.World?.Event?.EventId ==
                PrototypeNativeEndlessConveyor.EventId)
            return QueryNativeEndlessConveyorDishContinuation(
                origin, actionId, ordinal);

        if (ordinal is not (null or 0))
            throw new InvalidOperationException(
                "Endless Conveyor entry supports only dish ordinal zero.");
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world)
            throw new InvalidOperationException(
                "Endless Conveyor dish law requires a configured map-choice Event-node entry at ordinal 0.");
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId);
        var selectedAction = action ?? throw new InvalidOperationException(
            "Endless Conveyor dish law requires a current legal Event-node action.");
        var nodeId = selectedAction.Kind == "choose_map_node"
            ? selectedAction.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Endless Conveyor dish law requires a current legal Event-node action.");

        IReadOnlyList<NativeEventUnitFloatCaptureV111> captures;
        string? selectedEventId;
        using (var probe = NativeEventRandomProbeV111.Begin())
        using (RandomBoundaryJournalV111.Begin())
        using (NativeEngineEventJournalV111.Begin())
        {
            var projected = engine.Step(origin.Fork(), selectedAction).State;
            selectedEventId = projected.World?.Event?.EventId;
            captures = probe.UnitFloatCaptures;
        }
        if (selectedEventId != PrototypeNativeEndlessConveyor.EventId)
            throw new InvalidOperationException(
                "The ordinary event-selection provider did not enter Endless Conveyor for this Event node.");
        var matching = captures.Where(capture =>
            capture.Request.SiteId == PrototypeNativeEndlessConveyor
                .DishWeightedTicketSiteId).ToArray();
        if (matching.Length != 1)
            throw new InvalidOperationException(
                "Endless Conveyor entry must perform exactly one weighted dish ticket.");

        var capture = matching[0];
        var preDraw = capture.PreDrawState;
        var evt = preDraw.World?.Event;
        if (preDraw.Phase != RunPhase.Event ||
            evt?.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.NativeDishCount != 0 || evt.NativeDishId is not null ||
            evt.NativeLastDishId is not null ||
            evt.NativeEventPotionSlot is not null)
            throw new InvalidOperationException(
                "Endless Conveyor dish law was not captured at its initial pre-draw boundary.");

        var expectedStream = NativeRngV111Contexts.ForEvent(
            preDraw.RunSeed, PrototypeNativeEndlessConveyor.NativeRngEventId);
        if (evt.NativeEventRng is not { } stream ||
            NativeRngV111.ToSnapshot(stream) !=
                NativeRngV111.ToSnapshot(expectedStream) ||
            capture.Request.StreamId != expectedStream.StreamId ||
            capture.Request.Ordinal != 0 ||
            ordinal is { } requestedOrdinal &&
                capture.Request.Ordinal != requestedOrdinal)
            throw new InvalidOperationException(
                "Endless Conveyor dish law requires the untouched event-entry stream.");

        var candidates = PrototypeNativeEndlessConveyor
            .GetWeightedDishCandidates(preDraw, evt);
        if (candidates.Length == 0 || candidates.Any(candidate =>
                candidate.Weight <= 0))
            throw new InvalidOperationException(
                "Endless Conveyor has no positive weighted dish support.");
        var ownerId = "dish:1:pool:" + CanonicalJson.Sha256(candidates);
        if (capture.Request.OwnerId != ownerId)
            throw new InvalidDataException(
                "Endless Conveyor dish request owner differs from its weighted support.");

        var gridWeights = ChanceLawEngineV111
            .NativeNextFloatWeightedTicketGridWeights(candidates
                .Select(candidate => candidate.Weight).ToArray());
        var request = capture.Request;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            actionId,
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            dishCount = evt.NativeDishCount,
            lastDishId = evt.NativeLastDishId,
            hp = preDraw.Player.Hp,
            maxHp = preDraw.Player.MaxHp,
            emptyPotionSlots = preDraw.Player.PotionSlots
                .Select((potion, slot) => (potion, slot))
                .Where(entry => entry.potion is null)
                .Select(entry => entry.slot).ToArray(),
            candidates,
            gridWeights,
            request = new
            {
                request.SiteId, request.StreamId, request.OwnerId,
                request.Ordinal, request.SourceVersion
            },
            policy = NativeEndlessConveyorDishConditioningPolicyId
        });
        var outcomes = candidates.Select((candidate, index) =>
                new ChanceLawOutcomeV111(
                    "dish:" + candidate.Id, gridWeights[index]))
            .Where(outcome => outcome.Weight > 0).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.endless_conveyor.dish_weighted_ticket",
            actionId,
            conditioningFingerprint
        });
        return Create(eventId, request.SiteId,
            "unit_float_weighted_ticket", request.OwnerId, request.Ordinal,
            NativeEndlessConveyorDishConditioningPolicyId,
            conditioningFingerprint,
            ChanceLawEngineV111
                .NativeUnitFloatWeightedTicketSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    public static ChanceLawV111 QueryNativeEndlessConveyorUpgrade(
        RunState origin, string actionId, int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Event: { } evt, Combat: null } ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.NativeDishCount < 1 || evt.NativeDishId is null ||
            evt.NativeDishId != evt.NativeLastDishId ||
            evt.NativeDishRollPending || evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not (null or "scripted-v111") ||
            evt.ControlledChoice is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade law requires a configured page-zero action boundary.");
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Endless Conveyor upgrade law requires a current legal action.");
        var choice = action.Kind == "event_choice"
            ? action.ReadPayload<EventChoicePayload>().ChoiceId : null;
        var observeBoundary = choice == "observe" && evt.NativeDishCount == 1;
        var spicyGrabBoundary = choice == "grab" &&
            evt.NativeDishId == "SPICY_SNAPPY" && origin.Player.Gold >= 40;
        if (!observeBoundary && !spicyGrabBoundary)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade law requires first-dish Observe or a Spicy Snappy Grab.");

        var candidates = PrototypeNativeEndlessConveyor
            .GetUpgradeCardCandidates(origin);
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade has no eligible card and performs no index draw.");
        var stream = evt.NativeEventRng
            ?? throw new InvalidOperationException(
                "Endless Conveyor upgrade requires its persisted Native event cursor.");
        var expectedStreamId = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeEndlessConveyor.NativeRngEventId)
            .StreamId;
        if (stream.StreamId != expectedStreamId ||
            stream.CallCount is null or > int.MaxValue)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade requires a valid Native event cursor ordinal.");
        var requestOrdinal = (int)stream.CallCount.Value;
        if (ordinal is { } requestedOrdinal &&
            requestOrdinal != requestedOrdinal)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade ordinal differs from its Native event cursor.");

        var ownerId = "endless_conveyor:deck:" +
            CanonicalJson.Sha256(candidates);
        var request = new RandomIndexRequestV111(
            PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
            stream.StreamId,
            ownerId,
            requestOrdinal,
            Enumerable.Range(0, candidates.Length).ToArray());
        request.Validate();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            actionId,
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            dishCount = evt.NativeDishCount,
            dishId = evt.NativeDishId,
            nativeEventCursor = NativeRngV111.ToSnapshot(stream),
            providerMode = evt.NativeEventRngProviderMode,
            candidates,
            request,
            policy = NativeEndlessConveyorUpgradeConditioningPolicyId
        });
        var weights = NativeNextIntGridWeights(request.SupportCount);
        var outcomes = candidates.Select((candidate, index) =>
            new ChanceLawOutcomeV111(
                $"card-instance:{candidate}:index:{index}",
                weights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.endless_conveyor.random_upgrade",
            actionId,
            conditioningFingerprint
        });
        return Create(eventId, request.SiteId, request.OperationKind,
            request.OwnerId, request.Ordinal,
            NativeEndlessConveyorUpgradeConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    public static ChanceLawV111 QueryNativeEndlessConveyorColorlessCard(
        RunState origin, string actionId, int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Event: { } evt, Combat: null } ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.NativeDishCount < 1 || evt.NativeDishId != "FRIED_EEL" ||
            evt.NativeDishId != evt.NativeLastDishId ||
            evt.NativeDishRollPending || evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not (null or "scripted-v111") ||
            evt.ControlledChoice is not null ||
            origin.ControlledEventRewardChoice is not null ||
            origin.Player.Gold < 40)
            throw new InvalidOperationException(
                "Endless Conveyor Colorless law requires the configured Fried Eel Grab boundary.");
        PrototypeStateInvariants.Validate(origin);

        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Endless Conveyor Colorless law requires a current legal Grab action.");
        if (action.Kind != "event_choice" ||
            action.ReadPayload<EventChoicePayload>().ChoiceId != "grab")
            throw new InvalidOperationException(
                "Endless Conveyor Colorless law requires the Grab action.");

        var candidates = PrototypeNativeEndlessConveyor
            .GetColorlessCardCandidates();
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Fried Eel has no supported Colorless card and performs no index draw.");
        var stream = evt.NativeEventRng
            ?? throw new InvalidOperationException(
                "Endless Conveyor Colorless draw requires its persisted Native event cursor.");
        var expectedStreamId = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeEndlessConveyor.NativeRngEventId)
            .StreamId;
        if (stream.StreamId != expectedStreamId ||
            stream.CallCount is null or > int.MaxValue)
            throw new InvalidOperationException(
                "Endless Conveyor Colorless draw requires a valid Native event cursor ordinal.");
        var requestOrdinal = (int)stream.CallCount.Value;
        if (ordinal is { } requestedOrdinal &&
            requestOrdinal != requestedOrdinal)
            throw new InvalidOperationException(
                "Endless Conveyor Colorless ordinal differs from its Native event cursor.");

        var ownerId = $"dish:{evt.NativeDishCount}:pool:" +
            CanonicalJson.Sha256(candidates);
        var request = new RandomIndexRequestV111(
            PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
            stream.StreamId,
            ownerId,
            requestOrdinal,
            Enumerable.Range(0, candidates.Length).ToArray());
        request.Validate();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            actionId,
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            dishCount = evt.NativeDishCount,
            dishId = evt.NativeDishId,
            nativeEventCursor = NativeRngV111.ToSnapshot(stream),
            providerMode = evt.NativeEventRngProviderMode,
            candidates,
            request,
            policy = NativeEndlessConveyorColorlessConditioningPolicyId
        });
        var weights = NativeNextIntGridWeights(request.SupportCount);
        var outcomes = candidates.Select((candidate, index) =>
            new ChanceLawOutcomeV111(
                $"card-model:{candidate}:index:{index}",
                weights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.endless_conveyor.colorless_card",
            actionId,
            conditioningFingerprint
        });
        return Create(eventId, request.SiteId, request.OperationKind,
            request.OwnerId, request.Ordinal,
            NativeEndlessConveyorColorlessConditioningPolicyId,
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }

    private static ChanceLawV111 QueryNativeEndlessConveyorDishContinuation(
        RunState origin, string actionId, int? ordinal)
    {
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Event: { } eventState, Combat: null } world ||
            eventState.EventId != PrototypeNativeEndlessConveyor.EventId ||
            eventState.NativeEventRngProviderMode is not (null or "scripted-v111") ||
            eventState.NativeEventPotionSlot is not null ||
            eventState.ControlledChoice is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Endless Conveyor continuation law requires a pending configured event action.");
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Endless Conveyor continuation law requires a current legal action.");
        IReadOnlyList<NativeEventUnitFloatCaptureV111> captures;
        using (var probe = NativeEventRandomProbeV111.Begin())
        using (RandomBoundaryJournalV111.Begin())
        using (NativeEngineEventJournalV111.Begin())
        {
            _ = engine.Step(origin.Fork(), action).State;
            captures = probe.UnitFloatCaptures;
        }
        var matching = captures.Where(capture =>
            capture.Request.SiteId == PrototypeNativeEndlessConveyor
                .DishWeightedTicketSiteId).ToArray();
        if (matching.Length != 1)
            throw new InvalidOperationException(
                "Endless Conveyor continuation action must perform exactly one weighted dish ticket.");

        var capture = matching[0];
        var preDraw = capture.PreDrawState;
        var evt = preDraw.World?.Event;
        if (preDraw.Phase != RunPhase.Event ||
            preDraw.World?.Combat is not null ||
            evt?.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativePageIndex != 1 || evt.ChosenChoiceId != "grab" ||
            evt.NativeDishCount < 1 || evt.NativeDishId is null ||
            evt.NativeDishId != evt.NativeLastDishId ||
            evt.NativeDishRollPending || evt.NativeEventPotionSlot is not null ||
            (evt.NativeDishCount + 1) % 5 == 0)
            throw new InvalidOperationException(
                "Endless Conveyor continuation law was not captured at a weighted post-grab dish boundary.");

        var stream = evt.NativeEventRng
            ?? throw new InvalidOperationException(
                "Endless Conveyor continuation requires its Native event stream.");
        if (stream.StreamId != capture.Request.StreamId ||
            ordinal is { } requestedOrdinal &&
                capture.Request.Ordinal != requestedOrdinal)
            throw new InvalidOperationException(
                "Endless Conveyor continuation ordinal differs from the requested event cursor.");

        var candidates = PrototypeNativeEndlessConveyor
            .GetWeightedDishCandidates(preDraw, evt);
        if (candidates.Length == 0 || candidates.Any(candidate =>
                candidate.Weight <= 0))
            throw new InvalidOperationException(
                "Endless Conveyor has no positive weighted dish support.");
        var nextRoll = checked(evt.NativeDishCount + 1);
        var ownerId = $"dish:{nextRoll}:pool:" +
            CanonicalJson.Sha256(candidates);
        if (capture.Request.OwnerId != ownerId)
            throw new InvalidDataException(
                "Endless Conveyor dish request owner differs from its weighted support.");

        var gridWeights = ChanceLawEngineV111
            .NativeNextFloatWeightedTicketGridWeights(candidates
                .Select(candidate => candidate.Weight).ToArray());
        var request = capture.Request;
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            actionId,
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            dishCount = evt.NativeDishCount,
            lastDishId = evt.NativeLastDishId,
            nativeEventCursor = NativeRngV111.ToSnapshot(stream),
            hp = preDraw.Player.Hp,
            maxHp = preDraw.Player.MaxHp,
            emptyPotionSlots = preDraw.Player.PotionSlots
                .Select((potion, slot) => (potion, slot))
                .Where(entry => entry.potion is null)
                .Select(entry => entry.slot).ToArray(),
            candidates,
            gridWeights,
            request = new
            {
                request.SiteId, request.StreamId, request.OwnerId,
                request.Ordinal, request.SourceVersion
            },
            policy = NativeEndlessConveyorDishConditioningPolicyId
        });
        var outcomes = candidates.Select((candidate, index) =>
                new ChanceLawOutcomeV111(
                    "dish:" + candidate.Id, gridWeights[index]))
            .Where(outcome => outcome.Weight > 0).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.endless_conveyor.dish_weighted_ticket",
            actionId,
            conditioningFingerprint
        });
        return Create(eventId, request.SiteId,
            "unit_float_weighted_ticket", request.OwnerId, request.Ordinal,
            NativeEndlessConveyorDishConditioningPolicyId,
            conditioningFingerprint,
            ChanceLawEngineV111
                .NativeUnitFloatWeightedTicketSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }
}
