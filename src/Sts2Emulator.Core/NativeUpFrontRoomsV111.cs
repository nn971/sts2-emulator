namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111 fully-unlocked single-player RunManager.GenerateRooms
/// translation. Source: Flaaax/STS2-source@706ef1c9 RunManager,
/// ActModel, GrabBag, ModelDb, encounter Tags, and Overgrowth/Hive/Glory.
/// The generated source-model identities are retained for later room entry;
/// the existing prototype world is not yet driven by these queues.
/// </summary>
public static class NativeUpFrontRoomsV111
{
    private const string Stream = "run.up_front";

    public sealed record ActRooms(
        int Act, string[] Events, string[] NormalEncounters,
        string[] EliteEncounters, string BossEncounter,
        string Ancient, string? SecondBossEncounter,
        int RejectedCandidateDraws,
        NativeRngSnapshot AfterEvents, NativeRngSnapshot AfterRooms)
    {
        public ActRooms Fork() => this with
        {
            Events = (string[])Events.Clone(),
            NormalEncounters = (string[])NormalEncounters.Clone(),
            EliteEncounters = (string[])EliteEncounters.Clone()
        };
    }

    public sealed record Result(
        string[] HiveSharedAncients, string[] GlorySharedAncients,
        ActRooms[] Acts, NativeRngSnapshot AfterSharedAncients,
        NativeRngSnapshot FinalRng)
    {
        public Result Fork() => this with
        {
            HiveSharedAncients = (string[])HiveSharedAncients.Clone(),
            GlorySharedAncients = (string[])GlorySharedAncients.Clone(),
            Acts = Acts.Select(act => act.Fork()).ToArray()
        };
    }

    private sealed record ActCatalog(
        int Act, int WeakCount, int RoomCount,
        string[] Events, string[] Weak, string[] Normal,
        string[] Elite, string[] Boss, string[] Ancients);

    private static string[] Words(string names) =>
        names.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // Source order is meaningful: AllEvents followed by AllSharedEvents;
    // encounters are filtered from GenerateAllEncounters without sorting.
    private static readonly string[] SharedEvents = Words(
        "BrainLeech CrystalSphere DollRoom FakeMerchant PotionCourier " +
        "RanwidTheElder RelicTrader RoomFullOfCheese SelfHelpBook " +
        "SlipperyBridge StoneOfAllTime Symbiote TeaMaster " +
        "TheFutureOfPotions TheLegendsWereTrue ThisOrThat " +
        "WarHistorianRepy WelcomeToWongos");

    private static readonly ActCatalog[] Catalogs =
    [
        new(1, 3, 15,
            Words("AromaOfChaos ByrdonisNest DenseVegetation JungleMazeAdventure LuminousChoir MorphicGrove SapphireSeed SunkenStatue TabletOfTruth UnrestSite Wellspring WhisperingHollow WoodCarvings"),
            Words("FuzzyWurmCrawlerWeak NibbitsWeak ShrinkerBeetleWeak SlimesWeak"),
            Words("CubexConstructNormal FlyconidNormal FogmogNormal InkletsNormal MawlerNormal NibbitsNormal OvergrowthCrawlers RubyRaidersNormal SlimesNormal SlitheringStranglerNormal SnappingJaxfruitNormal VineShamblerNormal"),
            Words("BygoneEffigyElite ByrdonisElite PhrogParasiteElite"),
            Words("CeremonialBeastBoss TheKinBoss VantomBoss"),
            Words("Neow")),
        new(2, 2, 14,
            Words("Amalgamator Bugslayer ColorfulPhilosophers ColossalFlower FieldOfManSizedHoles InfestedAutomaton LostWisp SpiritGrafter TheLanternKey ZenWeaver"),
            Words("BowlbugsWeak ExoskeletonsWeak ThievingHopperWeak TunnelerWeak"),
            Words("BowlbugsNormal ChompersNormal ExoskeletonsNormal HunterKillerNormal LouseProgenitorNormal MytesNormal OvicopterNormal SlumberingBeetleNormal SpinyToadNormal TheObscuraNormal"),
            Words("DecimillipedeElite EntomancerElite InfestedPrismsElite"),
            Words("KaiserCrabBoss KnowledgeDemonBoss TheInsatiableBoss"),
            Words("Orobas Pael Tezcatara")),
        new(3, 2, 13,
            Words("BattlewornDummy GraveOfTheForgotten HungryForMushrooms Reflections RoundTeaParty Trial TinkerTime"),
            Words("DevotedSculptorWeak ScrollsOfBitingWeak TurretOperatorWeak"),
            Words("AxebotsNormal ConstructMenagerieNormal FabricatorNormal FrogKnightNormal GlobeHeadNormal OwlMagistrateNormal ScrollsOfBitingNormal SlimedBerserkerNormal TheLostAndForgottenNormal"),
            Words("KnightsElite MechaKnightElite SoulNexusElite"),
            Words("AeonglassBoss QueenBoss TestSubjectBoss"),
            Words("Nonupeipe Tanx Vakuu"))
    ];

    // All nonempty source EncounterModel.Tags; equality uses set intersection
    // (including encounters with multiple tags).
    private static readonly Dictionary<string, string[]> Tags =
        new(StringComparer.Ordinal)
    {
        ["FlyconidNormal"] = Words("Mushroom Slimes"),
        ["FuzzyWurmCrawlerWeak"] = Words("Crawler"),
        ["NibbitsWeak"] = Words("Nibbit"),
        ["OvergrowthCrawlers"] = Words("Shrinker Crawler"),
        ["ShrinkerBeetleWeak"] = Words("Shrinker"),
        ["SlimesNormal"] = Words("Slimes"),
        ["SlimesWeak"] = Words("Slimes"),
        ["SlitheringStranglerNormal"] = Words("Jaxfruit Slimes"),
        ["SnappingJaxfruitNormal"] = Words("Mushroom Jaxfruit"),
        ["BowlbugsNormal"] = Words("Workers"),
        ["BowlbugsWeak"] = Words("Workers"),
        ["ChompersNormal"] = Words("Chomper"),
        ["ExoskeletonsNormal"] = Words("Exoskeletons"),
        ["ExoskeletonsWeak"] = Words("Exoskeletons"),
        ["SlumberingBeetleNormal"] = Words("Workers"),
        ["ThievingHopperWeak"] = Words("Thieves"),
        ["TunnelerWeak"] = Words("Burrower"),
        ["ScrollsOfBitingNormal"] = Words("Scrolls"),
        ["ScrollsOfBitingWeak"] = Words("Scrolls"),
        ["KnightsElite"] = Words("Knights"),
    };

    /// <summary>
    /// Continue the real source UpFront stream immediately after the two
    /// relic-bag populations (230 calls), preserving all three act queues.
    /// No anonymous fast-forwarding or native state import is performed.
    /// </summary>
    public static Result Generate(RngBundle runStreams, int ascension)
    {
        ArgumentNullException.ThrowIfNull(runStreams);
        if (ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        if (Snapshot(runStreams).Counter != 230)
            throw new InvalidDataException(
                "Native GenerateRooms must follow both source relic bags.");
        if (SharedEvents.Length != 18 || Catalogs.Length != 3)
            throw new InvalidDataException("Pinned source room pools changed.");

        // UnlockState.SharedAncients has exactly Darv with full unlocks.
        // The one-element unstable shuffle consumes zero calls.
        var shared = new List<string> { "Darv" };
        NativeRngV111.Shuffle(runStreams, Stream, shared);
        string[] AllocateSharedAncients()
        {
            var count = NativeRngV111.NextInt(runStreams, Stream,
                shared.Count + 1);
            var subset = shared.Take(count).ToArray();
            shared.RemoveRange(0, count);
            return subset;
        }
        var hiveShared = AllocateSharedAncients();
        var gloryShared = AllocateSharedAncients();
        var afterShared = Snapshot(runStreams);

        var acts = new List<ActRooms>(3);
        foreach (var catalog in Catalogs)
        {
            var events = catalog.Events.Concat(SharedEvents).ToList();
            NativeRngV111.Shuffle(runStreams, Stream, events);
            var afterEvents = Snapshot(runStreams);

            // ActModel maintains separate refillable, weighted bags for weak,
            // normal and elite sequences. Weak and normal share history.
            var rejections = 0;
            var history = new List<string>();
            var weak = GenerateSequence(
                runStreams, catalog.Weak, catalog.WeakCount, history,
                ref rejections);
            var normal = GenerateSequence(
                runStreams, catalog.Normal,
                catalog.RoomCount - catalog.WeakCount, history,
                ref rejections);
            var elite = GenerateSequence(
                runStreams, catalog.Elite, 15, new List<string>(),
                ref rejections);
            var boss = NextItem(runStreams, catalog.Boss);
            var availableAncients = catalog.Ancients.Concat(
                catalog.Act == 2 ? hiveShared :
                catalog.Act == 3 ? gloryShared : []).ToArray();
            var ancient = NextItem(runStreams, availableAncients);

            // A10 DoubleBoss is generated only after the final act's
            // regular boss/Ancient assignments.
            string? secondBoss = null;
            if (catalog.Act == 3 && ascension >= 10)
                secondBoss = NextItem(runStreams,
                    catalog.Boss.Where(id => id != boss).ToArray());

            acts.Add(new ActRooms(
                catalog.Act, events.ToArray(), weak.Concat(normal).ToArray(),
                elite, boss, ancient, secondBoss, rejections,
                afterEvents, Snapshot(runStreams)));
        }
        return new Result(hiveShared, gloryShared, acts.ToArray(),
            afterShared, Snapshot(runStreams));
    }

    private static NativeRngSnapshot Snapshot(RngBundle runStreams) =>
        NativeRngV111.ToSnapshot(runStreams.Streams.Single(
            stream => stream.StreamId == Stream));

    private static string NextItem(RngBundle runStreams, string[] pool)
    {
        if (pool.Length == 0)
            throw new InvalidDataException("Empty pinned source item pool.");
        return pool[NativeRngV111.NextInt(runStreams, Stream, pool.Length)];
    }

    private static string[] GenerateSequence(
        RngBundle rng, string[] pool, int count, List<string> history,
        ref int rejectedCandidateDraws)
    {
        if (pool.Length == 0)
            throw new InvalidDataException("Empty pinned encounter bag.");
        var pending = new List<string>();
        var selected = new string[count];
        for (var index = 0; index < count; index++)
        {
            if (pending.Count == 0)
                pending.AddRange(pool);
            var previous = history.Count > 0 ? history[^1] : null;
            bool Eligible(string id) =>
                previous is null ||
                (id != previous && !SharesTags(id, previous));

            // Native GrabBag.GrabIndex checks for any eligible candidate
            // before sampling. If none exists it returns -1 WITHOUT a draw;
            // ActModel then does one unrestricted fallback sample.
            var hasEligible = pending.Any(Eligible);
            int sampled;
            do
            {
                sampled = GrabIndex(rng, pending.Count);
                if (hasEligible && !Eligible(pending[sampled]))
                    rejectedCandidateDraws++;
                else
                    break;
            } while (true);
            var chosen = pending[sampled];
            pending.RemoveAt(sampled);
            history.Add(chosen);
            selected[index] = chosen;
        }
        return selected;
    }

    private static int GrabIndex(RngBundle rng, int count)
    {
        // Native GrabBag is uniform here (weight 1.0 per encounter),
        // sampling NextDouble * totalWeight then comparing cumulative
        // weights with a strict '<'. This always consumes one draw.
        var sample = NativeRngV111.NextDouble(rng, Stream) * count;
        for (var index = 0; index < count; index++)
            if (sample < index + 1.0) return index;
        throw new InvalidDataException("Invalid native weighted sample.");
    }

    private static bool SharesTags(string left, string right) =>
        Tags.TryGetValue(left, out var l) &&
        Tags.TryGetValue(right, out var r) &&
        l.Intersect(r, StringComparer.Ordinal).Any();
}
