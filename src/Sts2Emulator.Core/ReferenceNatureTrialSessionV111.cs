using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed record TrialCaseChoicePlanV111(
    int Page,
    RandomIndexRequestV111 Request,
    string[] OutcomeIds,
    string ConditioningFingerprint);

internal static class TrialCaseChoicePlanBuilderV111
{
    private static readonly string[] OutcomeIds =
    [
        "trial_case:merchant",
        "trial_case:noble",
        "trial_case:nondescript"
    ];

    public static TrialCaseChoicePlanV111 Build(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var world = origin.World;
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            world is null || world.Act != 3 || world.Event is not { } evt ||
            evt.EventId != PrototypeNativeGloryEvents.TrialId ||
            evt.NativePageIndex is not (0 or 1) ||
            evt.ChosenChoiceId is not null ||
            evt.ControlledChoice is not null ||
            evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Trial Nature choices require a pending configured case draw.");

        var legalActionId = PrototypeAiEnvironment.StableActionId(
            GameAction.Create("event_choice",
                new EventChoicePayload("accept")));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(action =>
                PrototypeAiEnvironment.StableActionId(action) == legalActionId))
            throw new InvalidOperationException(
                "Trial acceptance is not legal on the current page.");

        const string ownerId = "event:glory_trial:trial_case";
        var request = new RandomIndexRequestV111(
            "event.glory_trial.damage_amount.index_draw",
            NativeRngV111Contexts.EventStreamId(
                PrototypeNativeGloryEvents.TrialNativeRngEventId),
            ownerId, 0, [],
            OperationKind: "bounded_integer", BoundedCount: 3);
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "glory.trial.case_choice",
            eventId = evt.EventId,
            page = evt.NativePageIndex,
            outcomeIds = OutcomeIds,
            request,
            policy = ChanceLawEngineV111.TrialConditioningPolicyId
        });
        return new(evt.NativePageIndex, request,
            (string[])OutcomeIds.Clone(), conditioningFingerprint);
    }

    public static void ValidateNativeEventEntryCursor(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var evt = origin.World?.Event;
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || evt is null ||
            evt.EventId != PrototypeNativeGloryEvents.TrialId)
            throw new InvalidOperationException(
                "Trial session replay requires its configured Native event context.");

        var expectedStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeGloryEvents.TrialNativeRngEventId);
        if (evt.NativeEventRng is { } savedStream &&
            savedStream.StreamId != expectedStream.StreamId)
            throw new InvalidOperationException(
                "Trial has the wrong Native event stream context.");
        if (evt.NativeEventRng is null && evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Trial cannot reconstruct a missing event stream after page progress.");
        var stream = evt.NativeEventRng ?? expectedStream;
        if (NativeRngV111.ToSnapshot(stream) !=
            NativeRngV111.ToSnapshot(expectedStream))
            throw new InvalidOperationException(
                "Trial session must start at the event-entry Native cursor.");
    }
}

public sealed record ReferenceNatureTrialSessionSnapshotV111(
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

/// <summary>Atomic ReferenceNature choice for the Glory Trial case draw.</summary>
public sealed record ReferenceNatureTrialSessionV111(
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
        "sts2-v111-reference-nature-trial-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length == 1;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryTrial(Origin);

    public static ReferenceNatureTrialSessionV111 Begin(
        RunState origin, ulong natureSeed) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Trial"));

    public static ReferenceNatureTrialSessionV111 Begin(RunState origin) =>
        Begin(origin, ReferenceNatureRunCursorV111.Begin(
            origin, null, "Trial"));

    private static ReferenceNatureTrialSessionV111 Begin(
        RunState origin, ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        _ = TrialCaseChoicePlanBuilderV111.Build(origin);
        TrialCaseChoicePlanBuilderV111
            .ValidateNativeEventEntryCursor(origin);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Trial");
        var session = new ReferenceNatureTrialSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureTrialSessionV111 Fork()
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

    public ReferenceNatureTrialSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Trial session is already complete.");
        var plan = TrialCaseChoicePlanBuilderV111.Build(Origin);
        var law = ChanceLawEngineV111.QueryTrial(Origin);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = Replay(Origin, plan, law, selection);
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

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Trial session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        TrialCaseChoicePlanBuilderV111
            .ValidateNativeEventEntryCursor(Origin);
        InitialNatureState.Validate();
        NatureState.Validate();
        var plan = TrialCaseChoicePlanBuilderV111.Build(Origin);
        if (DecisionTranscript.Length > 1 ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Trial cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        RunState expectedRun;
        if (Complete)
        {
            var law = ChanceLawEngineV111.QueryTrial(Origin);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[0]))
                throw new InvalidDataException(
                    "ReferenceNature Trial transcript does not replay.");
            expectedNatureState = selection.NextState;
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                Replay(Origin, plan, law, selection),
                expectedNatureState);
        }
        else
        {
            expectedRun = Origin.Fork();
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Trial replay differs from its session envelope.");
    }

    public static string Save(ReferenceNatureTrialSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureTrialSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            (ReferenceNatureDecisionV111[])
                session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureTrialSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureTrialSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Trial snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Trial snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.InitialNatureState.Fork());
        for (int index = 0;
             index < snapshot.DecisionTranscript.Length; index++)
            replay = replay.ResolveNext();
        replay.Validate();
        if (CanonicalJson.Sha256(replay.Run) !=
                CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !replay.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "ReferenceNature Trial snapshot does not replay from its origin.");
        return replay;
    }

    private static RunState Replay(
        RunState origin, TrialCaseChoicePlanV111 plan,
        ChanceLawV111 law, ReferenceNatureSelectionV111 selection)
    {
        var request = ControlledStochasticRequestV111.FromIndex(plan.Request);
        var selected = ReferenceNatureChanceLawDecisionV111
            .FromOrderedIndexSelection(
                law, request, selection, plan.OutcomeIds);
        return ReferenceNatureNativeEventIndexReplayV111
            .ReplayControlledIndex(
                origin, plan.Request, selected,
                run => new PrototypeGameEngine().Step(
                    run, GameAction.Create("event_choice",
                        new EventChoicePayload("accept"))).State,
                PrototypeNativeGloryEvents.TrialNativeRngEventId,
                "Trial");
    }
}

public static partial class ChanceLawEngineV111
{
    public const string TrialConditioningPolicyId =
        "glory-trial-case-choice-v1";

    public static string[] TrialSessionSiteIds =>
        ["event.glory_trial.damage_amount.index_draw"];

    public static ChanceLawV111 QueryTrial(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = TrialCaseChoicePlanBuilderV111.Build(origin);
        var indexWeights = NativeNextIntGridWeights(
            plan.Request.BoundedCount!.Value);
        var outcomes = plan.OutcomeIds.Select((outcomeId, index) =>
            new ChanceLawOutcomeV111(outcomeId, indexWeights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "glory.trial.case_choice",
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, TrialConditioningPolicyId,
            plan.ConditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }
}
