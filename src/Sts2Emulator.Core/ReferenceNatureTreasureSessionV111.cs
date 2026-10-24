using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureTreasureLawDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature treasure law decision.");
    }
}

public sealed record ReferenceNatureTreasureSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureTreasureLawDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    private ControlledStochasticDecisionV111[]? _providerDecisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(
            ProviderDecisions);
    private ReferenceNatureTreasureLawDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);
    private ControlledStochasticRequestV111? _pendingRequest =
        ReferenceNatureSessionValueCopiesV111.CopyRequest(PendingRequest);
    private string[]? _pendingCandidateLabels =
        ReferenceNatureSessionValueCopiesV111.CopyArray(
            PendingCandidateLabels);

    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_providerDecisions)!;
        init => _providerDecisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }
    public ReferenceNatureTreasureLawDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
    public ControlledStochasticRequestV111? PendingRequest
    {
        get => ReferenceNatureSessionValueCopiesV111.CopyRequest(
            _pendingRequest);
        init => _pendingRequest =
            ReferenceNatureSessionValueCopiesV111.CopyRequest(value);
    }
    public string[]? PendingCandidateLabels
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_pendingCandidateLabels);
        init => _pendingCandidateLabels = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
}

/// <summary>
/// Atomic configured native Treasure entry driven by conditional Chance Laws.
/// Each prefix replays the actual map action from its pre-entry checkpoint;
/// only the completed room result and Nature cursor are published.
/// </summary>
public sealed record ReferenceNatureTreasureSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureTreasureLawDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureRunActionMechanicsStateV111,
        ReferenceNatureControlledActionStochasticStateV111<
            ReferenceNatureTreasureLawDecisionV111>> Simulation { get; init; } =
        new(
            new ReferenceNatureRunActionMechanicsStateV111(
                Origin, Action, Run),
            new ReferenceNatureControlledActionStochasticStateV111<
                ReferenceNatureTreasureLawDecisionV111>(
                InitialNatureState, NatureState, ProviderDecisions,
                DecisionTranscript, PendingRequest, PendingCandidateLabels));

    [JsonIgnore]
    public ReferenceNatureRunActionMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureControlledActionStochasticStateV111<
        ReferenceNatureTreasureLawDecisionV111> Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public GameAction Action
    {
        get => Mechanics.Action;
        init => Mechanics = Mechanics with { Action = value };
    }

    public RunState Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics with { Run = value };
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

    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get => Stochastic.ProviderDecisions;
        init => Stochastic = Stochastic with { ProviderDecisions = value };
    }
    public ReferenceNatureTreasureLawDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }
    public ControlledStochasticRequestV111? PendingRequest
    {
        get => Stochastic.PendingRequest;
        init => Stochastic = Stochastic with { PendingRequest = value };
    }
    public string[]? PendingCandidateLabels
    {
        get => Stochastic.PendingCandidateLabels;
        init => Stochastic = Stochastic with
        { PendingCandidateLabels = value };
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-treasure-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Stochastic.PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => Stochastic.ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Stochastic.PendingRequest is null ? null :
        QueryLaw(Origin, Stochastic.PendingRequest, Stochastic.PendingCandidateLabels);

    public static ReferenceNatureTreasureSessionV111 Begin(
        RunState source, GameAction action, ulong seed) =>
        Begin(source, action, ReferenceNatureRunCursorV111.Begin(
            source, seed, "Native treasure"));

    public static ReferenceNatureTreasureSessionV111 Begin(
        RunState source, GameAction action) =>
        Begin(source, action, ReferenceNatureRunCursorV111.Begin(
            source, null, "Native treasure"));

    private static ReferenceNatureTreasureSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, action);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Native treasure");

        var natureOrigin = CreateNatureReplayOrigin(source);
        var replay = Replay(natureOrigin, action, []);
        var run = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, initialNatureState)
            : natureOrigin.Fork();
        var session = new ReferenceNatureTreasureSessionV111(
            CurrentSchemaId, natureOrigin.Fork(), CloneAction(action), run,
            initialNatureState.Fork(), initialNatureState.Fork(), [], [],
            replay.Continuation?.PendingRequest.Fork(),
            replay.Continuation?.PendingCandidateLabels is { } labels
                ? (string[])labels.Clone() : null);
        session.Validate();
        return session;
    }

    public ReferenceNatureTreasureSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureTreasureSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature treasure session is already complete.");

        var law = QueryLaw(Origin, Stochastic.PendingRequest!, Stochastic.PendingCandidateLabels);
        var selection = ReferenceNatureV111.SampleValidated(law, Stochastic.NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, Stochastic.PendingRequest!, selection, Origin, Stochastic.PendingCandidateLabels);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            Stochastic.ProviderDecisions, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            CreateNatureReplayOrigin(Origin), continuation, providerDecision,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State);
        var providerDecisions = replay.Continuation?.Decisions ??
            Stochastic.ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureTreasureLawDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var run = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, selection.NextState)
            : Origin.Fork();
        var next = this with
        {
            Run = run,
            NatureState = selection.NextState,
            ProviderDecisions = providerDecisions,
            DecisionTranscript = transcript,
            PendingRequest = replay.Continuation?.PendingRequest.Fork(),
            PendingCandidateLabels = replay.Continuation?.PendingCandidateLabels is { } labels
                ? (string[])labels.Clone() : null
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Run is null ||
            string.IsNullOrWhiteSpace(Action.Kind))
            throw new InvalidDataException(
                "Invalid ReferenceNature treasure session envelope.");
        Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction);
        Stochastic.Validate();
        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native Treasure session origin");
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native Treasure session replay");
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, Stochastic.InitialNatureState, "Native treasure");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var prefix = Stochastic.ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var continuationAtBoundary = replayAtBoundary.Continuation;
            var request = continuationAtBoundary?.PendingRequest
                ?? throw new InvalidDataException(
                    "Treasure replay completed before its saved decision transcript.");
            var labels = continuationAtBoundary!.PendingCandidateLabels;
            var savedDecision = Stochastic.ProviderDecisions[index];
            savedDecision.Validate();
            if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                    request, AsRequest(savedDecision)) ||
                !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                    labels, savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "ReferenceNature treasure provider transcript changed its request.");

            var law = QueryLaw(Origin, request, labels);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedTranscript = new ReferenceNatureTreasureLawDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "ReferenceNature treasure law decisions do not replay.");
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, Action, Stochastic.ProviderDecisions);
        if ((replay.Continuation is null) != Complete ||
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
                replay.Continuation?.PendingRequest, Stochastic.PendingRequest) ||
            !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                replay.Continuation?.PendingCandidateLabels,
                Stochastic.PendingCandidateLabels) ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState))
            throw new InvalidDataException(
                "ReferenceNature treasure continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState, Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature treasure run differs from its atomic cursor.");

        foreach (var decision in Stochastic.DecisionTranscript)
            decision.Validate();
    }

    public static string Save(ReferenceNatureTreasureSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureTreasureSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.Stochastic.InitialNatureState, session.Stochastic.NatureState,
            session.Stochastic.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureTreasureLawDecisionV111[])
                session.Stochastic.DecisionTranscript.Clone(),
            session.Stochastic.PendingRequest?.Fork(),
            session.Stochastic.PendingCandidateLabels is null ? null :
                (string[])session.Stochastic.PendingCandidateLabels.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureTreasureSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureTreasureSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature treasure snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.ProviderDecisions is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature treasure snapshot.");
        var session = new ReferenceNatureTreasureSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action), RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState.Fork(), snapshot.NatureState.Fork(),
            snapshot.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureTreasureLawDecisionV111[])
                snapshot.DecisionTranscript.Clone(),
            snapshot.PendingRequest?.Fork(),
            snapshot.PendingCandidateLabels is null ? null :
                (string[])snapshot.PendingCandidateLabels.Clone());
        session.Validate();
        return session;
    }

    private static ChanceLawV111 QueryLaw(
        RunState source, ControlledStochasticRequestV111 request,
        string[]? candidateLabels)
    {
        request.Validate();
        if (request.UnitFloatRequest is { } floatRequest &&
            floatRequest.SiteId == "relic_grab_bag.rarity.unit_float")
            return ChanceLawEngineV111.QueryNativeTreasureRarity(
                source, floatRequest);
        if (request.IndexRequest is { } indexRequest &&
            indexRequest.SiteId == PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId)
        {
            if (candidateLabels is null)
                throw new InvalidDataException(
                    "Treasure relic identity request lost its candidate labels.");
            var rarityText = SegmentAfter(indexRequest.OwnerId,
                ":rarity:", ":pool:");
            if (!Enum.TryParse<PrototypeRelicRarity>(rarityText,
                    ignoreCase: false, out var rarity) ||
                !Enum.IsDefined(rarity))
                throw new InvalidDataException(
                    "Treasure relic identity request has an invalid rarity.");
            return ChanceLawEngineV111.QueryNativeTreasureRelicSelection(
                source, indexRequest, rarity, candidateLabels);
        }
        throw new NotSupportedException(
            $"ReferenceNature treasure replay does not support stochastic site '{request.SiteId}'.");
    }

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, GameAction action,
        ControlledStochasticDecisionV111[] decisions)
        => ControlledChanceContinuationExecutorV111.CaptureStochastic(
            CreateNatureReplayOrigin(source),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State);

    private static RunState CreateNatureReplayOrigin(RunState source)
        => ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
            source, createRelicBagWhenMissing: true);

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        if (source.Phase != RunPhase.MapChoice || source.World is not { } world ||
            source.Stochastic.NativeRunRng is null ||
            source.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            PrototypeGameEngine.WillSkipNextTreasure(source.Player) ||
            action.Kind != "choose_map_node")
            throw new InvalidOperationException(
                "ReferenceNature treasure session requires a non-skipped configured Treasure choice.");
        var payload = action.ReadPayload<ChooseMapNodePayload>();
        if (!PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
                .Any(node => node.NodeId == payload.NodeId &&
                    node.RoomType == PrototypeRoomType.Treasure) ||
            !new PrototypeGameEngine().GetLegalActions(source).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "ReferenceNature treasure session requires a current legal Treasure-node action.");
        PrototypeStateInvariants.Validate(source);
    }

    private static string SegmentAfter(string value, string start, string end)
    {
        var startIndex = value.LastIndexOf(start, StringComparison.Ordinal);
        var endIndex = value.IndexOf(end,
            startIndex < 0 ? 0 : startIndex + start.Length,
            StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex + start.Length)
            throw new InvalidDataException(
                "Treasure relic request owner has a malformed rarity segment.");
        return value[(startIndex + start.Length)..endIndex];
    }

    private static ControlledStochasticRequestV111 AsRequest(
        ControlledStochasticDecisionV111 decision) => decision.ToRequest();

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };

}
