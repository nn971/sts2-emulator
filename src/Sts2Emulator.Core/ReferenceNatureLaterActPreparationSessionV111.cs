using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed class ReferenceNatureLaterActPendingChoiceException : Exception { }

public sealed record ReferenceNatureLaterActPreparationSessionSnapshotV111(
    string SchemaId,
    RngBundle InitialPrototypeRng,
    int Ascension,
    SimulationProfile Profile,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private RngBundle? _initialPrototypeRng = InitialPrototypeRng?.Fork();
    private SimulationProfile? _profile = Profile?.Fork();
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        Clone(DecisionTranscript);

    public RngBundle InitialPrototypeRng
    {
        get => _initialPrototypeRng?.Fork()!;
        init => _initialPrototypeRng = value?.Fork();
    }

    public SimulationProfile Profile
    {
        get => _profile?.Fork()!;
        init => _profile = value?.Fork();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Clone(_decisionTranscript)!;
        init => _decisionTranscript = Clone(value);
    }

    private static ReferenceNatureDecisionV111[]? Clone(
        ReferenceNatureDecisionV111[]? values) => values is null ? null :
        (ReferenceNatureDecisionV111[])values.Clone();
}

/// <summary>
/// Resumable Nature continuation for the existing experimental Hive/Glory
/// Ancient assignments and encounter-bag generators. It replays those
/// generators from their original RNG bundle under a typed Nature prefix.
/// </summary>
public sealed record ReferenceNatureLaterActPreparationSessionV111(
    string SchemaId,
    RngBundle InitialPrototypeRng,
    int Ascension,
    SimulationProfile Profile,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript,
    RandomIndexRequestV111? PendingRequest,
    PrototypeLaterActEncounterBagV111[]? Bags)
{
    private RngBundle? _initialPrototypeRng = InitialPrototypeRng?.Fork();
    private SimulationProfile? _profile = Profile?.Fork();
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();
    private ChanceLawV111? _pendingLaw = PendingLaw?.Fork();
    private RandomIndexRequestV111? _pendingRequest = Copy(PendingRequest);
    private PrototypeLaterActEncounterBagV111[]? _bags = Bags?.Select(bag =>
        bag.Fork()).ToArray();

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

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-later-act-preparation-session-v1";

    public RngBundle InitialPrototypeRng
    {
        get => _initialPrototypeRng?.Fork()!;
        init => _initialPrototypeRng = value?.Fork();
    }

    public SimulationProfile Profile
    {
        get => _profile?.Fork()!;
        init => _profile = value?.Fork();
    }

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public RandomIndexRequestV111? PendingRequest
    {
        get => Copy(_pendingRequest);
        init => _pendingRequest = Copy(value);
    }

    public PrototypeLaterActEncounterBagV111[]? Bags
    {
        get => _bags?.Select(bag => bag.Fork()).ToArray();
        init => _bags = value?.Select(bag => bag.Fork()).ToArray();
    }

    [JsonIgnore]
    public bool Complete => Bags is not null && PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get => _pendingLaw?.Fork();
        private init => _pendingLaw = value?.Fork();
    }

    public static ReferenceNatureLaterActPreparationSessionV111 Begin(
        RngBundle initialPrototypeRng, int ascension,
        SimulationProfile profile,
        ReferenceNatureStateV111 initialNatureState) => Rebuild(
            initialPrototypeRng, ascension, profile, initialNatureState, []);

    public ReferenceNatureLaterActPreparationSessionV111 Fork()
    {
        Validate();
        return this with
        {
            InitialPrototypeRng = InitialPrototypeRng.Fork(),
            Profile = Profile.Fork(),
            Stochastic = Stochastic.Fork(),
            ChanceLaws = ChanceLaws,
            PendingRequest = PendingRequest,
            Bags = Bags
        };
    }

    public ReferenceNatureLaterActPreparationSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature later-act preparation is already complete.");
        var law = PendingLaw ?? throw new InvalidDataException(
            "Incomplete later-act preparation has no pending Chance Law.");
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var decision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        var next = Rebuild(InitialPrototypeRng, Ascension, Profile,
            InitialNatureState, DecisionTranscript.Append(decision).ToArray());
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || InitialPrototypeRng is null ||
            Profile is null || ChanceLaws is null ||
            Ascension is < 0 or > 10)
            throw new InvalidDataException(
                "Invalid ReferenceNature later-act preparation session.");
        Stochastic.Validate();
        var capture = Capture(InitialPrototypeRng, Ascension, Profile,
            InitialNatureState, DecisionTranscript);
        if (!NatureState.SameCursor(capture.NatureState) ||
            ChanceLaws.Length != DecisionTranscript.Length ||
            CanonicalJson.Sha256(ChanceLaws) !=
                CanonicalJson.Sha256(capture.ChanceLaws) ||
            !SameRequest(PendingRequest, capture.PendingRequest) ||
            (PendingLaw is null) != (capture.PendingLaw is null) ||
            PendingLaw is not null && CanonicalJson.Sha256(PendingLaw) !=
                CanonicalJson.Sha256(capture.PendingLaw) ||
            (Bags is null) != (capture.Bags is null) ||
            Bags is not null && CanonicalJson.Sha256(Bags) !=
                CanonicalJson.Sha256(capture.Bags))
            throw new InvalidDataException(
                "ReferenceNature later-act preparation differs from its generator replay.");
        if (NatureState.SampleCount != checked(
                InitialNatureState.SampleCount + DecisionTranscript.Length) ||
            Complete != (PendingRequest is null) ||
            Complete != (PendingLaw is null))
            throw new InvalidDataException(
                "ReferenceNature later-act cursor and completion state differ.");
    }

    public static string Save(
        ReferenceNatureLaterActPreparationSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureLaterActPreparationSessionSnapshotV111(
                CurrentSchemaId, session.InitialPrototypeRng,
                session.Ascension, session.Profile,
                session.InitialNatureState, session.DecisionTranscript));
    }

    public static ReferenceNatureLaterActPreparationSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureLaterActPreparationSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature later-act preparation snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            snapshot.InitialPrototypeRng is null || snapshot.Profile is null ||
            snapshot.InitialNatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature later-act preparation snapshot.");
        return Rebuild(snapshot.InitialPrototypeRng, snapshot.Ascension,
            snapshot.Profile, snapshot.InitialNatureState,
            snapshot.DecisionTranscript);
    }

    private static ReferenceNatureLaterActPreparationSessionV111 Rebuild(
        RngBundle initialPrototypeRng, int ascension,
        SimulationProfile profile,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[] decisionTranscript)
    {
        var capture = Capture(initialPrototypeRng, ascension, profile,
            initialNatureState, decisionTranscript);
        var session = new ReferenceNatureLaterActPreparationSessionV111(
            CurrentSchemaId, initialPrototypeRng.Fork(), ascension,
            profile.Fork(), initialNatureState.Fork(), capture.NatureState,
            capture.ChanceLaws, (ReferenceNatureDecisionV111[])
                decisionTranscript.Clone(), capture.PendingRequest,
            capture.Bags)
        {
            PendingLaw = capture.PendingLaw
        };
        session.Validate();
        return session;
    }

    private static CaptureResult Capture(
        RngBundle initialPrototypeRng, int ascension,
        SimulationProfile profile,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[] decisionTranscript)
    {
        ArgumentNullException.ThrowIfNull(initialPrototypeRng);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        PrototypeRng.ValidateBundle(initialPrototypeRng,
            allowEmpty: false);
        initialNatureState.Validate();
        var assignmentCount = profile.RevealedEpochs.Contains(
            NativeDarvEventV111.EpochId, StringComparer.Ordinal) ? 4 : 2;
        if (decisionTranscript.Length > assignmentCount +
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15 +
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(3) + 15 +
            (ascension >= 10 ? 3 : 2))
            throw new InvalidDataException(
                "Later-act preparation transcript exceeds its generator boundary.");

        var assignmentsPrefix = decisionTranscript.Take(assignmentCount)
            .ToArray();
        var assignmentProvider =
            new ReferenceNatureLaterActAssignmentIndexProviderV111(
                initialNatureState, assignmentsPrefix,
                suspendAfterPrefix: true);
        string[] assignments;
        var assignmentRng = initialPrototypeRng.Fork();
        try
        {
            using (StochasticProviderScopeV111.Enter(
                       indexProvider: assignmentProvider,
                       overrideExplicitProviders: true))
                assignments = ExperimentalSilentThreeActRouteV111
                    .PrepareAncientAssignments(assignmentRng, ascension,
                        profile);
        }
        catch (ReferenceNatureLaterActPendingChoiceException)
        {
            return new CaptureResult(assignmentProvider.NatureState,
                assignmentProvider.ChanceLaws,
                assignmentProvider.PendingRequest,
                assignmentProvider.PendingLaw, null);
        }

        if (assignmentProvider.DecisionTranscript.Length != assignmentCount ||
            assignmentProvider.ChanceLaws.Length != assignmentCount)
            throw new InvalidDataException(
                "Ancient assignment replay ended before its declared choices.");

        var encounterPrefix = decisionTranscript.Skip(assignmentCount)
            .ToArray();
        var encounterProvider =
            new ReferenceNatureLaterActEncounterIndexProviderV111(
                initialPrototypeRng, assignmentProvider.NatureState,
                ascension, encounterPrefix, suspendAfterPrefix: true);
        var encounterRng = initialPrototypeRng.Fork();
        PrototypeLaterActEncounterBagV111[] bags;
        try
        {
            using (StochasticProviderScopeV111.Enter(
                       indexProvider: encounterProvider,
                       overrideExplicitProviders: true))
                bags = new[] { 2, 3 }.Select((act, index) =>
                    ExperimentalSilentThreeActRouteV111.InitialBag(
                        act, encounterRng, ascension, profile,
                        assignAncient: false) with
                    {
                        AssignedAncientId = assignments[index]
                    }).ToArray();
        }
        catch (ReferenceNatureLaterActPendingChoiceException)
        {
            return new CaptureResult(encounterProvider.NatureState,
                assignmentProvider.ChanceLaws.Concat(
                    encounterProvider.ChanceLaws).ToArray(),
                encounterProvider.PendingRequest,
                encounterProvider.PendingLaw, null);
        }

        var eventStream = initialPrototypeRng.Streams.Single(stream =>
            stream.StreamId == "event");
        var assignmentsResult = new ReferenceNatureLaterActAssignmentsV111(
            assignments, initialNatureState,
            assignmentProvider.NatureState,
            checked((int)(eventStream.CallCount ?? 0)),
            assignmentProvider.ChanceLaws,
            assignmentProvider.DecisionTranscript);
        assignmentsResult.Validate(profile);
        var bagDecisions = encounterProvider.DecisionTranscript;
        var bagLaws = encounterProvider.ChanceLaws;
        var combatStream = initialPrototypeRng.Streams.Single(stream =>
            stream.StreamId == "combat");
        var bagResult = new ReferenceNatureLaterActEncounterPreparationV111(
            bags, initialPrototypeRng, assignmentProvider.NatureState,
            encounterProvider.NatureState,
            checked((int)(combatStream.CallCount ?? 0)), ascension,
            bagLaws, bagDecisions);
        bagResult.Validate(profile);
        var allDecisions = assignmentsResult.DecisionTranscript
            .Concat(bagResult.DecisionTranscript).ToArray();
        if (allDecisions.Length != decisionTranscript.Length ||
            !allDecisions.SequenceEqual(decisionTranscript))
            throw new InvalidDataException(
                "Completed later-act generator prefix differs from its session transcript.");
        return new CaptureResult(bagResult.NatureState,
            assignmentsResult.ChanceLaws.Concat(bagResult.ChanceLaws).ToArray(),
            null, null, bagResult.Bags);
    }

    private sealed record CaptureResult(
        ReferenceNatureStateV111 NatureState,
        ChanceLawV111[] ChanceLaws,
        RandomIndexRequestV111? PendingRequest,
        ChanceLawV111? PendingLaw,
        PrototypeLaterActEncounterBagV111[]? Bags);

    private static bool SameRequest(
        RandomIndexRequestV111? left, RandomIndexRequestV111? right) =>
        left is null ? right is null : right is not null &&
        CanonicalJson.Sha256(left) == CanonicalJson.Sha256(right);

    private static RandomIndexRequestV111? Copy(
        RandomIndexRequestV111? request) => request is null ? null :
        request with
        {
            OrderedCandidates = (int[])request.OrderedCandidates.Clone()
        };

}
