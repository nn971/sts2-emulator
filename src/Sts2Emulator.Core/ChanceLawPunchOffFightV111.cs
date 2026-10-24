using System.Text.Json;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public static string[] NativePunchOffFightQuerySiteIds =>
        StochasticSiteRegistryV111.NativePunchOffFightSiteIds;

    public static ChanceLawV111 QueryNativePunchOffFight(
        RunState origin, string siteId, int ordinal, int[]? priorIndices,
        string actionId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!NativePunchOffFightQuerySiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Unsupported Punch Off fight site '{siteId}'.");
        priorIndices ??= [];
        var plan = NativePunchOffFightPlanBuilderV111.Build(
            origin, actionId, ordinal, priorIndices);
        var request = plan.Requests[ordinal];
        var weights = NativeNextIntGridWeights(8);
        var outcomes = Enumerable.Range(0, 8).Select(index =>
            new ChanceLawOutcomeV111(
                "hp_offset:" + (index + 2).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                weights[index]))
            .ToArray();
        var conditioning = CanonicalJson.Sha256(new
        {
            plan.ConditioningFingerprint,
            ordinal,
            priorIndices,
            request
        });
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.punch_off_construct_hp_offset",
            conditioning
        });
        return Create(eventId, siteId, request.OperationKind,
            request.OwnerId, ordinal,
            "native-punch-off-construct-hp-prefix-v1", conditioning,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }
}

public sealed record ReferenceNaturePunchOffFightSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, string ActionId,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
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

/// <summary>Atomic Nature sampling for the two Punch Off construct HP offsets.</summary>
public sealed record ReferenceNaturePunchOffFightSessionV111(
    string SchemaId, string ActionId, RunState Origin, RunState Run,
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

    [System.Text.Json.Serialization.JsonIgnore]
    public GameAction Action => new PrototypeGameEngine()
        .GetLegalActions(Origin)
        .SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == ActionId)
        ?? throw new InvalidOperationException(
            "Punch Off fight action is no longer legal at its saved origin.");

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "A pending Punch Off fight session has no completed provider prefix.");
            var plan = NativePunchOffFightPlanBuilderV111.Build(
                Origin, ActionId, 0, []);
            var natureState = Stochastic.InitialNatureState.Fork();
            var priorIndices = new List<int>();
            var decisions = new ControlledStochasticDecisionV111[2];
            for (var ordinal = 0; ordinal < decisions.Length; ordinal++)
            {
                var prior = priorIndices.ToArray();
                var law = ChanceLawEngineV111.QueryNativePunchOffFight(
                    Origin, PrototypeNativePunchOff.ConstructHpOffsetSiteId,
                    ordinal, prior, ActionId);
                var selection = ReferenceNatureV111.SampleValidated(
                    law, natureState);
                var expected = new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex);
                if (CanonicalJson.Sha256(expected) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[ordinal]))
                    throw new InvalidDataException(
                        "Punch Off fight provider prefix differs from its Nature transcript.");
                var labels = law.Outcomes.Select(outcome =>
                    outcome.OutcomeId).ToArray();
                var mapped = ReferenceNatureChanceLawDecisionV111
                    .FromOrderedIndexSelection(law,
                        ControlledStochasticRequestV111.FromIndex(
                            plan.Requests[ordinal]), selection, labels);
                var indexDecision = mapped.IndexDecision ??
                    throw new InvalidDataException(
                        "Punch Off fight provider replay requires an index decision.");
                decisions[ordinal] =
                    ControlledStochasticDecisionV111.FromIndex(
                        indexDecision, labels);
                priorIndices.Add(selection.OutcomeIndex);
                natureState = selection.NextState;
            }
            return decisions;
        }
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-punch-off-fight-session-v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Complete => Stochastic.DecisionTranscript.Length == 2;

    [System.Text.Json.Serialization.JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNativePunchOffFight(
            Origin, PrototypeNativePunchOff.ConstructHpOffsetSiteId,
            Stochastic.DecisionTranscript.Length,
            Stochastic.DecisionTranscript.Select(
                decision => decision.OutcomeIndex)
                .ToArray(), ActionId);

    public static string[] SessionSiteIds =>
        StochasticSiteRegistryV111.NativePunchOffFightSiteIds;

    public static ReferenceNaturePunchOffFightSessionV111 Begin(
        RunState origin, string actionId, ulong natureSeed) => Begin(
            origin, actionId, ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Punch Off fight"));

    public static ReferenceNaturePunchOffFightSessionV111 Begin(
        RunState origin, string actionId) => Begin(
            origin, actionId, ReferenceNatureRunCursorV111.Begin(
                origin, null, "Punch Off fight"));

    private static ReferenceNaturePunchOffFightSessionV111 Begin(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        _ = NativePunchOffFightPlanBuilderV111.Build(
            origin, actionId, 0, []);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Punch Off fight");
        var session = new ReferenceNaturePunchOffFightSessionV111(
            CurrentSchemaId, actionId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNaturePunchOffFightSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNaturePunchOffFightSessionV111 ResolveNext()
    {
        Validate();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "Punch Off Nature fight session is already complete.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, Stochastic.NatureState);
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextRun = transcript.Length == 2
            ? Replay(Origin, ActionId, transcript)
            : Run;
        var next = this with
        {
            Run = transcript.Length == 2
                ? ReferenceNatureRunCursorV111.Commit(
                    nextRun, selection.NextState)
                : nextRun,
            NatureState = selection.NextState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(ActionId) || Origin is null ||
            Run is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Punch Off fight session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        if (Stochastic.DecisionTranscript.Length > 2)
            throw new InvalidDataException(
                "Invalid ReferenceNature Punch Off fight session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        var expectedNature = Stochastic.InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var ordinal = 0; ordinal < Stochastic.DecisionTranscript.Length; ordinal++)
        {
            var law = ChanceLawEngineV111.QueryNativePunchOffFight(
                Origin, PrototypeNativePunchOff.ConstructHpOffsetSiteId,
                ordinal, priorIndices.ToArray(), ActionId);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNature);
            Stochastic.DecisionTranscript[ordinal].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(Stochastic.DecisionTranscript[ordinal]))
                throw new InvalidDataException(
                    "Punch Off fight transcript does not replay its conditioned prefix.");
            expectedNature = selection.NextState;
            priorIndices.Add(selection.OutcomeIndex);
        }
        var expectedRun = Complete
            ? ReferenceNatureRunCursorV111.Commit(
                Replay(Origin, ActionId, Stochastic.DecisionTranscript),
                expectedNature)
            : Origin.Fork();
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState,
                Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete) ||
            CanonicalJson.Sha256(expectedNature) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Punch Off fight replay differs from its session envelope.");
    }

    public static string Save(ReferenceNaturePunchOffFightSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNaturePunchOffFightSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                session.ActionId, RunSnapshot.Save(session.Run),
                session.Stochastic.InitialNatureState,
                session.Stochastic.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone()));
    }

    public static ReferenceNaturePunchOffFightSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNaturePunchOffFightSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Punch Off fight snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.ActionId) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null ||
            snapshot.DecisionTranscript.Length > 2)
            throw new InvalidDataException(
                "Invalid ReferenceNature Punch Off fight snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var session = Begin(origin, snapshot.ActionId,
            snapshot.InitialNatureState.Fork());
        foreach (var _ in snapshot.DecisionTranscript)
            session = session.ResolveNext();
        session.Validate();
        if (CanonicalJson.Sha256(session.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(session.Stochastic.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !session.Stochastic.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "Punch Off fight snapshot does not replay from its origin.");
        return session;
    }

    private static RunState Replay(
        RunState origin, string actionId,
        ReferenceNatureDecisionV111[] transcript)
    {
        var plan = NativePunchOffFightPlanBuilderV111.Build(
            origin, actionId, 0, []);
        var decisions = plan.Requests.Select((request, index) =>
            new RandomIndexDecisionV111(
                request, transcript[index].OutcomeIndex,
                "scripted-v111")).ToArray();
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(origin).SingleOrDefault(candidate =>
            PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidDataException(
                "Punch Off Fight action is no longer legal at replay origin.");
        using var journal = RandomBoundaryJournalV111.Begin();
        var replay = NativePunchOffControlledIndexReplayV111.Capture(
            origin,
            decisions,
            checkpoint => engine.Step(checkpoint.Fork(), action).State,
            [PrototypeNativePunchOff.ConstructHpOffsetSiteId]);
        if (replay.Suspended || replay.Run is not { } run)
            throw new InvalidDataException(
                "Punch Off fight action did not consume its complete selected HP-offset prefix.");
        var entries = journal.Entries.Where(entry =>
            entry.Request.SiteId ==
                PrototypeNativePunchOff.ConstructHpOffsetSiteId).ToArray();
        var combat = run.World?.Combat;
        var eventCursor = run.World?.Event?.Stochastic.NativeRng is { } eventRng
            ? NativeRngV111.ToSnapshot(eventRng) : null;
        var relicOffsets = transcript.Select(decision =>
            int.Parse(decision.OutcomeId["hp_offset:".Length..],
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var expectedCursors = plan.RunCursorsAfterProbe;
        var actualCursors = combat?.Stochastic.NativeRng?.RunStreams.Streams
            .OrderBy(stream => stream.StreamId, StringComparer.Ordinal)
            .Select(NativeRngV111.ToSnapshot).ToArray();
        var expectedMonsterCursors = plan.MonsterCursorsBefore;
        if (run.Phase != RunPhase.Combat ||
            combat is not
                { Enemies.Length: 2, Stochastic.NativeRng: not null } ||
            run.World?.Event is not
                { EventId: PrototypeNativePunchOff.EventId, NativePageIndex: 2 } ||
            !combat.Enemies.Select(enemy => enemy.Hp).SequenceEqual(
                plan.ConstructHpBeforeOffsets.Select((hp, index) =>
                    hp - relicOffsets[index])) ||
            !combat.Enemies.Select(enemy => enemy.MoveIndex)
                .SequenceEqual([1, 0]) ||
            entries.Length != 2 || entries.Any(entry =>
                entry.ProviderMode != "controlled-v111") ||
            entries.Where((entry, index) =>
                !ControlledStochasticChanceReplayV111.RequestsMatch(
                    entry.Request, plan.Requests[index]) ||
                entry.SelectedIndex !=
                    transcript[index].OutcomeIndex).Any() ||
            !expectedCursors.SequenceEqual(actualCursors ?? []) ||
            !expectedMonsterCursors.SequenceEqual(
                combat.Stochastic.NativeRng.Formation.MonsterContexts
                    .Select(context => NativeRngV111.ToSnapshot(context.Rng))) ||
            eventCursor != plan.EventCursorBefore)
            throw new InvalidDataException(
                "Punch Off Nature fight replay changed construct HP, cursor ownership, or event state.");
        return run;
    }

}
