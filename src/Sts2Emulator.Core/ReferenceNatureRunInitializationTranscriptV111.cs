namespace Sts2Emulator.Core;

/// <summary>
/// Validated ReferenceNature choices made synchronously while a configured
/// run is initialized. The transcript is retained with run provider state so
/// a snapshot can replay the sampler prefix after initialization has ended.
/// </summary>
public sealed record ReferenceNatureRunInitializationTranscriptV111(
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 FinalNatureState,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        DecisionTranscript is null ? null :
            (ReferenceNatureDecisionV111[])DecisionTranscript.Clone();
    private ReferenceNatureMapGenerationAuditV111? _actOneMapGeneration;

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }
    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => _decisionTranscript is null ? null! :
            (ReferenceNatureDecisionV111[])_decisionTranscript.Clone();
        init => _decisionTranscript = value is null ? null :
            (ReferenceNatureDecisionV111[])value.Clone();
    }
    public ReferenceNatureMapGenerationAuditV111? ActOneMapGeneration
    {
        get => _actOneMapGeneration?.Fork();
        init => _actOneMapGeneration = value?.Fork();
    }

    public ReferenceNatureRunInitializationTranscriptV111 Fork() => this with
    {
        InitialNatureState = InitialNatureState.Fork(),
        FinalNatureState = FinalNatureState.Fork(),
        ChanceLaws = ChanceLaws,
        DecisionTranscript = DecisionTranscript,
        ActOneMapGeneration = ActOneMapGeneration
    };

    public void Validate(bool replayMapGeneration = false)
    {
        ArgumentNullException.ThrowIfNull(InitialNatureState);
        ArgumentNullException.ThrowIfNull(FinalNatureState);
        InitialNatureState.Validate();
        FinalNatureState.Validate();
        var laws = ChanceLaws;
        var decisions = DecisionTranscript;
        if (laws is null || decisions is null || laws.Length == 0 ||
            laws.Length != decisions.Length)
            throw new InvalidDataException(
                "Invalid ReferenceNature run-initialization transcript length.");

        var cursor = InitialNatureState.Fork();
        for (var index = 0; index < laws.Length; index++)
        {
            var law = laws[index];
            var decision = decisions[index];
            ArgumentNullException.ThrowIfNull(law);
            ArgumentNullException.ThrowIfNull(decision);
            law.Validate();
            decision.Validate();
            var selection = ReferenceNatureV111.SampleValidated(law, cursor);
            var expected = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expected) !=
                CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    $"ReferenceNature run-initialization decision {index} does not replay.");
            cursor = selection.NextState;
        }

        if (!cursor.SameCursor(FinalNatureState) ||
            FinalNatureState.SampleCount != checked(
                InitialNatureState.SampleCount + laws.Length))
            throw new InvalidDataException(
                "ReferenceNature run-initialization cursor does not match its transcript.");

        if (ActOneMapGeneration is { } mapGeneration)
        {
            mapGeneration.ValidateEnvelope();
            var mapDecisions = mapGeneration.DecisionTranscript;
            var typedMapDecisions = mapGeneration.Decisions;
            if (laws.Length < checked(mapDecisions.Length + 1) ||
                typedMapDecisions.Length != mapDecisions.Length)
                throw new InvalidDataException(
                    "Run-initialization transcript omits part of its Overgrowth map prefix.");

            var mapCursor = ReferenceNatureV111.SampleValidated(
                laws[0], InitialNatureState).NextState;
            if (!mapCursor.SameCursor(mapGeneration.InitialNatureState))
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
            if (!mapCursor.SameCursor(mapGeneration.NatureState))
                throw new InvalidDataException(
                    "Overgrowth map audit cursor differs from the run-start law prefix.");
            if (replayMapGeneration)
                mapGeneration.Validate();
        }
    }
}
