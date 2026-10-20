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
        "sts2-v111-reference-nature-glory-reflections-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public string? LastSiteId => DecisionTranscript.Length == 0 ? null :
        NativeEventReflectionsChoicePlanBuilderV111.TryBuildNext(
            Origin, null, DecisionTranscript.Length - 1,
            DecisionTranscript.Take(DecisionTranscript.Length - 1)
                .Select(decision => decision.OutcomeIndex).ToArray(),
            "touch_mirror")?.Request.SiteId;

    [JsonIgnore]
    public bool Complete =>
        NativeEventReflectionsChoicePlanBuilderV111.TryBuildNext(
            Origin, null, DecisionTranscript.Length,
            DecisionTranscript.Select(decision => decision.OutcomeIndex)
                .ToArray(), "touch_mirror") is null;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var priorIndices = DecisionTranscript.Select(
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
        [.. ChanceLawEngineV111.NativeEventCardPickQuerySiteIds];

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
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureGloryReflectionsSessionV111 ResolveNext()
    {
        Validate();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "Reflections ReferenceNature session is already complete.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var transcript = DecisionTranscript.Append(
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
            var decisions = BuildNativeDecisions(Origin, transcript);
            var replay = ReferenceNatureNativeEventIndexReplayV111
                .ReplayControlledIndices(
                    Origin, decisions,
                    run => new PrototypeGameEngine().Step(
                        run, GameAction.Create("event_choice",
                            new EventChoicePayload("touch_mirror"))).State,
                    PrototypeNativeGloryEvents.ReflectionsNativeRngEventId,
                    "Glory Reflections");
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
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Glory Reflections session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();

        var expectedNature = InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
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
            DecisionTranscript[ordinal].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[ordinal]))
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
            expectedRun = ReferenceNatureNativeEventIndexReplayV111
                .ReplayControlledIndices(
                    Origin,
                    BuildNativeDecisions(Origin, DecisionTranscript),
                    run => new PrototypeGameEngine().Step(
                        run, GameAction.Create("event_choice",
                            new EventChoicePayload("touch_mirror"))).State,
                    PrototypeNativeGloryEvents.ReflectionsNativeRngEventId,
                    "Glory Reflections");
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNature);
        }

        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, complete) ||
            CanonicalJson.Sha256(expectedNature) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Reflections Nature replay differs from its session envelope.");
    }

    private static RandomIndexDecisionV111[] BuildNativeDecisions(
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

    public static string Save(
        ReferenceNatureGloryReflectionsSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureGloryReflectionsSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                RunSnapshot.Save(session.Run), session.InitialNatureState,
                session.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.DecisionTranscript.Clone()));
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
            CanonicalJson.Sha256(session.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !session.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "Reflections session snapshot does not replay from its origin.");
        return session;
    }
}
