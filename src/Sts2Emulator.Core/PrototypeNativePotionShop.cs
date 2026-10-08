namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 rarity and merchant price rules for the
/// 32 potions already implemented by the restricted emulator.
/// Pool weighting and byte-for-byte native Shops RNG remain deferred.
/// </summary>
public enum PrototypeNativePotionRarity
{
    Common,
    Uncommon,
    Rare
}

public static class PrototypeNativePotionShop
{
    private static readonly IReadOnlyDictionary<string, PrototypeNativePotionRarity>
        Rarities = new Dictionary<string, PrototypeNativePotionRarity>(
            StringComparer.Ordinal)
        {
            ["proto.potion.block"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.fire"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.poison"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.swift"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.weak"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.dexterity"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.explosive"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.energy"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.strength"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.flex"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.attack"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.skill"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.power"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.speed"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.vulnerable"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.blood"] = PrototypeNativePotionRarity.Common,

            ["proto.potion.regen"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.duplicator"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.blessing_of_the_forge"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.liquid_bronze"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.cure_all"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.fortifier"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.stable_serum"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.touch_of_insanity"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.gamblers_brew"] = PrototypeNativePotionRarity.Uncommon,

            ["proto.potion.liquid_memories"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.ghost_in_a_jar"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.fruit_juice"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.entropic_brew"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.snecko_oil"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.fairy_in_a_bottle"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.distilled_chaos"] = PrototypeNativePotionRarity.Rare
        };

    public static bool TryGetRarity(
        string potionId, out PrototypeNativePotionRarity rarity) =>
        Rarities.TryGetValue(potionId, out rarity);

    /// <summary>
    /// PotionFactory.CreateRandomPotions first rolls rarity (10% Rare,
    /// 25% Uncommon, 65% Common), then chooses an item of that rarity.
    /// Integer [0,100) rolls preserve these probabilities without
    /// reproducing native NextFloat bit sequences.
    /// </summary>
    public static PrototypeNativePotionRarity RollRarity(
        RngBundle rng, string streamId)
    {
        var roll = PrototypeRng.NextInt(rng, streamId, 100);
        return roll < 10
            ? PrototypeNativePotionRarity.Rare
            : roll < 35
                ? PrototypeNativePotionRarity.Uncommon
                : PrototypeNativePotionRarity.Common;
    }

    public static string PickWeighted(
        RngBundle rng, string streamId,
        IReadOnlyCollection<string>? excluded = null)
    {
        var rarity = RollRarity(rng, streamId);
        var candidates = PrototypeContent.PotionPool
            .Where(id => Rarities[id] == rarity
                && (excluded is null || !excluded.Contains(id)))
            .ToArray();
        if (candidates.Length == 0)
        {
            // Native PotionFactory does not silently substitute a
            // different rarity if the selected tier is exhausted.
            throw new InvalidOperationException(
                $"No unexcluded {rarity} potions remain.");
        }

        return candidates[PrototypeRng.NextInt(
            rng, streamId, candidates.Length)];
    }

    /// <summary>
    /// Native merchant inventories call CreateRandomPotions with a
    /// mutable candidate list, removing each selected model.
    /// </summary>
    public static string[] PickWeightedDistinct(
        RngBundle rng, string streamId, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var selected = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            selected.Add(PickWeighted(rng, streamId, selected));
        }

        return selected.ToArray();
    }

    /// <summary>
    /// Wellspring.Bottle uses Rewards.NextItem over the complete
    /// character/shared potion pool, with no rarity weighting.
    /// </summary>
    public static string PickUniform(RngBundle rng, string streamId)
    {
        var pool = PrototypeContent.PotionPool;
        return pool[PrototypeRng.NextInt(rng, streamId, pool.Length)];
    }

    /// <summary>
    /// MerchantPotionEntry.GetCost: Common 50, Uncommon 75, Rare 100.
    /// </summary>
    public static int MerchantBaseCost(string potionId)
    {
        if (!TryGetRarity(potionId, out var rarity))
        {
            throw new InvalidOperationException(
                $"No native merchant rarity for potion '{potionId}'.");
        }

        return rarity switch
        {
            PrototypeNativePotionRarity.Common => 50,
            PrototypeNativePotionRarity.Uncommon => 75,
            PrototypeNativePotionRarity.Rare => 100,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Native merchant potions use Shops.NextFloat(0.95, 1.05)
    /// and Godot Mathf.Round; discrete percent samples approximate
    /// the distribution until exact RNG parity is enabled.
    /// </summary>
    public static int MerchantPrice(string potionId, RngBundle rng)
    {
        var percent = 95 + PrototypeRng.NextInt(rng, "shop", 11);
        return Math.Max(1,
            (int)Math.Round(MerchantBaseCost(potionId) * percent / 100.0));
    }
}
