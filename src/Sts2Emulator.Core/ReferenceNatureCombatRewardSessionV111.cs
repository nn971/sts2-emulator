using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureCombatRewardLawDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature combat reward law decision.");
    }
}

public sealed record ReferenceNatureCombatRewardSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureCombatRewardLawDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    private ControlledStochasticDecisionV111[]? _providerDecisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(
            ProviderDecisions);
    private ReferenceNatureCombatRewardLawDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);
    private ControlledStochasticRequestV111? _pendingRequest =
        ReferenceNatureSessionValueCopiesV111.CopyRequest(PendingRequest);
    private string[]? _pendingCandidateLabels =
        ReferenceNatureSessionValueCopiesV111.CopyArray(
            PendingCandidateLabels);

    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(_providerDecisions)!;
        init => _providerDecisions = ReferenceNatureSessionValueCopiesV111
            .CopyProviderDecisions(value);
    }
    public ReferenceNatureCombatRewardLawDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
    public ControlledStochasticRequestV111? PendingRequest
    {
        get => ReferenceNatureSessionValueCopiesV111.CopyRequest(
            _pendingRequest);
        init => _pendingRequest =
            ReferenceNatureSessionValueCopiesV111.CopyRequest(value);
    }
    public string[]? PendingCandidateLabels
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_pendingCandidateLabels);
        init => _pendingCandidateLabels = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
}

/// <summary>
/// Atomic combat victory and reward generation driven by conditional Chance
/// Laws for registered Native and retained Prototype requests. Each prefix
/// replays the legal combat action from its checkpoint; only the completed
/// reward state and Nature cursor publish.
/// </summary>
public sealed record ReferenceNatureCombatRewardSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureCombatRewardLawDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureRunActionMechanicsStateV111,
        ReferenceNatureControlledActionStochasticStateV111<
            ReferenceNatureCombatRewardLawDecisionV111>> Simulation { get; init; } =
        new(
            new ReferenceNatureRunActionMechanicsStateV111(
                Origin, Action, Run),
            new ReferenceNatureControlledActionStochasticStateV111<
                ReferenceNatureCombatRewardLawDecisionV111>(
                InitialNatureState, NatureState, ProviderDecisions,
                DecisionTranscript, PendingRequest, PendingCandidateLabels));

    [JsonIgnore]
    public ReferenceNatureRunActionMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureControlledActionStochasticStateV111<
        ReferenceNatureCombatRewardLawDecisionV111> Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public GameAction Action
    {
        get => Mechanics.Action;
        init => Mechanics = Mechanics with { Action = value };
    }

    public RunState Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics with { Run = value };
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

    public ControlledStochasticDecisionV111[] ProviderDecisions
    {
        get => Stochastic.ProviderDecisions;
        init => Stochastic = Stochastic with { ProviderDecisions = value };
    }
    public ReferenceNatureCombatRewardLawDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }
    public ControlledStochasticRequestV111? PendingRequest
    {
        get => Stochastic.PendingRequest;
        init => Stochastic = Stochastic with { PendingRequest = value };
    }
    public string[]? PendingCandidateLabels
    {
        get => Stochastic.PendingCandidateLabels;
        init => Stochastic = Stochastic with
        { PendingCandidateLabels = value };
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-combat-reward-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Stochastic.PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => Stochastic.ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Stochastic.PendingRequest is null ? null :
        QueryLaw(Origin, Action, Stochastic.PendingRequest, Stochastic.PendingCandidateLabels,
            Stochastic.ProviderDecisions);

    public static ReferenceNatureCombatRewardSessionV111 Begin(
        RunState source, GameAction action, ulong seed) =>
        Begin(source, action, ReferenceNatureRunCursorV111.Begin(
            source, seed, "Combat reward"));

    public static ReferenceNatureCombatRewardSessionV111 Begin(
        RunState source, GameAction action) =>
        Begin(source, action, ReferenceNatureRunCursorV111.Begin(
            source, null, "Combat reward"));

    private static ReferenceNatureCombatRewardSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, action);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Combat reward");

        var natureOrigin = CreateNatureReplayOrigin(source);
        var replay = Replay(natureOrigin, action, []);
        var run = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, initialNatureState)
            : natureOrigin.Fork();
        var session = new ReferenceNatureCombatRewardSessionV111(
            CurrentSchemaId, natureOrigin.Fork(), CloneAction(action), run,
            initialNatureState.Fork(), initialNatureState.Fork(), [], [],
            replay.Continuation?.PendingRequest.Fork(),
            replay.Continuation?.PendingCandidateLabels is { } labels
                ? (string[])labels.Clone() : null);
        session.Validate();
        return session;
    }

    public ReferenceNatureCombatRewardSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureCombatRewardSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature combat reward session is already complete.");

        var law = QueryLaw(Origin, Action, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels, Stochastic.ProviderDecisions);
        var selection = ReferenceNatureV111.SampleValidated(law, Stochastic.NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, Stochastic.PendingRequest!, selection, Origin, Stochastic.PendingCandidateLabels);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            Stochastic.ProviderDecisions, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            CreateNatureReplayOrigin(Origin), continuation, providerDecision,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State);
        var providerDecisions = replay.Continuation?.Decisions ??
            Stochastic.ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureCombatRewardLawDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var run = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, selection.NextState)
            : Origin.Fork();
        var next = this with
        {
            Run = run,
            NatureState = selection.NextState,
            ProviderDecisions = providerDecisions,
            DecisionTranscript = transcript,
            PendingRequest = replay.Continuation?.PendingRequest.Fork(),
            PendingCandidateLabels = replay.Continuation?.PendingCandidateLabels is { } labels
                ? (string[])labels.Clone() : null
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Run is null ||
            string.IsNullOrWhiteSpace(Action.Kind))
            throw new InvalidDataException(
                "Invalid ReferenceNature combat reward session envelope.");
        Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction);
        Stochastic.Validate();
        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        if (RequiresNatureRelicBagProjection(Origin))
        {
            ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
                Origin, "Combat reward session origin");
            ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
                Run, "Combat reward session replay");
        }
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, Stochastic.InitialNatureState, "Combat reward");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var prefix = Stochastic.ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var continuationAtBoundary = replayAtBoundary.Continuation;
            var request = continuationAtBoundary?.PendingRequest
                ?? throw new InvalidDataException(
                    "Combat reward replay completed before its saved decision transcript.");
            var labels = continuationAtBoundary!.PendingCandidateLabels;
            var savedDecision = Stochastic.ProviderDecisions[index];
            savedDecision.Validate();
            if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                    request, AsRequest(savedDecision)) ||
                !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                    labels, savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "ReferenceNature combat reward provider transcript changed its request.");

            var law = QueryLaw(Origin, Action, request, labels, prefix);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedTranscript = new ReferenceNatureCombatRewardLawDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "ReferenceNature combat reward law decisions do not replay.");
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, Action, Stochastic.ProviderDecisions);
        if ((replay.Continuation is null) != Complete ||
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
                replay.Continuation?.PendingRequest, Stochastic.PendingRequest) ||
            !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                replay.Continuation?.PendingCandidateLabels,
                Stochastic.PendingCandidateLabels) ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState))
            throw new InvalidDataException(
                "ReferenceNature combat reward continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState, Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature combat reward run differs from its atomic cursor.");

        foreach (var decision in Stochastic.DecisionTranscript)
            decision.Validate();
    }

    public static string Save(ReferenceNatureCombatRewardSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureCombatRewardSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.Stochastic.InitialNatureState, session.Stochastic.NatureState,
            session.Stochastic.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureCombatRewardLawDecisionV111[])
                session.Stochastic.DecisionTranscript.Clone(),
            session.Stochastic.PendingRequest?.Fork(),
            session.Stochastic.PendingCandidateLabels is null ? null :
                (string[])session.Stochastic.PendingCandidateLabels.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureCombatRewardSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureCombatRewardSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature combat reward snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.ProviderDecisions is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature combat reward snapshot.");
        var session = new ReferenceNatureCombatRewardSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action), RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState.Fork(), snapshot.NatureState.Fork(),
            snapshot.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureCombatRewardLawDecisionV111[])
                snapshot.DecisionTranscript.Clone(),
            snapshot.PendingRequest?.Fork(),
            snapshot.PendingCandidateLabels is null ? null :
                (string[])snapshot.PendingCandidateLabels.Clone());
        session.Validate();
        return session;
    }

    private static ChanceLawV111 QueryLaw(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        request.Validate();
        if (source.Stochastic.NativeRunRng is null)
        {
            if (request.IndexRequest is not { } legacyRequest ||
                !LegacyRewardSessionSites.Contains(
                    legacyRequest.SiteId, StringComparer.Ordinal) ||
                legacyRequest.StreamId != "reward")
                throw new NotSupportedException(
                    $"ReferenceNature legacy combat reward replay does not support stochastic site '{request.SiteId}'.");
            var boundaryId = CanonicalJson.Sha256(new
            {
                boundary = "legacy-prototype-combat-reward",
                actionId = PrototypeAiEnvironment.StableActionId(action),
                requestFingerprint = CanonicalJson.Sha256(legacyRequest),
                candidateLabels,
                prefixFingerprint = CanonicalJson.Sha256(prefix)
            });
            return ChanceLawEngineV111.QueryRandomIndex(
                boundaryId, legacyRequest);
        }

        if (request.UnitFloatRequest is { } floatRequest)
        {
            if (floatRequest.SiteId == "reward.potion_odds.unit_float")
            {
                if (source.World is not { ActiveRoom: { } room } world ||
                    room is not (PrototypeRoomType.Combat or
                        PrototypeRoomType.Elite or PrototypeRoomType.Boss))
                    throw new InvalidDataException(
                        "Combat potion odds request has no active Native combat room.");
                var forced = source.Player.ActiveRelics.Any(relic =>
                    PrototypeContent.Relic(relic.RelicId)
                        .ForceCombatPotionReward);
                return ChanceLawEngineV111.QueryNativePotionRewardOdds(
                    world.PotionRewardOddsThousandths,
                    room == PrototypeRoomType.Elite, forced,
                    floatRequest.Ordinal);
            }
            if (floatRequest.SiteId == "potion_factory.rarity_roll.unit_float")
            {
                if (floatRequest.StreamId != NativePlayerRngV111.RewardsStreamId)
                    throw new InvalidDataException(
                        "Combat PotionFactory rarity request uses a non-reward stream.");
                return ChanceLawEngineV111.QueryNativePotionFactoryRarity(
                    floatRequest);
            }
            if (floatRequest.SiteId == "card_rarity.roll.unit_float")
            {
                var owner = ParseEncounterCardRarityOwner(
                    floatRequest.OwnerId);
                if (floatRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                    source.World is not { } world || owner.Act != world.Act ||
                    world.ActiveRoom != (owner.Source switch
                    {
                        PrototypeNativeCardOddsSource.RegularEncounter =>
                            PrototypeRoomType.Combat,
                        PrototypeNativeCardOddsSource.EliteEncounter =>
                            PrototypeRoomType.Elite,
                        PrototypeNativeCardOddsSource.BossEncounter =>
                            PrototypeRoomType.Boss,
                        _ => throw new ArgumentOutOfRangeException()
                    }))
                    throw new InvalidDataException(
                        "Combat card-rarity request differs from its active room.");
                return ChanceLawEngineV111.QueryNativeCardRarity(
                    owner.Source, owner.Offset, source.Ascension,
                    floatRequest.OwnerId, floatRequest.Ordinal);
            }
            if (floatRequest.SiteId == "card_rarity.upgrade_roll.unit_float")
            {
                var owner = ParseCardUpgradeOwner(floatRequest.OwnerId);
                if (floatRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                    source.World is not { } world || owner.Act != world.Act ||
                    owner.Ascension != source.Ascension)
                    throw new InvalidDataException(
                        "Combat card-upgrade request differs from its active run.");
                return ChanceLawEngineV111.QueryNativeEncounterCardUpgrade(
                    owner.CardId, owner.Act, owner.Ascension,
                    floatRequest.Ordinal);
            }
            if (floatRequest.SiteId == "relic_grab_bag.rarity.unit_float")
                return ChanceLawEngineV111.QueryNativeRelicRarity(
                    floatRequest);
        }

        if (request.IndexRequest is not { } indexRequest)
            throw new NotSupportedException(
                $"ReferenceNature combat reward replay does not support stochastic site '{request.SiteId}'.");

        if (indexRequest.SiteId == "reward.combat_gold.index_draw")
        {
            var owner = ParseCombatGoldOwner(indexRequest.OwnerId);
            if (indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                source.World?.ActiveRoom != owner.Room ||
                source.Ascension != owner.Ascension)
                throw new InvalidDataException(
                    "Combat gold request differs from its active room or ascension.");
            var expected = ChanceLawEngineV111.QueryNativeCombatGold(
                owner.Room, owner.Ascension,
                owner.Room == PrototypeRoomType.Combat ? 1f : null,
                indexRequest.Ordinal);
            ValidateIndexRequestAgainstLaw(indexRequest, expected,
                candidateLabels);
            return expected;
        }
        if (indexRequest.SiteId == "potion_factory.model_within_rarity.index_draw")
        {
            if (indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId)
                throw new InvalidDataException(
                    "Combat PotionFactory model request uses a non-reward stream.");
            var rarityDecision = prefix.LastOrDefault(decision =>
                decision.UnitFloatDecision?.Request.SiteId ==
                "potion_factory.rarity_roll.unit_float")?.UnitFloatDecision;
            if (rarityDecision is null || indexRequest.Ordinal != 0 ||
                prefix.Any(decision => decision.IndexDecision?.Request.SiteId ==
                    indexRequest.SiteId))
                throw new InvalidDataException(
                    "Combat potion model request has no unique preceding rarity decision.");
            var rarity = PrototypeNativePotionShop.ResolveRarityNative(
                rarityDecision.SelectedValue);
            return ChanceLawEngineV111.QueryNativePotionFactoryModelIndex(
                indexRequest, rarity, []);
        }
        if (indexRequest.SiteId ==
            StochasticSiteRegistryV111.NativeRewardCardModelSiteId)
        {
            if (indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                indexRequest.OperationKind != "choose_index" ||
                candidateLabels is not { Length: > 0 } labels ||
                labels.Length != indexRequest.SupportCount)
                throw new InvalidDataException(
                    "Native combat-reward card request has no ordered semantic pool.");
            var generatedCard =
                SemanticGeneratedCardNativeAuditV111
                    .CreateNativeRewardEvent(indexRequest, labels);
            return ChanceLawEngineV111.QueryGeneratedCard(generatedCard);
        }
        if (indexRequest.SiteId == "card_rarity.encounter_model.index_draw")
        {
            if (indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId)
                throw new InvalidDataException(
                    "Combat card model request uses a non-reward stream.");
            var expected = ChanceLawEngineV111.QueryNativeEncounterCardModelIndex(
                indexRequest.OwnerId, indexRequest.SupportCount,
                indexRequest.Ordinal);
            ValidateIndexRequestAgainstLaw(indexRequest, expected,
                candidateLabels);
            return expected;
        }
        if (indexRequest.SiteId == PrototypeNativeRelicGrabBag
                .NativeTreasureModelSiteId)
        {
            if (candidateLabels is null || source.World is not { } world)
                throw new InvalidDataException(
                    "Combat reward relic identity request lost its candidate labels or world.");
            var rarityText = SegmentAfter(indexRequest.OwnerId,
                ":rarity:", ":pool:");
            if (!Enum.TryParse<PrototypeRelicRarity>(rarityText,
                    ignoreCase: false, out var rarity) ||
                !Enum.IsDefined(rarity))
                throw new InvalidDataException(
                    "Combat reward relic identity request has an invalid rarity.");
            var bag = world.Stochastic.RelicBag ??
                PrototypeNativeRelicGrabBag.CreateNatureUnordered(source.Player);
            foreach (var decision in prefix.Where(decision =>
                decision.IndexDecision?.Request.SiteId ==
                PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId))
            {
                var indexDecision = decision.IndexDecision!;
                if (decision.CandidateLabels is not { } priorLabels ||
                    indexDecision.SelectedIndex >= priorLabels.Length)
                    throw new InvalidDataException(
                        "Combat reward relic prefix lost its selected identity.");
                var priorRarityText = SegmentAfter(
                    indexDecision.Request.OwnerId, ":rarity:", ":pool:");
                if (!Enum.TryParse<PrototypeRelicRarity>(priorRarityText,
                        ignoreCase: false, out var priorRarity) ||
                    !Enum.IsDefined(priorRarity))
                    throw new InvalidDataException(
                        "Combat reward relic prefix has an invalid rarity.");
                var priorPlan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                    bag, priorRarity, merchant: false, source.Player);
                if (indexDecision.Request.OwnerId !=
                    PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                        world, priorRarity, priorPlan.CandidateIds))
                    throw new InvalidDataException(
                        "Combat reward relic prefix differs from its canonical bag request.");
                (bag, _) = PrototypeNativeRelicGrabBag.PullNatureSelected(
                    bag, priorRarity, priorLabels,
                    indexDecision.SelectedIndex, merchant: false,
                    source.Player);
            }
            return ChanceLawEngineV111.QueryNativeCombatRewardRelicSelection(
                source, indexRequest, rarity, bag, candidateLabels);
        }
        if (indexRequest.SiteId ==
            StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId)
        {
            if (indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId)
                throw new InvalidDataException(
                    "Boss relic offer request uses a non-reward stream.");
            var law = ChanceLawEngineV111.QueryNativeBossRelicOfferShuffleIndex(
                indexRequest.OwnerId, indexRequest.SupportCount,
                indexRequest.Ordinal);
            ValidateIndexRequestAgainstLaw(indexRequest, law, candidateLabels);
            return law;
        }
        if (indexRequest.SiteId ==
            StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId)
        {
            if (indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId)
                throw new InvalidDataException(
                    "Lava Rock relic offer request uses a non-reward stream.");
            var law = ChanceLawEngineV111.QueryNativeLavaRockRelicOfferShuffleIndex(
                indexRequest.OwnerId, indexRequest.SupportCount,
                indexRequest.Ordinal);
            ValidateIndexRequestAgainstLaw(indexRequest, law, candidateLabels);
            return law;
        }

        throw new NotSupportedException(
            $"ReferenceNature combat reward replay does not support stochastic site '{request.SiteId}'.");
    }

    internal static ChanceLawV111 QueryLawForCapture(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix) =>
        QueryLaw(source, action, request, candidateLabels, prefix);

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, GameAction action,
        ControlledStochasticDecisionV111[] decisions)
        => ControlledChanceContinuationExecutorV111.CaptureStochastic(
            CreateNatureReplayOrigin(source),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State,
            request => SessionSites.Contains(
                request.SiteId, StringComparer.Ordinal));

    private static readonly string[] LegacyRewardSessionSites =
    [
        "reward.potion_odds.ticket",
        "reward.combat_gold.index_draw",
        "reward.random_potion.drop.ticket",
        "reward.random_potion.model",
        "reward.elite_relic.model",
        "reward.card_rarity.ticket",
        "reward.card_model.index_draw",
        "card_rarity.roll.ticket",
        "card_rarity.upgrade_roll.ticket",
        "potion.rarity_roll.ticket",
        "potion.model_within_rarity.index_draw",
        PrototypeNativePotionShop.UniformModelSiteId,
        "relic_grab_bag.rarity.ticket",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
        StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId,
        StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId
    ];

    private static readonly string[] SessionSites = new[]
    {
        "reward.potion_odds.unit_float",
        "reward.combat_gold.index_draw",
        "potion_factory.rarity_roll.unit_float",
        "potion_factory.model_within_rarity.index_draw",
        "card_rarity.roll.unit_float",
        "card_rarity.encounter_model.index_draw",
        StochasticSiteRegistryV111.NativeRewardCardModelSiteId,
        "card_rarity.upgrade_roll.unit_float",
        "relic_grab_bag.rarity.unit_float",
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
        StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId,
        StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId
    }.Concat(LegacyRewardSessionSites)
        .Distinct(StringComparer.Ordinal).ToArray();

    public static string[] SessionSiteIds => (string[])SessionSites.Clone();

    private static RunState CreateNatureReplayOrigin(RunState source)
    {
        if (!RequiresNatureRelicBagProjection(source))
            return source.Fork();
        return ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
            source, createRelicBagWhenMissing: true);
    }

    private static bool RequiresNatureRelicBagProjection(RunState source) =>
        source.Stochastic.NativeRunRng is not null ||
        (source.World is { } world &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world));

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        if (source.Phase != RunPhase.Combat || source.World is not { } world ||
            (source.Stochastic.NativeRunRng is not null &&
                !PrototypeGameEngine.UsesNativeRegionalSystems(world)) ||
            source.Stochastic.ControlledRunActionChoice is not null ||
            world.ActiveRoom is not (PrototypeRoomType.Combat or
                PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new InvalidOperationException(
                "ReferenceNature combat reward session requires a configured Native or retained Prototype combat boundary.");
        if (!new PrototypeGameEngine().GetLegalActions(source).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "ReferenceNature combat reward session requires a current legal combat action.");
        var preview = new PrototypeGameEngine().Step(
            source.Fork(), CloneAction(action)).State;
        if (preview.Phase != RunPhase.Reward || preview.World?.Reward is null)
            throw new InvalidOperationException(
                "ReferenceNature combat reward session requires an action that wins and generates a complete combat reward.");
        PrototypeStateInvariants.Validate(source);
    }

    private static void ValidateIndexRequestAgainstLaw(
        RandomIndexRequestV111 request, ChanceLawV111 law,
        string[]? candidateLabels)
    {
        if (candidateLabels is not null || law.SiteId != request.SiteId ||
            law.OwnerId != request.OwnerId || law.Ordinal != request.Ordinal ||
            law.OperationKind != request.OperationKind ||
            law.Outcomes.Length != request.SupportCount ||
            request.OperationKind == "choose_index" &&
                !request.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, request.SupportCount)))
            throw new InvalidDataException(
                "Combat reward index request differs from its Chance Law support.");
    }

    private static (PrototypeRoomType Room, int Ascension)
        ParseCombatGoldOwner(string ownerId)
    {
        var parts = ownerId.Split(':');
        if (parts.Length != 7 || parts[0] != "room" ||
            !Enum.TryParse<PrototypeRoomType>(parts[1], out var room) ||
            !int.TryParse(parts[3], out var ascension) ||
            parts[2] != "ascension" || parts[4] != "bounds" ||
            !int.TryParse(parts[5], out _) || !int.TryParse(parts[6], out _) ||
            !Enum.IsDefined(room))
            throw new InvalidDataException(
                "Combat gold request has a malformed owner.");
        return (room, ascension);
    }

    private static (int Act, int Offset, PrototypeNativeCardOddsSource Source)
        ParseEncounterCardRarityOwner(string ownerId)
    {
        var act = ParseOwnerInteger(ownerId, "act:", ":room:");
        var roomText = SegmentAfter(ownerId, ":room:", ":pick:");
        var offset = ParseOwnerInteger(ownerId, ":offset:", "");
        var source = roomText switch
        {
            "Combat" => PrototypeNativeCardOddsSource.RegularEncounter,
            "Elite" => PrototypeNativeCardOddsSource.EliteEncounter,
            "Boss" => PrototypeNativeCardOddsSource.BossEncounter,
            _ => throw new InvalidDataException(
                "Combat card-rarity owner has an unsupported room.")
        };
        return (act, offset, source);
    }

    private static (string CardId, int Act, int Ascension)
        ParseCardUpgradeOwner(string ownerId)
    {
        var act = ParseOwnerInteger(ownerId, ":act:", ":ascension:");
        var ascension = ParseOwnerInteger(ownerId, ":ascension:", "");
        if (!ownerId.StartsWith("card:", StringComparison.Ordinal))
            throw new InvalidDataException(
                "Combat card-upgrade owner has a malformed prefix.");
        return (ownerId[5..ownerId.IndexOf(":act:", StringComparison.Ordinal)],
            act, ascension);
    }

    private static int ParseOwnerInteger(
        string value, string start, string end)
    {
        var text = end.Length == 0
            ? value[(value.LastIndexOf(start, StringComparison.Ordinal) +
                start.Length)..]
            : SegmentAfter(value, start, end);
        if (!int.TryParse(text, out var result))
            throw new InvalidDataException(
                "Combat reward owner has a malformed numeric field.");
        return result;
    }

    private static string SegmentAfter(string value, string start, string end)
    {
        var startIndex = value.LastIndexOf(start, StringComparison.Ordinal);
        var endIndex = value.IndexOf(end,
            startIndex < 0 ? 0 : startIndex + start.Length,
            StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex + start.Length)
            throw new InvalidDataException(
                "Combat reward relic request owner has a malformed rarity segment.");
        return value[(startIndex + start.Length)..endIndex];
    }

    private static ControlledStochasticRequestV111 AsRequest(
        ControlledStochasticDecisionV111 decision) => decision.ToRequest();

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };

}
