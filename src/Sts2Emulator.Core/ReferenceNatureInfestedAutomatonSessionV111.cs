using System.Text.Json;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureInfestedAutomatonSessionSnapshotV111(
    string SchemaId,
    string ChoiceId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
}

/// <summary>Atomic ReferenceNature replay for one Infested Automaton card
/// offer. The action choice fixes the eligible card pool before Nature samples
/// its index.</summary>
public sealed record ReferenceNatureInfestedAutomatonSessionV111(
    string SchemaId,
    string ChoiceId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-infested-automaton-session-v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Complete => DecisionTranscript.Length == 1;

    [System.Text.Json.Serialization.JsonIgnore]
    public string? LastSiteId => DecisionTranscript.Length == 0 ? null :
        PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId;

    [System.Text.Json.Serialization.JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNativeEventCardPick(
            Origin,
            PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            0, actionId: ChoiceId);

    public static string[] SessionSiteIds =>
        [PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId];

    public static ReferenceNatureInfestedAutomatonSessionV111 Begin(
        RunState origin, string choiceId, ulong natureSeed) => Begin(
            origin, choiceId, ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Infested Automaton"));

    public static ReferenceNatureInfestedAutomatonSessionV111 Begin(
        RunState origin, string choiceId) => Begin(
            origin, choiceId, ReferenceNatureRunCursorV111.Begin(
                origin, null, "Infested Automaton"));

    private static ReferenceNatureInfestedAutomatonSessionV111 Begin(
        RunState origin, string choiceId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        _ = NativeEventInfestedAutomatonCardPickPlanBuilderV111.Build(
            origin, PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            0, choiceId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Infested Automaton");
        var session = new ReferenceNatureInfestedAutomatonSessionV111(
            CurrentSchemaId, choiceId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureInfestedAutomatonSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureInfestedAutomatonSessionV111 ResolveNext()
    {
        Validate();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "Infested Automaton ReferenceNature session is already complete.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var transcript = new[]
        {
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)
        };
        var replay = ReplaySelection(Origin, ChoiceId, law, selection);
        var next = this with
        {
            Run = ReferenceNatureRunCursorV111.Commit(
                replay, selection.NextState),
            NatureState = selection.NextState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    private static RunState ReplaySelection(
        RunState origin, string choiceId, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var plan = NativeEventInfestedAutomatonCardPickPlanBuilderV111.Build(
            origin, PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            0, choiceId);
        var request = ControlledStochasticRequestV111.FromIndex(plan.Request);
        var selected = ReferenceNatureChanceLawDecisionV111
            .FromOrderedIndexSelection(law, request, selection,
                plan.CandidateCardIds.Select(cardId => "card:" + cardId)
                    .ToArray());
        return ReferenceNatureNativeEventIndexReplayV111
            .ReplayControlledIndex(
                origin, plan.Request, selected,
                run => new PrototypeGameEngine().Step(
                    run, GameAction.Create("event_choice",
                        new EventChoicePayload(choiceId))).State,
                PrototypeNativeHiveEvents.InfestedAutomatonNativeRngEventId,
                "Infested Automaton");
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            ChoiceId is not ("study" or "touch_core") ||
            Origin is null || Run is null || InitialNatureState is null ||
            NatureState is null || DecisionTranscript is null ||
            DecisionTranscript.Length > 1)
            throw new InvalidDataException(
                "Invalid ReferenceNature Infested Automaton session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        _ = NativeEventInfestedAutomatonCardPickPlanBuilderV111.Build(
            Origin, PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            0, ChoiceId);

        var expectedNature = InitialNatureState.Fork();
        var expectedRun = Origin.Fork();
        if (Complete)
        {
            var law = ChanceLawEngineV111.QueryNativeEventCardPick(
                Origin,
                PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
                0, actionId: ChoiceId);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNature);
            DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[0]))
                throw new InvalidDataException(
                    "Infested Automaton Nature transcript does not match its offer law.");
            expectedNature = selection.NextState;
            expectedRun = ReplaySelection(
                Origin, ChoiceId, law, selection);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNature);
        }
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete) ||
            CanonicalJson.Sha256(expectedNature) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Infested Automaton Native replay differs from its session envelope.");
    }

    public static string Save(
        ReferenceNatureInfestedAutomatonSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureInfestedAutomatonSessionSnapshotV111(
                CurrentSchemaId, session.ChoiceId,
                RunSnapshot.Save(session.Origin),
                RunSnapshot.Save(session.Run), session.InitialNatureState,
                session.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.DecisionTranscript.Clone()));
    }

    public static ReferenceNatureInfestedAutomatonSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureInfestedAutomatonSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Infested Automaton snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            snapshot.ChoiceId is not ("study" or "touch_core") ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Infested Automaton snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var session = Begin(origin, snapshot.ChoiceId,
            snapshot.InitialNatureState.Fork());
        if (snapshot.DecisionTranscript.Length == 1)
            session = session.ResolveNext();
        session.Validate();
        if (CanonicalJson.Sha256(session.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(session.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !session.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "Infested Automaton snapshot does not replay from its origin.");
        return session;
    }

}
