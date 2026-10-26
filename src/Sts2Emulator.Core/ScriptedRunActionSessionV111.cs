using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// One outcome from the ordered random-boundary journal accepted by a
/// resumable scripted run-action session. Candidate labels accompany index
/// entries because the boundary journal intentionally does not own them.
/// </summary>
public sealed record ScriptedRunActionOutcomeV111(
    RandomBoundaryTranscriptEntryV111 Entry,
    string[]? CandidateLabels = null)
{
    private string[]? _candidateLabels = Copy(CandidateLabels);

    public string[]? CandidateLabels
    {
        get => Copy(_candidateLabels);
        init => _candidateLabels = Copy(value);
    }

    public ScriptedRunActionOutcomeV111 Fork() => this with
    {
        Entry = Entry with { Event = Entry.Event.Clone() },
        CandidateLabels = _candidateLabels
    };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Entry);
        Entry.Validate();
        if (CandidateLabels is not null &&
            (Entry.OperationKind != "index" ||
             CandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException(
                "Scripted run-action candidate labels require a valid index entry.");
    }

    public static ScriptedRunActionOutcomeV111 FromDecision(
        ControlledStochasticDecisionV111 decision, int sequence)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        decision.Validate();
        object boundary = decision.IndexDecision is { } index
            ? new RandomBoundaryEventV111(sequence, index.Request,
                index.SelectedIndex, index.SelectedValue, index.ProviderMode,
                index.NativeCursorBefore, index.NativeCursorAfter)
            : decision.UnitFloatDecision is { } unitFloat
                ? new RandomUnitFloatBoundaryEventV111(sequence,
                    unitFloat.Request, unitFloat.SelectedValue,
                    unitFloat.ProviderMode, unitFloat.NativeCursorBefore,
                    unitFloat.NativeCursorAfter)
                : decision.RangedFloatDecision is { } rangedFloat
                    ? new RandomRangedFloatBoundaryEventV111(sequence,
                        rangedFloat.Request, rangedFloat.SelectedValue,
                        rangedFloat.ProviderMode,
                        rangedFloat.NativeCursorBefore,
                        rangedFloat.NativeCursorAfter)
                    : decision.RangedDoubleDecision is { } rangedDouble
                        ? new RandomRangedDoubleBoundaryEventV111(sequence,
                            rangedDouble.Request,
                            rangedDouble.SelectedValue,
                            rangedDouble.ProviderMode,
                            rangedDouble.NativeCursorBefore,
                            rangedDouble.NativeCursorAfter)
                        : decision.GaussianIntDecision is { } gaussianInt
                            ? new RandomGaussianIntBoundaryEventV111(
                                sequence, gaussianInt.Request,
                                gaussianInt.SelectedValue,
                                gaussianInt.ProviderMode,
                                gaussianInt.NativeCursorBefore,
                                gaussianInt.NativeCursorAfter)
                            : decision.GaussianDoubleDecision is { } gaussianDouble
                                ? new RandomGaussianDoubleBoundaryEventV111(
                                    sequence, gaussianDouble.Request,
                                    gaussianDouble.SelectedValue,
                                    gaussianDouble.ProviderMode,
                                    gaussianDouble.NativeCursorBefore,
                                    gaussianDouble.NativeCursorAfter)
                                : throw new InvalidDataException(
                                    "Typed stochastic outcome has no decision.");
        var operationKind = decision.IndexDecision is not null ? "index" :
            decision.UnitFloatDecision is not null ? "unit_float" :
            decision.RangedFloatDecision is not null ? "ranged_float" :
            decision.RangedDoubleDecision is not null ? "ranged_double" :
            decision.GaussianIntDecision is not null ? "gaussian_int" :
            "gaussian_double";
        var entry = new RandomBoundaryTranscriptEntryV111(sequence,
            operationKind,
            RandomBoundaryTranscriptEntryV111.SerializeEvent(boundary));
        return new ScriptedRunActionOutcomeV111(entry,
            decision.CandidateLabels);
    }

    internal ControlledStochasticDecisionV111 ToDecision()
    {
        Validate();
        var decision = Entry.OperationKind switch
        {
            "index" => IndexDecision(),
            "unit_float" => UnitFloatDecision(),
            "ranged_float" => RangedFloatDecision(),
            "ranged_double" => RangedDoubleDecision(),
            "gaussian_int" => GaussianIntDecision(),
            "gaussian_double" => GaussianDoubleDecision(),
            _ => throw new InvalidDataException(
                "Unsupported scripted run-action operation.")
        };
        decision.Validate();
        return decision;
    }

    private RandomBoundaryEventV111 ReadIndexEvent() => ReadEvent<
        RandomBoundaryEventV111>("index");

    private T ReadEvent<T>(string operationKind) where T : class
    {
        if (Entry.OperationKind != operationKind)
            throw new InvalidDataException(
                "Scripted run-action operation kind does not match its payload.");
        return Entry.DeserializeEvent<T>();
    }

    private ControlledStochasticDecisionV111 IndexDecision()
    {
        var item = ReadIndexEvent();
        var decision = new RandomIndexDecisionV111(
            item.Request with
            {
                OrderedCandidates = (int[])item.Request.OrderedCandidates.Clone()
            }, item.SelectedIndex, "controlled-v111");
        decision.Validate();
        if (item.SelectedValue != decision.SelectedValue ||
            CandidateLabels is not null &&
                CandidateLabels.Length != item.Request.SupportCount)
            throw new InvalidDataException(
                "Scripted run-action index outcome disagrees with its support.");
        return ControlledStochasticDecisionV111.FromIndex(
            decision, CandidateLabels is null ? null :
                (string[])CandidateLabels.Clone());
    }

    private static string[]? Copy(string[]? labels) => labels is null
        ? null
        : (string[])labels.Clone();

    private ControlledStochasticDecisionV111 UnitFloatDecision()
    {
        var item = ReadEvent<RandomUnitFloatBoundaryEventV111>("unit_float");
        return ControlledStochasticDecisionV111.FromUnitFloat(
            new RandomUnitFloatDecisionV111(
                item.Request with { }, item.SelectedValue,
                "controlled-v111"));
    }

    private ControlledStochasticDecisionV111 RangedFloatDecision()
    {
        var item = ReadEvent<RandomRangedFloatBoundaryEventV111>(
            "ranged_float");
        return ControlledStochasticDecisionV111.FromRangedFloat(
            new RandomRangedFloatDecisionV111(
                item.Request with { }, item.SelectedValue,
                "controlled-v111"));
    }

    private ControlledStochasticDecisionV111 RangedDoubleDecision()
    {
        var item = ReadEvent<RandomRangedDoubleBoundaryEventV111>(
            "ranged_double");
        return ControlledStochasticDecisionV111.FromRangedDouble(
            new RandomRangedDoubleDecisionV111(
                item.Request with { }, item.SelectedValue,
                "controlled-v111"));
    }

    private ControlledStochasticDecisionV111 GaussianIntDecision()
    {
        var item = ReadEvent<RandomGaussianIntBoundaryEventV111>(
            "gaussian_int");
        return ControlledStochasticDecisionV111.FromGaussianInt(
            new RandomGaussianIntDecisionV111(
                item.Request with { }, item.SelectedValue,
                "controlled-v111"));
    }

    private ControlledStochasticDecisionV111 GaussianDoubleDecision()
    {
        var item = ReadEvent<RandomGaussianDoubleBoundaryEventV111>(
            "gaussian_double");
        return ControlledStochasticDecisionV111.FromGaussianDouble(
            new RandomGaussianDoubleDecisionV111(
                item.Request with { }, item.SelectedValue,
                "controlled-v111"));
    }

}

public sealed record ScriptedRunActionSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    GameAction Action,
    string? RunSnapshotJson,
    ControlledStochasticDecisionV111[] Decisions,
    ControlledStochasticContinuationStateV111? Continuation)
{
    private GameAction? _action = Action is null
        ? null
        : CloneAction(Action);
    private ControlledStochasticDecisionV111[]? _decisions =
        CloneDecisions(Decisions);

    public GameAction Action
    {
        get => CloneAction(_action) ??
            throw new InvalidDataException(
                "Scripted run-action session has no action.");
        init => _action = value is null ? null : CloneAction(value);
    }

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => _decisions is null
            ? null!
            : (ControlledStochasticDecisionV111[])_decisions.Clone();
        init => _decisions = CloneDecisions(value);
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

/// <summary>
/// Serializable scripted controller for one legal ordinary run action. It
/// replays the saved typed prefix through PrototypeGameEngine and pauses at
/// the next exact provider request until a matching journal outcome arrives.
/// </summary>
public sealed record ScriptedRunActionSessionV111(
    string SchemaId,
    RunState Origin,
    GameAction Action,
    RunState? Run,
    ControlledStochasticDecisionV111[] Decisions,
    ControlledStochasticContinuationStateV111? Continuation)
{
    [JsonIgnore]
    public SimulationStateV111<ScriptedRunActionMechanicsStateV111,
        ScriptedRunActionStochasticStateV111> Simulation { get; init; } = new(
            new ScriptedRunActionMechanicsStateV111(Origin, Action, Run),
            new ScriptedRunActionStochasticStateV111(Decisions, Continuation));

    [JsonIgnore]
    public ScriptedRunActionMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ScriptedRunActionStochasticStateV111 Stochastic
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

    public RunState? Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics with { Run = value };
    }

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => Stochastic.Decisions;
        init => Stochastic = Stochastic with { Decisions = value };
    }

    public ControlledStochasticContinuationStateV111? Continuation
    {
        get => Stochastic.Continuation;
        init => Stochastic = Stochastic with { Continuation = value };
    }

    public const string LegacySchemaId =
        "sts2-v111-scripted-run-action-session-v1";
    public const string CurrentSchemaId =
        "sts2-v111-scripted-run-action-session-v2";

    public static string[] Operations =>
    [
        "scripted_run_action_begin", "scripted_run_action_status",
        "scripted_run_action_resolve_next", "scripted_run_action_fork",
        "scripted_run_action_save", "scripted_run_action_load",
        "scripted_run_action_release_many"
    ];

    [JsonIgnore]
    public bool Complete => Run is not null && Stochastic.Continuation is null;

    [JsonIgnore]
    public ControlledStochasticRequestV111? PendingRequest =>
        Stochastic.Continuation?.PendingRequest;

    [JsonIgnore]
    public string[]? PendingCandidateLabels =>
        Stochastic.Continuation?.PendingCandidateLabels;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ScriptedRunActionSessionV111 Begin(
        RunState source, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ValidateOpeningBoundary(source, action);
        var replay = Capture(source, action, []);
        var session = new ScriptedRunActionSessionV111(
            CurrentSchemaId, source.Fork(), CloneAction(action), replay.Run,
            replay.Continuation?.Decisions ?? [], replay.Continuation);
        session.Validate();
        return session;
    }

    public ScriptedRunActionSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Action = CloneAction(Action),
            Run = Run?.Fork(),
            Decisions = Stochastic.Decisions.Select(decision => decision.Fork()).ToArray(),
            Continuation = Stochastic.Continuation?.Fork()
        };
    }

    public ScriptedRunActionSessionV111 ResolveNext(
        ScriptedRunActionOutcomeV111 outcome)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(outcome);
        if (Complete)
            throw new InvalidOperationException(
                "Scripted run-action session is already complete.");
        outcome.Validate();
        if (outcome.Entry.Sequence != Stochastic.Decisions.Length + 1)
            throw new InvalidDataException(
                "Scripted run-action transcript sequence is not contiguous.");
        var selected = outcome.ToDecision();
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            Origin, Stochastic.Continuation!, selected,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State);
        var decisions = replay.Continuation?.Decisions ??
            Stochastic.Decisions.Append(selected.Fork()).ToArray();
        var next = this with
        {
            Run = replay.Run,
            Decisions = decisions,
            Continuation = replay.Continuation
        };
        next.Validate();
        return next;
    }

    /// <summary>
    /// Resolve a pending index request by its saved candidate label while
    /// retaining the established index transcript.
    /// </summary>
    public ScriptedRunActionSessionV111 ResolveByCandidateLabel(
        string candidateLabel)
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "Scripted run-action session is already complete.");
        var request = PendingRequest?.IndexRequest ??
            throw new InvalidOperationException(
                "The pending scripted run-action request is not an index request.");
        var labels = PendingCandidateLabels;
        var selectedIndex = ControlledChanceContinuationExecutorV111
            .IndexOfCandidateLabel(labels, request.SupportCount,
                candidateLabel);
        var decision = ControlledStochasticDecisionV111.FromIndex(
            new RandomIndexDecisionV111(request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selectedIndex, "controlled-v111"), labels);
        return ResolveNext(ScriptedRunActionOutcomeV111.FromDecision(
            decision, checked(Stochastic.Decisions.Length + 1)));
    }

    public void Validate()
    {
        Mechanics.Validate();
        Stochastic.Validate();
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Stochastic.Decisions is null || string.IsNullOrWhiteSpace(Action.Kind) ||
            (Run is null) == (Stochastic.Continuation is null))
            throw new InvalidDataException(
                "Invalid scripted run-action session envelope.");
        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        if (Stochastic.Continuation is { } continuation)
        {
            if (continuation.SchemaId !=
                    ControlledStochasticContinuationStateV111.MixedPrimitiveSchemaId ||
                CanonicalJson.Sha256(continuation.Decisions) !=
                    CanonicalJson.Sha256(Stochastic.Decisions))
                throw new InvalidDataException(
                    "Scripted run-action session prefix differs from its continuation.");
        }
        var replay = Capture(Origin, Action, Stochastic.Decisions);
        if (!SameContinuation(replay.Continuation, Stochastic.Continuation) ||
            (replay.Run is null) != (Run is null) ||
            replay.Run is { } expectedRun &&
                CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Scripted run-action session differs from its action replay.");
        if (Run is { } run)
            PrototypeStateInvariants.Validate(run);
    }

    public static string Save(ScriptedRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ScriptedRunActionSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action),
            session.Run is null ? null : RunSnapshot.Save(session.Run),
            session.Stochastic.Decisions.Select(decision => decision.Fork()).ToArray(),
            session.Stochastic.Continuation?.Fork());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ScriptedRunActionSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ScriptedRunActionSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null scripted run-action snapshot.");
        if (snapshot.SchemaId is not (LegacySchemaId or CurrentSchemaId) ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null || snapshot.Decisions is null)
            throw new InvalidDataException(
                "Invalid scripted run-action snapshot.");
        var session = new ScriptedRunActionSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action),
            snapshot.RunSnapshotJson is null ? null :
                RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.Decisions.Select(decision => decision.Fork()).ToArray(),
            UpgradeContinuation(snapshot.Continuation));
        session.Validate();
        return session;
    }

    private static ControlledStochasticContinuationStateV111?
        UpgradeContinuation(
            ControlledStochasticContinuationStateV111? continuation)
    {
        if (continuation is null)
            return null;
        continuation.Validate();
        if (continuation.SchemaId ==
            ControlledStochasticContinuationStateV111.MixedPrimitiveSchemaId)
            return continuation.Fork();
        if (continuation.SchemaId !=
            ControlledStochasticContinuationStateV111.CurrentSchemaId)
            throw new InvalidDataException(
                "Scripted run-action continuation schema is unsupported.");

        var upgraded = continuation.Fork() with
        {
            SchemaId =
                ControlledStochasticContinuationStateV111.MixedPrimitiveSchemaId
        };
        upgraded.Validate();
        return upgraded;
    }

    internal static ScriptedRunActionSessionV111 ReplayCompleted(
        RunState origin, GameAction action,
        ControlledStochasticDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(decisions);
        ValidateOpeningBoundary(origin, action);
        var copiedDecisions = decisions.Select(decision =>
        {
            ArgumentNullException.ThrowIfNull(decision);
            return decision.Fork();
        }).ToArray();
        foreach (var decision in copiedDecisions)
            decision.Validate();

        var replay = Capture(origin, action, copiedDecisions);
        if (replay.Run is null || replay.Continuation is not null)
            throw new InvalidDataException(
                "Completed scripted action history did not reach a clean action boundary.");
        var session = new ScriptedRunActionSessionV111(
            CurrentSchemaId, origin.Fork(), CloneAction(action),
            replay.Run, copiedDecisions, null);
        return session;
    }

    private static ControlledStochasticContinuationResultV111 Capture(
        RunState origin, GameAction action,
        ControlledStochasticDecisionV111[] decisions) =>
        ControlledChanceContinuationExecutorV111.CaptureStochastic(
            origin,
            ControlledStochasticContinuationStateV111.MixedPrimitiveSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State);

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        RunConfiguration.ValidateState(source);
        if (source.Stochastic.ControlledEventRewardChoice is not null ||
            source.Stochastic.ControlledRunActionChoice is not null ||
            source.World?.Event?.Stochastic.ControlledChoice is not null ||
            new PrototypeGameEngine().GetLegalActions(source).All(candidate =>
                CanonicalJson.Sha256(candidate) != CanonicalJson.Sha256(action)))
            throw new InvalidOperationException(
                "Scripted run-action begin requires one current ordinary legal action.");
    }

    private static bool SameContinuation(
        ControlledStochasticContinuationStateV111? left,
        ControlledStochasticContinuationStateV111? right) =>
        left is null ? right is null : right is not null &&
            CanonicalJson.Sha256(left) == CanonicalJson.Sha256(right);

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
