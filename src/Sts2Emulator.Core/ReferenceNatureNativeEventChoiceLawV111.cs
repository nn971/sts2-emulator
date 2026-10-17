using System.Globalization;

namespace Sts2Emulator.Core;

internal sealed record NativeEventReflectionsChoicePlanV111(
    long[] CandidateCardInstanceIds,
    RandomIndexRequestV111 Request,
    string ConditioningFingerprint);

internal sealed record NativeEventInfestedAutomatonCardPickPlanV111(
    string[] CandidateCardIds,
    RandomIndexRequestV111 Request,
    string ConditioningFingerprint,
    string ChoiceId);

internal static class NativeEventInfestedAutomatonCardPickPlanBuilderV111
{
    public static NativeEventInfestedAutomatonCardPickPlanV111 Build(
        RunState origin, string siteId, int ordinal, string? actionId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        if (ordinal != 0 || string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Act: 2, Event: { } evt } ||
            evt.EventId != PrototypeNativeHiveEvents.InfestedAutomatonId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.ControlledChoice is not null ||
            evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not null ||
            origin.ControlledEventRewardChoice is not null ||
            actionId is not ("study" or "touch_core"))
            throw new InvalidOperationException(
                "Infested Automaton card-pick laws require a pending study or touch_core choice at ordinal 0.");
        if (siteId != PrototypeNativeHiveEvents
                .InfestedAutomatonCardOfferSiteId)
            throw new InvalidOperationException(
                $"Infested Automaton expects site '{PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId}'.");

        var action = GameAction.Create("event_choice",
            new EventChoicePayload(actionId));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                $"Infested Automaton choice '{actionId}' is not legal in the current state.");

        var candidates = PrototypeNativeHiveEvents
            .InfestedAutomatonCardCandidates(actionId);
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Infested Automaton has no card draw for this choice because its eligible pool is empty.");

        var expectedStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed,
            PrototypeNativeHiveEvents.InfestedAutomatonNativeRngEventId);
        var stream = evt.NativeEventRng ?? expectedStream;
        if (NativeRngV111.ToSnapshot(stream) !=
                NativeRngV111.ToSnapshot(expectedStream) ||
            (stream.CallCount ?? 0) != 0)
            throw new InvalidOperationException(
                "Infested Automaton has an advanced or incorrect Native event stream.");
        var ownerId = $"event:{evt.EventId}:choice:{actionId}:pool:" +
            CanonicalJson.Sha256(candidates);
        var request = new RandomIndexRequestV111(
            PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            stream.StreamId, ownerId, ordinal,
            Enumerable.Range(0, candidates.Length).ToArray());
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "hive.infested_automaton.card_offer",
            eventId = evt.EventId,
            actionId,
            candidateCardIds = candidates,
            request,
            policy = ChanceLawEngineV111
                .NativeEventCardPickConditioningPolicyId
        });
        return new(candidates, request, conditioningFingerprint, actionId);
    }
}

internal static class NativeEventReflectionsChoicePlanBuilderV111
{
    public static NativeEventReflectionsChoicePlanV111 BuildNext(
        RunState origin, string siteId, int ordinal, int[] priorIndices,
        string? actionId) => TryBuildNext(
            origin, siteId, ordinal, priorIndices, actionId) ??
        throw new InvalidOperationException(
            "Reflections has no pending card-pick draw at this ordinal.");

    public static NativeEventReflectionsChoicePlanV111? TryBuildNext(
        RunState origin, string? siteId, int ordinal, int[] priorIndices,
        string? actionId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (siteId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentNullException.ThrowIfNull(priorIndices);
        if (ordinal < 0 || priorIndices.Length != ordinal ||
            priorIndices.Any(index => index < 0))
            throw new InvalidOperationException(
                "Reflections Chance Law requires its complete nonnegative index prefix.");
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Act: 3, Event: { } evt } ||
            evt.EventId != PrototypeNativeGloryEvents.ReflectionsId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.ControlledChoice is not null ||
            evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not null ||
            origin.ControlledEventRewardChoice is not null ||
            actionId is not (null or "touch_mirror"))
            throw new InvalidOperationException(
                "Reflections card-pick laws require the pending configured touch_mirror action.");

        var action = GameAction.Create("event_choice",
            new EventChoicePayload("touch_mirror"));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Reflections touch_mirror is not legal in the current state.");

        var expectedStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeGloryEvents
                .ReflectionsNativeRngEventId);
        if (evt.NativeEventRng is { } savedStream &&
            NativeRngV111.ToSnapshot(savedStream) !=
                NativeRngV111.ToSnapshot(expectedStream))
            throw new InvalidOperationException(
                "Reflections has the wrong or advanced Native event stream context.");
        if (evt.NativeEventRng is null && evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Reflections cannot reconstruct a missing event stream after page progress.");

        var projectedDeck = origin.Player.Deck.ToList();
        var selectedCardIds = new List<long>();
        var globalOrdinal = 0;
        var downgradeCandidates = projectedDeck
            .Where(card => card.UpgradeLevel > 0)
            .Select(card => card.InstanceId)
            .ToList();
        for (var stageOrdinal = 0;
             stageOrdinal < 2 && downgradeCandidates.Count > 0;
             stageOrdinal++)
        {
            if (globalOrdinal == ordinal)
                return BuildPlan(
                    origin, siteId, ordinal, "downgrade", stageOrdinal,
                    downgradeCandidates, selectedCardIds);
            var selected = SelectPrior(
                priorIndices[globalOrdinal], downgradeCandidates,
                globalOrdinal);
            selectedCardIds.Add(selected);
            downgradeCandidates.RemoveAt(priorIndices[globalOrdinal]);
            UpdateUpgradeLevel(projectedDeck, selected, -1);
            globalOrdinal++;
        }

        var upgradeCandidates = EligibleUpgradeIds(projectedDeck);
        for (var stageOrdinal = 0;
             stageOrdinal < 4 && upgradeCandidates.Count > 0;
             stageOrdinal++)
        {
            if (globalOrdinal == ordinal)
                return BuildPlan(
                    origin, siteId, ordinal, "upgrade", stageOrdinal,
                    upgradeCandidates, selectedCardIds);
            var selected = SelectPrior(
                priorIndices[globalOrdinal], upgradeCandidates,
                globalOrdinal);
            selectedCardIds.Add(selected);
            upgradeCandidates.RemoveAt(priorIndices[globalOrdinal]);
            UpdateUpgradeLevel(projectedDeck, selected, +1);
            globalOrdinal++;
        }

        return null;
    }

    private static NativeEventReflectionsChoicePlanV111 BuildPlan(
        RunState origin, string? siteId, int ordinal, string stage,
        int stageOrdinal, List<long> candidates,
        List<long> selectedCardIds)
    {
        var expectedSiteId = stage == "downgrade"
            ? PrototypeNativeGloryEvents.ReflectionsDowngradeSiteId
            : PrototypeNativeGloryEvents.ReflectionsUpgradeSiteId;
        if (siteId is not null && siteId != expectedSiteId)
            throw new InvalidOperationException(
                $"Reflections expects site '{expectedSiteId}' at ordinal {ordinal}.");
        var evt = origin.World!.Event!;
        var ownerId = $"event:reflections:{stage}:{stageOrdinal}:pool:" +
            CanonicalJson.Sha256(candidates);
        var stream = evt.NativeEventRng ?? NativeRngV111Contexts.ForEvent(
            origin.RunSeed!, PrototypeNativeGloryEvents
                .ReflectionsNativeRngEventId);
        var request = new RandomIndexRequestV111(
            expectedSiteId, stream.StreamId, ownerId, ordinal,
            Enumerable.Range(0, candidates.Count).ToArray());
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "glory.reflections.touch_mirror.card_picks",
            eventId = evt.EventId,
            actionId = "touch_mirror",
            stage,
            stageOrdinal,
            selectedCardInstanceIds = selectedCardIds,
            candidateCardInstanceIds = candidates,
            request,
            policy = ChanceLawEngineV111.NativeEventCardPickConditioningPolicyId
        });
        return new(candidates.ToArray(), request,
            conditioningFingerprint);
    }

    private static long SelectPrior(
        int selectedIndex, List<long> candidates, int ordinal)
    {
        if ((uint)selectedIndex >= (uint)candidates.Count)
            throw new InvalidOperationException(
                $"Reflections prefix index at ordinal {ordinal} is outside its support.");
        return candidates[selectedIndex];
    }

    private static List<long> EligibleUpgradeIds(
        List<CardInstance> deck) => deck
        .Where(card => PrototypeGameEngine.CanSelectEventDeckCard(
            card, PrototypePersistentDeckChoiceKind.Upgrade,
            null, null, false))
        .Select(card => card.InstanceId)
        .ToList();

    private static void UpdateUpgradeLevel(
        List<CardInstance> deck, long instanceId, int delta)
    {
        var position = deck.FindIndex(card => card.InstanceId == instanceId);
        if (position < 0)
            throw new InvalidOperationException(
                "Reflections prefix selected a missing physical card instance.");
        var card = deck[position];
        deck[position] = card with
        {
            UpgradeLevel = checked(card.UpgradeLevel + delta)
        };
    }
}

public static partial class ChanceLawEngineV111
{
    public const string NativeEventCardPickConditioningPolicyId =
        "native-event-card-pick-prefix-v1";
    public const string NativeEventGoldIndexSiteId =
        "event.generated_gold.index_draw";
    public const string NativeEventGoldUnitFloatSiteId =
        "event.generated_gold.unit_float";

    public static string[] NativeEventGoldQuerySiteIds =>
        [NativeEventGoldIndexSiteId, NativeEventGoldUnitFloatSiteId];

    public static string[] NativeEventCardPickQuerySiteIds =>
    [
        PrototypeNativeGloryEvents.ReflectionsDowngradeSiteId,
        PrototypeNativeGloryEvents.ReflectionsUpgradeSiteId,
        PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId
    ];

    public static ChanceLawV111 QueryNativeEventGoldUnitFloat(
        RunState origin, string siteId, int ordinal, string actionId,
        string eventId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (siteId != NativeEventGoldUnitFloatSiteId || ordinal < 0 ||
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

        ValidateNativeEventGoldBranch(origin, world, eventId);
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
        if (siteId != NativeEventGoldIndexSiteId || ordinal < 0 ||
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

        ValidateNativeEventGoldBranch(origin, world, eventId);
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

    private static void ValidateNativeEventGoldBranch(
        RunState origin, RunWorldState world, string eventId)
    {
        if (!PrototypeContent.Events.TryGetValue(eventId, out var eventDefinition) ||
            world.Act < eventDefinition.MinAct ||
            world.Act > eventDefinition.MaxAct ||
            world.EventIds.Contains(eventId, StringComparer.Ordinal) &&
                eventDefinition.OncePerRun)
            throw new InvalidOperationException(
                "The declared event-gold branch is not available in this run state.");

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
                "The declared event-gold branch is outside the configured Native event route.");
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
            var infestedWeights = NativeNextIntGridWeights(
                infestedPlan.Request.SupportCount);
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
                NativeBoundedIndexSourceVerifiedProvenance,
                infestedOutcomes,
                ChanceLawV111.SourceVerifiedStatus);
        }
        var plan = NativeEventReflectionsChoicePlanBuilderV111.BuildNext(
            origin, siteId, ordinal, priorIndices, actionId);
        var weights = NativeNextIntGridWeights(plan.Request.SupportCount);
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
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }
}
