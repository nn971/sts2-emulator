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
        var world = origin.World;
        if (origin.Phase != RunPhase.Event || world is null ||
            world.Map.GenerationProfileId !=
                PrototypeNativeUnderdocks.GenerationProfileId ||
            world.Event is not { } evt ||
            evt.EventId != PrototypeNativeUnderdocksTrashHeap.EventId ||
            evt.NativePageIndex != 0 || evt.ChosenChoiceId is not null ||
            evt.Stochastic.ControlledChoice is not null ||
            evt.Stochastic.RandomOutcomes.NativeEventPotionSlot is not null ||
            evt.Stochastic.ProviderMode is not null ||
            origin.Stochastic.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Trash Heap Nature choices require an unchosen supported Native-regional event.");

        var nativeRoute = origin.Stochastic.NativeRunRng is not null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world);
        var legacyFallbackRoute = origin.Stochastic.NativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.Stochastic.NativeRng is null;
        if (!nativeRoute && !legacyFallbackRoute)
            throw new InvalidOperationException(
                "Trash Heap Nature choices require a configured Native or Native-regional legacy event RNG route.");

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

        var streamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeUnderdocksTrashHeap.NativeRngEventId)
            : "event";
        var ordinal = nativeRoute ? 0 : GetLegacyEventOrdinal(origin);
        var request = new RandomIndexRequestV111(
            siteId,
            streamId,
            ownerId, ordinal,
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

    private static int GetLegacyEventOrdinal(RunState origin)
    {
        if (!origin.Stochastic.PrototypeRng.TryGetStream("event", out var stream))
            throw new InvalidDataException(
                "Trash Heap fallback requires one legacy event stream.");
        var ordinal = stream.CallCount ?? 0;
        if (ordinal > int.MaxValue)
            throw new NotSupportedException(
                "Trash Heap legacy event ordinal exceeds the typed request range.");
        return (int)ordinal;
    }

    public static void ValidateReplayOrigin(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var evt = origin.World?.Event;
        var world = origin.World;
        if (world is null || world.Map.GenerationProfileId !=
                PrototypeNativeUnderdocks.GenerationProfileId ||
            evt is null ||
            evt.EventId != PrototypeNativeUnderdocksTrashHeap.EventId)
            throw new InvalidOperationException(
                "Trash Heap session replay requires its Native-regional event context.");
        if (origin.Stochastic.NativeRunRng is null)
        {
            if (!PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
                evt.Stochastic.NativeRng is not null ||
                evt.Stochastic.ProviderMode is not null)
                throw new InvalidOperationException(
                    "Trash Heap fallback replay requires the legacy event stream without a Native event cursor.");
            return;
        }
        if (string.IsNullOrWhiteSpace(origin.RunSeed))
            throw new InvalidOperationException(
                "Trash Heap Native replay requires its run seed.");
        var expected = NativeRngV111Contexts.ForEvent(
            origin.RunSeed,
            PrototypeNativeUnderdocksTrashHeap.NativeRngEventId);
        if (evt.Stochastic.NativeRng is { } saved &&
            saved.StreamId != expected.StreamId)
            throw new InvalidOperationException(
                "Trash Heap has the wrong Native event stream context.");
        if (evt.Stochastic.NativeRng is null && evt.NativePageIndex != 0)
            throw new InvalidOperationException(
                "Trash Heap cannot reconstruct a missing event stream after page progress.");
        if (NativeRngV111.ToSnapshot(evt.Stochastic.NativeRng ?? expected) !=
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
        new EventChoicePayload(ChoiceId));

    [JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "A pending ReferenceNature Trash Heap session has no completed provider prefix.");
            var plan = TrashHeapChoicePlanBuilderV111.Build(
                Origin, SiteIdForChoice(ChoiceId));
            var law = ChanceLawEngineV111.QueryTrashHeap(
                Origin, SiteIdForChoice(ChoiceId));
            return [ReferenceNatureChanceLawDecisionV111.FromInitialState(
                law, ControlledStochasticRequestV111.FromIndex(plan.Request),
                Stochastic.InitialNatureState, plan.OutcomeIds)];
        }
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-trash-heap-session-v1";

    public static string[] TrashHeapSessionSiteIds =>
    [
        PrototypeNativeUnderdocksTrashHeap.CardRewardSiteId,
        PrototypeNativeUnderdocksTrashHeap.RelicRewardSiteId
    ];

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => Stochastic.DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => Stochastic.DecisionTranscript.Length == 1;

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
        TrashHeapChoicePlanBuilderV111.ValidateReplayOrigin(origin);
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
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
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
        var selection = ReferenceNatureV111.SampleValidated(
            law, Stochastic.NatureState);
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = Replay(Origin, ChoiceId, plan, law, selection);
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
            string.IsNullOrWhiteSpace(ChoiceId))
            throw new InvalidDataException(
                "Invalid ReferenceNature Trash Heap session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        TrashHeapChoicePlanBuilderV111
            .ValidateReplayOrigin(Origin);
        var plan = TrashHeapChoicePlanBuilderV111.Build(
            Origin, SiteIdForChoice(ChoiceId));
        if (Stochastic.DecisionTranscript.Length > 1 ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState,
                Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Trash Heap cursor differs from its session envelope.");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        RunState expectedRun;
        if (Complete)
        {
            var law = ChanceLawEngineV111.QueryTrashHeap(
                Origin, plan.Request.SiteId);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            Stochastic.DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(Stochastic.DecisionTranscript[0]))
                throw new InvalidDataException(
                    "ReferenceNature Trash Heap transcript does not replay.");
            expectedNatureState = selection.NextState;
            expectedRun = Replay(Origin, ChoiceId, plan, law, selection);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNatureState);
        }
        else
        {
            expectedRun = Origin.Fork();
        }

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
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
            session.Stochastic.InitialNatureState,
            session.Stochastic.NatureState,
            (ReferenceNatureDecisionV111[])
                session.Stochastic.DecisionTranscript.Clone());
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
            CanonicalJson.Sha256(replay.Stochastic.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            !replay.Stochastic.DecisionTranscript.SequenceEqual(
                snapshot.DecisionTranscript))
            throw new InvalidDataException(
                "ReferenceNature Trash Heap snapshot does not replay from its origin.");
        return replay;
    }

    private static RunState Replay(
        RunState origin, string choiceId,
        TrashHeapChoicePlanV111 plan, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var request = ControlledStochasticRequestV111.FromIndex(plan.Request);
        var orderedOutcomeIds = plan.OutcomeIds.Select((id, index) =>
            $"trash_heap_reward:{id}:index:{index}").ToArray();
        var selected = ReferenceNatureChanceLawDecisionV111
            .FromOrderedIndexSelection(
                law, request, selection, orderedOutcomeIds);
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
                    "Trash Heap fallback replay did not consume its selected request.");
            return fallbackRun;
        }
        return ReferenceNatureNativeEventIndexReplayV111
            .ReplayControlledIndex(
                origin, plan.Request, selected,
                run => new PrototypeGameEngine().Step(
                    run, GameAction.Create("event_choice",
                        new EventChoicePayload(choiceId))).State,
                PrototypeNativeUnderdocksTrashHeap.NativeRngEventId,
                "Trash Heap");
    }

    private static string SiteIdForChoice(string choiceId) => choiceId switch
    {
        "grab" => PrototypeNativeUnderdocksTrashHeap.CardRewardSiteId,
        "dive" => PrototypeNativeUnderdocksTrashHeap.RelicRewardSiteId,
        _ => throw new NotSupportedException(
            $"Trash Heap ReferenceNature does not support choice '{choiceId}'.")
    };
}
