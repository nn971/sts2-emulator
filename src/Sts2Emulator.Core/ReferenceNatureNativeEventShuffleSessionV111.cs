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
    string ConditioningFingerprint)
{
    public int SwapCount => CandidateCount - 1;

    public RandomIndexRequestV111 Request(int ordinal)
    {
        if (ordinal < 0 || ordinal >= SwapCount)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        return new RandomIndexRequestV111(
            SiteId, StreamId, OwnerId, ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: CandidateCount - ordinal);
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
        var world = origin.World;
        var activeEvent = world?.Event;
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null ||
            world is null ||
            activeEvent is null && !fakeMerchantEntryAction ||
            activeEvent?.ControlledChoice is not null ||
            activeEvent?.NativeEventPotionSlot is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Native event shuffle Nature choices require an active native event or Fake Merchant entry.");
        var evt = activeEvent ?? new EventState(
            PrototypeNativeFakeMerchant.EventId);
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
            _ => throw new InvalidOperationException(
                "No native event shuffle is pending at this event page.")
        };
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);

        var battlewornVictoryAction =
            origin.Phase == RunPhase.Combat &&
            evt.EventId == PrototypeNativeGloryEvents.BattlewornDummyId &&
            evt.NativePageIndex == 2 &&
            evt.ChosenChoiceId == "setting_2";
        if (origin.Phase != RunPhase.Event && !battlewornVictoryAction &&
            !fakeMerchantEntryAction)
            throw new InvalidOperationException(
                "Native event shuffle Nature choices require a pending event action, a Battleworn victory action, or Fake Merchant entry.");
        if (origin.Phase == RunPhase.Event && evt.ChosenChoiceId is not null)
            throw new InvalidOperationException(
                "Native event shuffle Nature choices require an uncommitted event action.");

        string nativeRngEventId;
        string resolvedActionId;
        GameAction? replayAction = null;
        string[] orderedCandidateIds;
        int candidateCount;
        string ownerId;
        if (fakeMerchantEntryAction)
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

        if ((battlewornVictoryAction || fakeMerchantEntryAction) &&
            plannedCandidateIds is null)
        {
            RunState projected;
            NativeEventShuffleCaptureV111[] captures;
            using (var shuffleProbe = NativeEventShuffleProbeV111.Begin())
            using (RandomBoundaryJournalV111.Begin())
            using (NativeEngineEventJournalV111.Begin())
            {
                projected = new PrototypeGameEngine().Step(
                    origin.Fork(), replayAction).State;
                captures = shuffleProbe.Captures.ToArray();
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

        if (candidateCount <= 1)
            throw new InvalidOperationException(
                "Native event shuffle has fewer than two candidates and performs no draw.");

        var fingerprint = CanonicalJson.Sha256(new
        {
            boundary = "native_event.fisher_yates_shuffle",
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            actionId = resolvedActionId,
            offeredChoiceIds = evt.OfferedChoiceIds,
            selectedTinkerType = evt.NativeTinkerType,
            orderedCandidateIds,
            stream = NativeRngV111.ToSnapshot(stream),
            siteId,
            ownerId,
            policy = ChanceLawEngineV111.NativeEventShuffleConditioningPolicyId
        });
        return new(evt.EventId, nativeRngEventId, siteId,
            resolvedActionId, replayAction, orderedCandidateIds, candidateCount,
            stream.StreamId, ownerId, fingerprint);
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

/// <summary>Atomic ReferenceNature replay of a complete native event-local
/// Fisher-Yates shuffle through an admitted event action.</summary>
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

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(new
    {
        session = this,
        plannedCandidateIds = PlannedCandidateIds
    });

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length ==
        NativeEventShufflePlanBuilderV111.Build(
            Origin, SiteId, ActionId, PlannedCandidateIds).SwapCount;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNativeEventShuffle(
            Origin, SiteId, NextOrdinal,
            DecisionTranscript.Select(decision => decision.OutcomeIndex)
                .ToArray(), ActionId, PlannedCandidateIds);

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
        var priorIndices = DecisionTranscript.Select(
            decision => decision.OutcomeIndex).ToArray();
        var law = ChanceLawEngineV111.QueryNativeEventShuffle(
                Origin, SiteId, NextOrdinal, priorIndices, ActionId,
                PlannedCandidateIds);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNature = selection.NextState;
        var nextRun = Run;
        if (transcript.Length == plan.SwapCount)
        {
            var decisions = transcript.Select((decision, ordinal) =>
                new RandomIndexDecisionV111(
                    plan.Request(ordinal), decision.OutcomeIndex,
                    "scripted-v111")).ToArray();
            var replay = ReferenceNatureNativeEventIndexReplayV111.Replay(
                Origin, decisions, plan.ReplayAction,
                plan.NativeRngEventId, plan.EventId,
                SelectiveReplaySite(plan), plan.OrderedCandidateIds);
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
        if (DecisionTranscript.Length > plan.SwapCount ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
        {
            var law = ChanceLawEngineV111.QueryNativeEventShuffle(
                Origin, SiteId, ordinal, priorIndices.ToArray(), ActionId,
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
                    plan.Request(ordinal), decision.OutcomeIndex,
                    "scripted-v111")).ToArray();
            expectedRun = ReferenceNatureNativeEventIndexReplayV111.Replay(
                Origin, decisions, plan.ReplayAction,
                plan.NativeRngEventId, plan.EventId,
                SelectiveReplaySite(plan), plan.OrderedCandidateIds);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNatureState);
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle replay differs from its session envelope.");
    }

    private static string? SelectiveReplaySite(
        NativeEventShufflePlanV111 plan) =>
        plan.SiteId is PrototypeNativeGloryEvents
            .BattlewornUpgradeShuffleSiteId or
            PrototypeNativeFakeMerchant.RelicOfferShuffleSiteId
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

    public static string[] NativeEventShuffleSessionSiteIds =>
    [
        PrototypeNativeUnderdocksDoors.ShuffleSiteId,
        PrototypeNativeGloryEvents.HungryForMushroomsShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerTypeOfferShuffleSiteId,
        PrototypeNativeGloryEvents.TinkerRiderOfferShuffleSiteId,
        PrototypeNativeGloryEvents.BattlewornUpgradeShuffleSiteId,
        PrototypeNativeFakeMerchant.RelicOfferShuffleSiteId
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
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            plan.ConditioningFingerprint,
            priorIndices,
            request
        });
        var outcomes = Enumerable.Range(0, request.BoundedCount!.Value)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), 1L))
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
            "uniform-bounded-event-shuffle-index-unverified", outcomes);
    }
}
