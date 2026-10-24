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
        var world = origin.World;
        if (ordinal != 0 || origin.Phase != RunPhase.Event ||
            world is not { Act: 2, Event: { } evt } ||
            evt.EventId != PrototypeNativeHiveEvents.InfestedAutomatonId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.ControlledChoice is not null ||
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not null ||
            evt.Stochastic.ProviderMode is not null ||
            origin.Stochastic.ControlledEventRewardChoice is not null ||
            actionId is not ("study" or "touch_core"))
            throw new InvalidOperationException(
                "Infested Automaton card-pick laws require a supported pending study or touch_core choice at ordinal 0.");

        var nativeRoute = origin.Stochastic.NativeRunRng is not null;
        var legacyFallbackRoute = origin.Stochastic.NativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.Stochastic.NativeRng is null;
        if (!nativeRoute && !legacyFallbackRoute)
            throw new InvalidOperationException(
                "Infested Automaton requires a configured Native or Native-regional legacy event RNG route.");
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

        var expectedStreamId = NativeRngV111Contexts.EventStreamId(
            PrototypeNativeHiveEvents.InfestedAutomatonNativeRngEventId);
        if (evt.Stochastic.NativeRng is { } savedStream &&
            savedStream.StreamId != expectedStreamId)
            throw new InvalidOperationException(
                "Infested Automaton has an incorrect Native event stream identity.");
        var ownerId = $"event:{evt.EventId}:choice:{actionId}:pool:" +
            CanonicalJson.Sha256(candidates);
        var streamId = nativeRoute
            ? expectedStreamId
            : "event";
        var requestOrdinal = nativeRoute
            ? ordinal
            : GetLegacyEventOrdinal(origin);
        var request = new RandomIndexRequestV111(
            PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            streamId, ownerId, requestOrdinal,
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

    private static int GetLegacyEventOrdinal(RunState origin)
    {
        if (!origin.Stochastic.PrototypeRng.TryGetStream("event", out var stream))
            throw new InvalidDataException(
                "Infested Automaton fallback requires one legacy event stream.");
        var ordinal = stream.CallCount ?? 0;
        if (ordinal > int.MaxValue)
            throw new NotSupportedException(
                "Infested Automaton legacy event ordinal exceeds the typed request range.");
        return (int)ordinal;
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
        var world = origin.World;
        if (origin.Phase != RunPhase.Event ||
            world is not { Act: 3, Event: { } evt } ||
            evt.EventId != PrototypeNativeGloryEvents.ReflectionsId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.ControlledChoice is not null ||
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not null ||
            evt.Stochastic.ProviderMode is not null ||
            origin.Stochastic.ControlledEventRewardChoice is not null ||
            actionId is not (null or "touch_mirror"))
            throw new InvalidOperationException(
                "Reflections card-pick laws require a supported pending touch_mirror action.");

        var nativeRoute = origin.Stochastic.NativeRunRng is not null;
        var legacyFallbackRoute = origin.Stochastic.NativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.Stochastic.NativeRng is null;
        if (!nativeRoute && !legacyFallbackRoute)
            throw new InvalidOperationException(
                "Reflections requires a configured Native or Native-regional legacy event RNG route.");

        var action = GameAction.Create("event_choice",
            new EventChoicePayload("touch_mirror"));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Reflections touch_mirror is not legal in the current state.");

        var expectedStreamId = NativeRngV111Contexts.EventStreamId(
            PrototypeNativeGloryEvents.ReflectionsNativeRngEventId);
        if (evt.Stochastic.NativeRng is { } savedStream &&
            savedStream.StreamId != expectedStreamId)
            throw new InvalidOperationException(
                "Reflections has the wrong Native event stream identity.");
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
        var nativeRoute = origin.Stochastic.NativeRunRng is not null;
        var streamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeGloryEvents.ReflectionsNativeRngEventId)
            : "event";
        var requestOrdinal = nativeRoute
            ? ordinal
            : GetLegacyEventOrdinal(origin, ordinal);
        var request = new RandomIndexRequestV111(
            expectedSiteId, streamId, ownerId, requestOrdinal,
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

    private static int GetLegacyEventOrdinal(
        RunState origin, int drawOrdinal)
    {
        if (drawOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(drawOrdinal));
        if (!origin.Stochastic.PrototypeRng.TryGetStream("event", out var stream))
            throw new InvalidDataException(
                "Reflections fallback requires one legacy event stream.");
        var ordinal = stream.CallCount ?? 0;
        if (ordinal > (ulong)int.MaxValue - (ulong)drawOrdinal)
            throw new NotSupportedException(
                "Reflections legacy event ordinal exceeds the typed request range.");
        return (int)(ordinal + (ulong)drawOrdinal);
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
