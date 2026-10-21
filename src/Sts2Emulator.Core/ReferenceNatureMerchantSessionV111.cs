using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureMerchantLawDecisionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LawId) ||
            string.IsNullOrWhiteSpace(EventId) ||
            string.IsNullOrWhiteSpace(OutcomeId) || OutcomeIndex < 0)
            throw new InvalidDataException(
                "Invalid ReferenceNature merchant law decision.");
    }
}

public sealed record ReferenceNatureMerchantSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureMerchantLawDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    private ControlledStochasticDecisionV111[]? _providerDecisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(
            ProviderDecisions);
    private ReferenceNatureMerchantLawDecisionV111[]? _decisionTranscript =
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
    public ReferenceNatureMerchantLawDecisionV111[] DecisionTranscript
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
/// Atomic configured Native Shop entry and supported purchase boundaries
/// driven by conditional Chance Laws. The source mechanics are rerun from the
/// action checkpoint for each prefix; only the completed action and
/// ReferenceNature cursor are published.
/// </summary>
public sealed record ReferenceNatureMerchantSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ControlledStochasticDecisionV111[] ProviderDecisions,
    ReferenceNatureMerchantLawDecisionV111[] DecisionTranscript,
    ControlledStochasticRequestV111? PendingRequest,
    string[]? PendingCandidateLabels)
{
    private ControlledStochasticDecisionV111[]? _providerDecisions =
        ReferenceNatureSessionValueCopiesV111.CopyProviderDecisions(
            ProviderDecisions);
    private ReferenceNatureMerchantLawDecisionV111[]? _decisionTranscript =
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
    public ReferenceNatureMerchantLawDecisionV111[] DecisionTranscript
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

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-merchant-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => PendingRequest is null ? null :
        QueryLaw(Origin, Action, PendingRequest, PendingCandidateLabels,
            ProviderDecisions);

    public static ReferenceNatureMerchantSessionV111 Begin(
        RunState source, GameAction action, ulong seed) =>
        Begin(source, action, ReferenceNatureRunCursorV111.Begin(
            source, seed, "Native merchant"));

    public static ReferenceNatureMerchantSessionV111 Begin(
        RunState source, GameAction action) =>
        Begin(source, action, ReferenceNatureRunCursorV111.Begin(
            source, null, "Native merchant"));

    private static ReferenceNatureMerchantSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ValidateOpeningBoundary(source, action);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Native merchant");

        var natureOrigin =
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, createRelicBagWhenMissing: true);
        var replay = Replay(natureOrigin, action, []);
        var run = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(
                completed, initialNatureState)
            : natureOrigin.Fork();
        var session = new ReferenceNatureMerchantSessionV111(
            CurrentSchemaId, natureOrigin.Fork(), CloneAction(action), run,
            initialNatureState.Fork(), initialNatureState.Fork(), [], [],
            replay.Continuation?.PendingRequest.Fork(),
            replay.Continuation?.PendingCandidateLabels is { } labels
                ? (string[])labels.Clone() : null);
        session.Validate();
        return session;
    }

    public ReferenceNatureMerchantSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Action = CloneAction(Action),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            ProviderDecisions = ProviderDecisions
                .Select(decision => decision.Fork()).ToArray(),
            DecisionTranscript = (ReferenceNatureMerchantLawDecisionV111[])
                DecisionTranscript.Clone(),
            PendingRequest = PendingRequest?.Fork(),
            PendingCandidateLabels = PendingCandidateLabels is null ? null :
                (string[])PendingCandidateLabels.Clone()
        };
    }

    public ReferenceNatureMerchantSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature merchant session is already complete.");

        var law = QueryLaw(Origin, Action, PendingRequest!, PendingCandidateLabels,
            ProviderDecisions);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, PendingRequest!, selection, Origin,
            PendingCandidateLabels);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            ProviderDecisions, PendingRequest!, PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(Origin),
            continuation, providerDecision,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State);
        var providerDecisions = replay.Continuation?.Decisions ??
            ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureMerchantLawDecisionV111(
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
            Run is null || InitialNatureState is null || NatureState is null ||
            ProviderDecisions is null || DecisionTranscript is null ||
            ProviderDecisions.Length != DecisionTranscript.Length ||
            string.IsNullOrWhiteSpace(Action.Kind))
            throw new InvalidDataException(
                "Invalid ReferenceNature merchant session envelope.");
        if (PendingRequest is { } pendingRequest)
        {
            pendingRequest.Validate();
            if (PendingCandidateLabels is { } pendingLabels &&
                (pendingRequest.IndexRequest is null ||
                 pendingLabels.Length !=
                    pendingRequest.IndexRequest.SupportCount ||
                 pendingLabels.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidDataException(
                    "Invalid ReferenceNature merchant pending candidate labels.");
            if (StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(
                    pendingRequest.SiteId))
                ValidateRegisteredPurchaseIndexSupport(
                    pendingRequest, PendingCandidateLabels);
        }
        else if (PendingCandidateLabels is not null)
        {
            throw new InvalidDataException(
                "Completed ReferenceNature merchant session has pending candidate labels.");
        }
        ValidateOpeningBoundary(Origin, Action);
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native merchant session origin");
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native merchant session replay");
        InitialNatureState.Validate();
        NatureState.Validate();
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, InitialNatureState, "Native merchant");

        var expectedNatureState = InitialNatureState.Fork();
        for (var index = 0; index < ProviderDecisions.Length; index++)
        {
            var prefix = ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var continuationAtBoundary = replayAtBoundary.Continuation;
            var request = continuationAtBoundary?.PendingRequest
                ?? throw new InvalidDataException(
                    "Merchant replay completed before its saved decision transcript.");
            var labels = continuationAtBoundary!.PendingCandidateLabels;
            var savedDecision = ProviderDecisions[index];
            savedDecision.Validate();
            if (!SameRequest(request, AsRequest(savedDecision)) ||
                !SameLabels(labels, savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "ReferenceNature merchant provider transcript changed its request.");

            var law = QueryLaw(Origin, Action, request, labels, prefix);
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedTranscript = new ReferenceNatureMerchantLawDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "ReferenceNature merchant law decisions do not replay.");
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, Action, ProviderDecisions);
        if ((replay.Continuation is null) != Complete ||
            !SameRequest(replay.Continuation?.PendingRequest, PendingRequest) ||
            !SameLabels(replay.Continuation?.PendingCandidateLabels,
                PendingCandidateLabels) ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState))
            throw new InvalidDataException(
                "ReferenceNature merchant continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature merchant run differs from its atomic cursor.");

        foreach (var decision in DecisionTranscript)
            decision.Validate();
    }

    public static string Save(ReferenceNatureMerchantSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureMerchantSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.InitialNatureState, session.NatureState,
            session.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureMerchantLawDecisionV111[])
                session.DecisionTranscript.Clone(),
            session.PendingRequest?.Fork(),
            session.PendingCandidateLabels is null ? null :
                (string[])session.PendingCandidateLabels.Clone());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureMerchantSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureMerchantSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature merchant snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.ProviderDecisions is null ||
            snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature merchant snapshot.");
        var session = new ReferenceNatureMerchantSessionV111(
            CurrentSchemaId, RunSnapshot.Load(snapshot.OriginSnapshotJson),
            CloneAction(snapshot.Action),
            RunSnapshot.Load(snapshot.RunSnapshotJson),
            snapshot.InitialNatureState.Fork(), snapshot.NatureState.Fork(),
            snapshot.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureMerchantLawDecisionV111[])
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
        string[]? candidateLabels, ControlledStochasticDecisionV111[] prefix)
    {
        request.Validate();
        var siteId = request.SiteId;
        if (request.IndexRequest is { } playerModelRequest &&
            playerModelRequest.StreamId == NativePlayerRngV111.RewardsStreamId &&
            siteId is "relic.acquisition.random_rare_card.model" or
                "relic.acquisition.random_potion.model" or
                "run.potion.fill_empty_slots.model")
        {
            if (candidateLabels is null ||
                candidateLabels.Length != playerModelRequest.SupportCount)
                throw new InvalidDataException(
                    "Native player model request lost its ordered candidate labels.");
            return ChanceLawEngineV111.QueryNativePlayerModelIndex(
                siteId, playerModelRequest.OwnerId,
                playerModelRequest.SupportCount,
                playerModelRequest.Ordinal);
        }
        if (siteId == "shop.merchant.sale_slot.index_draw")
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, 0);

        if (siteId == "card_rarity.roll.unit_float")
        {
            var owner = ParseMerchantCardOwner(request.OwnerId);
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, owner.Ordinal,
                cardType: owner.CardType.ToString());
        }
        if (siteId == "shop.merchant.card_model.index_draw")
        {
            var owner = ParseMerchantCardModelOwner(request.OwnerId);
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, owner.Ordinal,
                cardType: owner.CardType.ToString(),
                rarity: owner.Rarity);
        }
        if (siteId == "shop.colorless_card_model.index_draw")
        {
            var ordinal = prefix.Count(decision =>
                decision.IndexDecision?.Request.SiteId == siteId);
            var rarity = SegmentAfter(request.OwnerId, "rarity:", ":pool:");
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, ordinal, rarity: rarity);
        }
        if (siteId == "potion_factory.rarity_roll.unit_float")
        {
            var ordinal = prefix.Count(decision =>
                decision.UnitFloatDecision?.Request.SiteId == siteId);
            if (IsAlchemicalCofferPurchase(source, action))
            {
                if (request.Ordinal != ordinal)
                    throw new InvalidDataException(
                        "Alchemical Coffer rarity request ordinal differs from its replay prefix.");
                return ChanceLawEngineV111
                    .QueryNativeAlchemicalCofferPotionRequest(
                        source, action, request, null, []);
            }
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, ordinal);
        }
        if (siteId == "potion_factory.model_within_rarity.index_draw")
        {
            var ordinal = prefix.Count(decision =>
                decision.IndexDecision?.Request.SiteId == siteId);
            var (rarity, excluded) = ResolvePotionPrefix(prefix);
            if (IsAlchemicalCofferPurchase(source, action))
            {
                if (request.Ordinal != ordinal)
                    throw new InvalidDataException(
                        "Alchemical Coffer model request ordinal differs from its replay prefix.");
                return ChanceLawEngineV111
                    .QueryNativeAlchemicalCofferPotionRequest(
                        source, action, request,
                        Enum.Parse<PrototypeNativePotionRarity>(rarity),
                        excluded);
            }
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, ordinal, rarity: rarity,
                excludedPotionIds: excluded);
        }
        if (siteId == "relic_grab_bag.rarity.unit_float")
        {
            if (IsToyBoxPurchase(source, action))
            {
                var relicRequest = request.UnitFloatRequest!;
                if (relicRequest.OwnerId != PrototypeNativeRelicGrabBag
                        .NativeTreasureRarityOwnerId)
                    throw new InvalidDataException(
                        "Toy Box relic acquisition has an unexpected rarity owner.");
                var law = ChanceLawEngineV111.QueryNativeRelicRarity(
                    relicRequest);
                return law with
                {
                    EventId = PurchaseRequestEventId(source, action,
                        request, candidateLabels, prefix)
                };
            }
            var ordinal = ParseOrdinal(request.OwnerId,
                ":merchant_relic:", ":merchant");
            return ChanceLawEngineV111.QueryNativeMerchantOffer(
                source, siteId, ordinal);
        }
        if (siteId == PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId &&
            IsToyBoxPurchase(source, action))
        {
            var indexRequest = request.IndexRequest!;
            if (indexRequest.StreamId !=
                    NativePlayerRngV111.RewardsStreamId ||
                candidateLabels is not { Length: > 0 } ||
                candidateLabels.Length != indexRequest.SupportCount ||
                candidateLabels.Any(string.IsNullOrWhiteSpace) ||
                !candidateLabels.SequenceEqual(candidateLabels.OrderBy(
                    label => label, StringComparer.Ordinal),
                    StringComparer.Ordinal) ||
                candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                    candidateLabels.Length)
                throw new InvalidDataException(
                    "Toy Box relic identity request lost its sorted candidate support.");
            var labels = candidateLabels;
            var rarityText = SegmentAfter(indexRequest.OwnerId,
                ":rarity:", ":pool:");
            if (!Enum.TryParse<PrototypeRelicRarity>(rarityText,
                    ignoreCase: false, out var rarity) ||
                !Enum.IsDefined(rarity) || source.World is not { } world ||
                indexRequest.OwnerId !=
                    PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                        world, rarity, labels) ||
                !indexRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, labels.Length)))
                throw new InvalidDataException(
                    "Toy Box relic identity request differs from its rarity-conditioned support.");

            return ChanceLawEngineV111.QueryUniformLabeledIndexSupport(
                PurchaseRequestEventId(source, action, request, labels,
                    prefix), indexRequest, labels,
                "native-toy-box-relic-identity-over-unordered-bag-v1",
                "Uniform identity over the captured remaining Native relic-bag support; queue identity and source-pool completeness remain unverified.");
        }
        if (siteId == PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId)
        {
            var ordinal = ParseOrdinal(request.OwnerId,
                ":merchant_relic:", ":rarity:");
            var rarityText = SegmentAfter(request.OwnerId,
                ":rarity:", ":pool:");
            if (!Enum.TryParse<PrototypeRelicRarity>(rarityText,
                    ignoreCase: false, out var rarity))
                throw new InvalidDataException(
                    "Merchant relic model owner has an invalid rarity.");
            if (candidateLabels is null)
                throw new InvalidDataException(
                    "Merchant relic model support labels are missing.");
            var excluded = ResolveSelectedRelics(prefix);
            return ChanceLawEngineV111.QueryNativeMerchantRelicSelection(
                source, request.IndexRequest!, ordinal, rarity,
                candidateLabels, excluded);
        }
        if (StochasticSiteRegistryV111.NativeMerchantPriceSiteIds
            .Contains(siteId, StringComparer.Ordinal))
        {
            var item = ParseMerchantPriceOwner(siteId, request.OwnerId);
            return ChanceLawEngineV111.QueryNativeMerchantPrice(
                source, siteId, item.Ordinal, item.CardId, item.PotionId,
                item.RelicId, item.ColorlessOffer, item.SaleReprice);
        }
        if (siteId == "shop.merchant.card_upgrade_roll.unit_float")
            return ChanceLawEngineV111.QueryNativeMerchantNoEffectUnitFloat(
                source, request.UnitFloatRequest!);

        if (StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(siteId) &&
            request.IndexRequest is { } purchaseIndexRequest)
        {
            var purchaseCandidateLabels =
                ValidateRegisteredPurchaseIndexSupport(
                    request, candidateLabels);

            var eventId = CanonicalJson.Sha256(new
            {
                SchemaId = CurrentSchemaId,
                MechanicsBoundary = SimulationMechanicsProjectionEngineV111
                    .Fingerprint(source),
                ActionFingerprint = PrototypeAiEnvironment.StableActionId(action),
                RequestFingerprint = CanonicalJson.Sha256(
                    purchaseIndexRequest),
                CandidateLabels = purchaseCandidateLabels,
                PriorPrefix = prefix.Select(decision =>
                    CanonicalJson.Sha256(decision)).ToArray()
            });
            return ChanceLawEngineV111.QueryRandomIndex(
                eventId, purchaseIndexRequest);
        }

        throw new NotSupportedException(
            $"ReferenceNature merchant replay does not support stochastic site '{siteId}'.");
    }

    private static string[] ValidateRegisteredPurchaseIndexSupport(
        ControlledStochasticRequestV111 request, string[]? candidateLabels)
    {
        if (!StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(
                request.SiteId) || request.IndexRequest is not { } indexRequest ||
            !ControlledRunActionEngineV111.IsSupportedShopPurchaseStream(
                indexRequest) ||
            candidateLabels is not { } labels ||
            labels.Length != indexRequest.SupportCount ||
            labels.Any(string.IsNullOrWhiteSpace) ||
            labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
            throw new InvalidDataException(
                "Registered Shop-purchase index request lost its distinct ordered support labels.");
        return labels;
    }

    private static ControlledStochasticContinuationResultV111 Replay(
        RunState source, GameAction action,
        ControlledStochasticDecisionV111[] decisions)
        => ControlledChanceContinuationExecutorV111.CaptureStochastic(
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(source),
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State);

    private static void ValidateOpeningBoundary(
        RunState source, GameAction action)
    {
        if (source.World is not { } world || source.NativeRunRng is null ||
            source.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "ReferenceNature merchant session requires configured Native Shop state.");
        var legal = new PrototypeGameEngine().GetLegalActions(source).Any(
            candidate => PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action));
        var validShopEntry = source.Phase == RunPhase.MapChoice &&
            action.Kind == "choose_map_node" &&
            PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
                .Any(node => node.NodeId ==
                    action.ReadPayload<ChooseMapNodePayload>().NodeId &&
                    node.RoomType == PrototypeRoomType.Shop);
        var validConfiguredShopPurchase =
            ControlledRunActionEngineV111.IsShopPurchaseAction(source, action)
            && source.World?.Shop is not null;
        if ((!validShopEntry && !validConfiguredShopPurchase) || !legal)
            throw new InvalidOperationException(
                "ReferenceNature merchant session requires a current legal configured Shop-entry or purchase action.");
        PrototypeStateInvariants.Validate(source);
    }

    private static bool IsAlchemicalCofferPurchase(
        RunState source, GameAction action)
    {
        if (source.Phase != RunPhase.Shop || action.Kind != "buy_relic" ||
            source.World?.Shop is not { } shop)
            return false;
        var payload = action.ReadPayload<BuyOfferPayload>();
        return shop.RelicOffers.Any(offer => offer.OfferId == payload.OfferId &&
            !offer.Sold && offer.ItemId ==
                PrototypeNativeOrobasRelics.AlchemicalCofferId);
    }

    private static bool IsToyBoxPurchase(
        RunState source, GameAction action)
    {
        if (source.Phase != RunPhase.Shop || action.Kind != "buy_relic" ||
            source.World?.Shop is not { } shop)
            return false;
        var payload = action.ReadPayload<BuyOfferPayload>();
        return shop.RelicOffers.Any(offer => offer.OfferId == payload.OfferId &&
            !offer.Sold && offer.ItemId == NativeAncientRewardsV111.ToyBoxId);
    }

    private static string PurchaseRequestEventId(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix) =>
        CanonicalJson.Sha256(new
        {
            SchemaId = CurrentSchemaId,
            MechanicsBoundary = SimulationMechanicsProjectionEngineV111
                .Fingerprint(source),
            ActionFingerprint = PrototypeAiEnvironment.StableActionId(action),
            RequestFingerprint = CanonicalJson.Sha256(request),
            CandidateLabels = candidateLabels,
            PriorPrefix = prefix.Select(decision =>
                CanonicalJson.Sha256(decision)).ToArray()
        });

    private static (PrototypeCardType CardType, int Ordinal)
        ParseMerchantCardOwner(string owner)
    {
        var parts = owner.Split(':');
        if (parts.Length != 6 || parts[0] != "type" ||
            !Enum.TryParse<PrototypeCardType>(parts[1], false,
                out var cardType) || parts[2] != "offer" ||
            !int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out var ordinal) ||
            parts[4] != "offset" || !int.TryParse(parts[5],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            throw new InvalidDataException(
                "Merchant card-rarity request owner is malformed.");
        return (cardType, ordinal);
    }

    private static (PrototypeCardType CardType, int Ordinal, string Rarity)
        ParseMerchantCardModelOwner(string owner)
    {
        var parts = owner.Split(':');
        if (parts.Length < 8 || parts[0] != "type" ||
            !Enum.TryParse<PrototypeCardType>(parts[1], false,
                out var cardType) || parts[2] != "offer" ||
            !int.TryParse(parts[3], NumberStyles.None,
                CultureInfo.InvariantCulture, out var ordinal) ||
            parts[4] != "rarity" || parts[6] != "pool")
            throw new InvalidDataException(
                "Merchant card-model request owner is malformed.");
        return (cardType, ordinal, parts[5]);
    }

    private static (string Rarity, string[] Excluded) ResolvePotionPrefix(
        ControlledStochasticDecisionV111[] prefix)
    {
        PrototypeNativePotionRarity? rarity = null;
        var excluded = new List<string>();
        foreach (var decision in prefix)
        {
            if (decision.UnitFloatDecision is { } floatDecision &&
                floatDecision.Request.SiteId ==
                    "potion_factory.rarity_roll.unit_float")
                rarity = PrototypeNativePotionShop.ResolveRarityNative(
                    floatDecision.SelectedValue);
            else if (decision.IndexDecision is { } indexDecision &&
                     indexDecision.Request.SiteId ==
                        "potion_factory.model_within_rarity.index_draw")
            {
                if (rarity is null)
                    throw new InvalidDataException(
                        "Potion model transcript has no preceding rarity outcome.");
                var candidates = PrototypeNativePotionShop.CandidatesForRarity(
                    rarity.Value, excluded);
                if (indexDecision.SelectedIndex >= candidates.Length)
                    throw new InvalidDataException(
                        "Potion model transcript index exceeds its current pool.");
                excluded.Add(candidates[indexDecision.SelectedIndex]);
                rarity = null;
            }
        }
        if (rarity is null)
            throw new InvalidDataException(
                "Merchant potion model request has no unresolved rarity outcome.");
        return (rarity.Value.ToString(), excluded.ToArray());
    }

    private static string[] ResolveSelectedRelics(
        ControlledStochasticDecisionV111[] prefix)
    {
        var result = new List<string>();
        foreach (var decision in prefix)
        {
            if (decision.IndexDecision is not { } index ||
                index.Request.SiteId !=
                    PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId)
                continue;
            var labels = decision.CandidateLabels ?? throw new InvalidDataException(
                "Merchant relic model transcript lost its candidate identities.");
            result.Add(labels[index.SelectedIndex]);
        }
        return result.ToArray();
    }

    private static (int Ordinal, string? CardId, string? PotionId,
        string? RelicId, bool ColorlessOffer, bool SaleReprice)
        ParseMerchantPriceOwner(string siteId, string owner)
    {
        if (siteId == "shop.merchant.card_price_modifier.unit_float")
        {
            if (owner.Contains(":colorless_offer:", StringComparison.Ordinal))
            {
                var ordinal = ParseOrdinal(owner, ":colorless_offer:", ":card:");
                return (ordinal, Suffix(owner, ":card:"), null, null,
                    true, false);
            }
            var offerOrdinal = ParseOrdinal(owner, ":offer:",
                owner.Contains(":sale_reprice:", StringComparison.Ordinal)
                    ? ":sale_reprice:" : ":card:");
            var sale = owner.Contains(":sale_reprice:", StringComparison.Ordinal);
            return (offerOrdinal, sale
                    ? Suffix(owner, ":sale_reprice:")
                    : Suffix(owner, ":card:"),
                null, null, false, sale);
        }
        if (siteId == "shop.merchant.potion_price_modifier.unit_float")
        {
            var ordinal = ParseOrdinal(owner, ":offer:", ":potion:");
            var potion = Suffix(owner, ":potion:").Split(':')[0];
            return (ordinal, null, potion, null, false, false);
        }
        if (siteId == "shop.merchant.relic_price_modifier.unit_float")
        {
            var ordinal = ParseOrdinal(owner, ":offer:", ":relic:");
            var relic = Suffix(owner, ":relic:").Split(':')[0];
            return (ordinal, null, null, relic, false, false);
        }
        throw new NotSupportedException(
            "Unsupported native merchant price site.");
    }

    private static int ParseOrdinal(string value, string start, string end)
    {
        var startIndex = value.LastIndexOf(start, StringComparison.Ordinal);
        var endIndex = value.IndexOf(end,
            startIndex < 0 ? 0 : startIndex + start.Length,
            StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < 0 ||
            !int.TryParse(value[(startIndex + start.Length)..endIndex],
                NumberStyles.None, CultureInfo.InvariantCulture,
                out var ordinal) || ordinal < 0)
            throw new InvalidDataException(
                "Merchant request owner has a malformed offer ordinal.");
        return ordinal;
    }

    private static string SegmentAfter(string value, string start, string end)
    {
        var startIndex = value.LastIndexOf(start, StringComparison.Ordinal);
        var endIndex = value.IndexOf(end,
            startIndex < 0 ? 0 : startIndex + start.Length,
            StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex + start.Length)
            throw new InvalidDataException(
                "Merchant request owner has a malformed segment.");
        return value[(startIndex + start.Length)..endIndex];
    }

    private static string Suffix(string value, string marker)
    {
        var index = value.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || index + marker.Length >= value.Length)
            throw new InvalidDataException(
                "Merchant request owner has a malformed item identity.");
        return value[(index + marker.Length)..];
    }

    private static bool SameLabels(string[]? left, string[]? right) =>
        left is null ? right is null : right is not null &&
            left.SequenceEqual(right, StringComparer.Ordinal);

    private static bool SameRequest(
        ControlledStochasticRequestV111? left,
        ControlledStochasticRequestV111? right) =>
        left is null ? right is null : right is not null &&
            CanonicalJson.Sha256(left) == CanonicalJson.Sha256(right);

    private static ControlledStochasticRequestV111 AsRequest(
        ControlledStochasticDecisionV111 decision) => decision.ToRequest();

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };

}
