namespace Sts2Emulator.Core;

/// <summary>
/// Mechanics captured for one provider replay interval. The action list is
/// detached from the caller so its fingerprint cannot change after capture.
/// </summary>
public sealed record SimulationProviderReplayMechanicsCaptureV111
{
    private RunState _origin = null!;
    private GameAction[] _actions = [];
    private RunState _result = null!;

    public RunState Origin
    {
        get => _origin.Fork();
        init => _origin = value?.Fork() ?? null!;
    }

    public GameAction[] Actions
    {
        get => CloneActions(_actions);
        init => _actions = CloneActions(value);
    }

    public RunState Result
    {
        get => _result.Fork();
        init => _result = value?.Fork() ?? null!;
    }

    public SimulationProviderReplayMechanicsCaptureV111(
        RunState Origin, GameAction[] Actions, RunState Result)
    {
        this.Origin = Origin;
        this.Actions = Actions;
        this.Result = Result;
    }

    public SimulationProviderReplayMechanicsCaptureV111 Fork() => this with
    {
        Origin = _origin.Fork(),
        Actions = CloneActions(_actions),
        Result = _result.Fork()
    };

    public void Validate()
    {
        if (_origin is null || _actions is null || _result is null)
            throw new InvalidDataException(
                "Provider replay mechanics capture is incomplete.");
        RunConfiguration.ValidateState(_origin);
        RunConfiguration.ValidateState(_result);
        for (var index = 0; index < _actions.Length; index++)
            if (_actions[index] is null ||
                string.IsNullOrWhiteSpace(_actions[index].Kind) ||
                _actions[index].Payload.ValueKind ==
                    System.Text.Json.JsonValueKind.Undefined)
                throw new InvalidDataException(
                    "Provider replay action is invalid at " + index + ".");
    }

    private static GameAction[] CloneActions(GameAction[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return actions.Select(action => action is null ? null! : action with
        {
            Payload = action.Payload.Clone()
        }).ToArray();
    }
}

/// <summary>Typed random-boundary evidence for the same captured interval.</summary>
public sealed record SimulationProviderReplayStochasticCaptureV111
{
    private RandomBoundaryTranscriptEntryV111[] _boundaries = [];

    public RandomBoundaryTranscriptEntryV111[] Boundaries
    {
        get => CloneBoundaries(_boundaries);
        init => _boundaries = CloneBoundaries(value);
    }

    public SimulationProviderReplayStochasticCaptureV111(
        RandomBoundaryTranscriptEntryV111[] Boundaries) =>
        this.Boundaries = Boundaries;

    public SimulationProviderReplayStochasticCaptureV111 Fork() => this with
    {
        Boundaries = CloneBoundaries(_boundaries)
    };

    public void Validate()
    {
        if (_boundaries is null)
            throw new InvalidDataException(
                "Provider replay stochastic capture is incomplete.");
        for (var index = 0; index < _boundaries.Length; index++)
        {
            var boundary = _boundaries[index];
            if (boundary is null || boundary.Sequence != index + 1)
                throw new InvalidDataException(
                    "Provider replay boundary sequence is invalid at " +
                    index + ".");
            boundary.Validate();
        }
    }

    private static RandomBoundaryTranscriptEntryV111[] CloneBoundaries(
        RandomBoundaryTranscriptEntryV111[] boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        return boundaries.Select(boundary => boundary is null
            ? null!
            : boundary.Fork()).ToArray();
    }
}

public static class SimulationProviderReplayCaptureProvenanceV111
{
    public const string CallerSupplied = "caller-supplied-unverified-v1";
    public const string ScriptedRunReplayValidated =
        "scripted-run-replay-validated-v1";
    public const string ScriptedActionReplayValidated =
        "scripted-action-replay-validated-v1";
    public const string SemanticNativePolicyReplayValidated =
        "semantic-native-policy-replay-validated-v1";
    public const string ReferenceNatureRunActionReplayValidated =
        "reference-nature-run-action-replay-validated-v1";
    public const string ReferenceNatureEventChoiceReplayValidated =
        "reference-nature-event-choice-replay-validated-v1";
    public const string ReferenceNatureControlledActionReplayValidated =
        "reference-nature-controlled-action-replay-validated-v1";
    public const string ReferenceNatureNeowOfferReplayValidated =
        "reference-nature-neow-offer-replay-validated-v1";

    public static bool IsKnown(string? provenance) => provenance is
        CallerSupplied or ScriptedRunReplayValidated or
        ScriptedActionReplayValidated or SemanticNativePolicyReplayValidated or
        ReferenceNatureRunActionReplayValidated or
        ReferenceNatureEventChoiceReplayValidated or
        ReferenceNatureControlledActionReplayValidated or
        ReferenceNatureNeowOfferReplayValidated;
}

/// <summary>
/// Common replay-validated shape for ReferenceNature sessions that suspend
/// one ordinary run action on a typed controlled-stochastic prefix.
/// </summary>
public interface IReferenceNatureControlledActionSessionV111
{
    RunState Origin { get; }
    GameAction Action { get; }
    RunState Run { get; }
    bool Complete { get; }
    ControlledStochasticDecisionV111[] ProviderDecisions { get; }
    void Validate();
}

/// <summary>
/// Versioned, detached capture of one provider replay interval. Mechanics and
/// stochastic evidence are grouped through the shared simulation envelope.
/// </summary>
public sealed record SimulationProviderReplayCaptureV111(
    string SchemaId,
    SimulationStateV111<SimulationProviderReplayMechanicsCaptureV111,
        SimulationProviderReplayStochasticCaptureV111> Simulation)
{
    public const string CurrentSchemaId =
        "sts2-v111-simulation-provider-replay-capture-v2";

    [System.Text.Json.Serialization.JsonInclude]
    public string CaptureProvenance { get; private init; } =
        SimulationProviderReplayCaptureProvenanceV111.CallerSupplied;

    public SimulationProviderReplayMechanicsCaptureV111 Mechanics =>
        Simulation.Mechanics;

    public SimulationProviderReplayStochasticCaptureV111 Stochastic =>
        Simulation.Stochastic;

    public static SimulationProviderReplayCaptureV111 Create(
        RunState origin, IReadOnlyList<GameAction> actions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> boundaries,
        RunState result)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(result);
        var capture = new SimulationProviderReplayCaptureV111(
            CurrentSchemaId,
            new SimulationStateV111<
                SimulationProviderReplayMechanicsCaptureV111,
                SimulationProviderReplayStochasticCaptureV111>(
                    new SimulationProviderReplayMechanicsCaptureV111(
                        origin, actions.ToArray(), result),
                    new SimulationProviderReplayStochasticCaptureV111(
                        boundaries.ToArray())));
        capture.Validate();
        return capture;
    }

    /// <summary>
    /// Builds a capture from a completed scripted run. Session validation
    /// replays its committed actions from the origin before the existing
    /// boundary journal materializes the saved typed decisions.
    /// </summary>
    public static SimulationProviderReplayCaptureV111 FromScriptedRunSession(
        ScriptedRunSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Validation replays mechanics too. Keep its provider callbacks out
        // of any journal owned by the caller.
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (session.ActiveAction is not null)
            throw new InvalidOperationException(
                "Provider replay capture requires a committed scripted-run boundary.");

        var completedActions = session.CompletedActions;
        return FromScriptedDecisions(
            session.Origin,
            completedActions.Select(action => action.Action).ToArray(),
            completedActions.SelectMany(action => action.Decisions),
            session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ScriptedRunReplayValidated);
    }

    /// <summary>Builds a capture from one completed scripted player action.</summary>
    public static SimulationProviderReplayCaptureV111
        FromScriptedRunActionSession(
            ScriptedRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (!session.Complete || session.Run is null)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed scripted action.");
        return FromScriptedDecisions(
            session.Origin, [session.Action], session.Decisions, session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ScriptedActionReplayValidated);
    }

    /// <summary>
    /// Builds a capture from a completed ReferenceNature run action when its
    /// controlled action trace contains the complete typed provider prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureRunActionSession(
            ReferenceNatureRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        using (RandomBoundaryJournalV111.Begin())
            session.Validate();
        if (!session.Complete || session.Run is null)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature run action.");
        var originHistory = session.Origin.ControlledRunActionHistory ?? [];
        var resultHistory = session.Run.ControlledRunActionHistory ?? [];
        if (resultHistory.Length < originHistory.Length)
            throw new InvalidDataException(
                "ReferenceNature run-action history lost its origin prefix.");
        for (var index = 0; index < originHistory.Length; index++)
            if (CanonicalJson.Sha256(originHistory[index]) !=
                CanonicalJson.Sha256(resultHistory[index]))
                throw new InvalidDataException(
                    "ReferenceNature run-action history changed before the captured action.");
        if (resultHistory.Length == originHistory.Length &&
            session.DecisionTranscript.Length == 0)
            return FromScriptedDecisions(
                session.Origin, [session.Action], [], session.Run,
                SimulationProviderReplayCaptureProvenanceV111
                    .ReferenceNatureRunActionReplayValidated);
        if (resultHistory.Length != originHistory.Length + 1)
            throw new NotSupportedException(
                "ReferenceNature action trace does not isolate one committed run action.");

        var trace = resultHistory[^1];
        trace.Validate();
        var mapNodeId = session.Action.Kind == "choose_map_node"
            ? session.Action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        var actionFingerprint = ControlledRunActionEngineV111
                .IsShopPurchaseAction(session.Origin, session.Action)
            ? PrototypeAiEnvironment.StableActionId(session.Action)
            : null;
        if (trace.ActionId != session.Action.Kind ||
            trace.MapNodeId != mapNodeId ||
            trace.ActionFingerprint != actionFingerprint)
            throw new NotSupportedException(
                "ReferenceNature run-action trace does not identify its saved action.");

        var decisions = trace.TypedDecisions ?? trace.Decisions
            .Select((decision, index) => ControlledStochasticDecisionV111
                .FromIndex(decision, trace.CandidateLabels[index]))
            .ToArray();
        if (decisions.Length != session.DecisionTranscript.Length)
            throw new NotSupportedException(
                "ReferenceNature run-action trace does not cover every sampled provider request.");
        return FromScriptedDecisions(
            session.Origin, [session.Action], decisions, session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureRunActionReplayValidated);
    }

    /// <summary>
    /// Builds a capture from a completed ReferenceNature event-choice
    /// session. Restore replays the mixed player-action and Nature chronology;
    /// the completed controlled trace supplies the typed stochastic requests.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureEventChoiceSession(
            ReferenceNatureControlledEventChoiceSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureControlledEventChoiceSessionV111 replay;
        using (RandomBoundaryJournalV111.Begin())
            replay = ReferenceNatureControlledEventChoiceSessionV111.Restore(
                ReferenceNatureControlledEventChoiceSessionV111.Save(session));
        if (!replay.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature event choice.");

        var originHistory = replay.Origin.ControlledEventChoiceHistory ?? [];
        var resultHistory = replay.Run.ControlledEventChoiceHistory ?? [];
        if (resultHistory.Length < originHistory.Length)
            throw new InvalidDataException(
                "ReferenceNature event-choice history lost its origin prefix.");
        for (var index = 0; index < originHistory.Length; index++)
            if (CanonicalJson.Sha256(originHistory[index]) !=
                CanonicalJson.Sha256(resultHistory[index]))
                throw new InvalidDataException(
                    "ReferenceNature event-choice history changed before the captured choice.");
        var natureSteps = replay.Steps
            .Where(step => step.Kind == "nature")
            .Select(step => step.NatureDecision ?? throw new InvalidDataException(
                "ReferenceNature event-choice step has no Nature decision."))
            .ToArray();
        ControlledStochasticDecisionV111[] decisions;
        if (natureSteps.Length == 0)
        {
            if (resultHistory.Length != originHistory.Length)
                throw new NotSupportedException(
                    "ReferenceNature event choice appended an unpaired stochastic trace.");
            decisions = [];
        }
        else
        {
            if (resultHistory.Length != originHistory.Length + 1)
                throw new NotSupportedException(
                    "ReferenceNature event-choice trace does not isolate one committed choice.");

            var trace = resultHistory[^1];
            trace.Validate();
            if (trace.EventId != replay.Origin.World?.Event?.EventId ||
                trace.ChoiceId != replay.ChoiceId ||
                trace.Decisions.Length != natureSteps.Length)
                throw new NotSupportedException(
                    "ReferenceNature event-choice trace does not cover its Nature chronology.");
            for (var index = 0; index < natureSteps.Length; index++)
                if (trace.Decisions[index].SelectedIndex !=
                    natureSteps[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "ReferenceNature event-choice outcomes differ from the typed action trace.");

            decisions = trace.Decisions.Select((decision, index) =>
                ControlledStochasticDecisionV111.FromIndex(
                    decision, trace.CandidateLabels[index])).ToArray();
        }

        var actions = replay.Steps
            .Where(step => step.Kind == "player_action")
            .Select(step => step.PlayerAction ?? throw new InvalidDataException(
                "ReferenceNature event-choice step has no player action."))
            .ToArray();
        return FromScriptedDecisions(
            replay.Origin, actions, decisions, replay.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureEventChoiceReplayValidated);
    }

    /// <summary>
    /// Builds a capture for the run-setup interval owned by a completed Neow
    /// offer session. The interval has no player actions; its request plan is
    /// reconstructed from the validated selected prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureNeowOfferSession(
            ReferenceNatureNeowOfferSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ReferenceNatureNeowOfferSessionV111 replay;
        using (RandomBoundaryJournalV111.Begin())
            replay = ReferenceNatureNeowOfferSessionV111.Restore(
                ReferenceNatureNeowOfferSessionV111.Save(session));
        if (!replay.Complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature Neow offer.");

        var plan = NeowOfferDecisionPlanBuilderV111.Build(
            replay.Origin.RunSeed, replay.Origin.Ascension,
            replay.DecisionTranscript.Select(decision =>
                decision.OutcomeIndex).ToArray());
        if (!plan.Complete || plan.Steps.Length !=
                replay.DecisionTranscript.Length)
            throw new InvalidDataException(
                "ReferenceNature Neow offer plan does not cover its saved transcript.");
        var decisions = plan.Steps.Select(step =>
        {
            var selectedIndex = step.SelectedIndex ??
                throw new InvalidDataException(
                    "Completed ReferenceNature Neow offer has a pending request.");
            var indexDecision = new RandomIndexDecisionV111(
                step.Request, selectedIndex, "controlled-v111");
            var decision = ControlledStochasticDecisionV111.FromIndex(
                indexDecision, step.OutcomeIds);
            decision.Validate();
            return decision;
        }).ToArray();
        return FromScriptedDecisions(
            replay.Origin, [], decisions, replay.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureNeowOfferReplayValidated);
    }

    /// <summary>
    /// Builds a capture from a completed ReferenceNature session that owns
    /// one player action and its replay-validated typed stochastic prefix.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromReferenceNatureControlledActionSession(
            IReferenceNatureControlledActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        bool complete;
        ControlledStochasticDecisionV111[] decisions;
        using (RandomBoundaryJournalV111.Begin())
        {
            session.Validate();
            complete = session.Complete;
            decisions = complete ? session.ProviderDecisions : [];
        }
        if (!complete)
            throw new InvalidOperationException(
                "Provider replay capture requires a completed ReferenceNature controlled action.");
        return FromScriptedDecisions(
            session.Origin, [session.Action], decisions,
            session.Run,
            SimulationProviderReplayCaptureProvenanceV111
                .ReferenceNatureControlledActionReplayValidated);
    }

    /// <summary>
    /// Builds a capture for the player-action interval of a validated Native
    /// policy baseline. The baseline's setup and initial-intent trace is used
    /// only to locate the beginning of the interval; it is excluded from the
    /// capture because the origin is the resulting initial player boundary.
    /// </summary>
    public static SimulationProviderReplayCaptureV111
        FromSemanticNativePolicySession(SemanticPilotCombatV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Restore and action replay invoke provider callbacks. Keep them out
        // of any journal owned by the caller.
        using var validationJournal = RandomBoundaryJournalV111.Begin();
        var validated = SemanticPilotCombatEngineV111.Restore(
            SemanticPilotCombatEngineV111.Save(session));
        var entry = validated.SeededEntry;
        if (entry is not { NativePolicyBaseline: true } ||
            !validated.UsesNativePolicyBaseline)
            throw new InvalidOperationException(
                "Provider replay capture requires a seeded Native policy baseline.");

        var finalAudit = validated.NativePolicyAudit ??
            throw new InvalidDataException(
                "Native policy session is missing its provider trace.");
        var replay = SemanticPilotCombatEngineV111.BeginSeeded(entry);
        var initialAudit = replay.NativePolicyAudit ??
            throw new InvalidDataException(
                "Native policy entry replay is missing its provider trace.");
        var initialTrace = initialAudit.ProviderTrace;
        var completeTrace = finalAudit.ProviderTrace;
        var initial = replay;
        if (initialTrace.Length > completeTrace.Length)
            throw new InvalidDataException(
                "Native policy entry trace is longer than the saved session trace.");
        for (var index = 0; index < initialTrace.Length; index++)
            if (CanonicalJson.Sha256(initialTrace[index]) !=
                CanonicalJson.Sha256(completeTrace[index]))
                throw new InvalidDataException(
                    "Native policy entry trace is not a prefix of the saved session trace.");

        var environment = new PrototypeAiEnvironment();
        var actions = new List<GameAction>(validated.History.Length);
        foreach (var transition in validated.History)
        {
            if (transition.Kind != "player" ||
                string.IsNullOrWhiteSpace(transition.ActionId))
                throw new NotSupportedException(
                    "Native policy capture requires a player-action-only history.");
            var resolvedAction = environment.ResolvePlayerAction(
                replay.Run, transition.ActionId);
            actions.Add(resolvedAction.Action);
            replay = SemanticPilotCombatEngineV111.Step(
                replay, replay.Fingerprint, transition.ActionId);
        }
        if (CanonicalJson.Sha256(replay) !=
            CanonicalJson.Sha256(validated))
            throw new InvalidDataException(
                "Native policy action replay differs from the saved session.");

        var boundaries = completeTrace.Skip(initialTrace.Length)
            .Select((boundary, index) => boundary with
            {
                Sequence = index + 1
            }).ToArray();
        var capture = Create(initial.Run, actions, boundaries, validated.Run) with
        {
            CaptureProvenance = SimulationProviderReplayCaptureProvenanceV111
                .SemanticNativePolicyReplayValidated
        };
        capture.Validate();
        return capture;
    }

    private static SimulationProviderReplayCaptureV111 FromScriptedDecisions(
        RunState origin, IReadOnlyList<GameAction> actions,
        IEnumerable<ControlledStochasticDecisionV111> decisions,
        RunState result, string captureProvenance)
    {
        using var journal = RandomBoundaryJournalV111.Begin();
        foreach (var decision in decisions)
        {
            decision.Validate();
            if (decision.IndexDecision is { } index)
                RandomBoundaryJournalV111.Record(index);
            else if (decision.UnitFloatDecision is { } unitFloat)
                RandomBoundaryJournalV111.Record(unitFloat);
            else if (decision.RangedFloatDecision is { } rangedFloat)
                RandomBoundaryJournalV111.Record(rangedFloat);
            else if (decision.RangedDoubleDecision is { } rangedDouble)
                RandomBoundaryJournalV111.Record(rangedDouble);
            else if (decision.GaussianIntDecision is { } gaussianInt)
                RandomBoundaryJournalV111.Record(gaussianInt);
            else
                RandomBoundaryJournalV111.Record(
                    decision.GaussianDoubleDecision ??
                    throw new InvalidDataException(
                        "Scripted run history contains an empty typed decision."));
        }

        var boundaries = journal.OrderedTranscript;
        var capture = Create(origin, actions, boundaries, result) with
        {
            CaptureProvenance = captureProvenance
        };
        capture.Validate();
        return capture;
    }

    public SimulationProviderReplayCaptureV111 Fork()
    {
        Validate();
        return this with
        {
            CaptureProvenance = CaptureProvenance,
            Simulation = new SimulationStateV111<
                SimulationProviderReplayMechanicsCaptureV111,
                SimulationProviderReplayStochasticCaptureV111>(
                    Mechanics.Fork(), Stochastic.Fork())
        };
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            !SimulationProviderReplayCaptureProvenanceV111
                .IsKnown(CaptureProvenance) || Simulation is null ||
            Simulation.Mechanics is null || Simulation.Stochastic is null)
            throw new InvalidDataException(
                "Provider replay capture envelope is invalid.");
        Simulation.Mechanics.Validate();
        Simulation.Stochastic.Validate();
    }
}

/// <summary>
/// Composed evidence for comparing two provider trajectories under the same
/// player actions: matching origin mechanics, ordered typed random outcomes,
/// and final mechanics. It does not establish source-hook parity or law
/// fidelity.
/// </summary>
public sealed record SimulationProviderReplayComparisonV111(
    string SchemaId,
    string LeftActionFingerprint,
    string RightActionFingerprint,
    SimulationMechanicsComparisonV111 OriginMechanics,
    RandomBoundaryDiffV111 RandomBoundaries,
    SimulationMechanicsComparisonV111 ResultMechanics,
    bool RequireNativeCursorEquality)
{
    public const string CurrentSchemaId =
        "sts2-v111-simulation-provider-replay-comparison-v2";

    public string LeftCaptureProvenance { get; init; } =
        SimulationProviderReplayCaptureProvenanceV111.CallerSupplied;
    public string RightCaptureProvenance { get; init; } =
        SimulationProviderReplayCaptureProvenanceV111.CallerSupplied;

    public bool SamePlayerActions => StringComparer.Ordinal.Equals(
        LeftActionFingerprint, RightActionFingerprint);

    public bool SameOriginMechanics => OriginMechanics.Equivalent;

    public bool SameTypedRandomBoundaries =>
        RandomBoundaries.Status == "RANDOM_BOUNDARIES_CONSISTENT";

    public bool SameResultMechanics => ResultMechanics.Equivalent;

    public bool EquivalentUnderComparedActions =>
        SchemaId == CurrentSchemaId && SamePlayerActions &&
        SameOriginMechanics && SameTypedRandomBoundaries &&
        SameResultMechanics;
}

/// <summary>
/// Composes the existing typed provider-trace and mechanics-projection
/// comparators. Inputs must cover the same action interval and capture every
/// typed random boundary in that interval. The result is diagnostic evidence,
/// not a native-fidelity or probability-law claim.
/// </summary>
public static class SimulationProviderReplayComparisonEngineV111
{
    public static SimulationProviderReplayComparisonV111 Compare(
        SimulationProviderReplayCaptureV111 left,
        SimulationProviderReplayCaptureV111 right,
        bool requireNativeCursorEquality = false,
        int maximumMechanicsDifferences = 64)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.Validate();
        right.Validate();
        var comparison = Compare(
            left.Mechanics.Origin, left.Mechanics.Actions,
            left.Stochastic.Boundaries, left.Mechanics.Result,
            right.Mechanics.Origin, right.Mechanics.Actions,
            right.Stochastic.Boundaries, right.Mechanics.Result,
            requireNativeCursorEquality, maximumMechanicsDifferences);
        return comparison with
        {
            LeftCaptureProvenance = left.CaptureProvenance,
            RightCaptureProvenance = right.CaptureProvenance
        };
    }

    public static SimulationProviderReplayComparisonV111 Compare(
        RunState leftOrigin,
        IReadOnlyList<GameAction> leftActions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> leftBoundaries,
        RunState leftResult,
        RunState rightOrigin,
        IReadOnlyList<GameAction> rightActions,
        IEnumerable<RandomBoundaryTranscriptEntryV111> rightBoundaries,
        RunState rightResult,
        bool requireNativeCursorEquality = false,
        int maximumMechanicsDifferences = 64)
    {
        ArgumentNullException.ThrowIfNull(leftOrigin);
        ArgumentNullException.ThrowIfNull(leftActions);
        ArgumentNullException.ThrowIfNull(leftBoundaries);
        ArgumentNullException.ThrowIfNull(leftResult);
        ArgumentNullException.ThrowIfNull(rightOrigin);
        ArgumentNullException.ThrowIfNull(rightActions);
        ArgumentNullException.ThrowIfNull(rightBoundaries);
        ArgumentNullException.ThrowIfNull(rightResult);
        if (maximumMechanicsDifferences <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumMechanicsDifferences));

        var leftActionFingerprint = FingerprintActions(leftActions);
        var rightActionFingerprint = FingerprintActions(rightActions);
        return new SimulationProviderReplayComparisonV111(
            SimulationProviderReplayComparisonV111.CurrentSchemaId,
            leftActionFingerprint,
            rightActionFingerprint,
            SimulationMechanicsProjectionEngineV111.Compare(
                leftOrigin, rightOrigin, maximumMechanicsDifferences),
            RandomBoundaryComparisonV111.CompareOrdered(
                leftBoundaries, rightBoundaries,
                requireNativeCursorEquality),
            SimulationMechanicsProjectionEngineV111.Compare(
                leftResult, rightResult, maximumMechanicsDifferences),
            requireNativeCursorEquality);
    }

    private static string FingerprintActions(
        IReadOnlyList<GameAction> actions)
    {
        var ownedActions = new GameAction[actions.Count];
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index] ?? throw new InvalidDataException(
                "Provider replay action sequence contains a null action.");
            ownedActions[index] = action with { Payload = action.Payload };
        }
        return CanonicalJson.Sha256(ownedActions);
    }
}
