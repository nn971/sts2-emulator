using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureEndlessConveyorSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    string ActionId,
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

/// <summary>Atomic ReferenceNature continuation for Endless Conveyor dish,
/// potion, card-upgrade, and Fried Eel Colorless-card boundaries.</summary>
public sealed record ReferenceNatureEndlessConveyorSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    string ActionId,
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
    public GameAction Action => RequireAction(Origin, ActionId);

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "A pending Endless Conveyor session has no completed provider prefix.");
            var selections = SampleTranscript(
                Origin, ActionId, InitialNatureState,
                Stochastic.DecisionTranscript, PlannedSiteIds);
            if (IsLegacyNativeFallback(Origin))
            {
                var probed = ProbeLegacyAction(Origin, ActionId);
                return selections.Select(entry =>
                {
                    var matches = probed.Requests.Where(request =>
                        request.SiteId == entry.Law.SiteId).ToArray();
                    if (matches.Length != 1)
                        throw new InvalidDataException(
                            "Endless Conveyor prefix no longer reaches one request for '"
                            + entry.Law.SiteId + "'.");
                    var request = matches[0];
                    ValidateLegacyFallbackRequest(
                        Origin, probed.Run, request, entry.Law.SiteId);
                    if (request.OwnerId != entry.Law.OwnerId ||
                        request.Ordinal != entry.Law.Ordinal ||
                        request.OperationKind != entry.Law.OperationKind ||
                        request.SupportCount != entry.Law.Outcomes.Length)
                        throw new InvalidDataException(
                            "Endless Conveyor prefix request differs from its sampled law.");
                    var decision = new RandomIndexDecisionV111(
                        request, entry.Selection.OutcomeIndex,
                        "controlled-v111");
                    decision.Validate();
                    return ControlledStochasticDecisionV111.FromIndex(
                        decision);
                }).ToArray();
            }

            var decisions = new ControlledStochasticDecisionV111[
                selections.Length];
            for (var index = 0; index < selections.Length; index++)
            {
                var entry = selections[index];
                if (entry.Law.SiteId ==
                    PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
                {
                    var prefix = selections.Take(index).Select(previous =>
                        ToControlledDecision(
                            Origin, previous.Law, previous.Selection))
                        .ToArray();
                    var capture = CapturePendingRequest(
                        Origin, ActionId, entry.Law.SiteId, prefix);
                    var conditioningRun = capture.UnitFloatCapture
                        ?.PreDrawState
                        ?? throw new InvalidDataException(
                            "Endless Conveyor decision lost its pre-draw boundary.");
                    var request = BuildUnitFloatRequest(
                        Origin, entry.Law);
                    var unitFloatDecision =
                        ReferenceNatureChanceLawDecisionV111.FromSelection(
                        entry.Law,
                        ControlledStochasticRequestV111.FromUnitFloat(request),
                        entry.Selection, conditioningRun).UnitFloatDecision!;
                    decisions[index] =
                        ControlledStochasticDecisionV111.FromUnitFloat(
                            unitFloatDecision);
                    continue;
                }
                decisions[index] = ToControlledDecision(
                    Origin, entry.Law, entry.Selection);
            }
            return decisions;
        }
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-endless-conveyor-session-v1";

    public static string[] SessionSiteIds =>
        [PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
         PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
         PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
         PrototypeNativePotionShop.UniformModelSiteId];

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => Stochastic.DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => Stochastic.DecisionTranscript.Length > 0 &&
        Stochastic.DecisionTranscript.Length == PlannedSiteIds.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var prefix = SampleTranscript(
                Origin, ActionId, Stochastic.InitialNatureState,
                Stochastic.DecisionTranscript, PlannedSiteIds)
                .Select(entry => ToControlledDecision(
                    Origin, entry.Law, entry.Selection)).ToArray();
            return QuerySite(Origin, ActionId,
                PlannedSiteIds[NextOrdinal], prefix);
        }
    }

    public static ReferenceNatureEndlessConveyorSessionV111 Begin(
        RunState origin, string actionId, ulong natureSeed) => Begin(origin,
            actionId, ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Endless Conveyor"));

    public static ReferenceNatureEndlessConveyorSessionV111 Begin(
        RunState origin, string actionId) => Begin(origin, actionId,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Endless Conveyor"));

    private static ReferenceNatureEndlessConveyorSessionV111 Begin(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(initial);
        var firstSiteId = GetPlannedSiteIds(origin, actionId)[0];
        _ = QuerySite(origin, actionId, firstSiteId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Endless Conveyor");
        var session = new ReferenceNatureEndlessConveyorSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(), actionId,
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public static ChanceLawV111 QueryChanceLaw(
        RunState origin, string actionId, string siteId,
        int? ordinal = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        return QuerySite(origin, actionId, siteId, [], ordinal);
    }

    public ReferenceNatureEndlessConveyorSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureEndlessConveyorSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Endless Conveyor session is already complete.");
        var plannedSiteIds = PlannedSiteIds;
        var prefix = SampleTranscript(
            Origin, ActionId, Stochastic.InitialNatureState,
            Stochastic.DecisionTranscript, plannedSiteIds)
            .Select(entry => ToControlledDecision(
                Origin, entry.Law, entry.Selection)).ToArray();
        var law = QuerySite(
            Origin, ActionId, plannedSiteIds[NextOrdinal], prefix);
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNatureState = selection.NextState;
        var nextRun = Origin.Fork();
        if (transcript.Length == plannedSiteIds.Length)
        {
            var selections = SampleTranscript(
                Origin, ActionId, InitialNatureState, transcript,
                plannedSiteIds);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                ReplaySelections(Origin, ActionId, selections),
                nextNatureState);
        }
        var next = this with
        {
            Run = nextRun,
            NatureState = nextNatureState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            string.IsNullOrWhiteSpace(ActionId))
            throw new InvalidDataException(
                "Invalid ReferenceNature Endless Conveyor session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        _ = RequireAction(Origin, ActionId);
        var plannedSiteIds = PlannedSiteIds;
        if (Stochastic.DecisionTranscript.Length > plannedSiteIds.Length ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState,
                Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Endless Conveyor cursor differs from its session envelope.");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        var selections = SampleTranscript(
            Origin, ActionId, Stochastic.InitialNatureState,
            Stochastic.DecisionTranscript,
            plannedSiteIds, out expectedNatureState);
        var expectedRun = Complete
            ? ReferenceNatureRunCursorV111.Commit(
                ReplaySelections(Origin, ActionId, selections),
                expectedNatureState)
            : Origin.Fork();

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Endless Conveyor replay differs from its session envelope.");
    }

    public static string Save(
        ReferenceNatureEndlessConveyorSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureEndlessConveyorSessionSnapshotV111(
                CurrentSchemaId,
                JsonSerializer.Serialize(session.Origin),
                JsonSerializer.Serialize(session.Run),
                session.ActionId,
                session.Stochastic.InitialNatureState,
                session.Stochastic.NatureState,
                session.Stochastic.DecisionTranscript),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public static ReferenceNatureEndlessConveyorSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
            ReferenceNatureEndlessConveyorSessionSnapshotV111>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException(
                "Invalid ReferenceNature Endless Conveyor snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId)
            throw new InvalidDataException(
                "Unsupported ReferenceNature Endless Conveyor snapshot schema.");
        var origin = JsonSerializer.Deserialize<RunState>(
            snapshot.OriginSnapshotJson)
            ?? throw new InvalidDataException(
                "Endless Conveyor snapshot has no origin state.");
        var run = JsonSerializer.Deserialize<RunState>(
            snapshot.RunSnapshotJson)
            ?? throw new InvalidDataException(
                "Endless Conveyor snapshot has no current state.");
        var session = new ReferenceNatureEndlessConveyorSessionV111(
            snapshot.SchemaId, origin, run, snapshot.ActionId,
            snapshot.InitialNatureState, snapshot.NatureState,
            snapshot.DecisionTranscript);
        session.Validate();
        return session;
    }

    private static GameAction RequireAction(RunState origin, string actionId)
    {
        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId);
        if (action is null)
            throw new InvalidOperationException(
                "Endless Conveyor Nature choice requires its current legal action.");
        if (origin.Phase == RunPhase.MapChoice)
        {
            var world = origin.World
                ?? throw new InvalidOperationException(
                    "Endless Conveyor map entry requires a world.");
            var nodeId = action.Kind == "choose_map_node"
                ? action.ReadPayload<ChooseMapNodePayload>().NodeId
                : null;
            if (nodeId is null || !PrototypeNativeWingedBoots
                    .AvailableNodes(world, origin.Player).Any(node =>
                        node.NodeId == nodeId &&
                        node.RoomType == PrototypeRoomType.Event))
                throw new InvalidOperationException(
                    "Endless Conveyor Nature choice requires a legal Event-node action.");
        }
        else if (origin.Phase != RunPhase.Event ||
            origin.World?.Event?.EventId != PrototypeNativeEndlessConveyor.EventId)
        {
            throw new InvalidOperationException(
                "Endless Conveyor Nature choice requires its active event action.");
        }
        return action;
    }

    private string[] PlannedSiteIds => GetPlannedSiteIds(Origin, ActionId);

    [JsonIgnore]
    public string? LastSiteId => Stochastic.DecisionTranscript.Length == 0
        ? null : PlannedSiteIds[Stochastic.DecisionTranscript.Length - 1];

    private static string[] GetPlannedSiteIds(
        RunState origin, string actionId)
    {
        var action = RequireAction(origin, actionId);
        var evt = origin.World?.Event;
        if (origin.Phase == RunPhase.Event && evt is not null &&
            action.Kind == "event_choice")
        {
            var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
            if (choice == "observe")
                return [PrototypeNativeEndlessConveyor.UpgradeCardSiteId];
            if (choice == "grab" && evt.Stochastic.RandomOutcomes.NativeDishId == "FRIED_EEL" &&
                origin.Player.Gold >= 40)
                return [PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
                    PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];
            if (choice == "grab" && evt.Stochastic.RandomOutcomes.NativeDishId == "SPICY_SNAPPY" &&
                origin.Player.Gold >= 40 &&
                PrototypeNativeEndlessConveyor
                    .GetUpgradeCardCandidates(origin).Length > 0)
                return [PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
                    PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];
            if (choice == "grab" &&
                evt.Stochastic.RandomOutcomes.NativeDishId == "SUSPICIOUS_CONDIMENT")
                return [PrototypeNativePotionShop.UniformModelSiteId];
        }
        return [PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];
    }

    private static ChanceLawV111 QuerySite(
        RunState origin, string actionId, string siteId,
        ControlledStochasticDecisionV111[]? prefix = null,
        int? ordinal = null)
    {
        prefix ??= [];
        if (IsLegacyNativeFallback(origin))
            return QueryLegacyFallbackSite(origin, actionId, siteId);

        if (siteId == PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
        {
            var capture = CapturePendingRequest(
                origin, actionId, siteId, prefix);
            var preDraw = capture.UnitFloatCapture?.PreDrawState
                ?? throw new InvalidDataException(
                    "Endless Conveyor dish boundary did not expose its pre-draw state.");
            var request = capture.Request.UnitFloatRequest
                ?? throw new InvalidDataException(
                    "Endless Conveyor dish boundary is not a unit-float request.");
            if (ordinal is { } requestedOrdinal &&
                requestedOrdinal != request.Ordinal)
                throw new InvalidOperationException(
                    "Endless Conveyor dish query ordinal differs from its pre-draw request.");
            var law = ChanceLawEngineV111
                .QueryNativeEndlessConveyorDishEntry(
                    preDraw, actionId, request.Ordinal);
            if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                    capture.Request,
                    ControlledStochasticRequestV111.FromUnitFloat(
                        BuildUnitFloatRequest(preDraw, law))))
                throw new InvalidDataException(
                    "Endless Conveyor dish law differs from its captured pre-draw request.");
            return law;
        }

        if (siteId is PrototypeNativeEndlessConveyor.UpgradeCardSiteId or
            PrototypeNativeEndlessConveyor.ColorlessCardSiteId)
        {
            var capture = CapturePendingRequest(
                origin, actionId, siteId, prefix);
            var request = capture.Request.IndexRequest
                ?? throw new InvalidDataException(
                    "Endless Conveyor card boundary is not an index request.");
            if (ordinal is { } requestedOrdinal &&
                requestedOrdinal != request.Ordinal)
                throw new InvalidOperationException(
                    "Endless Conveyor card query ordinal differs from its pre-draw request.");
            var law = siteId == PrototypeNativeEndlessConveyor.UpgradeCardSiteId
                ? ChanceLawEngineV111.QueryNativeEndlessConveyorUpgrade(
                    origin, actionId, request.Ordinal)
                : ChanceLawEngineV111.QueryNativeEndlessConveyorColorlessCard(
                    origin, actionId, request.Ordinal);
            if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                    capture.Request,
                    ControlledStochasticRequestV111.FromIndex(
                        BuildIndexRequest(origin, law))))
                throw new InvalidDataException(
                    "Endless Conveyor card law differs from its captured request.");
            return law;
        }

        if (siteId == PrototypeNativePotionShop.UniformModelSiteId)
            return QuerySuspiciousCondimentPotion(origin, actionId);

        throw new NotSupportedException(
            "Endless Conveyor session has no query for site '" + siteId + "'.");
    }

    private sealed record PendingRequestCapture(
        ControlledStochasticRequestV111 Request,
        NativeEventUnitFloatCaptureV111? UnitFloatCapture);

    private static PendingRequestCapture CapturePendingRequest(
        RunState origin, string actionId, string siteId,
        ControlledStochasticDecisionV111[] prefix)
    {
        var action = RequireAction(origin, actionId);
        var controlledSites = prefix.Select(decision =>
                decision.ToRequest().SiteId)
            .Append(siteId).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<NativeEventUnitFloatCaptureV111> captures;
        ControlledStochasticContinuationStateV111? continuation;
        using (var probe = NativeEventRandomProbeV111.Begin())
        using (NativeEngineEventJournalV111.Begin())
        {
            var replay = ControlledChanceContinuationExecutorV111
                .CaptureStochastic(
                    origin,
                    ControlledStochasticContinuationStateV111
                        .MixedPrimitiveSchemaId,
                    prefix,
                    checkpoint => new PrototypeGameEngine().Step(
                        checkpoint, action).State,
                    request => controlledSites.Contains(request.SiteId));
            continuation = replay.Continuation;
            captures = probe.UnitFloatCaptures;
        }

        var pending = continuation?.PendingRequest
            ?? throw new InvalidOperationException(
                "Endless Conveyor action did not suspend before its declared '"
                + siteId + "' outcome.");
        if (pending.SiteId != siteId)
            throw new InvalidDataException(
                "Endless Conveyor action suspended at a different site than requested.");
        var unitFloatCaptures = captures.Where(capture =>
            capture.Request.SiteId == siteId).ToArray();
        if (unitFloatCaptures.Length > 1)
            throw new InvalidDataException(
                "Endless Conveyor action reached its unit-float site more than once before suspension.");
        var unitFloatCapture = unitFloatCaptures.SingleOrDefault();
        if (unitFloatCapture is not null &&
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                pending,
                ControlledStochasticRequestV111.FromUnitFloat(
                    unitFloatCapture.Request)))
            throw new InvalidDataException(
                "Endless Conveyor pre-draw capture differs from its pending request.");
        return new PendingRequestCapture(pending, unitFloatCapture);
    }

    private static bool IsLegacyNativeFallback(RunState state) =>
        state.Stochastic.NativeRunRng is null && state.World is { } world &&
        PrototypeGameEngine.UsesNativeRegionalSystems(world);

    private static ChanceLawV111 QueryLegacyFallbackSite(
        RunState origin, string actionId, string siteId)
    {
        var (probedRun, requests) = ProbeLegacyAction(origin, actionId);
        var matching = requests.Where(request => request.SiteId == siteId)
            .ToArray();
        if (matching.Length != 1)
            throw new InvalidOperationException(
                "Native-regional Endless Conveyor fallback action must reach exactly one '"
                + siteId + "' request.");
        var request = matching[0];
        ValidateLegacyFallbackRequest(
            origin, probedRun, request, siteId);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "reference_nature.endless_conveyor.legacy_fallback",
            actionId,
            eventId = PrototypeNativeEndlessConveyor.EventId,
            request
        });
        return ChanceLawEngineV111.QueryRandomIndex(eventId, request);
    }

    private static (RunState Run, RandomIndexRequestV111[] Requests)
        ProbeLegacyAction(RunState origin, string actionId)
    {
        var action = RequireAction(origin, actionId);
        RunState run;
        RandomIndexRequestV111[] requests;
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            run = new PrototypeGameEngine().Step(origin.Fork(), action).State;
            requests = journal.Entries.Select(entry => entry.Request)
                .Select(request => request with
                {
                    OrderedCandidates =
                        (int[])request.OrderedCandidates.Clone()
                }).ToArray();
        }
        if (run.World?.Event?.EventId !=
            PrototypeNativeEndlessConveyor.EventId)
            throw new InvalidOperationException(
                "The source event-selection result did not enter Endless Conveyor.");
        return (run, requests);
    }

    private static void ValidateLegacyFallbackRequest(
        RunState origin, RunState probedRun,
        RandomIndexRequestV111 request, string siteId)
    {
        var expectedStream = siteId ==
                PrototypeNativePotionShop.UniformModelSiteId
            ? "reward" : "event";
        if (request.StreamId != expectedStream)
            throw new InvalidDataException(
                "Endless Conveyor fallback request uses an unexpected legacy stream.");

        if (siteId == PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
        {
            var candidates = GetLegacyDishCandidates(origin, probedRun);
            var previousCount = origin.World?.Event?.Stochastic.RandomOutcomes.NativeDishCount ?? 0;
            var totalWeight = candidates.Sum(candidate => candidate.Weight);
            var ownerId = $"dish:{previousCount + 1}:pool:"
                + CanonicalJson.Sha256(candidates);
            if (request.OperationKind != "bounded_integer" ||
                request.BoundedCount != totalWeight ||
                request.OwnerId != ownerId || totalWeight <= 0)
                throw new InvalidDataException(
                    "Endless Conveyor fallback dish ticket differs from its weighted source support.");
            return;
        }

        var (expectedOwner, expectedCount) = siteId switch
        {
            PrototypeNativeEndlessConveyor.UpgradeCardSiteId =>
                UpgradeFallbackSupport(origin),
            PrototypeNativeEndlessConveyor.ColorlessCardSiteId =>
                ($"dish:{origin.World?.Event?.Stochastic.RandomOutcomes.NativeDishCount ?? 0}:pool:"
                    + CanonicalJson.Sha256(
                        PrototypeNativeEndlessConveyor.GetColorlessCardCandidates(origin)),
                 PrototypeNativeEndlessConveyor.GetColorlessCardCandidates(origin).Length),
            PrototypeNativePotionShop.UniformModelSiteId =>
                ($"event:endless_conveyor:dish:{origin.World?.Event?.Stochastic.RandomOutcomes.NativeDishCount ?? 0}:potion:pool:"
                    + CanonicalJson.Sha256(
                        PrototypeNativePotionShop.NativePotionPoolOrder),
                 PrototypeNativePotionShop.NativePotionPoolOrder.Count),
            _ => throw new NotSupportedException(
                "Endless Conveyor fallback has no support validator for '" + siteId + "'.")
        };
        if (request.OperationKind != "choose_index" ||
            request.SupportCount != expectedCount ||
            request.OwnerId != expectedOwner || expectedCount <= 0 ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, expectedCount)) ||
            siteId == PrototypeNativePotionShop.UniformModelSiteId &&
                request.StreamId != "reward")
            throw new InvalidDataException(
                "Endless Conveyor fallback index differs from its ordered source support.");
    }

    private static (string OwnerId, int Count) UpgradeFallbackSupport(
        RunState origin)
    {
        var candidates = PrototypeNativeEndlessConveyor
            .GetUpgradeCardCandidates(origin);
        return ("endless_conveyor:deck:" + CanonicalJson.Sha256(candidates),
            candidates.Length);
    }

    private static (string Id, int Weight)[] GetLegacyDishCandidates(
        RunState origin, RunState probedRun)
    {
        var previous = origin.World?.Event;
        var current = new EventState(PrototypeNativeEndlessConveyor.EventId);
        current = current with
        {
            Stochastic = EventStochasticStateV111.WithCommittedRandom(
                current.Stochastic,
                current.Stochastic.RandomOutcomes with
                {
                    NativeDishCount = previous?.Stochastic.RandomOutcomes.NativeDishCount ?? 0,
                    NativeLastDishId = previous?.Stochastic.RandomOutcomes.NativeLastDishId
                })
        };
        var world = probedRun.World
            ?? throw new InvalidDataException(
                "Endless Conveyor fallback probe lost its event world.");
        return PrototypeNativeEndlessConveyor.GetWeightedDishCandidates(
            probedRun with
            {
                World = world with { Event = current }
            }, current);
    }

    private static ChanceLawV111 QuerySuspiciousCondimentPotion(
        RunState origin, string actionId)
    {
        var action = RequireAction(origin, actionId);
        var evt = origin.World?.Event;
        if (origin.Phase != RunPhase.Event || action.Kind != "event_choice" ||
            action.ReadPayload<EventChoicePayload>().ChoiceId != "grab" ||
            evt is null ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.Stochastic.RandomOutcomes.NativeDishId != "SUSPICIOUS_CONDIMENT" ||
            evt.Stochastic.RandomOutcomes.NativeDishRollPending ||
            evt.PendingPotionReplacement is not null ||
            evt.RemainingPotionIds.Length != 0 ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.Stochastic.NativeRunRng is null ||
            evt.Stochastic.NativeRng?.StreamId != NativeRngV111Contexts
                .ForEvent(origin.RunSeed,
                    PrototypeNativeEndlessConveyor.NativeRngEventId).StreamId ||
            !origin.Player.PotionSlots.Any(slot => slot is null))
            throw new InvalidOperationException(
                "Suspicious Condiment potion law requires its legal Grab action before the queued potion is resolved.");

        var ownerPrefix =
            $"event:endless_conveyor:dish:{evt.Stochastic.RandomOutcomes.NativeDishCount}:potion";
        return ChanceLawEngineV111.QueryLegacyUniformPotionModel(
            "reward", ownerPrefix,
            GetLegacyStreamOrdinal(origin, "reward"));
    }

    private static int GetLegacyStreamOrdinal(
        RunState origin, string streamId)
    {
        if (!origin.Stochastic.PrototypeRng.TryGetStream(streamId, out var stream))
            throw new InvalidDataException(
                $"Endless Conveyor potion request requires one '{streamId}' legacy stream.");
        var ordinal = stream.CallCount ?? 0;
        if (ordinal > int.MaxValue)
            throw new NotSupportedException(
                "Endless Conveyor potion request ordinal exceeds the typed request range.");
        return (int)ordinal;
    }

    private static (ChanceLawV111 Law,
        ReferenceNatureSelectionV111 Selection)[] SampleTranscript(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[] transcript,
        string[] plannedSiteIds) => SampleTranscript(
            origin, actionId, initialNatureState, transcript,
            plannedSiteIds, out _);

    private static (ChanceLawV111 Law,
        ReferenceNatureSelectionV111 Selection)[] SampleTranscript(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[] transcript,
        string[] plannedSiteIds,
        out ReferenceNatureStateV111 finalNatureState)
    {
        if (transcript.Length > plannedSiteIds.Length)
            throw new InvalidDataException(
                "Endless Conveyor transcript exceeds its planned event boundaries.");
        var cursor = initialNatureState.Fork();
        var selections = new List<(ChanceLawV111,
            ReferenceNatureSelectionV111)>(transcript.Length);
        for (var index = 0; index < transcript.Length; index++)
        {
            var prefix = selections.Select(entry => ToControlledDecision(
                origin, entry.Item1, entry.Item2)).ToArray();
            var law = QuerySite(
                origin, actionId, plannedSiteIds[index], prefix);
            var selection = ReferenceNatureV111.SampleValidated(law, cursor);
            transcript[index].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(transcript[index]))
                throw new InvalidDataException(
                    "ReferenceNature Endless Conveyor transcript does not replay.");
            selections.Add((law, selection));
            cursor = selection.NextState;
        }
        finalNatureState = cursor;
        return selections.ToArray();
    }

    private static ControlledStochasticDecisionV111 ToControlledDecision(
        RunState origin, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        if (law.OperationKind == "unit_float_weighted_ticket")
        {
            var request = BuildUnitFloatRequest(origin, law);
            var decision = ReferenceNatureChanceLawDecisionV111.FromSelection(
                law,
                ControlledStochasticRequestV111.FromUnitFloat(request),
                selection).UnitFloatDecision!;
            return ControlledStochasticDecisionV111.FromUnitFloat(decision);
        }

        var indexRequest = BuildIndexRequest(origin, law);
        var controlledRequest = ControlledStochasticRequestV111
            .FromIndex(indexRequest);
        var indexDecision = law.SiteId ==
                PrototypeNativePotionShop.UniformModelSiteId
            ? ReferenceNatureChanceLawDecisionV111
                .FromOrderedIndexSelection(law, controlledRequest, selection,
                    law.Outcomes.Select(outcome => outcome.OutcomeId).ToArray())
                .IndexDecision!
            : ReferenceNatureChanceLawDecisionV111.FromSelection(
                law, controlledRequest, selection).IndexDecision!;
        return ControlledStochasticDecisionV111.FromIndex(indexDecision);
    }

    private static RunState ReplaySelections(
        RunState origin, string actionId,
        (ChanceLawV111 Law,
            ReferenceNatureSelectionV111 Selection)[] selections)
    {
        if (IsLegacyNativeFallback(origin))
            return ReplayLegacyFallbackSelections(
                origin, actionId, selections);

        if (selections.Length == 1)
        {
            var (law, selection) = selections[0];
            if (law.SiteId == PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
                return ReplayDishSelection(origin, actionId, law, selection);
            if (law.OperationKind == "choose_index")
                return ReplayIndexSelection(origin, actionId, law, selection);
            throw new InvalidDataException(
                "Unsupported single-boundary Endless Conveyor Nature transcript.");
        }
        if (selections.Length != 2 ||
            selections[0].Law.OperationKind != "choose_index" ||
            selections[1].Law.SiteId != PrototypeNativeEndlessConveyor
                .DishWeightedTicketSiteId)
            throw new InvalidDataException(
                "Endless Conveyor mixed transcript must be one index followed by one dish ticket.");

        var indexChoice = selections[0];
        var dishChoice = selections[1];
        var indexRequest = BuildIndexRequest(origin, indexChoice.Law);
        var typedIndex = ReferenceNatureChanceLawDecisionV111.FromSelection(
            indexChoice.Law,
            ControlledStochasticRequestV111.FromIndex(indexRequest),
            indexChoice.Selection).IndexDecision!;
        var preDraw = CaptureDishPreDrawState(
            origin, actionId,
            [ControlledStochasticDecisionV111.FromIndex(typedIndex)]);
        var floatRequest = BuildUnitFloatRequest(origin, dishChoice.Law);
        var typedFloat = ReferenceNatureChanceLawDecisionV111.FromSelection(
            dishChoice.Law,
            ControlledStochasticRequestV111.FromUnitFloat(floatRequest),
            dishChoice.Selection, preDraw).UnitFloatDecision!;
        var run = ReferenceNatureNativeEventIndexReplayV111
            .ReplayMixedIndexAndUnitFloat(
                origin, typedIndex, typedFloat,
                RequireAction(origin, actionId),
                PrototypeNativeEndlessConveyor.NativeRngEventId,
                PrototypeNativeEndlessConveyor.EventId,
                indexChoice.Law.SiteId,
                dishChoice.Law.SiteId);
        ValidateSelectedEffect(origin, run, indexChoice.Law,
            indexChoice.Selection);
        ValidateSelectedDish(run, dishChoice.Selection);
        return run;
    }

    private static RunState ReplayLegacyFallbackSelections(
        RunState origin, string actionId,
        (ChanceLawV111 Law,
            ReferenceNatureSelectionV111 Selection)[] selections)
    {
        if (selections.Length == 0)
            throw new InvalidDataException(
                "Endless Conveyor fallback replay requires a nonempty transcript.");
        var probed = ProbeLegacyAction(origin, actionId);
        var requests = selections.Select(entry =>
        {
            var matching = probed.Requests.Where(request =>
                request.SiteId == entry.Law.SiteId).ToArray();
            if (matching.Length != 1)
                throw new InvalidDataException(
                    "Endless Conveyor fallback replay no longer reaches one request for '"
                    + entry.Law.SiteId + "'.");
            ValidateLegacyFallbackRequest(
                origin, probed.Run, matching[0], entry.Law.SiteId);
            if (matching[0].OwnerId != entry.Law.OwnerId ||
                matching[0].Ordinal != entry.Law.Ordinal ||
                matching[0].OperationKind != entry.Law.OperationKind ||
                matching[0].SupportCount != entry.Law.Outcomes.Length)
                throw new InvalidDataException(
                    "Endless Conveyor fallback request differs from its sampled law.");
            return matching[0];
        }).ToArray();
        var decisions = selections.Select((entry, index) =>
        {
            var decision = new RandomIndexDecisionV111(
                requests[index], entry.Selection.OutcomeIndex,
                "controlled-v111");
            decision.Validate();
            return ControlledStochasticDecisionV111.FromIndex(decision);
        }).ToArray();
        var requestSet = requests.Select(request =>
            ControlledStochasticRequestV111.FromIndex(request)).ToArray();
        var action = RequireAction(origin, actionId);
        var replay = ControlledChanceContinuationExecutorV111.CaptureStochastic(
            origin,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, action).State,
            request => request.IndexRequest is not null &&
                requestSet.Any(expected =>
                    ControlledStochasticChanceReplayV111.RequestsMatch(
                        request, expected)));
        if (replay.Suspended || replay.Run is not { } run)
            throw new InvalidDataException(
                "Endless Conveyor fallback action did not consume its complete Nature prefix.");

        for (var index = 0; index < selections.Length; index++)
        {
            var (law, selection) = selections[index];
            if (law.SiteId ==
                PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
            {
                ValidateLegacySelectedDish(
                    origin, probed.Run, run, law, selection);
            }
            else
            {
                ValidateSelectedEffect(origin, run, law, selection);
            }
        }
        return run;
    }

    private static RunState ReplayDishSelection(
        RunState origin, string actionId, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var conditioningRun = CaptureDishPreDrawState(
            origin, actionId, []);
        var request = BuildUnitFloatRequest(origin, law);
        var controlled = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, ControlledStochasticRequestV111.FromUnitFloat(request),
            selection, conditioningRun);
        var run = ReferenceNatureNativeEventIndexReplayV111.ReplayUnitFloat(
            origin,
            controlled.UnitFloatDecision! with
            {
                ProviderMode = "scripted-v111"
            },
            RequireAction(origin, actionId),
            PrototypeNativeEndlessConveyor.NativeRngEventId,
            PrototypeNativeEndlessConveyor.EventId,
            PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
            GetPreDrawDishCount(law));
        ValidateSelectedDish(run, selection);
        return run;
    }

    private static RunState ReplayIndexSelection(
        RunState origin, string actionId, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var request = BuildIndexRequest(origin, law);
        var controlledRequest =
            ControlledStochasticRequestV111.FromIndex(request);
        var selected = law.SiteId ==
                PrototypeNativePotionShop.UniformModelSiteId
            ? ReferenceNatureChanceLawDecisionV111.FromOrderedIndexSelection(
                law, controlledRequest, selection,
                law.Outcomes.Select(outcome => outcome.OutcomeId).ToArray())
            : ReferenceNatureChanceLawDecisionV111.FromSelection(
                law, controlledRequest, selection);
        var action = RequireAction(origin, actionId);
        var run = ReferenceNatureNativeEventIndexReplayV111
            .ReplayControlledIndex(
                origin, request, selected,
                checkpoint => new PrototypeGameEngine().Step(
                    checkpoint, action).State,
                PrototypeNativeEndlessConveyor.NativeRngEventId,
                PrototypeNativeEndlessConveyor.EventId);
        ValidateSelectedEffect(origin, run, law, selection);
        return run;
    }

    private static RandomIndexRequestV111 BuildIndexRequest(
        RunState origin, ChanceLawV111 law)
    {
        if (!ReferenceNatureEndlessConveyorSessionV111.SessionSiteIds.Contains(
                law.SiteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"Endless Conveyor index replay does not support site '{law.SiteId}'.");
        if (law.SiteId == PrototypeNativePotionShop.UniformModelSiteId)
        {
            var request = new RandomIndexRequestV111(
                law.SiteId, "reward", law.OwnerId, law.Ordinal,
                Enumerable.Range(0, law.Outcomes.Length).ToArray());
            request.Validate();
            return request;
        }
        if (origin.Stochastic.NativeRunRng is null)
        {
            var request = new RandomIndexRequestV111(
                law.SiteId, "event", law.OwnerId, law.Ordinal,
                Enumerable.Range(0, law.Outcomes.Length).ToArray());
            request.Validate();
            return request;
        }
        var stream = origin.World?.Event?.Stochastic.NativeRng
            ?? throw new InvalidDataException(
                "Endless Conveyor index replay lost its Native event cursor.");
        return new RandomIndexRequestV111(
            law.SiteId, stream.StreamId, law.OwnerId, law.Ordinal,
            Enumerable.Range(0, law.Outcomes.Length).ToArray());
    }

    private static RandomUnitFloatRequestV111 BuildUnitFloatRequest(
        RunState origin, ChanceLawV111 law) => new(
            law.SiteId,
            origin.World?.Event?.Stochastic.NativeRng?.StreamId ??
                NativeRngV111Contexts.ForEvent(
                    origin.RunSeed,
                    PrototypeNativeEndlessConveyor.NativeRngEventId).StreamId,
            law.OwnerId,
            law.Ordinal);

    private static void ValidateSelectedEffect(
        RunState origin, RunState run, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        if (law.SiteId == PrototypeNativeEndlessConveyor.UpgradeCardSiteId)
        {
            var candidates = PrototypeNativeEndlessConveyor
                .GetUpgradeCardCandidates(origin);
            var expectedOutcomeId = origin.Stochastic.NativeRunRng is null
                ? "choice_index:" + selection.OutcomeIndex
                : (uint)selection.OutcomeIndex < (uint)candidates.Length
                    ? $"card-instance:{candidates[selection.OutcomeIndex]}:index:{selection.OutcomeIndex}"
                    : string.Empty;
            if ((uint)selection.OutcomeIndex >= (uint)candidates.Length ||
                law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                    expectedOutcomeId)
                throw new InvalidDataException(
                    "Endless Conveyor upgrade law differs from its ordered card support.");
            var selectedInstanceId = candidates[selection.OutcomeIndex];
            var expectedDeck = origin.Player.Deck.Select(card =>
                card.InstanceId == selectedInstanceId
                    ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                    : card).ToArray();
            if (!run.Player.Deck.SequenceEqual(expectedDeck))
                throw new InvalidDataException(
                    "Scripted Endless Conveyor action upgraded a different card than its Nature outcome.");
            return;
        }

        if (law.SiteId == PrototypeNativeEndlessConveyor.ColorlessCardSiteId)
        {
            var candidates = PrototypeNativeEndlessConveyor
                .GetColorlessCardCandidates(origin);
            var expectedOutcomeId = origin.Stochastic.NativeRunRng is null
                ? "choice_index:" + selection.OutcomeIndex
                : (uint)selection.OutcomeIndex < (uint)candidates.Length
                    ? $"card-model:{candidates[selection.OutcomeIndex]}:index:{selection.OutcomeIndex}"
                    : string.Empty;
            if ((uint)selection.OutcomeIndex >= (uint)candidates.Length ||
                law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                    expectedOutcomeId)
                throw new InvalidDataException(
                    "Endless Conveyor Colorless law differs from its ordered card support.");
            var cardId = candidates[selection.OutcomeIndex];
            var world = origin.World
                ?? throw new InvalidDataException(
                    "Endless Conveyor Colorless replay lost its world.");
            if (run.Player.Deck.Length != origin.Player.Deck.Length + 1 ||
                run.Player.Deck[^1].InstanceId != world.NextCardInstanceId ||
                run.Player.Deck[^1].CardId != cardId)
                throw new InvalidDataException(
                    "Scripted Fried Eel added a different Colorless card than its Nature outcome.");
            return;
        }

        if (law.SiteId == PrototypeNativePotionShop.UniformModelSiteId)
        {
            var candidates = PrototypeNativePotionShop.NativePotionPoolOrder;
            var eventState = run.World?.Event;
            var expectedOutcomeId = origin.Stochastic.NativeRunRng is null
                ? "choice_index:" + selection.OutcomeIndex
                : (uint)selection.OutcomeIndex < (uint)candidates.Count
                    ? "potion:" + candidates[selection.OutcomeIndex]
                    : string.Empty;
            if ((uint)selection.OutcomeIndex >= (uint)candidates.Count ||
                law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                    expectedOutcomeId ||
                run.Phase != RunPhase.Event || eventState is null ||
                eventState.EventId != PrototypeNativeEndlessConveyor.EventId ||
                eventState.NativePageIndex != 1 ||
                eventState.ChosenChoiceId != "grab" ||
                eventState.Stochastic.RandomOutcomes.NativeDishId != "SUSPICIOUS_CONDIMENT" ||
                !eventState.Stochastic.RandomOutcomes.NativeDishRollPending ||
                eventState.PendingPotionReplacement?.PotionId !=
                    candidates[selection.OutcomeIndex] ||
                run.Player.Gold != origin.Player.Gold - 40 ||
                !run.Player.PotionSlots.SequenceEqual(
                    origin.Player.PotionSlots) ||
                CanonicalJson.Sha256(run.Stochastic.PrototypeRng) !=
                    CanonicalJson.Sha256(origin.Stochastic.PrototypeRng))
                throw new InvalidDataException(
                    "Scripted Suspicious Condiment action did not queue the Nature-selected potion without advancing the legacy provider.");
            return;
        }

        throw new InvalidDataException(
            "Unknown Endless Conveyor selected event effect site '" + law.SiteId + "'.");
    }

    private static void ValidateSelectedDish(
        RunState run, ReferenceNatureSelectionV111 selection)
    {
        if (!selection.OutcomeId.StartsWith("dish:", StringComparison.Ordinal) ||
            run.World?.Event?.Stochastic.RandomOutcomes.NativeDishId != selection.OutcomeId[5..])
            throw new InvalidDataException(
                "Scripted Endless Conveyor ticket resolved a different dish than its Nature outcome.");
    }

    private static void ValidateLegacySelectedDish(
        RunState origin, RunState probedRun, RunState run,
        ChanceLawV111 law, ReferenceNatureSelectionV111 selection)
    {
        var candidates = GetLegacyDishCandidates(origin, probedRun);
        if (law.OperationKind != "bounded_integer" ||
            (uint)selection.OutcomeIndex >= (uint)law.Outcomes.Length ||
            law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                "choice_index:" + selection.OutcomeIndex)
            throw new InvalidDataException(
                "Endless Conveyor fallback ticket outcome is outside its ordered support.");
        var ticket = selection.OutcomeIndex;
        string? expectedDish = null;
        foreach (var candidate in candidates)
        {
            if (ticket < candidate.Weight)
            {
                expectedDish = candidate.Id;
                break;
            }
            ticket -= candidate.Weight;
        }
        if (expectedDish is null ||
            run.World?.Event?.Stochastic.RandomOutcomes.NativeDishId != expectedDish)
            throw new InvalidDataException(
                "Scripted Endless Conveyor fallback ticket resolved a different dish.");
    }

    private static RunState CaptureDishPreDrawState(
        RunState origin, string actionId,
        ControlledStochasticDecisionV111[] prefix)
    {
        var capture = CapturePendingRequest(
            origin, actionId,
            PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
            prefix);
        return capture.UnitFloatCapture?.PreDrawState
            ?? throw new InvalidDataException(
                "Endless Conveyor action lost its weighted-ticket pre-draw state.");
    }

    private static int GetPreDrawDishCount(ChanceLawV111 law)
    {
        var parts = law.OwnerId.Split(':');
        if (parts.Length != 4 || parts[0] != "dish" ||
            !int.TryParse(parts[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var nextRoll) || nextRoll <= 0)
            throw new InvalidDataException(
                "Endless Conveyor law has a malformed dish owner identity.");
        return nextRoll - 1;
    }
}
