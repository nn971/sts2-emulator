using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string NativeMerchantIndexConditioningPolicyId =
        "uniform-native-merchant-index-support-v1";

    public const string NativeMerchantThresholdConditioningPolicyId =
        "source-threshold-native-merchant-category-v1";

    public const string NativeMerchantPriceConditioningPolicyId =
        "native-merchant-rounded-price-modifier-v1";

    /// <summary>
    /// Query discrete native merchant offer decisions before entering an
    /// available configured Shop node. Bounded-index transforms are
    /// source-verified on the declared support; model-pool completeness and
    /// threshold/float provenance remain separate claims.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantOffer(
        RunState run, string siteId, int ordinal,
        string? cardType = null, string? rarity = null,
        string[]? excludedPotionIds = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var world = run.World!;
        var actFloor = $"act:{world.Act}:floor:{world.Floor}";
        string ownerId;
        string operationKind;
        string policy;
        string provenance;
        var probabilityStatus = ChanceLawV111.UnverifiedStatus;
        object conditioning;
        ChanceLawOutcomeV111[] outcomes;

        if (siteId == "shop.merchant.sale_slot.index_draw")
        {
            if (ordinal != 0 || cardType is not null || rarity is not null ||
                excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant sale-slot queries use ordinal 0 and no pool parameters.");
            ownerId = $"merchant:act:{world.Act}:floor:{world.Floor}";
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new { siteId, ownerId, candidateCount = 5 };
            var weights = NativeNextIntGridWeights(5);
            outcomes = Enumerable.Range(0, 5)
                .Select(index => new ChanceLawOutcomeV111(
                    "sale_slot:" + index.ToString(CultureInfo.InvariantCulture),
                    weights[index]))
                .ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "card_rarity.roll.unit_float")
        {
            var type = ParseMerchantCardType(cardType);
            if (rarity is not null || excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant card-rarity queries accept only card_type.");
            var count = MerchantCardCount(type);
            if (ordinal >= count)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            var (rareThreshold, uncommonEnd) =
                PrototypeNativeCardRarityOdds.MerchantFloatThresholds(
                    world.CardRarityOffsetBasisPoints, run.Ascension);
            var weights = rareThreshold >= 10000
                ? (Rare: NativeNextDoubleGridSize, Uncommon: 0L, Common: 0L)
                : NativeCardRarityGridWeights(
                    rareThreshold, uncommonEnd);
            ownerId = $"type:{type}:offer:{ordinal}:offset:{world.CardRarityOffsetBasisPoints}";
            operationKind = "unit_float_threshold";
            policy = NativeMerchantThresholdConditioningPolicyId;
            provenance = NativeUnitFloatThresholdSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, cardType = type.ToString(),
                world.CardRarityOffsetBasisPoints, run.Ascension,
                rareThreshold, uncommonEnd, weights
            };
            outcomes = new[]
                {
                    new ChanceLawOutcomeV111("rarity:rare", weights.Rare),
                    new ChanceLawOutcomeV111("rarity:uncommon", weights.Uncommon),
                    new ChanceLawOutcomeV111("rarity:common", weights.Common)
                }
                .Where(outcome => outcome.Weight > 0).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "shop.merchant.card_model.index_draw")
        {
            var type = ParseMerchantCardType(cardType);
            var selectedRarity = ParseCardRarity(rarity);
            if (excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant card-model queries do not accept potion exclusions.");
            var count = MerchantCardCount(type);
            if (ordinal >= count)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            var candidates = PrototypeContent.NativeSilentRewardCardPool
                .Where(id => PrototypeContent.Card(id).Type == type &&
                    PrototypeContent.Card(id).Rarity == selectedRarity)
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "Native merchant card pool has no models for the requested rarity.");
            ownerId = $"type:{type}:offer:{ordinal}:rarity:{selectedRarity}:pool:" +
                CanonicalJson.Sha256(candidates);
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, cardType = type.ToString(),
                rarity = selectedRarity.ToString(), candidates
            };
            var weights = NativeNextIntGridWeights(candidates.Length);
            outcomes = candidates.Select((id, index) =>
                new ChanceLawOutcomeV111(
                    $"card_model:{id}:index:{index}", weights[index])).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "shop.colorless_card_model.index_draw")
        {
            if (cardType is not null || excludedPotionIds is not null)
                throw new ArgumentException(
                    "Colorless merchant model queries accept only ordinal and optional rarity.");
            var selectedRarity = ordinal switch
            {
                0 => PrototypeCardRarity.Uncommon,
                1 => PrototypeCardRarity.Rare,
                _ => throw new ArgumentOutOfRangeException(nameof(ordinal))
            };
            if (rarity is not null &&
                ParseCardRarity(rarity) != selectedRarity)
                throw new ArgumentException(
                    "Colorless merchant offer rarity is fixed by its ordinal.", nameof(rarity));
            var profile = run.Configuration!.Profile;
            var candidates = PrototypeColorlessCards.MerchantPoolForProfile(
                selectedRarity, profile.FullyUnlocked,
                profile.UnlockedColorlessEpochs);
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "Native merchant colorless pool is empty for this rarity.");
            ownerId = $"rarity:{selectedRarity}:pool:" +
                CanonicalJson.Sha256(candidates);
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, ordinal,
                rarity = selectedRarity.ToString(), candidates
            };
            var weights = NativeNextIntGridWeights(candidates.Length);
            outcomes = candidates.Select((id, index) =>
                new ChanceLawOutcomeV111(
                    $"card_model:{id}:index:{index}", weights[index])).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "potion_factory.rarity_roll.unit_float")
        {
            if (ordinal >= 3 || cardType is not null || rarity is not null ||
                excludedPotionIds is not null)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            ownerId = $"merchant:act:{world.Act}:floor:{world.Floor}:potions";
            operationKind = "unit_float_threshold";
            policy = NativeMerchantThresholdConditioningPolicyId;
            provenance = NativeUnitFloatThresholdSourceVerifiedProvenance;
            var (commonWeight, uncommonWeight, rareWeight) =
                NativePotionFactoryRarityGridWeights();
            conditioning = new
            {
                siteId, ownerId, ordinal,
                rareInclusiveThreshold = 0.1f,
                uncommonInclusiveThreshold = 0.35f,
                common = commonWeight,
                uncommon = uncommonWeight,
                rare = rareWeight
            };
            outcomes =
            [
                new ChanceLawOutcomeV111("rarity:common", commonWeight),
                new ChanceLawOutcomeV111("rarity:uncommon", uncommonWeight),
                new ChanceLawOutcomeV111("rarity:rare", rareWeight)
            ];
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "potion_factory.model_within_rarity.index_draw")
        {
            if (cardType is not null || rarity is null || ordinal >= 3)
                throw new ArgumentException(
                    "Potion model queries require a supported rarity and offer ordinal 0-2.");
            if (excludedPotionIds is null)
                excludedPotionIds = [];
            if (excludedPotionIds.Any(string.IsNullOrWhiteSpace) ||
                excludedPotionIds.Distinct(StringComparer.Ordinal).Count() !=
                    excludedPotionIds.Length || excludedPotionIds.Length != ordinal ||
                excludedPotionIds.Any(id =>
                    !PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                        id, StringComparer.Ordinal)))
                throw new ArgumentException(
                    "excluded_potion_ids must contain exactly one unique native potion ID per preceding offer.");
            var selectedRarity = ParsePotionRarity(rarity);
            var candidates = PrototypeNativePotionShop.CandidatesForRarity(
                selectedRarity, excludedPotionIds);
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "Native merchant potion pool is empty for the requested rarity and exclusions.");
            ownerId = $"merchant:act:{world.Act}:floor:{world.Floor}:potions";
            operationKind = "choose_index";
            policy = NativeMerchantIndexConditioningPolicyId;
            provenance = NativeBoundedIndexSourceVerifiedProvenance;
            conditioning = new
            {
                siteId, ownerId, ordinal,
                rarity = selectedRarity.ToString(),
                excludedPotionIds, candidates
            };
            var weights = NativeNextIntGridWeights(candidates.Length);
            outcomes = candidates.Select((id, index) =>
                new ChanceLawOutcomeV111(
                    $"potion_model:{id}:index:{index}", weights[index])).ToArray();
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else if (siteId == "relic_grab_bag.rarity.unit_float")
        {
            if (ordinal >= 2 || cardType is not null || rarity is not null ||
                excludedPotionIds is not null)
                throw new ArgumentException(
                    "Merchant relic-rarity queries support only the first two offer ordinals.");
            ownerId = $"{actFloor}:merchant_relic:{ordinal}:merchant";
            operationKind = "unit_float_threshold";
            policy = NativeMerchantThresholdConditioningPolicyId;
            provenance = NativeUnitFloatThresholdSourceVerifiedProvenance;
            var weights = NativeRelicRarityGridWeights();
            conditioning = new
            {
                siteId, ownerId, ordinal,
                commonThreshold = PrototypeNativeRelicGrabBag
                    .CommonRarityWeight /
                    (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
                uncommonThreshold = (PrototypeNativeRelicGrabBag
                    .CommonRarityWeight +
                    PrototypeNativeRelicGrabBag.UncommonRarityWeight) /
                    (float)PrototypeNativeRelicGrabBag.RarityWeightScale,
                weights
            };
            outcomes =
            [
                new ChanceLawOutcomeV111("rarity:common", weights.Common),
                new ChanceLawOutcomeV111("rarity:uncommon", weights.Uncommon),
                new ChanceLawOutcomeV111("rarity:rare", weights.Rare)
            ];
            probabilityStatus = ChanceLawV111.SourceVerifiedStatus;
        }
        else
        {
            throw new NotSupportedException(
                $"Native merchant Chance Law query does not support site '{siteId}'.");
        }

        return CreateNativeBoundaryLaw(
            siteId, operationKind, ownerId, ordinal, policy,
            CanonicalJson.Sha256(conditioning), provenance, outcomes,
            probabilityStatus);
    }

    /// <summary>
    /// Query the next identity from a Native merchant relic queue while its
    /// order remains private. The ordered support is canonicalized by the
    /// caller and checked against the source fallback plan after prior
    /// session selections have been removed.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantRelicSelection(
        RunState run, RandomIndexRequestV111 request,
        int offerOrdinal, PrototypeRelicRarity rarity,
        string[] candidateIds, string[] excludedRelicIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateIds);
        ArgumentNullException.ThrowIfNull(excludedRelicIds);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        request.Validate();
        var world = run.World!;
        if (request.SiteId !=
                PrototypeNativeRelicGrabBag.NativeMerchantModelSiteId ||
            request.StreamId != NativePlayerRngV111.ShopsStreamId ||
            request.OperationKind != "choose_index" ||
            offerOrdinal is < 0 or >= 3 ||
            !Enum.IsDefined(rarity) ||
            candidateIds.Length == 0 ||
            request.SupportCount != candidateIds.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidateIds.Length)) ||
            !candidateIds.SequenceEqual(candidateIds.OrderBy(
                id => id, StringComparer.Ordinal), StringComparer.Ordinal) ||
            candidateIds.Any(string.IsNullOrWhiteSpace) ||
            candidateIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateIds.Length ||
            excludedRelicIds.Any(string.IsNullOrWhiteSpace) ||
            excludedRelicIds.Distinct(StringComparer.Ordinal).Count() !=
                excludedRelicIds.Length ||
            excludedRelicIds.Length > offerOrdinal ||
            excludedRelicIds.Any(id =>
                !PrototypeContent.RelicPool.Contains(id, StringComparer.Ordinal)))
            throw new ArgumentException(
                "Native merchant relic selection request is malformed.");

        var ownerId = PrototypeNativeRelicGrabBag.MerchantModelOwnerId(
            world, offerOrdinal, rarity, candidateIds);
        if (request.OwnerId != ownerId ||
            world.Stochastic.RelicBag is not { } bag)
            throw new InvalidDataException(
                "Native merchant relic request owner or bag differs from its boundary.");

        var excluded = excludedRelicIds.ToHashSet(StringComparer.Ordinal);
        static string[] RemoveSelected(string[] ids,
            HashSet<string> selected) => ids.Where(id => !selected.Contains(id))
            .ToArray();
        var conditionedBag = bag with
        {
            Common = RemoveSelected(bag.Common, excluded),
            Uncommon = RemoveSelected(bag.Uncommon, excluded),
            Rare = RemoveSelected(bag.Rare, excluded),
            Shop = RemoveSelected(bag.Shop, excluded)
        };
        var plan = PrototypeNativeRelicGrabBag.PlanPull(
            conditionedBag, rarity, merchant: true, run.Player);
        var expectedCandidates = plan.CandidateIds
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (plan.SelectedTier is null ||
            !candidateIds.SequenceEqual(expectedCandidates,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Native merchant relic support differs from the conditioned hidden queue.");

        var policy = NativeMerchantIndexConditioningPolicyId;
        var conditioning = new
        {
            request,
            offerOrdinal,
            rarity = rarity.ToString(),
            selectedTier = plan.SelectedTier.Value.ToString(),
            candidateIds,
            excludedRelicIds = excludedRelicIds.OrderBy(
                id => id, StringComparer.Ordinal).ToArray(),
            policy
        };
        var outcomes = candidateIds.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"relic_model:{id}:index:{index}", 1L)).ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, policy, CanonicalJson.Sha256(conditioning),
            "native-merchant-hidden-relic-queue-identity-unverified",
            outcomes);
    }

    /// <summary>
    /// Query the mapped integer price produced by one native merchant unit
    /// float. We count exact source-grid preimages through the unit-float
    /// rounding and the source price conversion.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantPrice(
        RunState run, string siteId, int ordinal,
        string? cardId = null, string? potionId = null,
        string? relicId = null, bool colorlessOffer = false,
        bool saleReprice = false)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var world = run.World!;
        var actFloor = $"act:{world.Act}:floor:{world.Floor}";
        int baseCost;
        double minimumModifier;
        double maximumModifier;
        string ownerId;
        string itemId;
        if (siteId == "shop.merchant.card_price_modifier.unit_float")
        {
            if (cardId is null || potionId is not null || relicId is not null ||
                (colorlessOffer
                    ? ordinal >= 2 || !PrototypeColorlessCards.ImplementedShopPool
                        .Contains(cardId, StringComparer.Ordinal)
                    : ordinal >= 5 || !PrototypeContent.NativeSilentRewardCardPool
                        .Contains(cardId, StringComparer.Ordinal)) ||
                saleReprice && colorlessOffer)
                throw new ArgumentException(
                    "Card-price queries require a supported character or Colorless card and offer ordinal.");
            itemId = cardId;
            baseCost = PrototypeGameEngine.NativeMerchantCardBaseCost(cardId);
            minimumModifier = 0.95;
            maximumModifier = 1.05;
            ownerId = colorlessOffer
                ? $"{actFloor}:colorless_offer:{ordinal}:card:{cardId}"
                : saleReprice
                    ? $"{actFloor}:offer:{ordinal}:sale_reprice:{cardId}"
                    : $"{actFloor}:offer:{ordinal}:card:{cardId}";
        }
        else if (siteId == "shop.merchant.potion_price_modifier.unit_float")
        {
            if (potionId is null || cardId is not null || relicId is not null ||
                colorlessOffer || saleReprice || ordinal >= 3 ||
                !PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                    potionId, StringComparer.Ordinal))
                throw new ArgumentException(
                    "Potion-price queries require a native potion ID and offer ordinal 0-2.");
            itemId = potionId;
            baseCost = PrototypeNativePotionShop.MerchantBaseCost(potionId);
            minimumModifier = 0.95;
            maximumModifier = 1.05;
            ownerId = $"{actFloor}:offer:{ordinal}:potion:{potionId}:{potionId}";
        }
        else if (siteId == "shop.merchant.relic_price_modifier.unit_float")
        {
            if (relicId is null || cardId is not null || potionId is not null ||
                colorlessOffer || saleReprice || ordinal >= 3 ||
                !PrototypeNativeRelicGrabBag.TryGetRarity(relicId, out _))
                throw new ArgumentException(
                    "Relic-price queries require a native relic ID and offer ordinal 0-2.");
            itemId = relicId;
            baseCost = PrototypeNativeRelicGrabBag.MerchantBaseCost(relicId);
            minimumModifier = 0.85;
            maximumModifier = 1.15;
            ownerId = $"{actFloor}:offer:{ordinal}:relic:{relicId}:{relicId}";
        }
        else
        {
            throw new NotSupportedException(
                $"Native merchant price query does not support site '{siteId}'.");
        }

        int MapPrice(float roll)
        {
            if (siteId == "shop.merchant.card_price_modifier.unit_float")
            {
                var price = PrototypeGameEngine
                    .ResolveNativeMerchantCardPrice(itemId, roll);
                return saleReprice ? price / 2 : price;
            }
            if (siteId == "shop.merchant.potion_price_modifier.unit_float")
                return PrototypeNativePotionShop
                    .ResolveMerchantPriceNative(itemId, roll);
            if (siteId == "shop.merchant.relic_price_modifier.unit_float")
                return PrototypeNativeRelicGrabBag
                    .ResolveMerchantPriceNative(itemId, roll);
            throw new NotSupportedException(
                $"Native merchant price mapping does not support '{siteId}'.");
        }

        var outcomes = NativeNextFloatMappedIntegerWeights(MapPrice)
            .Select(pair => new ChanceLawOutcomeV111(
                "price:" + pair.Key.ToString(CultureInfo.InvariantCulture),
                pair.Value)).ToArray();
        var conditioning = new
        {
            siteId, ownerId, ordinal, itemId, baseCost,
            minimumModifier, maximumModifier, colorlessOffer, saleReprice,
            outcomes
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_mapped_price", ownerId, ordinal,
            NativeMerchantPriceConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatMappedOutcomeSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Merchant card creation consumes this float even though the pinned
    /// negative upgrade chance makes every value mechanically equivalent.
    /// The only semantic result is source-verified as no_effect; the raw float
    /// sample and Native cursor advancement remain outside this law.
    /// </summary>
    public static ChanceLawV111 QueryNativeMerchantNoEffectUnitFloat(
        RunState run, RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        PrototypeStateInvariants.Validate(run);
        ValidateNativeMerchantQueryBoundary(run);
        request.Validate();
        if (request.SiteId != "shop.merchant.card_upgrade_roll.unit_float" ||
            request.StreamId != NativePlayerRngV111.ShopsStreamId ||
            !request.OwnerId.StartsWith("type:", StringComparison.Ordinal) ||
            !request.OwnerId.Contains(":offer:", StringComparison.Ordinal) ||
            !request.OwnerId.Contains(":card:", StringComparison.Ordinal))
            throw new ArgumentException(
                "Merchant upgrade-roll request is malformed.");
        return CreateNativeBoundaryLaw(
            request.SiteId, "unit_float_no_effect", request.OwnerId,
            request.Ordinal, "native-merchant-no-visible-upgrade-effect-v1",
            CanonicalJson.Sha256(request),
            "native-merchant-upgrade-roll-always-has-no-mechanical-effect",
            [new ChanceLawOutcomeV111("no_effect", 1L)],
            ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one source-ordered potion-factory request made while buying an
    /// Alchemical Coffer from a configured Native shop. The request and exact
    /// legal purchase action bind the law; prior selected models condition the
    /// remaining rarity pool without consulting either run RNG stream.
    /// </summary>
    public static ChanceLawV111 QueryNativeAlchemicalCofferPotionRequest(
        RunState run, GameAction action,
        ControlledStochasticRequestV111 request,
        PrototypeNativePotionRarity? rarity,
        string[] excludedPotionIds)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(excludedPotionIds);
        PrototypeStateInvariants.Validate(run);
        request.Validate();
        if (run.Phase != RunPhase.Shop || run.World is not { } world ||
            run.Stochastic.NativeRunRng is null ||
            run.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            world.Stochastic.RelicBag is null || action.Kind != "buy_relic" ||
            world.Shop is not { } shop)
            throw new InvalidOperationException(
                "Alchemical Coffer potion laws require a configured Native Shop purchase boundary.");

        var payload = action.ReadPayload<BuyOfferPayload>();
        if (!shop.RelicOffers.Any(offer =>
                offer.OfferId == payload.OfferId && !offer.Sold &&
                offer.ItemId == PrototypeNativeOrobasRelics.AlchemicalCofferId) ||
            !new PrototypeGameEngine().GetLegalActions(run).Any(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) ==
                PrototypeAiEnvironment.StableActionId(action)))
            throw new InvalidOperationException(
                "Alchemical Coffer potion laws require its current legal relic purchase.");

        const string ownerId = "relic:alchemical_coffer:acquisition";
        const string streamId = "run.combat_potion_generation";
        if (request.StreamId != streamId || request.OwnerId != ownerId ||
            request.Ordinal >= 4)
            throw new InvalidDataException(
                "Alchemical Coffer potion request has an invalid source identity.");

        var actionFingerprint = PrototypeAiEnvironment.StableActionId(action);
        if (request.UnitFloatRequest is { } floatRequest &&
            floatRequest.SiteId == "potion_factory.rarity_roll.unit_float")
        {
            if (rarity is not null || excludedPotionIds.Length != 0)
                throw new ArgumentException(
                    "Coffer rarity laws do not accept a model-pool prefix.");
            var (common, uncommon, rare) =
                NativePotionFactoryRarityGridWeights();
            var conditioning = new
            {
                actionFingerprint, request = floatRequest,
                rareInclusiveThreshold = 0.1f,
                uncommonInclusiveThreshold = 0.35f,
                common, uncommon, rare
            };
            return CreateNativeBoundaryLaw(
                floatRequest.SiteId, "unit_float_threshold", ownerId,
                floatRequest.Ordinal,
                NativePotionFactoryThresholdConditioningPolicyId,
                CanonicalJson.Sha256(conditioning),
                NativeUnitFloatThresholdSourceVerifiedProvenance,
                [
                    new ChanceLawOutcomeV111("rarity:common", common),
                    new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
                    new ChanceLawOutcomeV111("rarity:rare", rare)
                ], ChanceLawV111.SourceVerifiedStatus);
        }

        if (request.IndexRequest is not { } indexRequest ||
            indexRequest.SiteId != "potion_factory.model_within_rarity.index_draw" ||
            rarity is null || excludedPotionIds.Length != indexRequest.Ordinal ||
            excludedPotionIds.Any(string.IsNullOrWhiteSpace) ||
            excludedPotionIds.Distinct(StringComparer.Ordinal).Count() !=
                excludedPotionIds.Length ||
            excludedPotionIds.Any(id =>
                !PrototypeNativePotionShop.NativePotionPoolOrder.Contains(
                    id, StringComparer.Ordinal)))
            throw new InvalidDataException(
                "Alchemical Coffer potion model request has an invalid rarity prefix.");

        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            rarity.Value, excludedPotionIds);
        if (candidates.Length == 0 ||
            indexRequest.SupportCount != candidates.Length ||
            !indexRequest.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)))
            throw new InvalidDataException(
                "Alchemical Coffer potion model support differs from its source pool.");
        var weights = NativeNextIntGridWeights(candidates.Length);
        var outcomes = candidates.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"potion_model:{id}:index:{index}", weights[index])).ToArray();
        var modelConditioning = new
        {
            actionFingerprint, request = indexRequest,
            rarity = rarity.Value.ToString(), excludedPotionIds, candidates
        };
        return CreateNativeBoundaryLaw(
            indexRequest.SiteId, indexRequest.OperationKind, ownerId,
            indexRequest.Ordinal,
            NativePotionFactoryIndexConditioningPolicyId,
            CanonicalJson.Sha256(modelConditioning),
            NativeBoundedIndexSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    private static void ValidateNativeMerchantQueryBoundary(RunState run)
    {
        if (run.Phase != RunPhase.MapChoice || run.World is not { } world ||
            run.Stochastic.NativeRunRng is null ||
            run.Stochastic.ControlledRunActionChoice is not null ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeNativeWingedBoots.AvailableNodes(world, run.Player)
                .Any(node => node.RoomType == PrototypeRoomType.Shop))
            throw new InvalidOperationException(
                "Native merchant Chance Laws require an available configured Shop node before entry.");
    }

    private static PrototypeCardType ParseMerchantCardType(string? value)
    {
        if (!Enum.TryParse<PrototypeCardType>(value, ignoreCase: true,
                out var parsed) || !Enum.IsDefined(parsed) ||
            parsed is not (PrototypeCardType.Attack or PrototypeCardType.Skill or
                PrototypeCardType.Power))
            throw new ArgumentException(
                "card_type must be Attack, Skill, or Power.", nameof(value));
        return parsed;
    }

    private static PrototypeCardRarity ParseCardRarity(string? value)
    {
        if (!Enum.TryParse<PrototypeCardRarity>(value, ignoreCase: true,
                out var parsed) || !Enum.IsDefined(parsed) ||
            parsed is not (PrototypeCardRarity.Common or
                PrototypeCardRarity.Uncommon or PrototypeCardRarity.Rare))
            throw new ArgumentException(
                "rarity must be Common, Uncommon, or Rare.", nameof(value));
        return parsed;
    }

    private static PrototypeNativePotionRarity ParsePotionRarity(string value)
    {
        if (!Enum.TryParse<PrototypeNativePotionRarity>(value, ignoreCase: true,
                out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                "Potion rarity must be Common, Uncommon, or Rare.", nameof(value));
        return parsed;
    }

    private static int MerchantCardCount(PrototypeCardType type) => type switch
    {
        PrototypeCardType.Attack or PrototypeCardType.Skill => 2,
        PrototypeCardType.Power => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    private static void ValidateLegacyShopCardType(
        PrototypeCardType requiredType)
    {
        if (requiredType is not (PrototypeCardType.Attack or
            PrototypeCardType.Skill or PrototypeCardType.Power))
            throw new ArgumentOutOfRangeException(nameof(requiredType));
    }

}
