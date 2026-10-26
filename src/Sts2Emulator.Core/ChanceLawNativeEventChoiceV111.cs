using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string NativeEventCardPickConditioningPolicyId =
        "native-event-card-pick-prefix-v1";
    public const string NativeEventGoldIndexSiteId =
        StochasticSiteRegistryV111.NativeEventGoldIndexSiteId;
    public const string NativeEventGoldUnitFloatSiteId =
        StochasticSiteRegistryV111.NativeEventGoldUnitFloatSiteId;
    public const string NativeStoneOfAllTimePotionSlotSiteId =
        StochasticSiteRegistryV111.NativeStoneOfAllTimePotionSlotSiteId;
    public const string NativeFutureOfPotionsCardTypeSiteId =
        StochasticSiteRegistryV111.NativeFutureOfPotionsCardTypeSiteId;
    public const string NativeStoneOfAllTimePushTicketSiteId =
        StochasticSiteRegistryV111.NativeStoneOfAllTimePushTicketSiteId;
    public const string NativeStoneOfAllTimeLiftTicketSiteId =
        StochasticSiteRegistryV111.NativeStoneOfAllTimeLiftTicketSiteId;

    private const string NativeStoneOfAllTimeNoEffectProvenance =
        "v0.111.0 Stone of All Time consumes the bounded ticket after the " +
        "push or lift effect and discards the selected index; every source " +
        "index therefore has the same no_effect mechanic. Native event " +
        "cursor advancement is outside this Chance Law.";

    private const string LegacyStoneOfAllTimeNoEffectProvenance =
        "v0.111.0 Stone of All Time consumes the bounded ticket after the " +
        "push or lift effect and discards the selected index; every source " +
        "index therefore has the same no_effect mechanic. Legacy event " +
        "stream advancement is outside this Chance Law.";

    private const string NativeStoneOfAllTimeNoEffectPolicyId =
        "native-stone-of-all-time-ticket-no-effect-v1";

    private const string LegacyStoneOfAllTimeNoEffectPolicyId =
        "legacy-stone-of-all-time-ticket-no-effect-v1";

    private const string LegacyStoneOfAllTimePotionSlotProvenance =
        "ReferenceNature models the retained legacy Stone of All Time " +
        "potion-slot draw as equal weight over the ordered occupied slots; " +
        "legacy seed frequencies and exact provider parity remain unverified.";

    public static string[] NativeEventGoldQuerySiteIds =>
        StochasticSiteRegistryV111.NativeEventGoldSiteIds;

    public static string[] NativeEventEntryIndexQuerySiteIds =>
        StochasticSiteRegistryV111.NativeEventEntryIndexSiteIds;

    public static string[] NativeEventNoEffectIndexQuerySiteIds =>
        StochasticSiteRegistryV111.NativeEventNoEffectIndexSiteIds;

    public static string[] NativeEventCardPickQuerySiteIds =>
        StochasticSiteRegistryV111.NativeEventCardPickSiteIds;

    /// <summary>
    /// Query Stone of All Time's event-entry potion-slot draw, conditional on
    /// an explicit legal Event-node action and the declared event branch.
    /// </summary>
    public static ChanceLawV111 QueryNativeStoneOfAllTimePotionSlot(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId)
        => QueryStoneOfAllTimePotionSlot(
            origin, siteId, ordinal, actionId, eventId);

    public static ChanceLawV111 QueryStoneOfAllTimePotionSlot(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (siteId is not (NativeStoneOfAllTimePotionSlotSiteId))
            throw new NotSupportedException(
                $"Stone of All Time potion-slot law does not support site '{siteId}'.");
        if (ordinal < 0 ||
            eventId != PrototypeNativeStoneOfAllTime.EventId ||
            origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world)
            throw new InvalidOperationException(
                "Stone of All Time potion-slot laws require a pre-entry Event-node boundary and explicit Stone branch.");

        var nativeRoute = origin.Stochastic.NativeRunRng is not null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world);
        var legacyRoute = origin.Stochastic.NativeRunRng is null &&
            !PrototypeGameEngine.UsesNativeRegionalSystems(world);
        var nativeRegionalLegacyRoute =
            origin.Stochastic.NativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world);
        if (!nativeRoute && !legacyRoute && !nativeRegionalLegacyRoute)
            throw new InvalidOperationException(
                "Stone of All Time potion-slot law requires a supported Native-regional or legacy event RNG route.");
        var streamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId)
            : "event";
        if (nativeRoute && ordinal != 0)
            throw new InvalidOperationException(
                "Native Stone of All Time potion-slot law requires event-stream ordinal 0.");
        if (legacyRoute)
        {
            if (origin.Configuration?.UsesExperimentalSilentThreeActRoute
                    != true ||
                !ExperimentalSilentThreeActRouteV111.ContainsEvent(
                    eventId, world.Act))
                throw new InvalidOperationException(
                    "Legacy Stone of All Time branch is outside the supported later-act event route.");
            var legacyEvent = PrototypeContent.Event(eventId);
            if (world.Act < legacyEvent.MinAct ||
                world.Act > legacyEvent.MaxAct ||
                legacyEvent.OncePerRun && world.EventIds.Contains(
                    eventId, StringComparer.Ordinal) ||
                !PrototypeNativeHiveEvents.IsEligible(
                    legacyEvent, origin.Player))
                throw new InvalidOperationException(
                    "Legacy Stone of All Time event branch is unavailable in this run state.");
        }
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Stone of All Time potion-slot law requires a current legal Event-node action.");
        var nodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Stone of All Time potion-slot law requires a current legal Event-node action.");

        ValidateNativeEventBranch(origin, world, eventId);
        var eventDefinition = PrototypeContent.Event(eventId);
        if (!PrototypeNativeHiveEvents.IsEligible(
                eventDefinition, origin.Player))
            throw new InvalidOperationException(
                "Stone of All Time is ineligible because the player has no potion.");
        var slots = PrototypeNativeStoneOfAllTime.OccupiedPotionSlots(
            origin.Player);
        if (slots.Length == 0)
            throw new InvalidOperationException(
                "Stone of All Time has no occupied potion slot support.");

        var ownerId = "event:stone_of_all_time:potion_slots:" +
            CanonicalJson.Sha256(slots);
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, slots.Length).ToArray());
        request.Validate();
        var weights = nativeRoute
            ? NativeNextIntGridWeights(slots.Length)
            : Enumerable.Repeat(1L, slots.Length).ToArray();
        var outcomes = slots.Select((slot, index) =>
            new ChanceLawOutcomeV111(
                $"potion_slot:{slot}:index:{index}", weights[index]))
            .ToArray();
        var conditioningFingerprint = nativeRoute
            ? CanonicalJson.Sha256(new
            {
                boundary = "native_event.stone_of_all_time.entry_potion_slot",
                actionId,
                eventId,
                nodeId,
                orderedOccupiedPotionSlots = slots,
                request
            })
            : CanonicalJson.Sha256(new
            {
                boundary = "reference_nature.stone_of_all_time.entry_potion_slot",
                actionId,
                eventId,
                nodeId,
                providerRoute = nativeRegionalLegacyRoute
                    ? "native_regional_legacy" : "legacy",
                orderedOccupiedPotionSlots = slots,
                request
            });
        return CreateNativeBoundaryLaw(
            siteId, request.OperationKind, ownerId, ordinal,
            nativeRoute
                ? "native-stone-of-all-time-potion-slot-index-v1"
                : "legacy-stone-of-all-time-potion-slot-index-v1",
            conditioningFingerprint,
            nativeRoute
                ? NativeBoundedIndexSourceVerifiedProvenance
                : LegacyStoneOfAllTimePotionSlotProvenance,
            outcomes,
            nativeRoute
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    /// <summary>
    /// Query one Future of Potions card-type index draw before Event entry.
    /// The event branch is explicit, and the ordinal follows occupied potion
    /// slots in source order without consulting a Native selection result.
    /// </summary>
    public static ChanceLawV111 QueryNativeFutureOfPotionsCardType(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId) => QueryFutureOfPotionsCardType(
            origin, siteId, ordinal, actionId, eventId, null);

    public static ChanceLawV111
        QueryLegacyNativeRegionalFutureOfPotionsCardType(
            RunState origin, RandomIndexRequestV111 request,
            string actionId, string eventId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.SiteId != NativeFutureOfPotionsCardTypeSiteId ||
            request.StreamId != "event" ||
            request.OperationKind != "choose_index")
            throw new InvalidOperationException(
                "Legacy Future of Potions laws require a saved event-stream index request.");
        var potionSlotPrefix = "potion_slot:";
        if (!request.OwnerId.StartsWith(
                potionSlotPrefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Legacy Future of Potions owner does not identify its potion slot.");
        var potionSlotText = request.OwnerId[potionSlotPrefix.Length..]
            .Split(':', 2)[0];
        if (!int.TryParse(potionSlotText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var potionSlot))
            throw new InvalidDataException(
                "Legacy Future of Potions owner has an invalid potion slot.");
        if (origin.World is null)
            throw new InvalidOperationException(
                "Legacy Future of Potions law requires its source run world.");
        var occupiedPotionSlots = origin.Player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion is not null)
            .ToArray();
        var logicalOrdinal = Array.FindIndex(
            occupiedPotionSlots, item => item.Slot == potionSlot);
        if (logicalOrdinal < 0)
            throw new InvalidDataException(
                "Legacy Future of Potions request names an unowned potion slot.");
        return QueryFutureOfPotionsCardType(
            origin, request.SiteId, logicalOrdinal, actionId, eventId,
            request);
    }

    private static ChanceLawV111 QueryFutureOfPotionsCardType(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId, RandomIndexRequestV111? savedLegacyRequest)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (siteId is not (NativeFutureOfPotionsCardTypeSiteId))
            throw new NotSupportedException(
                $"Future of Potions card-type law does not support site '{siteId}'.");
        if (ordinal < 0 ||
            eventId != PrototypeNativeFutureOfPotions.EventId ||
            origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Future of Potions card-type laws require a pre-entry Native-regional Event-node boundary and explicit event branch.");
        var nativeRoute = origin.Stochastic.NativeRunRng is not null;
        var legacyRoute = savedLegacyRequest is not null &&
            origin.Stochastic.NativeRunRng is null;
        if (!nativeRoute && !legacyRoute)
            throw new InvalidOperationException(
                "Future of Potions card-type laws require a Native or captured Native-regional legacy event stream.");
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Future of Potions card-type law requires a current legal Event-node action.");
        var nodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Future of Potions card-type law requires a current legal Event-node action.");

        ValidateNativeEventBranch(origin, world, eventId);
        if (!PrototypeNativeFutureOfPotions.IsEligible(origin.Player))
            throw new InvalidOperationException(
                "Future of Potions is ineligible for this potion inventory.");
        var potionEntries = origin.Player.PotionSlots
            .Select((potion, slot) => (Potion: potion, Slot: slot))
            .Where(item => item.Potion is not null)
            .Select(item => (PotionId: item.Potion!.PotionId, item.Slot))
            .ToArray();
        if (ordinal >= potionEntries.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        var entry = potionEntries[ordinal];
        if (!PrototypeNativeFutureOfPotions.TryGetCardRarity(
                entry.PotionId, out var rarity))
            throw new InvalidOperationException(
                "Future of Potions source support contains an unsupported potion.");
        var cardTypes = PrototypeNativeFutureOfPotions
            .CardTypesForRarity(rarity);
        var ownerId = $"potion_slot:{entry.Slot}:potion:{entry.PotionId}:pool:" +
            CanonicalJson.Sha256(cardTypes);
        var request = new RandomIndexRequestV111(
            siteId,
            nativeRoute
                ? NativeRngV111Contexts.EventStreamId(
                    PrototypeNativeFutureOfPotions.NativeRngEventId)
                : "event",
            ownerId, nativeRoute ? ordinal : savedLegacyRequest!.Ordinal,
            Enumerable.Range(0, cardTypes.Length).ToArray());
        request.Validate();
        if (savedLegacyRequest is not null &&
            (savedLegacyRequest.SiteId != request.SiteId ||
             savedLegacyRequest.StreamId != request.StreamId ||
             savedLegacyRequest.OwnerId != request.OwnerId ||
             savedLegacyRequest.Ordinal != request.Ordinal ||
             savedLegacyRequest.OperationKind != request.OperationKind ||
             !savedLegacyRequest.OrderedCandidates.SequenceEqual(
                 request.OrderedCandidates)))
            throw new InvalidDataException(
                "Legacy Future of Potions request differs from its current potion support.");
        var weights = nativeRoute
            ? NativeNextIntGridWeights(cardTypes.Length)
            : Enumerable.Repeat(1L, cardTypes.Length).ToArray();
        var outcomes = cardTypes.Select((cardType, index) =>
            new ChanceLawOutcomeV111(
                $"card_type:{cardType}:index:{index}", weights[index]))
            .ToArray();
        var priorCalls = potionEntries.Take(ordinal).Select(prior =>
        {
            if (!PrototypeNativeFutureOfPotions.TryGetCardRarity(
                    prior.PotionId, out var priorRarity))
                throw new InvalidOperationException(
                    "Future of Potions source prefix contains an unsupported potion.");
            return new
            {
                prior.Slot,
                prior.PotionId,
                CardTypes = PrototypeNativeFutureOfPotions
                    .CardTypesForRarity(priorRarity)
            };
        }).ToArray();
        var conditioningFingerprint = nativeRoute
            ? CanonicalJson.Sha256(new
            {
                boundary = "native_event.future_of_potions.card_type",
                actionId,
                eventId,
                nodeId,
                priorCalls,
                currentSlot = entry.Slot,
                entry.PotionId,
                rarity,
                cardTypes,
                request
            })
            : CanonicalJson.Sha256(new
            {
                boundary = "native_regional_legacy.future_of_potions.card_type",
                actionId,
                eventId,
                nodeId,
                priorCalls,
                currentSlot = entry.Slot,
                entry.PotionId,
                rarity,
                cardTypes,
                request
            });
        var lawOrdinal = nativeRoute ? ordinal : savedLegacyRequest!.Ordinal;
        return CreateNativeBoundaryLaw(
            siteId, request.OperationKind, ownerId, lawOrdinal,
            nativeRoute
                ? "native-event-index-prefix-v1"
                : "native-regional-legacy-event-index-prefix-v1",
            conditioningFingerprint,
            nativeRoute
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-legacy-event-index-support-unverified",
            outcomes, nativeRoute
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    /// <summary>
    /// Query the event-local bounded ticket which Stone of All Time discards
    /// after either choice. The event action is a declared conditioning
    /// branch; the query does not inspect or advance either RNG provider.
    /// </summary>
    public static ChanceLawV111 QueryNativeStoneOfAllTimeNoEffectIndex(
        RunState origin, string siteId, int ordinal, string actionId)
        => QueryStoneOfAllTimeNoEffectIndex(
            origin, siteId, ordinal, actionId);

    public static ChanceLawV111 QueryStoneOfAllTimeNoEffectIndex(
        RunState origin, string siteId, int ordinal, string actionId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (!NativeEventNoEffectIndexQuerySiteIds.Contains(
                siteId, StringComparer.Ordinal) || ordinal < 0 ||
            origin.Phase != RunPhase.Event ||
            origin.World is not { Act: 2, Event: { } evt } world ||
            evt.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.ControlledChoice is not null ||
            evt.Stochastic.ProviderMode is not (null or "scripted-v111") ||
            origin.Stochastic.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Stone of All Time ticket laws require its active event-choice boundary.");

        var nativeRoute = origin.Stochastic.NativeRunRng is not null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.Stochastic.NativeRng?.StreamId ==
                NativeRngV111Contexts.EventStreamId(
                    PrototypeNativeStoneOfAllTime.NativeRngEventId);
        var legacyRoute = origin.Stochastic.NativeRunRng is null &&
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.Stochastic.NativeRng is null &&
            evt.Stochastic.ProviderMode is null;
        var nativeRegionalLegacyRoute = origin.Stochastic.NativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.Stochastic.NativeRng is null &&
            evt.Stochastic.ProviderMode is null;
        if (!nativeRoute && !legacyRoute && !nativeRegionalLegacyRoute)
            throw new InvalidOperationException(
                "Stone of All Time ticket laws require a complete Native or legacy event RNG route.");
        var streamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId)
            : "event";
        if (nativeRoute && ordinal != 1)
            throw new InvalidOperationException(
                "Native Stone of All Time ticket laws require event-stream ordinal 1 after the entry potion-slot draw.");
        PrototypeStateInvariants.Validate(origin);

        var expectedChoice = siteId switch
        {
            NativeStoneOfAllTimePushTicketSiteId => "push",
            NativeStoneOfAllTimeLiftTicketSiteId => "lift",
            _ => throw new NotSupportedException(
                $"Stone of All Time does not support ticket site '{siteId}'.")
        };
        if (actionId != expectedChoice)
            throw new ArgumentException(
                $"Stone of All Time ticket site '{siteId}' requires action '{expectedChoice}'.",
                nameof(actionId));
        if (siteId == NativeStoneOfAllTimeLiftTicketSiteId &&
            (evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not
                { } potionSlot ||
             potionSlot < 0 || potionSlot >= origin.Player.PotionSlots.Length ||
             origin.Player.PotionSlots[potionSlot] is null))
            throw new InvalidOperationException(
                "Stone of All Time lift requires its saved occupied potion slot.");

        var action = GameAction.Create(
            "event_choice", new EventChoicePayload(expectedChoice));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                $"Stone of All Time choice '{expectedChoice}' is not legal in the current state.");

        var ownerId = $"event:stone_of_all_time:{expectedChoice}";
        var conditioningFingerprint = nativeRoute
            ? CanonicalJson.Sha256(new
            {
                boundary = "native_event.stone_of_all_time.no_effect_ticket",
                eventId = evt.EventId,
                actionId,
                sourceOperation = new
                {
                    siteId,
                    streamId,
                    ownerId,
                    operationKind = "bounded_integer",
                    boundedCount = 100
                }
            })
            : CanonicalJson.Sha256(new
            {
                boundary = "reference_nature.stone_of_all_time.no_effect_ticket",
                eventId = evt.EventId,
                actionId,
                providerRoute = "legacy",
                sourceOperation = new
                {
                    siteId,
                    streamId,
                    ownerId,
                    ordinal,
                    operationKind = "bounded_integer",
                    boundedCount = 100
                }
            });
        return CreateNativeBoundaryLaw(
            siteId, "bounded_integer_no_effect", ownerId, ordinal,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectPolicyId
                : LegacyStoneOfAllTimeNoEffectPolicyId,
            conditioningFingerprint,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectProvenance
                : LegacyStoneOfAllTimeNoEffectProvenance,
            [new ChanceLawOutcomeV111("no_effect", 1L)],
            ChanceLawV111.SourceVerifiedStatus);
    }

    public static ChanceLawV111 QueryNativeEventGoldUnitFloat(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (siteId is not (NativeEventGoldUnitFloatSiteId))
            throw new NotSupportedException(
                $"Native event gold unit-float law does not support site '{siteId}'.");
        if (ordinal < 0 ||
            origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Native event gold laws require a configured pre-entry Event-node boundary.");
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Native event gold law requires a current legal Event-node action.");
        var nodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Native event gold law requires a current legal Event-node action.");

        ValidateNativeEventBranch(origin, world, eventId);
        var (nativeEventEntry, stages) = eventId switch
        {
            PrototypeNativeHiveEvents.LostWispId =>
                (PrototypeNativeHiveEvents.LostWispNativeRngEventId,
                    new[] { "primary" }),
            "proto.native.event.jungle_maze_adventure" =>
                ("JUNGLE_MAZE_ADVENTURE",
                    new[] { "primary", "secondary" }),
            _ => throw new InvalidOperationException(
                "The requested Event ID has no pinned Native gold unit-float mapping.")
        };
        if (ordinal >= stages.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var stage = stages[ordinal];
        var ownerId = $"event:{eventId}:gold:{stage}";
        var streamId = NativeRngV111Contexts.EventStreamId(nativeEventEntry);
        var request = new RandomUnitFloatRequestV111(
            siteId, streamId, ownerId, ordinal);
        request.Validate();

        int MapGold(float roll) => NativeEventGoldUnitFloatOutcome(
            eventId, stage, roll);
        var weights = NativeNextFloatMappedIntegerWeights(MapGold);
        var conditioning = new
        {
            boundary = "native_event.generated_gold.unit_float",
            actionId,
            eventId,
            stage,
            sourceMapping = eventId == PrototypeNativeHiveEvents.LostWispId
                ? "lost-wisp-60-plus-unit-float-times-30-minus-15-truncate-v1"
                : stage == "primary"
                    ? "jungle-maze-solo-150-plus-unit-float-times-30-minus-15-truncate-v1"
                    : "jungle-maze-join-50-plus-unit-float-times-30-minus-15-truncate-v1",
            request
        };
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventLawId = CanonicalJson.Sha256(new
        {
            siteId, ownerId, ordinal, conditioningFingerprint
        });
        var outcomes = weights.Select(pair => new ChanceLawOutcomeV111(
            "gold:" + pair.Key.ToString(CultureInfo.InvariantCulture),
            pair.Value)).ToArray();
        return Create(eventLawId, siteId, "unit_float_mapped_gold",
            ownerId, ordinal, "native-event-gold-unit-float-v1",
            conditioningFingerprint,
            NativeUnitFloatMappedOutcomeSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    public static ChanceLawV111 QueryNativeEventGoldIndex(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (siteId is not (NativeEventGoldIndexSiteId))
            throw new NotSupportedException(
                $"Native event gold index law does not support site '{siteId}'.");
        if (ordinal < 0 ||
            origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Native event gold laws require a configured pre-entry Event-node boundary.");
        PrototypeStateInvariants.Validate(origin);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Native event gold law requires a current legal Event-node action.");
        var nodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Native event gold law requires a current legal Event-node action.");

        ValidateNativeEventBranch(origin, world, eventId);
        var (nativeEventEntry, stages, bounds) = eventId switch
        {
            var id when id == PrototypeNativeDenseVegetation.EventId =>
                ("DENSE_VEGETATION", new[] { "primary" },
                    new[] { 39 }),
            "proto.native.event.whispering_hollow" =>
                ("WHISPERING_HOLLOW",
                    new[] { "primary" }, new[] { 19 }),
            "proto.native.event.sunken_statue" or
                "proto.native.underdocks.sunken_statue" =>
                ("SUNKEN_STATUE", new[] { "primary" },
                    new[] { 21 }),
            "proto.native.event.luminous_choir" =>
                ("LUMINOUS_CHOIR",
                    new[] { "primary" }, new[] { 50 }),
            "proto.native.underdocks.sunken_treasury" =>
                ("SUNKEN_TREASURY",
                    new[] { "primary", "secondary" }, new[] { 16, 61 }),
            var id when id == PrototypeNativePunchOff.EventId =>
                (PrototypeNativePunchOff.NativeRngEventId,
                    new[] { "primary" }, new[] { 8 }),
            var id when id == PrototypeNativeSharedEvents.ThisOrThatId =>
                (PrototypeNativeSharedEvents.ThisOrThatNativeRngEventId,
                    new[] { "primary" }, new[] { 28 }),
            _ => throw new InvalidOperationException(
                "The requested Event ID has no pinned Native gold index mapping.")
        };
        if (ordinal >= stages.Length)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var stage = stages[ordinal];
        var ownerId = $"event:{eventId}:gold:{stage}";
        var streamId = NativeRngV111Contexts.EventStreamId(nativeEventEntry);
        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: bounds[ordinal]);
        request.Validate();

        var indexWeights = NativeNextIntGridWeights(request.SupportCount);
        var goldWeights = new SortedDictionary<int, long>();
        for (var index = 0; index < indexWeights.Length; index++)
        {
            var gold = NativeEventGoldIndexOutcome(eventId, stage, index);
            goldWeights[gold] = checked(
                goldWeights.GetValueOrDefault(gold) + indexWeights[index]);
        }
        var sourceMapping = NativeEventGoldIndexMappingId(eventId, stage);
        var conditioning = new
        {
            boundary = "native_event.generated_gold.index_draw",
            actionId,
            eventId,
            stage,
            sourceMapping,
            request
        };
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventLawId = CanonicalJson.Sha256(new
        {
            siteId, ownerId, ordinal, conditioningFingerprint
        });
        var outcomes = goldWeights.Select(pair => new ChanceLawOutcomeV111(
            "gold:" + pair.Key.ToString(CultureInfo.InvariantCulture),
            pair.Value)).ToArray();
        return Create(eventLawId, siteId, "index_mapped_gold",
            ownerId, ordinal, "native-event-gold-index-v1",
            conditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query a captured legacy generated-gold index on the configured
    /// Native-regional route. The saved typed request supplies its actual
    /// event-stream ordinal; the declared event and stage determine the
    /// ordered support without inspecting the selected event or RNG cursor.
    /// </summary>
    public static ChanceLawV111 QueryLegacyNativeRegionalEventGoldIndex(
        RunState origin, RandomIndexRequestV111 request, string actionId,
        string eventId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        request.Validate();
        if (request.SiteId != NativeEventGoldIndexSiteId ||
            request.StreamId != "event" ||
            request.OperationKind != "choose_index" ||
            origin.Stochastic.NativeRunRng is not null ||
            origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Legacy event-gold laws require a captured index request at a configured Native-regional pre-entry boundary.");
        PrototypeStateInvariants.Validate(origin);

        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Legacy event-gold law requires a current legal Event-node action.");
        var nodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Legacy event-gold law requires a current legal Event-node action.");

        ValidateNativeEventBranch(origin, world, eventId);
        var ownerPrefix = $"event:{eventId}:gold:";
        if (!request.OwnerId.StartsWith(ownerPrefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Legacy event-gold owner does not match the declared event branch.");
        var stage = request.OwnerId[ownerPrefix.Length..];
        var (minimum, count) = (eventId, stage) switch
        {
            (PrototypeNativeDenseVegetation.EventId, "primary") =>
                (61, 39),
            ("proto.native.event.whispering_hollow", "primary") =>
                (26, 19),
            ("proto.native.event.sunken_statue", "primary") =>
                (101, 21),
            ("proto.native.event.luminous_choir", "primary") =>
                (100, 50),
            ("proto.native.event.jungle_maze_adventure", "primary") =>
                (135, 30),
            ("proto.native.event.jungle_maze_adventure", "secondary") =>
                (35, 30),
            ("proto.native.underdocks.sunken_treasury", "primary") =>
                (52, 16),
            ("proto.native.underdocks.sunken_treasury", "secondary") =>
                (303, 61),
            (PrototypeNativeHiveEvents.LostWispId, "primary") =>
                (45, 31),
            ("proto.native.underdocks.sunken_statue", "primary") =>
                (101, 21),
            (PrototypeNativePunchOff.EventId, "primary") =>
                (91, 8),
            (PrototypeNativeSharedEvents.ThisOrThatId, "primary") =>
                (41, 28),
            _ => throw new InvalidOperationException(
                "The declared legacy event branch has no supported gold-index stage.")
        };
        var candidates = Enumerable.Range(minimum, count).ToArray();
        if (!request.OrderedCandidates.SequenceEqual(candidates))
            throw new InvalidDataException(
                "Legacy event-gold request differs from its declared ordered source support.");

        var conditioning = new
        {
            boundary = "native_regional_legacy_event.generated_gold.index_draw",
            actionId,
            eventId,
            stage,
            sourceMapping = "saved-legacy-event-gold-ordered-support-v1",
            request
        };
        var conditioningFingerprint = CanonicalJson.Sha256(conditioning);
        var eventLawId = CanonicalJson.Sha256(new
        {
            siteId = request.SiteId,
            ownerId = request.OwnerId,
            ordinal = request.Ordinal,
            conditioningFingerprint
        });
        var outcomes = candidates.Select(gold =>
            new ChanceLawOutcomeV111(
                "gold:" + gold.ToString(CultureInfo.InvariantCulture), 1L))
            .ToArray();
        return Create(eventLawId, request.SiteId,
            "legacy_index_mapped_gold", request.OwnerId, request.Ordinal,
            "native-regional-legacy-event-gold-index-v1",
            conditioningFingerprint,
            "uniform-saved-legacy-event-gold-index-support-unverified",
            outcomes, ChanceLawV111.UnverifiedStatus);
    }

    private static void ValidateNativeEventBranch(
        RunState origin, RunWorldState world, string eventId)
    {
        if (!PrototypeContent.Events.TryGetValue(eventId, out var eventDefinition) ||
            world.Act < eventDefinition.MinAct ||
            world.Act > eventDefinition.MaxAct ||
            world.EventIds.Contains(eventId, StringComparer.Ordinal) &&
                eventDefinition.OncePerRun)
            throw new InvalidOperationException(
                "The declared Native event branch is not available in this run state.");

        var overgrowth = world.Act == 1 &&
            PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId);
        var underdocks = world.Act == 1 &&
            world.Map.GenerationProfileId ==
                PrototypeNativeUnderdocks.GenerationProfileId;
        var laterRoute = world.Act is 2 or 3 &&
            origin.Configuration?.UsesExperimentalSilentThreeActRoute == true &&
            world.Map.GenerationProfileId is
                PrototypeNativeLaterActMap.HiveProfileId or
                PrototypeNativeLaterActMap.GloryProfileId or
                PrototypeGoldenPathMapV111.ProfileId;
        var isSupported = overgrowth &&
                (PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(eventId) ||
                    PrototypeNativeSharedEvents.IsSupported(eventId)) ||
            underdocks && PrototypeNativeUnderdocksEvents.IsSupported(eventId) ||
            laterRoute && ExperimentalSilentThreeActRouteV111
                .ContainsEvent(eventId, world.Act);
        if (!isSupported || !laterRoute && eventDefinition.Weight <= 0)
            throw new InvalidOperationException(
                "The declared event branch is outside the configured Native event route.");
    }

    internal static int NativeEventGoldIndexOutcome(
        string eventId, string stage, int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        return (eventId, stage) switch
        {
            ("proto.native.event.dense_vegetation", "primary") =>
                61 + index,
            ("proto.native.event.whispering_hollow", "primary") =>
                26 + index,
            ("proto.native.event.sunken_statue", "primary") or
                ("proto.native.underdocks.sunken_statue", "primary") =>
                101 + index,
            ("proto.native.event.luminous_choir", "primary") =>
                149 - index,
            ("proto.native.underdocks.sunken_treasury", "primary") =>
                52 + index,
            ("proto.native.underdocks.sunken_treasury", "secondary") =>
                303 + index,
            ("proto.native.underdocks.punch_off", "primary") =>
                91 + index,
            ("proto.native.event.this_or_that", "primary") =>
                41 + index,
            _ => throw new NotSupportedException(
                $"Native bounded-index gold mapping does not support '{eventId}' stage '{stage}'.")
        };
    }

    private static string NativeEventGoldIndexMappingId(
        string eventId, string stage) => (eventId, stage) switch
    {
        ("proto.native.event.dense_vegetation", "primary") =>
            "dense-vegetation-61-plus-index-v1",
        ("proto.native.event.whispering_hollow", "primary") =>
            "whispering-hollow-26-plus-index-v1",
        ("proto.native.event.sunken_statue", "primary") =>
            "sunken-statue-101-plus-index-v1",
        ("proto.native.underdocks.sunken_statue", "primary") =>
            "underdocks-sunken-statue-101-plus-index-v1",
        ("proto.native.event.luminous_choir", "primary") =>
            "luminous-choir-149-minus-index-v1",
        ("proto.native.underdocks.sunken_treasury", "primary") =>
            "sunken-treasury-52-plus-index-v1",
        ("proto.native.underdocks.sunken_treasury", "secondary") =>
            "sunken-treasury-303-plus-index-v1",
        ("proto.native.underdocks.punch_off", "primary") =>
            "punch-off-91-plus-index-v1",
        ("proto.native.event.this_or_that", "primary") =>
            "this-or-that-41-plus-index-v1",
        _ => throw new NotSupportedException(
            $"Native bounded-index gold mapping does not support '{eventId}' stage '{stage}'.")
    };

    internal static int NativeEventGoldUnitFloatOutcome(
        string eventId, string stage, float roll)
    {
        if (!float.IsFinite(roll) || roll < 0 || roll > 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
        return (eventId, stage) switch
        {
            (PrototypeNativeHiveEvents.LostWispId, "primary") =>
                (int)(60f + (roll * 30f - 15f)),
            ("proto.native.event.jungle_maze_adventure", "primary") =>
                (int)(150f + (roll * 30f - 15f)),
            ("proto.native.event.jungle_maze_adventure", "secondary") =>
                (int)(50f + (roll * 30f - 15f)),
            _ => throw new NotSupportedException(
                $"Native unit-float gold mapping does not support '{eventId}' stage '{stage}'.")
        };
    }

    public static ChanceLawV111 QueryNativeEventCardPick(
        RunState origin, string siteId, int ordinal,
        int[]? priorIndices = null, string? actionId = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        priorIndices ??= [];
        if (siteId == PrototypeNativeHiveEvents
                .InfestedAutomatonCardOfferSiteId)
        {
            if (priorIndices.Length != 0)
                throw new InvalidOperationException(
                    "Infested Automaton card-pick queries accept no prior indices.");
            var infestedPlan =
                NativeEventInfestedAutomatonCardPickPlanBuilderV111.Build(
                    origin, siteId, ordinal, actionId);
            var infestedNativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
                infestedPlan.Request.StreamId);
            var infestedWeights = infestedNativeIndexLaw
                ? NativeNextIntGridWeights(
                    infestedPlan.Request.SupportCount)
                : Enumerable.Repeat(1L,
                    infestedPlan.Request.SupportCount).ToArray();
            var infestedOutcomes = infestedPlan.CandidateCardIds.Select(
                (cardId, index) => new ChanceLawOutcomeV111(
                    "card:" + cardId, infestedWeights[index])).ToArray();
            var infestedEventId = CanonicalJson.Sha256(new
            {
                boundary = "native_event.card_pick",
                infestedPlan.ConditioningFingerprint
            });
            return Create(infestedEventId, infestedPlan.Request.SiteId,
                infestedPlan.Request.OperationKind,
                infestedPlan.Request.OwnerId,
                infestedPlan.Request.Ordinal,
                NativeEventCardPickConditioningPolicyId,
                infestedPlan.ConditioningFingerprint,
                infestedNativeIndexLaw
                    ? NativeBoundedIndexSourceVerifiedProvenance
                    : "uniform-saved-index-support-unverified",
                infestedOutcomes,
                infestedNativeIndexLaw
                    ? ChanceLawV111.SourceVerifiedStatus
                    : ChanceLawV111.UnverifiedStatus);
        }
        var plan = NativeEventReflectionsChoicePlanBuilderV111.BuildNext(
            origin, siteId, ordinal, priorIndices, actionId);
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            plan.Request.StreamId);
        var weights = nativeIndexLaw
            ? NativeNextIntGridWeights(plan.Request.SupportCount)
            : Enumerable.Repeat(1L,
                plan.Request.SupportCount).ToArray();
        var outcomes = plan.CandidateCardInstanceIds.Select((instanceId, index) =>
            new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), weights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.card_pick",
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, NativeEventCardPickConditioningPolicyId,
            plan.ConditioningFingerprint,
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified",
            outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    public static ChanceLawV111 QueryControlledEventChoice(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var pending = ControlledEventChoicePendingV111.Describe(run);
        if (pending.EventId == PrototypeNativeStoneOfAllTime.EventId &&
            pending.Kind == "event_random_choice" &&
            pending.ChoiceId is "push" or "lift")
            return QueryControlledStoneOfAllTimeTicket(
                run, pending.Request, pending.BoundaryFingerprint);
        if (QueryControlledEventChoiceSemanticLaw(run, pending) is
            { } semanticLaw)
            return semanticLaw;
        return QueryRandomIndex(pending.ChanceEventId, pending.Request);
    }

    private static ChanceLawV111? QueryControlledEventChoiceSemanticLaw(
        RunState run, ControlledEventChoicePendingV111 pending)
    {
        var request = pending.Request;
        PrototypeControlledEventChoiceContinuationV111? effect = null;
        PrototypeControlledEventRewardChoiceContinuationV111? reward = null;
        RunState source;
        if (pending.Kind == "event_random_choice")
        {
            effect = run.World?.Event?.Stochastic.ControlledChoice ??
                throw new InvalidDataException(
                    "Controlled event-effect law has no saved continuation.");
            if (effect.EventId != pending.EventId ||
                effect.ChoiceId != pending.ChoiceId)
                throw new InvalidDataException(
                    "Controlled event-effect law differs from its pending identity.");
            source = effect.PartialState;
        }
        else if (pending.Kind == "event_reward_random_choice")
        {
            reward = run.Stochastic.ControlledEventRewardChoice ??
                throw new InvalidDataException(
                    "Controlled event-reward law has no saved continuation.");
            if (reward.EventId != pending.EventId ||
                reward.ChoiceId != pending.ChoiceId)
                throw new InvalidDataException(
                    "Controlled event-reward law differs from its pending identity.");
            source = reward.PartialState;
        }
        else
        {
            throw new InvalidDataException(
                "Controlled event-choice law has an unknown continuation kind.");
        }

        ChanceLawV111? law = request.SiteId switch
        {
            "event.random_upgrade.card_instance"
                => effect is not null
                    ? QueryLegacyEventRandomUpgradeCardInstance(
                        source, pending.EventId, pending.ChoiceId,
                        request.Ordinal)
                    : throw new InvalidDataException(
                        "Legacy event random-upgrade law is outside an event-effect continuation."),
            "event.reward.random_rare_card.model"
                => effect is not null
                    ? QueryLegacyEventRewardRareCardModel(
                        pending.EventId, pending.ChoiceId, request.Ordinal)
                    : throw new InvalidDataException(
                        "Legacy event Rare-card law is outside an event-effect continuation."),
            "event.reward.random_potion.model"
                => effect is not null
                    ? QueryLegacyEventRewardPotionModel(
                        pending.EventId, pending.ChoiceId, request.Ordinal)
                    : throw new InvalidDataException(
                        "Legacy event potion law is outside an event-effect continuation."),
            "event.potion.fill_empty_slots.model"
                => effect is not null
                    ? QueryLegacyControlledEventFillPotionSlot(
                        source, request)
                    : throw new InvalidDataException(
                        "Legacy event potion-fill law is outside an event-effect continuation."),
            "event.reward.offer_potion.model.index_draw"
                => effect is not null
                    ? QueryLegacyEventOfferPotionModel(
                        "Event:" + pending.EventId + ":choice:" +
                            pending.ChoiceId,
                        PrototypeGameEngine.CanAcquirePotion(source.Player),
                        request.Ordinal)
                    : throw new InvalidDataException(
                        "Legacy event potion-offer law is outside an event-effect continuation."),
            "event.reward.random_modifier_curse.index_draw"
                => reward is not null
                    ? QueryLegacyEventModifierCurseModel(
                        pending.EventId, request.Ordinal)
                    : throw new InvalidDataException(
                        "Legacy event modifier-curse law is outside an event-reward continuation."),
            "reward.card_rarity.ticket" or
                "reward.card_model.index_draw"
                => effect is not null
                    ? QueryControlledLegacyEventRewardCardLaw(
                        source, effect, request)
                    : throw new InvalidDataException(
                        "Legacy event reward-card law is outside an event-effect continuation."),
            "reward.card_transform.replacement.index_draw"
                when effect is not null =>
                    QueryControlledLegacyEventCardTransform(
                        source, effect, request),
            "reward.card_transform.replacement.index_draw" => null,
            StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId
                when effect is not null =>
                    QueryControlledEventPoolShuffle(
                        source, effect, pending),
            StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId
                when effect is not null =>
                    QueryControlledEventPoolShuffle(
                        source, effect, pending),
            "relic.unowned_random_selection.index_draw"
                when effect is not null =>
                    QueryControlledEventUnownedRelicSelection(
                        source, effect, request),
            "relic.unowned_random_selection.index_draw" => null,
            NativePaelRewardsV111.LegionSkinSiteId
                when effect is not null =>
                    QueryControlledEventPaelsLegionSkin(
                        source, effect, request),
            NativePaelRewardsV111.LegionSkinSiteId => null,
            NativeGloryAncientsV111.DistinguishedCapeCurseSiteId
                when effect is not null =>
                    QueryControlledEventDistinguishedCapeCurse(
                        source, effect, pending),
            NativeGloryAncientsV111.DistinguishedCapeCurseSiteId => null,
            "relic.acquisition.random_rare_card.model"
                when effect is null => throw new InvalidDataException(
                    "Rare-card acquisition law is outside an event-effect continuation."),
            "relic.acquisition.random_rare_card.model"
                when request.StreamId is "reward" or
                    NativePlayerRngV111.RewardsStreamId =>
                    QueryControlledEventRelicAcquisitionModel(
                        source, effect!, pending),
            "relic.acquisition.random_rare_card.model" => null,
            "relic.acquisition.random_potion.model"
                when effect is null => throw new InvalidDataException(
                    "Potion acquisition law is outside an event-effect continuation."),
            "relic.acquisition.random_potion.model"
                when request.StreamId is "event" or
                    NativePlayerRngV111.RewardsStreamId =>
                    QueryControlledEventRelicAcquisitionModel(
                        source, effect!, pending),
            "relic.acquisition.random_potion.model" => null,
            _ => null
        };
        if (law is null)
            return null;

        var expectedStream = request.SiteId switch
        {
            "event.reward.random_rare_card.model" or
                "reward.card_rarity.ticket" or
                "reward.card_model.index_draw" or
                "reward.card_transform.replacement.index_draw" or
                StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId or
                StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId or
                NativePaelRewardsV111.LegionSkinSiteId or
                "event.reward.offer_potion.model.index_draw" or
                "event.reward.random_modifier_curse.index_draw" => "reward",
            "relic.unowned_random_selection.index_draw" => request.StreamId,
            "relic.acquisition.random_rare_card.model" or
                "relic.acquisition.random_potion.model" => request.StreamId,
            NativeGloryAncientsV111.DistinguishedCapeCurseSiteId =>
                request.StreamId,
            _ => "event"
        };
        string[] expectedLabels;
        var numericSupportLabels = request.SiteId is
            "reward.card_rarity.ticket" or
            NativePaelRewardsV111.LegionSkinSiteId ||
            (request.StreamId == NativePlayerRngV111.RewardsStreamId &&
                (request.SiteId ==
                    "relic.acquisition.random_rare_card.model" ||
                 request.SiteId ==
                    "relic.acquisition.random_potion.model"));
        var boundedIndexSupport = request.SiteId ==
                "reward.card_rarity.ticket" || request.SiteId is
            StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId or
            StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId;
        if (request.SiteId == "event.random_upgrade.card_instance")
        {
            const string outcomePrefix = "card_instance:";
            expectedLabels = law.Outcomes.Select(outcome =>
            {
                if (!outcome.OutcomeId.StartsWith(
                        outcomePrefix, StringComparison.Ordinal) ||
                    !long.TryParse(
                        outcome.OutcomeId[outcomePrefix.Length..],
                        NumberStyles.None, CultureInfo.InvariantCulture,
                        out var instanceId))
                    throw new InvalidDataException(
                        "Controlled event random-upgrade law has an invalid physical card identity.");
                var card = source.Player.Deck.SingleOrDefault(candidate =>
                    candidate.InstanceId == instanceId) ??
                    throw new InvalidDataException(
                        "Controlled event random-upgrade law names a card outside its saved deck.");
                return card.CardId + "#" + instanceId.ToString(
                    CultureInfo.InvariantCulture);
            }).ToArray();
        }
        else if (numericSupportLabels)
        {
            expectedLabels = Enumerable.Range(0, law.SupportCount)
                .Select(index => index.ToString(CultureInfo.InvariantCulture))
                .ToArray();
        }
        else
        {
            var eventEffectKind = effect is null
                ? (PrototypeRunEffectKind?)null
                : ControlledEventChoiceEffectKind(effect);
            var relicOutcomes = request.SiteId ==
                    "relic.unowned_random_selection.index_draw" ||
                (request.SiteId ==
                    StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId &&
                    eventEffectKind ==
                        PrototypeRunEffectKind.OfferNeowsBonesRelics);
            var cardOutcomes = request.SiteId is
                StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId or
                StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId or
                "event.reward.random_rare_card.model" or
                "event.reward.random_modifier_curse.index_draw" or
                NativeGloryAncientsV111.DistinguishedCapeCurseSiteId or
                "relic.acquisition.random_rare_card.model" or
                "reward.card_model.index_draw" or
                "reward.card_transform.replacement.index_draw";
            var outcomePrefix = relicOutcomes
                ? "relic:" : cardOutcomes ? "card:" : "potion:";
            expectedLabels = law.Outcomes.Select(outcome =>
            {
                if (!outcome.OutcomeId.StartsWith(
                        outcomePrefix, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Controlled event law has an unexpected semantic outcome identity.");
                return outcome.OutcomeId[outcomePrefix.Length..];
            }).ToArray();
        }
        var orderedSupportMatches = boundedIndexSupport
            ? request.OperationKind == "bounded_integer" &&
                request.BoundedCount == law.SupportCount &&
                request.OrderedCandidates.Length == 0
            : request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, law.SupportCount));
        if (request.SiteId != law.SiteId ||
            request.StreamId != expectedStream ||
            request.OperationKind != law.OperationKind ||
            request.OwnerId != law.OwnerId ||
            request.Ordinal != law.Ordinal ||
            request.SupportCount != law.SupportCount ||
            !orderedSupportMatches ||
            !ControlledEventLabelsMatch(
                request.SiteId, pending.CandidateLabels, expectedLabels,
                boundedIndexSupport))
            throw new InvalidDataException(
                "Controlled event law differs from its saved semantic support.");

        return law with { EventId = pending.ChanceEventId };
    }

    private static ChanceLawV111 QueryControlledLegacyEventRewardCardLaw(
        RunState source,
        PrototypeControlledEventChoiceContinuationV111 continuation,
        RandomIndexRequestV111 request)
    {
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Legacy event reward-card law is outside its saved event boundary.");

        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate =>
                candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Legacy event reward-card law references an unknown choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Legacy event reward-card law is outside a supported card-offer effect.");
        var rewardCardCount = choice.Effects[continuation.NextEffectIndex].Kind
            switch
            {
                PrototypeRunEffectKind.OfferThreeCardsAndPotion => 3,
                PrototypeRunEffectKind.OfferFiveClassCards => 5,
                _ => 0
            };
        if (rewardCardCount == 0)
            throw new InvalidDataException(
                "Legacy event reward-card law is outside a supported card-offer effect.");

        var groupOwnerId = $"act:{world.Act}:reward_group:external";
        var ownerPrefix = groupOwnerId + ":card:";
        if (!request.OwnerId.StartsWith(ownerPrefix,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "Legacy event reward-card request has an invalid reward-group owner.");

        var suffix = request.OwnerId[ownerPrefix.Length..];
        string[] priorCardIds;
        if (request.SiteId == "reward.card_rarity.ticket")
        {
            const string weightsMarker = ":weights:";
            var weightsOffset = suffix.IndexOf(weightsMarker,
                StringComparison.Ordinal);
            if (weightsOffset < 0 ||
                !int.TryParse(suffix[..weightsOffset], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var priorPickCount) ||
                priorPickCount < 0 || priorPickCount >= rewardCardCount)
                throw new InvalidDataException(
                    "Legacy event reward-card rarity request has an invalid pick count.");
            priorCardIds = ControlledEventRewardPriorCardIds(
                continuation, groupOwnerId, priorPickCount);
            return QueryLegacyRewardCardRarityTicketIndices(
                world.Act, groupOwnerId, priorCardIds, request.Ordinal);
        }

        const string rarityMarker = ":rarity:";
        const string poolMarker = ":pool:";
        var rarityOffset = suffix.IndexOf(rarityMarker,
            StringComparison.Ordinal);
        var poolSearchOffset = rarityOffset + rarityMarker.Length;
        var poolOffset = rarityOffset < 0 ||
            poolSearchOffset > suffix.Length ? -1 : suffix.IndexOf(
                poolMarker, poolSearchOffset, StringComparison.Ordinal);
        if (rarityOffset < 0 || poolOffset < 0 ||
            !int.TryParse(suffix[..rarityOffset], NumberStyles.None,
                CultureInfo.InvariantCulture, out var modelPriorPickCount) ||
            modelPriorPickCount < 0 || modelPriorPickCount >= rewardCardCount ||
            !Enum.TryParse<PrototypeCardRarity>(
                suffix[(rarityOffset + rarityMarker.Length)..poolOffset],
                ignoreCase: true, out var rarity) || !Enum.IsDefined(rarity))
            throw new InvalidDataException(
                "Legacy event reward-card model request has an invalid owner.");

        priorCardIds = ControlledEventRewardPriorCardIds(
            continuation, groupOwnerId, modelPriorPickCount);
        return QueryLegacyRewardCardModel(
            world.Act, groupOwnerId, rarity, priorCardIds, request.Ordinal);
    }

    private static string[] ControlledEventRewardPriorCardIds(
        PrototypeControlledEventChoiceContinuationV111 continuation,
        string groupOwnerId,
        int priorPickCount)
    {
        var cardIds = new List<string>(priorPickCount);
        var decisionCandidateLabels = continuation.DecisionCandidateLabels ??
            throw new InvalidDataException(
                "Legacy event reward-card continuation has no saved candidate labels.");
        for (var pickIndex = priorPickCount - 1;
             pickIndex >= 0; pickIndex--)
        {
            var ownerPrefix = groupOwnerId + ":card:" +
                pickIndex.ToString(CultureInfo.InvariantCulture) + ":rarity:";
            var decisionIndex = -1;
            for (var index = continuation.Decisions.Length - 1;
                 index >= continuation.EffectDecisionStartIndex; index--)
            {
                var decision = continuation.Decisions[index];
                if (decision.Request.SiteId ==
                        "reward.card_model.index_draw" &&
                    decision.Request.OwnerId.StartsWith(ownerPrefix,
                        StringComparison.Ordinal))
                {
                    decisionIndex = index;
                    break;
                }
            }

            if (decisionIndex < 0)
                throw new InvalidDataException(
                    "Legacy event reward-card request is missing a prior card pick.");
            var labels = decisionCandidateLabels[decisionIndex];
            var selectedIndex = continuation.Decisions[decisionIndex]
                .SelectedIndex;
            if ((uint)selectedIndex >= (uint)labels.Length ||
                string.IsNullOrWhiteSpace(labels[selectedIndex]))
                throw new InvalidDataException(
                    "Legacy event reward-card prefix has an invalid selected card identity.");
            cardIds.Add(labels[selectedIndex]);
        }

        cardIds.Reverse();
        return cardIds.ToArray();
    }

    private static ChanceLawV111 QueryControlledEventPaelsLegionSkin(
        RunState source,
        PrototypeControlledEventChoiceContinuationV111 continuation,
        RandomIndexRequestV111 request)
    {
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Pael's Legion skin law is outside its saved event boundary.");

        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate => candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Pael's Legion skin law references an unknown event choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Pael's Legion skin law is outside its saved effect boundary.");
        var effect = choice.Effects[continuation.NextEffectIndex];
        if (effect.Kind != PrototypeRunEffectKind.GainRelic ||
            effect.RelicId != NativePaelRewardsV111.LegionId)
            throw new InvalidDataException(
                "Pael's Legion skin request differs from its saved relic-acquisition effect.");

        // Event relic acquisition appends the new instance before running
        // AfterObtained. The suspended effect checkpoint intentionally keeps
        // its pre-effect player, so that next append index is the saved count.
        var expectedOwnerId = "relic:paels_legion:instance:" +
            source.Player.Relics.Length.ToString(
                CultureInfo.InvariantCulture);
        if (request.StreamId != "reward" || request.Ordinal != 0 ||
            request.OwnerId != expectedOwnerId)
            throw new InvalidDataException(
                "Pael's Legion skin request differs from its saved acquisition slot.");

        return QueryPaelsLegionSkin(expectedOwnerId, ordinal: 0);
    }

    private static ChanceLawV111
        QueryControlledEventRelicAcquisitionModel(
            RunState source,
            PrototypeControlledEventChoiceContinuationV111 continuation,
            ControlledEventChoicePendingV111 pending)
    {
        var request = pending.Request;
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Relic-acquisition law is outside its saved event boundary.");
        var nativeRequest = request.StreamId ==
            NativePlayerRngV111.RewardsStreamId;
        var legacyStream = request.SiteId switch
        {
            "relic.acquisition.random_rare_card.model" => "reward",
            "relic.acquisition.random_potion.model" => "event",
            _ => string.Empty
        };
        if (nativeRequest != (source.Stochastic.NativeRunRng is not null) ||
            (!nativeRequest && request.StreamId != legacyStream) ||
            request.OperationKind != "choose_index")
            throw new InvalidDataException(
                "Relic-acquisition law differs from its saved Native or legacy index route.");

        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate => candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Relic-acquisition law references an unknown event choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Relic-acquisition law is outside its saved effect boundary.");
        var eventEffect = choice.Effects[continuation.NextEffectIndex];
        if (eventEffect.Kind != PrototypeRunEffectKind.GainRelic ||
            string.IsNullOrWhiteSpace(eventEffect.RelicId))
            throw new InvalidDataException(
                "Relic-acquisition request is outside a saved GainRelic event effect.");

        var relic = PrototypeContent.Relic(eventEffect.RelicId);
        var acquisitionEffects = (relic.RunTriggers ?? [])
            .Where(trigger => trigger.EventKind ==
                    PrototypeRunEventKind.RelicAcquired &&
                trigger.RequiredCardType is null &&
                trigger.RouteCondition is null)
            .SelectMany(trigger => trigger.Effects)
            .ToArray();
        string[] orderedPool;
        var expectedOwnerId = string.Empty;
        int? potionSlot = null;
        if (request.SiteId ==
                "relic.acquisition.random_rare_card.model")
        {
            if (eventEffect.RelicId != PrototypeNativeOvergrowthEvents
                    .NeowRelicId("ArcaneScroll") ||
                acquisitionEffects.Length != 1 ||
                acquisitionEffects[0].Kind !=
                    PrototypeRunEffectKind.GainRandomRareCard)
                throw new InvalidDataException(
                    "Rare-card request is not produced by the acquired relic's saved trigger.");
            orderedPool = PrototypeContent.RewardCardPool
                .Where(cardId => PrototypeContent.Card(cardId).Rarity ==
                    PrototypeCardRarity.Rare)
                .ToArray();
            expectedOwnerId = "player:rare_pool:" +
                CanonicalJson.Sha256(orderedPool);
        }
        else if (request.SiteId ==
            "relic.acquisition.random_potion.model")
        {
            if (eventEffect.RelicId != PrototypeNativeOvergrowthEvents
                    .NeowRelicId("PhialHolster") ||
                acquisitionEffects.Length != 3 ||
                acquisitionEffects[0].Kind !=
                    PrototypeRunEffectKind.GainPotionSlots ||
                acquisitionEffects[0].Amount != 1 ||
                acquisitionEffects[1].Kind !=
                    PrototypeRunEffectKind.GainRandomPotion ||
                acquisitionEffects[2].Kind !=
                    PrototypeRunEffectKind.GainRandomPotion)
                throw new InvalidDataException(
                    "Potion request is not produced by the acquired relic's saved trigger.");
            orderedPool = PrototypeContent.PotionPool;
            if (!PrototypeGameEngine.CanAcquirePotion(source.Player) ||
                relic.PreventPotionAcquisition)
                throw new InvalidDataException(
                    "Potion-acquisition trigger is disabled by the saved relic support.");

            var slots = source.Player.PotionSlots.ToList();
            var priorPotionDecisions = continuation.Decisions
                .Select((decision, index) => (decision, index))
                .Skip(continuation.EffectDecisionStartIndex)
                .Where(item => item.decision.Request.SiteId == request.SiteId)
                .ToArray();
            var labels = continuation.DecisionCandidateLabels ??
                throw new InvalidDataException(
                    "Potion-acquisition continuation has no saved candidate labels.");
            if (labels.Length != continuation.Decisions.Length)
                throw new InvalidDataException(
                    "Potion-acquisition continuation has a malformed candidate-label prefix.");

            var potionDecisionIndex = 0;
            foreach (var acquisitionEffect in acquisitionEffects)
            {
                if (acquisitionEffect.Kind ==
                    PrototypeRunEffectKind.GainPotionSlots)
                {
                    if (acquisitionEffect.Amount <= 0)
                        throw new InvalidDataException(
                        "Potion-acquisition trigger has an invalid slot increase.");
                    slots.AddRange(Enumerable.Repeat<PotionInstance?>(
                        null, acquisitionEffect.Amount));
                    continue;
                }

                if (acquisitionEffect.Kind !=
                    PrototypeRunEffectKind.GainRandomPotion)
                    continue;

                var slotIndex = slots.FindIndex(potion => potion is null);
                if (slotIndex < 0)
                    throw new InvalidDataException(
                        "Potion-acquisition trigger has no open slot for its saved request.");
                const string ownerPrefix = "player:potion_slot:";
                const string poolMarker = ":pool:";
                var slotOwner = ownerPrefix + slotIndex.ToString(
                        CultureInfo.InvariantCulture) + poolMarker +
                    CanonicalJson.Sha256(orderedPool);
                if (potionDecisionIndex < priorPotionDecisions.Length)
                {
                    var prior = priorPotionDecisions[potionDecisionIndex++];
                    var priorRequest = prior.decision.Request;
                    var expectedCandidateLabels = Enumerable.Range(0,
                            orderedPool.Length).Select(index =>
                                nativeRequest
                                    ? index.ToString(
                                        CultureInfo.InvariantCulture)
                                    : orderedPool[index]).ToArray();
                    if (priorRequest.SiteId != request.SiteId ||
                        priorRequest.StreamId != request.StreamId ||
                        priorRequest.OperationKind != "choose_index" ||
                        priorRequest.OwnerId != slotOwner ||
                        priorRequest.SupportCount != orderedPool.Length ||
                        !priorRequest.OrderedCandidates.SequenceEqual(
                            Enumerable.Range(0, orderedPool.Length)) ||
                        !labels[prior.index].SequenceEqual(
                            expectedCandidateLabels,
                            StringComparer.Ordinal) ||
                        (uint)prior.decision.SelectedIndex >=
                            (uint)orderedPool.Length)
                        throw new InvalidDataException(
                            "Potion-acquisition prefix differs from its saved ordered slot and pool.");
                    slots[slotIndex] = new PotionInstance(
                        orderedPool[prior.decision.SelectedIndex],
                        PrototypeJson.EmptyObject());
                    continue;
                }

                expectedOwnerId = slotOwner;
                potionSlot = slotIndex;
                break;
            }
            if (potionDecisionIndex != priorPotionDecisions.Length ||
                potionSlot is null)
                throw new InvalidDataException(
                    "Potion-acquisition request is outside the saved relic trigger prefix.");
        }
        else
        {
            throw new InvalidDataException(
                "Relic-acquisition law has an unsupported SiteId.");
        }

        if (orderedPool.Length == 0 || request.OwnerId != expectedOwnerId ||
            request.SupportCount != orderedPool.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, orderedPool.Length)))
            throw new InvalidDataException(
                "Relic-acquisition request differs from its saved ordered model pool.");

        var indexLaw = nativeRequest
            ? QueryRandomIndex(pending.ChanceEventId, request)
            : request.SiteId switch
            {
                "relic.acquisition.random_rare_card.model" =>
                    QueryLegacyRelicAcquisitionRareCardModel(
                        request.Ordinal),
                "relic.acquisition.random_potion.model" =>
                    QueryLegacyRelicAcquisitionPotionModel(
                        potionSlot!.Value, request.Ordinal),
                _ => throw new InvalidDataException(
                    "Legacy relic-acquisition law has an unsupported SiteId.")
            };
        var outcomePrefix = request.SiteId ==
                "relic.acquisition.random_rare_card.model"
            ? "card:" : "potion:";
        var outcomes = orderedPool.Select((modelId, index) =>
            new ChanceLawOutcomeV111(
                outcomePrefix + modelId, indexLaw.OutcomeWeightAt(index)))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            sourceLawId = indexLaw.LawId,
            sourceConditioningFingerprint =
                indexLaw.ConditioningFingerprint,
            eventId = continuation.EventId,
            choiceId = continuation.ChoiceId,
            effectIndex = continuation.NextEffectIndex,
            relicId = eventEffect.RelicId,
            siteId = request.SiteId,
            potionSlot,
            orderedPool
        });
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "controlled-event-native-relic-acquisition-model",
            indexLaw.LawId,
            continuation.EventId,
            continuation.ChoiceId,
            continuation.NextEffectIndex,
            relicId = eventEffect.RelicId,
            potionSlot,
            orderedPool
        });
        return Create(
            eventId, indexLaw.SiteId, indexLaw.OperationKind,
            indexLaw.OwnerId, indexLaw.Ordinal,
            indexLaw.ConditioningPolicyId + "-semantic-relic-model-v1",
            conditioningFingerprint, indexLaw.ProbabilityProvenance,
            outcomes, indexLaw.ProbabilityStatus);
    }

    private static ChanceLawV111 QueryControlledEventDistinguishedCapeCurse(
        RunState source,
        PrototypeControlledEventChoiceContinuationV111 continuation,
        ControlledEventChoicePendingV111 pending)
    {
        var request = pending.Request;
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Distinguished Cape curse law is outside its saved event boundary.");

        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate => candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Distinguished Cape curse law references an unknown event choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Distinguished Cape curse law is outside its saved effect boundary.");
        var effect = choice.Effects[continuation.NextEffectIndex];
        if (effect.Kind != PrototypeRunEffectKind.GainRelic ||
            effect.RelicId != NativeGloryAncientsV111.DistinguishedCapeId)
            throw new InvalidDataException(
                "Distinguished Cape curse request differs from its saved relic-acquisition effect.");

        const string expectedOwnerId =
            "relic:distinguished_cape:acquisition";
        var expectedStreamId = source.Stochastic.NativeRunRng is null
            ? "event" : "run.niche";
        if (request.SiteId !=
                NativeGloryAncientsV111.DistinguishedCapeCurseSiteId ||
            request.OwnerId != expectedOwnerId ||
            request.StreamId != expectedStreamId ||
            request.OperationKind != "choose_index" || request.Ordinal != 0 ||
            request.OrderedCandidates.Length != request.SupportCount)
            throw new InvalidDataException(
                "Distinguished Cape curse request differs from its saved acquisition owner.");

        var remainingCurseIds = NativeGloryAncientsV111
            .ModifierGeneratedCurseIds.ToList();
        if (remainingCurseIds.Count < 2 ||
            remainingCurseIds.Any(string.IsNullOrWhiteSpace) ||
            remainingCurseIds.Distinct(StringComparer.Ordinal).Count() !=
                remainingCurseIds.Count)
            throw new InvalidDataException(
                "Distinguished Cape has an invalid ordered curse source pool.");

        var labels = continuation.DecisionCandidateLabels ??
            throw new InvalidDataException(
                "Distinguished Cape continuation has no saved candidate labels.");
        if (labels.Length != continuation.Decisions.Length)
            throw new InvalidDataException(
                "Distinguished Cape continuation has a malformed candidate-label prefix.");
        for (var index = continuation.EffectDecisionStartIndex;
             index < continuation.Decisions.Length; index++)
        {
            var decision = continuation.Decisions[index];
            decision.Validate();
            var priorRequest = decision.Request;
            if (priorRequest.SiteId != request.SiteId ||
                priorRequest.OwnerId != request.OwnerId)
                continue;

            var priorSupport = remainingCurseIds.ToArray();
            if (priorRequest.StreamId != request.StreamId ||
                priorRequest.OperationKind != "choose_index" ||
                priorRequest.Ordinal != 0 ||
                !priorRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, priorSupport.Length)) ||
                !ControlledEventLabelsMatch(
                    request.SiteId, labels[index], priorSupport,
                    boundedIndexSupport: false))
                throw new InvalidDataException(
                    "Distinguished Cape curse prefix differs from its shrinking ordered support.");

            var selectedIndex = decision.SelectedIndex;
            if ((uint)selectedIndex >= (uint)priorSupport.Length)
                throw new InvalidDataException(
                    "Distinguished Cape curse prefix has an invalid selected index.");
            remainingCurseIds.RemoveAt(selectedIndex);
        }

        var currentSupport = remainingCurseIds.ToArray();
        if (currentSupport.Length < 1 ||
            request.SupportCount != currentSupport.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, currentSupport.Length)) ||
            !ControlledEventLabelsMatch(
                request.SiteId, pending.CandidateLabels, currentSupport,
                boundedIndexSupport: false))
            throw new InvalidDataException(
                "Distinguished Cape curse request differs from its saved shrinking support.");

        var indexLaw = QueryRandomIndex(pending.ChanceEventId, request);
        var outcomes = currentSupport.Select((curseId, index) =>
            new ChanceLawOutcomeV111(
                "card:" + curseId, indexLaw.OutcomeWeightAt(index)))
            .ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            sourceLawId = indexLaw.LawId,
            sourceConditioningFingerprint =
                indexLaw.ConditioningFingerprint,
            eventId = continuation.EventId,
            choiceId = continuation.ChoiceId,
            effectIndex = continuation.NextEffectIndex,
            orderedRemainingCurseIds = currentSupport
        });
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "controlled-event-distinguished-cape-curse",
            indexLaw.LawId,
            continuation.EventId,
            continuation.ChoiceId,
            continuation.NextEffectIndex,
            orderedRemainingCurseIds = currentSupport
        });
        return Create(
            eventId, indexLaw.SiteId, indexLaw.OperationKind,
            indexLaw.OwnerId, indexLaw.Ordinal,
            indexLaw.ConditioningPolicyId + "-semantic-curse-v1",
            conditioningFingerprint, indexLaw.ProbabilityProvenance,
            outcomes, indexLaw.ProbabilityStatus);
    }

    private static ChanceLawV111? QueryControlledEventPoolShuffle(
        RunState source,
        PrototypeControlledEventChoiceContinuationV111 continuation,
        ControlledEventChoicePendingV111 pending)
    {
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Controlled event pool-shuffle law is outside its saved event boundary.");

        var effectKind = ControlledEventChoiceEffectKind(continuation);
        var request = pending.Request;
        string[] orderedPool;
        string outcomePrefix;
        if (request.SiteId ==
            StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId)
        {
            int takeCount;
            switch (effectKind)
            {
                case PrototypeRunEffectKind.OfferThreeRareCards:
                    orderedPool = PrototypeContent.NativeSilentRewardCardPool
                        .Where(id => PrototypeContent.Card(id).Rarity ==
                            PrototypeCardRarity.Rare).ToArray();
                    takeCount = 3;
                    outcomePrefix = "card:";
                    break;
                case PrototypeRunEffectKind.OfferThreeColorlessCards:
                    orderedPool = PrototypeColorlessCards
                        .ImplementedCombatGenerationPool;
                    takeCount = 3;
                    outcomePrefix = "card:";
                    break;
                case PrototypeRunEffectKind.OfferTwoColorlessCards:
                {
                    var profile = source.Configuration?.Profile ??
                        throw new InvalidDataException(
                            "Controlled two-Colorless event offer has no saved run profile.");
                    orderedPool = PrototypeColorlessCards.RewardPoolForProfile(
                        profile.FullyUnlocked,
                        profile.UnlockedColorlessEpochs);
                    takeCount = 2;
                    outcomePrefix = "card:";
                    break;
                }
                case PrototypeRunEffectKind.OfferNeowsBonesRelics:
                    orderedPool = PrototypeNativeOvergrowthEvents
                        .NeowBonesSupportedRelicNames
                        .Select(PrototypeNativeOvergrowthEvents.NeowRelicId)
                        .Where(id => !source.Player.Relics.Any(relic =>
                            StringComparer.Ordinal.Equals(
                                relic.RelicId, id)))
                        .ToArray();
                    takeCount = 2;
                    outcomePrefix = "relic:";
                    break;
                default:
                    return null;
            }

            var requiredPoolLength = effectKind ==
                PrototypeRunEffectKind.OfferThreeRareCards
                    ? 2 : takeCount;
            if (orderedPool.Length < requiredPoolLength ||
                orderedPool.Any(string.IsNullOrWhiteSpace) ||
                orderedPool.Distinct(StringComparer.Ordinal).Count() !=
                    orderedPool.Length)
                throw new InvalidDataException(
                    "Controlled event pool-shuffle source pool is not a valid ordered set.");
            var expectedOwnerId = PrototypeGameEngine
                .DistinctPoolShuffleOwnerId("reward", takeCount, orderedPool);
            if (request.StreamId != "reward" ||
                request.OwnerId != expectedOwnerId)
                throw new InvalidDataException(
                    "Controlled event pool-shuffle request differs from its saved effect and source pool.");
        }
        else if (request.SiteId ==
            StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId)
        {
            if (effectKind != PrototypeRunEffectKind.OfferCardBundles)
                return null;

            var prefix = "Event:" + continuation.EventId + ":";
            if (request.OwnerId == prefix + "common_bundle")
            {
                orderedPool = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity ==
                        PrototypeCardRarity.Common).ToArray();
                if (orderedPool.Length < 4)
                    throw new InvalidDataException(
                        "Controlled event Common-card bundle pool is too small.");
            }
            else if (request.OwnerId == prefix + "uncommon_bundle")
            {
                orderedPool = PrototypeContent.NativeSilentRewardCardPool
                    .Where(id => PrototypeContent.Card(id).Rarity ==
                        PrototypeCardRarity.Uncommon).ToArray();
                if (orderedPool.Length < 2)
                    throw new InvalidDataException(
                        "Controlled event Uncommon-card bundle pool is too small.");
            }
            else
            {
                throw new InvalidDataException(
                    "Controlled event card-bundle shuffle has an unexpected pool owner.");
            }
            if (request.StreamId != "reward" || orderedPool.Length < 2 ||
                orderedPool.Any(string.IsNullOrWhiteSpace) ||
                orderedPool.Distinct(StringComparer.Ordinal).Count() !=
                    orderedPool.Length)
                throw new InvalidDataException(
                    "Controlled event card-bundle source pool is not valid.");
            outcomePrefix = "card:";
        }
        else
        {
            return null;
        }

        var values = (string[])orderedPool.Clone();
        var swapCount = 0;
        var decisionCandidateLabels =
            continuation.DecisionCandidateLabels;
        if (decisionCandidateLabels is not null &&
            decisionCandidateLabels.Length != continuation.Decisions.Length)
            throw new InvalidDataException(
                "Controlled event pool-shuffle label prefix has the wrong length.");

        for (var index = 0; index < continuation.Decisions.Length; index++)
        {
            var decision = continuation.Decisions[index];
            decision.Validate();
            var priorRequest = decision.Request;
            if (priorRequest.SiteId != request.SiteId ||
                priorRequest.OwnerId != request.OwnerId)
                continue;

            var expectedBound = values.Length - swapCount;
            if (expectedBound < 2 ||
                priorRequest.StreamId != "reward" ||
                priorRequest.OperationKind != "bounded_integer" ||
                priorRequest.BoundedCount != expectedBound ||
                priorRequest.OrderedCandidates.Length != 0)
                throw new InvalidDataException(
                    "Controlled event pool-shuffle prefix differs from the descending source shuffle.");

            var priorSupport = values.Take(expectedBound).ToArray();
            if (decisionCandidateLabels is { } allLabels &&
                !ControlledEventLabelsMatch(
                    request.SiteId, allLabels[index], priorSupport,
                    boundedIndexSupport: true))
                throw new InvalidDataException(
                    "Controlled event pool-shuffle prefix has changed its ordered candidate labels.");

            var selectedIndex = decision.SelectedIndex;
            if ((uint)selectedIndex >= (uint)expectedBound)
                throw new InvalidDataException(
                    "Controlled event pool-shuffle prefix has an invalid selected index.");
            (values[expectedBound - 1], values[selectedIndex]) =
                (values[selectedIndex], values[expectedBound - 1]);
            swapCount++;
        }

        var currentBound = values.Length - swapCount;
        if (currentBound < 2 ||
            request.OperationKind != "bounded_integer" ||
            request.BoundedCount != currentBound ||
            request.OrderedCandidates.Length != 0)
            throw new InvalidDataException(
                "Controlled event pool-shuffle request differs from its saved swap prefix.");
        var currentSupport = values.Take(currentBound).ToArray();
        if (!ControlledEventLabelsMatch(
                request.SiteId, pending.CandidateLabels, currentSupport,
                boundedIndexSupport: true))
            throw new InvalidDataException(
                "Controlled event pool-shuffle request has changed its ordered candidate support.");

        var indexLaw = QueryRandomIndex(pending.ChanceEventId, request);
        var outcomes = currentSupport.Select((candidateId, index) =>
            new ChanceLawOutcomeV111(
                outcomePrefix + candidateId,
                indexLaw.OutcomeWeightAt(index))).ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            sourceLawId = indexLaw.LawId,
            sourceConditioningFingerprint =
                indexLaw.ConditioningFingerprint,
            eventId = continuation.EventId,
            choiceId = continuation.ChoiceId,
            effectIndex = continuation.NextEffectIndex,
            orderedCandidateIds = currentSupport
        });
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "controlled-event-pool-shuffle",
            indexLaw.LawId,
            continuation.EventId,
            continuation.ChoiceId,
            continuation.NextEffectIndex,
            orderedCandidateIds = currentSupport
        });
        return Create(
            eventId, indexLaw.SiteId, indexLaw.OperationKind,
            indexLaw.OwnerId, indexLaw.Ordinal,
            indexLaw.ConditioningPolicyId + "-semantic-pool-outcome-v1",
            conditioningFingerprint, indexLaw.ProbabilityProvenance,
            outcomes, indexLaw.ProbabilityStatus);
    }

    private static bool ControlledEventLabelsMatch(
        string siteId, string[] candidateLabels,
        string[] semanticCandidateIds, bool boundedIndexSupport)
    {
        if (candidateLabels.SequenceEqual(
                semanticCandidateIds, StringComparer.Ordinal))
            return true;
        if (!boundedIndexSupport || siteId is not (
                StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId or
                StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId))
            return false;
        var legacyIndexLabels = Enumerable.Range(
                0, semanticCandidateIds.Length)
            .Select(index => index.ToString(CultureInfo.InvariantCulture));
        return candidateLabels.SequenceEqual(
            legacyIndexLabels, StringComparer.Ordinal);
    }

    private static PrototypeRunEffectKind ControlledEventChoiceEffectKind(
        PrototypeControlledEventChoiceContinuationV111 continuation)
    {
        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate => candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Controlled event law references an unknown choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Controlled event law is outside its saved effect boundary.");
        return choice.Effects[continuation.NextEffectIndex].Kind;
    }

    private static ChanceLawV111? QueryControlledEventUnownedRelicSelection(
        RunState source,
        PrototypeControlledEventChoiceContinuationV111 continuation,
        RandomIndexRequestV111 request)
    {
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Controlled unowned-relic law is outside its saved event boundary.");

        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate => candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Controlled unowned-relic law references an unknown choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Controlled unowned-relic law is outside its saved effect boundary.");

        var effectKind = choice.Effects[continuation.NextEffectIndex].Kind;
        var availableRelicIds = PrototypeContent.RelicPool
            .Where(id => !source.Player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, id)))
            .ToArray();
        if (availableRelicIds.Length == 0)
            throw new InvalidDataException(
                "Controlled unowned-relic request has no eligible relic support.");

        string expectedOwnerId;
        string expectedStreamId;
        if (effectKind == PrototypeRunEffectKind.GainRandomRelic)
        {
            if (PrototypeGameEngine.UsesNativeRegionalSystems(world))
                throw new InvalidDataException(
                    "Native-regional random relics must use the relic grab bag, not the legacy unowned pool.");
            expectedStreamId = "event";
            expectedOwnerId = "event:" + continuation.EventId + ":choice:" +
                continuation.ChoiceId + ":pool:" +
                CanonicalJson.Sha256(availableRelicIds);
        }
        else if (effectKind == PrototypeRunEffectKind.OfferRandomRelic)
        {
            expectedStreamId = source.Stochastic.NativeRunRng is not null &&
                PrototypeGameEngine.UsesNativeRegionalSystems(world)
                    ? NativePlayerRngV111.RewardsStreamId : "reward";
            expectedOwnerId = "Event:" + continuation.EventId +
                ":unowned_relic_pool:" +
                CanonicalJson.Sha256(availableRelicIds);
        }
        else
        {
            return null;
        }

        if (request.StreamId != expectedStreamId ||
            request.OwnerId != expectedOwnerId)
            throw new InvalidDataException(
                "Controlled unowned-relic request differs from its saved event effect and pool.");

        return QueryUnownedRelicSelectionIndex(
            expectedStreamId, expectedOwnerId, availableRelicIds,
            request.Ordinal);
    }

    private static ChanceLawV111? QueryControlledLegacyEventCardTransform(
        RunState source,
        PrototypeControlledEventChoiceContinuationV111 continuation,
        RandomIndexRequestV111 request)
    {
        if (source.Phase != RunPhase.Event || source.World is not { } world ||
            world.Event?.EventId != continuation.EventId)
            throw new InvalidDataException(
                "Legacy event card-transform law is outside its saved event boundary.");

        var choice = PrototypeContent.Event(continuation.EventId).Choices
            .SingleOrDefault(candidate => candidate.Id == continuation.ChoiceId) ??
            throw new InvalidDataException(
                "Legacy event card-transform law references an unknown choice.");
        if ((uint)continuation.NextEffectIndex >=
                (uint)choice.Effects.Length)
            throw new InvalidDataException(
                "Legacy event card-transform law is outside its saved effect boundary.");
        var effect = choice.Effects[continuation.NextEffectIndex];
        if (effect.Kind != PrototypeRunEffectKind.TransformFirstCardOfId ||
            effect.CardId is null)
            return null;

        const string ownerPrefix = "card_instance:";
        const string originalMarker = ":original:";
        const string poolMarker = ":pool:";
        var owner = request.OwnerId;
        var originalOffset = owner.IndexOf(originalMarker,
            StringComparison.Ordinal);
        var poolSearchOffset = originalOffset + originalMarker.Length;
        var poolOffset = originalOffset < 0 ||
            poolSearchOffset > owner.Length ? -1 : owner.IndexOf(
                poolMarker, poolSearchOffset, StringComparison.Ordinal);
        if (!owner.StartsWith(ownerPrefix, StringComparison.Ordinal) ||
            originalOffset <= ownerPrefix.Length || poolOffset < 0 ||
            !long.TryParse(owner.AsSpan(ownerPrefix.Length,
                    originalOffset - ownerPrefix.Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out var cardInstanceId) ||
            cardInstanceId < 0)
            throw new InvalidDataException(
                "Legacy event card-transform request has an invalid owner.");

        var originalCardId = owner[(originalOffset + originalMarker.Length)..
            poolOffset];
        var card = source.Player.Deck.SingleOrDefault(candidate =>
            candidate.InstanceId == cardInstanceId);
        if (string.IsNullOrWhiteSpace(originalCardId) || card is null ||
            card.CardId != originalCardId || effect.CardId != originalCardId ||
            source.Player.Deck.FirstOrDefault(candidate =>
                candidate.CardId == effect.CardId)?.InstanceId != cardInstanceId)
            throw new InvalidDataException(
                "Legacy event card-transform request differs from its saved physical card and effect.");

        return QueryLegacyRewardCardTransform(
            cardInstanceId, originalCardId, request.Ordinal);
    }

    private static ChanceLawV111 QueryLegacyControlledEventFillPotionSlot(
        RunState source, RandomIndexRequestV111 request)
    {
        var slotIndex = ControlledPotionSlotIndex(request);
        var slots = source.Player.PotionSlots;
        var slotIsEmpty = (uint)slotIndex < (uint)slots.Length &&
            slots[slotIndex] is null;
        return QueryLegacyEventFillPotionSlot(
            slotIndex,
            PrototypeGameEngine.CanAcquirePotion(source.Player),
            slotIsEmpty,
            request.Ordinal);
    }

    private static ChanceLawV111 QueryControlledStoneOfAllTimeTicket(
        RunState run, RandomIndexRequestV111 request,
        string boundaryFingerprint)
    {
        var continuation = run.World?.Event?.Stochastic.ControlledChoice ??
            throw new InvalidOperationException(
                "Stone of All Time ticket law requires a pending controlled event choice.");
        continuation.Validate();
        var choiceId = continuation.ChoiceId;
        var expectedSiteId = choiceId switch
        {
            "push" => NativeStoneOfAllTimePushTicketSiteId,
            "lift" => NativeStoneOfAllTimeLiftTicketSiteId,
            _ => throw new InvalidOperationException(
                "Stone of All Time controlled choice has no ticket law.")
        };
        var partialState = continuation.PartialState;
        var partialWorld = partialState.World;
        var partialEvent = partialWorld?.Event ??
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket has no event state.");
        var nativeRoute = partialState.Stochastic.NativeRunRng is not null;
        var expectedStreamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId)
            : "event";
        if (continuation.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            request.SiteId != expectedSiteId ||
            request.StreamId != expectedStreamId ||
            request.OwnerId != $"event:stone_of_all_time:{choiceId}" ||
            (nativeRoute && request.Ordinal != 1) ||
            request.OperationKind != "bounded_integer" ||
            request.BoundedCount != 100)
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket request disagrees with its source boundary.");
        var nativeRouteMismatch = nativeRoute &&
            (partialWorld is not { Act: 2 } ||
             !PrototypeGameEngine.UsesNativeRegionalSystems(partialWorld!));
        var legacyRouteMismatch = !nativeRoute &&
            partialWorld is not { Act: 2 };
        var eventRngMismatch = nativeRoute
            ? partialEvent.Stochastic.NativeRng?.StreamId != expectedStreamId ||
                partialEvent.Stochastic.ProviderMode is not null
            : partialEvent.Stochastic.NativeRng is not null ||
                partialEvent.Stochastic.ProviderMode is not null;
        if (partialState.Phase != RunPhase.Event ||
            partialWorld is not { Act: 2 } ||
            partialEvent.EventId != PrototypeNativeStoneOfAllTime.EventId ||
            partialEvent.NativePageIndex != 0 ||
            eventRngMismatch ||
            nativeRouteMismatch || legacyRouteMismatch ||
            (choiceId == "lift" && partialEvent.ChosenChoiceId is not null) ||
            (choiceId == "push" && partialEvent.ChosenChoiceId != "push"))
            throw new InvalidOperationException(
                "Pending Stone of All Time ticket is outside its Native or legacy event-action boundary.");
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "reference_nature.controlled_event.stone_of_all_time.ticket",
            boundaryFingerprint,
            eventId = partialEvent.EventId,
            actionId = choiceId,
            providerRoute = nativeRoute ? "native" : "legacy",
            request
        });
        return CreateNativeBoundaryLaw(
            request.SiteId, "bounded_integer_no_effect", request.OwnerId,
            request.Ordinal,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectPolicyId
                : LegacyStoneOfAllTimeNoEffectPolicyId,
            conditioningFingerprint,
            nativeRoute
                ? NativeStoneOfAllTimeNoEffectProvenance
                : LegacyStoneOfAllTimeNoEffectProvenance,
            [new ChanceLawOutcomeV111("no_effect", 1L)],
            ChanceLawV111.SourceVerifiedStatus);
    }

    public static string ControlledEventChoiceBoundaryFingerprint(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return ControlledEventChoicePendingV111.Describe(run)
            .BoundaryFingerprint;
    }
}
