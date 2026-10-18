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
    MapState? Map);

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
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-map-generation-session-v1";

    [JsonIgnore]
    public bool Complete => Map is not null && PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNativeMapRequest(PendingRequest ??
            throw new InvalidDataException(
                "Incomplete map session has no pending request."));

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ReferenceNatureMapGenerationSessionV111 Begin(
        string seed, int act, int ascension, ulong natureSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ValidateMapParameters(act, ascension);
        var initial = ReferenceNatureStateV111.Create(natureSeed);
        var captured = Capture(seed, act, ascension, []);
        var session = new ReferenceNatureMapGenerationSessionV111(
            CurrentSchemaId, seed, act, ascension, initial, initial,
            [], [], captured.PendingRequest, captured.Map);
        session.Validate();
        return session;
    }

    public ReferenceNatureMapGenerationSessionV111 Fork()
    {
        Validate();
        return this with
        {
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            Decisions = Decisions.Select(decision => decision.Fork()).ToArray(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone(),
            PendingRequest = PendingRequest?.Fork(),
            Map = Map?.Fork()
        };
    }

    public ReferenceNatureMapGenerationSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature map-generation session is already complete.");
        var law = PendingLaw ?? throw new InvalidDataException(
            "Incomplete map session has no pending Chance Law.");
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var typedDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, PendingRequest!, selection);
        var decisions = Decisions.Append(typedDecision).ToArray();
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var captured = Capture(Seed, Act, Ascension, decisions);
        var next = this with
        {
            NatureState = selection.NextState,
            Decisions = decisions,
            DecisionTranscript = transcript,
            PendingRequest = captured.PendingRequest,
            Map = captured.Map
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(Seed) || InitialNatureState is null ||
            NatureState is null || Decisions is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature map-generation session envelope.");
        ValidateMapParameters(Act, Ascension);
        InitialNatureState.Validate();
        NatureState.Validate();
        if (Decisions.Length != DecisionTranscript.Length ||
            NatureState.SampleCount != checked(
                InitialNatureState.SampleCount + Decisions.Length) ||
            Complete != (PendingRequest is null))
            throw new InvalidDataException(
                "ReferenceNature map cursor, transcript, and completion state differ.");

        var expectedNatureState = InitialNatureState.Fork();
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            var transcript = DecisionTranscript[index];
            if (decision is null || transcript is null)
                throw new InvalidDataException(
                    "Map-generation transcript contains a null decision.");
            decision.Validate();
            transcript.Validate();
            var request = decision.ToRequest();
            var law = ChanceLawEngineV111.QueryNativeMapRequest(request);
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
        if (!expectedNatureState.SameCursor(NatureState))
            throw new InvalidDataException(
                "ReferenceNature map sampler cursor differs from its transcript.");

        var captured = Capture(Seed, Act, Ascension, Decisions);
        if (!SameRequest(captured.PendingRequest, PendingRequest) ||
            (captured.Map is null) != (Map is null) ||
            captured.Map is not null &&
                CanonicalJson.Sha256(captured.Map) != CanonicalJson.Sha256(Map))
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
                session.InitialNatureState, session.NatureState,
                session.Decisions.Select(decision => decision.Fork()).ToArray(),
                (ReferenceNatureDecisionV111[])
                    session.DecisionTranscript.Clone(),
                session.PendingRequest?.Fork(), session.Map?.Fork()));
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

    private static (ControlledStochasticRequestV111? PendingRequest,
        MapState? Map) Capture(
        string seed, int act, int ascension,
        ControlledStochasticDecisionV111[] decisions)
    {
        using var replay = ControlledStochasticChanceReplayV111.Begin(decisions);
        using var providerScope = StochasticProviderScopeV111.EnterBoundary(replay);
        try
        {
            var map = GenerateNative(seed, act, ascension);
            replay.EnsureConsumed();
            return (null, map.Fork());
        }
        catch (ControlledStochasticChanceRequiredExceptionV111 pending)
        {
            return (pending.Request.Fork(), null);
        }
    }

    private static MapState GenerateNative(
        string seed, int act, int ascension) => act switch
    {
        1 => PrototypeNativeOvergrowthMap.GenerateNative(seed, ascension),
        2 or 3 => PrototypeNativeLaterActMap.GenerateNative(
            seed, act, ascension),
        _ => throw new ArgumentOutOfRangeException(nameof(act))
    };

    private static void ValidateMapParameters(int act, int ascension)
    {
        if (act is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(act),
                "ReferenceNature map generation supports Overgrowth, Hive, or Glory.");
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
    }

    private static bool SameRequest(
        ControlledStochasticRequestV111? left,
        ControlledStochasticRequestV111? right) =>
        left is null ? right is null : right is not null &&
            ControlledStochasticChanceReplayV111.SameRequest(left, right);
}
