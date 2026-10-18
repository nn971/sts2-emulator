using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ScriptedRunActionRecordV111(
    GameAction Action,
    ControlledStochasticDecisionV111[] Decisions)
{
    public ScriptedRunActionRecordV111 Fork() => this with
    {
        Action = CloneAction(Action),
        Decisions = Decisions.Select(decision => decision.Fork()).ToArray()
    };

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

public sealed record ScriptedRunSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ScriptedRunActionRecordV111[] CompletedActions,
    string? ActiveActionSnapshotJson);

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
    public const string CurrentSchemaId =
        "sts2-v111-scripted-run-session-v1";

    public static string[] Operations =>
    [
        "scripted_run_begin", "scripted_run_status",
        "scripted_run_begin_action", "scripted_run_resolve_next",
        "scripted_run_fork", "scripted_run_save", "scripted_run_load",
        "scripted_run_release_many"
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
    public IReadOnlyList<GameAction> LegalActions =>
        ActiveAction is null && !Complete &&
            Run.ControlledEventRewardChoice is null &&
            Run.ControlledRunActionChoice is null &&
            Run.World?.Event?.ControlledChoice is null
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
        if (ActiveAction is not null)
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
        if (ActiveAction is null)
            throw new InvalidOperationException(
                "No scripted random request is pending for the run.");

        var actionSession = ActiveAction.ResolveNext(outcome);
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
            CompletedActions = CompletedActions
                .Select(action => action.Fork()).ToArray(),
            ActiveAction = ActiveAction?.Fork()
        };
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            CompletedActions is null)
            throw new InvalidDataException(
                "Invalid scripted run-session envelope.");
        RunConfiguration.ValidateState(Origin);
        PrototypeStateInvariants.Validate(Origin);

        var replayedRun = Origin.Fork();
        foreach (var action in CompletedActions)
        {
            if (action is null || action.Action is null ||
                action.Decisions is null)
                throw new InvalidDataException(
                    "Scripted run history contains an invalid action record.");
            foreach (var decision in action.Decisions)
            {
                if (decision is null)
                    throw new InvalidDataException(
                        "Scripted run history contains a null decision.");
                decision.Validate();
            }

            var completed = ScriptedRunActionSessionV111.ReplayCompleted(
                replayedRun, action.Action, action.Decisions);
            replayedRun = completed.Run!.Fork();
        }

        if (ActiveAction is { } active)
        {
            active.Validate();
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
            session.CompletedActions.Select(action => action.Fork()).ToArray(),
            session.ActiveAction is null
                ? null
                : ScriptedRunActionSessionV111.Save(session.ActiveAction));
        return JsonSerializer.Serialize(snapshot);
    }

    public static ScriptedRunSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ScriptedRunSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null scripted run-session snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.CompletedActions is null ||
            snapshot.CompletedActions.Any(action => action is null ||
                action.Action is null || action.Decisions is null) ||
            snapshot.ActiveActionSnapshotJson is not null &&
                string.IsNullOrWhiteSpace(
                    snapshot.ActiveActionSnapshotJson))
            throw new InvalidDataException(
                "Invalid scripted run-session snapshot.");

        var session = new ScriptedRunSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.CompletedActions.Select(action => action.Fork()).ToArray(),
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
        return session with
        {
            Run = actionSession.Run.Fork(),
            CompletedActions = session.CompletedActions
                .Append(record).ToArray(),
            ActiveAction = null
        };
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
