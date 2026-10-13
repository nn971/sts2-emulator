namespace Sts2Emulator.Core;

/// <summary>
/// Persistent rarity-partitioned relic queues for the restricted
/// configured single-player Silent profile. The native game shuffles
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
            [PrototypeNativeSharedRelics.BookOfFiveRingsId] =
                PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.PendulumId] =
                PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.GorgetId] =
                PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.StrikeDummyId] =
                PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.BreadId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.CauldronId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.OrreryId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.LavaLampId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.MiniatureTentId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.TheCourierId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.TinyMailboxId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.AmethystAubergineId] =
                PrototypeRelicRarity.Common,
            ["proto.relic.lantern"] = PrototypeRelicRarity.Common,
            ["proto.relic.bag_of_preparation"] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.JuzuBraceletId] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.StrawberryId] = PrototypeRelicRarity.Common,
            ["proto.relic.happy_flower"] = PrototypeRelicRarity.Common,
            ["proto.relic.anchor"] = PrototypeRelicRarity.Common,
            ["proto.relic.vajra"] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.FestivePopperId] =
                PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.RegalPillowId] =
                PrototypeRelicRarity.Common,
            ["proto.relic.blood_vial"] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.CentennialPuzzleId] =
                PrototypeRelicRarity.Common,
            ["proto.relic.potion_belt"] = PrototypeRelicRarity.Common,
            [PrototypeNativeSilentRelics.SneckoSkullId] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.WarPaintId] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.WhetstoneId] = PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.VenerableTeaSetId] =
                PrototypeRelicRarity.Common,
            [PrototypeNativeSharedRelics.RedMaskId] =
                PrototypeRelicRarity.Common,
            ["proto.relic.meal_ticket"] = PrototypeRelicRarity.Common,
            ["proto.relic.bronze_scales"] = PrototypeRelicRarity.Common,
            ["proto.relic.oddly_smooth_stone"] = PrototypeRelicRarity.Common,
            ["proto.relic.bag_of_marbles"] = PrototypeRelicRarity.Common,

            [PrototypeNativeSharedRelics.PearId] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.PantographId] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.PetrifiedToadId] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.AkabekoId] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.EternalFeatherId] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.ornamental_fan"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.nunchaku"] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.KusarigamaId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.BowlerHatId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.SparklingRougeId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.PlanisphereId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.LastingCandyId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.RippleBasinId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.VambraceId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.UnsettlingLampId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.DingyRugId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.WingCharmId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.PermafrostId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.ParryingShieldId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.VexingPuzzleboxId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.MiniatureCannonId] =
                PrototypeRelicRarity.Uncommon,
            ["proto.relic.letter_opener"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.reptile_trinket"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.lucky_fysh"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.gremlin_horn"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.mercury_hourglass"] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSilentRelics.TwistedFunnelId] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.orichalcum"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.horn_cleat"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.tingsha"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.joss_paper"] = PrototypeRelicRarity.Uncommon,
            ["proto.relic.tuning_fork"] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.PenNibId] = PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.CandelabraId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.StoneCrackerId] =
                PrototypeRelicRarity.Uncommon,
            [PrototypeNativeSharedRelics.SturdyClampId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.GhostSeedId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.RoyalStampId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.MysticLighterId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.GnarledHammerId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.BeltBuckleId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.KifudaId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.PunchDaggerId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.DollysMirrorId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.DragonFruitId] =
                PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.ScreamingFlagonId] =
                PrototypeRelicRarity.Shop,

            ["proto.relic.meat_on_the_bone"] = PrototypeRelicRarity.Rare,
            ["proto.relic.old_coin"] = PrototypeRelicRarity.Rare,
            ["proto.relic.mango"] = PrototypeRelicRarity.Rare,
            ["proto.relic.molten_egg"] = PrototypeRelicRarity.Rare,
            ["proto.relic.toxic_egg"] = PrototypeRelicRarity.Rare,
            ["proto.relic.frozen_egg"] = PrototypeRelicRarity.Rare,
            ["proto.relic.captains_wheel"] = PrototypeRelicRarity.Rare,
            ["proto.relic.black_star"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSilentRelics.PaperKraneId] = PrototypeRelicRarity.Rare,
            ["proto.relic.mummified_hand"] = PrototypeRelicRarity.Rare,
            ["proto.relic.kunai"] = PrototypeRelicRarity.Rare,
            ["proto.relic.shuriken"] = PrototypeRelicRarity.Rare,
            ["proto.relic.ice_cream"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSilentRelics.HelicalDartId] = PrototypeRelicRarity.Rare,
            ["proto.relic.tough_bandages"] = PrototypeRelicRarity.Rare,
            ["proto.relic.unceasing_top"] = PrototypeRelicRarity.Rare,
            ["proto.relic.prayer_wheel"] = PrototypeRelicRarity.Rare,
            ["proto.relic.pocketwatch"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.GamePieceId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.RainbowRingId] =
                PrototypeRelicRarity.Rare,
            ["proto.relic.white_beast_statue"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.BellowsId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.ChandelierId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.CloakClaspId] =
                PrototypeRelicRarity.Rare,
            ["proto.relic.art_of_war"] = PrototypeRelicRarity.Rare,
            ["proto.relic.charons_ashes"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.WhiteStarId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.BeatingRemnantId] =
                PrototypeRelicRarity.Rare,
            [PrototypeNativeRestSiteRelics.ShovelId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeRestSiteRelics.GiryaId] = PrototypeRelicRarity.Rare,
            ["proto.relic.gambling_chip"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.TungstenRodId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.LizardTailId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.StoneCalendarId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.RazorToothId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSharedRelics.IntimidatingHelmetId] =
                PrototypeRelicRarity.Rare,

            ["proto.relic.lees_waffle"] = PrototypeRelicRarity.Shop,
            ["proto.relic.membership_card"] = PrototypeRelicRarity.Shop,
            ["proto.relic.burning_sticks"] = PrototypeRelicRarity.Shop,
            ["proto.relic.toolbox"] = PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.SlingOfCourageId] =
                PrototypeRelicRarity.Shop,
            ["proto.relic.the_abacus"] = PrototypeRelicRarity.Shop,
            [PrototypeNativeSilentRelics.NinjaScrollId] = PrototypeRelicRarity.Shop,
            ["proto.relic.chemical_x"] = PrototypeRelicRarity.Shop,
            [PrototypeNativeSharedRelics.RingingTriangleId] =
                PrototypeRelicRarity.Shop
        };

    private static readonly HashSet<string> NotSoldInShops =
        new(StringComparer.Ordinal)
        {
            PrototypeNativeSharedRelics.AmethystAubergineId,
            PrototypeNativeSharedRelics.BowlerHatId,
            "proto.relic.old_coin",
            "proto.relic.lucky_fysh",
            PrototypeNativeSharedRelics.TheCourierId
        };

    public static bool TryGetRarity(string id, out PrototypeRelicRarity rarity) =>
        Rarities.TryGetValue(id, out rarity);

    public static bool IsShopEligible(string id) =>
        !NotSoldInShops.Contains(id);

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
        bool merchant, PlayerState player, bool shopEligible = false)
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
                && (!(merchant || shopEligible)
                    || !NotSoldInShops.Contains(candidate)));
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

    /// <summary>
    /// RelicCmd.Obtain can grant a specific relic outside the ordinary
    /// grab-bag draw path (events, Ancients and special rewards). Such
    /// acquired entries must be removed from all persistent queues before
    /// the next decision boundary. Do not reshuffle or consume RNG.
    /// </summary>
    public static RunWorldState PruneOwned(
        RunWorldState world, PlayerState player)
    {
        if (world.RelicBag is not { } bag)
            return world;
        var owned = player.Relics.Select(relic => relic.RelicId)
            .ToHashSet(StringComparer.Ordinal);
        static string[] Prune(string[] remaining, HashSet<string> owned) =>
            remaining.Where(id => !owned.Contains(id)).ToArray();
        var pruned = bag with
        {
            Common = Prune(bag.Common, owned),
            Uncommon = Prune(bag.Uncommon, owned),
            Rare = Prune(bag.Rare, owned),
            Shop = Prune(bag.Shop, owned)
        };
        return world with { RelicBag = pruned };
    }

    public static (RunWorldState World, string? Id) Draw(
        RunWorldState world, PlayerState player, RngBundle rng,
        bool merchant, PrototypeRelicRarity? forcedRarity = null,
        bool shopEligible = false)
    {
        var bag = world.RelicBag ?? Create(player, rng);
        if (world.Map.GenerationProfileId ==
                PrototypeNativeLaterActMap.GloryProfileId
            && world.Floor >= PrototypeNativeLaterActMap.RoomRows(3) + 1 - 7)
        {
            bag = bag with
            {
                Common = bag.Common.Where(id =>
                    id != PrototypeNativeSharedRelics.AmethystAubergineId
                    && id != PrototypeNativeSharedRelics.BookOfFiveRingsId)
                    .ToArray(),
                Uncommon = bag.Uncommon.Where(id =>
                    id != PrototypeNativeSharedRelics.BowlerHatId
                    && id != PrototypeNativeSharedRelics.PlanisphereId
                    && id != PrototypeNativeSharedRelics.LastingCandyId)
                    .ToArray(),
                Rare = bag.Rare.Where(id =>
                    id != PrototypeNativeSharedRelics.WhiteStarId)
                    .ToArray(),
                Shop = bag.Shop.Where(id =>
                    id != PrototypeNativeSharedRelics.DragonFruitId)
                    .ToArray()
            };
        }
        var rarity = forcedRarity ?? RollRarity(rng, merchant);
        var (remaining, id) = Pull(
            bag, rarity, merchant, player, shopEligible);
        return (world with { RelicBag = remaining }, id);
    }
}
