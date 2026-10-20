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

    public ReferenceNatureRunInitializationTranscriptV111 Fork() => this with
    {
        InitialNatureState = InitialNatureState.Fork(),
        FinalNatureState = FinalNatureState.Fork(),
        ChanceLaws = ChanceLaws,
        DecisionTranscript = DecisionTranscript
    };

    public void Validate()
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
    }
}
