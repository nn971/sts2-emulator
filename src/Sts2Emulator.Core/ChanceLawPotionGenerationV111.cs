namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    public const string NativePotionFactoryIndexConditioningPolicyId =
        "source-bounded-potion-factory-model-index-v1";
    public const string NativePotionFactoryThresholdConditioningPolicyId =
        "source-threshold-potion-factory-rarity-v1";

    /// <summary>Query the retained legacy 10/25/65 potion rarity ticket.</summary>
    public static ChanceLawV111 QueryLegacyPotionRarityTicket(
        string streamId, string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (streamId is not ("reward" or "event" or "shop"))
            throw new ArgumentException(
                "Legacy potion rarity tickets use reward, event, or shop streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = PrototypeNativePotionShop.RarityRollSiteId;
        var outcomes = LegacyRarityOutcomes(65, 25, 10);
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            ticketCount = 100,
            rareTicketEnd = 10,
            uncommonTicketEnd = 35
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-potion-rarity-ticket-v1", conditioning,
            "uniform 100-way legacy bounded-ticket model with source-shaped " +
            "10/25/65 rarity bins; probabilities remain unverified against " +
            "the native game RNG.", outcomes);
    }

    /// <summary>
    /// Query the retained legacy Uncommon-potion model index over its fixed
    /// source-ordered implemented pool.
    /// </summary>
    public static ChanceLawV111 QueryLegacyUncommonPotionModel(
        string streamId, string ownerPrefix, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        if (streamId is not ("event" or "reward"))
            throw new ArgumentException(
                "Legacy Uncommon-potion model requests use event or reward streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "potion.uncommon_model.index_draw";
        var pool = PrototypeNativePotionCourier.ImplementedUncommonPotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The implemented Uncommon-potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = ownerPrefix + ":pool:" + poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ownerPrefix,
            ordinal,
            orderedImplementedUncommonPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-uncommon-potion-model-pool-v1", conditioning,
            "Equal index weights over the ordered implemented Uncommon " +
            "potion pool; source-pool completeness and stream parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query a legacy potion model draw within a declared rarity after prior
    /// potion exclusions have been applied to the source-ordered pool.
    /// </summary>
    public static ChanceLawV111 QueryLegacyPotionModelWithinRarity(
        PrototypeNativePotionRarity rarity, string streamId,
        string ownerPrefix, string[] excludedPotionIds, int ordinal)
    {
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity));
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        ArgumentNullException.ThrowIfNull(excludedPotionIds);
        if (streamId is not ("event" or "reward" or "shop"))
            throw new ArgumentException(
                "Legacy potion model requests use event, reward, or shop streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var knownPotionIds = PrototypeNativePotionShop.NativePotionPoolOrder
            .ToHashSet(StringComparer.Ordinal);
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var potionId in excludedPotionIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(potionId);
            if (!knownPotionIds.Contains(potionId))
                throw new ArgumentException(
                    "Excluded potion IDs must belong to the supported potion pool.",
                    nameof(excludedPotionIds));
            excluded.Add(potionId);
        }

        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            rarity, excluded);
        if (candidates.Length == 0)
            throw new ArgumentException(
                "The declared rarity and exclusions leave no potion model to draw.",
                nameof(excludedPotionIds));
        var poolFingerprint = CanonicalJson.Sha256(candidates);
        var ownerId = ownerPrefix + ":rarity:" + rarity + ":pool:" +
            poolFingerprint;
        var outcomes = candidates.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId = PrototypeNativePotionShop.ModelWithinRaritySiteId,
            streamId,
            ownerId,
            ownerPrefix,
            ordinal,
            rarity,
            excludedPotionIds = excluded.Order(StringComparer.Ordinal)
                .ToArray(),
            orderedCandidates = candidates,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            PrototypeNativePotionShop.ModelWithinRaritySiteId, streamId,
            ownerId,
            ordinal, "legacy-potion-model-within-rarity-pool-v1",
            conditioning,
            "Equal index weights over the remaining source-ordered legacy " +
            "potion rarity pool; source-pool completeness and stream parity " +
            "remain unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query a legacy no-rarity potion draw over the complete ordered potion
    /// pool shared by uniform-offer source paths.
    /// </summary>
    public static ChanceLawV111 QueryLegacyUniformPotionModel(
        string streamId, string ownerPrefix, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPrefix);
        if (streamId is not ("event" or "reward"))
            throw new ArgumentException(
                "Legacy uniform potion requests use event or reward streams.",
                nameof(streamId));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = PrototypeNativePotionShop.UniformModelSiteId;
        var pool = PrototypeNativePotionShop.NativePotionPoolOrder;
        if (pool.Count == 0)
            throw new InvalidOperationException(
                "The legacy uniform potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = ownerPrefix + ":pool:" + poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ownerPrefix,
            ordinal,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-uniform-potion-model-pool-v1", conditioning,
            "Equal index weights over the ordered legacy uniform potion pool; " +
            "source-pool completeness and stream parity remain unverified.",
            outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the source-ordered PotionFactory rarity thresholds for an
    /// explicitly declared Native request, independent of its RNG cursor.
    /// </summary>
    public static ChanceLawV111 QueryNativePotionFactoryRarity(
        RandomUnitFloatRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        const string siteId = "potion_factory.rarity_roll.unit_float";
        if (request.SiteId != siteId || request.StreamId is not
            ("run.combat_potion_generation" or "player.rewards" or
             "player.shops"))
            throw new ArgumentException(
                "PotionFactory rarity requests require a recognized source stream.",
                nameof(request));

        var (common, uncommon, rare) =
            NativePotionFactoryRarityGridWeights();
        var outcomes = new[]
            {
                new ChanceLawOutcomeV111("rarity:common", common),
                new ChanceLawOutcomeV111("rarity:uncommon", uncommon),
                new ChanceLawOutcomeV111("rarity:rare", rare)
            }
            .Where(outcome => outcome.Weight > 0).ToArray();
        var conditioning = new
        {
            request,
            rareInclusiveThreshold = 0.1f,
            uncommonInclusiveThreshold = 0.35f,
            common,
            uncommon,
            rare
        };
        return CreateNativeBoundaryLaw(
            siteId, "unit_float_threshold", request.OwnerId,
            request.Ordinal, NativePotionFactoryThresholdConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            NativeUnitFloatThresholdSourceVerifiedProvenance,
            outcomes, ChanceLawV111.SourceVerifiedStatus);
    }

    /// <summary>
    /// Query one Native PotionFactory model index over the source-ordered
    /// remaining support for the preceding rarity result.
    /// </summary>
    public static ChanceLawV111 QueryNativePotionFactoryModelIndex(
        RandomIndexRequestV111 request, PrototypeNativePotionRarity rarity,
        string[] excludedPotionIds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(excludedPotionIds);
        request.Validate();
        if (request.SiteId != "potion_factory.model_within_rarity.index_draw" ||
            request.StreamId is not ("run.combat_potion_generation" or
                NativePlayerRngV111.RewardsStreamId or
                NativePlayerRngV111.ShopsStreamId) ||
            request.OperationKind != "choose_index" ||
            !Enum.IsDefined(rarity) || request.Ordinal != excludedPotionIds.Length ||
            excludedPotionIds.Any(string.IsNullOrWhiteSpace) ||
            excludedPotionIds.Distinct(StringComparer.Ordinal).Count() !=
                excludedPotionIds.Length)
            throw new ArgumentException(
                "Native combat PotionFactory model request is malformed.");

        var candidates = PrototypeNativePotionShop.CandidatesForRarity(
            rarity, excludedPotionIds);
        if (candidates.Length == 0 ||
            request.SupportCount != candidates.Length ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, candidates.Length)))
            throw new InvalidDataException(
                "Native combat PotionFactory support differs from its rarity and exclusion prefix.");
        var weights = NativeNextIntGridWeights(candidates.Length);
        var conditioning = new
        {
            request,
            rarity = rarity.ToString(),
            excludedPotionIds,
            candidates
        };
        var outcomes = candidates.Select((id, index) =>
            new ChanceLawOutcomeV111(
                $"potion_model:{id}:index:{index}", weights[index]))
            .ToArray();
        return CreateNativeBoundaryLaw(
            request.SiteId, request.OperationKind, request.OwnerId,
            request.Ordinal, NativePotionFactoryIndexConditioningPolicyId,
            CanonicalJson.Sha256(conditioning),
            NativeBoundedIndexSourceVerifiedProvenance, outcomes,
            ChanceLawV111.SourceVerifiedStatus);
    }
}
