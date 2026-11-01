namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111 fully-unlocked starting relic-bag initialization.
/// Source: SharedRelicPool, SilentRelicPool, RelicGrabBag.Populate,
/// RunManager.InitializeNewRun (STS2-source@706ef1c9).
/// Source model names are retained rather than mapped to the emulator's
/// incomplete relic registry. This is the first independent source
/// startup phase; NativeUpFrontRoomsV111 follows it on the same stream.
/// </summary>
public static class NativeUpFrontRelicBagsV111
{
    public enum RelicRarity { Common, Uncommon, Rare, Shop, Event, Ancient, Starter }
    public sealed record SourceRelic(string ModelName, RelicRarity Rarity);
    public sealed record Bag(
        string[] Common, string[] Uncommon, string[] Rare, string[] Shop,
        string[] Event, string[] Ancient)
    {
        public Bag Fork() => new(
            (string[])Common.Clone(), (string[])Uncommon.Clone(),
            (string[])Rare.Clone(), (string[])Shop.Clone(),
            (string[])Event.Clone(), (string[])Ancient.Clone());
        public int TotalCount => Common.Length + Uncommon.Length
            + Rare.Length + Shop.Length + Event.Length + Ancient.Length;
    }

    // Exact Source Pool.GenerateAllRelics order (before unlock filtering).
    private static readonly SourceRelic[] Shared =
    [
        new("Akabeko", RelicRarity.Uncommon),
        new("AmethystAubergine", RelicRarity.Common),
        new("Anchor", RelicRarity.Common),
        new("ArtOfWar", RelicRarity.Rare),
        new("BagOfMarbles", RelicRarity.Common),
        new("BagOfPreparation", RelicRarity.Common),
        new("BeatingRemnant", RelicRarity.Rare),
        new("Bellows", RelicRarity.Rare),
        new("BeltBuckle", RelicRarity.Shop),
        new("BloodVial", RelicRarity.Common),
        new("BookOfFiveRings", RelicRarity.Common),
        new("BowlerHat", RelicRarity.Uncommon),
        new("Bread", RelicRarity.Shop),
        new("BronzeScales", RelicRarity.Common),
        new("BurningSticks", RelicRarity.Shop),
        new("Candelabra", RelicRarity.Uncommon),
        new("CaptainsWheel", RelicRarity.Rare),
        new("Cauldron", RelicRarity.Shop),
        new("CentennialPuzzle", RelicRarity.Common),
        new("Chandelier", RelicRarity.Rare),
        new("ChemicalX", RelicRarity.Shop),
        new("CloakClasp", RelicRarity.Rare),
        new("DingyRug", RelicRarity.Shop),
        new("DollysMirror", RelicRarity.Shop),
        new("DragonFruit", RelicRarity.Shop),
        new("EternalFeather", RelicRarity.Uncommon),
        new("FestivePopper", RelicRarity.Common),
        new("FresnelLens", RelicRarity.Event),
        new("FrozenEgg", RelicRarity.Rare),
        new("GamblingChip", RelicRarity.Rare),
        new("GamePiece", RelicRarity.Rare),
        new("GhostSeed", RelicRarity.Shop),
        new("Girya", RelicRarity.Rare),
        new("GnarledHammer", RelicRarity.Shop),
        new("Gorget", RelicRarity.Common),
        new("GremlinHorn", RelicRarity.Uncommon),
        new("HappyFlower", RelicRarity.Common),
        new("HornCleat", RelicRarity.Uncommon),
        new("IceCream", RelicRarity.Rare),
        new("IntimidatingHelmet", RelicRarity.Rare),
        new("JossPaper", RelicRarity.Uncommon),
        new("JuzuBracelet", RelicRarity.Common),
        new("Kifuda", RelicRarity.Shop),
        new("Kunai", RelicRarity.Rare),
        new("Kusarigama", RelicRarity.Uncommon),
        new("Lantern", RelicRarity.Common),
        new("LastingCandy", RelicRarity.Uncommon),
        new("LavaLamp", RelicRarity.Shop),
        new("LeesWaffle", RelicRarity.Shop),
        new("LetterOpener", RelicRarity.Uncommon),
        new("LizardTail", RelicRarity.Rare),
        new("LoomingFruit", RelicRarity.Ancient),
        new("LuckyFysh", RelicRarity.Uncommon),
        new("Mango", RelicRarity.Rare),
        new("MealTicket", RelicRarity.Common),
        new("MeatOnTheBone", RelicRarity.Rare),
        new("MembershipCard", RelicRarity.Shop),
        new("MercuryHourglass", RelicRarity.Uncommon),
        new("MiniatureCannon", RelicRarity.Uncommon),
        new("MiniatureTent", RelicRarity.Shop),
        new("MoltenEgg", RelicRarity.Rare),
        new("MummifiedHand", RelicRarity.Rare),
        new("MysticLighter", RelicRarity.Shop),
        new("Nunchaku", RelicRarity.Uncommon),
        new("OddlySmoothStone", RelicRarity.Common),
        new("OldCoin", RelicRarity.Rare),
        new("Orichalcum", RelicRarity.Uncommon),
        new("OrnamentalFan", RelicRarity.Uncommon),
        new("Orrery", RelicRarity.Shop),
        new("Pantograph", RelicRarity.Uncommon),
        new("ParryingShield", RelicRarity.Uncommon),
        new("Pear", RelicRarity.Uncommon),
        new("PenNib", RelicRarity.Uncommon),
        new("Pendulum", RelicRarity.Common),
        new("Permafrost", RelicRarity.Uncommon),
        new("PetrifiedToad", RelicRarity.Uncommon),
        new("Planisphere", RelicRarity.Uncommon),
        new("Pocketwatch", RelicRarity.Rare),
        new("PotionBelt", RelicRarity.Common),
        new("PrayerWheel", RelicRarity.Rare),
        new("PunchDagger", RelicRarity.Shop),
        new("RainbowRing", RelicRarity.Rare),
        new("RazorTooth", RelicRarity.Rare),
        new("RedMask", RelicRarity.Common),
        new("RegalPillow", RelicRarity.Common),
        new("ReptileTrinket", RelicRarity.Uncommon),
        new("RingingTriangle", RelicRarity.Shop),
        new("RippleBasin", RelicRarity.Uncommon),
        new("RoyalStamp", RelicRarity.Shop),
        new("ScreamingFlagon", RelicRarity.Shop),
        new("Shovel", RelicRarity.Rare),
        new("Shuriken", RelicRarity.Rare),
        new("SlingOfCourage", RelicRarity.Shop),
        new("SparklingRouge", RelicRarity.Uncommon),
        new("StoneCalendar", RelicRarity.Rare),
        new("StoneCracker", RelicRarity.Uncommon),
        new("Strawberry", RelicRarity.Common),
        new("StrikeDummy", RelicRarity.Common),
        new("SturdyClamp", RelicRarity.Rare),
        new("TheAbacus", RelicRarity.Shop),
        new("TheCourier", RelicRarity.Rare),
        new("TinyMailbox", RelicRarity.Uncommon),
        new("Toolbox", RelicRarity.Shop),
        new("ToxicEgg", RelicRarity.Rare),
        new("TungstenRod", RelicRarity.Rare),
        new("TuningFork", RelicRarity.Uncommon),
        new("UnceasingTop", RelicRarity.Rare),
        new("UnsettlingLamp", RelicRarity.Rare),
        new("Vajra", RelicRarity.Common),
        new("Vambrace", RelicRarity.Uncommon),
        new("VenerableTeaSet", RelicRarity.Common),
        new("VeryHotCocoa", RelicRarity.Ancient),
        new("VexingPuzzlebox", RelicRarity.Rare),
        new("WarPaint", RelicRarity.Common),
        new("Whetstone", RelicRarity.Common),
        new("WhiteBeastStatue", RelicRarity.Rare),
        new("WhiteStar", RelicRarity.Rare),
        new("WingCharm", RelicRarity.Shop),
    ];
    private static readonly SourceRelic[] Silent =
    [
        new("HelicalDart", RelicRarity.Rare),
        new("NinjaScroll", RelicRarity.Shop),
        new("PaperKrane", RelicRarity.Rare),
        new("RingOfTheSnake", RelicRarity.Starter),
        new("SneckoSkull", RelicRarity.Common),
        new("Tingsha", RelicRarity.Uncommon),
        new("ToughBandages", RelicRarity.Rare),
        new("TwistedFunnel", RelicRarity.Uncommon),
    ];

    public sealed record Result(
        Bag SharedBag, Bag PlayerBag,
        NativeRngSnapshot AfterSharedBag,
        NativeRngSnapshot AfterPlayerBag)
    {
        public Result Fork() => this with
        {
            SharedBag = SharedBag.Fork(),
            PlayerBag = PlayerBag.Fork()
        };
    }

    /// <summary>
    /// RunManager.InitializeNewRun calls SharedRelicGrabBag.Populate
    /// followed by Player.PopulateRelicGrabBagIfNecessary. Both use the
    /// SAME run.up_front stream. Group buckets by rarity in first-seen
    /// source order and UnstableShuffle each bucket independently.
    /// Source RelicGrabBag.Populate(IEnumerable) retains all rarities in
    /// the shared bag, including Event and Ancient. Its two Ancient relics
    /// require one extra Fisher-Yates draw. The player overload filters to
    /// Common/Uncommon/Rare/Shop before shuffling. This difference was
    /// confirmed by the native RunStarted three-act queue capture for
    /// seed 6JK8MD0VQQTA (2026-11-01).
    /// </summary>
    public static Result Populate(RngBundle runStreams)
    {
        ArgumentNullException.ThrowIfNull(runStreams);
        if (Shared.Length != 118 || Silent.Length != 8
            || Shared.Concat(Silent).Select(e => e.ModelName)
                .Distinct(StringComparer.Ordinal).Count() != 126)
            throw new InvalidDataException(
                "Pinned source pool name/inventory changed.");
        var shared = Shuffle(Shared, runStreams, retainAllRarities: true);
        var first = NativeRngV111.ToSnapshot(runStreams.Streams.Single(
            stream => stream.StreamId == "run.up_front"));
        var player = Shuffle(Shared.Concat(Silent), runStreams,
            retainAllRarities: false);
        var second = NativeRngV111.ToSnapshot(runStreams.Streams.Single(
            stream => stream.StreamId == "run.up_front"));
        if (shared.TotalCount != 118 || player.TotalCount != 122
            || shared.Event.Length != 1 || shared.Ancient.Length != 2
            || player.Event.Length != 0 || player.Ancient.Length != 0
            || first.Counter != 112 || second.Counter != 230)
            throw new InvalidDataException(
                "Pinned relic bag bucket sizes or shuffle call count changed.");
        return new Result(shared, player, first, second);
    }

    private static Bag Shuffle(IEnumerable<SourceRelic> source,
        RngBundle runStreams, bool retainAllRarities)
    {
        // Overload-specific source behavior: the shared enumerable overload
        // retains Ancient and Event buckets; the player overload filters them.
        var eligible = (retainAllRarities ? source :
            source.Where(p => p.Rarity is RelicRarity.Common or
                RelicRarity.Uncommon or RelicRarity.Rare or
                RelicRarity.Shop)).ToArray();
        var buckets = eligible.GroupBy(p => p.Rarity)
            .Select(g => (Rarity: g.Key,
                Values: g.Select(e => e.ModelName).ToList())).ToArray();
        if (buckets.Length != 4)
            throw new InvalidDataException(
                "Source relic bag must own four rarity buckets.");
        foreach (var bucket in buckets)
            NativeRngV111.Shuffle(runStreams, "run.up_front", bucket.Values);
        string[] Get(RelicRarity rarity) =>
            buckets.Where(bucket => bucket.Rarity == rarity)
                .SelectMany(bucket => bucket.Values).ToArray();
        return new Bag(Get(RelicRarity.Common),
            Get(RelicRarity.Uncommon), Get(RelicRarity.Rare),
            Get(RelicRarity.Shop), Get(RelicRarity.Event),
            Get(RelicRarity.Ancient));
    }
}
