namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics owned by one scripted ordinary action. A suspended action is
/// reconstructed from its source checkpoint and typed decisions, so its
/// completed run is absent until the action reaches a clean boundary.
/// </summary>
public sealed record ScriptedRunActionMechanicsStateV111
{
    private RunState? _origin;
    private RunState? _run;
    private GameAction? _action;

    public RunState Origin
    {
        get => _origin?.Fork()!;
        init => _origin = value?.Fork();
    }
    public GameAction Action
    {
        get => _action is null ? null! : CloneAction(_action);
        init => _action = value is null ? null : CloneAction(value);
    }
    public RunState? Run
    {
        get => _run?.Fork();
        init => _run = value?.Fork();
    }

    public ScriptedRunActionMechanicsStateV111(
        RunState Origin, GameAction Action, RunState? Run)
    {
        this.Origin = Origin;
        this.Action = Action;
        this.Run = Run;
    }

    public ScriptedRunActionMechanicsStateV111 Fork() => this with
    {
        Origin = Origin.Fork(),
        Action = Action,
        Run = Run?.Fork()
    };

    public void Validate()
    {
        var origin = _origin;
        var run = _run;
        if (origin is null || _action is null ||
            string.IsNullOrWhiteSpace(_action.Kind) ||
            run is not null && (run.RunId != origin.RunId ||
                run.RunSeed != origin.RunSeed ||
                run.GameBuild != origin.GameBuild ||
                run.EmulatorSchema != origin.EmulatorSchema))
            throw new InvalidDataException(
                "Scripted run-action mechanics have mismatched run identity.");
    }

    private static GameAction CloneAction(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action with { Payload = action.Payload.Clone() };
    }
}

/// <summary>
/// Typed stochastic outcomes and the next request for one scripted action.
/// </summary>
public sealed record ScriptedRunActionStochasticStateV111
{
    private ControlledStochasticDecisionV111[]? _decisions;
    private ControlledStochasticContinuationStateV111? _continuation;

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => CloneDecisions(_decisions)!;
        init => _decisions = CloneDecisions(value);
    }

    public ControlledStochasticContinuationStateV111? Continuation
    {
        get => _continuation?.Fork();
        init => _continuation = value?.Fork();
    }

    public ScriptedRunActionStochasticStateV111(
        ControlledStochasticDecisionV111[] Decisions,
        ControlledStochasticContinuationStateV111? Continuation)
    {
        this.Decisions = Decisions;
        this.Continuation = Continuation;
    }

    public ScriptedRunActionStochasticStateV111 Fork() => this with
    {
        Decisions = Decisions,
        Continuation = Continuation
    };

    public void Validate()
    {
        var decisions = _decisions;
        var continuation = _continuation;
        if (decisions is null ||
            (continuation is not null &&
             CanonicalJson.Sha256(continuation.Decisions) !=
                 CanonicalJson.Sha256(decisions)))
            throw new InvalidDataException(
                "Scripted run-action stochastic prefix is inconsistent.");
        foreach (var decision in decisions)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Scripted run-action session contains a null decision.");
            decision.Validate();
        }
        continuation?.Validate();
    }

    private static ControlledStochasticDecisionV111[]? CloneDecisions(
        ControlledStochasticDecisionV111[]? decisions) => decisions?.Select(
            decision => decision is null ? null! : decision.Fork()).ToArray();
}

/// <summary>
/// Source and current run checkpoints for a whole scripted replay.
/// </summary>
public sealed record ScriptedRunMechanicsStateV111
{
    public PrototypeMechanicsContinuationStateV111 Continuation { get; init; }

    public RunState Origin
    {
        get => Continuation.Origin!;
        init => Continuation = Continuation with { Origin = value };
    }

    public RunState Run
    {
        get => Continuation.Current;
        init => Continuation = Continuation with { Current = value };
    }

    public ScriptedRunMechanicsStateV111(RunState Origin, RunState Run)
    {
        Continuation = new PrototypeMechanicsContinuationStateV111(
            PrototypeMechanicsContinuationKindV111.ScriptedRunSession,
            Origin, Run);
    }

    public ScriptedRunMechanicsStateV111 Fork() => this with
    {
        Continuation = Continuation.Fork()
    };

    public void Validate() => Continuation.Validate(
        PrototypeMechanicsContinuationKindV111.ScriptedRunSession);
}

/// <summary>
/// Completed action history and the optional currently suspended action.
/// </summary>
public sealed record ScriptedRunStochasticStateV111
{
    private ScriptedRunActionRecordV111[]? _completedActions;

    public ScriptedRunActionRecordV111[] CompletedActions
    {
        get => CloneActions(_completedActions)!;
        init => _completedActions = CloneActions(value);
    }

    public ScriptedRunActionSessionV111? ActiveAction { get; init; }

    public ScriptedRunStochasticStateV111(
        ScriptedRunActionRecordV111[] CompletedActions,
        ScriptedRunActionSessionV111? ActiveAction)
    {
        this.CompletedActions = CompletedActions;
        this.ActiveAction = ActiveAction?.Fork();
    }

    public ScriptedRunStochasticStateV111 Fork() => this with
    {
        CompletedActions = CompletedActions,
        ActiveAction = ActiveAction?.Fork()
    };

    public void Validate()
    {
        if (_completedActions is null)
            throw new InvalidDataException(
                "Scripted run history is missing.");
        foreach (var action in _completedActions)
        {
            if (action is null)
                throw new InvalidDataException(
                    "Scripted run history contains an invalid action record.");
            action.Validate();
        }
        ActiveAction?.Validate();
    }

    internal ScriptedRunStochasticStateV111 AppendCompletedAction(
        ScriptedRunActionRecordV111 action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.Validate();
        var completed = _completedActions ?? throw new InvalidDataException(
            "Scripted run history is missing.");
        foreach (var entry in completed)
        {
            if (entry is null)
                throw new InvalidDataException(
                    "Scripted run history contains a null action record.");
            entry.Validate();
        }
        return this with
        {
            CompletedActions = completed.Select(entry => entry.Fork())
                .Append(action.Fork()).ToArray(),
            ActiveAction = null
        };
    }

    private static ScriptedRunActionRecordV111[]? CloneActions(
        ScriptedRunActionRecordV111[]? actions) => actions?.Select(
            action => action?.Fork()!).ToArray();
}
