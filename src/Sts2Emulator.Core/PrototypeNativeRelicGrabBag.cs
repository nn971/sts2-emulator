using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Persistent rarity-partitioned relic state for the restricted configured
/// single-player Silent profile. Native state stores shuffled queues; Nature
/// state stores canonical remaining identities until a law selects one.
/// </summary>
public enum PrototypeRelicRarity { Common, Uncommon, Rare, Shop }

/// <summary>
/// Describes whether rarity arrays carry a committed Native queue order or
/// only the remaining identities available to a Nature selection.
/// </summary>
public enum PrototypeRelicBagOrderMode
{
    NativeCommitted,
    NatureUnordered
}

public sealed record PrototypeRelicBagState
{
    private string[] _common;
    private string[] _uncommon;
    private string[] _rare;
    private string[] _shop;

    [JsonConstructor]
    public PrototypeRelicBagState(
        string[] Common, string[] Uncommon, string[] Rare, string[] Shop)
    {
        _common = CloneRequired(Common, nameof(Common));
        _uncommon = CloneRequired(Uncommon, nameof(Uncommon));
        _rare = CloneRequired(Rare, nameof(Rare));
        _shop = CloneRequired(Shop, nameof(Shop));
    }

    public string[] Common
    {
        get => (string[])_common.Clone();
        init => _common = CloneRequired(value, nameof(Common));
    }
    public string[] Uncommon
    {
        get => (string[])_uncommon.Clone();
        init => _uncommon = CloneRequired(value, nameof(Uncommon));
    }
    public string[] Rare
    {
        get => (string[])_rare.Clone();
        init => _rare = CloneRequired(value, nameof(Rare));
    }
    public string[] Shop
    {
        get => (string[])_shop.Clone();
        init => _shop = CloneRequired(value, nameof(Shop));
    }

    // Native snapshots retain their existing shape. Nature snapshots carry
    // this optional discriminator so sorted identity sets cannot be mistaken
    // for committed queue order after a restore.
    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public PrototypeRelicBagOrderMode OrderMode { get; init; }

    public PrototypeRelicBagState Fork() => this with
    {
        Common = _common,
        Uncommon = _uncommon,
        Rare = _rare,
        Shop = _shop
    };

    public void Deconstruct(
        out string[] Common, out string[] Uncommon,
        out string[] Rare, out string[] Shop)
    {
        Common = this.Common;
        Uncommon = this.Uncommon;
        Rare = this.Rare;
        Shop = this.Shop;
    }

    public string[] Remaining =>
        Common.Concat(Uncommon).Concat(Rare).Concat(Shop).ToArray();

    private static string[] CloneRequired(string[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (string[])values.Clone();
    }
}

public static class PrototypeNativeRelicGrabBag
{
    public const string QueueShuffleSiteId =
        "relic_grab_bag.queue_shuffle.swap_index";
    public const string NativeMerchantModelSiteId =
        "shop.merchant.relic_model.index_draw";
    public const string NativeTreasureModelSiteId =
        "treasure.relic_grab_bag.model.index_draw";
    public const string NativeTreasureRarityOwnerId =
        "relic_grab_bag:reward";
    internal const int RarityWeightScale = 10_000;
    internal const int CommonRarityWeight = 5_000;
    internal const int UncommonRarityWeight = 3_300;
    internal const int RareRarityWeight = RarityWeightScale -
        CommonRarityWeight - UncommonRarityWeight;

    internal static (PrototypeRelicRarity Rarity, int BasisPoints)[]
        NativeRarityBranches() =>
    [
        (PrototypeRelicRarity.Common, CommonRarityWeight),
        (PrototypeRelicRarity.Uncommon, UncommonRarityWeight),
        (PrototypeRelicRarity.Rare, RareRarityWeight)
    ];

    internal static string[] LegacyRarityTicketLabels() =>
        Enumerable.Range(0, 100).Select(ticket =>
            (ticket < CommonRarityWeight / 100 ? "common:" :
                ticket < (CommonRarityWeight + UncommonRarityWeight) / 100
                    ? "uncommon:" : "rare:")
            + ticket.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

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
            ["proto.relic.potion_belt"] = PrototypeRelicRarity.Common,
            [PrototypeNativeSilentRelics.SneckoSkullId] = PrototypeRelicRarity.Common,
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
            [PrototypeNativeSilentRelics.TwistedFunnelId] = PrototypeRelicRarity.Uncommon,
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
            ["proto.relic.black_star"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSilentRelics.PaperKraneId] = PrototypeRelicRarity.Rare,
            ["proto.relic.kunai"] = PrototypeRelicRarity.Rare,
            ["proto.relic.shuriken"] = PrototypeRelicRarity.Rare,
            ["proto.relic.ice_cream"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeSilentRelics.HelicalDartId] = PrototypeRelicRarity.Rare,
            ["proto.relic.tough_bandages"] = PrototypeRelicRarity.Rare,
            ["proto.relic.prayer_wheel"] = PrototypeRelicRarity.Rare,
            ["proto.relic.white_beast_statue"] = PrototypeRelicRarity.Rare,
            ["proto.relic.art_of_war"] = PrototypeRelicRarity.Rare,
            ["proto.relic.charons_ashes"] = PrototypeRelicRarity.Rare,
            [PrototypeNativeRestSiteRelics.ShovelId] = PrototypeRelicRarity.Rare,
            [PrototypeNativeRestSiteRelics.GiryaId] = PrototypeRelicRarity.Rare,
            ["proto.relic.gambling_chip"] = PrototypeRelicRarity.Rare,

            ["proto.relic.lees_waffle"] = PrototypeRelicRarity.Shop,
            ["proto.relic.membership_card"] = PrototypeRelicRarity.Shop,
            ["proto.relic.burning_sticks"] = PrototypeRelicRarity.Shop,
            ["proto.relic.toolbox"] = PrototypeRelicRarity.Shop,
            [PrototypeNativeSilentRelics.NinjaScrollId] = PrototypeRelicRarity.Shop
        };

    private static readonly HashSet<string> NotSoldInShops =
        new(StringComparer.Ordinal)
        {
            "proto.relic.old_coin",
            "proto.relic.lucky_fysh"
        };

    public static bool TryGetRarity(string id, out PrototypeRelicRarity rarity) =>
        Rarities.TryGetValue(id, out rarity);

    public static string TreasureModelOwnerId(
        RunWorldState world, PrototypeRelicRarity rarity,
        string[] candidateIds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(candidateIds);
        if (!Enum.IsDefined(rarity) || candidateIds.Length == 0 ||
            candidateIds.Any(string.IsNullOrWhiteSpace) ||
            !candidateIds.SequenceEqual(candidateIds.OrderBy(
                id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException(
                "Treasure relic support must be a nonempty sorted identity list.",
                nameof(candidateIds));
        return $"act:{world.Act}:floor:{world.Floor}:treasure:relic_grab_bag" +
            $":rarity:{rarity}:pool:" +
            CanonicalJson.Sha256(candidateIds);
    }

    public static void Validate(PrototypeRelicBagState bag, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(player);
        ValidateBagStructure(bag);

        if (bag.Remaining.Any(id => player.Relics.Any(relic =>
                StringComparer.Ordinal.Equals(relic.RelicId, id))))
            throw new InvalidOperationException(
                "Native relic bag contains duplicates, invalid or owned relics.");
    }

    internal static void ValidateBagStructure(PrototypeRelicBagState bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (bag.Common is null || bag.Uncommon is null || bag.Rare is null ||
            bag.Shop is null)
            throw new InvalidOperationException(
                "Native relic bag is missing a rarity queue.");
        if (!Enum.IsDefined(bag.OrderMode))
            throw new InvalidOperationException(
                "Native relic bag has an unknown queue order mode.");

        var remaining = bag.Remaining;
        if (remaining.Length != remaining.Distinct(StringComparer.Ordinal).Count()
            || remaining.Any(id => !PrototypeContent.RelicPool.Contains(
                    id, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Native relic bag contains duplicates or invalid relics.");

        foreach (var (tier, ids) in new[]
        {
            (PrototypeRelicRarity.Common, bag.Common),
            (PrototypeRelicRarity.Uncommon, bag.Uncommon),
            (PrototypeRelicRarity.Rare, bag.Rare),
            (PrototypeRelicRarity.Shop, bag.Shop)
        })
        {
            if (ids.Any(id =>
                !TryGetRarity(id, out var actual) || actual != tier))
                throw new InvalidOperationException(
                    "Native relic bag has incorrectly partitioned relics.");
        }

        if (bag.OrderMode == PrototypeRelicBagOrderMode.NatureUnordered &&
            new[] { bag.Common, bag.Uncommon, bag.Rare, bag.Shop }.Any(ids =>
                !ids.SequenceEqual(ids.OrderBy(
                    id => id, StringComparer.Ordinal), StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Nature relic identity sets must use canonical sorted order.");
    }

    public static PrototypeRelicBagState Create(
        PlayerState player, RngBundle rng)
    {
        string[] Make(PrototypeRelicRarity rarity)
        {
            var ids = EligibleIds(player, rarity);
            _ = LegacyFisherYatesV111.ShuffleInPlace(
                ids,
                rng,
                "reward",
                QueueShuffleSiteId,
                "player:0:relic_grab_bag:" + rarity);
            return ids;
        }

        return new PrototypeRelicBagState(
            Make(PrototypeRelicRarity.Common),
            Make(PrototypeRelicRarity.Uncommon),
            Make(PrototypeRelicRarity.Rare),
            Make(PrototypeRelicRarity.Shop));
    }

    /// <summary>
    /// Build Nature's canonical rarity sets without committing a hidden
    /// queue order. Nature draws identities conditionally from these sets,
    /// so Native's shuffled queue order is not an input to the first pull.
    /// </summary>
    internal static PrototypeRelicBagState CreateNatureUnordered(
        PlayerState player, PrototypeRelicBagState? sourceBag = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        static string[] Canonical(string[] ids) => ids
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (sourceBag is not null)
            Validate(sourceBag, player);

        var bag = new PrototypeRelicBagState(
            Canonical(sourceBag?.Common ??
                EligibleIds(player, PrototypeRelicRarity.Common)),
            Canonical(sourceBag?.Uncommon ??
                EligibleIds(player, PrototypeRelicRarity.Uncommon)),
            Canonical(sourceBag?.Rare ??
                EligibleIds(player, PrototypeRelicRarity.Rare)),
            Canonical(sourceBag?.Shop ??
                EligibleIds(player, PrototypeRelicRarity.Shop)))
        {
            OrderMode = PrototypeRelicBagOrderMode.NatureUnordered
        };
        Validate(bag, player);
        return bag;
    }

    private static string[] EligibleIds(
        PlayerState player, PrototypeRelicRarity rarity) =>
        PrototypeContent.RelicPool
            .Where(id => Rarities.TryGetValue(id, out var value)
                && value == rarity
                && !player.Relics.Any(relic =>
                    StringComparer.Ordinal.Equals(relic.RelicId, id)))
            .ToArray();

    /// <summary>
    /// Native RelicGrabBag construction shuffles each ordered rarity pool
    /// with the persistent treasure-room relic stream.
    /// </summary>
    public static PrototypeRelicBagState CreateNative(
        PlayerState player, RngBundle nativeRunRng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(nativeRunRng);
        string[] Make(PrototypeRelicRarity rarity)
        {
            var ids = EligibleIds(player, rarity);
            var originalIds = RandomBoundaryJournalV111.HasActiveJournal
                ? (string[])ids.Clone()
                : null;
            var decisions = NativeFisherYatesV111.ShuffleInPlace(
                ids,
                nativeRunRng,
                new NativeRandomIndexProviderV111(),
                "run.treasure_room_relics",
                "player:0:relic_grab_bag:" + rarity,
                QueueShuffleSiteId);
            if (originalIds is not null)
                SemanticPoolShuffleChoiceV111.RecordNativeAudit(
                    originalIds, ids, decisions, originalIds.Length,
                    SemanticPoolShuffleChoiceV111.NativeRelicQueueOperation,
                    null);
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

/// <summary>Source fallback result before the queue's hidden identity order is
/// resolved. CandidateIds preserve the committed queue order for Native.</summary>
public sealed record PrototypeRelicBagPullPlanV111
{
    private string[] _candidateIds;

    [System.Text.Json.Serialization.JsonConstructor]
    public PrototypeRelicBagPullPlanV111(
        PrototypeRelicRarity RequestedRarity,
        PrototypeRelicRarity? SelectedTier,
        string[] CandidateIds)
    {
        this.RequestedRarity = RequestedRarity;
        this.SelectedTier = SelectedTier;
        _candidateIds = CloneRequired(CandidateIds);
    }

    public PrototypeRelicRarity RequestedRarity { get; init; }
    public PrototypeRelicRarity? SelectedTier { get; init; }

    public string[] CandidateIds
    {
        get => (string[])_candidateIds.Clone();
        init => _candidateIds = CloneRequired(value);
    }

    public PrototypeRelicBagPullPlanV111 Fork() => this with
    {
        CandidateIds = _candidateIds
    };

    public void Deconstruct(
        out PrototypeRelicRarity RequestedRarity,
        out PrototypeRelicRarity? SelectedTier,
        out string[] CandidateIds)
    {
        RequestedRarity = this.RequestedRarity;
        SelectedTier = this.SelectedTier;
        CandidateIds = this.CandidateIds;
    }

    private static string[] CloneRequired(string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (string[])values.Clone();
    }
}

    /// <summary>
    /// MerchantRelicEntry.CalcCost uses Shops.NextFloat(0.85, 1.15)
    /// and Math.Round. This retained legacy overload approximates it with
    /// integer hundredths; configured runs use MerchantPriceNative.
    /// </summary>
    public static int MerchantPrice(
        string relicId, RngBundle rng,
        string ownerId = "shop:merchant:relic")
    {
        var percent = 85 + LegacyRandomChoiceV111.ChooseIndexRange(
            rng,
            "shop.merchant.relic_price_modifier.index_draw",
            "shop",
            ownerId + ":" + relicId,
            31).SelectedIndex;
        return (int)Math.Round(MerchantBaseCost(relicId) * percent / 100.0);
    }

    public static int MerchantPriceNative(
        string relicId, RngBundle nativeRunRng, string ownerId)
    {
        var roll = NativePlayerRngV111.NextUnitFloat(
            nativeRunRng,
            "shop.merchant.relic_price_modifier.unit_float",
            NativePlayerRngV111.ShopsStreamId,
            ownerId + ":" + relicId);
        return ResolveMerchantPriceNative(relicId, roll);
    }

    internal static int ResolveMerchantPriceNative(
        string relicId, float roll)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relicId);
        if (!float.IsFinite(roll) || roll < 0 || roll > 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var modifier = 0.85f + (1.15f - 0.85f) * roll;
        return (int)MathF.Round(MerchantBaseCost(relicId) * modifier,
            MidpointRounding.ToEven);
    }

    public static PrototypeRelicRarity RollRarity(
        RngBundle rng, bool merchant = false,
        string ownerId = "relic_grab_bag:reward")
    {
        // The legacy ticket approximation and native float path share the
        // native nominal 50/33/17 rarity partition.
        var commonTicketCount = CommonRarityWeight / 100;
        var uncommonTicketEnd = (CommonRarityWeight +
            UncommonRarityWeight) / 100;
        var ticketLabels = LegacyRarityTicketLabels();
        int roll;
        using (ControlledEventChoiceReplayV111.DescribeCandidates(
            ticketLabels))
        {
            roll = LegacyRandomChoiceV111.ChooseBoundedInteger(
                rng,
                "relic_grab_bag.rarity.ticket",
                "reward",
                ownerId + (merchant ? ":merchant" : ":reward"),
                100).SelectedIndex;
        }
        return roll < commonTicketCount
            ? PrototypeRelicRarity.Common
            : roll < uncommonTicketEnd
                ? PrototypeRelicRarity.Uncommon
                : PrototypeRelicRarity.Rare;
    }

    public static PrototypeRelicRarity RollRarityNative(
        RngBundle nativeRunRng, bool merchant = false,
        string ownerId = "relic_grab_bag:reward")
    {
        var roll = NativePlayerRngV111.NextUnitFloat(
            nativeRunRng,
            "relic_grab_bag.rarity.unit_float",
            NativePlayerRngV111.RewardsStreamId,
            ownerId + (merchant ? ":merchant" : ":reward"));
        return ResolveRarityNative(roll);
    }

    internal static PrototypeRelicRarity ResolveRarityNative(float roll)
    {
        if (!float.IsFinite(roll) || roll < 0 || roll > 1)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var commonThreshold = CommonRarityWeight /
            (float)RarityWeightScale;
        var uncommonThreshold = (CommonRarityWeight +
            UncommonRarityWeight) / (float)RarityWeightScale;
        return roll < commonThreshold
            ? PrototypeRelicRarity.Common
            : roll < uncommonThreshold
                ? PrototypeRelicRarity.Uncommon
                : PrototypeRelicRarity.Rare;
    }

    public static (PrototypeRelicBagState Bag, string? Id) Pull(
        PrototypeRelicBagState bag, PrototypeRelicRarity rarity,
        bool merchant, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (bag.OrderMode == PrototypeRelicBagOrderMode.NatureUnordered)
            throw new NotSupportedException(
                "Nature relic identity sets require a controlled Chance Law selection; queue-order pull is unsupported.");
        var plan = PlanPull(bag, rarity, merchant, player);
        return plan.CandidateIds.Length == 0
            ? (bag, null)
            : PullSelected(bag, plan, merchant, player,
                plan.CandidateIds[0]);
    }

    /// <summary>
    /// Selects the first nonempty fallback tier and returns its eligible IDs
    /// in source queue order. Native pulls choose CandidateIds[0]; a semantic
    /// continuation can canonicalize the support and pass its chosen identity
    /// to PullSelected.
    /// </summary>
    public static PrototypeRelicBagPullPlanV111 PlanPull(
        PrototypeRelicBagState bag, PrototypeRelicRarity rarity,
        bool merchant, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(player);
        foreach (var tier in PullFallbackTiers(rarity))
        {
            var candidateIds = EligiblePullIds(
                bag, tier, merchant, player);
            if (candidateIds.Length > 0)
                return new PrototypeRelicBagPullPlanV111(
                    rarity, tier, candidateIds);
        }

        return new PrototypeRelicBagPullPlanV111(rarity, null, []);
    }

    /// <summary>
    /// Plans a Nature identity pull over the canonical, unordered remainder
    /// of a relic bag. Native queue order must never be used as Nature support.
    /// </summary>
    public static PrototypeRelicBagPullPlanV111 PlanNaturePull(
        PrototypeRelicBagState bag, PrototypeRelicRarity rarity,
        bool merchant, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (bag.OrderMode != PrototypeRelicBagOrderMode.NatureUnordered)
            throw new InvalidDataException(
                "Nature relic identity support requires an unordered relic bag.");
        var plan = PlanPull(bag, rarity, merchant, player);
        return plan with
        {
            CandidateIds = plan.CandidateIds
                .OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
    }

    /// <summary>
    /// Replays one selected identity against the current Nature bag support
    /// and applies that removal through the shared relic-bag owner.
    /// </summary>
    public static (PrototypeRelicBagState Bag, string Id)
        PullNatureSelected(
            PrototypeRelicBagState bag, PrototypeRelicRarity rarity,
            string[] candidateIds, int selectedIndex, bool merchant,
            PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(candidateIds);
        ArgumentNullException.ThrowIfNull(player);
        var plan = PlanNaturePull(bag, rarity, merchant, player);
        if (plan.SelectedTier is null ||
            !candidateIds.SequenceEqual(
                plan.CandidateIds, StringComparer.Ordinal) ||
            (uint)selectedIndex >= (uint)plan.CandidateIds.Length)
            throw new InvalidDataException(
                "Nature relic selection differs from the current canonical bag support.");
        var selectedId = plan.CandidateIds[selectedIndex];
        var (nextBag, pulledId) = PullSelected(
            bag, plan, merchant, player, selectedId);
        if (!StringComparer.Ordinal.Equals(pulledId, selectedId))
            throw new InvalidDataException(
                "Nature relic selection did not remove its selected identity.");
        return (nextBag, selectedId);
    }

    /// <summary>Removes one legal identity selected from PlanPull's currently
    /// active fallback tier while preserving all other queue entries.</summary>
    public static (PrototypeRelicBagState Bag, string? Id) PullSelected(
        PrototypeRelicBagState bag, PrototypeRelicBagPullPlanV111 plan,
        bool merchant, PlayerState player, string? selectedId)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(player);
        if (plan.SelectedTier is not { } tier || selectedId is null)
        {
            if (plan.SelectedTier is null && plan.CandidateIds.Length == 0 &&
                selectedId is null)
                return (bag, null);
            throw new InvalidOperationException(
                "A relic pull selection does not match a nonempty fallback plan.");
        }
        var currentPlan = PlanPull(
            bag, plan.RequestedRarity, merchant, player);
        if (currentPlan.SelectedTier != tier ||
            !currentPlan.CandidateIds.OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(plan.CandidateIds.OrderBy(
                    id => id, StringComparer.Ordinal), StringComparer.Ordinal) ||
            !currentPlan.CandidateIds.Contains(selectedId,
                StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Relic pull plan is stale or the selected identity is ineligible.");

        var remaining = QueueForTier(bag, tier).Where(candidate =>
            !StringComparer.Ordinal.Equals(candidate, selectedId)).ToArray();
        return (tier switch
        {
            PrototypeRelicRarity.Common => bag with { Common = remaining },
            PrototypeRelicRarity.Uncommon => bag with { Uncommon = remaining },
            PrototypeRelicRarity.Rare => bag with { Rare = remaining },
            _ => bag with { Shop = remaining }
        }, selectedId);
    }

    /// <summary>
    /// Source fallback order shared by mechanics resolution and conditional
    /// laws for a still-hidden rarity-queue permutation.
    /// </summary>
    internal static PrototypeRelicRarity[] PullFallbackTiers(
        PrototypeRelicRarity rarity) => rarity switch
    {
        PrototypeRelicRarity.Shop =>
            [PrototypeRelicRarity.Shop, PrototypeRelicRarity.Common,
                PrototypeRelicRarity.Uncommon, PrototypeRelicRarity.Rare],
        PrototypeRelicRarity.Common =>
            [PrototypeRelicRarity.Common, PrototypeRelicRarity.Uncommon,
                PrototypeRelicRarity.Rare],
        PrototypeRelicRarity.Uncommon =>
            [PrototypeRelicRarity.Uncommon, PrototypeRelicRarity.Rare],
        PrototypeRelicRarity.Rare => [PrototypeRelicRarity.Rare],
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };

    /// <summary>
    /// Returns currently eligible identities in one rarity partition. Native
    /// state retains queue order; Nature state retains only canonical support.
    /// Merchant draws inspect the Native queue back and apply the source
    /// merchant exclusion set; reward draws inspect its front.
    /// </summary>
    internal static string[] EligiblePullIds(
        PrototypeRelicBagState bag, PrototypeRelicRarity tier,
        bool merchant, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(player);
        var ids = QueueForTier(bag, tier);
        var orderedIds = merchant ? ids.Reverse() : ids;
        return orderedIds.Where(candidate =>
            !player.Relics.Any(relic => StringComparer.Ordinal.Equals(
                relic.RelicId, candidate))
            && (!merchant || !NotSoldInShops.Contains(candidate)))
            .ToArray();
    }

    private static string[] QueueForTier(
        PrototypeRelicBagState bag, PrototypeRelicRarity tier) => tier switch
    {
        PrototypeRelicRarity.Common => bag.Common,
        PrototypeRelicRarity.Uncommon => bag.Uncommon,
        PrototypeRelicRarity.Rare => bag.Rare,
        PrototypeRelicRarity.Shop => bag.Shop,
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };

    /// <summary>
    /// RelicCmd.Obtain can grant a specific relic outside the ordinary
    /// grab-bag draw path (events, Ancients and special rewards). Such
    /// acquired entries must be removed from all persistent queues before
    /// the next decision boundary. Do not reshuffle or consume RNG.
    /// </summary>
    public static RunWorldState PruneOwned(
        RunWorldState world, PlayerState player)
    {
        if (world.Stochastic.RelicBag is not { } bag)
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
        return world with
        {
            Stochastic = RunWorldStochasticStateV111.CommitRelicBag(
                world.Stochastic, pruned)
        };
    }

    public static (RunWorldState World, string? Id) Draw(
        RunWorldState world, PlayerState player, RngBundle rng,
        bool merchant, PrototypeRelicRarity? forcedRarity = null,
        RngBundle? nativeRunRng = null,
        int merchantOfferOrdinal = 0)
    {
        var nativeEventChoicePull = !merchant && nativeRunRng is not null &&
            ControlledEventChoiceReplayV111.IsActive &&
            ControlledEventChoiceReplayV111.IsEventChoiceSite(
                NativeTreasureModelSiteId);
        var legacyNatureTreasurePull = !merchant && nativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            ControlledStochasticChanceReplayV111.IsActive;
        var legacyNatureMerchantPull = merchant &&
            nativeRunRng is null &&
            PrototypeGameEngine.UsesNativeRegionalSystems(world) &&
            ControlledStochasticChanceReplayV111.IsActive;
        var naturePull = legacyNatureTreasurePull ||
            legacyNatureMerchantPull ||
            (nativeRunRng is not null &&
                (ControlledStochasticChanceReplayV111.IsActive ||
                 nativeEventChoicePull));
        var bag = world.Stochastic.RelicBag ?? (naturePull
            ? CreateNatureUnordered(player)
            : nativeRunRng is null
                ? Create(player, rng)
                : CreateNative(player, nativeRunRng));
        if (!Enum.IsDefined(bag.OrderMode))
            throw new InvalidOperationException(
                "Relic bag has an unknown queue order mode.");
        if (bag.OrderMode == PrototypeRelicBagOrderMode.NatureUnordered &&
            !naturePull)
            throw new NotSupportedException(
                "Nature relic identity sets require a controlled Native relic-pull Chance Law replay.");
        var rarityOwnerId = merchant
            ? $"act:{world.Act}:floor:{world.Floor}:merchant_relic:{merchantOfferOrdinal}"
            : "relic_grab_bag";
        var rarity = forcedRarity ?? (nativeRunRng is null
            ? RollRarity(rng, merchant)
            : RollRarityNative(nativeRunRng, merchant, rarityOwnerId));
        (PrototypeRelicBagState Bag, string? Id) pull;
        if (!merchant && naturePull)
        {
            var plan = PlanNaturePull(
                bag, rarity, merchant: false, player);
            if (plan.CandidateIds.Length == 0)
            {
                pull = (bag, null);
            }
            else
            {
                var candidates = plan.CandidateIds;
                var ownerId = TreasureModelOwnerId(world, rarity, candidates);
                int selectedIndex;
                using (nativeEventChoicePull
                    ? ControlledEventChoiceReplayV111
                        .DescribeCandidates(candidates)
                    : ControlledStochasticChanceReplayV111
                        .DescribeCandidates(candidates))
                {
                    selectedIndex = nativeRunRng is null
                        ? LegacyRandomChoiceV111.ChooseIndexRange(
                            rng, NativeTreasureModelSiteId, "reward",
                            ownerId, candidates.Length).SelectedIndex
                        : NativeRandomChoiceV111.ChooseIndexRange(
                            nativeRunRng, NativeTreasureModelSiteId,
                            NativePlayerRngV111.RewardsStreamId, ownerId,
                            candidates.Length,
                            NativePlayerRngV111.NextOrdinal(
                                nativeRunRng,
                                NativePlayerRngV111.RewardsStreamId))
                            .SelectedIndex;
                }
                var selected = PullNatureSelected(
                    bag, rarity, candidates, selectedIndex,
                    merchant: false, player);
                pull = (selected.Bag, selected.Id);
            }
        }
        else if (merchant && nativeRunRng is not null &&
            ControlledStochasticChanceReplayV111.IsActive)
        {
            if (merchantOfferOrdinal < 0 || merchantOfferOrdinal >= 3)
                throw new ArgumentOutOfRangeException(
                    nameof(merchantOfferOrdinal));
            var plan = PlanNaturePull(
                bag, rarity, merchant: true, player);
            if (plan.CandidateIds.Length == 0)
            {
                pull = (bag, null);
            }
            else
            {
                var candidates = plan.CandidateIds;
                var ownerId = MerchantModelOwnerId(
                    world, merchantOfferOrdinal, rarity, candidates);
                var ordinal = NativePlayerRngV111.NextOrdinal(
                    nativeRunRng, NativePlayerRngV111.ShopsStreamId);
                int selectedIndex;
                using (ControlledStochasticChanceReplayV111
                    .DescribeCandidates(candidates))
                {
                    selectedIndex = NativeRandomChoiceV111.ChooseIndexRange(
                        nativeRunRng, NativeMerchantModelSiteId,
                        NativePlayerRngV111.ShopsStreamId, ownerId,
                        candidates.Length, ordinal).SelectedIndex;
                }
                var selected = PullNatureSelected(
                    bag, rarity, candidates, selectedIndex,
                    merchant: true, player);
                pull = (selected.Bag, selected.Id);
            }
        }
        else if (legacyNatureMerchantPull)
        {
            if (merchantOfferOrdinal < 0 || merchantOfferOrdinal >= 3)
                throw new ArgumentOutOfRangeException(
                    nameof(merchantOfferOrdinal));
            var plan = PlanNaturePull(
                bag, rarity, merchant: true, player);
            if (plan.CandidateIds.Length == 0)
            {
                pull = (bag, null);
            }
            else
            {
                var candidates = plan.CandidateIds;
                var ownerId = MerchantModelOwnerId(
                    world, merchantOfferOrdinal, rarity, candidates);
                int selectedIndex;
                using (ControlledStochasticChanceReplayV111
                    .DescribeCandidates(candidates))
                {
                    selectedIndex = LegacyRandomChoiceV111.ChooseIndexRange(
                        rng, NativeMerchantModelSiteId, "shop", ownerId,
                        candidates.Length).SelectedIndex;
                }
                var selected = PullNatureSelected(
                    bag, rarity, candidates, selectedIndex,
                    merchant: true, player);
                pull = (selected.Bag, selected.Id);
            }
        }
        else
        {
            pull = Pull(bag, rarity, merchant, player);
        }
        var (remaining, id) = pull;
        return (world with
        {
            Stochastic = RunWorldStochasticStateV111.CommitRelicBag(
                world.Stochastic, remaining)
        }, id);
    }

    public static string MerchantModelOwnerId(
        RunWorldState world, int offerOrdinal,
        PrototypeRelicRarity rarity, string[] orderedCandidateIds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(orderedCandidateIds);
        return $"act:{world.Act}:floor:{world.Floor}:merchant_relic:" +
            $"{offerOrdinal}:rarity:{rarity}:pool:" +
            CanonicalJson.Sha256(orderedCandidateIds);
    }
}
