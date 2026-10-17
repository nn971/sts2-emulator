using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureNativeEventGoldSessionSnapshotV111(
    string SchemaId, string EventId, string ActionId,
    string OriginSnapshotJson, string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>
/// Atomic ReferenceNature replay for Native generated-gold floats during the
/// configured Jungle Maze Adventure or Lost Wisp Event-node entry.
/// </summary>
public sealed record ReferenceNatureNativeEventGoldSessionV111(
    string SchemaId, string EventId, string ActionId,
    RunState Origin, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-native-event-gold-session-v1";
    private const string GoldSiteId =
        ChanceLawEngineV111.NativeEventGoldUnitFloatSiteId;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length ==
        DecisionCount(EventId);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var prefix = CreateProviderPrefix();
            var replay = Replay(Origin, ActionId, prefix);
            var request = replay.PendingChance?.Request ??
                throw new InvalidDataException(
                    "Native event gold replay completed before its sampled prefix.");
            return QueryLaw(Origin, ActionId, EventId, request);
        }
    }

    public static string[] SessionSiteIds => [GoldSiteId];

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
        var request = replay.PendingChance?.Request ??
            throw new NotSupportedException(
                "Configured Event entry did not reach a supported Native gold float.");
        var eventId = ParseSupportedEventId(request);
        _ = QueryLaw(origin, actionId, eventId, request);
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
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript =
                (ReferenceNatureDecisionV111[])DecisionTranscript.Clone()
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
        var pending = replayAtBoundary.PendingChance ??
            throw new InvalidDataException(
                "Native event gold request disappeared before resolution.");
        var law = QueryLaw(Origin, ActionId, EventId, pending.Request);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, pending.Request, selection, Origin);
        var providerDecisions = prefix.Append(providerDecision).ToArray();
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = Replay(Origin, ActionId, providerDecisions);
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
            EventId is not (PrototypeNativeHiveEvents.LostWispId or
                "proto.native.event.jungle_maze_adventure") ||
            string.IsNullOrWhiteSpace(ActionId) || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null ||
            DecisionTranscript.Length > DecisionCount(EventId))
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event gold session envelope.");
        ValidateOpeningBoundary(Origin, ActionId);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, InitialNatureState, "Native event gold");

        var expectedNatureState = InitialNatureState.Fork();
        var providerPrefix = new List<ControlledStochasticDecisionV111>();
        for (var index = 0; index < DecisionTranscript.Length; index++)
        {
            var replayAtBoundary = Replay(
                Origin, ActionId, providerPrefix.ToArray());
            var request = replayAtBoundary.PendingChance?.Request ??
                throw new InvalidDataException(
                    "Native event gold replay completed before its saved transcript.");
            var law = QueryLaw(Origin, ActionId, EventId, request);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                    CanonicalJson.Sha256(DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native event gold Nature transcript does not replay.");
            providerPrefix.Add(
                ReferenceNatureChanceLawDecisionV111.FromSelection(
                    law, request, selection, Origin));
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, ActionId, providerPrefix.ToArray());
        if ((replay.PendingChance is null) != Complete ||
            replay.PendingChance is { } pending &&
                ParseSupportedEventId(pending.Request) != EventId ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState))
            throw new InvalidDataException(
                "Native event gold continuation differs from its replay.");
        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "Native event gold run differs from its atomic Nature cursor.");
        foreach (var decision in DecisionTranscript)
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
            session.InitialNatureState, session.NatureState,
            (ReferenceNatureDecisionV111[])session.DecisionTranscript.Clone());
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
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native event gold replay selected a different outcome.");
        }
        if (replay.Complete && savedRun.ReferenceNatureState is null)
            savedRun = savedRun with
            {
                ReferenceNatureState = snapshot.NatureState.Fork()
            };
        if (CanonicalJson.Sha256(replay.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length !=
                snapshot.DecisionTranscript.Length)
            throw new InvalidDataException(
                "Native event gold replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private ControlledStochasticDecisionV111[] CreateProviderPrefix()
    {
        var prefix = new List<ControlledStochasticDecisionV111>();
        var expectedNatureState = InitialNatureState.Fork();
        for (var index = 0; index < DecisionTranscript.Length; index++)
        {
            var replay = Replay(Origin, ActionId, prefix.ToArray());
            var request = replay.PendingChance?.Request ??
                throw new InvalidDataException(
                    "Native event gold replay ended before its provider prefix.");
            var law = QueryLaw(Origin, ActionId, EventId, request);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native event gold prefix changed its Nature outcome.");
            prefix.Add(ReferenceNatureChanceLawDecisionV111.FromSelection(
                law, request, selection, Origin));
            expectedNatureState = selection.NextState;
        }
        return prefix.ToArray();
    }

    private static ChanceLawV111 QueryLaw(
        RunState origin, string actionId, string eventId,
        ControlledStochasticRequestV111 request)
    {
        request.Validate();
        if (request.UnitFloatRequest is not { } floatRequest ||
            floatRequest.SiteId != GoldSiteId)
            throw new NotSupportedException(
                $"Native event gold replay reached unsupported site '{request.SiteId}'.");
        var law = ChanceLawEngineV111.QueryNativeEventGoldUnitFloat(
            origin, floatRequest.SiteId, floatRequest.Ordinal,
            actionId, eventId);
        var expectedStream = eventId == PrototypeNativeHiveEvents.LostWispId
            ? PrototypeNativeHiveEvents.LostWispNativeRngEventId
            : "JUNGLE_MAZE_ADVENTURE";
        if (law.SiteId != floatRequest.SiteId ||
            law.OwnerId != floatRequest.OwnerId ||
            law.Ordinal != floatRequest.Ordinal ||
            floatRequest.StreamId !=
                NativeRngV111Contexts.EventStreamId(expectedStream))
            throw new InvalidDataException(
                "Native event gold request differs from its source Chance Law.");
        return law;
    }

    private static string ParseSupportedEventId(
        ControlledStochasticRequestV111 request)
    {
        request.Validate();
        if (request.UnitFloatRequest is not { } floatRequest ||
            floatRequest.SiteId != GoldSiteId)
            throw new NotSupportedException(
                "Configured Event entry did not request Native generated gold.");
        const string prefix = "event:";
        const string marker = ":gold:";
        if (!floatRequest.OwnerId.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Native event gold owner is malformed.");
        var markerIndex = floatRequest.OwnerId.IndexOf(
            marker, prefix.Length, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidDataException(
                "Native event gold owner is malformed.");
        var eventId = floatRequest.OwnerId[prefix.Length..
            markerIndex];
        if (eventId is not (PrototypeNativeHiveEvents.LostWispId or
            "proto.native.event.jungle_maze_adventure"))
            throw new NotSupportedException(
                $"Native event gold session does not support '{eventId}'.");
        return eventId;
    }

    private static int DecisionCount(string eventId) => eventId switch
    {
        PrototypeNativeHiveEvents.LostWispId => 1,
        "proto.native.event.jungle_maze_adventure" => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(eventId))
    };

    private static void ValidateOpeningBoundary(
        RunState origin, string actionId)
    {
        if (origin.Phase != RunPhase.MapChoice ||
            origin.World is not { Event: null } world ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null ||
            origin.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "Native event gold session requires a configured pre-entry Event-node state.");
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

    private static ControlledStochasticReplayResultV111 Replay(
        RunState source, string actionId,
        ControlledStochasticDecisionV111[] decisions)
    {
        var action = new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidOperationException(
                "Native event gold action is no longer legal at its origin.");
        var replay = ControlledChanceContinuationExecutorV111.ResumeStochastic(
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, allowMissingRelicBag: true),
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, action with { Payload = action.Payload.Clone() }).State,
            request => request.UnitFloatRequest?.SiteId == GoldSiteId);
        if (replay.Run is not { } completed)
            return replay;
        var world = completed.World ?? throw new InvalidDataException(
            "Native event gold replay completed without its run world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            "Native event gold replay completed without its event state.");
        return replay with
        {
            Run = completed with
            {
                World = world with
                {
                    Event = eventState with
                    {
                        NativeEventRngProviderMode = "scripted-v111"
                    }
                }
            }
        };
    }
}
