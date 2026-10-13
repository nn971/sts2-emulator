namespace Sts2Emulator.Core;

/// <summary>Native potion rarity tiers.</summary>
public enum PrototypeNativePotionRarity
{
    Common,
    Uncommon,
    Rare
}

/// <summary>
/// Pinned v0.111.0 rarity and merchant price rules for the supported
/// full-unlock Silent/shared potion pool. Out-of-combat native generation
/// preserves the source pool order and draw sequence.
/// </summary>
public static class PrototypeNativePotionShop
{
    // PotionFactory concatenates the current character's unlocked pool before
    // the shared pool. Keep that ordering for NativeRngV111.NextItem: a
    // different ordering changes the selected potion for the same RNG draw.
    private static readonly string[] NativeSilentPotionPoolOrder =
    [
        "proto.potion.poison",
        "proto.potion.ghost_in_a_jar",
        "proto.potion.cunning",
        "proto.potion.attack",
        "proto.potion.beetle_juice",
        "proto.potion.blessing_of_the_forge",
        "proto.potion.block",
        "proto.potion.bottled_potential",
        "proto.potion.clarity",
        "proto.potion.colorless",
        "proto.potion.cure_all",
        "proto.potion.dexterity",
        "proto.potion.distilled_chaos",
        "proto.potion.droplet_of_precognition",
        "proto.potion.duplicator",
        "proto.potion.energy",
        "proto.potion.entropic_brew",
        "proto.potion.explosive_ampoule",
        "proto.potion.fairy_in_a_bottle",
        "proto.potion.fire",
        "proto.potion.flex",
        "proto.potion.fortifier",
        "proto.potion.fruit_juice",
        "proto.potion.fysh_oil",
        "proto.potion.gamblers_brew",
        "proto.potion.gigantification",
        "proto.potion.heart_of_iron",
        "proto.potion.liquid_bronze",
        "proto.potion.liquid_memories",
        "proto.potion.lucky_tonic",
        "proto.potion.mazaleths_gift",
        "proto.potion.orobic_acid",
        "proto.potion.binding",
        "proto.potion.powdered_demise",
        "proto.potion.power",
        "proto.potion.radiant_tincture",
        "proto.potion.regen",
        "proto.potion.shackling",
        "proto.potion.ship_in_a_bottle",
        "proto.potion.skill",
        "proto.potion.snecko_oil",
        "proto.potion.speed",
        "proto.potion.stable_serum",
        "proto.potion.strength",
        "proto.potion.swift",
        "proto.potion.touch_of_insanity",
        "proto.potion.vulnerable",
        "proto.potion.weak"
    ];
    private static readonly IReadOnlyList<string>
        NativeSilentPotionPoolOrderView =
            Array.AsReadOnly(NativeSilentPotionPoolOrder);

    /// <summary>
    /// Silent's full-unlock character potion pool followed by the shared pool,
    /// in the order used by the pinned source's PotionFactory.
    /// </summary>
    public static IReadOnlyList<string> NativePotionPoolOrder =>
        NativeSilentPotionPoolOrderView;

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
            ["proto.potion.energy"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.strength"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.flex"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.explosive_ampoule"] =
                PrototypeNativePotionRarity.Common,
            ["proto.potion.attack"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.skill"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.power"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.speed"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.vulnerable"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.blood"] = PrototypeNativePotionRarity.Common,

            ["proto.potion.regen"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.cunning"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.fysh_oil"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.binding"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.clarity"] = PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.colorless"] = PrototypeNativePotionRarity.Common,
            ["proto.potion.powdered_demise"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.lucky_tonic"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.orobic_acid"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.heart_of_iron"] =
                PrototypeNativePotionRarity.Uncommon,
            ["proto.potion.radiant_tincture"] =
                PrototypeNativePotionRarity.Uncommon,
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
            ["proto.potion.distilled_chaos"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.ship_in_a_bottle"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.droplet_of_precognition"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.bottled_potential"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.shackling"] = PrototypeNativePotionRarity.Rare,
            ["proto.potion.mazaleths_gift"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.beetle_juice"] =
                PrototypeNativePotionRarity.Rare,
            ["proto.potion.gigantification"] =
                PrototypeNativePotionRarity.Rare
        };

    public static bool TryGetRarity(
        string potionId, out PrototypeNativePotionRarity rarity) =>
        Rarities.TryGetValue(potionId, out rarity);

    /// <summary>
    /// PotionFactory.CreateRandomPotions first rolls rarity (10% Rare,
    /// 25% Uncommon, 65% Common), then chooses an item of that rarity.
    /// Integer [0,100) rolls preserve these probabilities for prototype-owned
    /// generation. Native source-owned generation uses NextFloat below.
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
        var candidates = NativePotionPoolOrder
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
    /// mutable candidate list, removing each selected model. Retain source
    /// order because each NextItem indexes the remaining rarity subset.
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
    /// Source-ordered out-of-combat PotionFactory generation. Each item rolls
    /// rarity with NextFloat, chooses from the remaining same-rarity models,
    /// and removes the selected model before the next item.
    /// </summary>
    public static string[] PickWeightedDistinctNative(
        RngBundle rng, string streamId, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var remaining = NativePotionPoolOrder.ToList();
        var selected = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var roll = NativeRngV111.NextFloat(rng, streamId);
            var rarity = roll <= 0.1f
                ? PrototypeNativePotionRarity.Rare
                : roll <= 0.35f
                    ? PrototypeNativePotionRarity.Uncommon
                    : PrototypeNativePotionRarity.Common;
            var candidates = remaining.Where(id => Rarities[id] == rarity)
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    $"No unselected {rarity} potions remain.");
            var potionId = NativeRngV111.NextItem(
                rng, streamId, (IReadOnlyList<string>)candidates)
                ?? throw new InvalidOperationException(
                    "Native potion selection returned no model.");
            selected.Add(potionId);
            remaining.Remove(potionId);
        }
        return selected.ToArray();
    }

    /// <summary>
    /// Source-shaped single PotionReward population. Separate rewards each
    /// start with the full pool, so repeated potion IDs remain possible.
    /// </summary>
    public static string PickWeightedNative(
        RngBundle rng, string streamId)
    {
        var roll = NativeRngV111.NextFloat(rng, streamId);
        var rarity = roll <= 0.1f
            ? PrototypeNativePotionRarity.Rare
            : roll <= 0.35f
                ? PrototypeNativePotionRarity.Uncommon
                : PrototypeNativePotionRarity.Common;
        var candidates = NativePotionPoolOrder
            .Where(id => Rarities[id] == rarity).ToArray();
        return NativeRngV111.NextItem(
                rng, streamId, (IReadOnlyList<string>)candidates)
            ?? throw new InvalidOperationException(
                $"No {rarity} potions are available.");
    }

    /// <summary>
    /// Source uniform selection over the full-unlock character/shared potion
    /// pool, with no rarity weighting. Used by Wellspring and event offers.
    /// </summary>
    public static string PickUniform(RngBundle rng, string streamId)
    {
        return NativePotionPoolOrder[PrototypeRng.NextInt(
            rng, streamId, NativePotionPoolOrder.Count)];
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
