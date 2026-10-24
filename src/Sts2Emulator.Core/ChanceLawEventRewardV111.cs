namespace Sts2Emulator.Core;

public static partial class ChanceLawEngineV111
{
    /// <summary>
    /// Query the legacy custom event reward's modifier-generated curse draw.
    /// Native event reward ownership and source-pool completeness remain open.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventModifierCurseModel(
        string eventId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId =
            "event.reward.random_modifier_curse.index_draw";
        const string streamId = "reward";
        var pool = NativeGloryAncientsV111.ModifierGeneratedCurseIds;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The modifier-generated event curse pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"event:{eventId}:pool:" + poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L)).ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            orderedModifierCursePool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-modifier-curse-model-pool-v1", conditioning,
            "Equal index weights over the implemented ordered modifier-curse " +
            "pool; source event-reward audit and Native parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy event potion fill draw for a declared empty slot.
    /// The source skips all slot draws when potion acquisition is unavailable.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventFillPotionSlot(
        int slotIndex, bool canAcquirePotion, bool slotIsEmpty, int ordinal)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        if (!canAcquirePotion || !slotIsEmpty)
            throw new ArgumentException(
                "Event potion-slot filling requires available potion capacity and a declared empty slot.");
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.potion.fill_empty_slots.model";
        const string streamId = "event";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The event potion fill pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = "player:potion_slot:" + slotIndex + ":pool:" +
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
            "legacy-event-fill-potion-slot-pool-v1", conditioning,
            "Equal index weights over the ordered prototype potion pool for " +
            "an eligible empty slot; pool fidelity and Native parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy custom event reward's potion option. The source skips
    /// the draw when potion acquisition is prevented.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventOfferPotionModel(
        string rewardOwnerPrefix, bool canAcquirePotion, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardOwnerPrefix);
        if (!canAcquirePotion)
            throw new ArgumentException(
                "The event potion-option draw is skipped when potion acquisition is prevented.",
                nameof(canAcquirePotion));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.reward.offer_potion.model.index_draw";
        const string streamId = "reward";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The custom event potion-option pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = rewardOwnerPrefix + ":potion_pool:" + poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            rewardOwnerPrefix,
            ordinal,
            canAcquirePotion,
            orderedPotionPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-offer-potion-model-pool-v1", conditioning,
            "Equal index weights over the ordered prototype custom-reward " +
            "potion pool; pool completeness and Native parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

    /// <summary>
    /// Query the generic legacy random-potion event effect. Event-specific
    /// Native branches use separate source sites and are outside this law.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventRewardPotionModel(
        string eventId, string choiceId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.reward.random_potion.model";
        const string streamId = "event";
        var pool = PrototypeContent.PotionPool;
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The generic event reward potion pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"event:{eventId}:choice:{choiceId}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(potionId =>
            new ChanceLawOutcomeV111("potion:" + potionId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            choiceId,
            orderedPotionPool = pool,
            poolFingerprint,
            route = "generic_legacy_uniform_pool"
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-reward-potion-model-pool-v1", conditioning,
            "Equal index weights over the generic event's ordered prototype " +
            "potion pool; Native event-specific branches and source-pool " +
            "completeness remain unverified.", outcomes,
            operationKind: "choose_index");
    }

    /// <summary>
    /// Query the legacy random-Rare-card event effect over the current ordered
    /// prototype reward pool filtered to Rare cards.
    /// </summary>
    public static ChanceLawV111 QueryLegacyEventRewardRareCardModel(
        string eventId, string choiceId, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        const string siteId = "event.reward.random_rare_card.model";
        const string streamId = "reward";
        var pool = PrototypeContent.RewardCardPool.Where(cardId =>
            PrototypeContent.Card(cardId).Rarity ==
            PrototypeCardRarity.Rare).ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException(
                "The generic event reward Rare-card pool is empty.");
        var poolFingerprint = CanonicalJson.Sha256(pool);
        var ownerId = $"event:{eventId}:choice:{choiceId}:pool:" +
            poolFingerprint;
        var outcomes = pool.Select(cardId =>
            new ChanceLawOutcomeV111("card:" + cardId, 1L))
            .ToArray();
        var conditioning = new
        {
            siteId,
            streamId,
            ownerId,
            ordinal,
            eventId,
            choiceId,
            orderedRareRewardPool = pool,
            poolFingerprint
        };
        return CreateLegacyCategoricalLaw(
            siteId, streamId, ownerId, ordinal,
            "legacy-event-reward-rare-card-pool-v1", conditioning,
            "Equal index weights over the ordered prototype Rare-card reward " +
            "pool; Native character-pool fidelity and stream parity remain " +
            "unverified.", outcomes, operationKind: "choose_index");
    }

}
