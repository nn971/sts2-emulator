using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureNativeShovelDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native Shovel decision.");
    }
}

public sealed record ReferenceNatureNativeShovelSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureNativeShovelDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    private ControlledStochasticDecisionV111[]? _providerDecisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(
            ProviderDecisions);
    private ReferenceNatureNativeShovelDecisionV111[]? _decisionTranscript =
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
    public ReferenceNatureNativeShovelDecisionV111[] DecisionTranscript
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
/// Atomic configured Native Shovel dig. It samples the relic rarity and
/// identity from the projected bag, then commits the Reward and Nature cursor
/// together after replaying the ordinary Rest action.
/// </summary>
public sealed record ReferenceNatureNativeShovelSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureNativeShovelDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureRunActionMechanicsStateV111,
        ReferenceNatureControlledActionStochasticStateV111<
            ReferenceNatureNativeShovelDecisionV111>> Simulation { get; init; } =
        new(
            new ReferenceNatureRunActionMechanicsStateV111(
                Origin, Action, Run),
            new ReferenceNatureControlledActionStochasticStateV111<
                ReferenceNatureNativeShovelDecisionV111>(
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
        ReferenceNatureNativeShovelDecisionV111> Stochastic
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
    public ReferenceNatureNativeShovelDecisionV111[] DecisionTranscript
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
        "sts2-v111-reference-nature-native-shovel-session-v1";
    private const int DrawRequestCount = 2;
    private const string RelicRaritySiteId =
        StochasticSiteRegistryV111.RelicGrabBagRaritySiteId;
    private const string RelicModelSiteId =
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Stochastic.PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => Stochastic.ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Stochastic.PendingRequest is null ? null :
        QueryLaw(Origin, Action, Stochastic.PendingRequest, Stochastic.PendingCandidateLabels,
            Stochastic.ProviderDecisions);

    public static string[] SessionSiteIds =>
        [StochasticSiteRegistryV111.RelicGrabBagRaritySiteId,
            PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId];

    public static ReferenceNatureNativeShovelSessionV111 Begin(
        RunState source, ulong natureSeed) => Begin(
            source, FindDigAction(source), ReferenceNatureRunCursorV111.Begin(
                source, natureSeed, "Native Shovel dig"));

    public static ReferenceNatureNativeShovelSessionV111 Begin(
        RunState source) => Begin(
            source, FindDigAction(source), ReferenceNatureRunCursorV111.Begin(
                source, null, "Native Shovel dig"));

    private static ReferenceNatureNativeShovelSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, action);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Native Shovel dig");

        var origin = CreateNatureReplayOrigin(source);
        var replay = Replay(origin, action, []);
        if (replay.Continuation is null)
            throw new NotSupportedException(
                "Native Shovel dig did not reach a supported relic draw.");
        var session = new ReferenceNatureNativeShovelSessionV111(
            CurrentSchemaId, origin.Fork(), CloneAction(action), origin.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), [], [],
            replay.Continuation.PendingRequest.Fork(),
            CloneLabels(replay.Continuation.PendingCandidateLabels));
        session.Validate();
        return session;
    }

    public ReferenceNatureNativeShovelSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureNativeShovelSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Native Shovel session is already complete.");

        var law = QueryLaw(Origin, Action, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels, Stochastic.ProviderDecisions);
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
                checkpoint, CloneAction(Action)).State,
            RequireSessionRequest);
        var providerDecisions = replay.Continuation?.Decisions ??
            Stochastic.ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureNativeShovelDecisionV111(
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
            PendingCandidateLabels = CloneLabels(
                replay.Continuation?.PendingCandidateLabels)
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
                "Invalid ReferenceNature Native Shovel session envelope.");
        Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction);
        Stochastic.Validate();
        if (Stochastic.DecisionTranscript.Length > DrawRequestCount)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native Shovel session envelope.");

        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native Shovel session origin");
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native Shovel session replay");
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, Stochastic.InitialNatureState, "Native Shovel dig");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var prefix = Stochastic.ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var request = replayAtBoundary.Continuation?.PendingRequest ??
                throw new InvalidDataException(
                    "Native Shovel replay completed before its transcript.");
            var labels = replayAtBoundary.Continuation!.PendingCandidateLabels;
            var savedDecision = Stochastic.ProviderDecisions[index];
            savedDecision.Validate();
            if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                    request, AsRequest(savedDecision)) ||
                !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                    labels, savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "Native Shovel provider transcript changed its request.");

            var law = QueryLaw(Origin, Action, request, labels, prefix);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedTranscript = new ReferenceNatureNativeShovelDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "Native Shovel law decisions do not replay.");
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
                CanonicalJson.Sha256(Stochastic.NatureState) ||
            Complete && Stochastic.ProviderDecisions.Length != DrawRequestCount)
            throw new InvalidDataException(
                "Native Shovel continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState, Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "Native Shovel run differs from its atomic Nature cursor.");
        foreach (var decision in Stochastic.DecisionTranscript)
            decision.Validate();
    }

    public static string Save(ReferenceNatureNativeShovelSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureNativeShovelSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                CloneAction(session.Action), RunSnapshot.Save(session.Run),
                session.Stochastic.InitialNatureState, session.Stochastic.NatureState,
                session.Stochastic.ProviderDecisions.Select(
                    decision => decision.Fork()).ToArray(),
                (ReferenceNatureNativeShovelDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone(),
                session.Stochastic.PendingRequest?.Fork(),
                CloneLabels(session.Stochastic.PendingCandidateLabels)));
    }

    public static ReferenceNatureNativeShovelSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNativeShovelSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Native Shovel snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.ProviderDecisions is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native Shovel snapshot.");
        var session = new ReferenceNatureNativeShovelSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action), RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState.Fork(), snapshot.NatureState.Fork(),
            snapshot.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureNativeShovelDecisionV111[])
                snapshot.DecisionTranscript.Clone(),
            snapshot.PendingRequest?.Fork(),
            CloneLabels(snapshot.PendingCandidateLabels));
        session.Validate();
        return session;
    }

    private static ChanceLawV111 QueryLaw(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request, string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        request.Validate();
        if (request.UnitFloatRequest is { } floatRequest &&
            floatRequest.SiteId == RelicRaritySiteId)
        {
            if (floatRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                floatRequest.OwnerId != PrototypeNativeRelicGrabBag
                    .NativeTreasureRarityOwnerId || candidateLabels is not null)
                throw new InvalidDataException(
                    "Native Shovel rarity request differs from the reward draw.");
            return ChanceLawEngineV111.QueryNativeRelicRarity(floatRequest);
        }

        if (request.IndexRequest is { } indexRequest &&
            indexRequest.SiteId == RelicModelSiteId)
        {
            if (source.World is not { } world || candidateLabels is null ||
                indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                indexRequest.OperationKind != "choose_index" ||
                indexRequest.SupportCount != candidateLabels.Length ||
                !indexRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, candidateLabels.Length)) ||
                !candidateLabels.SequenceEqual(candidateLabels.OrderBy(
                    id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Native Shovel relic identity request is malformed.");

            var rarity = ParseRarity(indexRequest.OwnerId);
            var bag = BagAfterPrefix(source, prefix);
            var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                bag, rarity, merchant: false, source.Player);
            var expectedCandidates = plan.CandidateIds;
            var expectedOwner = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                world, rarity, expectedCandidates);
            if (plan.SelectedTier is null ||
                !candidateLabels.SequenceEqual(expectedCandidates,
                    StringComparer.Ordinal) || indexRequest.OwnerId != expectedOwner)
                throw new InvalidDataException(
                    "Native Shovel identities differ from the canonical relic bag.");

            var lawEventId = CanonicalJson.Sha256(new
            {
                boundary = "native-shovel-dig",
                actionId = PrototypeAiEnvironment.StableActionId(action),
                request = indexRequest
            });
            return ChanceLawEngineV111.QueryRandomIndex(
                lawEventId, indexRequest);
        }

        throw new NotSupportedException(
            $"Native Shovel replay does not support stochastic site '{request.SiteId}'.");
    }

    private static PrototypeRelicBagState BagAfterPrefix(
        RunState source, ControlledStochasticDecisionV111[] prefix)
    {
        var bag = source.World?.Stochastic.RelicBag ??
            throw new InvalidDataException(
                "Native Shovel replay lost its canonical relic bag.");
        foreach (var decision in prefix.Where(decision =>
            decision.IndexDecision?.Request.SiteId == RelicModelSiteId))
        {
            var indexDecision = decision.IndexDecision!;
            var labels = decision.CandidateLabels ?? throw new InvalidDataException(
                "Native Shovel prefix lost its selected identity.");
            var rarity = ParseRarity(indexDecision.Request.OwnerId);
            if (indexDecision.SelectedIndex >= labels.Length)
                throw new InvalidDataException(
                    "Native Shovel prefix selected outside its candidate labels.");
            var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                bag, rarity, merchant: false, source.Player);
            var expected = plan.CandidateIds;
            if (plan.SelectedTier is null ||
                !labels.SequenceEqual(expected, StringComparer.Ordinal) ||
                indexDecision.Request.OwnerId !=
                    PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                        source.World!, rarity, expected))
                throw new InvalidDataException(
                    "Native Shovel prefix differs from its canonical bag draw.");
            (bag, _) = PrototypeNativeRelicGrabBag.PullNatureSelected(
                bag, rarity, labels, indexDecision.SelectedIndex,
                merchant: false, source.Player);
        }
        return bag;
    }

    private static PrototypeRelicRarity ParseRarity(string ownerId)
    {
        var start = ownerId.LastIndexOf(":rarity:", StringComparison.Ordinal);
        var end = ownerId.IndexOf(":pool:",
            start < 0 ? 0 : start + ":rarity:".Length,
            StringComparison.Ordinal);
        if (start < 0 || end <= start + ":rarity:".Length ||
            !Enum.TryParse<PrototypeRelicRarity>(
                ownerId[(start + ":rarity:".Length)..end], false,
                out var rarity) || !Enum.IsDefined(rarity))
            throw new InvalidDataException(
                "Native Shovel request owner has a malformed rarity.");
        return rarity;
    }

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, GameAction action,
        ControlledStochasticDecisionV111[] decisions) =>
        ControlledChanceContinuationExecutorV111.CaptureStochastic(
            CreateNatureReplayOrigin(source),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State,
            RequireSessionRequest);

    private static RunState CreateNatureReplayOrigin(RunState source) =>
        ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
            source, createRelicBagWhenMissing: true);

    private static bool RequireSessionRequest(
        ControlledStochasticRequestV111 request)
    {
        if (request.SiteId is RelicRaritySiteId or RelicModelSiteId)
            return true;
        throw new NotSupportedException(
            $"Native Shovel replay does not support stochastic site '{request.SiteId}'.");
    }

    private static GameAction FindDigAction(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                candidate.Kind == PrototypeNativeRestSiteActionsV111.Dig)
            ?? throw new InvalidOperationException(
                "Native Shovel dig is not legal in the current state.");
    }

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        var world = source.World;
        if (source.Phase != RunPhase.Rest || world is null ||
            source.Stochastic.NativeRunRng is null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            source.Stochastic.ControlledRunActionChoice is not null ||
            source.Stochastic.ControlledEventRewardChoice is not null ||
            !PrototypeNativeRestSiteRelics.CanDig(source.Player) ||
            action.Kind != PrototypeNativeRestSiteActionsV111.Dig ||
            !new PrototypeGameEngine().GetLegalActions(source).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Native Shovel session requires a legal configured Native Rest dig action.");
        PrototypeStateInvariants.Validate(source);
    }

    private static ControlledStochasticRequestV111 AsRequest(
        ControlledStochasticDecisionV111 decision) => decision switch
    {
        { UnitFloatDecision: { } unit } =>
            ControlledStochasticRequestV111.FromUnitFloat(unit.Request),
        { IndexDecision: { } index } =>
            ControlledStochasticRequestV111.FromIndex(index.Request),
        _ => throw new InvalidDataException(
            "Native Shovel provider decision has an unsupported type.")
    };

    private static string[]? CloneLabels(string[]? labels) =>
        labels is null ? null : (string[])labels.Clone();

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
