using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureNativeEventRelicDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event relic decision.");
    }
}

public sealed record ReferenceNatureNativeEventRelicSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureNativeEventRelicDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    private ControlledStochasticDecisionV111[]? _providerDecisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(
            ProviderDecisions);
    private ReferenceNatureNativeEventRelicDecisionV111[]? _decisionTranscript =
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
    public ReferenceNatureNativeEventRelicDecisionV111[] DecisionTranscript
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
/// Atomic configured Native relic-grant continuation. Supported event
/// random-relic effects, Neow pickup triggers and card-bundle shuffles,
/// Arcane Scroll and Phial Holster acquisition draws, configured combat
/// reward relic pickups, event-specific relic rewards, Battleworn's
/// setting-three reward, Pael's Wing, and Toy Box pulls share one typed
/// ReferenceNature transcript.
/// </summary>
public sealed record ReferenceNatureNativeEventRelicSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureNativeEventRelicDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureRunActionMechanicsStateV111,
        ReferenceNatureControlledActionStochasticStateV111<
            ReferenceNatureNativeEventRelicDecisionV111>> Simulation { get; init; } =
        new(
            new ReferenceNatureRunActionMechanicsStateV111(
                Origin, Action, Run),
            new ReferenceNatureControlledActionStochasticStateV111<
                ReferenceNatureNativeEventRelicDecisionV111>(
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
        ReferenceNatureNativeEventRelicDecisionV111> Stochastic
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
    public ReferenceNatureNativeEventRelicDecisionV111[] DecisionTranscript
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
        "sts2-v111-reference-nature-native-event-relic-session-v1";
    private const int ToyBoxDrawCount = 5;
    private const string RelicRaritySiteId =
        StochasticSiteRegistryV111.RelicGrabBagRaritySiteId;
    private const string RelicModelSiteId =
        PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId;
    private const string UnownedRelicSiteId =
        StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId;
    private const string NeowPickupShuffleSiteId =
        StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId;
    private const string NeowCardBundleShuffleSiteId =
        StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId;
    private const string NeowLostCofferCardRaritySiteId =
        "reward.card_rarity.ticket";
    private const string NeowLostCofferCardModelSiteId =
        "reward.card_model.index_draw";
    private const string NeowLostCofferPotionModelSiteId =
        "event.reward.offer_potion.model.index_draw";
    private const string NeowRelicAcquisitionRareCardSiteId =
        "relic.acquisition.random_rare_card.model";
    private const string NeowRelicAcquisitionPotionSiteId =
        "relic.acquisition.random_potion.model";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Stochastic.PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => Stochastic.ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Stochastic.PendingRequest is null ? null :
        QueryLawForCapture(Origin, Action, Stochastic.PendingRequest, Stochastic.PendingCandidateLabels,
            Stochastic.ProviderDecisions);

    public static string[] SessionSiteIds =>
        [StochasticSiteRegistryV111.RelicGrabBagRaritySiteId,
            PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId,
            StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId,
            StochasticSiteRegistryV111.RandomPoolDistinctShuffleSiteId,
            StochasticSiteRegistryV111.EventRewardCardBundleShuffleSiteId,
            NeowLostCofferCardRaritySiteId,
            NeowLostCofferCardModelSiteId,
            NeowLostCofferPotionModelSiteId,
            NeowRelicAcquisitionRareCardSiteId,
            NeowRelicAcquisitionPotionSiteId,
            NativeGloryAncientsV111.BeautifulBraceletShuffleSiteId,
            NativeGloryAncientsV111.DistinguishedCapeCurseSiteId,
            NativePaelRewardsV111.LegionSkinSiteId,
            PrototypeNativeOrobasRelics.SandCastleAcquisitionShuffleSiteId];

    public static ReferenceNatureNativeEventRelicSessionV111 Begin(
        RunState source, GameAction action, ulong natureSeed) => Begin(
            source, action, ReferenceNatureRunCursorV111.Begin(
                source, natureSeed, "Native event relic"));

    public static ReferenceNatureNativeEventRelicSessionV111 Begin(
        RunState source, GameAction action) => Begin(
            source, action, ReferenceNatureRunCursorV111.Begin(
                source, null, "Native event relic"));

    private static ReferenceNatureNativeEventRelicSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, action);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Native event relic");

        var origin = ReferenceNatureRunProjectionV111
            .CreateNatureReplayOrigin(source, createRelicBagWhenMissing: true);
        var replay = Replay(origin, action, []);
        if (replay.Continuation is null)
            throw new NotSupportedException(
                "Native relic action did not reach a supported relic draw.");
        var session = new ReferenceNatureNativeEventRelicSessionV111(
            CurrentSchemaId, origin.Fork(), CloneAction(action), origin.Fork(),
            initialNatureState.Fork(), initialNatureState.Fork(), [], [],
            replay.Continuation.PendingRequest.Fork(),
            CloneLabels(replay.Continuation.PendingCandidateLabels));
        session.Validate();
        return session;
    }

    public ReferenceNatureNativeEventRelicSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureNativeEventRelicSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Native event relic session is already complete.");

        var law = QueryLawForCapture(Origin, Action, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels, Stochastic.ProviderDecisions);
        var selection = ReferenceNatureV111.SampleValidated(law, Stochastic.NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, Stochastic.PendingRequest!, selection, Origin, Stochastic.PendingCandidateLabels);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            Stochastic.ProviderDecisions, Stochastic.PendingRequest!, Stochastic.PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            CreateNatureReplayOrigin(Origin), continuation, providerDecision,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State,
            request => RequireSessionRequest(Origin, Action, request));
        var providerDecisions = replay.Continuation?.Decisions ??
            Stochastic.ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = Stochastic.DecisionTranscript.Append(
            new ReferenceNatureNativeEventRelicDecisionV111(
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
            PendingCandidateLabels = CloneLabels(
                replay.Continuation?.PendingCandidateLabels)
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
                "Invalid ReferenceNature Native event relic session envelope.");
        Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction);
        Stochastic.Validate();
        if (Stochastic.DecisionTranscript.Length >
                ExpectedRelicBagDrawCount(Origin, Action) * 2 +
                    ExpectedNeowUnownedRelicRequestCount(Origin, Action) +
                    ExpectedPunchOffUnownedRelicRequestCount(
                        Origin, Action) +
                    ExpectedNeowPickupShuffleRequestCount(
                        Origin, Action) +
                    ExpectedNeowCardBundleShuffleRequestCount(
                        Origin, Action) +
                    ExpectedNeowLostCofferRequestCount(
                        Origin, Action) +
                    ExpectedNeowRelicAcquisitionModelRequestCount(
                        Origin, Action) +
                    ExpectedCombatRewardRelicAcquisitionRequestCount(
                        Origin, Action))
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event relic session envelope.");

        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native event relic session origin");
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native event relic session replay");
        Stochastic.InitialNatureState.Validate();
        Stochastic.NatureState.Validate();
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, Stochastic.InitialNatureState, "Native event relic");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var prefix = Stochastic.ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var request = replayAtBoundary.Continuation?.PendingRequest ??
                throw new InvalidDataException(
                    "Native event relic replay completed before its transcript.");
            var labels = replayAtBoundary.Continuation!.PendingCandidateLabels;
            var savedDecision = Stochastic.ProviderDecisions[index];
            savedDecision.Validate();
            if (!ControlledChanceContinuationExecutorV111.RequestsMatch(
                    request, AsRequest(savedDecision)) ||
                !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                    labels, savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "Native event relic provider transcript changed its request.");

            var law = QueryLawForCapture(Origin, Action, request, labels, prefix);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedTranscript = new ReferenceNatureNativeEventRelicDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "Native event relic law decisions do not replay.");
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, Action, Stochastic.ProviderDecisions);
        var expectedRelicBagDrawCount =
            ExpectedRelicBagDrawCount(Origin, Action);
        var expectedNeowUnownedRelicRequestCount =
            ExpectedNeowUnownedRelicRequestCount(Origin, Action);
        var expectedPunchOffUnownedRelicRequestCount =
            ExpectedPunchOffUnownedRelicRequestCount(Origin, Action);
        var expectedNeowPickupShuffleRequestCount =
            ExpectedNeowPickupShuffleRequestCount(Origin, Action);
        var expectedNeowCardBundleShuffleRequestCount =
            ExpectedNeowCardBundleShuffleRequestCount(Origin, Action);
        var neowLostCofferPlan = NeowLostCofferPlan(Origin, Action);
        var expectedNeowLostCofferCardPickCount =
            neowLostCofferPlan?.CardPickCount ?? 0;
        var expectedNeowLostCofferPotionRequestCount =
            neowLostCofferPlan?.OffersPotion == true ? 1 : 0;
        var expectedNeowRelicAcquisitionRareCardRequestCount =
            ExpectedNeowRelicAcquisitionModelRequestCount(
                Origin, Action, NeowRelicAcquisitionRareCardSiteId);
        var expectedNeowRelicAcquisitionPotionRequestCount =
            ExpectedNeowRelicAcquisitionModelRequestCount(
                Origin, Action, NeowRelicAcquisitionPotionSiteId);
        var rarityRequestCount = Stochastic.ProviderDecisions.Count(decision =>
            decision.UnitFloatDecision?.Request.SiteId == RelicRaritySiteId);
        var identityRequestCount = Stochastic.ProviderDecisions.Count(decision =>
            decision.IndexDecision?.Request.SiteId == RelicModelSiteId);
        var unownedRelicRequestCount = Stochastic.ProviderDecisions.Count(decision =>
            decision.IndexDecision?.Request.SiteId == UnownedRelicSiteId);
        var neowPickupShuffleRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowPickupShuffleSiteId);
        var neowCardBundleShuffleRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowCardBundleShuffleSiteId);
        var neowLostCofferCardRarityRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowLostCofferCardRaritySiteId);
        var neowLostCofferCardModelRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowLostCofferCardModelSiteId);
        var neowLostCofferPotionModelRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowLostCofferPotionModelSiteId);
        var neowRelicAcquisitionRareCardRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowRelicAcquisitionRareCardSiteId);
        var neowRelicAcquisitionPotionRequestCount = Stochastic.ProviderDecisions.Count(
            decision => decision.IndexDecision?.Request.SiteId ==
                NeowRelicAcquisitionPotionSiteId);
        var combatRewardRelicAcquisitionRequestCount =
            IsCombatRewardRelicAcquisitionAction(Origin, Action)
                ? Stochastic.ProviderDecisions.Count(decision =>
                    StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(
                        decision.IndexDecision?.Request.SiteId ??
                            string.Empty))
                : 0;
        if ((replay.Continuation is null) != Complete ||
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
                replay.Continuation?.PendingRequest, Stochastic.PendingRequest) ||
            !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                replay.Continuation?.PendingCandidateLabels,
                Stochastic.PendingCandidateLabels) ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState) ||
            rarityRequestCount > expectedRelicBagDrawCount ||
            Complete && rarityRequestCount != expectedRelicBagDrawCount ||
            identityRequestCount > rarityRequestCount ||
            unownedRelicRequestCount >
                expectedNeowUnownedRelicRequestCount +
                    expectedPunchOffUnownedRelicRequestCount ||
            Complete && unownedRelicRequestCount !=
                expectedNeowUnownedRelicRequestCount +
                    expectedPunchOffUnownedRelicRequestCount ||
            neowPickupShuffleRequestCount >
                expectedNeowPickupShuffleRequestCount ||
            Complete && neowPickupShuffleRequestCount !=
                expectedNeowPickupShuffleRequestCount ||
            neowCardBundleShuffleRequestCount >
                expectedNeowCardBundleShuffleRequestCount ||
            Complete && neowCardBundleShuffleRequestCount !=
                expectedNeowCardBundleShuffleRequestCount ||
            neowLostCofferCardRarityRequestCount >
                expectedNeowLostCofferCardPickCount ||
            Complete && neowLostCofferCardRarityRequestCount !=
                expectedNeowLostCofferCardPickCount ||
            neowLostCofferCardModelRequestCount >
                expectedNeowLostCofferCardPickCount ||
            Complete && neowLostCofferCardModelRequestCount !=
                expectedNeowLostCofferCardPickCount ||
            neowLostCofferPotionModelRequestCount >
                expectedNeowLostCofferPotionRequestCount ||
            Complete && neowLostCofferPotionModelRequestCount !=
                expectedNeowLostCofferPotionRequestCount ||
            neowRelicAcquisitionRareCardRequestCount >
                expectedNeowRelicAcquisitionRareCardRequestCount ||
            Complete && neowRelicAcquisitionRareCardRequestCount !=
                expectedNeowRelicAcquisitionRareCardRequestCount ||
            neowRelicAcquisitionPotionRequestCount >
                expectedNeowRelicAcquisitionPotionRequestCount ||
            Complete && neowRelicAcquisitionPotionRequestCount !=
                expectedNeowRelicAcquisitionPotionRequestCount ||
            combatRewardRelicAcquisitionRequestCount >
                ExpectedCombatRewardRelicAcquisitionRequestCount(
                    Origin, Action) ||
            Complete && combatRewardRelicAcquisitionRequestCount !=
                ExpectedCombatRewardRelicAcquisitionRequestCount(
                    Origin, Action) ||
            Stochastic.ProviderDecisions.Length != rarityRequestCount +
                identityRequestCount + unownedRelicRequestCount +
                neowPickupShuffleRequestCount +
                neowCardBundleShuffleRequestCount +
                neowLostCofferCardRarityRequestCount +
                neowLostCofferCardModelRequestCount +
                neowLostCofferPotionModelRequestCount +
                neowRelicAcquisitionRareCardRequestCount +
                neowRelicAcquisitionPotionRequestCount +
                combatRewardRelicAcquisitionRequestCount)
            throw new InvalidDataException(
                "Native event relic continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState, Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "Native event relic run differs from its atomic Nature cursor.");
        foreach (var decision in Stochastic.DecisionTranscript)
            decision.Validate();
    }

    public static string Save(
        ReferenceNatureNativeEventRelicSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureNativeEventRelicSessionSnapshotV111(
                CurrentSchemaId, RunSnapshot.Save(session.Origin),
                CloneAction(session.Action), RunSnapshot.Save(session.Run),
                session.Stochastic.InitialNatureState, session.Stochastic.NatureState,
                session.Stochastic.ProviderDecisions.Select(
                    decision => decision.Fork()).ToArray(),
                (ReferenceNatureNativeEventRelicDecisionV111[])
                    session.Stochastic.DecisionTranscript.Clone(),
                session.Stochastic.PendingRequest?.Fork(),
                CloneLabels(session.Stochastic.PendingCandidateLabels)));
    }

    public static ReferenceNatureNativeEventRelicSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureNativeEventRelicSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature Native event relic snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.ProviderDecisions is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Native event relic snapshot.");
        var session = new ReferenceNatureNativeEventRelicSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action), RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState.Fork(), snapshot.NatureState.Fork(),
            snapshot.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureNativeEventRelicDecisionV111[])
                snapshot.DecisionTranscript.Clone(),
            snapshot.PendingRequest?.Fork(),
            CloneLabels(snapshot.PendingCandidateLabels));
        session.Validate();
        return session;
    }

    internal static ChanceLawV111 QueryLawForCapture(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request, string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        request.Validate();
        if (request.UnitFloatRequest is { } floatRequest &&
            floatRequest.SiteId == RelicRaritySiteId)
        {
            if (floatRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                floatRequest.OwnerId != PrototypeNativeRelicGrabBag
                    .NativeTreasureRarityOwnerId || candidateLabels is not null)
                throw new InvalidDataException(
                    "Native event relic rarity request differs from the declared draw mechanics.");
            return ChanceLawEngineV111.QueryNativeRelicRarity(floatRequest);
        }

        if (request.IndexRequest is { } unownedRelicRequest &&
            unownedRelicRequest.SiteId == UnownedRelicSiteId)
        {
            var expected = UnownedRelicSupport(source, action);
            if (expected is null ||
                unownedRelicRequest.StreamId !=
                    NativePlayerRngV111.RewardsStreamId ||
                unownedRelicRequest.OperationKind != "choose_index" ||
                unownedRelicRequest.SupportCount !=
                    expected.Value.CandidateIds.Length ||
                !unownedRelicRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0,
                        expected.Value.CandidateIds.Length)) ||
                unownedRelicRequest.OwnerId != expected.Value.OwnerId ||
                candidateLabels is not null && !candidateLabels.SequenceEqual(
                    expected.Value.CandidateIds, StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Native event unowned-relic support differs from its saved action.");

            var isPunchOffOffer =
                ExpectedPunchOffUnownedRelicRequestCount(source, action) > 0;
            var lawEventId = isPunchOffOffer
                ? CanonicalJson.Sha256(new
                {
                    boundary = "native-punch-off-unowned-relic-offer",
                    eventId = PrototypeNativePunchOff.EventId,
                    actionId = PrototypeAiEnvironment.StableActionId(action),
                    request = unownedRelicRequest
                })
                : CanonicalJson.Sha256(new
                {
                    boundary = "native-neow-small-capsule-relic",
                    eventId = PrototypeNativeOvergrowthEvents.NeowEventId,
                    actionId = PrototypeAiEnvironment.StableActionId(action),
                    request = unownedRelicRequest
                });
            return ChanceLawEngineV111.QueryRandomIndexWithCandidateOutcomeIds(
                lawEventId, unownedRelicRequest, candidateLabels);
        }

        if (request.IndexRequest is { } neowPickupShuffleRequest &&
            neowPickupShuffleRequest.SiteId == NeowPickupShuffleSiteId)
        {
            var expected = NeowPickupShuffleBoundary(
                source, action, prefix);
            if (expected is null || candidateLabels is not null ||
                neowPickupShuffleRequest.StreamId != "reward" ||
                neowPickupShuffleRequest.OperationKind != "bounded_integer" ||
                neowPickupShuffleRequest.SupportCount !=
                    expected.Value.SupportCount ||
                neowPickupShuffleRequest.OrderedCandidates.Length != 0 ||
                neowPickupShuffleRequest.OwnerId != expected.Value.OwnerId)
                throw new InvalidDataException(
                    "Neow pickup shuffle differs from its current ordered pool.");

            return ChanceLawEngineV111.QueryRandomIndex(
                CanonicalJson.Sha256(new
                {
                    boundary = expected.Value.BoundaryId,
                    eventId = PrototypeNativeOvergrowthEvents.NeowEventId,
                    actionId = PrototypeAiEnvironment.StableActionId(action),
                    prefixFingerprint = CanonicalJson.Sha256(prefix),
                    request = neowPickupShuffleRequest
                }), neowPickupShuffleRequest);
        }

        if (request.IndexRequest is { } neowCardBundleShuffleRequest &&
            neowCardBundleShuffleRequest.SiteId == NeowCardBundleShuffleSiteId)
        {
            var expected = NeowCardBundleShuffleBoundary(
                source, action, prefix);
            if (expected is null || candidateLabels is not null ||
                neowCardBundleShuffleRequest.StreamId != "reward" ||
                neowCardBundleShuffleRequest.OperationKind !=
                    "bounded_integer" ||
                neowCardBundleShuffleRequest.SupportCount !=
                    expected.Value.SupportCount ||
                neowCardBundleShuffleRequest.OrderedCandidates.Length != 0 ||
                neowCardBundleShuffleRequest.OwnerId != expected.Value.OwnerId)
                throw new InvalidDataException(
                    "Neow Scroll Boxes shuffle differs from its current ordered pool.");

            return ChanceLawEngineV111.QueryRandomIndex(
                CanonicalJson.Sha256(new
                {
                    boundary = expected.Value.BoundaryId,
                    eventId = PrototypeNativeOvergrowthEvents.NeowEventId,
                    actionId = PrototypeAiEnvironment.StableActionId(action),
                    prefixFingerprint = CanonicalJson.Sha256(prefix),
                    request = neowCardBundleShuffleRequest
                }), neowCardBundleShuffleRequest);
        }

        if (request.IndexRequest is { } neowLostCofferRequest &&
            IsNeowLostCofferRequestSite(neowLostCofferRequest.SiteId))
        {
            var boundaryId = NeowLostCofferRequestBoundary(
                source, action, neowLostCofferRequest, candidateLabels, prefix);
            if (boundaryId is null)
                throw new InvalidDataException(
                    "Neow Lost Coffer request differs from its current reward generation.");

            return ChanceLawEngineV111.QueryRandomIndexWithCandidateOutcomeIds(
                CanonicalJson.Sha256(new
                {
                    boundary = boundaryId,
                    eventId = PrototypeNativeOvergrowthEvents.NeowEventId,
                    actionId = PrototypeAiEnvironment.StableActionId(action),
                    prefixFingerprint = CanonicalJson.Sha256(prefix),
                    request = neowLostCofferRequest
                }), neowLostCofferRequest, candidateLabels);
        }

        if (request.IndexRequest is { } neowRelicAcquisitionRequest &&
            IsNeowRelicAcquisitionModelSite(
                neowRelicAcquisitionRequest.SiteId))
        {
            var boundaryId = NeowRelicAcquisitionRequestBoundary(
                source, action, neowRelicAcquisitionRequest,
                candidateLabels, prefix);
            if (boundaryId is null)
                throw new InvalidDataException(
                    "Neow relic-acquisition model request differs from its saved pickup.");

            var eventId = CanonicalJson.Sha256(new
            {
                boundary = boundaryId,
                eventId = PrototypeNativeOvergrowthEvents.NeowEventId,
                actionId = PrototypeAiEnvironment.StableActionId(action),
                prefixFingerprint = CanonicalJson.Sha256(prefix),
                request = neowRelicAcquisitionRequest
            });
            return ChanceLawEngineV111.QueryRandomIndexWithCandidateOutcomeIds(
                eventId, neowRelicAcquisitionRequest, candidateLabels);
        }

        if (request.IndexRequest is { } indexRequest &&
            indexRequest.SiteId == RelicModelSiteId)
        {
            if (source.World is not { } world || candidateLabels is null ||
                indexRequest.StreamId != NativePlayerRngV111.RewardsStreamId ||
                indexRequest.OperationKind != "choose_index" ||
                indexRequest.SupportCount != candidateLabels.Length ||
                !indexRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, candidateLabels.Length)) ||
                !candidateLabels.SequenceEqual(candidateLabels.OrderBy(
                    id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Native event relic identity request is malformed.");

            var rarity = ParseRarity(indexRequest.OwnerId);
            var bag = BagAfterPrefix(source, prefix);
            var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                bag, rarity, merchant: false, source.Player);
            var expectedCandidates = plan.CandidateIds;
            var expectedOwner = PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                world, rarity, expectedCandidates);
            if (plan.SelectedTier is null ||
                !candidateLabels.SequenceEqual(expectedCandidates,
                    StringComparer.Ordinal) || indexRequest.OwnerId != expectedOwner)
                throw new InvalidDataException(
                    "Native event relic identities differ from the canonical remaining relic bag.");

            var eventId = source.World?.Event?.EventId;
            var legacyToyBoxBoundary = eventId ==
                    NativeAncientRewardsV111.TezcataraId &&
                EventChoiceId(action) is { } choiceId &&
                HasToyBoxEffect(eventId, choiceId);
            var lawEventId = legacyToyBoxBoundary
                ? CanonicalJson.Sha256(new
                {
                    boundary = "native-event-toy-box-relic",
                    eventId = NativeAncientRewardsV111.TezcataraId,
                    choiceId = EventChoiceId(action),
                    request = indexRequest
                })
                : action.Kind == "event_choice"
                    ? CanonicalJson.Sha256(new
                    {
                        boundary = "native-event-relic",
                        eventId,
                        choiceId = EventChoiceId(action),
                        request = indexRequest
                    })
                    : IsBattlewornSettingThreeCombat(source)
                    ? CanonicalJson.Sha256(new
                    {
                        boundary = "native-battleworn-relic-reward",
                        eventId,
                        actionId = PrototypeAiEnvironment.StableActionId(action),
                        request = indexRequest
                    })
                    : IsPaelWingRelicReward(source, action)
                    ? CanonicalJson.Sha256(new
                    {
                        boundary = "native-pael-wing-relic-reward",
                        actionId = PrototypeAiEnvironment.StableActionId(action),
                        request = indexRequest
                    })
                    : CanonicalJson.Sha256(new
                    {
                        boundary = "native-neow-relic-pickup",
                        eventId,
                        actionId = PrototypeAiEnvironment.StableActionId(action),
                        request = indexRequest
                    });
            return ChanceLawEngineV111.QueryRandomIndexWithCandidateOutcomeIds(
                lawEventId, indexRequest, candidateLabels);
        }

        if (IsCombatRewardRelicAcquisitionAction(source, action) &&
            request.IndexRequest is { } acquisitionRequest &&
            IsSupportedCombatRewardRelicAcquisitionRequest(
                source, action, acquisitionRequest))
        {
            var legionSkinRequest = acquisitionRequest.SiteId ==
                NativePaelRewardsV111.LegionSkinSiteId;
            var labelsValid = legionSkinRequest
                ? candidateLabels is null &&
                    acquisitionRequest.StreamId == "reward" &&
                    acquisitionRequest.Ordinal == 0 &&
                    acquisitionRequest.SupportCount ==
                        NativePaelRewardsV111.LegionSkins.Length &&
                    acquisitionRequest.OrderedCandidates.SequenceEqual(
                        Enumerable.Range(0,
                            NativePaelRewardsV111.LegionSkins.Length)) &&
                    acquisitionRequest.OwnerId ==
                        $"relic:paels_legion:instance:{source.Player.Relics.Length}"
                : candidateLabels is not null &&
                    candidateLabels.Length ==
                        acquisitionRequest.SupportCount &&
                    candidateLabels.All(label =>
                        !string.IsNullOrWhiteSpace(label)) &&
                    candidateLabels.Distinct(StringComparer.Ordinal).Count() ==
                        candidateLabels.Length;
            if (!ControlledRunActionEngineV111
                    .IsSupportedShopPurchaseStream(acquisitionRequest) ||
                !labelsValid)
                throw new InvalidDataException(
                    "Combat reward relic acquisition request differs from its registered source support.");

            var eventId = CanonicalJson.Sha256(new
            {
                boundary = "native-combat-reward-relic-acquisition",
                relicId = SelectedCombatRewardRelicId(source, action),
                actionId = PrototypeAiEnvironment.StableActionId(action),
                mechanicsBoundary =
                    SimulationMechanicsProjectionEngineV111.Fingerprint(source),
                request = acquisitionRequest,
                candidateLabels,
                prefixFingerprint = CanonicalJson.Sha256(prefix)
            });
            return ChanceLawEngineV111.QueryRandomIndexWithCandidateOutcomeIds(
                eventId, acquisitionRequest, candidateLabels);
        }

        throw new NotSupportedException(
            $"Native event relic replay does not support stochastic site '{request.SiteId}'.");
    }

    private static PrototypeRelicBagState BagAfterPrefix(
        RunState source, ControlledStochasticDecisionV111[] prefix)
    {
        var bag = source.World?.Stochastic.RelicBag ??
            throw new InvalidDataException(
                "Native event relic replay lost its canonical relic bag.");
        foreach (var decision in prefix.Where(decision =>
            decision.IndexDecision?.Request.SiteId == RelicModelSiteId))
        {
            var indexDecision = decision.IndexDecision!;
            var labels = decision.CandidateLabels ?? throw new InvalidDataException(
                "Native event relic prefix lost its selected identity.");
            var rarity = ParseRarity(indexDecision.Request.OwnerId);
            if (indexDecision.SelectedIndex >= labels.Length)
                throw new InvalidDataException(
                    "Native event relic prefix selected outside its candidate labels.");
            var plan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                bag, rarity, merchant: false, source.Player);
            var expected = plan.CandidateIds;
            if (plan.SelectedTier is null ||
                !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    labels, expected) ||
                indexDecision.Request.OwnerId !=
                    PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                        source.World!, rarity, expected))
                throw new InvalidDataException(
                    "Native event relic prefix differs from its canonical bag draw.");
            (bag, _) = PrototypeNativeRelicGrabBag.PullNatureSelected(
                bag, rarity, labels, indexDecision.SelectedIndex,
                merchant: false, source.Player);
        }
        return bag;
    }

    private static PrototypeRelicRarity ParseRarity(string ownerId)
    {
        var start = ownerId.LastIndexOf(":rarity:", StringComparison.Ordinal);
        var end = ownerId.IndexOf(":pool:",
            start < 0 ? 0 : start + ":rarity:".Length,
            StringComparison.Ordinal);
        if (start < 0 || end <= start + ":rarity:".Length ||
            !Enum.TryParse<PrototypeRelicRarity>(
                ownerId[(start + ":rarity:".Length)..end], false,
                out var rarity) || !Enum.IsDefined(rarity))
            throw new InvalidDataException(
                "Native event relic request owner has a malformed rarity.");
        return rarity;
    }

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, GameAction action,
        ControlledStochasticDecisionV111[] decisions) =>
        ControlledChanceContinuationExecutorV111.CaptureStochastic(
            CreateNatureReplayOrigin(source),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State,
            request => RequireSessionRequest(source, action, request));

    private static RunState CreateNatureReplayOrigin(RunState source) =>
        ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
            source, createRelicBagWhenMissing: true);

    private static bool IsSessionRequest(
        ControlledStochasticRequestV111 request) =>
        request.SiteId is RelicRaritySiteId or RelicModelSiteId or
            UnownedRelicSiteId or NeowPickupShuffleSiteId or
            NeowCardBundleShuffleSiteId or NeowLostCofferCardRaritySiteId or
            NeowLostCofferCardModelSiteId or NeowLostCofferPotionModelSiteId or
            NeowRelicAcquisitionRareCardSiteId or
            NeowRelicAcquisitionPotionSiteId;

    private static bool RequireSessionRequest(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request)
    {
        if (IsSessionRequest(request) ||
            IsCombatRewardRelicAcquisitionAction(source, action) &&
            request.IndexRequest is { } acquisitionRequest &&
            IsSupportedCombatRewardRelicAcquisitionRequest(
                source, action, acquisitionRequest) &&
            ControlledRunActionEngineV111.IsSupportedShopPurchaseStream(
                acquisitionRequest))
            return true;
        throw new NotSupportedException(
            $"Native event relic replay does not support stochastic site '{request.SiteId}'.");
    }

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        var world = source.World;
        var eventState = world?.Event;
        var choiceId = EventChoiceId(action);
        var nativeDirectEventRelicDrawCount =
            NativeDirectEventRelicDrawCount(source, action);
        var controlledEventRelicChoice = eventState is not null &&
            choiceId is not null &&
            PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId) &&
            ExpectedEventRelicDrawCount(source, action) +
                ExpectedNeowUnownedRelicRequestCount(source, action) +
                ExpectedNeowPickupShuffleRequestCount(
                    source, action) +
                ExpectedNeowCardBundleShuffleRequestCount(
                    source, action) +
                ExpectedNeowLostCofferRequestCount(source, action) +
                ExpectedNeowRelicAcquisitionModelRequestCount(
                    source, action) > 0 &&
            (!HasUnsupportedFixedRelicEffect(eventState.EventId, choiceId) ||
             ExpectedNeowUnownedRelicRequestCount(source, action) > 0 ||
             ExpectedNeowPickupShuffleRequestCount(source, action) > 0 ||
             ExpectedNeowCardBundleShuffleRequestCount(source, action) > 0 ||
             ExpectedNeowLostCofferRequestCount(source, action) > 0 ||
             ExpectedNeowRelicAcquisitionModelRequestCount(
                 source, action) > 0);
        var punchOffUnownedRelicOffer =
            ExpectedPunchOffUnownedRelicRequestCount(source, action) > 0;
        var eventRelicChoice = source.Phase == RunPhase.Event &&
            action.Kind == "event_choice" &&
            eventState is not null && choiceId is not null &&
            (nativeDirectEventRelicDrawCount > 0 ||
                controlledEventRelicChoice || punchOffUnownedRelicOffer);
        var neowRelicPickup = NeowRelicPickupDrawCount(source, action) > 0 ||
            ExpectedNeowUnownedRelicRequestCount(source, action) > 0 ||
            ExpectedNeowPickupShuffleRequestCount(source, action) > 0 ||
            ExpectedNeowCardBundleShuffleRequestCount(source, action) > 0 ||
            ExpectedNeowLostCofferRequestCount(source, action) > 0 ||
            ExpectedNeowRelicAcquisitionModelRequestCount(
                source, action) > 0;
        var battlewornRelicVictory =
            IsBattlewornSettingThreeCombat(source);
        var paelWingRelicReward = IsPaelWingRelicReward(source, action);
        var combatRewardRelicAcquisition =
            IsCombatRewardRelicAcquisitionAction(source, action);
        var supportedBoundary =
            (eventRelicChoice && eventState is
                { ControlledChoice: null, ChosenChoiceId: null }) ||
            neowRelicPickup || battlewornRelicVictory ||
            paelWingRelicReward || combatRewardRelicAcquisition;
        if (world is null || source.Stochastic.NativeRunRng is null ||
            source.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !supportedBoundary ||
            !new PrototypeGameEngine().GetLegalActions(source).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Native relic session requires a current legal configured Native event choice, relic pickup, Battleworn victory action, or Pael's Wing reward sacrifice.");
        PrototypeStateInvariants.Validate(source);
    }

    private static int ExpectedCombatRewardRelicAcquisitionRequestCount(
        RunState source, GameAction action)
    {
        var relicId = SelectedCombatRewardRelicId(source, action);
        if (relicId is null)
            return 0;

        if (relicId == NativeGloryAncientsV111.BeautifulBraceletId)
            return Math.Max(0, source.Player.Deck.Count(card =>
                PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Enchant,
                    null, PrototypeCardEnchantmentKind.Swift, false)) - 1);
        if (relicId == NativeGloryAncientsV111.DistinguishedCapeId)
            return 2;
        if (relicId == NativePaelRewardsV111.LegionId)
            return 1;
        if (relicId == PrototypeNativeOrobasRelics.SandCastleId)
            return Math.Max(0, source.Player.Deck.Count(card =>
                PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Upgrade,
                    null, null, false)) - 1);

        return 0;
    }

    private static bool IsCombatRewardRelicAcquisitionAction(
        RunState source, GameAction action) =>
        ExpectedCombatRewardRelicAcquisitionRequestCount(source, action) > 0;

    private static bool IsSupportedCombatRewardRelicAcquisitionRequest(
        RunState source, GameAction action,
        RandomIndexRequestV111 request)
    {
        var relicId = SelectedCombatRewardRelicId(source, action);
        return relicId switch
        {
            NativeGloryAncientsV111.BeautifulBraceletId =>
                request.SiteId == NativeGloryAncientsV111
                    .BeautifulBraceletShuffleSiteId,
            NativeGloryAncientsV111.DistinguishedCapeId =>
                request.SiteId == NativeGloryAncientsV111
                    .DistinguishedCapeCurseSiteId,
            NativePaelRewardsV111.LegionId =>
                request.SiteId == NativePaelRewardsV111.LegionSkinSiteId,
            PrototypeNativeOrobasRelics.SandCastleId =>
                request.SiteId ==
                    PrototypeNativeOrobasRelics
                        .SandCastleAcquisitionShuffleSiteId,
            _ => false
        };
    }

    private static string? SelectedCombatRewardRelicId(
        RunState source, GameAction action)
    {
        if (source.Phase != RunPhase.Reward ||
            source.Stochastic.NativeRunRng is null || source.World is not { } world ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            world.Reward is not
                { SourceRoom: "Combat" or "Elite" or "Boss",
                    PendingDeckChoice: null } reward)
            return null;

        var payload = action.Payload.Deserialize<ChooseRelicPayload>();
        if (payload is null)
            return null;

        if (action.Kind == "take_reward_relic")
        {
            if (!reward.RelicResolved)
            {
                var options = reward.CurrentRelicOptions;
                return (uint)payload.Index < (uint)options.Length
                    ? options[payload.Index]
                    : null;
            }

            var groupIndex = reward.FirstPendingExtraRelicGroupIndex;
            return payload.Index == 0 && groupIndex >= 0 &&
                groupIndex < (reward.ExtraRelicRewardIds?.Length ?? 0) &&
                !reward.IsExtraRelicGroupResolved(groupIndex)
                    ? reward.ExtraRelicRewardIds![groupIndex]
                    : null;
        }

        if (action.Kind == "take_reward_extra_relic")
        {
            var groupIndex = payload.Index;
            return groupIndex >= 0 &&
                groupIndex < (reward.ExtraRelicRewardIds?.Length ?? 0) &&
                !reward.IsExtraRelicGroupResolved(groupIndex)
                    ? reward.ExtraRelicRewardIds![groupIndex]
                    : null;
        }

        return null;
    }

    private static int ExpectedRelicBagDrawCount(
        RunState source, GameAction action)
    {
        if (action.Kind != "event_choice")
        {
            if (IsBattlewornSettingThreeCombat(source))
                return 1;
            return NeowRelicPickupDrawCount(source, action) +
                PaelWingRelicDrawCount(source, action);
        }
        return ExpectedEventRelicDrawCount(source, action);
    }

    private static int ExpectedNeowUnownedRelicRequestCount(
        RunState source, GameAction action)
    {
        var effects = NeowPickupEffects(source, action);
        return effects is { Length: 2 } &&
            effects[0].Kind == PrototypeRunEffectKind.GainRelic &&
            StringComparer.Ordinal.Equals(effects[0].RelicId,
                PrototypeNativeOvergrowthEvents.NeowRelicId("SmallCapsule")) &&
            effects[1].Kind == PrototypeRunEffectKind.OfferRandomRelic
                ? 1
                : 0;
    }

    private static int ExpectedPunchOffUnownedRelicRequestCount(
        RunState source, GameAction action)
    {
        var world = source.World;
        var eventState = world?.Event;
        if (source.Phase != RunPhase.Event || action.Kind != "event_choice" ||
            eventState is null ||
            eventState.EventId != PrototypeNativePunchOff.EventId ||
            eventState.NativePageIndex != 0 ||
            EventChoiceId(action) != "nab" || world is null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            return 0;

        var ownedRelics = source.Player.Relics.Select(relic => relic.RelicId)
            .ToHashSet(StringComparer.Ordinal);
        return PrototypeContent.RelicPool.Any(id =>
            !ownedRelics.Contains(id)) ? 1 : 0;
    }

    private static int ExpectedNeowPickupShuffleRequestCount(
        RunState source, GameAction action)
    {
        var plan = NeowPickupShufflePlan(source, action);
        if (plan is null || plan.Value.Pool.Length < 2)
            return 0;
        return plan.Value.Pool.Length - 1;
    }

    private static int ExpectedNeowCardBundleShuffleRequestCount(
        RunState source, GameAction action)
    {
        var pools = NeowCardBundleShufflePools(source, action);
        if (pools is null)
            return 0;
        return pools.Value.Commons.Length - 1 +
            pools.Value.Uncommons.Length - 1;
    }

    private static int ExpectedNeowLostCofferRequestCount(
        RunState source, GameAction action)
    {
        var plan = NeowLostCofferPlan(source, action);
        return plan is null
            ? 0
            : plan.Value.CardPickCount * 2 +
                (plan.Value.OffersPotion ? 1 : 0);
    }

    private static int ExpectedNeowRelicAcquisitionModelRequestCount(
        RunState source, GameAction action) =>
        ExpectedNeowRelicAcquisitionModelRequestCount(
            source, action, NeowRelicAcquisitionRareCardSiteId) +
        ExpectedNeowRelicAcquisitionModelRequestCount(
            source, action, NeowRelicAcquisitionPotionSiteId);

    private static int ExpectedNeowRelicAcquisitionModelRequestCount(
        RunState source, GameAction action, string siteId)
    {
        if (!IsNeowRelicAcquisitionModelSite(siteId))
            return 0;
        return NeowRelicAcquisitionEffects(source, action).Count(effect =>
            siteId switch
            {
                NeowRelicAcquisitionRareCardSiteId =>
                    effect.Kind == PrototypeRunEffectKind.GainRandomRareCard,
                NeowRelicAcquisitionPotionSiteId =>
                    effect.Kind == PrototypeRunEffectKind.GainRandomPotion,
                _ => false
            });
    }

    private static PrototypeRunEffectSpec[] NeowRelicAcquisitionEffects(
        RunState source, GameAction action)
    {
        var pickupEffects = NeowPickupEffects(source, action);
        if (pickupEffects is null)
            return [];
        var acquiredRelicIds = pickupEffects.Where(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic)
            .Select(effect => effect.RelicId)
            .ToArray();
        if (acquiredRelicIds.Length != 1)
            return [];
        var relicId = acquiredRelicIds[0];
        if (relicId is null ||
            !PrototypeContent.Relics.TryGetValue(relicId, out var definition))
            return [];
        return (definition.RunTriggers ??
                Array.Empty<PrototypeRelicRunTriggerSpec>())
            .Where(trigger => trigger.EventKind ==
                PrototypeRunEventKind.RelicAcquired)
            .SelectMany(trigger => trigger.Effects)
            .ToArray();
    }

    private static bool IsNeowRelicAcquisitionModelSite(string siteId) =>
        siteId is NeowRelicAcquisitionRareCardSiteId or
            NeowRelicAcquisitionPotionSiteId;

    private static string? NeowRelicAcquisitionRequestBoundary(
        RunState source, GameAction action, RandomIndexRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        var expectedCount = ExpectedNeowRelicAcquisitionModelRequestCount(
            source, action, request.SiteId);
        var priorCount = prefix.Count(decision =>
            decision.IndexDecision?.Request.SiteId == request.SiteId);
        if (expectedCount == 0 || priorCount >= expectedCount ||
            request.StreamId != NativePlayerRngV111.RewardsStreamId ||
            request.OperationKind != "choose_index")
            return null;

        var pool = request.SiteId == NeowRelicAcquisitionRareCardSiteId
            ? PrototypeContent.RewardCardPool.Where(cardId =>
                PrototypeContent.Card(cardId).Rarity ==
                PrototypeCardRarity.Rare).ToArray()
            : PrototypeContent.PotionPool;
        if (pool.Length == 0 || request.SupportCount != pool.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, pool.Length)))
            throw new InvalidDataException(
                "Neow relic-acquisition model support differs from its ordered pool.");

        string expectedOwner;
        if (request.SiteId == NeowRelicAcquisitionRareCardSiteId)
        {
            expectedOwner = "player:rare_pool:" + CanonicalJson.Sha256(pool);
        }
        else
        {
            var acquisitionEffects = NeowRelicAcquisitionEffects(
                source, action);
            var addedSlots = acquisitionEffects
                .TakeWhile(effect => effect.Kind !=
                    PrototypeRunEffectKind.GainRandomPotion)
                .Where(effect => effect.Kind ==
                    PrototypeRunEffectKind.GainPotionSlots)
                .Sum(effect => effect.Amount);
            var slots = source.Player.PotionSlots.ToList();
            for (var index = 0; index < addedSlots; index++)
                slots.Add(null);

            foreach (var decision in prefix.Where(decision =>
                decision.IndexDecision?.Request.SiteId ==
                    NeowRelicAcquisitionPotionSiteId))
            {
                var previousSlot = slots.FindIndex(potion => potion is null);
                if (previousSlot < 0)
                    throw new InvalidDataException(
                        "Neow Phial Holster has no open slot for its saved potion prefix.");
                var previousIndex = decision.IndexDecision!;
                var previousOwner = "player:potion_slot:" + previousSlot +
                    ":pool:" + CanonicalJson.Sha256(pool);
                if (previousIndex.Request.OwnerId != previousOwner ||
                    previousIndex.SelectedIndex >= pool.Length)
                    throw new InvalidDataException(
                        "Neow Phial Holster potion prefix differs from its next open slot.");
                slots[previousSlot] = new PotionInstance(
                    pool[previousIndex.SelectedIndex],
                    PrototypeJson.EmptyObject());
            }

            var slot = slots.FindIndex(potion => potion is null);
            if (slot < 0)
                throw new InvalidDataException(
                    "Neow Phial Holster has no open potion slot.");
            expectedOwner = "player:potion_slot:" + slot + ":pool:" +
                CanonicalJson.Sha256(pool);
        }

        if (request.OwnerId != expectedOwner ||
            candidateLabels is not null &&
                candidateLabels.Length != pool.Length)
            throw new InvalidDataException(
                "Neow relic-acquisition model owner differs from its picked-up relic.");

        var expected = Replay(source, action, prefix).Continuation;
        if (expected is null ||
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
                expected.PendingRequest,
                ControlledStochasticRequestV111.FromIndex(request)) ||
            !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                expected.PendingCandidateLabels, candidateLabels))
            throw new InvalidDataException(
                "Neow relic-acquisition request differs from replay at its saved prefix.");

        return request.SiteId == NeowRelicAcquisitionRareCardSiteId
            ? "native-neow-arcane-scroll-rare-card"
            : "native-neow-phial-holster-potion";
    }

    private static (int CardPickCount, bool OffersPotion)?
        NeowLostCofferPlan(RunState source, GameAction action)
    {
        var effects = NeowPickupEffects(source, action);
        var world = source.World;
        if (effects is not { Length: 2 } || world is null ||
            effects[0].Kind != PrototypeRunEffectKind.GainRelic ||
            effects[0].RelicId != PrototypeNativeOvergrowthEvents.NeowRelicId(
                "LostCoffer") ||
            effects[1].Kind !=
                PrototypeRunEffectKind.OfferThreeCardsAndPotion)
            return null;

        var selectableRarities = PrototypeContent.RewardRarityWeights(
                world.Act).Where(item => item.Weight > 0)
            .Select(item => item.Rarity).ToHashSet();
        var selectableCardCount = PrototypeContent.RewardCardPool.Count(
            cardId => selectableRarities.Contains(
                PrototypeContent.Card(cardId).Rarity));
        var cardPickCount = Math.Min(3, selectableCardCount);
        if (cardPickCount == 0)
            return null;

        var acquiredRelicPreventsPotion = effects[0].RelicId is { } relicId &&
            PrototypeContent.Relic(relicId).PreventPotionAcquisition;
        var offersPotion = !acquiredRelicPreventsPotion &&
            !source.Player.ActiveRelics.Any(relic =>
                PrototypeContent.Relic(relic.RelicId)
                    .PreventPotionAcquisition);
        return (cardPickCount, offersPotion);
    }

    private static bool IsNeowLostCofferRequestSite(string siteId) =>
        siteId is NeowLostCofferCardRaritySiteId or
            NeowLostCofferCardModelSiteId or
            NeowLostCofferPotionModelSiteId;

    private static string? NeowLostCofferRequestBoundary(
        RunState source, GameAction action, RandomIndexRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        if (NeowLostCofferPlan(source, action) is null ||
            !IsNeowLostCofferRequestSite(request.SiteId) ||
            request.StreamId != "reward")
            return null;

        var expected = Replay(source, action, prefix).Continuation;
        if (expected is null ||
            !ControlledChanceContinuationExecutorV111.RequestsMatch(
                expected.PendingRequest,
                ControlledStochasticRequestV111.FromIndex(request)) ||
            !ControlledChanceContinuationExecutorV111.CandidateLabelsMatch(
                expected.PendingCandidateLabels, candidateLabels))
            throw new InvalidDataException(
                "Neow Lost Coffer request differs from replay at its saved prefix.");

        return request.SiteId switch
        {
            NeowLostCofferCardRaritySiteId =>
                "native-neow-lost-coffer-card-rarity",
            NeowLostCofferCardModelSiteId =>
                "native-neow-lost-coffer-card-model",
            NeowLostCofferPotionModelSiteId =>
                "native-neow-lost-coffer-potion-model",
            _ => null
        };
    }

    private static (string[] Commons, string[] Uncommons)?
        NeowCardBundleShufflePools(RunState source, GameAction action)
    {
        var effects = NeowPickupEffects(source, action);
        if (effects is not { Length: 2 } ||
            effects[0].Kind != PrototypeRunEffectKind.GainRelic ||
            effects[0].RelicId != PrototypeNativeOvergrowthEvents.NeowRelicId(
                "ScrollBoxes") ||
            effects[1].Kind != PrototypeRunEffectKind.OfferCardBundles)
            return null;

        var commons = PrototypeContent.NativeSilentRewardCardPool
            .Where(id => PrototypeContent.Card(id).Rarity ==
                PrototypeCardRarity.Common).ToArray();
        var uncommons = PrototypeContent.NativeSilentRewardCardPool
            .Where(id => PrototypeContent.Card(id).Rarity ==
                PrototypeCardRarity.Uncommon).ToArray();
        return commons.Length >= 4 && uncommons.Length >= 2
            ? (commons, uncommons)
            : null;
    }

    private static (int SupportCount, string OwnerId, string BoundaryId)?
        NeowCardBundleShuffleBoundary(
            RunState source, GameAction action,
            ControlledStochasticDecisionV111[] prefix)
    {
        var pools = NeowCardBundleShufflePools(source, action);
        if (pools is null)
            return null;

        var orderedPools = new[]
        {
            pools.Value.Commons,
            pools.Value.Uncommons
        };
        var eventPrefix = "Event:" +
            PrototypeNativeOvergrowthEvents.NeowEventId;
        var owners = new[]
        {
            eventPrefix + ":common_bundle",
            eventPrefix + ":uncommon_bundle"
        };
        var boundaryIds = new[]
        {
            "native-neow-scroll-boxes-common-bundle",
            "native-neow-scroll-boxes-uncommon-bundle"
        };
        var poolsInProgress = orderedPools
            .Select(pool => (string[])pool.Clone()).ToArray();
        var poolIndex = 0;
        var nextEnd = poolsInProgress[poolIndex].Length - 1;
        foreach (var savedDecision in prefix.Where(decision =>
            decision.IndexDecision?.Request.SiteId ==
                NeowCardBundleShuffleSiteId))
        {
            if (nextEnd == 0)
            {
                poolIndex++;
                if (poolIndex >= poolsInProgress.Length)
                    throw new InvalidDataException(
                        "Neow Scroll Boxes shuffle prefix exceeds its two pools.");
                nextEnd = poolsInProgress[poolIndex].Length - 1;
            }

            var decision = savedDecision.IndexDecision!;
            var expectedBound = nextEnd + 1;
            var pool = poolsInProgress[poolIndex];
            if (savedDecision.CandidateLabels is not null ||
                decision.Request.StreamId != "reward" ||
                decision.Request.OperationKind != "bounded_integer" ||
                decision.Request.OwnerId != owners[poolIndex] ||
                decision.Request.SupportCount != expectedBound ||
                decision.Request.OrderedCandidates.Length != 0 ||
                decision.SelectedIndex < 0 ||
                decision.SelectedIndex >= expectedBound)
                throw new InvalidDataException(
                    "Neow Scroll Boxes shuffle prefix differs from its ordered pools.");

            (pool[nextEnd], pool[decision.SelectedIndex]) =
                (pool[decision.SelectedIndex], pool[nextEnd]);
            nextEnd--;
        }

        if (nextEnd == 0 && poolIndex == 0)
        {
            poolIndex++;
            nextEnd = poolsInProgress[poolIndex].Length - 1;
        }
        return nextEnd > 0
            ? (nextEnd + 1, owners[poolIndex], boundaryIds[poolIndex])
            : null;
    }

    private static (string BoundaryId, string[] Pool, string OwnerId)?
        NeowPickupShufflePlan(RunState source, GameAction action)
    {
        var effects = NeowPickupEffects(source, action);
        if (effects is not { Length: 2 } ||
            effects[0].Kind != PrototypeRunEffectKind.GainRelic)
            return null;
        var acquiredRelicId = effects[0].RelicId;
        if (acquiredRelicId is null)
            return null;

        var offerKind = effects[1].Kind;
        string boundaryId;
        string[] pool;
        int takeCount;
        if (offerKind == PrototypeRunEffectKind.OfferNeowsBonesRelics &&
            acquiredRelicId == PrototypeNativeOvergrowthEvents.NeowRelicId(
                "NeowsBones"))
        {
            var owned = source.Player.Relics.Select(relic => relic.RelicId)
                .Append(acquiredRelicId)
                .ToHashSet(StringComparer.Ordinal);
            pool = PrototypeNativeOvergrowthEvents
                .NeowBonesSupportedRelicNames
                .Select(PrototypeNativeOvergrowthEvents.NeowRelicId)
                .Where(id => !owned.Contains(id)).ToArray();
            takeCount = 2;
            boundaryId = "native-neow-bones-relic-offer";
        }
        else if (offerKind ==
                PrototypeRunEffectKind.OfferThreeRareCards &&
            acquiredRelicId == PrototypeNativeOvergrowthEvents.NeowRelicId(
                "HeftyTablet"))
        {
            pool = PrototypeContent.NativeSilentRewardCardPool
                .Where(id => PrototypeContent.Card(id).Rarity ==
                    PrototypeCardRarity.Rare).ToArray();
            takeCount = 3;
            boundaryId = "native-neow-hefty-tablet-rare-card-offer";
        }
        else if (offerKind ==
                PrototypeRunEffectKind.OfferTwoColorlessCards &&
            acquiredRelicId == PrototypeNativeOvergrowthEvents.NeowRelicId(
                "LeadPaperweight"))
        {
            var profile = source.Configuration?.Profile ??
                throw new NotSupportedException(
                    "Lead Paperweight requires configured run profile data.");
            pool = PrototypeColorlessCards.RewardPoolForProfile(
                profile.FullyUnlocked,
                profile.UnlockedColorlessEpochs);
            takeCount = 2;
            boundaryId = "native-neow-lead-paperweight-colorless-offer";
        }
        else
        {
            return null;
        }

        if (pool.Length < 2)
            return null;
        var ownerId = PrototypeGameEngine.DistinctPoolShuffleOwnerId(
            "reward", takeCount, pool);
        return (boundaryId, pool, ownerId);
    }

    private static (int SupportCount, string OwnerId, string BoundaryId)?
        NeowPickupShuffleBoundary(
            RunState source, GameAction action,
            ControlledStochasticDecisionV111[] prefix)
    {
        var plan = NeowPickupShufflePlan(source, action);
        if (plan is null || plan.Value.Pool.Length < 2)
            return null;

        var pool = (string[])plan.Value.Pool.Clone();
        var nextEnd = pool.Length - 1;
        foreach (var savedDecision in prefix.Where(decision =>
            decision.IndexDecision?.Request.SiteId ==
                NeowPickupShuffleSiteId))
        {
            var decision = savedDecision.IndexDecision!;
            var expectedBound = nextEnd + 1;
            if (nextEnd <= 0 || savedDecision.CandidateLabels is not null ||
                decision.Request.StreamId != "reward" ||
                decision.Request.OperationKind != "bounded_integer" ||
                decision.Request.OwnerId != plan.Value.OwnerId ||
                decision.Request.SupportCount != expectedBound ||
                decision.Request.OrderedCandidates.Length != 0 ||
                decision.SelectedIndex < 0 ||
                decision.SelectedIndex >= expectedBound)
                throw new InvalidDataException(
                    "Neow saved shuffle prefix differs from its ordered pool.");

            (pool[nextEnd], pool[decision.SelectedIndex]) =
                (pool[decision.SelectedIndex], pool[nextEnd]);
            nextEnd--;
        }

        return nextEnd > 0
            ? (nextEnd + 1, plan.Value.OwnerId, plan.Value.BoundaryId)
            : null;
    }

    private static (string[] CandidateIds, string OwnerId)?
        UnownedRelicSupport(RunState source, GameAction action)
    {
        if (ExpectedNeowUnownedRelicRequestCount(source, action) > 0)
        {
            var effects = NeowPickupEffects(source, action)!;
            var acquiredRelicId = effects[0].RelicId ??
                throw new InvalidDataException(
                    "Neow Small Capsule acquisition lost its relic identity.");
            var ownedRelics = source.Player.Relics.Select(relic => relic.RelicId)
                .Append(acquiredRelicId)
                .ToHashSet(StringComparer.Ordinal);
            var candidates = PrototypeContent.RelicPool.Where(id =>
                !ownedRelics.Contains(id)).ToArray();
            if (candidates.Length == 0)
                throw new InvalidDataException(
                    "Neow Small Capsule has no unowned relic support.");
            var prefix = "Event:" + PrototypeNativeOvergrowthEvents.NeowEventId;
            return (candidates, prefix + ":unowned_relic_pool:" +
                CanonicalJson.Sha256(candidates));
        }

        if (ExpectedPunchOffUnownedRelicRequestCount(source, action) > 0)
        {
            var ownedRelics = source.Player.Relics.Select(relic => relic.RelicId)
                .ToHashSet(StringComparer.Ordinal);
            var candidates = PrototypeContent.RelicPool.Where(id =>
                !ownedRelics.Contains(id)).ToArray();
            var prefix = "Event:" + PrototypeNativePunchOff.EventId;
            return (candidates, prefix + ":unowned_relic_pool:" +
                CanonicalJson.Sha256(candidates));
        }

        return null;
    }

    private static PrototypeRunEffectSpec[]? NeowPickupEffects(
        RunState source, GameAction action)
    {
        var world = source.World;
        if (world?.Event?.EventId !=
            PrototypeNativeOvergrowthEvents.NeowEventId)
            return null;

        if (source.Phase == RunPhase.Event && action.Kind == "event_choice" &&
            EventChoiceId(action) is { } choiceId)
        {
            return PrototypeNativeOvergrowthEvents.NeowDefinition.Choices
                .SingleOrDefault(choice => StringComparer.Ordinal.Equals(
                    choice.Id, choiceId))?.Effects;
        }

        if (source.Phase != RunPhase.Reward ||
            action.Kind is not ("take_reward_relic" or
                "take_reward_extra_relic") ||
            world.Reward is not { RelicResolved: true,
                ExtraRelicRewardIds: { } ids } reward)
            return null;

        var payload = action.Payload.Deserialize<ChooseRelicPayload>();
        var groupIndex = action.Kind == "take_reward_relic"
            ? reward.FirstPendingExtraRelicGroupIndex
            : payload?.Index ?? -1;
        if (payload is null ||
            action.Kind == "take_reward_relic" && payload.Index != 0 ||
            groupIndex < 0 || groupIndex >= ids.Length ||
            reward.IsExtraRelicGroupResolved(groupIndex))
            return null;

        var relicId = ids[groupIndex];
        var pickupChoiceId = "take_" + relicId;
        return PrototypeNativeOvergrowthEvents.NeowDefinition.Choices
            .SingleOrDefault(choice => StringComparer.Ordinal.Equals(
                choice.Id, pickupChoiceId))?.Effects;
    }

    private static int ExpectedEventRelicDrawCount(
        RunState source, GameAction action)
    {
        var eventState = source.World?.Event;
        var choiceId = EventChoiceId(action);
        var directDrawCount = NativeDirectEventRelicDrawCount(
            source, action);
        if (directDrawCount > 0)
            return directDrawCount;
        if (eventState is null || choiceId is null ||
            !PrototypeContent.Events.TryGetValue(
                eventState.EventId, out var definition))
            return 0;
        var effects = definition.Choices.FirstOrDefault(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId))?.Effects;
        if (effects is null)
            return 0;
        var randomRelicDraws = effects.Count(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRandomRelic);
        var toyBoxAcquisitions = effects.Count(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRelic &&
            StringComparer.Ordinal.Equals(
                effect.RelicId, NativeAncientRewardsV111.ToyBoxId));
        var existingReward = source.World?.Reward;
        if (existingReward?.ExtraRelicWaxFlags is { Length: 5 } flags &&
            flags.All(flag => flag) &&
            existingReward.SourceRoom.StartsWith(
                "Event:", StringComparison.Ordinal))
            toyBoxAcquisitions = 0;
        return randomRelicDraws + toyBoxAcquisitions * ToyBoxDrawCount;
    }

    private static int NativeDirectEventRelicDrawCount(
        RunState source, GameAction action)
    {
        if (source.Phase != RunPhase.Event || action.Kind != "event_choice" ||
            source.World?.Event is not { } eventState ||
            EventChoiceId(action) is not { } choiceId)
            return 0;
        if (eventState.EventId == PrototypeNativeGloryEvents.RoundTeaPartyId &&
            eventState.NativePageIndex == 1 && choiceId == "continue_fight")
            return 1;
        if (eventState.EventId == PrototypeNativeGloryEvents.TrialId &&
            eventState.NativePageIndex == 2 && choiceId == "merchant_guilty")
            return 2;
        return 0;
    }

    private static int NeowRelicPickupDrawCount(
        RunState source, GameAction action)
    {
        var world = source.World;
        if (source.Phase != RunPhase.Reward || world is null ||
            world.Event?.EventId !=
                PrototypeNativeOvergrowthEvents.NeowEventId ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            action.Kind is not ("take_reward_relic" or
                "take_reward_extra_relic") ||
            world.Reward is not { } reward ||
            !reward.RelicResolved || reward.ExtraRelicRewardIds is not { } ids)
            return 0;

        var payload = action.Payload.Deserialize<ChooseRelicPayload>();
        var groupIndex = action.Kind == "take_reward_relic"
            ? reward.FirstPendingExtraRelicGroupIndex
            : payload?.Index ?? -1;
        if (payload is null ||
            action.Kind == "take_reward_relic" && payload.Index != 0 ||
            groupIndex < 0 || groupIndex >= ids.Length ||
            reward.IsExtraRelicGroupResolved(groupIndex))
            return 0;

        var relicId = ids[groupIndex];
        var choiceId = "take_" + relicId;
        var choice = PrototypeNativeOvergrowthEvents.NeowDefinition
            .Choices.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.Id, choiceId));
        var effects = choice?.Effects;
        if (effects is not { Length: > 1 } ||
            effects[0].Kind != PrototypeRunEffectKind.GainRelic ||
            !StringComparer.Ordinal.Equals(effects[0].RelicId, relicId) ||
            effects.Skip(1).Any(effect =>
                effect.Kind is not (PrototypeRunEffectKind.GainRandomRelic or
                    PrototypeRunEffectKind.AddCard)))
            return 0;
        return effects.Count(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRandomRelic);
    }

    private static int PaelWingRelicDrawCount(
        RunState source, GameAction action)
    {
        var world = source.World;
        if (source.Phase != RunPhase.Reward || world is null ||
            world.Reward is null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            action.Kind is not ("sacrifice_reward_card" or
                "sacrifice_reward_card_group"))
            return 0;
        var wing = source.Player.Relics.FirstOrDefault(relic =>
            relic.RelicId == NativePaelRewardsV111.WingId &&
            !relic.IsMelted);
        if (wing is null)
            return 0;
        var nextSacrificeCount = checked(
            NativePaelRewardsV111.RewardsSacrificed(wing) + 1);
        return nextSacrificeCount % 2 == 0 ? 1 : 0;
    }

    private static bool IsPaelWingRelicReward(
        RunState source, GameAction action) =>
        PaelWingRelicDrawCount(source, action) > 0;

    private static bool IsBattlewornSettingThreeCombat(RunState source)
    {
        var world = source.World;
        var eventState = world?.Event;
        return source.Phase == RunPhase.Combat &&
            world?.Combat is not null &&
            eventState is
            {
                EventId: PrototypeNativeGloryEvents.BattlewornDummyId,
                NativePageIndex: 3,
                ChosenChoiceId: "setting_3"
            };
    }

    private static bool HasUnsupportedFixedRelicEffect(
        string eventId, string choiceId)
    {
        if (!PrototypeContent.Events.TryGetValue(eventId, out var definition))
            return true;
        var effects = definition.Choices.FirstOrDefault(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId))?.Effects;
        return effects is null || effects.Any(effect =>
            effect.Kind == PrototypeRunEffectKind.GainRelic &&
            !StringComparer.Ordinal.Equals(
                effect.RelicId, NativeAncientRewardsV111.ToyBoxId));
    }

    private static bool HasToyBoxEffect(string eventId, string choiceId) =>
        PrototypeContent.Events.TryGetValue(eventId, out var definition) &&
        definition.Choices.FirstOrDefault(choice =>
            StringComparer.Ordinal.Equals(choice.Id, choiceId))?.Effects.Any(
                effect => effect.Kind == PrototypeRunEffectKind.GainRelic &&
                    StringComparer.Ordinal.Equals(
                        effect.RelicId, NativeAncientRewardsV111.ToyBoxId)) == true;

    private static string? EventChoiceId(GameAction action) =>
        action.Kind == "event_choice"
            ? action.Payload.Deserialize<EventChoicePayload>()?.ChoiceId
            : null;

    private static ControlledStochasticRequestV111 AsRequest(
        ControlledStochasticDecisionV111 decision) => decision switch
    {
        { UnitFloatDecision: { } unit } =>
            ControlledStochasticRequestV111.FromUnitFloat(unit.Request),
        { IndexDecision: { } index } =>
            ControlledStochasticRequestV111.FromIndex(index.Request),
        _ => throw new InvalidDataException(
            "Native event relic provider decision has an unsupported type.")
    };

    private static string[]? CloneLabels(string[]? labels) =>
        labels is null ? null : (string[])labels.Clone();

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}
