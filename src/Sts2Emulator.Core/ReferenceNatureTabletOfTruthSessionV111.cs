using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

internal sealed record TabletOfTruthChoicePlanV111(
    int Page,
    int Cost,
    RandomIndexRequestV111 Request,
    long[] CandidateCardInstanceIds,
    string ConditioningFingerprint);

internal static class TabletOfTruthChoicePlanBuilderV111
{
    public static TabletOfTruthChoicePlanV111 Build(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var world = origin.World;
        if (origin.Phase != RunPhase.Event || world is null ||
            world.Event is not { } evt ||
            evt.EventId != PrototypeNativeTabletOfTruth.EventId ||
            evt.NativePageIndex is < 0 or > 3 ||
            evt.ChosenChoiceId is not null || evt.ControlledChoice is not null ||
            evt.NativeEventPotionSlot is not null ||
            evt.NativeEventRngProviderMode is not (null or "scripted-v111") ||
            origin.ControlledEventRewardChoice is not null)
            throw new InvalidOperationException(
                "Tablet of Truth Nature choices require a pending supported page draw.");

        var nativeRoute = origin.NativeRunRng is not null;
        var legacyFallbackRoute = origin.NativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            evt.NativeEventRng is null &&
            evt.NativeEventRngProviderMode is null;
        if (!nativeRoute && !legacyFallbackRoute)
            throw new InvalidOperationException(
                "Tablet of Truth requires a configured Native or Native-regional legacy event RNG route.");

        var page = evt.NativePageIndex;
        var cost = PrototypeNativeTabletOfTruth.DecipherCost(
            page, origin.Player.MaxHp);
        if (cost >= origin.Player.MaxHp)
            throw new InvalidOperationException(
                "Tablet of Truth decipher is lethal and has no card draw.");
        var playerAfterCost = PrototypeNativeTabletOfTruth
            .ApplyDecipherCost(origin.Player, cost);
        var candidates = PrototypeNativeTabletOfTruth
            .EligibleCardInstanceIds(playerAfterCost);
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Tablet of Truth has no eligible card draw on this page.");

        var legalActionId = PrototypeAiEnvironment.StableActionId(
            GameAction.Create("event_choice",
                new EventChoicePayload("decipher")));
        if (!new PrototypeGameEngine().GetLegalActions(origin).Any(action =>
                PrototypeAiEnvironment.StableActionId(action) == legalActionId))
            throw new InvalidOperationException(
                "Tablet of Truth decipher is not legal on the current page.");

        var streamId = nativeRoute
            ? NativeRngV111Contexts.EventStreamId(
                PrototypeNativeTabletOfTruth.NativeRngEventId)
            : "event";
        if (evt.NativeEventRng is { } savedStream &&
            savedStream.StreamId != streamId)
            throw new InvalidOperationException(
                "Tablet of Truth has the wrong Native event stream context.");
        if (nativeRoute && evt.NativeEventRng is null && page != 0)
            throw new InvalidOperationException(
                "Tablet of Truth cannot reconstruct a missing event stream after page progress.");
        var ownerId = "event:tablet_of_truth:eligible_cards:" +
            CanonicalJson.Sha256(candidates);
        var request = new RandomIndexRequestV111(
            PrototypeNativeTabletOfTruth.CardDrawSiteId,
            streamId, ownerId,
            nativeRoute ? page : GetLegacyEventOrdinal(origin),
            Enumerable.Range(0, candidates.Length).ToArray());
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            boundary = "overgrowth.tablet_of_truth.page_choice",
            eventId = evt.EventId,
            page,
            cost,
            playerHp = origin.Player.Hp,
            playerMaxHp = origin.Player.MaxHp,
            candidateCardInstanceIds = candidates,
            request,
            policy = ChanceLawEngineV111.TabletOfTruthConditioningPolicyId
        });
        return new(page, cost, request, candidates, conditioningFingerprint);
    }

    public static int GetLegacyEventOrdinal(RunState origin)
    {
        var streams = origin.Rng.Streams.Where(stream =>
            stream.StreamId == "event").ToArray();
        if (streams.Length != 1)
            throw new InvalidDataException(
                "Tablet of Truth fallback requires one legacy event stream.");
        var ordinal = streams[0].CallCount ?? 0;
        if (ordinal > int.MaxValue)
            throw new NotSupportedException(
                "Tablet of Truth legacy event ordinal exceeds the typed request range.");
        return (int)ordinal;
    }
}

public sealed record ReferenceNatureTabletOfTruthSessionSnapshotV111(
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

/// <summary>Atomic one-page Nature choice for the Tablet of Truth's eligible
/// physical-card upgrade draw.</summary>
public sealed record ReferenceNatureTabletOfTruthSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
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

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-tablet-of-truth-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length == 1;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryTabletOfTruth(Origin);

    public static ReferenceNatureTabletOfTruthSessionV111 Begin(
        RunState origin, ulong natureSeed) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Tablet of Truth"));

    public static ReferenceNatureTabletOfTruthSessionV111 Begin(
        RunState origin) => Begin(origin,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Tablet of Truth"));

    private static ReferenceNatureTabletOfTruthSessionV111 Begin(
        RunState origin, ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        _ = TabletOfTruthChoicePlanBuilderV111.Build(origin);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Tablet of Truth");
        var session = new ReferenceNatureTabletOfTruthSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureTabletOfTruthSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureTabletOfTruthSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Tablet of Truth session is already complete.");
        var plan = TabletOfTruthChoicePlanBuilderV111.Build(Origin);
        var law = ChanceLawEngineV111.QueryTabletOfTruth(Origin);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = Replay(Origin, plan, law, selection);
        if (Origin.NativeRunRng is not null)
        {
            var expectedStream = Origin.World?.Event?.NativeEventRng ??
                NativeRngV111Contexts.ForEvent(
                    Origin.RunSeed,
                    PrototypeNativeTabletOfTruth.NativeRngEventId);
            if (CanonicalJson.Sha256(replay.World?.Event?.NativeEventRng) !=
                CanonicalJson.Sha256(expectedStream))
                throw new InvalidDataException(
                    "Scripted Tablet of Truth resolution advanced its Native event cursor.");
        }
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
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Tablet of Truth session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        var plan = TabletOfTruthChoicePlanBuilderV111.Build(Origin);
        if (DecisionTranscript.Length > 1 ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Tablet of Truth cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        RunState expectedRun;
        if (Complete)
        {
            var law = ChanceLawEngineV111.QueryTabletOfTruth(Origin);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[0]))
                throw new InvalidDataException(
                    "ReferenceNature Tablet of Truth transcript does not replay.");
            expectedNatureState = selection.NextState;
            expectedRun = Replay(Origin, plan, law, selection);
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
                "ReferenceNature Tablet of Truth replay differs from its session envelope.");
    }

    public static string Save(ReferenceNatureTabletOfTruthSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureTabletOfTruthSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            (ReferenceNatureDecisionV111[])
                session.DecisionTranscript.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureTabletOfTruthSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureTabletOfTruthSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Tablet of Truth snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Tablet of Truth snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var initial = snapshot.InitialNatureState.Fork();
        var replay = Begin(origin, initial);
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
                "ReferenceNature Tablet of Truth snapshot does not replay from its origin.");
        return replay;
    }

    private static RunState Replay(
        RunState origin, TabletOfTruthChoicePlanV111 plan,
        ChanceLawV111 law, ReferenceNatureSelectionV111 selection)
    {
        var request = ControlledStochasticRequestV111.FromIndex(plan.Request);
        var orderedOutcomeIds = plan.CandidateCardInstanceIds.Select(
            instanceId => "card_instance:" + instanceId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var selected = ReferenceNatureChanceLawDecisionV111
            .FromOrderedIndexSelection(
                law, request, selection, orderedOutcomeIds);
        if (origin.NativeRunRng is null)
        {
            var replay = ControlledChanceContinuationExecutorV111
                .CaptureStochastic(
                    origin,
                    ControlledStochasticContinuationStateV111
                        .CurrentSchemaId,
                    [selected],
                    run => new PrototypeGameEngine().Step(
                        run, GameAction.Create("event_choice",
                            new EventChoicePayload("decipher"))).State,
                    candidate =>
                        ControlledStochasticChanceReplayV111.SameRequest(
                            candidate, request));
            if (replay.Suspended || replay.Run is not { } fallbackRun)
                throw new InvalidDataException(
                    "Tablet of Truth fallback replay did not consume its selected request.");
            var originEvent = origin.World?.Event;
            var replayEvent = fallbackRun.World?.Event;
            if (originEvent?.NativeEventRng is not null ||
                originEvent?.NativeEventRngProviderMode is not null ||
                replayEvent?.NativeEventRng is not null ||
                replayEvent?.NativeEventRngProviderMode is not null ||
                TabletOfTruthChoicePlanBuilderV111.GetLegacyEventOrdinal(
                    fallbackRun) !=
                    TabletOfTruthChoicePlanBuilderV111.GetLegacyEventOrdinal(
                        origin))
                throw new InvalidDataException(
                    "Tablet of Truth fallback replay changed an event RNG cursor or provider marker.");
            return fallbackRun;
        }
        return ReferenceNatureNativeEventIndexReplayV111
            .ReplayControlledIndex(
                origin, plan.Request, selected,
                run => new PrototypeGameEngine().Step(
                    run, GameAction.Create("event_choice",
                        new EventChoicePayload("decipher"))).State,
                PrototypeNativeTabletOfTruth.NativeRngEventId,
                "Tablet of Truth");
    }
}

public static partial class ChanceLawEngineV111
{
    public const string TabletOfTruthConditioningPolicyId =
        "tablet-of-truth-page-eligible-card-v1";

    public static string[] TabletOfTruthSessionSiteIds =>
        [PrototypeNativeTabletOfTruth.CardDrawSiteId];

    public static ChanceLawV111 QueryTabletOfTruth(RunState origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var plan = TabletOfTruthChoicePlanBuilderV111.Build(origin);
        var nativeIndexLaw = NativeRngV111.IsCanonicalStreamId(
            plan.Request.StreamId);
        var indexWeights = nativeIndexLaw
            ? NativeNextIntGridWeights(plan.Request.SupportCount)
            : Enumerable.Repeat(1L, plan.Request.SupportCount).ToArray();
        var outcomes = plan.CandidateCardInstanceIds.Select((instanceId, index) =>
            new ChanceLawOutcomeV111(
                "card_instance:" + instanceId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                indexWeights[index]))
            .ToArray();
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "overgrowth.tablet_of_truth.page_choice",
            plan.ConditioningFingerprint
        });
        return Create(eventId, plan.Request.SiteId,
            plan.Request.OperationKind, plan.Request.OwnerId,
            plan.Request.Ordinal, TabletOfTruthConditioningPolicyId,
            plan.ConditioningFingerprint,
            nativeIndexLaw
                ? NativeBoundedIndexSourceVerifiedProvenance
                : "uniform-saved-index-support-unverified",
            outcomes,
            nativeIndexLaw
                ? ChanceLawV111.SourceVerifiedStatus
                : ChanceLawV111.UnverifiedStatus);
    }
}
