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
/// boundaries and registered Native map quota Gaussians can be sampled by
/// ReferenceNature; any of the six typed boundaries can instead be resolved
/// from a caller-supplied transcript.
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

    // Non-index Nature laws are admitted only for registered Native map
    // quota Gaussians; other typed primitives remain caller-scripted.
    private static ChanceLawV111? QueryNatureLaw(
        ScriptedRunActionSessionV111 active,
        ControlledStochasticRequestV111 request)
    {
        if (request.IndexRequest is { } indexRequest)
            return ChanceLawEngineV111.QueryRandomIndex(
                PendingEventId(active, indexRequest), indexRequest);
        if (request.GaussianIntRequest is { } gaussianRequest &&
            ChanceLawEngineV111.MatchesNativeMapGaussianIntRequest(
                gaussianRequest))
            return ChanceLawEngineV111.QueryNativeMapRequest(request);
        return null;
    }

    private static ScriptedRunActionOutcomeV111 CreateNatureOutcome(
        ScriptedRunActionSessionV111 active,
        ControlledStochasticRequestV111 request,
        ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        if (request.IndexRequest is { } indexRequest)
            return CreateOutcome(active, indexRequest, selection);
        var decision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, request, selection);
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

    private static ScriptedRunActionOutcomeV111 CreateOutcome(
        ScriptedRunActionSessionV111 active,
        RandomIndexRequestV111 request,
        ReferenceNatureSelectionV111 selection)
    {
        var sequence = checked(active.Decisions.Length + 1);
        var selectedValue = request.OperationKind == "bounded_integer"
            ? selection.OutcomeIndex
            : request.OrderedCandidates[selection.OutcomeIndex];
        var boundary = new RandomBoundaryEventV111(
            sequence, request with
            {
                OrderedCandidates = request.OrderedCandidates
            }, selection.OutcomeIndex, selectedValue, "controlled-v111",
            null, null);
        var entry = new RandomBoundaryTranscriptEntryV111(
            sequence, "index",
            RandomBoundaryTranscriptEntryV111.SerializeEvent(boundary));
        return new ScriptedRunActionOutcomeV111(
            entry, active.PendingCandidateLabels);
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
