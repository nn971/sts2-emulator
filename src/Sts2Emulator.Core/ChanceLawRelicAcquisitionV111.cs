using System.Globalization;

namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    /// <summary>
    /// Query the legacy random-Rare-card relic acquisition model draw. The
    /// configured Native route can query its declared player-stream bound
    /// separately through <see cref="QueryNativePlayerModelIndex"/>.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRelicAcquisitionRareCardModel(
        int ordinal)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "relic.acquisition.random_rare_card.model";
        const string streamId = "reward";
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity ==
            PrototypeCardRarity.Rare).ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy random-Rare-card relic acquisition pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = "player:rare_pool:" + poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            orderedRareRewardPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-relic-acquisition-rare-card-pool-v1", conditioning,
            "Equal index weights over the ordered prototype Rare reward-card " +
            "pool; Native character-pool fidelity and stream attribution " +
            "remain unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy random-potion relic acquisition model draw for the
    /// caller-declared first open potion slot.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRelicAcquisitionPotionModel(
        int slotIndex, int ordinal)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "relic.acquisition.random_potion.model";
        const string streamId = "event";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The legacy random-potion relic acquisition pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"player:potion_slot:{slotIndex}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            slotIndex,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-relic-acquisition-potion-pool-v1", conditioning,
            "Equal index weights over the ordered prototype potion pool for " +
            "the caller-declared open slot; source-pool fidelity and Native " +
            "stream attribution remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy Run-effect potion fill draw for one caller-declared
    /// empty slot. The effect skips all draws when potion acquisition is not
    /// allowed and draws only for currently empty slots.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRunPotionFillSlotModel(
        int slotIndex, bool canAcquirePotion, bool slotIsEmpty, int ordinal)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (!canAcquirePotion || !slotIsEmpty)
            throw new ArgumentException(
                "Run potion-slot filling requires available potion capacity and a declared empty slot.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "run.potion.fill_empty_slots.model";
        const string streamId = "reward";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The Run potion fill pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"player:potion_slot:{slotIndex}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            slotIndex,
            canAcquirePotion,
            slotIsEmpty,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-run-potion-fill-pool-v1", conditioning,
            "Equal index weights over the ordered prototype potion pool for " +
            "the caller-declared empty slot; source-pool fidelity and Native " +
            "stream attribution remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query a declared Native player-stream bounded index for a model draw.
    /// This verifies the NextInt transform, not owner or pool fidelity.
    /// </summary>
    public static ChanceLawV111 QueryNativePlayerModelIndex(
        string siteId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (siteId is not ("relic.acquisition.random_rare_card.model" or
            "relic.acquisition.random_potion.model" or
            "run.potion.fill_empty_slots.model" or
            StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId))
            throw new NotSupportedException(
                $"Native player model law does not support site '{siteId}'.");
        if (supportCount < 1)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var request = new RandomIndexRequestV111(
            siteId, NativePlayerRngV111.RewardsStreamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-relic-acquisition-model-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query one Native player-reward draw made by Entropic Brew while
    /// filling an empty potion slot. The caller validates the slot owner
    /// against the action and the ordered source potion pool.
    /// </summary>
    public static ChanceLawV111 QueryNativeRunPotionFillSlotModel(
        RandomIndexRequestV111 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.ValidateForNativeProvider();
        var pool = PrototypeContent.PotionPool;
        if (request.SiteId != "run.potion.fill_empty_slots.model"
            || request.StreamId != NativePlayerRngV111.RewardsStreamId
            || request.OperationKind != "choose_index"
            || request.SupportCount != pool.Length
            || !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, pool.Length)))
            throw new InvalidDataException(
                "Native Entropic Brew request differs from its ordered potion pool.");

        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "native-run-potion-fill-slot-model-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query a legacy persistent-deck transform over the ordered reward pool
    /// with the source card model removed.
    /// </summary>
    public static ChanceLawV111 QueryLegacyRewardCardTransform(
        long cardInstanceId, string originalCardId, int ordinal)
    {
        if (cardInstanceId < 0)
            throw new ArgumentOutOfRangeException(nameof(cardInstanceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCardId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "reward.card_transform.replacement.index_draw";
        const string streamId = "reward";
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
            !StringComparer.Ordinal.Equals(cardId, originalCardId)).ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "No prototype card is available for transformation.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = "card_instance:" + cardInstanceId +
            ":original:" + originalCardId + ":pool:" + poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            cardInstanceId,
            originalCardId,
            orderedCandidates = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-reward-card-transform-pool-v1", conditioning,
            "Equal index weights over the source-ordered prototype reward " +
            "pool excluding the original model; pool fidelity and Native " +
            "parity remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    public static ChanceLawV111 QueryPaelsLegionSkin(
        string ownerId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string ownerPrefix = "relic:paels_legion:instance:";
        var slotText = ownerId.StartsWith(ownerPrefix,
            StringComparison.Ordinal)
            ? ownerId[ownerPrefix.Length..] : string.Empty;
        if (ordinal != 0 || !int.TryParse(slotText,
                System.Globalization.NumberStyles.None,
                CultureInfo.InvariantCulture, out var slot) || slot < 0)
            throw new ArgumentException(
                "Pael's Legion law requires its relic-instance owner ID and isolated stream ordinal zero.",
                ordinal != 0 ? nameof(ordinal) : nameof(ownerId));
        var request = new RandomIndexRequestV111(
            NativePaelRewardsV111.LegionSkinSiteId, "reward", ownerId,
            ordinal, Enumerable.Range(0,
                NativePaelRewardsV111.LegionSkins.Length).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "relic.paels_legion.skin_choice",
            slot
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query the caller-declared unowned-relic index support on one of its
    /// legacy event streams or the hypothesized Native player reward stream.
    /// Pool filtering and the owner prefix are supplied by the caller.
    /// </summary>
    public static ChanceLawV111 QueryUnownedRelicSelectionIndex(
        string streamId, string ownerId, int supportCount, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (supportCount < 1)
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId =
            StochasticSiteRegistryV111.UnownedRandomRelicSelectionSiteId;
        if (streamId == NativePlayerRngV111.RewardsStreamId)
            return QueryNativePlayerModelIndex(
                siteId, ownerId, supportCount, ordinal);
        if (streamId is not ("event" or "reward"))
            throw new ArgumentException(
                "Unowned-relic selection stream must be event, reward, or player.rewards.",
                nameof(streamId));

        var request = new RandomIndexRequestV111(
            siteId, streamId, ownerId, ordinal,
            Enumerable.Range(0, supportCount).ToArray());
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "legacy-unowned-relic-selection-index",
            request
        });
        return QueryRandomIndex(eventId, request);
    }

    /// <summary>
    /// Query an unowned-relic index with its declared source-ordered model
    /// identities. The index law supplies the weights; this overload binds
    /// the semantic result IDs to the caller's exact ordered pool.
    /// </summary>
    public static ChanceLawV111 QueryUnownedRelicSelectionIndex(
        string streamId, string ownerId, string[] candidateRelicIds,
        int ordinal)
    {
        ArgumentNullException.ThrowIfNull(candidateRelicIds);
        if (candidateRelicIds.Length == 0 ||
            candidateRelicIds.Any(string.IsNullOrWhiteSpace) ||
            candidateRelicIds.Distinct(StringComparer.Ordinal).Count() !=
                candidateRelicIds.Length)
            throw new ArgumentException(
                "Unowned-relic candidates must be a nonempty ordered set of unique IDs.",
                nameof(candidateRelicIds));

        var indexLaw = QueryUnownedRelicSelectionIndex(
            streamId, ownerId, candidateRelicIds.Length, ordinal);
        var outcomes = candidateRelicIds.Select((relicId, index) =>
            new ChanceLawOutcomeV111(
                "relic:" + relicId,
                indexLaw.OutcomeWeightAt(index))).ToArray();
        var conditioningFingerprint = CanonicalJson.Sha256(new
        {
            sourceLawId = indexLaw.LawId,
            sourceConditioningFingerprint =
                indexLaw.ConditioningFingerprint,
            orderedCandidateRelicIds = candidateRelicIds
        });
        var eventId = CanonicalJson.Sha256(new
        {
            boundary = "unowned-relic-semantic-index",
            indexLaw.LawId,
            orderedCandidateRelicIds = candidateRelicIds
        });
        return Create(
            eventId, indexLaw.SiteId, indexLaw.OperationKind,
            indexLaw.OwnerId, indexLaw.Ordinal,
            indexLaw.ConditioningPolicyId + "-relic-identity-v1",
            conditioningFingerprint, indexLaw.ProbabilityProvenance,
            outcomes, indexLaw.ProbabilityStatus);
    }
}
