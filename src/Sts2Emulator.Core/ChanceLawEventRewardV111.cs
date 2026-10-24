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

}
