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
        "sts2-v111-simulation-provider-replay-capture-v1";

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

    public SimulationProviderReplayCaptureV111 Fork()
    {
        Validate();
        return this with
        {
            Simulation = new SimulationStateV111<
                SimulationProviderReplayMechanicsCaptureV111,
                SimulationProviderReplayStochasticCaptureV111>(
                    Mechanics.Fork(), Stochastic.Fork())
        };
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Simulation is null ||
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
        "sts2-v111-simulation-provider-replay-comparison-v1";

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
        return Compare(
            left.Mechanics.Origin, left.Mechanics.Actions,
            left.Stochastic.Boundaries, left.Mechanics.Result,
            right.Mechanics.Origin, right.Mechanics.Actions,
            right.Stochastic.Boundaries, right.Mechanics.Result,
            requireNativeCursorEquality, maximumMechanicsDifferences);
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
