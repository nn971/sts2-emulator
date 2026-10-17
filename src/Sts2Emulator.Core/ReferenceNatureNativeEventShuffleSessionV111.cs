using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed record NativeEventShufflePlanV111(
    string EventId,
    string NativeRngEventId,
    string SiteId,
    string ActionId,
    long[] CandidateCardInstanceIds,
    string StreamId,
    string OwnerId,
    string ConditioningFingerprint)
{
    public int SwapCount => CandidateCardInstanceIds.Length - 1;

    public RandomIndexRequestV111 Request(int ordinal)
    {
        if (ordinal < 0 || ordinal >= SwapCount)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        return new RandomIndexRequestV111(
            SiteId, StreamId, OwnerId, ordinal, [],
            OperationKind: "bounded_integer",
            BoundedCount: CandidateCardInstanceIds.Length - ordinal);
    }
}

internal static class NativeEventShufflePlanBuilderV111
{
    public static NativeEventShufflePlanV111 Build(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || origin.Phase != RunPhase.Event ||
            origin.World?.Event is not { } evt ||
            evt.EventId != PrototypeNativeUnderdocksDoors.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.ControlledChoice is not null ||
            evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Native event shuffle Nature choices require a pending Doors of Light and Dark Light action.");

        var actionId = PrototypeAiEnvironment.StableActionId(
            GameAction.Create("event_choice",
                new EventChoicePayload("light")));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(action =>
                PrototypeAiEnvironment.StableActionId(action) == actionId))
            throw new InvalidOperationException(
                "Doors of Light and Dark Light is not legal in the current state.");

        var candidates = origin.Player.Deck
            .Where(card => PrototypeGameEngine.CanSelectEventDeckCard(
                card, PrototypePersistentDeckChoiceKind.Upgrade,
                null, null, false))
            .Select(card => card.InstanceId)
            .ToArray();
        if (candidates.Length <= 1)
            throw new InvalidOperationException(
                "Doors of Light and Dark has fewer than two eligible cards and performs no shuffle draw.");

        var expectedStream = NativeRngV111Contexts.ForEvent(
            origin.RunSeed, PrototypeNativeUnderdocksDoors.NativeRngEventId);
        if (evt.NativeEventRng is { } savedStream &&
            savedStream.StreamId != expectedStream.StreamId)
            throw new InvalidOperationException(
                "Doors of Light and Dark has the wrong Native event stream context.");
        if (evt.NativeEventRng is null && evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Doors of Light and Dark cannot reconstruct a missing event stream after page progress.");
        var stream = evt.NativeEventRng ?? expectedStream;
        if (NativeRngV111.ToSnapshot(stream) !=
            NativeRngV111.ToSnapshot(expectedStream))
            throw new InvalidOperationException(
                "Doors of Light and Dark shuffle is not at the initial event cursor.");

        var ownerId = "event:doors_light:eligible_cards:" +
            CanonicalJson.Sha256(candidates);
        var fingerprint = CanonicalJson.Sha256(new
        {
            boundary = "underdocks.doors_of_light.shuffle",
            eventId = evt.EventId,
            actionId = "light",
            candidateCardInstanceIds = candidates,
            stream = NativeRngV111.ToSnapshot(stream),
            ownerId,
            policy = ChanceLawEngineV111.NativeEventShuffleConditioningPolicyId
        });
        return new(evt.EventId,
            PrototypeNativeUnderdocksDoors.NativeRngEventId,
            PrototypeNativeUnderdocksDoors.ShuffleSiteId,
            "light", candidates, stream.StreamId, ownerId, fingerprint);
    }
}

public sealed record ReferenceNatureNativeEventShuffleSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>Atomic ReferenceNature replay of a complete native event-local
/// Fisher-Yates shuffle. The first supported resolver is Doors of Light.</summary>
public sealed record ReferenceNatureNativeEventShuffleSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-native-event-shuffle-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length ==
        NativeEventShufflePlanBuilderV111.Build(Origin).SwapCount;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNativeEventShuffle(
            Origin, NextOrdinal,
            DecisionTranscript.Select(decision => decision.OutcomeIndex)
                .ToArray());

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, ulong natureSeed) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Native event shuffle"));

    public static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Native event shuffle"));

    private static ReferenceNatureNativeEventShuffleSessionV111 Begin(
        RunState origin, ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        _ = NativeEventShufflePlanBuilderV111.Build(origin);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Native event shuffle");
        var session = new ReferenceNatureNativeEventShuffleSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureNativeEventShuffleSessionV111 Fork()
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

    public ReferenceNatureNativeEventShuffleSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature native event shuffle session is already complete.");
        var plan = NativeEventShufflePlanBuilderV111.Build(Origin);
        var priorIndices = DecisionTranscript.Select(
            decision => decision.OutcomeIndex).ToArray();
        var law = ChanceLawEngineV111.QueryNativeEventShuffle(
            Origin, NextOrdinal, priorIndices);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNature = selection.NextState;
        var nextRun = Run;
        if (transcript.Length == plan.SwapCount)
        {
            var decisions = transcript.Select((decision, ordinal) =>
                new RandomIndexDecisionV111(
                    plan.Request(ordinal), decision.OutcomeIndex,
                    "scripted-v111")).ToArray();
            var replay = ReferenceNatureNativeEventIndexReplayV111.Replay(
                Origin, decisions,
                GameAction.Create("event_choice",
                    new EventChoicePayload(plan.ActionId)),
                plan.NativeRngEventId, "Doors of Light and Dark");
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
                "Invalid ReferenceNature native event shuffle session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        var plan = NativeEventShufflePlanBuilderV111.Build(Origin);
        if (DecisionTranscript.Length > plan.SwapCount ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var ordinal = 0; ordinal < DecisionTranscript.Length; ordinal++)
        {
            var law = ChanceLawEngineV111.QueryNativeEventShuffle(
                Origin, ordinal, priorIndices.ToArray());
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            DecisionTranscript[ordinal].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[ordinal]))
                throw new InvalidDataException(
                    "ReferenceNature native event shuffle transcript does not replay.");
            expectedNatureState = selection.NextState;
            priorIndices.Add(selection.OutcomeIndex);
        }

        var expectedRun = Origin.Fork();
        if (Complete)
        {
            var decisions = DecisionTranscript.Select((decision, ordinal) =>
                new RandomIndexDecisionV111(
                    plan.Request(ordinal), decision.OutcomeIndex,
                    "scripted-v111")).ToArray();
            expectedRun = ReferenceNatureNativeEventIndexReplayV111.Replay(
                Origin, decisions,
                GameAction.Create("event_choice",
                    new EventChoicePayload(plan.ActionId)),
                plan.NativeRngEventId, "Doors of Light and Dark");
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNatureState);
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature native event shuffle replay differs from its session envelope.");
    }

    public static string Save(
        ReferenceNatureNativeEventShuffleSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new
            ReferenceNatureNativeEventShuffleSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                RunSnapshot.Save(session.Run), session.InitialNatureState,
                session.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureNativeEventShuffleSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNativeEventShuffleSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature native event shuffle snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature native event shuffle snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.InitialNatureState.Fork());
        for (var index = 0;
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
                "ReferenceNature native event shuffle snapshot does not replay from its origin.");
        return replay;
    }
}

public static partial class ChanceLawEngineV111
{
    public const string NativeEventShuffleConditioningPolicyId =
        "native-event-fisher-yates-prefix-v1";

    public static string[] NativeEventShuffleSessionSiteIds =>
        [PrototypeNativeUnderdocksDoors.ShuffleSiteId];

    public static ChanceLawV111 QueryNativeEventShuffle(
        RunState origin, int ordinal, int[]? priorIndices = null)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = NativeEventShufflePlanBuilderV111.Build(origin);
        priorIndices ??= [];
        if (ordinal < 0 || ordinal >= plan.SwapCount ||
            priorIndices.Length != ordinal)
            throw new InvalidOperationException(
                "Native event shuffle law requires the complete preceding swap-index prefix.");
        for (var index = 0; index < priorIndices.Length; index++)
            if (priorIndices[index] < 0 ||
                priorIndices[index] >= plan.CandidateCardInstanceIds.Length - index)
                throw new InvalidOperationException(
                    "Native event shuffle prefix contains an unsupported bounded index.");

        var request = plan.Request(ordinal);
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            plan.ConditioningFingerprint,
            priorIndices,
            request
        });
        var outcomes = Enumerable.Range(0, request.BoundedCount!.Value)
            .Select(index => new ChanceLawOutcomeV111(
                "swap_index:" + index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), 1L))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "underdocks.doors_of_light.shuffle_swap",
            conditioningFingerprint
        });
        return Create(eventId, request.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            NativeEventShuffleConditioningPolicyId,
            conditioningFingerprint,
            "uniform-bounded-event-shuffle-index-unverified", outcomes);
    }
}
