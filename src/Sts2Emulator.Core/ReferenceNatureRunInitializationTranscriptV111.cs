namespace Sts2Emulator.Core;

/// <summary>
/// Validated ReferenceNature choices made while a configured run is initialized.
/// The transcript is retained with run provider state so a snapshot can replay
/// the sampler prefix and, for experimental starts, rebuild the prepared bags.
/// </summary>
public sealed record ReferenceNatureRunInitializationTranscriptV111(
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 FinalNatureState,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();
    private ReferenceNatureMapGenerationAuditV111? _actOneMapGeneration;
    private RngBundle? _laterActPreparationInitialPrototypeRng;

    [System.Text.Json.Serialization.JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, FinalNatureState,
            DecisionTranscript);

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 FinalNatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
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
    public ReferenceNatureMapGenerationAuditV111? ActOneMapGeneration
    {
        get => _actOneMapGeneration?.Fork();
        init => _actOneMapGeneration = value?.Fork();
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RngBundle? LaterActPreparationInitialPrototypeRng
    {
        get => _laterActPreparationInitialPrototypeRng?.Fork();
        init => _laterActPreparationInitialPrototypeRng = value?.Fork();
    }

    public ReferenceNatureRunInitializationTranscriptV111 Fork() => this with
    {
        Stochastic = Stochastic.Fork(),
        ChanceLaws = ChanceLaws,
        ActOneMapGeneration = ActOneMapGeneration,
        LaterActPreparationInitialPrototypeRng =
            LaterActPreparationInitialPrototypeRng
    };

    public void Validate(bool replayMapGeneration = false)
    {
        Stochastic.Validate();
        var stochastic = Stochastic;
        var laws = ChanceLaws;
        var decisions = stochastic.DecisionTranscript;
        if (laws is null || decisions is null || laws.Length == 0 ||
            laws.Length != decisions.Length)
            throw new InvalidDataException(
                "Invalid ReferenceNature run-initialization transcript length.");

        var cursor = stochastic.InitialNatureState.Fork();
        for (var index = 0; index < laws.Length; index++)
        {
            var law = laws[index];
            var decision = decisions[index];
            ArgumentNullException.ThrowIfNull(law);
            ArgumentNullException.ThrowIfNull(decision);
            law.Validate();
            decision.Validate();
            var selection = ReferenceNatureV111.SampleValidated(law, cursor);
            var expected = ReferenceNatureDecisionV111.FromSelection(
                law, selection);
            if (CanonicalJson.Sha256(expected) !=
                CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    $"ReferenceNature run-initialization decision {index} does not replay.");
            cursor = selection.NextState;
        }

        if (!cursor.SameCursor(stochastic.NatureState) ||
            stochastic.NatureState.SampleCount != checked(
                stochastic.InitialNatureState.SampleCount + laws.Length))
            throw new InvalidDataException(
                "ReferenceNature run-initialization cursor does not match its transcript.");

        if (ActOneMapGeneration is { } mapGeneration)
        {
            mapGeneration.ValidateEnvelope();
            var mapDecisions = mapGeneration.Stochastic.DecisionTranscript;
            var typedMapDecisions = mapGeneration.Decisions;
            if (laws.Length < checked(mapDecisions.Length + 1) ||
                typedMapDecisions.Length != mapDecisions.Length)
                throw new InvalidDataException(
                    "Run-initialization transcript omits part of its Overgrowth map prefix.");

            var mapCursor = ReferenceNatureV111.SampleValidated(
                laws[0], stochastic.InitialNatureState).NextState;
            if (!mapCursor.SameCursor(
                    mapGeneration.Stochastic.InitialNatureState))
                throw new InvalidDataException(
                    "Overgrowth map prefix does not follow its boss selection.");
            for (var index = 0; index < mapDecisions.Length; index++)
            {
                var mapLaw = ChanceLawEngineV111.QueryNativeMapRequest(
                    typedMapDecisions[index].ToRequest());
                var mapSelection = ReferenceNatureV111.SampleValidated(
                    mapLaw, mapCursor);
                var transcriptIndex = index + 1;
                if (CanonicalJson.Sha256(mapLaw) !=
                        CanonicalJson.Sha256(laws[transcriptIndex]) ||
                    CanonicalJson.Sha256(mapDecisions[index]) !=
                        CanonicalJson.Sha256(decisions[transcriptIndex]))
                    throw new InvalidDataException(
                        $"Overgrowth map prefix differs from run-start transcript at {index}.");
                mapCursor = mapSelection.NextState;
            }
            if (!mapCursor.SameCursor(mapGeneration.Stochastic.NatureState))
                throw new InvalidDataException(
                    "Overgrowth map audit cursor differs from the run-start law prefix.");
            if (replayMapGeneration)
                mapGeneration.Validate();
        }
    }
}
