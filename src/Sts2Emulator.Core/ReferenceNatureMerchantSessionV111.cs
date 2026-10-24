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
    : IReferenceNatureControlledActionSessionV111
{
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureRunActionMechanicsStateV111,
        ReferenceNatureControlledActionStochasticStateV111<
            ReferenceNatureMerchantLawDecisionV111>> Simulation { get; init; } =
        new(
            new ReferenceNatureRunActionMechanicsStateV111(
                Origin, Action, Run),
            new ReferenceNatureControlledActionStochasticStateV111<
                ReferenceNatureMerchantLawDecisionV111>(
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
        ReferenceNatureMerchantLawDecisionV111> Stochastic
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
    public ReferenceNatureMerchantLawDecisionV111[] DecisionTranscript
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
        "sts2-v111-reference-nature-merchant-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Stochastic.PendingRequest is null;

    [JsonIgnore]
    public int NextOrdinal => Stochastic.ProviderDecisions.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw =>
        Stochastic.PendingRequest is null ? null :
            QueryLaw(Origin, Action, Stochastic.PendingRequest,
                Stochastic.PendingCandidateLabels,
                Stochastic.ProviderDecisions);

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
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureMerchantSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature merchant session is already complete.");

        var law = QueryLaw(Origin, Action, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels,
            Stochastic.ProviderDecisions);
        var selection = ReferenceNatureV111.SampleValidated(law, Stochastic.NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, Stochastic.PendingRequest!, selection, Origin,
            Stochastic.PendingCandidateLabels);
        var continuation = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            Stochastic.ProviderDecisions, Stochastic.PendingRequest!,
            Stochastic.PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111.ResolveStochastic(
            ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(Origin),
            continuation, providerDecision,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(Action)).State);
        var providerDecisions = replay.Continuation?.Decisions ??
            Stochastic.ProviderDecisions.Append(providerDecision.Fork()).ToArray();
        var transcript = Stochastic.DecisionTranscript.Append(
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
            Run is null ||
            string.IsNullOrWhiteSpace(Action.Kind))
            throw new InvalidDataException(
                "Invalid ReferenceNature merchant session envelope.");
        Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction);
        Stochastic.Validate();
        if (Stochastic.PendingRequest is { } pendingRequest)
        {
            if (StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(
                    pendingRequest.SiteId))
                ValidateRegisteredPurchaseIndexSupport(
                    pendingRequest, Stochastic.PendingCandidateLabels);
        }
        ValidateOpeningBoundary(Origin, Action);
        if (IsToyBoxPurchase(Origin, Action))
            ValidateToyBoxPurchasePrefix();
        if (Origin.Stochastic.NativeRunRng is null &&
            IsAlchemicalCofferPurchase(Origin, Action))
            ValidateLegacyAlchemicalCofferPurchasePrefix();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Origin, "Native merchant session origin");
        ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
            Run, "Native merchant session replay");
        ReferenceNatureRunCursorV111.ValidateInitial(
            Origin, Stochastic.InitialNatureState, "Native merchant");

        var expectedNatureState = Stochastic.InitialNatureState.Fork();
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var prefix = Stochastic.ProviderDecisions.Take(index).ToArray();
            var replayAtBoundary = Replay(Origin, Action, prefix);
            var continuationAtBoundary = replayAtBoundary.Continuation;
            var request = continuationAtBoundary?.PendingRequest
                ?? throw new InvalidDataException(
                    "Merchant replay completed before its saved decision transcript.");
            var labels = continuationAtBoundary!.PendingCandidateLabels;
            var savedDecision = Stochastic.ProviderDecisions[index];
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
                    CanonicalJson.Sha256(Stochastic.DecisionTranscript[index]) ||
                CanonicalJson.Sha256(ReferenceNatureChanceLawDecisionV111
                    .FromSelection(law, request, selection, Origin, labels)) !=
                CanonicalJson.Sha256(savedDecision))
                throw new InvalidDataException(
                    "ReferenceNature merchant law decisions do not replay.");
            expectedNatureState = selection.NextState;
        }

        var replay = Replay(Origin, Action, Stochastic.ProviderDecisions);
        if ((replay.Continuation is null) != Complete ||
            !SameRequest(replay.Continuation?.PendingRequest, Stochastic.PendingRequest) ||
            !SameLabels(replay.Continuation?.PendingCandidateLabels,
                Stochastic.PendingCandidateLabels) ||
            CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(Stochastic.NatureState))
            throw new InvalidDataException(
                "ReferenceNature merchant continuation differs from its replay.");

        var expectedRun = replay.Run is { } completed
            ? ReferenceNatureRunCursorV111.Commit(completed, expectedNatureState)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run) ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, Stochastic.InitialNatureState, Stochastic.NatureState,
                Stochastic.DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature merchant run differs from its atomic cursor.");

        foreach (var decision in Stochastic.DecisionTranscript)
            decision.Validate();
    }

    private void ValidateToyBoxPurchasePrefix()
    {
        var raritySiteId = Origin.Stochastic.NativeRunRng is null
            ? "relic_grab_bag.rarity.ticket"
            : "relic_grab_bag.rarity.unit_float";
        var expectedSites = Enumerable.Range(0, 5)
            .SelectMany(_ => new[]
            {
                raritySiteId,
                PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId
            }).ToArray();
        if (Stochastic.ProviderDecisions.Length > expectedSites.Length)
            throw new InvalidDataException(
                "Toy Box purchase replay differs from its five rarity/identity request pairs.");
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var decision = Stochastic.ProviderDecisions[index];
            if (decision is null ||
                AsRequest(decision).SiteId != expectedSites[index])
                throw new InvalidDataException(
                    "Toy Box purchase replay differs from its five rarity/identity request pairs.");
        }
        if (Stochastic.PendingRequest is { } pending &&
            (Stochastic.ProviderDecisions.Length >= expectedSites.Length ||
             pending.SiteId != expectedSites[Stochastic.ProviderDecisions.Length]))
            throw new InvalidDataException(
                "Toy Box purchase replay differs from its five rarity/identity request pairs.");

        if (!Complete)
            return;
        var reward = Run.World?.Reward;
        if (Stochastic.ProviderDecisions.Length != expectedSites.Length ||
            reward is not
                { SourceRoom: "Relic:ToyBox", ExtraRelicRewardIds: { Length: 5 } })
            throw new InvalidDataException(
                "Completed Toy Box purchase does not contain all five controlled relic offers.");
    }

    private void ValidateLegacyAlchemicalCofferPurchasePrefix()
    {
        var expectedSites = Enumerable.Range(0, 4)
            .SelectMany(_ => new[]
            {
                PrototypeNativePotionShop.RarityRollSiteId,
                PrototypeNativePotionShop.ModelWithinRaritySiteId
            }).ToArray();
        if (Stochastic.ProviderDecisions.Length > expectedSites.Length)
            throw new InvalidDataException(
                "Legacy Alchemical Coffer purchase differs from its four rarity/model pairs.");
        for (var index = 0; index < Stochastic.ProviderDecisions.Length; index++)
        {
            var decision = Stochastic.ProviderDecisions[index];
            if (decision is null ||
                AsRequest(decision).SiteId != expectedSites[index])
                throw new InvalidDataException(
                    "Legacy Alchemical Coffer purchase differs from its four rarity/model pairs.");
        }
        if (Stochastic.PendingRequest is { } pending &&
            (Stochastic.ProviderDecisions.Length >= expectedSites.Length ||
             pending.SiteId != expectedSites[Stochastic.ProviderDecisions.Length]))
            throw new InvalidDataException(
                "Legacy Alchemical Coffer purchase differs from its four rarity/model pairs.");
        if (Complete && Stochastic.ProviderDecisions.Length != expectedSites.Length)
            throw new InvalidDataException(
                "Completed legacy Alchemical Coffer purchase is missing potion draws.");
    }

    public static string Save(ReferenceNatureMerchantSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureMerchantSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.Stochastic.InitialNatureState, session.Stochastic.NatureState,
            session.Stochastic.ProviderDecisions.Select(decision => decision.Fork()).ToArray(),
            (ReferenceNatureMerchantLawDecisionV111[])
                session.Stochastic.DecisionTranscript.Clone(),
            session.Stochastic.PendingRequest?.Fork(),
            session.Stochastic.PendingCandidateLabels is null ? null :
                (string[])session.Stochastic.PendingCandidateLabels.Clone());
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
        if (IsLegacyNativeMerchantFallbackBoundary(source, action) &&
            StochasticSiteRegistryV111
                .IsLegacyNativeMerchantFallbackSite(siteId))
            return QueryLegacyNativeMerchantFallback(
                source, action, request, candidateLabels, prefix);

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
                    EventId = MerchantRequestEventId(source, action,
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
                MerchantRequestEventId(source, action, request, labels,
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

    private static ChanceLawV111 QueryLegacyNativeMerchantFallback(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        if (request.IndexRequest is not { } indexRequest)
            throw new InvalidDataException(
                "Legacy Native merchant fallback requires an index request.");
        ValidateLegacyNativeMerchantRequest(
            source, action, indexRequest, candidateLabels, prefix);

        if (indexRequest.SiteId ==
            PrototypeNativePotionShop.RarityRollSiteId)
        {
            _ = ValidateLegacyPotionFactoryRequest(
                source, action, indexRequest, candidateLabels, prefix);
            return ChanceLawEngineV111.QueryRandomIndex(
                MerchantRequestEventId(source, action, request,
                    candidateLabels, prefix), indexRequest);
        }
        if (indexRequest.SiteId ==
            PrototypeNativePotionShop.ModelWithinRaritySiteId)
        {
            var candidates = ValidateLegacyPotionFactoryRequest(
                source, action, indexRequest, candidateLabels, prefix);
            var eventId = MerchantRequestEventId(source, action, request,
                candidates, prefix);
            var cofferPurchase = IsAlchemicalCofferPurchase(source, action);
            return ChanceLawEngineV111.QueryUniformLabeledIndexSupport(
                eventId, indexRequest, candidates,
                cofferPurchase
                    ? "legacy-alchemical-coffer-potion-model-v1"
                    : "legacy-native-shop-potion-model-v1",
                "Uniform index over the source-ordered, rarity-conditioned remaining potion models; pool completeness and legacy stream probabilities remain unverified.");
        }

        if (IsToyBoxPurchase(source, action))
        {
            if (indexRequest.SiteId == "relic_grab_bag.rarity.ticket")
                return ChanceLawEngineV111.QueryRandomIndex(
                    MerchantRequestEventId(source, action, request,
                        candidateLabels, prefix), indexRequest);

            if (indexRequest.SiteId == PrototypeNativeRelicGrabBag
                    .NativeTreasureModelSiteId)
            {
                var labels = candidateLabels ?? throw new InvalidDataException(
                    "Native-regional Toy Box relic selection lost its identity labels.");
                return ChanceLawEngineV111.QueryUniformLabeledIndexSupport(
                    MerchantRequestEventId(source, action, request,
                        labels, prefix), indexRequest, labels,
                    "native-regional-toy-box-hidden-relic-identity-v1",
                    "Uniform identity over the captured sorted remaining relic-bag support; source pool completeness and legacy stream probabilities remain unverified.");
            }
        }

        if (indexRequest.SiteId is
                "shop.merchant.card_price_modifier.index_draw" or
                "shop.merchant.card_upgrade_roll.ticket" or
                "shop.merchant.potion_price_modifier.index_draw" or
                "shop.merchant.relic_price_modifier.index_draw")
            return ChanceLawEngineV111.QueryLegacyMerchantIndexRequest(
                indexRequest.SiteId, indexRequest.OwnerId,
                indexRequest.Ordinal, indexRequest.SupportCount);

        return ChanceLawEngineV111.QueryRandomIndex(
            MerchantRequestEventId(source, action, request,
                candidateLabels, prefix), indexRequest);
    }

    private static void ValidateLegacyNativeMerchantRequest(
        RunState source, GameAction action,
        RandomIndexRequestV111 request, string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        if (!StochasticSiteRegistryV111
                .IsLegacyNativeMerchantFallbackSite(request.SiteId))
            throw new NotSupportedException(
                $"Legacy Native merchant replay does not support stochastic site '{request.SiteId}'.");

        var isLegacyPotionFactorySite = request.SiteId is
            PrototypeNativePotionShop.RarityRollSiteId or
            PrototypeNativePotionShop.ModelWithinRaritySiteId;
        var expectedStream = request.SiteId is "card_rarity.roll.ticket" or
            "relic_grab_bag.rarity.ticket" or
            PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId
                ? "reward" :
            isLegacyPotionFactorySite &&
                IsAlchemicalCofferPurchase(source, action)
                ? "event" : "shop";
        var expectedBound = request.SiteId switch
        {
            "shop.merchant.sale_slot.index_draw" => 5,
            "card_rarity.roll.ticket" or
                "shop.merchant.card_upgrade_roll.ticket" => 10000,
            "shop.merchant.card_price_modifier.index_draw" or
                "shop.merchant.potion_price_modifier.index_draw" => 11,
            "shop.merchant.relic_price_modifier.index_draw" => 31,
            "potion.rarity_roll.ticket" or
                "relic_grab_bag.rarity.ticket" => 100,
            _ => request.SupportCount
        };
        var boundedInteger = request.SiteId is "card_rarity.roll.ticket" or
            "shop.merchant.card_upgrade_roll.ticket" or
            "potion.rarity_roll.ticket" or
            "relic_grab_bag.rarity.ticket";
        var expectedOperation = boundedInteger
            ? "bounded_integer" : "choose_index";
        if (request.StreamId != expectedStream ||
            request.OperationKind != expectedOperation ||
            request.SupportCount != expectedBound ||
            request.OrderedCandidates.Length !=
                (boundedInteger ? 0 : expectedBound) ||
            !boundedInteger && !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, expectedBound)) ||
            candidateLabels is not null &&
                (candidateLabels.Length != request.SupportCount ||
                 candidateLabels.Any(string.IsNullOrWhiteSpace) ||
                 candidateLabels.Distinct(StringComparer.Ordinal).Count() !=
                    candidateLabels.Length))
            throw new InvalidDataException(
                "Legacy Native merchant request differs from its declared stream and support.");

        if (request.SiteId == "shop.merchant.sale_slot.index_draw" &&
            source.World is { } world && request.OwnerId !=
                $"act:{world.Act}:floor:{world.Floor}")
            throw new InvalidDataException(
                "Legacy Native merchant sale-slot owner differs from the current Shop.");

        if (isLegacyPotionFactorySite)
        {
            if (candidateLabels is not null)
                throw new InvalidDataException(
                    "Legacy PotionFactory requests use an owner-bound support without candidate labels.");
        }
        else if (request.SiteId ==
                PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId)
        {
            if (!IsToyBoxPurchase(source, action) ||
                candidateLabels is not { Length: > 0 } labels)
                throw new InvalidDataException(
                    "Legacy Native merchant treasure identity requests are limited to Toy Box acquisition.");
            ValidateToyBoxLegacyIdentityPrefix(
                source, request, labels, prefix);
        }
        else if (request.SiteId ==
                PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId)
        {
            if (candidateLabels is not { Length: > 0 } labels ||
                source.World is not { } merchantWorld)
                throw new InvalidDataException(
                    "Legacy Native merchant relic identity request lost its ordered candidates.");
            var ordinal = ParseOrdinal(request.OwnerId,
                ":merchant_relic:", ":rarity:");
            var rarityText = SegmentAfter(request.OwnerId,
                ":rarity:", ":pool:");
            if (ordinal is < 0 or >= 3 ||
                !Enum.TryParse<PrototypeRelicRarity>(rarityText,
                    ignoreCase: false, out var rarity) ||
                !Enum.IsDefined(rarity) ||
                !labels.SequenceEqual(labels.OrderBy(
                    label => label, StringComparer.Ordinal),
                    StringComparer.Ordinal) ||
                request.OwnerId != PrototypeNativeRelicGrabBag
                    .MerchantModelOwnerId(
                        merchantWorld, ordinal, rarity, labels))
                throw new InvalidDataException(
                    "Legacy Native merchant relic identity owner differs from its canonical rarity support.");
        }
        else if (request.SiteId == "relic_grab_bag.rarity.ticket")
        {
            if (IsToyBoxPurchase(source, action))
            {
                if (request.OwnerId != PrototypeNativeRelicGrabBag
                        .NativeTreasureRarityOwnerId + ":reward" ||
                    candidateLabels is not { Length: 100 } rarityTickets ||
                    !rarityTickets.SequenceEqual(
                        PrototypeNativeRelicGrabBag.LegacyRarityTicketLabels(),
                        StringComparer.Ordinal))
                    throw new InvalidDataException(
                        "Legacy Native-regional Toy Box rarity request differs from its relic-bag draw.");
            }
            else
            {
                var merchantOrdinal = ParseOrdinal(request.OwnerId,
                    ":merchant_relic:", ":merchant");
                if (merchantOrdinal is < 0 or >= 2 ||
                    candidateLabels is not { Length: 100 } rarityTickets ||
                    !rarityTickets.SequenceEqual(Enumerable.Range(0, 100)
                        .Select(ticket => (ticket < 50 ? "common:" :
                            ticket < 83 ? "uncommon:" : "rare:") +
                            ticket.ToString(CultureInfo.InvariantCulture)),
                        StringComparer.Ordinal))
                    throw new InvalidDataException(
                        "Legacy Native merchant relic rarity request differs from its two rolled offers.");
            }
        }
        else if (candidateLabels is not null)
        {
            throw new InvalidDataException(
                "Legacy Native merchant request has unexpected candidate labels.");
        }
    }

    private static string[] ValidateLegacyPotionFactoryRequest(
        RunState source, GameAction action, RandomIndexRequestV111 request,
        string[]? candidateLabels,
        ControlledStochasticDecisionV111[] prefix)
    {
        var isCofferPurchase = IsAlchemicalCofferPurchase(source, action);
        var expectedStream = isCofferPurchase ? "event" : "shop";
        var drawCount = isCofferPurchase ? 4 : 3;
        if (!IsLegacyNativeMerchantFallbackBoundary(source, action) ||
            candidateLabels is not null ||
            request.SiteId is not (PrototypeNativePotionShop.RarityRollSiteId or
                PrototypeNativePotionShop.ModelWithinRaritySiteId) ||
            request.StreamId != expectedStream)
            throw new InvalidDataException(
                "Legacy PotionFactory request differs from its Native merchant or Alchemical Coffer boundary.");

        var excluded = new List<string>();
        PrototypeNativePotionRarity? pendingRarity = null;
        var pickCount = 0;
        foreach (var decision in prefix)
        {
            if (decision.IndexDecision is not { } previous)
                continue;
            if (previous.Request.SiteId ==
                PrototypeNativePotionShop.RarityRollSiteId)
            {
                if (pendingRarity is not null || pickCount >= drawCount ||
                    decision.CandidateLabels is not null ||
                    !IsLegacyPotionRarityRequest(
                        previous.Request, expectedStream, pickCount))
                    throw new InvalidDataException(
                        "Legacy PotionFactory prefix has an invalid rarity ticket.");
                if ((uint)previous.SelectedIndex >= 100)
                    throw new InvalidDataException(
                        "Legacy PotionFactory rarity ticket exceeds its 100-way support.");
                pendingRarity = ResolveLegacyPotionRarity(
                    previous.SelectedIndex);
                pickCount++;
                continue;
            }

            if (previous.Request.SiteId !=
                PrototypeNativePotionShop.ModelWithinRaritySiteId)
                continue;
            if (pendingRarity is not { } previousRarity || pickCount == 0 ||
                decision.CandidateLabels is not null)
                throw new InvalidDataException(
                    "Legacy PotionFactory model prefix has no preceding rarity ticket.");
            var previousCandidates = PrototypeNativePotionShop
                .CandidatesForRarity(previousRarity, excluded);
            var previousPick = pickCount - 1;
            if (!IsLegacyPotionModelRequest(
                    previous.Request, expectedStream, previousPick,
                    previousRarity, previousCandidates) ||
                (uint)previous.SelectedIndex >=
                    (uint)previousCandidates.Length)
                throw new InvalidDataException(
                    "Legacy PotionFactory model prefix differs from its remaining rarity-conditioned pool.");
            excluded.Add(previousCandidates[previous.SelectedIndex]);
            pendingRarity = null;
        }

        if (request.SiteId == PrototypeNativePotionShop.RarityRollSiteId)
        {
            if (pendingRarity is not null || pickCount >= drawCount ||
                !IsLegacyPotionRarityRequest(
                    request, expectedStream, pickCount))
                throw new InvalidDataException(
                    "Legacy PotionFactory rarity request differs from its sequential offer prefix.");
            return [];
        }

        if (pendingRarity is not { } currentRarity || pickCount == 0)
            throw new InvalidDataException(
                "Legacy PotionFactory model request has no preceding rarity ticket.");
        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            currentRarity, excluded);
        if (!IsLegacyPotionModelRequest(
                request, expectedStream, pickCount - 1,
                currentRarity, candidates))
            throw new InvalidDataException(
                "Legacy PotionFactory model request differs from its remaining rarity-conditioned pool.");
        return candidates;
    }

    private static bool IsLegacyPotionRarityRequest(
        RandomIndexRequestV111 request, string streamId, int pickOrdinal)
    {
        var ownerId = $"potion:weighted_distinct:pick:{pickOrdinal}:rarity";
        return request.SiteId == PrototypeNativePotionShop.RarityRollSiteId &&
            request.StreamId == streamId &&
            request.OperationKind == "bounded_integer" &&
            request.SupportCount == 100 &&
            request.OrderedCandidates.Length == 0 &&
            request.OwnerId == ownerId;
    }

    private static bool IsLegacyPotionModelRequest(
        RandomIndexRequestV111 request, string streamId, int pickOrdinal,
        PrototypeNativePotionRarity rarity, string[] candidates)
    {
        var ownerId = $"potion:weighted_distinct:pick:{pickOrdinal}" +
            $":rarity:{rarity}:pool:{CanonicalJson.Sha256(candidates)}";
        return candidates.Length > 0 &&
            request.SiteId == PrototypeNativePotionShop
                .ModelWithinRaritySiteId &&
            request.StreamId == streamId &&
            request.OperationKind == "choose_index" &&
            request.SupportCount == candidates.Length &&
            request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)) &&
            request.OwnerId == ownerId;
    }

    private static PrototypeNativePotionRarity ResolveLegacyPotionRarity(
        int ticket) => ticket < 10
            ? PrototypeNativePotionRarity.Rare
            : ticket < 35
                ? PrototypeNativePotionRarity.Uncommon
                : PrototypeNativePotionRarity.Common;

    private static bool IsLegacyNativeMerchantFallbackBoundary(
        RunState source, GameAction action) =>
        source.Stochastic.NativeRunRng is null &&
        source.World is { } world &&
        PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
        ((source.Phase == RunPhase.MapChoice &&
            action.Kind == "choose_map_node" &&
            PrototypeNativeWingedBoots.AvailableNodes(world, source.Player)
                .Any(node => node.NodeId ==
                        action.ReadPayload<ChooseMapNodePayload>().NodeId &&
                    node.RoomType == PrototypeRoomType.Shop)) ||
         ControlledRunActionEngineV111.IsShopPurchaseAction(
             source, action));

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
        if (source.World is not { } world ||
            source.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world))
            throw new InvalidOperationException(
                "ReferenceNature merchant session requires a Native-regional Shop state.");
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
            source.Stochastic.NativeRunRng is not null &&
            ControlledRunActionEngineV111.IsShopPurchaseAction(source, action)
            && source.World?.Shop is not null;
        var validLegacyNativeShopPurchase =
            source.Stochastic.NativeRunRng is null &&
            ControlledRunActionEngineV111.IsShopPurchaseAction(
                source, action) &&
            source.World?.Shop is not null;
        if ((!validShopEntry && !validConfiguredShopPurchase &&
                !validLegacyNativeShopPurchase) || !legal)
            throw new InvalidOperationException(
                "ReferenceNature merchant session requires a current legal Native Shop entry, configured Shop purchase, or Native-regional Shop purchase without Native RNG.");
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

    private static string MerchantRequestEventId(
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

    private static void ValidateToyBoxLegacyIdentityPrefix(
        RunState source, RandomIndexRequestV111 request,
        string[] candidateLabels, ControlledStochasticDecisionV111[] prefix)
    {
        if (source.World is not { } world ||
            request.SiteId != PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId ||
            request.StreamId != "reward" ||
            request.OperationKind != "choose_index")
            throw new InvalidDataException(
                "Legacy Native-regional Toy Box identity request has an invalid site boundary.");

        var bag = world.Stochastic.RelicBag ??
            PrototypeNativeRelicGrabBag.CreateNatureUnordered(source.Player);
        if (bag.OrderMode != PrototypeRelicBagOrderMode.NatureUnordered)
            throw new InvalidDataException(
                "Legacy Native-regional Toy Box replay requires a Nature-owned relic identity set.");
        var rewardPlayer = source.Player.Relics.Any(relic =>
                relic.RelicId == NativeAncientRewardsV111.ToyBoxId)
            ? source.Player
            : source.Player with
            {
                Relics = source.Player.Relics.Append(new RelicInstance(
                    NativeAncientRewardsV111.ToyBoxId,
                    PrototypeJson.EmptyObject())).ToArray()
            };

        PrototypeRelicRarity? pendingRarity = null;
        foreach (var decision in prefix)
        {
            if (decision.IndexDecision is not { } indexDecision)
                continue;
            if (indexDecision.Request.SiteId == "relic_grab_bag.rarity.ticket")
            {
                if (pendingRarity is not null ||
                    indexDecision.Request.OwnerId !=
                        PrototypeNativeRelicGrabBag.NativeTreasureRarityOwnerId +
                            ":reward" ||
                    indexDecision.SelectedIndex >= 100 ||
                    decision.CandidateLabels is not { } ticketLabels ||
                    !ticketLabels.SequenceEqual(
                        PrototypeNativeRelicGrabBag.LegacyRarityTicketLabels(),
                        StringComparer.Ordinal))
                    throw new InvalidDataException(
                        "Legacy Native-regional Toy Box prefix has an invalid relic rarity ticket.");
                pendingRarity = ResolveLegacyRelicRarity(
                    indexDecision.SelectedIndex);
                continue;
            }

            if (indexDecision.Request.SiteId !=
                PrototypeNativeRelicGrabBag.NativeTreasureModelSiteId)
                continue;
            if (pendingRarity is not { } priorRarity ||
                decision.CandidateLabels is not { } priorLabels)
                throw new InvalidDataException(
                    "Legacy Native-regional Toy Box identity prefix has no preceding rarity ticket.");
            var priorPlan = PrototypeNativeRelicGrabBag.PlanNaturePull(
                bag, priorRarity, merchant: false, rewardPlayer);
            if (priorPlan.SelectedTier is null ||
                !priorLabels.SequenceEqual(
                    priorPlan.CandidateIds, StringComparer.Ordinal) ||
                indexDecision.Request.OwnerId !=
                    PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                        world, priorRarity, priorPlan.CandidateIds))
                throw new InvalidDataException(
                    "Legacy Native-regional Toy Box identity prefix differs from its residual bag support.");
            (bag, _) = PrototypeNativeRelicGrabBag.PullNatureSelected(
                bag, priorRarity, priorLabels,
                indexDecision.SelectedIndex, merchant: false, rewardPlayer);
            pendingRarity = null;
        }

        if (pendingRarity is not { } currentRarity)
            throw new InvalidDataException(
                "Legacy Native-regional Toy Box identity request has no preceding rarity ticket.");
        var currentPlan = PrototypeNativeRelicGrabBag.PlanNaturePull(
            bag, currentRarity, merchant: false, rewardPlayer);
        if (currentPlan.SelectedTier is null ||
            !candidateLabels.SequenceEqual(
                currentPlan.CandidateIds, StringComparer.Ordinal) ||
            request.OwnerId != PrototypeNativeRelicGrabBag.TreasureModelOwnerId(
                world, currentRarity, currentPlan.CandidateIds))
            throw new InvalidDataException(
                "Legacy Native-regional Toy Box identity request differs from its residual bag support.");
    }

    private static PrototypeRelicRarity ResolveLegacyRelicRarity(int ticket) =>
        ticket < 50 ? PrototypeRelicRarity.Common :
        ticket < 83 ? PrototypeRelicRarity.Uncommon :
            PrototypeRelicRarity.Rare;

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
