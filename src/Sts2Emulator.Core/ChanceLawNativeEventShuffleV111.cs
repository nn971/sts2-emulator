using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

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

    public static string[] NativeEventShufflePlanSiteIds =>
        StochasticSiteRegistryV111.NativeEventShufflePlanSiteIds;

    public static string[] NativeEventShuffleSessionSiteIds =>
        ReferenceNatureNativeEventShuffleSessionV111
            .NativeEventShuffleSessionSiteIds;

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
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            request.StreamId);
        var weights = nativeIndexLaw
            ? NativeNextIntGridWeights(request.BoundedCount!.Value)
            : Enumerable.Repeat(1L, request.BoundedCount!.Value).ToArray();
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
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified",
            outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
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
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            request.StreamId);
        var weights = nativeIndexLaw
            ? NativeNextIntGridWeights(potionPool.Length)
            : Enumerable.Repeat(1L, potionPool.Length).ToArray();
        var outcomes = potionPool.Select((potionId, index) =>
            new ChanceLawOutcomeV111(
                $"potion:{potionId}:index:{index}", weights[index])).ToArray();
        return Create(
            eventId, request.SiteId, request.OperationKind,
            request.OwnerId, request.Ordinal,
            NativeRepyChestOfferConditioningPolicyId, conditioning,
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified", outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }

    public static ChanceLawV111 QueryNativeEndlessConveyorDishEntry(
        RunState origin, string actionId, int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.NativeRunRng is null ||
            origin.Phase != RunPhase.Event ||
            origin.World is not { Event: { } evt, Combat: null } ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.Stochastic.NativeRng is not { } stream)
            throw new InvalidOperationException(
                "Endless Conveyor dish law requires a configured pre-draw Event boundary.");
        PrototypeStateInvariants.Validate(origin);

        var isEntryBoundary = evt.NativePageIndex == 0 &&
            evt.ChosenChoiceId is null &&
            evt.Stochastic.RandomOutcomes.NativeDishCount == 0 &&
            evt.Stochastic.RandomOutcomes.NativeDishId is null &&
            evt.Stochastic.RandomOutcomes.NativeLastDishId is null &&
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is null;
        var isGrabBoundary = evt.NativePageIndex == 1 &&
            evt.ChosenChoiceId == "grab" &&
            evt.Stochastic.RandomOutcomes.NativeDishCount > 0 &&
            evt.Stochastic.RandomOutcomes.NativeDishId is not null &&
            evt.Stochastic.RandomOutcomes.NativeDishId ==
                evt.Stochastic.RandomOutcomes.NativeLastDishId &&
            !evt.Stochastic.RandomOutcomes.NativeDishRollPending &&
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is null;
        var nextRoll = checked(
            evt.Stochastic.RandomOutcomes.NativeDishCount + 1);
        if ((!isEntryBoundary && !isGrabBoundary) ||
            isGrabBoundary && nextRoll % 5 == 0)
            throw new InvalidOperationException(
                "Endless Conveyor dish law requires a pre-draw weighted dish boundary.");

        var expectedStreamId = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeEndlessConveyor.NativeRngEventId)
            .StreamId;
        if (stream.StreamId != expectedStreamId)
            throw new InvalidOperationException(
                "Endless Conveyor dish law requires its declared Native event stream.");

        var requestOrdinal = ordinal ?? (isEntryBoundary ? 0 :
            throw new InvalidOperationException(
                "Endless Conveyor continuation law requires a declared request ordinal."));
        if (requestOrdinal < 0 || isEntryBoundary && requestOrdinal != 0)
            throw new InvalidOperationException(
                "Endless Conveyor dish request ordinal differs from its declared pre-draw boundary.");

        var candidates = PrototypeNativeEndlessConveyor
            .GetWeightedDishCandidates(origin, evt);
        if (candidates.Length == 0 || candidates.Any(candidate =>
                candidate.Weight <= 0))
            throw new InvalidOperationException(
                "Endless Conveyor has no positive weighted dish support.");
        var ownerId = $"dish:{nextRoll}:pool:" +
            CanonicalJson.Sha256(candidates);
        var request = new RandomUnitFloatRequestV111(
            PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
            stream.StreamId, ownerId, requestOrdinal);
        request.Validate();

        var gridWeights = ChanceLawEngineV111
            .NativeNextFloatWeightedTicketGridWeights(candidates
                .Select(candidate => candidate.Weight).ToArray());
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            actionId,
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            dishCount = evt.Stochastic.RandomOutcomes.NativeDishCount,
            lastDishId = evt.Stochastic.RandomOutcomes.NativeLastDishId,
            hp = origin.Player.Hp,
            maxHp = origin.Player.MaxHp,
            emptyPotionSlots = origin.Player.PotionSlots
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
            origin.Stochastic.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Event: { } evt, Combat: null } ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.RandomOutcomes.NativeDishCount < 1 || evt.Stochastic.RandomOutcomes.NativeDishId is null ||
            evt.Stochastic.RandomOutcomes.NativeDishId != evt.Stochastic.RandomOutcomes.NativeLastDishId ||
            evt.Stochastic.RandomOutcomes.NativeDishRollPending ||
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not null ||
            evt.Stochastic.ProviderMode is not (null or "scripted-v111") ||
            evt.Stochastic.ControlledChoice is not null ||
            origin.Stochastic.ControlledEventRewardChoice is not null)
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
        var observeBoundary = choice == "observe" && evt.Stochastic.RandomOutcomes.NativeDishCount == 1;
        var spicyGrabBoundary = choice == "grab" &&
            evt.Stochastic.RandomOutcomes.NativeDishId == "SPICY_SNAPPY" && origin.Player.Gold >= 40;
        if (!observeBoundary && !spicyGrabBoundary)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade law requires first-dish Observe or a Spicy Snappy Grab.");

        var candidates = PrototypeNativeEndlessConveyor
            .GetUpgradeCardCandidates(origin);
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade has no eligible card and performs no index draw.");
        var stream = evt.Stochastic.NativeRng
            ?? throw new InvalidOperationException(
                "Endless Conveyor upgrade requires its persisted Native event cursor.");
        var expectedStreamId = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeEndlessConveyor.NativeRngEventId)
            .StreamId;
        if (stream.StreamId != expectedStreamId)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade requires its declared Native event stream.");
        var requestOrdinal = ordinal ?? throw new InvalidOperationException(
            "Endless Conveyor upgrade law requires a declared request ordinal.");
        if (requestOrdinal < 0)
            throw new InvalidOperationException(
                "Endless Conveyor upgrade request ordinal cannot be negative.");

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
            dishCount = evt.Stochastic.RandomOutcomes.NativeDishCount,
            dishId = evt.Stochastic.RandomOutcomes.NativeDishId,
            providerMode = evt.Stochastic.ProviderMode,
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
            origin.Stochastic.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World is not { Event: { } evt, Combat: null } ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.RandomOutcomes.NativeDishCount < 1 || evt.Stochastic.RandomOutcomes.NativeDishId != "FRIED_EEL" ||
            evt.Stochastic.RandomOutcomes.NativeDishId != evt.Stochastic.RandomOutcomes.NativeLastDishId ||
            evt.Stochastic.RandomOutcomes.NativeDishRollPending ||
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not null ||
            evt.Stochastic.ProviderMode is not (null or "scripted-v111") ||
            evt.Stochastic.ControlledChoice is not null ||
            origin.Stochastic.ControlledEventRewardChoice is not null ||
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
            .GetColorlessCardCandidates(origin);
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Fried Eel has no supported Colorless card and performs no index draw.");
        var stream = evt.Stochastic.NativeRng
            ?? throw new InvalidOperationException(
                "Endless Conveyor Colorless draw requires its persisted Native event cursor.");
        var expectedStreamId = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeEndlessConveyor.NativeRngEventId)
            .StreamId;
        if (stream.StreamId != expectedStreamId)
            throw new InvalidOperationException(
                "Endless Conveyor Colorless draw requires its declared Native event stream.");
        var requestOrdinal = ordinal ?? throw new InvalidOperationException(
            "Endless Conveyor Colorless law requires a declared request ordinal.");
        if (requestOrdinal < 0)
            throw new InvalidOperationException(
                "Endless Conveyor Colorless request ordinal cannot be negative.");

        var ownerId = $"dish:{evt.Stochastic.RandomOutcomes.NativeDishCount}:pool:" +
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
            dishCount = evt.Stochastic.RandomOutcomes.NativeDishCount,
            dishId = evt.Stochastic.RandomOutcomes.NativeDishId,
            providerMode = evt.Stochastic.ProviderMode,
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


}
