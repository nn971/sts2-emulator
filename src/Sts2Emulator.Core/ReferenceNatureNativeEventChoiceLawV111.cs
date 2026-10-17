using System.Globalization;

namespace Sts2Emulator.Core;

internal sealed record NativeEventReflectionsChoicePlanV111(
    long[] CandidateCardInstanceIds,
    RandomIndexRequestV111 Request,
    string ConditioningFingerprint);

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

    public static string[] NativeEventCardPickQuerySiteIds =>
    [
        PrototypeNativeGloryEvents.ReflectionsDowngradeSiteId,
        PrototypeNativeGloryEvents.ReflectionsUpgradeSiteId
    ];

    public static ChanceLawV111 QueryNativeEventCardPick(
        RunState origin, string siteId, int ordinal,
        int[]? priorIndices = null, string? actionId = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        priorIndices ??= [];
        var plan = NativeEventReflectionsChoicePlanBuilderV111.BuildNext(
            origin, siteId, ordinal, priorIndices, actionId);
        var outcomes = plan.CandidateCardInstanceIds.Select(instanceId =>
            new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    CultureInfo.InvariantCulture), 1L)).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.card_pick",
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, NativeEventCardPickConditioningPolicyId,
            plan.ConditioningFingerprint,
            "uniform-without-replacement-event-card-unverified", outcomes);
    }
}
