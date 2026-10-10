namespace Sts2Emulator.Core;

/// <summary>
/// Persistent rarity-partitioned relic queues for the restricted
/// single-player Overgrowth/Silent profile. The native game shuffles
/// queues at run creation and draws front for rewards, back for shops.
/// This class preserves the mechanics, not byte-identical RNG.
/// </summary>
public enum PrototypeRelicRarity { Common, Uncommon, Rare, Shop }

public sealed record PrototypeRelicBagState(
    string[] Common,
    string[] Uncommon,
    string[] Rare,
    string[] Shop)
{
    public PrototypeRelicBagState Fork() => this with
    {
        Common = (string[])Common.Clone(),
        Uncommon = (string[])Uncommon.Clone(),
        Rare = (string[])Rare.Clone(),
        Shop = (string[])Shop.Clone()
    };

    public string[] Remaining =>
        Common.Concat(Uncommon).Concat(Rare).Concat(Shop).ToArray();
}

public static class PrototypeNativeRelicGrabBag
{
    // Pinned v0.111.0 relic models' Rarity properties. Event-only
    // and source-absent legacy prototype relics are not eligible here.
    private static readonly Dictionary<string, PrototypeRelicRarity> Rarities =
        new(StringComparer.Ordinal)
        {
            ["proto.relic.lantern"] = PrototypeRelicRarity.Common,
            ["proto.relic.bag_of_preparation"] = PrototypeRelicRarity.Common,
            ["proto.relic.happy_flower"] = PrototypeRelicRarity.Common,
            ["proto.relic.anchor"] = PrototypeRelicRarity.Common,
            ["proto.relic.vajra"] = PrototypeRelicRarity.Common,
            ["proto.relic.blood_vial"] = PrototypeRelicRarity.Common,
            ["proto.relic.meal_ticket"] = PrototypeRelicRarity.Common,
            ["proto.relic.bronze_scales"] = PrototypeRelicRarity.Common,
            ["proto.relic.oddly_smooth_stone"] = PrototypeRelicRarity.Common,
            ["proto.relic.bag_of_marbles"] = PrototypeRelicRarity.Common,

            ["proto.relic.ornamental_fan"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.nunchaku"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.letter_opener"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.reptile_trinket"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.lucky_fysh"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.gremlin_horn"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.mercury_hourglass"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.orichalcum"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.horn_cleat"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.tingsha"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.joss_paper"] = PrototypeRelicRarity.Uncommon,

            ["proto.relic.meat_on_the_bone"] = PrototypeRelicRarity.Rare,
            ["proto.relic.old_coin"] = PrototypeRelicRarity.Rare,
            ["proto.relic.mango"] = PrototypeRelicRarity.Rare,
            ["proto.relic.molten_egg"] = PrototypeRelicRarity.Rare,
            ["proto.relic.toxic_egg"] = PrototypeRelicRarity.Rare,
            ["proto.relic.frozen_egg"] = PrototypeRelicRarity.Rare,
            ["proto.relic.captains_wheel"] = PrototypeRelicRarity.Rare,
            ["proto.relic.kunai"] = PrototypeRelicRarity.Rare,
            ["proto.relic.shuriken"] = PrototypeRelicRarity.Rare,
            ["proto.relic.ice_cream"] = PrototypeRelicRarity.Rare,
            ["proto.relic.tough_bandages"] = PrototypeRelicRarity.Rare,
            ["proto.relic.prayer_wheel"] = PrototypeRelicRarity.Rare,
            ["proto.relic.white_beast_statue"] = PrototypeRelicRarity.Rare,
            ["proto.relic.art_of_war"] = PrototypeRelicRarity.Rare,
            ["proto.relic.charons_ashes"] = PrototypeRelicRarity.Rare,
            ["proto.relic.gambling_chip"] = PrototypeRelicRarity.Rare,

            ["proto.relic.lees_waffle"] = PrototypeRelicRarity.Shop,
            ["proto.relic.membership_card"] = PrototypeRelicRarity.Shop,
            ["proto.relic.burning_sticks"] = PrototypeRelicRarity.Shop,
            ["proto.relic.toolbox"] = PrototypeRelicRarity.Shop
        };

    private static readonly HashSet<string> NotSoldInShops =
        new(StringComparer.Ordinal)
        {
            "proto.relic.old_coin",
            "proto.relic.lucky_fysh"
        };

    public static bool TryGetRarity(string id, out PrototypeRelicRarity rarity) =>
        Rarities.TryGetValue(id, out rarity);

    public static PrototypeRelicBagState Create(
        PlayerState player, RngBundle rng)
    {
        string[] Make(PrototypeRelicRarity rarity)
        {
            var ids = PrototypeContent.RelicPool
                .Where(id => Rarities.TryGetValue(id, out var value)
                    && value == rarity
                    && !player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(relic.RelicId, id)))
                .ToArray();
            PrototypeRng.Shuffle(rng, "reward", ids);
            return ids;
        }

        return new PrototypeRelicBagState(
            Make(PrototypeRelicRarity.Common),
            Make(PrototypeRelicRarity.Uncommon),
            Make(PrototypeRelicRarity.Rare),
            Make(PrototypeRelicRarity.Shop));
    }

    /// <summary>
    /// RelicModel.MerchantCost's default for each native rarity.
    /// All source-classified relics in this restricted bag use that default.
    /// </summary>
    public static int MerchantBaseCost(string relicId)
    {
        if (!TryGetRarity(relicId, out var rarity))
        {
            throw new InvalidOperationException(
                $"No native merchant rarity for relic '{relicId}'.");
        }

        return rarity switch
        {
            PrototypeRelicRarity.Common => 175,
            PrototypeRelicRarity.Uncommon => 225,
            PrototypeRelicRarity.Rare => 275,
            PrototypeRelicRarity.Shop => 200,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// MerchantRelicEntry.CalcCost uses Shops.NextFloat(0.85, 1.15)
    /// and Math.Round. Sample integer hundredths from the shop stream
    /// until exact native float/PRNG parity is implemented.
    /// </summary>
    public static int MerchantPrice(string relicId, RngBundle rng)
    {
        var percent = 85 + PrototypeRng.NextInt(rng, "shop", 31);
        return (int)Math.Round(MerchantBaseCost(relicId) * percent / 100.0);
    }

    public static PrototypeRelicRarity RollRarity(
        RngBundle rng, bool merchant = false)
    {
        // Native relic rarity roll: <0.50 Common, <0.83 Uncommon,
        // otherwise Rare. Merchant rolls consume Rewards RNG too.
        var roll = PrototypeRng.NextInt(rng, "reward", 100);
        return roll < 50
            ? PrototypeRelicRarity.Common
            : roll < 83
                ? PrototypeRelicRarity.Uncommon
                : PrototypeRelicRarity.Rare;
    }

    public static (PrototypeRelicBagState Bag, string? Id) Pull(
        PrototypeRelicBagState bag, PrototypeRelicRarity rarity,
        bool merchant, PlayerState player)
    {
        // Native empty-queue fallback: Shop -> Common -> Uncommon
        // -> Rare. Rare has no further tier.
        var chain = rarity switch
        {
            PrototypeRelicRarity.Shop =>
                new[] { PrototypeRelicRarity.Shop, PrototypeRelicRarity.Common,
                    PrototypeRelicRarity.Uncommon, PrototypeRelicRarity.Rare },
            PrototypeRelicRarity.Common =>
                new[] { PrototypeRelicRarity.Common,
                    PrototypeRelicRarity.Uncommon, PrototypeRelicRarity.Rare },
            PrototypeRelicRarity.Uncommon =>
                new[] { PrototypeRelicRarity.Uncommon, PrototypeRelicRarity.Rare },
            _ => new[] { PrototypeRelicRarity.Rare }
        };
        foreach (var tier in chain)
        {
            var ids = tier switch
            {
                PrototypeRelicRarity.Common => bag.Common,
                PrototypeRelicRarity.Uncommon => bag.Uncommon,
                PrototypeRelicRarity.Rare => bag.Rare,
                _ => bag.Shop
            };
            var candidates = merchant
                ? ids.Reverse().ToArray()
                : ids;
            var id = candidates.FirstOrDefault(candidate =>
                !player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(relic.RelicId, candidate))
                && (!merchant || !NotSoldInShops.Contains(candidate)));
            if (id is null)
            {
                continue;
            }

            var updated = ids.Where(candidate =>
                    !StringComparer.Ordinal.Equals(candidate, id))
                .ToArray();
            return (tier switch
            {
                PrototypeRelicRarity.Common => bag with { Common = updated },
                PrototypeRelicRarity.Uncommon => bag with { Uncommon = updated },
                PrototypeRelicRarity.Rare => bag with { Rare = updated },
                _ => bag with { Shop = updated }
            }, id);
        }

        return (bag, null);
    }

    public static (RunWorldState World, string? Id) Draw(
        RunWorldState world, PlayerState player, RngBundle rng,
        bool merchant, PrototypeRelicRarity? forcedRarity = null)
    {
        var bag = world.RelicBag ?? Create(player, rng);
        var rarity = forcedRarity ?? RollRarity(rng, merchant);
        var (remaining, id) = Pull(bag, rarity, merchant, player);
        return (world with { RelicBag = remaining }, id);
    }
}
