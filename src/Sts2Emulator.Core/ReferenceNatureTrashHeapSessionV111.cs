using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed record TrashHeapChoicePlanV111(
    RandomIndexRequestV111 Request,
    string[] OutcomeIds,
    string ConditioningFingerprint);

internal static class TrashHeapChoicePlanBuilderV111
{
    public static TrashHeapChoicePlanV111 Build(
        RunState origin, string siteId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (origin.NativeRunRng is null ||
            origin.Phase != RunPhase.Event ||
            origin.World?.Map.GenerationProfileId !=
                PrototypeNativeUnderdocks.GenerationProfileId ||
            origin.World?.Event is not { } evt ||
            evt.EventId != PrototypeNativeUnderdocksTrashHeap.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.ControlledChoice is not null ||
            evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not null ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Trash Heap Nature choices require an unchosen configured Native event.");

        string choiceId;
        string ownerId;
        string[] outcomeIds;
        if (siteId == PrototypeNativeUnderdocksTrashHeap.CardRewardSiteId)
        {
            choiceId = "grab";
            ownerId = "event:trash_heap:cards";
            outcomeIds = PrototypeNativeUnderdocksTrashHeap.CardIds;
        }
        else if (siteId == PrototypeNativeUnderdocksTrashHeap.RelicRewardSiteId)
        {
            choiceId = "dive";
            ownerId = "event:trash_heap:relics";
            outcomeIds = PrototypeNativeUnderdocksTrashHeap.RelicIds;
            if (origin.Player.Hp <= 8)
                throw new InvalidOperationException(
                    "Trash Heap Dive is lethal and has no relic draw at this HP.");
        }
        else
        {
            throw new NotSupportedException(
                $"Trash Heap Chance Law does not support site '{siteId}'.");
        }

        var legalActionId = PrototypeAiEnvironment.StableActionId(
            GameAction.Create("event_choice", new EventChoicePayload(choiceId)));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(action =>
                PrototypeAiEnvironment.StableActionId(action) == legalActionId))
            throw new InvalidOperationException(
                $"Trash Heap choice '{choiceId}' is not legal in the current state.");

        var request = new RandomIndexRequestV111(
            siteId,
            NativeRngV111Contexts.EventStreamId(
                PrototypeNativeUnderdocksTrashHeap.NativeRngEventId),
            ownerId, 0,
            Enumerable.Range(0, outcomeIds.Length).ToArray());
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "underdocks.trash_heap.choice",
            eventId = evt.EventId,
            choiceId,
            playerHp = origin.Player.Hp,
            playerMaxHp = origin.Player.MaxHp,
            nativePageIndex = evt.NativePageIndex,
            request,
            outcomeIds,
            policy = ChanceLawEngineV111.TrashHeapConditioningPolicyId
        });
        return new(request, (string[])outcomeIds.Clone(),
            conditioningFingerprint);
    }

    public static void ValidateNativeEventEntryCursor(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var evt = origin.World?.Event;
        if (string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null || evt is null ||
            evt.EventId != PrototypeNativeUnderdocksTrashHeap.EventId)
            throw new InvalidOperationException(
                "Trash Heap session replay requires its configured Native event context.");
        var expected = NativeRngV111Contexts.ForEvent(
            origin.RunSeed,
            PrototypeNativeUnderdocksTrashHeap.NativeRngEventId);
        if (evt.NativeEventRng is { } saved &&
            saved.StreamId != expected.StreamId)
            throw new InvalidOperationException(
                "Trash Heap has the wrong Native event stream context.");
        if (evt.NativeEventRng is null && evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Trash Heap cannot reconstruct a missing event stream after page progress.");
        if (NativeRngV111.ToSnapshot(evt.NativeEventRng ?? expected) !=
            NativeRngV111.ToSnapshot(expected))
            throw new InvalidOperationException(
                "Trash Heap session must start at the event-entry Native cursor.");
    }
}

public sealed record ReferenceNatureTrashHeapSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    string ChoiceId,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>Atomic one-choice ReferenceNature continuation for the two
/// source-modeled Trash Heap fixed-pool rewards.</summary>
public sealed record ReferenceNatureTrashHeapSessionV111(
    string SchemaId,
    RunState Origin,
    string ChoiceId,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-trash-heap-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length == 1;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryTrashHeap(Origin,
            SiteIdForChoice(ChoiceId));

    public static ReferenceNatureTrashHeapSessionV111 Begin(
        RunState origin, string choiceId, ulong natureSeed)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        var initial = ReferenceNatureRunCursorV111.Begin(
            origin, natureSeed, "Trash Heap");
        return Begin(origin, choiceId, initial);
    }

    private static ReferenceNatureTrashHeapSessionV111 Begin(
        RunState origin, string choiceId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        ArgumentNullException.ThrowIfNull(initial);
        _ = TrashHeapChoicePlanBuilderV111.Build(
            origin, SiteIdForChoice(choiceId));
        TrashHeapChoicePlanBuilderV111.ValidateNativeEventEntryCursor(origin);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Trash Heap");
        var session = new ReferenceNatureTrashHeapSessionV111(
            CurrentSchemaId, origin.Fork(), choiceId, origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureTrashHeapSessionV111 Fork()
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

    public ReferenceNatureTrashHeapSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Trash Heap session is already complete.");
        var plan = TrashHeapChoicePlanBuilderV111.Build(
            Origin, SiteIdForChoice(ChoiceId));
        var law = ChanceLawEngineV111.QueryTrashHeap(
            Origin, plan.Request.SiteId);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = Replay(Origin, ChoiceId, plan,
            selection.OutcomeIndex);
        if (CanonicalJson.Sha256(replay.World?.Event?.NativeEventRng) !=
            CanonicalJson.Sha256(NativeRngV111Contexts.ForEvent(
                Origin.RunSeed,
                PrototypeNativeUnderdocksTrashHeap.NativeRngEventId)))
            throw new InvalidDataException(
                "Scripted Trash Heap resolution advanced its Native event cursor.");
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
            string.IsNullOrWhiteSpace(ChoiceId) ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Trash Heap session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        TrashHeapChoicePlanBuilderV111
            .ValidateNativeEventEntryCursor(Origin);
        InitialNatureState.Validate();
        NatureState.Validate();
        var plan = TrashHeapChoicePlanBuilderV111.Build(
            Origin, SiteIdForChoice(ChoiceId));
        if (DecisionTranscript.Length > 1 ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Trash Heap cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        RunState expectedRun;
        if (Complete)
        {
            var law = ChanceLawEngineV111.QueryTrashHeap(
                Origin, plan.Request.SiteId);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[0]))
                throw new InvalidDataException(
                    "ReferenceNature Trash Heap transcript does not replay.");
            expectedNatureState = selection.NextState;
            expectedRun = Replay(Origin, ChoiceId, plan,
                selection.OutcomeIndex);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNatureState);
        }
        else
        {
            expectedRun = Origin.Fork();
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Trash Heap replay differs from its session envelope.");
    }

    public static string Save(ReferenceNatureTrashHeapSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureTrashHeapSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.ChoiceId,
            session.InitialNatureState, session.NatureState,
            (ReferenceNatureDecisionV111[])
                session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureTrashHeapSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureTrashHeapSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Trash Heap snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.ChoiceId) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Trash Heap snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var initial = snapshot.InitialNatureState.Fork();
        var replay = Begin(origin, snapshot.ChoiceId, initial);
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
                "ReferenceNature Trash Heap snapshot does not replay from its origin.");
        return replay;
    }

    private static RunState Replay(
        RunState origin, string choiceId,
        TrashHeapChoicePlanV111 plan, int selectedIndex)
    {
        var scripted = new ScriptedRandomIndexProviderV111(
        [
            new RandomIndexDecisionV111(
                plan.Request, selectedIndex, "scripted-v111")
        ]);
        var action = GameAction.Create(
            "event_choice", new EventChoicePayload(choiceId));
        using var providerScope = StochasticProviderScopeV111.Enter(
            scripted, overrideExplicitProviders: true);
        var run = new PrototypeGameEngine().Step(origin.Fork(), action).State;
        scripted.AssertComplete();
        var world = run.World ?? throw new InvalidDataException(
            "Trash Heap scripted resolution lost its event world.");
        var eventState = world.Event ?? throw new InvalidDataException(
            "Trash Heap scripted resolution lost its completed event page.");
        return run with
        {
            World = world with
            {
                Event = eventState with
                {
                    NativeEventRngProviderMode = "scripted-v111"
                }
            }
        };
    }

    private static string SiteIdForChoice(string choiceId) => choiceId switch
    {
        "grab" => PrototypeNativeUnderdocksTrashHeap.CardRewardSiteId,
        "dive" => PrototypeNativeUnderdocksTrashHeap.RelicRewardSiteId,
        _ => throw new NotSupportedException(
            $"Trash Heap ReferenceNature does not support choice '{choiceId}'.")
    };
}

public static partial class ChanceLawEngineV111
{
    public const string TrashHeapConditioningPolicyId =
        "trash-heap-single-choice-source-pool-v1";

    public static string[] TrashHeapSessionSiteIds =>
    [
        PrototypeNativeUnderdocksTrashHeap.CardRewardSiteId,
        PrototypeNativeUnderdocksTrashHeap.RelicRewardSiteId
    ];

    public static ChanceLawV111 QueryTrashHeap(
        RunState origin, string siteId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = TrashHeapChoicePlanBuilderV111.Build(origin, siteId);
        var indexWeights = NativeNextIntGridWeights(
            plan.Request.SupportCount);
        var outcomes = plan.OutcomeIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"trash_heap_reward:{id}:index:{index}",
                indexWeights[index])).ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "underdocks.trash_heap.choice",
            siteId,
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, TrashHeapConditioningPolicyId,
            plan.ConditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }
}
