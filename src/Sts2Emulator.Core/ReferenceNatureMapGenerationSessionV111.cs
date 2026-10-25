using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureMapGenerationSessionSnapshotV111(
    string SchemaId,
    string Seed,
    int Act,
    int Ascension,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] Decisions,
    ReferenceNatureDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    MapState? Map)
{
    private ControlledStochasticDecisionV111[]? _decisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(Decisions);
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);
    private ControlledStochasticRequestV111? _pendingRequest =
        ReferenceNatureSessionValueCopiesV111.CopyRequest(PendingRequest);
    private MapState? _map = Map?.Fork();

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_decisions)!;
        init => _decisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }
    public ReferenceNatureDecisionV111[] DecisionTranscript
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
    public MapState? Map
    {
        get => _map?.Fork();
        init => _map = value?.Fork();
    }
}

/// <summary>
/// Compact completed-map audit retained by a run snapshot. It owns the typed
/// request prefix and map fingerprint but leaves the authoritative map in the
/// run's mechanics state.
/// </summary>
public sealed record ReferenceNatureMapGenerationAuditV111(
    string Seed,
    int Act,
    int Ascension,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] Decisions,
    ReferenceNatureDecisionV111[] DecisionTranscript,
    string MapFingerprint)
{
    private ControlledStochasticDecisionV111[]? _decisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(Decisions);

    [JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, NatureState,
            DecisionTranscript);

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

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_decisions)!;
        init => _decisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }
    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public ReferenceNatureMapGenerationAuditV111 Fork() => this with
    {
        Stochastic = Stochastic.Fork(),
        Decisions = Decisions,
        DecisionTranscript = Stochastic.DecisionTranscript
    };

    public void Validate()
    {
        ValidateEnvelope();
        ArgumentException.ThrowIfNullOrWhiteSpace(Seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(MapFingerprint);
        ReferenceNatureMapGenerationSessionV111.ValidateCompletedAudit(
            Seed, Act, Ascension, Stochastic.InitialNatureState,
            Stochastic.NatureState,
            Decisions, Stochastic.DecisionTranscript, MapFingerprint);
    }

    public void ValidateEnvelope()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(MapFingerprint);
        Stochastic.Validate();
        ArgumentNullException.ThrowIfNull(Decisions);
        ReferenceNatureMapGenerationSessionV111.ValidateMapParameters(
            Act, Ascension);
        var decisions = Decisions;
        var transcript = Stochastic.DecisionTranscript;
        if (decisions.Length == 0 || decisions.Length != transcript.Length ||
            Stochastic.NatureState.SampleCount != checked(
                Stochastic.InitialNatureState.SampleCount + decisions.Length))
            throw new InvalidDataException(
                "Invalid completed ReferenceNature map audit envelope.");
        foreach (var decision in decisions)
        {
            ArgumentNullException.ThrowIfNull(decision);
            decision.Validate();
        }
        foreach (var decision in transcript)
        {
            ArgumentNullException.ThrowIfNull(decision);
            decision.Validate();
        }
    }
}

/// <summary>
/// Generator inputs and completed map output owned by the map continuation.
/// </summary>
public sealed record ReferenceNatureMapGenerationMechanicsStateV111
{
    private MapState? _map;

    public string Seed { get; init; }
    public int Act { get; init; }
    public int Ascension { get; init; }

    public MapState? Map
    {
        get => _map?.Fork();
        init => _map = value?.Fork();
    }

    public ReferenceNatureMapGenerationMechanicsStateV111(
        string seed, int act, int ascension, MapState? map)
    {
        Seed = seed;
        Act = act;
        Ascension = ascension;
        Map = map;
    }

    public ReferenceNatureMapGenerationMechanicsStateV111 Fork() => this with
    {
        Map = Map
    };

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Seed);
        ReferenceNatureMapGenerationSessionV111.ValidateMapParameters(
            Act, Ascension);
    }
}

/// <summary>
/// Typed provider prefix and ReferenceNature cursor for map generation.
/// </summary>
public sealed record ReferenceNatureMapGenerationStochasticStateV111
{
    private ControlledStochasticDecisionV111[]? _decisions;
    private ControlledStochasticRequestV111? _pendingRequest;

    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Cursor { get; init; }

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_decisions)!;
        init => _decisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }

    public ControlledStochasticRequestV111? PendingRequest
    {
        get => ReferenceNatureSessionValueCopiesV111.CopyRequest(
            _pendingRequest);
        init => _pendingRequest = ReferenceNatureSessionValueCopiesV111
            .CopyRequest(value);
    }

    public ReferenceNatureMapGenerationStochasticStateV111(
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111> cursor,
        ControlledStochasticDecisionV111[] decisions,
        ControlledStochasticRequestV111? pendingRequest)
    {
        Cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        Decisions = decisions;
        PendingRequest = pendingRequest;
    }

    public ReferenceNatureMapGenerationStochasticStateV111 Fork() => this with
    {
        Cursor = Cursor.Fork(),
        Decisions = Decisions,
        PendingRequest = PendingRequest
    };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Cursor);
        Cursor.Validate();
        var decisions = Decisions;
        ArgumentNullException.ThrowIfNull(decisions);
        foreach (var decision in decisions)
        {
            ArgumentNullException.ThrowIfNull(decision);
            decision.Validate();
        }
        PendingRequest?.Validate();
    }
}

/// <summary>
/// Optional atomic ReferenceNature map-generation continuation for the
/// source-shaped Overgrowth, Hive, and Glory generators. It keeps map output
/// private until every typed request has been sampled and replayed.
/// </summary>
public sealed record ReferenceNatureMapGenerationSessionV111(
    string SchemaId,
    string Seed,
    int Act,
    int Ascension,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] Decisions,
    ReferenceNatureDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    MapState? Map)
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureMapGenerationMechanicsStateV111,
        ReferenceNatureMapGenerationStochasticStateV111> Simulation { get; init; } =
        new(
            new ReferenceNatureMapGenerationMechanicsStateV111(
                Seed, Act, Ascension, Map),
            new ReferenceNatureMapGenerationStochasticStateV111(
                new ReferenceNatureStochasticStateV111<
                    ReferenceNatureDecisionV111>(InitialNatureState,
                    NatureState, DecisionTranscript),
                Decisions, PendingRequest));

    [JsonIgnore]
    public ReferenceNatureMapGenerationMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureMapGenerationStochasticStateV111 StochasticState
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    [JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic
    {
        get => StochasticState.Cursor;
        init => StochasticState = StochasticState with { Cursor = value };
    }

    public string Seed
    {
        get => Mechanics.Seed;
        init => Mechanics = Mechanics with { Seed = value };
    }

    public int Act
    {
        get => Mechanics.Act;
        init => Mechanics = Mechanics with { Act = value };
    }

    public int Ascension
    {
        get => Mechanics.Ascension;
        init => Mechanics = Mechanics with { Ascension = value };
    }

    public MapState? Map
    {
        get => Mechanics.Map;
        init => Mechanics = Mechanics with { Map = value };
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

    public ControlledStochasticDecisionV111[] Decisions
    {
        get => StochasticState.Decisions;
        init => StochasticState = StochasticState with { Decisions = value };
    }
    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }
    public ControlledStochasticRequestV111? PendingRequest
    {
        get => StochasticState.PendingRequest;
        init => StochasticState = StochasticState with
        {
            PendingRequest = value
        };
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-map-generation-session-v1";

    [JsonIgnore]
    public bool Complete => Map is not null &&
        StochasticState.PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => Stochastic.DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryMapRequest(
            StochasticState.PendingRequest ??
            throw new InvalidDataException(
                "Incomplete map session has no pending request."));

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ReferenceNatureMapGenerationSessionV111 Begin(
        string seed, int act, int ascension, ulong natureSeed)
        => Begin(seed, act, ascension,
            ReferenceNatureStateV111.Create(natureSeed));

    public static ReferenceNatureMapGenerationSessionV111 Begin(
        string seed, int act, int ascension,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateMapParameters(act, ascension);
        initialNatureState.Validate();
        var initial = initialNatureState.Fork();
        var captured = Capture(seed, act, ascension, []);
        var session = new ReferenceNatureMapGenerationSessionV111(
            CurrentSchemaId, seed, act, ascension, initial, initial,
            [], [], captured.Continuation?.PendingRequest,
            captured.Value?.Fork());
        session.Validate();
        return session;
    }

    public ReferenceNatureMapGenerationAuditV111 ToAudit()
    {
        Validate();
        if (!Complete)
            throw new InvalidOperationException(
                "An incomplete map-generation session cannot become a run audit.");
        return new ReferenceNatureMapGenerationAuditV111(
            Seed, Act, Ascension, Stochastic.InitialNatureState,
            Stochastic.NatureState, StochasticState.Decisions,
            Stochastic.DecisionTranscript, CanonicalJson.Sha256(Map!));
    }

    public ReferenceNatureMapGenerationSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Simulation = Simulation with
            {
                Mechanics = Mechanics.Fork(),
                Stochastic = StochasticState.Fork()
            }
        };
    }

    public ReferenceNatureMapGenerationSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature map-generation session is already complete.");
        var checkpoint = new ReferenceNatureMapGenerationMechanicsStateV111(
            Seed, Act, Ascension, map: null);
        var capturedPrefix = Capture(
            Seed, Act, Ascension, StochasticState.Decisions);
        var continuation = capturedPrefix.Continuation ??
            throw new InvalidDataException(
                "Incomplete map session lost its typed continuation.");
        if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                continuation.PendingRequest, StochasticState.PendingRequest))
            throw new InvalidDataException(
                "Map session request differs from the typed replay checkpoint.");
        var request = StochasticState.PendingRequest ??
            throw new InvalidDataException(
                "Incomplete map session has no pending Chance Law.");
        var law = ChanceLawEngineV111.QueryMapRequest(request);
        var selection = ReferenceNatureV111.SampleValidated(
            law, Stochastic.NatureState);
        var typedDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, request, selection,
            candidateLabels: continuation.PendingCandidateLabels);
        var decisions = StochasticState.Decisions.Append(typedDecision)
            .ToArray();
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = ControlledChanceContinuationExecutorV111.ResolveTyped<
            ReferenceNatureMapGenerationMechanicsStateV111, MapState>(
                checkpoint, continuation, typedDecision,
                mapCheckpoint => GenerateNative(
                    mapCheckpoint.Seed, mapCheckpoint.Act,
                    mapCheckpoint.Ascension));
        var next = this with
        {
            NatureState = selection.NextState,
            Decisions = decisions,
            DecisionTranscript = transcript,
            PendingRequest = replay.Continuation?.PendingRequest,
            Map = replay.Value?.Fork()
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId)
            throw new InvalidDataException(
                "Invalid ReferenceNature map-generation session envelope.");
        Mechanics.Validate();
        StochasticState.Validate();
        var decisions = StochasticState.Decisions;
        var decisionTranscript = Stochastic.DecisionTranscript;
        if (decisions.Length != decisionTranscript.Length ||
            Stochastic.NatureState.SampleCount != checked(
                Stochastic.InitialNatureState.SampleCount + decisions.Length) ||
            Complete != (StochasticState.PendingRequest is null))
            throw new InvalidDataException(
                "ReferenceNature map cursor, transcript, and completion state differ.");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            var transcript = decisionTranscript[index];
            if (decision is null || transcript is null)
                throw new InvalidDataException(
                    "Map-generation transcript contains a null decision.");
            decision.Validate();
            transcript.Validate();
            var request = decision.ToRequest();
            var law = ChanceLawEngineV111.QueryMapRequest(request);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedDecision = ReferenceNatureChanceLawDecisionV111
                .FromSelection(law, request, selection);
            var expectedTranscript = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(decision) !=
                    CanonicalJson.Sha256(expectedDecision) ||
                CanonicalJson.Sha256(transcript) !=
                    CanonicalJson.Sha256(expectedTranscript))
                throw new InvalidDataException(
                    "ReferenceNature map decision transcript does not replay.");
            expectedNatureState = selection.NextState;
        }
        if (!expectedNatureState.SameCursor(Stochastic.NatureState))
            throw new InvalidDataException(
                "ReferenceNature map sampler cursor differs from its transcript.");

        var captured = Capture(Seed, Act, Ascension, decisions);
        if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                captured.Continuation?.PendingRequest,
                StochasticState.PendingRequest) ||
            (captured.Value is null) != (Map is null) ||
            captured.Value is not null &&
                CanonicalJson.Sha256(captured.Value) != CanonicalJson.Sha256(Map))
            throw new InvalidDataException(
                "ReferenceNature map mechanics replay differs from its saved continuation.");
    }

    public static string Save(
        ReferenceNatureMapGenerationSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureMapGenerationSessionSnapshotV111(
                CurrentSchemaId, session.Seed, session.Act, session.Ascension,
                session.Stochastic.InitialNatureState,
                session.Stochastic.NatureState,
                session.StochasticState.Decisions.Select(
                    decision => decision.Fork()).ToArray(),
                (ReferenceNatureDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone(),
                session.StochasticState.PendingRequest?.Fork(),
                session.Map?.Fork()));
    }

    public static ReferenceNatureMapGenerationSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureMapGenerationSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature map-generation snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            snapshot.Decisions is null || snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature map-generation snapshot schema.");
        var session = new ReferenceNatureMapGenerationSessionV111(
            snapshot.SchemaId, snapshot.Seed, snapshot.Act,
            snapshot.Ascension, snapshot.InitialNatureState,
            snapshot.NatureState, snapshot.Decisions,
            snapshot.DecisionTranscript, snapshot.PendingRequest,
            snapshot.Map);
        session.Validate();
        return session;
    }

    private static ControlledStochasticTypedContinuationResultV111<MapState>
        Capture(
        string seed, int act, int ascension,
        ControlledStochasticDecisionV111[] decisions)
        => ControlledChanceContinuationExecutorV111.CaptureTyped(
            new ReferenceNatureMapGenerationMechanicsStateV111(
                seed, act, ascension, map: null),
            ControlledStochasticContinuationStateV111.MixedPrimitiveSchemaId,
            decisions,
            checkpoint => GenerateNative(
                checkpoint.Seed, checkpoint.Act, checkpoint.Ascension));

    internal static void ValidateCompletedAudit(
        string seed, int act, int ascension,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureStateV111 natureState,
        ControlledStochasticDecisionV111[] decisions,
        ReferenceNatureDecisionV111[] decisionTranscript,
        string mapFingerprint)
    {
        ValidateMapParameters(act, ascension);
        if (decisions is null || decisionTranscript is null ||
            decisions.Length == 0 ||
            decisions.Length != decisionTranscript.Length ||
            natureState.SampleCount != checked(
                initialNatureState.SampleCount + decisions.Length))
            throw new InvalidDataException(
                "Invalid completed ReferenceNature map audit prefix.");
        initialNatureState.Validate();
        natureState.Validate();
        var cursor = initialNatureState.Fork();
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            var transcript = decisionTranscript[index];
            ArgumentNullException.ThrowIfNull(decision);
            ArgumentNullException.ThrowIfNull(transcript);
            decision.Validate();
            transcript.Validate();
            var request = decision.ToRequest();
            var law = ChanceLawEngineV111.QueryMapRequest(request);
            var selection = ReferenceNatureV111.SampleValidated(law, cursor);
            var expectedDecision = ReferenceNatureChanceLawDecisionV111
                .FromSelection(law, request, selection);
            var expectedTranscript = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(decision) !=
                    CanonicalJson.Sha256(expectedDecision) ||
                CanonicalJson.Sha256(transcript) !=
                    CanonicalJson.Sha256(expectedTranscript))
                throw new InvalidDataException(
                    $"ReferenceNature map audit decision {index} does not replay.");
            cursor = selection.NextState;
        }
        if (!cursor.SameCursor(natureState))
            throw new InvalidDataException(
                "ReferenceNature map audit cursor differs from its decision transcript.");

        var captured = Capture(seed, act, ascension, decisions);
        if (captured.Continuation is not null ||
            captured.Value is null ||
            CanonicalJson.Sha256(captured.Value) != mapFingerprint)
            throw new InvalidDataException(
                "ReferenceNature map audit does not replay to its committed map fingerprint.");
    }

    private static MapState GenerateNative(
        string seed, int act, int ascension) => act switch
    {
        1 => PrototypeNativeOvergrowthMap.GenerateNative(seed, ascension),
        2 or 3 => PrototypeNativeLaterActMap.GenerateNative(
            seed, act, ascension),
        _ => throw new ArgumentOutOfRangeException(nameof(act))
    };

    internal static void ValidateMapParameters(int act, int ascension)
    {
        if (act is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(act),
                "ReferenceNature map generation supports Overgrowth, Hive, or Glory.");
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
    }

}
