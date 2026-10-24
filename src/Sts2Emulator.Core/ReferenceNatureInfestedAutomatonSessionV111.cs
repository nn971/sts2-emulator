using System.Text.Json;
using System.Text.Json.Serialization;

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
    : IReferenceNatureControlledActionSessionV111
{
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<PrototypeMechanicsContinuationStateV111,
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>>
        Simulation { get; init; } = new(
            new PrototypeMechanicsContinuationStateV111(
                PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession,
                Origin, Run),
            new ReferenceNatureStochasticStateV111<
                ReferenceNatureDecisionV111>(InitialNatureState, NatureState,
                DecisionTranscript));

    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeMechanicsContinuationStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin!;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public RunState Run
    {
        get => Mechanics.Current;
        init => Mechanics = Mechanics with { Current = value };
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

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    [JsonIgnore]
    public GameAction Action => GameAction.Create("event_choice",
        new EventChoicePayload(ChoiceId));

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "A pending Infested Automaton session has no completed provider prefix.");
            var plan = NativeEventInfestedAutomatonCardPickPlanBuilderV111
                .Build(Origin,
                    PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
                    0, ChoiceId);
            var law = ChanceLawEngineV111.QueryNativeEventCardPick(
                Origin,
                PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
                0, actionId: ChoiceId);
            var labels = plan.CandidateCardIds
                .Select(cardId => "card:" + cardId).ToArray();
            return [ReferenceNatureChanceLawDecisionV111.FromInitialState(
                law, ControlledStochasticRequestV111.FromIndex(plan.Request),
                Stochastic.InitialNatureState, labels)];
        }
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-infested-automaton-session-v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Complete => Stochastic.DecisionTranscript.Length == 1;

    [System.Text.Json.Serialization.JsonIgnore]
    public string? LastSiteId => Stochastic.DecisionTranscript.Length == 0 ? null :
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
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureInfestedAutomatonSessionV111 ResolveNext()
    {
        Validate();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "Infested Automaton ReferenceNature session is already complete.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, Stochastic.NatureState);
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
        if (origin.Stochastic.NativeRunRng is null)
        {
            var replay = ControlledChanceContinuationExecutorV111
                .CaptureStochastic(
                    origin,
                    ControlledStochasticContinuationStateV111
                        .CurrentSchemaId,
                    [selected],
                    run => new PrototypeGameEngine().Step(
                        run, GameAction.Create("event_choice",
                            new EventChoicePayload(choiceId))).State,
                    candidate =>
                        ControlledStochasticChanceReplayV111.RequestsMatch(
                            candidate, request));
            if (replay.Suspended || replay.Run is not { } fallbackRun)
                throw new InvalidDataException(
                    "Infested Automaton fallback replay did not consume its selected request.");
            return fallbackRun;
        }
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
            Origin is null || Run is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Infested Automaton session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        if (Stochastic.DecisionTranscript.Length > 1)
            throw new InvalidDataException(
                "Invalid ReferenceNature Infested Automaton session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        _ = NativeEventInfestedAutomatonCardPickPlanBuilderV111.Build(
            Origin, PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
            0, ChoiceId);

        var expectedNature = Stochastic.InitialNatureState.Fork();
        var expectedRun = Origin.Fork();
        if (Complete)
        {
            var law = ChanceLawEngineV111.QueryNativeEventCardPick(
                Origin,
                PrototypeNativeHiveEvents.InfestedAutomatonCardOfferSiteId,
                0, actionId: ChoiceId);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNature);
            Stochastic.DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(Stochastic.DecisionTranscript[0]))
                throw new InvalidDataException(
                    "Infested Automaton Nature transcript does not match its offer law.");
            expectedNature = selection.NextState;
            expectedRun = ReplaySelection(
                Origin, ChoiceId, law, selection);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNature);
        }
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState,
                Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete) ||
            CanonicalJson.Sha256(expectedNature) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
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
                RunSnapshot.Save(session.Run),
                session.Stochastic.InitialNatureState,
                session.Stochastic.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone()));
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
            CanonicalJson.Sha256(session.Stochastic.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !session.Stochastic.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "Infested Automaton snapshot does not replay from its origin.");
        return session;
    }

}
