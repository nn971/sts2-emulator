using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureNativeEventGoldSessionSnapshotV111(
    string SchemaId, string EventId, string ActionId,
    string OriginSnapshotJson, string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
}

/// <summary>
/// Atomic ReferenceNature replay for supported Native and Native-regional
/// legacy Event-node entry stochastic draws.
/// </summary>
public sealed record ReferenceNatureNativeEventGoldSessionV111(
    string SchemaId, string EventId, string ActionId,
    RunState Origin, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<PrototypeMechanicsContinuationStateV111,
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>>
        Simulation { get; init; } = new(
            new PrototypeMechanicsContinuationStateV111(
                PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession,
                Origin, Run),
            new ReferenceNatureStochasticStateV111<
                ReferenceNatureDecisionV111>(InitialNatureState, NatureState,
                DecisionTranscript));

    [JsonIgnore]
    public PrototypeMechanicsContinuationStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin!;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public RunState Run
    {
        get => Mechanics.Current;
        init => Mechanics = Mechanics with { Current = value };
    }

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    [JsonIgnore]
    public GameAction Action => new PrototypeGameEngine()
        .GetLegalActions(Origin)
        .SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == ActionId)
        ?? throw new InvalidOperationException(
            "Native event-gold action is no longer legal at its saved origin.");

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions =>
        CreateProviderPrefix();

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-native-event-gold-session-v1";
    private const string GoldUnitFloatSiteId =
        ChanceLawEngineV111.NativeEventGoldUnitFloatSiteId;
    private const string GoldIndexSiteId =
        ChanceLawEngineV111.NativeEventGoldIndexSiteId;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Stochastic.DecisionTranscript.Length ==
        DecisionCount(EventId, Origin);

    [JsonIgnore]
    public int NextOrdinal => Stochastic.DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var prefix = CreateProviderPrefix();
            var replay = Replay(Origin, ActionId, prefix);
            var request = replay.Continuation?.PendingRequest ??
                throw new InvalidDataException(
                    "Native event gold replay completed before its sampled prefix.");
            return QueryLawForCapture(Origin, ActionId, EventId, request);
        }
    }

    public static string[] SessionSiteIds =>
        [ChanceLawEngineV111.NativeEventGoldUnitFloatSiteId,
            ChanceLawEngineV111.NativeEventGoldIndexSiteId,
            ChanceLawEngineV111.NativeStoneOfAllTimePotionSlotSiteId,
            ChanceLawEngineV111.NativeFutureOfPotionsCardTypeSiteId];

    public static ReferenceNatureNativeEventGoldSessionV111 Begin(
        RunState source, string actionId, ulong natureSeed) => Begin(
            source, actionId, ReferenceNatureRunCursorV111.Begin(
                source, natureSeed, "Native event gold"));

    public static ReferenceNatureNativeEventGoldSessionV111 Begin(
        RunState source, string actionId) => Begin(
            source, actionId, ReferenceNatureRunCursorV111.Begin(
                source, null, "Native event gold"));

    private static ReferenceNatureNativeEventGoldSessionV111 Begin(
        RunState source, string actionId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, actionId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Native event gold");
        var origin = ReferenceNatureRunProjectionV111
            .CreateNatureReplayOrigin(source, allowMissingRelicBag: true);
        var replay = Replay(origin, actionId, []);
        var request = replay.Continuation?.PendingRequest ??
            throw new NotSupportedException(
                "Configured Event entry did not reach a supported event-entry draw.");
        var eventId = ParseSupportedEventId(request);
        _ = QueryLawForCapture(origin, actionId, eventId, request);
        var session = new ReferenceNatureNativeEventGoldSessionV111(
            CurrentSchemaId, eventId, actionId, origin.Fork(), origin.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureNativeEventGoldSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureNativeEventGoldSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Native event gold session is already complete.");

        var prefix = CreateProviderPrefix();
        var replayAtBoundary = Replay(Origin, ActionId, prefix);
        var pending = replayAtBoundary.Continuation ??
            throw new InvalidDataException(
                "Native event gold request disappeared before resolution.");
        var law = QueryLawForCapture(Origin, ActionId, EventId,
            pending.PendingRequest);
        var selection = ReferenceNatureV111.SampleValidated(law, Stochastic.NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, pending.PendingRequest, selection, Origin);
        var continuation = pending;
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = Resolve(Origin, ActionId, continuation,
            providerDecision);
        var next = this with
        {
            Run = replay.Run is { } completed
                ? ReferenceNatureRunCursorV111.Commit(
                    completed, selection.NextState)
                : Origin.Fork(),
            NatureState = selection.NextState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            !IsSupportedEventId(EventId) ||
            string.IsNullOrWhiteSpace(ActionId) || Origin is null || Run is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event gold session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        if (Stochastic.DecisionTranscript.Length > DecisionCount(EventId, Origin))
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event gold session envelope.");
        ValidateOpeningBoundary(Origin, ActionId);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native event-gold session origin", allowMissing: true);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native event-gold session replay", allowMissing: true);
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, Stochastic.InitialNatureState, "Native event gold");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        var providerPrefix = new List<ControlledStochasticDecisionV111>();
        for (var index = 0; index < Stochastic.DecisionTranscript.Length; index++)
        {
            var replayAtBoundary = Replay(
                Origin, ActionId, providerPrefix.ToArray());
            var request = replayAtBoundary.Continuation?.PendingRequest ??
                throw new InvalidDataException(
                    "Native event gold replay completed before its saved transcript.");
            var law = QueryLawForCapture(Origin, ActionId, EventId, request);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native event gold Nature transcript does not replay.");
            providerPrefix.Add(
                ReferenceNatureChanceLawDecisionV111.FromSelection(
                    law, request, selection, Origin));
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, ActionId, providerPrefix.ToArray());
        if ((replay.Continuation is null) != Complete ||
            replay.Continuation is { } pending &&
                ParseSupportedEventId(pending.PendingRequest) != EventId ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState))
            throw new InvalidDataException(
                "Native event gold continuation differs from its replay.");
        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState, Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "Native event gold run differs from its atomic Nature cursor.");
        foreach (var decision in Stochastic.DecisionTranscript)
            decision.Validate();
    }

    public static string Save(
        ReferenceNatureNativeEventGoldSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureNativeEventGoldSessionSnapshotV111(
            CurrentSchemaId, session.EventId, session.ActionId,
            RunSnapshot.Save(session.Origin), RunSnapshot.Save(session.Run),
            session.Stochastic.InitialNatureState, session.Stochastic.NatureState,
            (ReferenceNatureDecisionV111[])session.Stochastic.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureNativeEventGoldSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNativeEventGoldSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Native event gold snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.EventId) ||
            string.IsNullOrWhiteSpace(snapshot.ActionId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event gold snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.ActionId,
            snapshot.InitialNatureState);
        if (replay.EventId != snapshot.EventId)
            throw new InvalidDataException(
                "Native event gold branch differs from its saved boundary.");
        for (var index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "Native event gold replay ended before its transcript.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.Stochastic.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native event gold replay selected a different outcome.");
        }
        if (replay.Complete &&
            savedRun.Stochastic.ReferenceNature is null)
            savedRun = savedRun with
            {
                Stochastic = RunStochasticStateV111.WithReferenceNature(
                    savedRun.Stochastic, snapshot.NatureState.Fork())
            };
        if (CanonicalJson.Sha256(replay.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.Stochastic.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.Stochastic.DecisionTranscript.Length !=
                snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "Native event gold replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private ControlledStochasticDecisionV111[] CreateProviderPrefix()
    {
        var prefix = new List<ControlledStochasticDecisionV111>();
        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < Stochastic.DecisionTranscript.Length; index++)
        {
            var replay = Replay(Origin, ActionId, prefix.ToArray());
            var request = replay.Continuation?.PendingRequest ??
                throw new InvalidDataException(
                    "Native event gold replay ended before its provider prefix.");
            var law = QueryLawForCapture(Origin, ActionId, EventId, request);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native event gold prefix changed its Nature outcome.");
            prefix.Add(ReferenceNatureChanceLawDecisionV111.FromSelection(
                law, request, selection, Origin));
            expectedNatureState = selection.NextState;
        }
        return prefix.ToArray();
    }

    internal static ChanceLawV111 QueryLawForCapture(
        RunState origin, string actionId, string eventId,
        ControlledStochasticRequestV111 request)
    {
        request.Validate();
        var (siteId, ordinal, ownerId, streamId) = request switch
        {
            { UnitFloatRequest: { } floatRequest, IndexRequest: null } =>
                (floatRequest.SiteId, floatRequest.Ordinal,
                    floatRequest.OwnerId, floatRequest.StreamId),
            { IndexRequest: { } indexRequest, UnitFloatRequest: null } =>
                (indexRequest.SiteId, indexRequest.Ordinal,
                    indexRequest.OwnerId, indexRequest.StreamId),
            _ => throw new InvalidDataException(
                "Native event gold request has an invalid typed outcome.")
        };
        if (siteId is not (GoldUnitFloatSiteId or GoldIndexSiteId or
            ChanceLawEngineV111.NativeStoneOfAllTimePotionSlotSiteId or
            ChanceLawEngineV111.NativeFutureOfPotionsCardTypeSiteId))
            throw new NotSupportedException(
                $"Native event-entry replay reached unsupported site '{siteId}'.");
        if (origin.Stochastic.NativeRunRng is null)
        {
            if (request.IndexRequest is not { } legacyIndexRequest)
                throw new NotSupportedException(
                    "Native-regional legacy Event-node replay requires an index request.");
            if (siteId == GoldIndexSiteId)
                return ChanceLawEngineV111
                    .QueryLegacyNativeRegionalEventGoldIndex(
                        origin, legacyIndexRequest, actionId, eventId);
            if (siteId == ChanceLawEngineV111
                    .NativeStoneOfAllTimePotionSlotSiteId)
            {
                var legacyPotionSlotLaw = ChanceLawEngineV111
                    .QueryNativeStoneOfAllTimePotionSlot(
                        origin, siteId, ordinal, actionId, eventId);
                if (streamId != "event" ||
                    legacyIndexRequest.OperationKind != "choose_index" ||
                    legacyPotionSlotLaw.SiteId != siteId ||
                    legacyPotionSlotLaw.OperationKind !=
                        legacyIndexRequest.OperationKind ||
                    legacyPotionSlotLaw.OwnerId != ownerId ||
                    legacyPotionSlotLaw.Ordinal != ordinal ||
                    !legacyIndexRequest.OrderedCandidates.SequenceEqual(
                        Enumerable.Range(
                            0, legacyPotionSlotLaw.Outcomes.Length)))
                    throw new InvalidDataException(
                        "Legacy Stone of All Time entry request differs from its conditional Chance Law.");
                return legacyPotionSlotLaw;
            }
            if (siteId == ChanceLawEngineV111
                    .NativeFutureOfPotionsCardTypeSiteId)
                return ChanceLawEngineV111
                    .QueryLegacyNativeRegionalFutureOfPotionsCardType(
                        origin, legacyIndexRequest, actionId, eventId);
            throw new NotSupportedException(
                $"Native-regional legacy Event-node replay does not support site '{siteId}'.");
        }
        var law = siteId switch
        {
            GoldUnitFloatSiteId =>
                ChanceLawEngineV111.QueryNativeEventGoldUnitFloat(
                    origin, siteId, ordinal, actionId, eventId),
            GoldIndexSiteId => ChanceLawEngineV111.QueryNativeEventGoldIndex(
                origin, siteId, ordinal, actionId, eventId),
            var id when id == ChanceLawEngineV111
                .NativeStoneOfAllTimePotionSlotSiteId =>
                ChanceLawEngineV111.QueryNativeStoneOfAllTimePotionSlot(
                    origin, siteId, ordinal, actionId, eventId),
            _ => ChanceLawEngineV111.QueryNativeFutureOfPotionsCardType(
                origin, siteId, ordinal, actionId, eventId)
        };
        var expectedStream = NativeRngV111Contexts.EventStreamId(
            NativeEventEntry(eventId));
        if (law.SiteId != siteId || law.OwnerId != ownerId ||
            law.Ordinal != ordinal || streamId != expectedStream)
            throw new InvalidDataException(
                "Native event-entry request differs from its source Chance Law.");
        return law;
    }

    internal static string NativeEventEntry(string eventId) => eventId switch
    {
        PrototypeNativeStoneOfAllTime.EventId =>
            PrototypeNativeStoneOfAllTime.NativeRngEventId,
        PrototypeNativeFutureOfPotions.EventId =>
            PrototypeNativeFutureOfPotions.NativeRngEventId,
        PrototypeNativeDenseVegetation.EventId => "DENSE_VEGETATION",
        "proto.native.event.whispering_hollow" => "WHISPERING_HOLLOW",
        "proto.native.event.sunken_statue" or
            "proto.native.underdocks.sunken_statue" => "SUNKEN_STATUE",
        "proto.native.event.luminous_choir" => "LUMINOUS_CHOIR",
        "proto.native.underdocks.sunken_treasury" => "SUNKEN_TREASURY",
        PrototypeNativePunchOff.EventId =>
            PrototypeNativePunchOff.NativeRngEventId,
        PrototypeNativeSharedEvents.ThisOrThatId =>
            PrototypeNativeSharedEvents.ThisOrThatNativeRngEventId,
        PrototypeNativeHiveEvents.LostWispId =>
            PrototypeNativeHiveEvents.LostWispNativeRngEventId,
        "proto.native.event.jungle_maze_adventure" =>
            "JUNGLE_MAZE_ADVENTURE",
        _ => throw new NotSupportedException(
            $"Native event gold session does not support '{eventId}'.")
    };

    private static string ParseSupportedEventId(
        ControlledStochasticRequestV111 request)
    {
        request.Validate();
        if (request.SiteId == ChanceLawEngineV111
            .NativeStoneOfAllTimePotionSlotSiteId)
            return PrototypeNativeStoneOfAllTime.EventId;
        if (request.SiteId == ChanceLawEngineV111
            .NativeFutureOfPotionsCardTypeSiteId)
            return PrototypeNativeFutureOfPotions.EventId;
        if (request.SiteId is not (GoldUnitFloatSiteId or GoldIndexSiteId))
            throw new NotSupportedException(
                "Configured Event entry did not request a supported Native event-entry draw.");
        const string prefix = "event:";
        const string marker = ":gold:";
        if (!request.OwnerId.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Native event gold owner is malformed.");
        var markerIndex = request.OwnerId.IndexOf(
            marker, prefix.Length, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidDataException(
                "Native event gold owner is malformed.");
        var eventId = request.OwnerId[prefix.Length..markerIndex];
        if (!IsSupportedEventId(eventId))
            throw new NotSupportedException(
                $"Native event gold session does not support '{eventId}'.");
        return eventId;
    }

    private static int DecisionCount(string eventId, RunState origin) => eventId switch
    {
        PrototypeNativeStoneOfAllTime.EventId => 1,
        PrototypeNativeFutureOfPotions.EventId => origin.Player.PotionSlots
            .Count(potion => potion is not null),
        "proto.native.event.jungle_maze_adventure" or
            "proto.native.underdocks.sunken_treasury" => 2,
        _ when IsSupportedEventId(eventId) => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(eventId))
    };

    private static bool IsSupportedEventId(string eventId) => eventId is
        PrototypeNativeDenseVegetation.EventId or
        PrototypeNativeStoneOfAllTime.EventId or
        PrototypeNativeFutureOfPotions.EventId or
        "proto.native.event.whispering_hollow" or
        "proto.native.event.sunken_statue" or
        "proto.native.underdocks.sunken_statue" or
        "proto.native.event.luminous_choir" or
        "proto.native.underdocks.sunken_treasury" or
        PrototypeNativePunchOff.EventId or
        PrototypeNativeSharedEvents.ThisOrThatId or
        PrototypeNativeHiveEvents.LostWispId or
        "proto.native.event.jungle_maze_adventure";

    private static void ValidateOpeningBoundary(
        RunState origin, string actionId)
    {
        if (origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Native event gold session requires a configured Native-regional pre-entry Event-node state.");
        PrototypeStateInvariants.Validate(origin);
        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId);
        var nodeId = action?.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null;
        if (nodeId is null ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, origin.Player)
                .Any(node => node.NodeId == nodeId &&
                    node.RoomType == PrototypeRoomType.Event))
            throw new InvalidOperationException(
                "Native event gold session requires a current legal Event-node action.");
    }

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, string actionId,
        ControlledStochasticDecisionV111[] decisions)
    {
        var action = new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Native event gold action is no longer legal at its origin.");
        var replay = ControlledChanceContinuationExecutorV111.CaptureStochastic(
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, allowMissingRelicBag: true),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, action with { Payload = action.Payload.Clone() }).State,
            request => request.SiteId is
                GoldUnitFloatSiteId or GoldIndexSiteId or
                ChanceLawEngineV111.NativeStoneOfAllTimePotionSlotSiteId or
                ChanceLawEngineV111.NativeFutureOfPotionsCardTypeSiteId);
        return replay.Run is { } completed &&
            source.Stochastic.NativeRunRng is not null
                ? replay with { Run = MarkCompletedReplay(completed) }
                : replay;
    }

    private static ControlledStochasticContinuationResultV111 Resolve(
        RunState source, string actionId,
        ControlledStochasticContinuationStateV111 continuation,
        ControlledStochasticDecisionV111 selectedOutcome)
    {
        var action = new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Native event gold action is no longer legal at its origin.");
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, allowMissingRelicBag: true),
            continuation, selectedOutcome,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, action with { Payload = action.Payload.Clone() }).State,
            request => request.SiteId is
                GoldUnitFloatSiteId or GoldIndexSiteId or
                ChanceLawEngineV111.NativeStoneOfAllTimePotionSlotSiteId or
                ChanceLawEngineV111.NativeFutureOfPotionsCardTypeSiteId);
        return replay.Run is { } completed &&
            source.Stochastic.NativeRunRng is not null
                ? replay with { Run = MarkCompletedReplay(completed) }
                : replay;
    }

    private static RunState MarkCompletedReplay(RunState completed)
    {
        var world = completed.World ?? throw new InvalidDataException(
            "Native event gold replay completed without its run world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            "Native event gold replay completed without its event state.");
        return completed with
        {
            World = world with
            {
                Event = eventState with
                {
                    Stochastic = EventStochasticStateV111.WithProviderMode(
                        eventState.Stochastic, "scripted-v111")
                }
            }
        };
    }
}
