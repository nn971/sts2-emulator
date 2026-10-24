using System.Text.Json;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string NativePunchOffRewardConditioningPolicyId =
        "native-punch-off-reward-prefix-v1";

    public static string[] NativePunchOffRewardQuerySiteIds =>
        StochasticSiteRegistryV111.NativePunchOffRewardSiteIds;

    public static ChanceLawV111 QueryLegacyPunchOffReward(
        string siteId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!ChanceLawEngineV111.NativePunchOffRewardQuerySiteIds.Contains(
                siteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"Legacy Punch Off reward law does not support site '{siteId}'.");
        if (supportCount < 1)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            siteId, "reward", ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "legacy-punch-off-combat-victory-reward-index",
            request
        });
        return ChanceLawEngineV111.QueryRandomIndex(eventId, request);
    }

    public static ChanceLawV111 QueryNativePunchOffReward(
        RunState origin, string siteId, int ordinal, int[]? priorIndices,
        string actionId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        priorIndices ??= [];
        var plan = NativePunchOffRewardPlanBuilderV111.Build(
            origin, siteId, ordinal, priorIndices, actionId);
        var candidates = NativePunchOffRewardPlanBuilderV111.Candidates(plan);
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native_event.punch_off_reward",
            plan.ConditioningFingerprint
        });
        var request = NativePunchOffRewardPlanBuilderV111.Request(plan);
        var boundedIndexLaw = QueryRandomIndex(eventId, request);
        var outcomes = candidates.Select((candidateId, index) =>
            new ChanceLawOutcomeV111(
                (siteId == PrototypeNativePunchOff.RelicRewardSiteId
                    ? "relic:" : "potion:") + candidateId,
                boundedIndexLaw.Outcomes[index].Weight)).ToArray();
        return Create(eventId, plan.SiteId,
            request.OperationKind, request.OwnerId, request.Ordinal,
            NativePunchOffRewardConditioningPolicyId,
            plan.ConditioningFingerprint,
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }
}

public sealed record ReferenceNaturePunchOffRewardSessionSnapshotV111(
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

/// <summary>Atomic Nature sampling for the two independent Punch Off fight
/// rewards, resolved by one immediate winning combat action.</summary>
public sealed record ReferenceNaturePunchOffRewardSessionV111(
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
            "Punch Off reward action is no longer legal at its saved origin.");

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get
        {
            if (!Complete)
                throw new InvalidOperationException(
                    "A pending Punch Off reward session has no completed provider prefix.");
            var natureState = Stochastic.InitialNatureState.Fork();
            var priorIndices = new List<int>();
            var decisions = new ControlledStochasticDecisionV111[2];
            for (var index = 0; index < decisions.Length; index++)
            {
                var siteId = index == 0
                    ? PrototypeNativePunchOff.RelicRewardSiteId
                    : PrototypeNativePunchOff.PotionRewardSiteId;
                var ordinal = index == 0
                    ? CurrentRewardCursor(Origin)
                    : unchecked(CurrentRewardCursor(Origin) + 1);
                var prior = priorIndices.ToArray();
                var plan = NativePunchOffRewardPlanBuilderV111.Build(
                    Origin, siteId, ordinal, prior, ActionId);
                var law = ChanceLawEngineV111.QueryNativePunchOffReward(
                    Origin, siteId, ordinal, prior, ActionId);
                var selection = ReferenceNatureV111.SampleValidated(
                    law, natureState);
                var expected = new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex);
                if (CanonicalJson.Sha256(expected) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]))
                    throw new InvalidDataException(
                        "Punch Off reward provider prefix differs from its Nature transcript.");
                var labels = law.Outcomes.Select(outcome =>
                    outcome.OutcomeId).ToArray();
                var mapped = ReferenceNatureChanceLawDecisionV111
                    .FromOrderedIndexSelection(law,
                        ControlledStochasticRequestV111.FromIndex(
                            NativePunchOffRewardPlanBuilderV111.Request(plan)),
                        selection, labels);
                var indexDecision = mapped.IndexDecision ??
                    throw new InvalidDataException(
                        "Punch Off reward provider replay requires an index decision.");
                decisions[index] =
                    ControlledStochasticDecisionV111.FromIndex(
                        indexDecision, labels);
                priorIndices.Add(selection.OutcomeIndex);
                natureState = selection.NextState;
            }
            return decisions;
        }
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-punch-off-reward-session-v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Complete => Stochastic.DecisionTranscript.Length == 2;

    [System.Text.Json.Serialization.JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (Complete)
                return null;
            var priorIndices = Stochastic.DecisionTranscript.Select(
                decision => decision.OutcomeIndex).ToArray();
            var plan = NativePunchOffRewardPlanBuilderV111.Build(
                Origin,
                Stochastic.DecisionTranscript.Length == 0
                    ? PrototypeNativePunchOff.RelicRewardSiteId
                    : PrototypeNativePunchOff.PotionRewardSiteId,
                Stochastic.DecisionTranscript.Length == 0
                    ? CurrentRewardCursor(Origin)
                    : unchecked(CurrentRewardCursor(Origin) + 1),
                priorIndices, ActionId);
            return ChanceLawEngineV111.QueryNativePunchOffReward(
                Origin, plan.SiteId, plan.RequestedOrdinal,
                priorIndices, ActionId);
        }
    }

    public static string[] SessionSiteIds =>
        StochasticSiteRegistryV111.NativePunchOffRewardSiteIds;

    public static ReferenceNaturePunchOffRewardSessionV111 Begin(
        RunState origin, string actionId, ulong natureSeed) => Begin(
            origin, actionId, ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Punch Off rewards"));

    public static ReferenceNaturePunchOffRewardSessionV111 Begin(
        RunState origin, string actionId) => Begin(
            origin, actionId, ReferenceNatureRunCursorV111.Begin(
                origin, null, "Punch Off rewards"));

    private static ReferenceNaturePunchOffRewardSessionV111 Begin(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(initial);
        _ = NativePunchOffRewardPlanBuilderV111.Build(
            origin, PrototypeNativePunchOff.RelicRewardSiteId,
            CurrentRewardCursor(origin), [], actionId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Punch Off rewards");
        var session = new ReferenceNaturePunchOffRewardSessionV111(
            CurrentSchemaId, actionId, origin.Fork(), origin.Fork(),
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNaturePunchOffRewardSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNaturePunchOffRewardSessionV111 ResolveNext()
    {
        Validate();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "Punch Off ReferenceNature session is already complete.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, Stochastic.NatureState);
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNature = selection.NextState;
        var nextRun = Run;
        if (transcript.Length == 2)
            nextRun = Replay(Origin, ActionId, transcript);
        var next = this with
        {
            Run = transcript.Length == 2
                ? ReferenceNatureRunCursorV111.Commit(nextRun, nextNature)
                : nextRun,
            NatureState = nextNature,
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
                "Invalid ReferenceNature Punch Off reward session envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        if (Stochastic.DecisionTranscript.Length > 2)
            throw new InvalidDataException(
                "Invalid ReferenceNature Punch Off reward session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        var expectedNature = Stochastic.InitialNatureState.Fork();
        var priorIndices = new List<int>();
        for (var index = 0; index < Stochastic.DecisionTranscript.Length; index++)
        {
            var siteId = index == 0
                ? PrototypeNativePunchOff.RelicRewardSiteId
                : PrototypeNativePunchOff.PotionRewardSiteId;
            var ordinal = index == 0
                ? CurrentRewardCursor(Origin)
                : unchecked(CurrentRewardCursor(Origin) + 1);
            var law = ChanceLawEngineV111.QueryNativePunchOffReward(
                Origin, siteId, ordinal, priorIndices.ToArray(), ActionId);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNature);
            Stochastic.DecisionTranscript[index].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Punch Off reward transcript does not replay from its conditioned prefix.");
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
                "Punch Off reward replay differs from its session envelope.");
    }

    public static string Save(
        ReferenceNaturePunchOffRewardSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNaturePunchOffRewardSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                session.ActionId, RunSnapshot.Save(session.Run),
                session.Stochastic.InitialNatureState,
                session.Stochastic.NatureState,
                (ReferenceNatureDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone()));
    }

    public static ReferenceNaturePunchOffRewardSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNaturePunchOffRewardSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Punch Off reward snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.ActionId) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null ||
            snapshot.DecisionTranscript is null ||
            snapshot.DecisionTranscript.Length > 2)
            throw new InvalidDataException(
                "Invalid ReferenceNature Punch Off reward snapshot.");
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
                "Punch Off reward snapshot does not replay from its origin.");
        return session;
    }

    private static int CurrentRewardCursor(RunState origin)
    {
        if (origin.Stochastic.NativeRunRng is not { } nativeRunRng ||
            !nativeRunRng.TryGetStream(
                NativePlayerRngV111.RewardsStreamId, out var stream))
            throw new InvalidOperationException(
                "Punch Off ReferenceNature session has no player.rewards stream.");
        return NativeRngV111.ToSnapshot(stream).Counter;
    }

    private static RunState Replay(
        RunState origin, string actionId,
        ReferenceNatureDecisionV111[] transcript)
    {
        var relicPlan = NativePunchOffRewardPlanBuilderV111.Build(
            origin, PrototypeNativePunchOff.RelicRewardSiteId,
            CurrentRewardCursor(origin), [], actionId);
        var potionPlan = NativePunchOffRewardPlanBuilderV111.Build(
            origin, PrototypeNativePunchOff.PotionRewardSiteId,
            unchecked(CurrentRewardCursor(origin) + 1),
            [transcript[0].OutcomeIndex], actionId);
        var decisions = new[]
        {
            new RandomIndexDecisionV111(
                relicPlan.RelicRequest, transcript[0].OutcomeIndex,
                "scripted-v111"),
            new RandomIndexDecisionV111(
                potionPlan.PotionRequest, transcript[1].OutcomeIndex,
                "scripted-v111")
        };
        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId)
            ?? throw new InvalidDataException(
                "Punch Off replay action is no longer legal at the session origin.");
        using var journal = RandomBoundaryJournalV111.Begin();
        var replay = NativePunchOffControlledIndexReplayV111.Capture(
            origin, decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint.Fork(), action).State,
            [PrototypeNativePunchOff.RelicRewardSiteId,
             PrototypeNativePunchOff.PotionRewardSiteId]);
        if (replay.Suspended || replay.Run is not { } run)
            throw new InvalidDataException(
                "Punch Off reward action did not consume its complete selected reward prefix.");
        var entries = journal.Entries;
        RngStreamState? rewardsStream = null;
        if (run.Stochastic.NativeRunRng is { } nativeRunRng)
            nativeRunRng.TryGetStream(
                NativePlayerRngV111.RewardsStreamId, out rewardsStream);
        var originRewards = relicPlan.RewardsCursorBefore;
        var eventStream = run.World?.Event?.Stochastic.NativeRng;
        var relicId = relicPlan.RelicCandidates[transcript[0].OutcomeIndex];
        var potionId = potionPlan.PotionCandidates[transcript[1].OutcomeIndex];
        var expectedEventCursor = relicPlan.EventCursorBefore;
        var actualEventCursor = eventStream is null ? null :
            NativeRngV111.ToSnapshot(eventStream);
        var rewardEntries = entries.Where(entry =>
            entry.Request.StreamId == NativePlayerRngV111.RewardsStreamId)
            .ToArray();
        if (run.Phase != RunPhase.Reward ||
            run.World?.Combat is not null ||
            run.World?.Reward is not { } reward ||
            reward.RelicOption != relicId || reward.PotionOption != potionId ||
            rewardsStream is null ||
            NativeRngV111.ToSnapshot(rewardsStream) != originRewards ||
            expectedEventCursor != actualEventCursor ||
            rewardEntries.Length != 2 ||
            rewardEntries[0].ProviderMode != "controlled-v111" ||
            rewardEntries[0].Request.SiteId != relicPlan.RelicRequest.SiteId ||
            rewardEntries[1].ProviderMode != "controlled-v111" ||
            rewardEntries[1].Request.SiteId != potionPlan.PotionRequest.SiteId ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                rewardEntries[0].Request, relicPlan.RelicRequest) ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                rewardEntries[1].Request, potionPlan.PotionRequest))
            throw new InvalidDataException(
                "Scripted Punch Off rewards changed their candidate order, player.rewards cursor, event cursor, or reward identity.");
        return run;
    }

}
