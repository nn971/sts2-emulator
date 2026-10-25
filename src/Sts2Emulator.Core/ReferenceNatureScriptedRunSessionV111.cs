using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureScriptedRunActionRecordV111(
    GameAction Action,
    ControlledStochasticDecisionV111[] Decisions,
    ReferenceNatureSelectionV111?[] NatureDecisions)
{
    private GameAction? _action = CopyAction(Action);
    private ControlledStochasticDecisionV111[]? _decisions =
        CopyDecisions(Decisions);
    private ReferenceNatureSelectionV111?[]? _natureDecisions =
        CopyNatureDecisions(NatureDecisions);

    public GameAction Action
    {
        get => CopyAction(_action) ?? throw new InvalidDataException(
            "ReferenceNature run history has no action.");
        init => _action = CopyAction(value);
    }

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => CopyDecisions(_decisions)!;
        init => _decisions = CopyDecisions(value);
    }

    public ReferenceNatureSelectionV111?[] NatureDecisions
    {
        get => CopyNatureDecisions(_natureDecisions)!;
        init => _natureDecisions = CopyNatureDecisions(value);
    }

    public ReferenceNatureScriptedRunActionRecordV111 Fork() => this with
    {
        Action = _action!,
        Decisions = _decisions!,
        NatureDecisions = _natureDecisions!
    };

    public void Validate()
    {
        if (_action is null || string.IsNullOrWhiteSpace(_action.Kind) ||
            _decisions is null || _natureDecisions is null ||
            _decisions.Length != _natureDecisions.Length)
            throw new InvalidDataException(
                "Invalid ReferenceNature run action record.");
        foreach (var decision in _decisions)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "ReferenceNature run action has a null provider decision.");
            decision.Validate();
        }
        foreach (var decision in _natureDecisions)
            if (decision is not null)
                ValidateNatureDecision(decision);
    }

    internal static void ValidateNatureDecision(
        ReferenceNatureSelectionV111? decision)
    {
        if (decision is null || string.IsNullOrWhiteSpace(decision.LawId) ||
            string.IsNullOrWhiteSpace(decision.EventId) ||
            string.IsNullOrWhiteSpace(decision.OutcomeId) ||
            decision.OutcomeIndex < 0 || decision.NextState is null)
            throw new InvalidDataException(
                "ReferenceNature run transcript contains an invalid selection.");
        decision.NextState.Validate();
    }

    private static GameAction? CopyAction(GameAction? action) => action is null
        ? null
        : action with { Payload = action.Payload.Clone() };

    private static ControlledStochasticDecisionV111[]? CopyDecisions(
        ControlledStochasticDecisionV111[]? decisions) => decisions?
            .Select(decision => decision?.Fork()!).ToArray();

    private static ReferenceNatureSelectionV111?[]? CopyNatureDecisions(
        ReferenceNatureSelectionV111?[]? decisions) => decisions?
            .Select(decision => decision is null ? null : decision with
            {
                NextState = decision.NextState.Fork()
            }).ToArray();
}

public sealed record ReferenceNatureScriptedRunMechanicsStateV111
{
    private RunState? _origin;
    private RunState? _run;

    public RunState Origin
    {
        get => _origin?.Fork() ?? throw new InvalidDataException(
            "ReferenceNature whole-run mechanics have no origin.");
        init => _origin = value?.Fork();
    }

    public RunState Run
    {
        get => _run?.Fork() ?? throw new InvalidDataException(
            "ReferenceNature whole-run mechanics have no current run.");
        init => _run = value?.Fork();
    }

    public ReferenceNatureScriptedRunMechanicsStateV111(
        RunState Origin, RunState Run)
    {
        this.Origin = Origin;
        this.Run = Run;
    }

    public ReferenceNatureScriptedRunMechanicsStateV111 Fork() => this with
    {
        Origin = Origin,
        Run = Run
    };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Origin);
        ArgumentNullException.ThrowIfNull(Run);
        RunConfiguration.ValidateState(Origin);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
    }
}

public sealed record ReferenceNatureScriptedRunStochasticStateV111(
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureScriptedRunActionRecordV111[] CompletedActions,
    ScriptedRunActionSessionV111? ActiveAction,
    ReferenceNatureSelectionV111?[] ActiveNatureDecisions)
{
    public ReferenceNatureScriptedRunActionRecordV111[] CompletedActions
    {
        get => CopyActions(_completedActions)!;
        init => _completedActions = CopyActions(value);
    }

    private ReferenceNatureScriptedRunActionRecordV111[]? _completedActions =
        CopyActions(CompletedActions);

    public ScriptedRunActionSessionV111? ActiveAction
    {
        get => _activeAction?.Fork();
        init => _activeAction = value?.Fork();
    }

    private ScriptedRunActionSessionV111? _activeAction = ActiveAction?.Fork();

    public ReferenceNatureSelectionV111?[] ActiveNatureDecisions
    {
        get => CopyNatureDecisions(_activeNatureDecisions)!;
        init => _activeNatureDecisions = CopyNatureDecisions(value);
    }

    private ReferenceNatureSelectionV111?[]? _activeNatureDecisions =
        CopyNatureDecisions(ActiveNatureDecisions);

    public ReferenceNatureScriptedRunStochasticStateV111 Fork() => this with
    {
        InitialNatureState = InitialNatureState.Fork(),
        NatureState = NatureState.Fork(),
        CompletedActions = CompletedActions,
        ActiveAction = ActiveAction,
        ActiveNatureDecisions = ActiveNatureDecisions
    };

    public void Validate()
    {
        InitialNatureState.Validate();
        NatureState.Validate();
        var completed = CompletedActions;
        var active = ActiveNatureDecisions;
        if (completed is null || active is null ||
            completed.Any(action => action is null) ||
            (ActiveAction is null && active.Length != 0) ||
            ActiveAction is { } activeAction &&
                active.Length != activeAction.Decisions.Length)
            throw new InvalidDataException(
                "Invalid ReferenceNature whole-run stochastic state.");
        foreach (var action in completed)
            action.Validate();
        foreach (var decision in active)
            if (decision is not null)
                ReferenceNatureScriptedRunActionRecordV111
                    .ValidateNatureDecision(decision);
        ActiveAction?.Validate();
    }

    private static ReferenceNatureScriptedRunActionRecordV111[]? CopyActions(
        ReferenceNatureScriptedRunActionRecordV111[]? actions) => actions?
            .Select(action => action?.Fork()!).ToArray();

    private static ReferenceNatureSelectionV111?[]? CopyNatureDecisions(
        ReferenceNatureSelectionV111?[]? decisions) => decisions?
            .Select(decision => decision is null ? null : decision with
            {
                NextState = decision.NextState.Fork()
            }).ToArray();
}

public sealed record ReferenceNatureScriptedRunSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureScriptedRunActionRecordV111[] CompletedActions,
    string? ActiveActionSnapshotJson,
    ReferenceNatureSelectionV111?[] ActiveNatureDecisions)
{
    private ReferenceNatureScriptedRunActionRecordV111[]? _completedActions =
        CopyActions(CompletedActions);
    private ReferenceNatureSelectionV111?[]? _activeNatureDecisions =
        CopyDecisions(ActiveNatureDecisions);

    public ReferenceNatureScriptedRunActionRecordV111[] CompletedActions
    {
        get => CopyActions(_completedActions)!;
        init => _completedActions = CopyActions(value);
    }

    public ReferenceNatureSelectionV111?[] ActiveNatureDecisions
    {
        get => CopyDecisions(_activeNatureDecisions)!;
        init => _activeNatureDecisions = CopyDecisions(value);
    }

    private static ReferenceNatureScriptedRunActionRecordV111[]? CopyActions(
        ReferenceNatureScriptedRunActionRecordV111[]? actions) => actions?
            .Select(action => action?.Fork()!).ToArray();

    private static ReferenceNatureSelectionV111?[]? CopyDecisions(
        ReferenceNatureSelectionV111?[]? decisions) => decisions?
            .Select(decision => decision is null ? null : decision with
            {
                NextState = decision.NextState.Fork()
            }).ToArray();
}

/// <summary>
/// Whole-run ReferenceNature session for ordinary legal actions. Finite-index
/// boundaries, registered Native map quota Gaussians, selected Native event
/// gold, card-rarity, combat-potion odds, card-upgrade, and Endless Conveyor
/// floats, PotionFactory rarity, Native merchant prices, and the inert
/// merchant upgrade roll can be sampled by ReferenceNature;
/// any of the six typed boundaries can instead be resolved from a caller-supplied
/// transcript.
/// </summary>
public sealed record ReferenceNatureScriptedRunSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureScriptedRunActionRecordV111[] CompletedActions,
    ScriptedRunActionSessionV111? ActiveAction,
    ReferenceNatureSelectionV111?[] ActiveNatureDecisions)
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureScriptedRunMechanicsStateV111,
        ReferenceNatureScriptedRunStochasticStateV111> Simulation { get; init; } =
        new(
            new ReferenceNatureScriptedRunMechanicsStateV111(Origin, Run),
            new ReferenceNatureScriptedRunStochasticStateV111(
                InitialNatureState, NatureState, CompletedActions,
                ActiveAction, ActiveNatureDecisions));

    [JsonIgnore]
    public ReferenceNatureScriptedRunMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureScriptedRunStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin;
        init => Mechanics = Mechanics with { Origin = value };
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

    public ReferenceNatureScriptedRunActionRecordV111[] CompletedActions
    {
        get => Stochastic.CompletedActions;
        init => Stochastic = Stochastic with { CompletedActions = value };
    }

    public ScriptedRunActionSessionV111? ActiveAction
    {
        get => Stochastic.ActiveAction;
        init => Stochastic = Stochastic with { ActiveAction = value };
    }

    public ReferenceNatureSelectionV111?[] ActiveNatureDecisions
    {
        get => Stochastic.ActiveNatureDecisions;
        init => Stochastic = Stochastic with
        {
            ActiveNatureDecisions = value
        };
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-scripted-run-session-v2";
    public const string LegacySchemaId =
        "sts2-v111-reference-nature-scripted-run-session-v1";

    public static string[] Operations =>
    [
        "reference_nature_scripted_run_begin",
        "reference_nature_scripted_run_status",
        "reference_nature_scripted_run_begin_action",
        "reference_nature_scripted_run_resolve_next",
        "reference_nature_scripted_run_resolve_outcome",
        "reference_nature_scripted_run_fork",
        "reference_nature_scripted_run_save",
        "reference_nature_scripted_run_load",
        "reference_nature_scripted_run_release_many"
    ];

    [JsonIgnore]
    public bool Complete => ActiveAction is null &&
        Run.Phase == RunPhase.Terminal;

    [JsonIgnore]
    public ControlledStochasticRequestV111? PendingRequest =>
        ActiveAction?.PendingRequest;

    [JsonIgnore]
    public string[]? PendingCandidateLabels =>
        ActiveAction?.PendingCandidateLabels;

    [JsonIgnore]
    public bool RequiresScriptedOutcome =>
        ActiveAction?.PendingRequest is not null && PendingLaw is null;

    [JsonIgnore]
    public string? PendingOperationKind => ActiveAction?.PendingRequest is
        { } pending
            ? pending.IndexRequest is not null ? "index" :
                pending.UnitFloatRequest is not null ? "unit_float" :
                pending.RangedFloatRequest is not null ? "ranged_float" :
                pending.RangedDoubleRequest is not null ? "ranged_double" :
                pending.GaussianIntRequest is not null ? "gaussian_int" :
                pending.GaussianDoubleRequest is not null ? "gaussian_double" :
                "unknown"
            : null;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => ActiveAction is { } active &&
        active.PendingRequest is { } request
            ? QueryNatureLaw(active, request)
            : null;

    [JsonIgnore]
    public IReadOnlyList<GameAction> LegalActions =>
        ActiveAction is null && !Complete &&
            Run.Stochastic.ControlledEventRewardChoice is null &&
            Run.Stochastic.ControlledRunActionChoice is null &&
            Run.World?.Event?.Stochastic.ControlledChoice is null
            ? new PrototypeGameEngine().GetLegalActions(Run)
            : Array.Empty<GameAction>();

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ReferenceNatureScriptedRunSessionV111 Begin(
        RunState origin, ulong? natureSeed)
    {
        ArgumentNullException.ThrowIfNull(origin);
        RunConfiguration.ValidateState(origin);
        var cursor = ReferenceNatureStateV111.ForRunSession(
            origin, natureSeed);
        var natureOrigin = ReferenceNatureRunCursorV111.Commit(
            origin.Fork(), cursor);
        var session = new ReferenceNatureScriptedRunSessionV111(
            CurrentSchemaId, natureOrigin.Fork(), natureOrigin.Fork(),
            cursor.Fork(),
            cursor.Fork(), [], null, []);
        session.Validate();
        return session;
    }

    public ReferenceNatureScriptedRunSessionV111 BeginAction(
        string fingerprint, GameAction action)
    {
        Validate();
        if (Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale ReferenceNature whole-run fingerprint.");
        ArgumentNullException.ThrowIfNull(action);
        if (ActiveAction is not null)
            throw new InvalidOperationException(
                "Resolve the active ReferenceNature action first.");
        if (Complete)
            throw new InvalidOperationException(
                "A terminal ReferenceNature run has no next action.");
        var active = ScriptedRunActionSessionV111.Begin(
            Run, CloneAction(action));
        if (active.Complete)
            return CommitAction(this, active, []);
        var next = this with { ActiveAction = active };
        next.Validate();
        return next;
    }

    public ReferenceNatureScriptedRunSessionV111 ResolveNext(
        string fingerprint)
    {
        Validate();
        if (Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale ReferenceNature whole-run fingerprint.");
        var active = ActiveAction ?? throw new InvalidOperationException(
            "No ReferenceNature whole-run action is active.");
        var request = active.PendingRequest ?? throw new InvalidOperationException(
            "No stochastic request is pending for the active action.");
        var law = QueryNatureLaw(active, request) ??
            throw new NotSupportedException(
                $"Whole-run request {request.SiteId} " +
                $"({PendingOperationKind}) requires a caller-supplied typed outcome.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var outcome = CreateNatureOutcome(active, request, law, selection);
        return ResolveOutcomeCore(fingerprint, outcome, selection);
    }

    public ReferenceNatureScriptedRunSessionV111 ResolveOutcome(
        string fingerprint, ScriptedRunActionOutcomeV111 outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return ResolveOutcomeCore(fingerprint, outcome, null);
    }

    private ReferenceNatureScriptedRunSessionV111 ResolveOutcomeCore(
        string fingerprint, ScriptedRunActionOutcomeV111 outcome,
        ReferenceNatureSelectionV111? natureSelection)
    {
        Validate();
        if (Fingerprint != fingerprint)
            throw new InvalidOperationException(
                "Stale ReferenceNature whole-run fingerprint.");
        var active = ActiveAction ?? throw new InvalidOperationException(
            "No ReferenceNature whole-run action is active.");
        var request = active.PendingRequest ?? throw new InvalidOperationException(
            "No stochastic request is pending for the active action.");
        outcome.Validate();
        var decision = outcome.ToDecision();
        if (CanonicalJson.Sha256(decision.ToRequest()) !=
                CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Whole-run typed outcome does not match the pending request.");
        if (natureSelection is not null)
        {
            var law = QueryNatureLaw(active, request) ??
                throw new InvalidDataException(
                    "Nature selection has no supported whole-run Chance Law.");
            var expected = CreateNatureOutcome(
                active, request, law, natureSelection).ToDecision();
            if (CanonicalJson.Sha256(expected) !=
                CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "Whole-run Nature outcome differs from its sampled Chance Law.");
        }
        var resolved = active.ResolveNext(outcome);
        var natureDecisions = ActiveNatureDecisions
            .Append(natureSelection is null ? null :
                natureSelection with
                {
                    NextState = natureSelection.NextState.Fork()
                }).ToArray();
        var cursor = natureSelection?.NextState ?? NatureState;
        if (resolved.Complete)
            return CommitAction(this, resolved, natureDecisions, cursor);
        var next = this with
        {
            ActiveAction = resolved,
            NatureState = cursor,
            ActiveNatureDecisions = natureDecisions
        };
        next.Validate();
        return next;
    }

    // Non-index Nature laws are admitted only for registered map quota
    // Gaussians and the supported Native unit-float mappings below; other
    // typed primitives remain caller-scripted.
    internal static ChanceLawV111? QueryNatureLaw(
        ScriptedRunActionSessionV111 active,
        ControlledStochasticRequestV111 request)
    {
        if (request.IndexRequest is { } indexRequest)
        {
            if (ChanceLawEngineV111.IsNativeMapGenerationRequestSite(
                    indexRequest.SiteId))
                return ChanceLawEngineV111.QueryNativeMapRequest(request);
            if (indexRequest.SiteId == StochasticSiteRegistryV111
                    .NativeCombatGoldSiteId)
                return QueryNativeCombatGold(active, indexRequest);
            if (indexRequest.SiteId is
                    ChanceLawEngineV111.NativeStoneOfAllTimePushTicketSiteId or
                    ChanceLawEngineV111.NativeStoneOfAllTimeLiftTicketSiteId)
                return QueryNativeStoneOfAllTimeTicket(
                    active, indexRequest);
            if (indexRequest.SiteId == ChanceLawEngineV111
                    .NativeEventGoldIndexSiteId)
                return QueryNativeEventGoldIndex(active, indexRequest);
            if (indexRequest.SiteId is
                ChanceLawEngineV111.NativeStoneOfAllTimePotionSlotSiteId or
                ChanceLawEngineV111.NativeFutureOfPotionsCardTypeSiteId)
                return QueryNativeEventEntryIndex(active, indexRequest);
            if (indexRequest.SiteId == StochasticSiteRegistryV111
                    .NativeGloryTrialDamageSiteId)
            {
                var plan = TrialCaseChoicePlanBuilderV111.Build(
                    active.Origin);
                if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                        plan.Request, indexRequest))
                    throw new InvalidDataException(
                        "Glory Trial request differs from its saved case-choice boundary.");
                return ChanceLawEngineV111.QueryTrial(active.Origin);
            }
            if (indexRequest.SiteId == StochasticSiteRegistryV111
                    .NativePotionFactoryModelSiteId)
                return QueryNativePotionFactoryModel(active, indexRequest);
            return ChanceLawEngineV111.QueryRandomIndex(
                PendingEventId(active, indexRequest), indexRequest);
        }
        if (request.UnitFloatRequest is { } unitFloatRequest)
        {
            if (QueryNativeMerchantPrice(active, unitFloatRequest) is
                { } merchantPriceLaw)
                return merchantPriceLaw;
            if (unitFloatRequest.SiteId ==
                PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
                return QueryEndlessConveyorDish(active, unitFloatRequest);
            if (QueryNativeCardRarity(active, unitFloatRequest) is
                { } cardRarityLaw)
                return cardRarityLaw;
            if (QueryNativeEncounterCardUpgrade(active, unitFloatRequest) is
                { } cardUpgradeLaw)
                return cardUpgradeLaw;
            if (unitFloatRequest.SiteId ==
                StochasticSiteRegistryV111.NativePotionFactoryRarityRollSiteId)
                return ChanceLawEngineV111.QueryNativePotionFactoryRarity(
                    unitFloatRequest);
            if (unitFloatRequest.SiteId ==
                    StochasticSiteRegistryV111.RelicGrabBagRaritySiteId &&
                unitFloatRequest.StreamId ==
                    NativePlayerRngV111.RewardsStreamId)
                return ChanceLawEngineV111.QueryNativeRelicRarity(
                    unitFloatRequest);
            if (QueryNativeCombatPotionOdds(active, unitFloatRequest) is
                { } combatPotionOddsLaw)
                return combatPotionOddsLaw;
            if (unitFloatRequest.SiteId ==
                    ChanceLawEngineV111.NativeEventGoldUnitFloatSiteId &&
                QueryNativeEventGold(active, unitFloatRequest) is { } eventGoldLaw)
                return eventGoldLaw;
            if (unitFloatRequest.SiteId ==
                    "shop.merchant.card_upgrade_roll.unit_float" &&
                unitFloatRequest.StreamId == NativePlayerRngV111.ShopsStreamId &&
                active.Origin.Stochastic.NativeRunRng is not null)
                return ChanceLawEngineV111
                    .QueryNativeMerchantNoEffectUnitFloat(
                        active.Origin, unitFloatRequest);
        }
        if (request.GaussianIntRequest is { } gaussianRequest &&
            ChanceLawEngineV111.MatchesNativeMapGaussianIntRequest(
                gaussianRequest))
            return ChanceLawEngineV111.QueryNativeMapRequest(request);
        return null;
    }

    private static ChanceLawV111 QueryNativeEventEntryIndex(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request)
    {
        request.Validate();
        var eventId = request.SiteId switch
        {
            var siteId when siteId == ChanceLawEngineV111
                .NativeStoneOfAllTimePotionSlotSiteId =>
                PrototypeNativeStoneOfAllTime.EventId,
            var siteId when siteId == ChanceLawEngineV111
                .NativeFutureOfPotionsCardTypeSiteId =>
                PrototypeNativeFutureOfPotions.EventId,
            _ => throw new InvalidDataException(
                "Native Event-entry request has no declared event branch.")
        };
        var law = ReferenceNatureNativeEventGoldSessionV111
            .QueryLawForCapture(
                active.Origin,
                PrototypeAiEnvironment.StableActionId(active.Action),
                eventId,
                new ControlledStochasticRequestV111(request));
        var expected = new RandomIndexRequestV111(
            law.SiteId, request.StreamId, law.OwnerId, law.Ordinal,
            Enumerable.Range(0, law.Outcomes.Length).ToArray(),
            OperationKind: law.OperationKind);
        if (CanonicalJson.Sha256(expected) !=
            CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Native Event-entry request differs from its source law.");
        return law;
    }

    private static ChanceLawV111 QueryNativeCombatGold(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request)
    {
        request.Validate();
        var run = active.Origin;
        if (request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OperationKind != "bounded_integer" ||
            run.Phase != RunPhase.Combat ||
            run.World is not { ActiveRoom: { } room } ||
            room is not (PrototypeRoomType.Combat or
                PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new InvalidDataException(
                "Native combat-gold request differs from its active reward boundary.");

        var law = ChanceLawEngineV111.QueryNativeCombatGold(
            room, run.Ascension,
            room == PrototypeRoomType.Combat ? 1f : null,
            request.Ordinal);
        var expected = new RandomIndexRequestV111(
            law.SiteId, NativePlayerRngV111.RewardsStreamId,
            law.OwnerId, law.Ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: law.SupportCount);
        if (CanonicalJson.Sha256(expected) !=
            CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Native combat-gold request differs from its source law.");
        return law;
    }

    private static ChanceLawV111 QueryNativeStoneOfAllTimeTicket(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request)
    {
        request.Validate();
        if (active.Action.Kind != "event_choice")
            throw new InvalidDataException(
                "Stone of All Time ticket request is outside its event choice action.");
        var actionId = active.Action
            .ReadPayload<EventChoicePayload>().ChoiceId;
        var law = ChanceLawEngineV111
            .QueryNativeStoneOfAllTimeNoEffectIndex(
                active.Origin, request.SiteId, request.Ordinal, actionId);
        var expectedStream = active.Origin.Stochastic.NativeRunRng is not null
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId)
            : "event";
        if (request.StreamId != expectedStream ||
            request.OperationKind != "bounded_integer" ||
            request.OwnerId != law.OwnerId ||
            request.Ordinal != law.Ordinal || request.BoundedCount != 100)
            throw new InvalidDataException(
                "Stone of All Time ticket request differs from its event law.");
        return law;
    }

    private static ChanceLawV111 QueryNativeEventGoldIndex(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request)
    {
        request.Validate();
        var eventId = NativeEventGoldEventId(request.OwnerId) ??
            throw new InvalidDataException(
                "Native event-gold index owner has no declared event branch.");
        var actionId = PrototypeAiEnvironment.StableActionId(active.Action);
        var origin = active.Origin;
        if (origin.Stochastic.NativeRunRng is null)
        {
            if (request.StreamId != "event" ||
                origin.World is not { } legacyWorld ||
                !PrototypeGameEngine.UsesNativeRegionalSystems(legacyWorld))
                throw new InvalidDataException(
                    "Legacy event-gold index request has no Native-regional event stream.");
            return ChanceLawEngineV111
                .QueryLegacyNativeRegionalEventGoldIndex(
                    origin, request, actionId, eventId);
        }

        var law = ChanceLawEngineV111.QueryNativeEventGoldIndex(
            origin, request.SiteId, request.Ordinal, actionId, eventId);
        var expectedStream = NativeRngV111Contexts.EventStreamId(
            ReferenceNatureNativeEventGoldSessionV111.NativeEventEntry(
                eventId));
        if (request.StreamId != expectedStream ||
            request.OperationKind != "bounded_integer" ||
            request.OwnerId != law.OwnerId ||
            request.Ordinal != law.Ordinal ||
            request.BoundedCount != law.SupportCount)
            throw new InvalidDataException(
                "Native event-gold index request differs from its source law.");
        return law;
    }

    private static ChanceLawV111 QueryNativePotionFactoryModel(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request)
    {
        var excludedPotionIds = new List<string>();
        PrototypeNativePotionRarity? pendingRarity = null;
        foreach (var decision in active.Decisions)
        {
            if (decision.UnitFloatDecision is { } rarityDecision &&
                rarityDecision.Request.SiteId == StochasticSiteRegistryV111
                    .NativePotionFactoryRarityRollSiteId &&
                rarityDecision.Request.StreamId == request.StreamId &&
                rarityDecision.Request.OwnerId == request.OwnerId)
            {
                if (pendingRarity is not null)
                    throw new InvalidDataException(
                        "Native PotionFactory model prefix has an unresolved rarity outcome.");
                if (rarityDecision.Request.Ordinal !=
                    excludedPotionIds.Count)
                    throw new InvalidDataException(
                        "Native PotionFactory rarity ordinal differs from its model prefix.");
                pendingRarity = PrototypeNativePotionShop
                    .ResolveRarityNative(rarityDecision.SelectedValue);
            }
            else if (decision.IndexDecision is { } modelDecision &&
                     modelDecision.Request.SiteId == request.SiteId &&
                     modelDecision.Request.StreamId == request.StreamId &&
                     modelDecision.Request.OwnerId == request.OwnerId)
            {
                if (pendingRarity is not { } rarity)
                    throw new InvalidDataException(
                        "Native PotionFactory model prefix has no preceding rarity outcome.");
                var previousLaw = ChanceLawEngineV111
                    .QueryNativePotionFactoryModelIndex(
                        modelDecision.Request, rarity,
                        excludedPotionIds.ToArray());
                var selectedIndex = modelDecision.SelectedIndex;
                if (selectedIndex < 0 ||
                    selectedIndex >= previousLaw.SupportCount)
                    throw new InvalidDataException(
                        "Native PotionFactory model prefix selected outside its remaining pool.");
                var potionId = previousLaw.OutcomeIdAt(selectedIndex);
                const string prefix = "potion_model:";
                var marker = potionId.IndexOf(":index:",
                    StringComparison.Ordinal);
                if (!potionId.StartsWith(prefix, StringComparison.Ordinal) ||
                    marker <= prefix.Length)
                    throw new InvalidDataException(
                        "Native PotionFactory model law has a malformed potion identity.");
                excludedPotionIds.Add(
                    potionId[prefix.Length..marker]);
                pendingRarity = null;
            }
        }
        if (pendingRarity is not { } currentRarity)
            throw new InvalidDataException(
                "Native PotionFactory model request has no unresolved rarity outcome.");
        return ChanceLawEngineV111.QueryNativePotionFactoryModelIndex(
            request, currentRarity, excludedPotionIds.ToArray());
    }

    private static ChanceLawV111? QueryNativeMerchantPrice(
        ScriptedRunActionSessionV111 active,
        RandomUnitFloatRequestV111 request)
    {
        if (!StochasticSiteRegistryV111.IsNativeMerchantPriceSite(
                request.SiteId) ||
            request.StreamId != NativePlayerRngV111.ShopsStreamId)
            return null;

        var owner = request.OwnerId.Split(':');
        if (owner.Length < 6 || owner[0] != "act" ||
            !int.TryParse(owner[1], out var act) || owner[2] != "floor" ||
            !int.TryParse(owner[3], out var floor))
            throw new InvalidDataException(
                "Native merchant price owner is malformed.");
        var run = active.Origin;
        if (run.World is not { } world || world.Act != act ||
            world.Floor != floor)
            throw new InvalidDataException(
                "Native merchant price owner differs from its saved Shop boundary.");

        int ordinal;
        string? cardId = null;
        string? potionId = null;
        string? relicId = null;
        var colorlessOffer = false;
        var saleReprice = false;
        if (owner[4] == "colorless_offer" && owner.Length >= 8 &&
            owner[6] == "card" &&
            int.TryParse(owner[5], out ordinal))
        {
            cardId = string.Join(":", owner[7..]);
            colorlessOffer = true;
        }
        else if (owner[4] == "offer" && owner.Length >= 8 &&
            int.TryParse(owner[5], out ordinal))
        {
            switch (owner[6])
            {
                case "card":
                    cardId = string.Join(":", owner[7..]);
                    break;
                case "sale_reprice":
                    cardId = string.Join(":", owner[7..]);
                    saleReprice = true;
                    break;
                case "potion":
                    potionId = RepeatedMerchantItem(owner, 7);
                    break;
                case "relic":
                    relicId = RepeatedMerchantItem(owner, 7);
                    break;
                default:
                    throw new InvalidDataException(
                        "Native merchant price owner has an unsupported item kind.");
            }
        }
        else
            throw new InvalidDataException(
                "Native merchant price owner has an unsupported offer boundary.");

        var law = ChanceLawEngineV111.QueryNativeMerchantPrice(
            run, request.SiteId, ordinal, cardId, potionId, relicId,
            colorlessOffer, saleReprice);
        var expected = new RandomUnitFloatRequestV111(
            request.SiteId, NativePlayerRngV111.ShopsStreamId,
            law.OwnerId, ordinal);
        if (ordinal != request.Ordinal ||
            CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Native merchant price request differs from its source law.");
        return law;
    }

    private static string RepeatedMerchantItem(string[] owner, int start)
    {
        var suffix = string.Join(":", owner[start..]);
        for (var index = 1; index < suffix.Length; index++)
        {
            if (suffix[index] == ':' &&
                suffix[..index] == suffix[(index + 1)..])
                return suffix[..index];
        }
        throw new InvalidDataException(
            "Native merchant price owner does not repeat its item identity.");
    }

    private static ChanceLawV111 QueryEndlessConveyorDish(
        ScriptedRunActionSessionV111 active,
        RandomUnitFloatRequestV111 request)
    {
        var law = ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry(
            active.Origin,
            PrototypeAiEnvironment.StableActionId(active.Action),
            request.Ordinal);
        var streamId = active.Origin.World?.Event?.Stochastic.NativeRng?
            .StreamId ?? throw new InvalidDataException(
                "Endless Conveyor event boundary has no Native event stream.");
        var expected = new RandomUnitFloatRequestV111(
            PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
            streamId, law.OwnerId, law.Ordinal);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Endless Conveyor dish request differs from its event, stream, or owner.");
        return law;
    }

    private static ChanceLawV111? QueryNativeCardRarity(
        ScriptedRunActionSessionV111 active,
        RandomUnitFloatRequestV111 request)
    {
        if (request.SiteId != StochasticSiteRegistryV111
                .CardRarityRollUnitFloatSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId)
            return null;

        ChanceLawV111 law;
        if (request.OwnerId.StartsWith("type:", StringComparison.Ordinal))
        {
            var owner = request.OwnerId.Split(':');
            if (owner.Length != 6 || owner[2] != "offer" ||
                !int.TryParse(owner[3], out var offerOrdinal) ||
                owner[4] != "offset" ||
                !int.TryParse(owner[5], out _) ||
                offerOrdinal != request.Ordinal)
                throw new InvalidDataException(
                    "Native merchant card-rarity owner is malformed.");
            law = ChanceLawEngineV111.QueryNativeMerchantOffer(
                active.Origin, request.SiteId, offerOrdinal,
                cardType: owner[1]);
        }
        else
        {
            var owner = request.OwnerId.Split(':');
            if (owner.Length != 8 || owner[0] != "act" ||
                !int.TryParse(owner[1], out var act) ||
                owner[2] != "room" ||
                !Enum.TryParse<PrototypeRoomType>(owner[3], out var room) ||
                owner[4] != "pick" ||
                !int.TryParse(owner[5], out _) || owner[6] != "offset" ||
                !int.TryParse(owner[7], out var offset))
                throw new InvalidDataException(
                    "Native reward card-rarity owner is malformed.");
            var run = active.Origin;
            if (run.Phase != RunPhase.Combat ||
                run.World is not { ActiveRoom: { } activeRoom } world ||
                world.Act != act || activeRoom != room)
                throw new InvalidDataException(
                    "Native reward card-rarity request differs from its combat boundary.");
            var source = room switch
            {
                PrototypeRoomType.Combat =>
                    PrototypeNativeCardOddsSource.RegularEncounter,
                PrototypeRoomType.Elite =>
                    PrototypeNativeCardOddsSource.EliteEncounter,
                PrototypeRoomType.Boss =>
                    PrototypeNativeCardOddsSource.BossEncounter,
                _ => throw new InvalidDataException(
                    "Native reward card-rarity owner has an invalid room.")
            };
            law = ChanceLawEngineV111.QueryNativeCardRarity(
                source, offset, run.Ascension, request.OwnerId,
                request.Ordinal);
        }

        var expected = new RandomUnitFloatRequestV111(
            request.SiteId, NativePlayerRngV111.RewardsStreamId,
            law.OwnerId, request.Ordinal);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Native card-rarity request differs from its source law.");
        return law;
    }

    private static ChanceLawV111? QueryNativeEncounterCardUpgrade(
        ScriptedRunActionSessionV111 active,
        RandomUnitFloatRequestV111 request)
    {
        if (request.SiteId !=
                StochasticSiteRegistryV111.CardRarityUpgradeRollUnitFloatSiteId ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId)
            return null;
        var owner = request.OwnerId.Split(':');
        if (owner.Length < 6 || owner[0] != "card" ||
            !int.TryParse(owner[^3], out var act) || owner[^4] != "act" ||
            owner[^2] != "ascension" ||
            !int.TryParse(owner[^1], out var ascension))
            throw new InvalidDataException(
                "Native card-upgrade request owner is malformed.");
        var cardId = string.Join(":", owner[1..^4]);
        var run = active.Origin;
        if (run.Phase != RunPhase.Combat ||
            run.World is not { ActiveRoom: not null } world ||
            act != world.Act || ascension != run.Ascension)
            throw new InvalidDataException(
                "Native card-upgrade request differs from its combat reward boundary.");
        var law = ChanceLawEngineV111.QueryNativeEncounterCardUpgrade(
            cardId, act, ascension, request.Ordinal);
        var expected = new RandomUnitFloatRequestV111(
            request.SiteId, NativePlayerRngV111.RewardsStreamId,
            law.OwnerId, request.Ordinal);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Native card-upgrade request differs from its source law.");
        return law;
    }

    private static ChanceLawV111? QueryNativeCombatPotionOdds(
        ScriptedRunActionSessionV111 active,
        RandomUnitFloatRequestV111 request)
    {
        if (request.SiteId != "reward.potion_odds.unit_float" ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OwnerId != "run:potion_reward_odds")
            return null;
        var run = active.Origin;
        if (run.Phase != RunPhase.Combat ||
            run.World is not { ActiveRoom: { } room } world)
            throw new InvalidDataException(
                "Native combat-potion odds request is outside its combat action boundary.");
        var forced = run.Player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId).ForceCombatPotionReward);
        return ChanceLawEngineV111.QueryNativePotionRewardOdds(
            world.PotionRewardOddsThousandths,
            room == PrototypeRoomType.Elite, forced, request.Ordinal);
    }

    private static ChanceLawV111? QueryNativeEventGold(
        ScriptedRunActionSessionV111 active,
        RandomUnitFloatRequestV111 request)
    {
        var eventId = NativeEventGoldEventId(request.OwnerId);
        if (eventId is null)
            return null;
        var (nativeEventId, stage) = eventId switch
        {
            var id when id == PrototypeNativeHiveEvents.LostWispId &&
                request.Ordinal == 0 =>
                    (PrototypeNativeHiveEvents.LostWispNativeRngEventId,
                        "primary"),
            "proto.native.event.jungle_maze_adventure" when
                request.Ordinal == 0 => ("JUNGLE_MAZE_ADVENTURE", "primary"),
            "proto.native.event.jungle_maze_adventure" when
                request.Ordinal == 1 => ("JUNGLE_MAZE_ADVENTURE", "secondary"),
            _ => (null, null)
        };
        if (nativeEventId is null || stage is null)
            return null;

        var expected = new RandomUnitFloatRequestV111(
            ChanceLawEngineV111.NativeEventGoldUnitFloatSiteId,
            NativeRngV111Contexts.EventStreamId(nativeEventId),
            $"event:{eventId}:gold:{stage}", request.Ordinal);
        if (CanonicalJson.Sha256(expected) != CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Native event-gold request differs from its event, stage, stream, or ordinal.");
        return ChanceLawEngineV111.QueryNativeEventGoldUnitFloat(
            active.Origin, request.SiteId, request.Ordinal,
            PrototypeAiEnvironment.StableActionId(active.Action), eventId);
    }

    private static string? NativeEventGoldEventId(string ownerId)
    {
        const string prefix = "event:";
        const string marker = ":gold:";
        if (!ownerId.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        var markerIndex = ownerId.LastIndexOf(marker,
            StringComparison.Ordinal);
        if (markerIndex <= prefix.Length ||
            ownerId[(markerIndex + marker.Length)..] is not
                ("primary" or "secondary"))
            return null;
        return ownerId[prefix.Length..markerIndex];
    }

    private static ScriptedRunActionOutcomeV111 CreateNatureOutcome(
        ScriptedRunActionSessionV111 active,
        ControlledStochasticRequestV111 request,
        ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var decision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, request, selection, active.Origin,
            active.PendingCandidateLabels);
        return ScriptedRunActionOutcomeV111.FromDecision(
            decision, checked(active.Decisions.Length + 1));
    }

    public ReferenceNatureScriptedRunSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public void Validate()
    {
        Mechanics.Validate();
        Stochastic.Validate();
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            Stochastic.CompletedActions is null ||
            Stochastic.ActiveNatureDecisions is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature whole-run envelope.");
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, InitialNatureState, "Whole-run");

        var replayRun = Origin.Fork();
        var replayNature = InitialNatureState.Fork();
        foreach (var record in Stochastic.CompletedActions)
        {
            var replay = ReplayAction(replayRun, record.Action,
                record.Decisions, record.NatureDecisions, replayNature,
                out replayNature);
            if (!replay.Complete || replay.Run is null)
                throw new InvalidDataException(
                    "Completed ReferenceNature run action did not reach a commit boundary.");
            replayRun = ReferenceNatureRunCursorV111.Commit(
                replay.Run, replayNature);
        }
        if (CanonicalJson.Sha256(replayRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature whole-run checkpoint differs from its action history.");

        if (Stochastic.ActiveAction is { } active)
        {
            if (active.Complete ||
                CanonicalJson.Sha256(active.Origin) !=
                    CanonicalJson.Sha256(replayRun))
                throw new InvalidDataException(
                    "Active ReferenceNature action does not follow the committed run history.");
            var replayActive = ReplayAction(replayRun, active.Action,
                active.Decisions, Stochastic.ActiveNatureDecisions,
                replayNature, out replayNature);
            if (CanonicalJson.Sha256(replayActive) !=
                CanonicalJson.Sha256(active))
                throw new InvalidDataException(
                    "Active ReferenceNature action differs from its sampled prefix.");
        }
        else if (Stochastic.ActiveNatureDecisions.Length != 0)
            throw new InvalidDataException(
                "ReferenceNature whole-run has decisions without an active action.");

        if (!replayNature.SameCursor(NatureState))
            throw new InvalidDataException(
                "ReferenceNature whole-run transcript does not end at its cursor.");
        if (ActiveAction is null &&
            !NatureState.SameCursor(Run.Stochastic.ReferenceNature))
            throw new InvalidDataException(
                "Committed ReferenceNature cursor differs from the run checkpoint.");
    }

    public static string Save(ReferenceNatureScriptedRunSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureScriptedRunSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            session.CompletedActions.Select(action => action.Fork()).ToArray(),
            session.ActiveAction is null ? null :
                ScriptedRunActionSessionV111.Save(session.ActiveAction),
            session.ActiveNatureDecisions);
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureScriptedRunSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureScriptedRunSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature whole-run snapshot.");
        if (snapshot.SchemaId is not (LegacySchemaId or CurrentSchemaId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.CompletedActions is null ||
            snapshot.ActiveNatureDecisions is null ||
            snapshot.ActiveActionSnapshotJson is not null &&
                string.IsNullOrWhiteSpace(snapshot.ActiveActionSnapshotJson))
            throw new InvalidDataException(
                "Invalid ReferenceNature whole-run snapshot.");
        var session = new ReferenceNatureScriptedRunSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState, snapshot.NatureState,
            snapshot.CompletedActions.Select(action => action.Fork()).ToArray(),
            snapshot.ActiveActionSnapshotJson is null ? null :
                ScriptedRunActionSessionV111.Restore(
                    snapshot.ActiveActionSnapshotJson),
            snapshot.ActiveNatureDecisions);
        session.Validate();
        return session;
    }

    private static ReferenceNatureScriptedRunSessionV111 CommitAction(
        ReferenceNatureScriptedRunSessionV111 session,
        ScriptedRunActionSessionV111 action, 
        ReferenceNatureSelectionV111?[] natureDecisions,
        ReferenceNatureStateV111? nextNatureState = null)
    {
        if (!action.Complete || action.Run is null)
            throw new InvalidOperationException(
                "Only a completed action can commit to the ReferenceNature run.");
        var cursor = nextNatureState ?? session.NatureState;
        var record = new ReferenceNatureScriptedRunActionRecordV111(
            CloneAction(action.Action),
            action.Decisions.Select(decision => decision.Fork()).ToArray(),
            natureDecisions);
        record.Validate();
        var committedRun = ReferenceNatureRunCursorV111.Commit(
            action.Run, cursor);
        var next = session with
        {
            Run = committedRun,
            NatureState = cursor,
            CompletedActions = session.CompletedActions.Append(record)
                .ToArray(),
            ActiveAction = null,
            ActiveNatureDecisions = []
        };
        next.Validate();
        return next;
    }

    private static ScriptedRunActionSessionV111 ReplayAction(
        RunState origin, GameAction action,
        ControlledStochasticDecisionV111[] decisions,
        ReferenceNatureSelectionV111?[] natureDecisions,
        ReferenceNatureStateV111 initialNatureState,
        out ReferenceNatureStateV111 finalNatureState)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(natureDecisions);
        if (decisions.Length != natureDecisions.Length)
            throw new InvalidDataException(
                "ReferenceNature and provider action transcripts differ in length.");
        var replay = ScriptedRunActionSessionV111.Begin(origin, action);
        var cursor = initialNatureState.Fork();
        for (var index = 0; index < decisions.Length; index++)
        {
            var request = replay.PendingRequest ?? throw new InvalidDataException(
                "ReferenceNature action transcript exceeds its provider requests.");
            if (natureDecisions[index] is null)
            {
                var scriptedOutcome = ScriptedRunActionOutcomeV111.FromDecision(
                    decisions[index], index + 1);
                if (CanonicalJson.Sha256(
                        scriptedOutcome.ToDecision()) !=
                    CanonicalJson.Sha256(decisions[index]))
                    throw new InvalidDataException(
                        "Scripted whole-run outcome differs from its saved decision.");
                replay = replay.ResolveNext(scriptedOutcome);
                if (CanonicalJson.Sha256(replay.Decisions[index]) !=
                    CanonicalJson.Sha256(decisions[index]))
                    throw new InvalidDataException(
                        "Scripted provider decision differs from its saved action transcript.");
                continue;
            }
            var law = QueryNatureLaw(replay, request) ??
                throw new InvalidDataException(
                    "Nature-sampled whole-run decision has no supported Chance Law.");
            var selection = ReferenceNatureV111.SampleValidated(law, cursor);
            if (CanonicalJson.Sha256(selection) !=
                CanonicalJson.Sha256(natureDecisions[index]!))
                throw new InvalidDataException(
                    "ReferenceNature action transcript differs from its Chance Law replay.");
            var outcome = CreateNatureOutcome(
                replay, request, law, selection);
            if (CanonicalJson.Sha256(outcome.ToDecision()) !=
                CanonicalJson.Sha256(decisions[index]))
                throw new InvalidDataException(
                    "ReferenceNature provider decision differs from its Chance Law outcome.");
            replay = replay.ResolveNext(outcome);
            if (CanonicalJson.Sha256(replay.Decisions[index]) !=
                CanonicalJson.Sha256(decisions[index]))
                throw new InvalidDataException(
                    "ReferenceNature provider decision differs from its saved action transcript.");
            cursor = selection.NextState;
        }
        if (replay.Complete != (replay.Run is not null) ||
            replay.PendingRequest is null && !replay.Complete)
            throw new InvalidDataException(
                "ReferenceNature action prefix does not end at a valid boundary.");
        finalNatureState = cursor;
        return replay;
    }

    private static string PendingEventId(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request) => CanonicalJson.Sha256(new
        {
            BoundaryFingerprint = active.Fingerprint,
            ActionId = PrototypeAiEnvironment.StableActionId(active.Action),
            RequestFingerprint = CanonicalJson.Sha256(request),
            DecisionCount = active.Decisions.Length
        });

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
