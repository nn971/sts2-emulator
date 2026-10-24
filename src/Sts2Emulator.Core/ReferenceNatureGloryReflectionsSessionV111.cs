using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureGloryReflectionsSessionSnapshotV111(
    string SchemaId,
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

/// <summary>Atomic Nature replay for the Reflections touch_mirror action.
/// Downgrade and upgrade picks share one ordered prefix and event action.</summary>
public sealed record ReferenceNatureGloryReflectionsSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<PrototypeMechanicsContinuationStateV111,
        ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>>
        Simulation { get; init; } = new(
            new PrototypeMechanicsContinuationStateV111(
                PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession,
                Origin, Run),
            new ReferenceNatureStochasticStateV111<
                ReferenceNatureDecisionV111>(InitialNatureState, NatureState,
                DecisionTranscript));

    [JsonIgnore]
    public PrototypeMechanicsContinuationStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
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
        new EventChoicePayload("touch_mirror"));

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "A pending Glory Reflections session has no completed provider prefix.");
            var natureState = Stochastic.InitialNatureState.Fork();
            var priorIndices = new List<int>();
            var decisions = new List<ControlledStochasticDecisionV111>(
                Stochastic.DecisionTranscript.Length);
            for (var ordinal = 0;
                 ordinal < Stochastic.DecisionTranscript.Length; ordinal++)
            {
                var prior = priorIndices.ToArray();
                var plan = NativeEventReflectionsChoicePlanBuilderV111
                    .TryBuildNext(Origin, null, ordinal, prior,
                        "touch_mirror")
                    ?? throw new InvalidDataException(
                        "Reflections provider prefix extends beyond its draw sequence.");
                var law = ChanceLawEngineV111.QueryNativeEventCardPick(
                    Origin, plan.Request.SiteId, ordinal, prior,
                    "touch_mirror");
                var selection = ReferenceNatureV111.SampleValidated(
                    law, natureState);
                var expected = new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex);
                if (CanonicalJson.Sha256(expected) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[ordinal]))
                    throw new InvalidDataException(
                        "Reflections provider prefix differs from its Nature transcript.");
                var labels = plan.CandidateCardInstanceIds.Select(instanceId =>
                    "card_instance:" + instanceId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray();
                var decision = ReferenceNatureChanceLawDecisionV111
                    .FromOrderedIndexSelection(law,
                        ControlledStochasticRequestV111.FromIndex(
                            plan.Request), selection, labels);
                var indexDecision = decision.IndexDecision ??
                    throw new InvalidDataException(
                        "Reflections provider replay requires an index decision.");
                decisions.Add(ControlledStochasticDecisionV111.FromIndex(
                    indexDecision, labels));
                priorIndices.Add(selection.OutcomeIndex);
                natureState = selection.NextState;
            }
            return decisions.ToArray();
        }
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-glory-reflections-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => Stochastic.DecisionTranscript.Length;

    [JsonIgnore]
    public string? LastSiteId => Stochastic.DecisionTranscript.Length == 0 ? null :
        NativeEventReflectionsChoicePlanBuilderV111.TryBuildNext(
            Origin, null, Stochastic.DecisionTranscript.Length - 1,
            Stochastic.DecisionTranscript.Take(Stochastic.DecisionTranscript.Length - 1)
                .Select(decision => decision.OutcomeIndex).ToArray(),
            "touch_mirror")?.Request.SiteId;

    [JsonIgnore]
    public bool Complete =>
        NativeEventReflectionsChoicePlanBuilderV111.TryBuildNext(
            Origin, null, Stochastic.DecisionTranscript.Length,
            Stochastic.DecisionTranscript.Select(
                decision => decision.OutcomeIndex)
                .ToArray(), "touch_mirror") is null;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var priorIndices = Stochastic.DecisionTranscript.Select(
                decision => decision.OutcomeIndex).ToArray();
            var plan = NativeEventReflectionsChoicePlanBuilderV111
                .TryBuildNext(Origin, null, priorIndices.Length,
                    priorIndices, "touch_mirror")
                ?? throw new InvalidDataException(
                    "Reflections Nature session lost its pending card draw.");
            return ChanceLawEngineV111.QueryNativeEventCardPick(
                Origin, plan.Request.SiteId, priorIndices.Length,
                priorIndices, "touch_mirror");
        }
    }

    public static string[] SessionSiteIds =>
        StochasticSiteRegistryV111.NativeEventCardPickSiteIds;

    public static ReferenceNatureGloryReflectionsSessionV111 Begin(
        RunState origin, ulong natureSeed) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Glory Reflections"));

    public static ReferenceNatureGloryReflectionsSessionV111 Begin(
        RunState origin) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Glory Reflections"));

    private static ReferenceNatureGloryReflectionsSessionV111 Begin(
        RunState origin, ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        if (NativeEventReflectionsChoicePlanBuilderV111.TryBuildNext(
                origin, null, 0, [], "touch_mirror") is null)
            throw new InvalidOperationException(
                "Reflections touch_mirror has no random card picks.");
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Glory Reflections");
        var session = new ReferenceNatureGloryReflectionsSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureGloryReflectionsSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureGloryReflectionsSessionV111 ResolveNext()
    {
        Validate();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "Reflections ReferenceNature session is already complete.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNature = selection.NextState;
        var indices = transcript.Select(
            decision => decision.OutcomeIndex).ToArray();
        var complete = NativeEventReflectionsChoicePlanBuilderV111
            .TryBuildNext(Origin, null, indices.Length, indices,
                "touch_mirror") is null;
        var nextRun = Run;
        if (complete)
        {
            var decisions = BuildIndexDecisions(Origin, transcript);
            var replay = ReplayTouchMirror(Origin, decisions);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                replay, nextNature);
        }

        var next = this with
        {
            Run = nextRun,
            NatureState = nextNature,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Glory Reflections session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);

        var expectedNature = Stochastic.InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var ordinal = 0; ordinal < Stochastic.DecisionTranscript.Length; ordinal++)
        {
            var plan = NativeEventReflectionsChoicePlanBuilderV111
                .TryBuildNext(Origin, null, ordinal,
                    priorIndices.ToArray(), "touch_mirror")
                ?? throw new InvalidDataException(
                    "Reflections transcript continues after its draw sequence ended.");
            var law = ChanceLawEngineV111.QueryNativeEventCardPick(
                Origin, plan.Request.SiteId, ordinal,
                priorIndices.ToArray(), "touch_mirror");
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNature);
            Stochastic.DecisionTranscript[ordinal].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(Stochastic.DecisionTranscript[ordinal]))
                throw new InvalidDataException(
                    "Reflections Nature transcript does not replay from its prefix.");
            expectedNature = selection.NextState;
            priorIndices.Add(selection.OutcomeIndex);
        }

        var complete = NativeEventReflectionsChoicePlanBuilderV111
            .TryBuildNext(Origin, null, priorIndices.Count,
                priorIndices.ToArray(), "touch_mirror") is null;
        var expectedRun = Origin.Fork();
        if (complete)
        {
            expectedRun = ReplayTouchMirror(
                Origin, BuildIndexDecisions(
                    Origin, Stochastic.DecisionTranscript));
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNature);
        }

        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState,
                Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, complete) ||
            CanonicalJson.Sha256(expectedNature) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Reflections Nature replay differs from its session envelope.");
    }

    private static RandomIndexDecisionV111[] BuildIndexDecisions(
        RunState origin, ReferenceNatureDecisionV111[] transcript)
    {
        var priorIndices = new List<int>();
        var decisions = new RandomIndexDecisionV111[transcript.Length];
        for (var ordinal = 0; ordinal < transcript.Length; ordinal++)
        {
            var plan = NativeEventReflectionsChoicePlanBuilderV111
                .TryBuildNext(origin, null, ordinal,
                    priorIndices.ToArray(), "touch_mirror")
                ?? throw new InvalidDataException(
                    "Reflections replay prefix extends beyond its draw sequence.");
            var selectedIndex = transcript[ordinal].OutcomeIndex;
            if ((uint)selectedIndex >=
                (uint)plan.CandidateCardInstanceIds.Length)
                throw new InvalidDataException(
                    "Reflections replay outcome is outside its card support.");
            decisions[ordinal] = new RandomIndexDecisionV111(
                plan.Request, selectedIndex, "scripted-v111");
            priorIndices.Add(selectedIndex);
        }
        return decisions;
    }

    private static RunState ReplayTouchMirror(
        RunState origin, RandomIndexDecisionV111[] decisions)
    {
        RunState ReplayAction(RunState run) =>
            new PrototypeGameEngine().Step(
                run, GameAction.Create("event_choice",
                    new EventChoicePayload("touch_mirror"))).State;

        if (origin.Stochastic.NativeRunRng is not null)
            return ReferenceNatureNativeEventIndexReplayV111
                .ReplayControlledIndices(
                    origin, decisions, ReplayAction,
                    PrototypeNativeGloryEvents.ReflectionsNativeRngEventId,
                    "Glory Reflections");

        var typedDecisions = decisions.Select(decision =>
            ControlledStochasticDecisionV111.FromIndex(
                new RandomIndexDecisionV111(
                    decision.Request, decision.SelectedIndex,
                    "controlled-v111"))).ToArray();
        var requests = decisions.Select(decision =>
            ControlledStochasticRequestV111.FromIndex(
                decision.Request)).ToArray();
        var replay = ControlledChanceContinuationExecutorV111
            .CaptureStochastic(
                origin,
                ControlledStochasticContinuationStateV111.CurrentSchemaId,
                typedDecisions,
                ReplayAction,
                candidate => requests.Any(request =>
                    ControlledStochasticChanceReplayV111.RequestsMatch(
                        candidate, request)));
        if (replay.Suspended || replay.Run is not { } fallbackRun)
            throw new InvalidDataException(
                "Reflections fallback replay did not consume its selected request prefix.");
        var originalEvent = origin.World?.Event;
        var replayEvent = fallbackRun.World?.Event;
        if (originalEvent?.Stochastic.NativeRng is not null ||
            originalEvent?.Stochastic.ProviderMode is not null ||
            replayEvent?.Stochastic.NativeRng is not null ||
            replayEvent?.Stochastic.ProviderMode is not null ||
            GetLegacyEventCallCount(fallbackRun) !=
                GetLegacyEventCallCount(origin))
            throw new InvalidDataException(
                "Reflections fallback replay changed an event RNG cursor or provider marker.");
        return fallbackRun;
    }

    private static ulong GetLegacyEventCallCount(RunState run)
    {
        if (!run.Stochastic.PrototypeRng.TryGetStream("event", out var stream))
            throw new InvalidDataException(
                "Reflections fallback requires one legacy event stream.");
        return stream.CallCount ?? 0;
    }

    public static string Save(
        ReferenceNatureGloryReflectionsSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureGloryReflectionsSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                RunSnapshot.Save(session.Run),
                session.Stochastic.InitialNatureState,
                session.Stochastic.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone()));
    }

    public static ReferenceNatureGloryReflectionsSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureGloryReflectionsSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Glory Reflections snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Glory Reflections snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var session = Begin(origin, snapshot.InitialNatureState.Fork());
        for (var index = 0;
             index < snapshot.DecisionTranscript.Length; index++)
            session = session.ResolveNext();
        session.Validate();
        if (CanonicalJson.Sha256(session.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(session.Stochastic.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !session.Stochastic.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "Reflections session snapshot does not replay from its origin.");
        return session;
    }
}
