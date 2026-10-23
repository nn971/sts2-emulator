using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ScriptedRunActionRecordV111(
    GameAction Action,
    ControlledStochasticDecisionV111[] Decisions)
{
    private GameAction? _action = CloneAction(Action);
    private ControlledStochasticDecisionV111[]? _decisions =
        CloneDecisions(Decisions);

    public GameAction Action
    {
        get => _action is null ? null! : CloneAction(_action);
        init => _action = CloneAction(value);
    }

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => _decisions is null
            ? null!
            : (ControlledStochasticDecisionV111[])_decisions.Clone();
        init => _decisions = CloneDecisions(value);
    }

    public ScriptedRunActionRecordV111 Fork() => this with
    {
        Action = _action!,
        Decisions = _decisions!
    };

    public void Validate()
    {
        if (_action is null || string.IsNullOrWhiteSpace(_action.Kind) ||
            _decisions is null)
            throw new InvalidDataException(
                "Invalid scripted run-action record.");
        foreach (var decision in _decisions)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Scripted run-action record contains a null decision.");
            decision.Validate();
        }
    }

    private static GameAction? CloneAction(GameAction? action) =>
        action is null ? null : action with
        {
            Payload = action.Payload.Clone()
        };

    private static ControlledStochasticDecisionV111[]? CloneDecisions(
        ControlledStochasticDecisionV111[]? decisions) =>
        decisions?.Select(decision => decision is null
            ? null!
            : decision.Fork()).ToArray();
}

public sealed record ScriptedRunSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ScriptedRunActionRecordV111[] CompletedActions,
    string? ActiveActionSnapshotJson)
{
    private ScriptedRunActionRecordV111[]? _completedActions =
        Clone(CompletedActions);

    public ScriptedRunActionRecordV111[] CompletedActions
    {
        get => Clone(_completedActions)!;
        init => _completedActions = Clone(value);
    }

    private static ScriptedRunActionRecordV111[]? Clone(
        ScriptedRunActionRecordV111[]? actions) => actions is null
            ? null
            : (ScriptedRunActionRecordV111[])actions.Clone();
}

/// <summary>
/// Serializable whole-run scripted replay. It composes the ordinary
/// per-action continuation, keeping the run at the last committed player
/// boundary while a random request within the active action is unresolved.
/// Completed actions are replayed from the origin during validation.
/// </summary>
public sealed record ScriptedRunSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    ScriptedRunActionRecordV111[] CompletedActions,
    ScriptedRunActionSessionV111? ActiveAction)
{
    [JsonIgnore]
    public SimulationStateV111<ScriptedRunMechanicsStateV111,
        ScriptedRunStochasticStateV111> Simulation { get; init; } = new(
            new ScriptedRunMechanicsStateV111(Origin, Run),
            new ScriptedRunStochasticStateV111(CompletedActions, ActiveAction));

    [JsonIgnore]
    public ScriptedRunMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ScriptedRunStochasticStateV111 Stochastic
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

    public ScriptedRunActionRecordV111[] CompletedActions
    {
        get => Stochastic.CompletedActions;
        init => Stochastic = Stochastic with { CompletedActions = value };
    }

    public ScriptedRunActionSessionV111? ActiveAction
    {
        get => Stochastic.ActiveAction;
        init => Stochastic = Stochastic with { ActiveAction = value };
    }

    public const string LegacySchemaId =
        "sts2-v111-scripted-run-session-v1";
    public const string CurrentSchemaId =
        "sts2-v111-scripted-run-session-v2";

    public static string[] Operations =>
    [
        "scripted_run_begin", "scripted_run_status",
        "scripted_run_begin_action", "scripted_run_resolve_next",
        "scripted_run_fork", "scripted_run_save", "scripted_run_load",
        "scripted_run_release_many"
    ];

    [JsonIgnore]
    public bool Complete => Stochastic.ActiveAction is null &&
        Run.Phase == RunPhase.Terminal;

    [JsonIgnore]
    public ControlledStochasticRequestV111? PendingRequest =>
        Stochastic.ActiveAction?.PendingRequest;

    [JsonIgnore]
    public string[]? PendingCandidateLabels =>
        Stochastic.ActiveAction?.PendingCandidateLabels;

    [JsonIgnore]
    public IReadOnlyList<GameAction> LegalActions =>
        Stochastic.ActiveAction is null && !Complete &&
            Run.Stochastic.ControlledEventRewardChoice is null &&
            Run.Stochastic.ControlledRunActionChoice is null &&
            Run.World?.Event?.Stochastic.ControlledChoice is null
            ? new PrototypeGameEngine().GetLegalActions(Run)
            : Array.Empty<GameAction>();

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ScriptedRunSessionV111 Begin(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        RunConfiguration.ValidateState(origin);
        var session = new ScriptedRunSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(), [], null);
        session.Validate();
        return session;
    }

    public ScriptedRunSessionV111 BeginAction(GameAction action)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(action);
        if (Stochastic.ActiveAction is not null)
            throw new InvalidOperationException(
                "Resolve the active scripted action before starting another.");
        if (Complete)
            throw new InvalidOperationException(
                "A terminal scripted run has no next action.");

        var actionSession = ScriptedRunActionSessionV111.Begin(
            Run, CloneAction(action));
        var next = actionSession.Complete
            ? CommitAction(this, actionSession)
            : this with { ActiveAction = actionSession };
        next.Validate();
        return next;
    }

    public ScriptedRunSessionV111 ResolveNext(
        ScriptedRunActionOutcomeV111 outcome)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(outcome);
        if (Stochastic.ActiveAction is null)
            throw new InvalidOperationException(
                "No scripted random request is pending for the run.");

        var actionSession = Stochastic.ActiveAction.ResolveNext(outcome);
        var next = actionSession.Complete
            ? CommitAction(this, actionSession)
            : this with { ActiveAction = actionSession };
        next.Validate();
        return next;
    }

    public ScriptedRunSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            CompletedActions = Stochastic.CompletedActions
                .Select(action => action.Fork()).ToArray(),
            ActiveAction = Stochastic.ActiveAction?.Fork()
        };
    }

    public void Validate()
    {
        Mechanics.Validate();
        Stochastic.Validate();
        var completedActions = Stochastic.CompletedActions;
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            completedActions is null)
            throw new InvalidDataException(
                "Invalid scripted run-session envelope.");
        RunConfiguration.ValidateState(Origin);
        PrototypeStateInvariants.Validate(Origin);

        var replayedRun = Origin.Fork();
        foreach (var action in completedActions)
        {
            var actionValue = action?.Action;
            var decisions = action?.Decisions;
            if (action is null || actionValue is null || decisions is null)
                throw new InvalidDataException(
                    "Scripted run history contains an invalid action record.");
            foreach (var decision in decisions)
            {
                if (decision is null)
                    throw new InvalidDataException(
                        "Scripted run history contains a null decision.");
                decision.Validate();
            }

            var completed = ScriptedRunActionSessionV111.ReplayCompleted(
                replayedRun, actionValue, decisions);
            replayedRun = completed.Run!.Fork();
        }

        if (Stochastic.ActiveAction is { } active)
        {
            if (active.Complete ||
                CanonicalJson.Sha256(active.Origin) !=
                    CanonicalJson.Sha256(replayedRun))
                throw new InvalidDataException(
                    "Active scripted action does not follow the committed run history.");
        }

        if (CanonicalJson.Sha256(replayedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Scripted run checkpoint differs from its completed action history.");
        PrototypeStateInvariants.Validate(Run);
    }

    public static string Save(ScriptedRunSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ScriptedRunSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run),
            session.Stochastic.CompletedActions
                .Select(action => action.Fork()).ToArray(),
            session.Stochastic.ActiveAction is null
                ? null
                : ScriptedRunActionSessionV111.Save(session.Stochastic.ActiveAction));
        return JsonSerializer.Serialize(snapshot);
    }

    public static ScriptedRunSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ScriptedRunSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null scripted run-session snapshot.");
        var completedActions = snapshot.CompletedActions;
        if (snapshot.SchemaId is not (LegacySchemaId or CurrentSchemaId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            completedActions is null ||
            completedActions.Any(action => action is null ||
                action.Action is null || action.Decisions is null) ||
            snapshot.ActiveActionSnapshotJson is not null &&
                string.IsNullOrWhiteSpace(
                    snapshot.ActiveActionSnapshotJson))
            throw new InvalidDataException(
                "Invalid scripted run-session snapshot.");

        var session = new ScriptedRunSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            RunSnapshot.Load(snapshot.RunSnapshotJson),
            completedActions.Select(action => action.Fork()).ToArray(),
            snapshot.ActiveActionSnapshotJson is null
                ? null
                : ScriptedRunActionSessionV111.Restore(
                    snapshot.ActiveActionSnapshotJson));
        session.Validate();
        return session;
    }

    private static ScriptedRunSessionV111 CommitAction(
        ScriptedRunSessionV111 session,
        ScriptedRunActionSessionV111 actionSession)
    {
        if (!actionSession.Complete || actionSession.Run is null)
            throw new InvalidOperationException(
                "Only a completed scripted action can commit to the run.");
        var record = new ScriptedRunActionRecordV111(
            CloneAction(actionSession.Action),
            actionSession.Decisions.Select(decision => decision.Fork())
                .ToArray());
        record.Validate();
        return session with
        {
            Mechanics = session.Mechanics with
            {
                Run = actionSession.Run.Fork()
            },
            Stochastic = session.Stochastic.AppendCompletedAction(record)
        };
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
