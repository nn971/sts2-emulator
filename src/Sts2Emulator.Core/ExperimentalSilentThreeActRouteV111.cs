namespace Sts2Emulator.Core;

/// <summary>
/// The configured v111 Silent end-to-end integration route. Catalogues all
/// ordinary regional native encounter registrations and deliberately records
/// that selection distributions and combat RNG still require oracle audit.
/// Legacy prototype and default v111 modes are unaffected.
/// </summary>
public static class ExperimentalSilentThreeActRouteV111
{
    public static readonly string[] HiveWeak =
    [
        "proto.native.hive.encounter.bowlbugs_weak",
        "proto.native.hive.encounter.exoskeletons_weak",
        "proto.native.hive.encounter.thieving_hopper_weak",
        "proto.native.hive.encounter.tunneler_weak"
    ];
    public static readonly string[] HiveNormal =
    [
        PrototypeNativeHiveNormals.BowlbugsNormalId,
        PrototypeNativeHiveNormals.ChompersNormalId,
        PrototypeNativeHiveNormals.ExoskeletonsNormalId,
        PrototypeNativeHiveRemainingNormals.HunterKillerNormalId,
        PrototypeNativeHiveRemainingNormals.LouseProgenitorNormalId,
        PrototypeNativeHiveNormals.MytesNormalId,
        PrototypeNativeHiveSummoningNormals.OvicopterNormalId,
        PrototypeNativeHiveNormals.SlumberingBeetleNormalId,
        PrototypeNativeHiveNormals.SpinyToadNormalId,
        PrototypeNativeHiveSummoningNormals.ObscuraNormalId
    ];
    public static readonly string[] HiveElites =
    [
        PrototypeNativeHiveElites.DecimillipedeEncounterId,
        PrototypeNativeHiveElites.EntomancerEncounterId,
        PrototypeNativeHiveElites.PrismEncounterId,
    ];
    public static readonly string[] HiveBosses =
    [
        PrototypeNativeHiveBosses.KaiserEncounterId,
        PrototypeNativeHiveBosses.KnowledgeEncounterId,
        PrototypeNativeHiveBosses.InsatiableEncounterId
    ];
    // ActModel.BossDiscoveryOrder is independent of GenerateAllEncounters()
    // pool order. The source replaces the randomly preselected boss with the
    // first entry in this sequence the profile has not discovered yet.
    public static readonly string[] HiveBossDiscoveryOrder =
    [
        PrototypeNativeHiveBosses.InsatiableEncounterId,
        PrototypeNativeHiveBosses.KnowledgeEncounterId,
        PrototypeNativeHiveBosses.KaiserEncounterId
    ];
    public static readonly string[] GloryWeak =
    [
        PrototypeNativeLaterActs.GlorySculptorWeakId,
        PrototypeNativeGloryScrolls.WeakEncounterId,
        PrototypeNativeGloryWeak.TurretOperatorWeakId
    ];
    public static readonly string[] GloryNormal =
    [
        PrototypeNativeGloryNewNormals.AxebotsEncounterId,
        "proto.native.glory.encounter.construct_menagerie_normal",
        PrototypeNativeGloryNewNormals.FabricatorEncounterId,
        PrototypeNativeGloryNormals.FrogKnightEncounterId,
        PrototypeNativeGloryNewNormals.GlobeHeadEncounterId,
        PrototypeNativeGloryNewNormals.OwlEncounterId,
        PrototypeNativeGloryScrolls.NormalEncounterId,
        PrototypeNativeGloryNormals.SlimedBerserkerEncounterId,
        PrototypeNativeGloryNewNormals.LostAndForgottenEncounterId
    ];
    public static readonly string[] GloryElites =
    [
        PrototypeNativeGloryBosses.KnightsEncounterId,
        PrototypeNativeGloryElites.MechaKnightEncounterId,
        PrototypeNativeGloryElites.SoulNexusEncounterId
    ];
    public static readonly string[] GloryBosses =
    [
        PrototypeNativeGloryBosses.AeonglassEncounterId,
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId
    ];
    public static readonly string[] GloryBossDiscoveryOrder =
    [
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId,
        PrototypeNativeGloryBosses.AeonglassEncounterId
    ];

    // Colorful Philosophers is still unimplemented, so it is not
    // misrepresented as an executable event despite its native ID.
    public static readonly string[] HiveEvents =
    [
        PrototypeNativeBrainLeech.EventId,
        PrototypeNativeSelfHelpBook.EventId,
        PrototypeNativePotionCourier.EventId,
        PrototypeNativeHiveEvents.AmalgamatorId,
        PrototypeNativeLaterActs.HiveBugslayerId,
        PrototypeNativeHiveEvents.ColossalFlowerId,
        PrototypeNativeHiveEvents.FieldOfManSizedHolesId,
        PrototypeNativeHiveEvents.InfestedAutomatonId,
        PrototypeNativeHiveEvents.LostWispId,
        PrototypeNativeHiveEvents.SpiritGrafterId,
        PrototypeNativeHiveEvents.LanternKeyEventId,
        PrototypeNativeHiveEvents.ZenWeaverId,
        PrototypeNativeStoneOfAllTime.EventId,
        PrototypeNativeFakeMerchant.EventId,
        PrototypeNativeSharedEvents.ThisOrThatId,
        PrototypeNativeSharedEvents.SymbioteId,
        PrototypeNativeFutureOfPotions.EventId,
        PrototypeNativeRelicTrader.EventId
    ];
    public static readonly string[] GloryEvents =
    [
        PrototypeNativeSelfHelpBook.EventId,
        PrototypeNativePotionCourier.EventId,
        PrototypeNativeGloryEvents.HungryForMushroomsId,
        PrototypeNativeGloryEvents.ReflectionsId,
        PrototypeNativeGloryEvents.RoundTeaPartyId,
        PrototypeNativeGloryEvents.TeaMasterId,
        PrototypeNativeGloryEvents.GraveOfTheForgottenId,
        PrototypeNativeGloryEvents.TrialId,
        PrototypeNativeGloryEvents.TinkerTimeId,
        PrototypeNativeGloryEvents.BattlewornDummyId,
        PrototypeNativeFakeMerchant.EventId,
        PrototypeNativeSharedEvents.ThisOrThatId,
        PrototypeNativeSharedEvents.SymbioteId,
        PrototypeNativeFutureOfPotions.EventId,
        PrototypeNativeRelicTrader.EventId
    ];

    public static string[] Pool(int act, PrototypeRoomType room, bool weak)
        => (act, room, weak) switch
        {
            (2, PrototypeRoomType.Combat, true) => HiveWeak,
            (2, PrototypeRoomType.Combat, false) => HiveNormal,
            (2, PrototypeRoomType.Elite, _) => HiveElites,
            (2, PrototypeRoomType.Boss, _) => HiveBosses,
            (3, PrototypeRoomType.Combat, true) => GloryWeak,
            (3, PrototypeRoomType.Combat, false) => GloryNormal,
            (3, PrototypeRoomType.Elite, _) => GloryElites,
            (3, PrototypeRoomType.Boss, _) => GloryBosses,
            _ => throw new ArgumentOutOfRangeException(nameof(act))
        };

    public static bool ContainsEncounter(string encounterId, int act) =>
        act is 2 or 3
        && new[] { true, false }
            .SelectMany(weak => new[] {
                PrototypeRoomType.Combat, PrototypeRoomType.Elite,
                PrototypeRoomType.Boss
            }.SelectMany(room => Pool(act, room, weak)))
            .Contains(encounterId, StringComparer.Ordinal);

    public static bool ContainsEvent(string eventId, int act) =>
        (act == 2 ? HiveEvents : act == 3 ? GloryEvents : [])
            .Contains(eventId, StringComparer.Ordinal);

    /// <summary>
    /// Number of map rooms before the boss/Ancient in the pinned v111
    /// single-player ActModel. These are queue lengths, not the number
    /// of combats a particular map path must contain.
    /// </summary>
    public static int OrdinaryRoomCount(int act) => act switch
    {
        2 => 14, // Hive.BaseNumberOfRooms
        3 => 13, // Glory.BaseNumberOfRooms
        _ => throw new ArgumentOutOfRangeException(nameof(act))
    };

    // Source: EncounterModel.Tags and the individual encounter models at
    // Flaaax/STS2-source@706ef1c9. EncounterTag.None is represented by null.
    // The tag constraint spans the weak -> normal queue boundary.
    public static string? EncounterTag(string encounterId) => encounterId switch
    {
        "proto.native.hive.encounter.bowlbugs_weak" => "Workers",
        "proto.native.hive.encounter.exoskeletons_weak" => "Exoskeletons",
        "proto.native.hive.encounter.thieving_hopper_weak" => "Thieves",
        "proto.native.hive.encounter.tunneler_weak" => "Burrower",
        PrototypeNativeHiveNormals.BowlbugsNormalId => "Workers",
        PrototypeNativeHiveNormals.ChompersNormalId => "Chomper",
        PrototypeNativeHiveNormals.ExoskeletonsNormalId => "Exoskeletons",
        PrototypeNativeHiveNormals.SlumberingBeetleNormalId => "Workers",
        PrototypeNativeGloryScrolls.WeakEncounterId => "Scrolls",
        PrototypeNativeGloryScrolls.NormalEncounterId => "Scrolls",
        PrototypeNativeGloryBosses.KnightsEncounterId => "Knights",
        _ => null
    };

    /// <summary>
    /// ActModel.GenerateRooms eagerly constructs the entire weak, normal and
    /// elite histories using three separate refillable, without-replacement
    /// grab bags. Its AddWithoutRepeatingTags checks the immediately preceding
    /// encounter (including the weak -> normal boundary), and falls back to
    /// the whole remaining bag if every entry conflicts.
    ///
    /// Here uniform eligible choices use the PROTOTYPE combat RNG. The queue
    /// structure and exclusion distribution follow the pinned game rules;
    /// exact native NextDouble rejection draws/stream ownership are pending.
    /// </summary>
    private static string[] GenerateSequence(
        string[] pool, int count, RngBundle rng, List<string> history)
    {
        var remaining = new List<string>();
        var result = new string[count];
        for (var i = 0; i < result.Length; i++)
        {
            if (remaining.Count == 0)
                remaining.AddRange(pool);
            var previous = history.LastOrDefault();
            var previousTag = previous is null ? null : EncounterTag(previous);
            var eligible = remaining.Where(id =>
                id != previous
                && (previousTag is null
                    || EncounterTag(id) != previousTag)).ToArray();
            if (eligible.Length == 0)
                eligible = remaining.ToArray();
            var selected = eligible[PrototypeRng.NextInt(
                rng, "combat", eligible.Length)];
            remaining.Remove(selected);
            history.Add(selected);
            result[i] = selected;
        }
        return result;
    }

    public static PrototypeLaterActEncounterBagV111 InitialBag(
        int act, RngBundle rng, int ascension = 0,
        SimulationProfile? profile = null, bool assignAncient = false,
        bool assignDarv = false)
    {
        var rooms = OrdinaryRoomCount(act);
        var history = new List<string>(rooms);
        var weak = GenerateSequence(
            Pool(act, PrototypeRoomType.Combat, true), 2, rng, history);
        var normal = GenerateSequence(
            Pool(act, PrototypeRoomType.Combat, false),
            rooms - 2, rng, history);
        // The game's elite encounter sequence is prepared for 15 draws,
        // independently of the weak and normal queues.
        var elite = GenerateSequence(
            Pool(act, PrototypeRoomType.Elite, false), 15, rng,
            new List<string>(15));
        var bosses = Pool(act, PrototypeRoomType.Boss, false);
        var boss = bosses[PrototypeRng.NextInt(rng, "combat", bosses.Length)];
        var discoveryOrder = act switch
        {
            2 => HiveBossDiscoveryOrder,
            3 => GloryBossDiscoveryOrder,
            _ => []
        };
        var seenBosses = profile?.SeenBossEncounters ?? [];
        var firstUndiscoveredBoss = discoveryOrder.FirstOrDefault(id =>
            !seenBosses.Contains(id, StringComparer.Ordinal));
        if (firstUndiscoveredBoss is not null)
            boss = firstUndiscoveredBoss;
        string? secondBoss = null;
        if (act == 3 && ascension >= 10)
        {
            var alternatives = bosses.Where(id => id != boss).ToArray();
            secondBoss = alternatives[PrototypeRng.NextInt(
                rng, "combat", alternatives.Length)];
        }
        return new PrototypeLaterActEncounterBagV111(
            act, 0, weak, normal, elite,
            boss, secondBoss,
            AssignedAncientId: assignAncient
                ? SelectAncient(act, rng, assignDarv) : null);
    }

    /// <summary>
    /// Executable act-local Ancient assignment. Shared Darv partitioning and
    /// native UpFront draw order remain separate implementation work.
    /// Cross-character Orobas offers are deferred in its event profile.
    /// </summary>
    public static string[] AncientPool(int act, bool includeDarv = false) =>
        ConcatAncientPool(act, includeDarv);

    private static string[] ConcatAncientPool(int act, bool includeDarv)
    {
        string[] pool = act switch
        {
            2 => [NativeOrobasEventV111.EventId, NativePaelRewardsV111.PaelId,
                NativeAncientRewardsV111.TezcataraId],
            3 => [NativeNonupeipeEventV111.EventId, NativeTanxEventV111.EventId,
                NativeVakuuEventV111.EventId],
            _ => throw new ArgumentOutOfRangeException(nameof(act))
        };
        return includeDarv
            ? pool.Append(NativeDarvEventV111.EventId).ToArray()
            : pool;
    }

    public static string[] PrepareAncientAssignments(
        RngBundle rng, int ascension, SimulationProfile profile)
    {
        var hasDarv = profile.RevealedEpochs.Contains(
            NativeDarvEventV111.EpochId, StringComparer.Ordinal);
        if (!hasDarv)
            return [SelectAncient(2, rng), SelectAncient(3, rng)];

        // Darv is shared between later acts. Each act takes a random count
        // from the remaining shared pool, so the relic may be assigned to
        // Hive, Glory or neither. This uses the existing prototype stream;
        // the native UpFront schedule is a later RNG pass.
        var hiveReceivesDarv = PrototypeRng.NextInt(rng, "event", 2) == 1;
        // RunManager draws a count for Glory even if Hive removed the only
        // shared Ancient; that call is a bound-one draw in that case.
        var gloryReceivesDarv = PrototypeRng.NextInt(
            rng, "event", hiveReceivesDarv ? 1 : 2) == 1;
        var sharedAncientAct = hiveReceivesDarv ? 2
            : gloryReceivesDarv ? 3 : -1;
        return [SelectAncient(2, rng, sharedAncientAct == 2),
            SelectAncient(3, rng, sharedAncientAct == 3)];
    }

    private static string SelectAncient(int act, RngBundle rng,
        bool includeDarv = false)
    {
        var pool = AncientPool(act, includeDarv);
        return pool[PrototypeRng.NextInt(rng, "event", pool.Length)];
    }

    public static RunState ContinueAct(RunState state)
    {
        if (state.Configuration?.UsesExperimentalSilentThreeActRoute != true
            || state.Configuration.CharacterId != "silent"
            || state.Phase != RunPhase.ActTransition
            || state.World is not { } world
            || world.Act is < 1 or >= 3
            || world.Combat is not null || world.Reward is not null)
            throw new NotSupportedException(
                "Three-act continuation requires a completed configured Silent boss reward.");

        var nextAct = world.Act + 1;
        var prepared = world.PreparedLaterActEncounterBags
            ?? throw new InvalidOperationException(
                "Three-act continuation requires run-start encounter histories; regenerate old experimental snapshots.");
        var nextBag = prepared.SingleOrDefault(bag => bag.Act == nextAct)
            ?? throw new InvalidOperationException(
                "The next act has no prepared encounter history.");
        var generatedMap = PrototypeNativeLaterActMap.GenerateNative(
            state.RunSeed, nextAct, state.Ascension);
        var spoilsMap = PrototypeGameEngine
            .AssignSpoilsMapQuestToGeneratedMap(
                state.Player, generatedMap, nextAct);
        var nextWorld = world with
        {
            Act = nextAct,
            ActIdentity = state.Configuration.Acts[nextAct - 1],
            Floor = 0,
            ActiveRoom = null, Combat = null, Reward = null,
            Shop = null, Event = null, Map = spoilsMap.Map,
            ActOneRegion = null, ActOneEncounterPool = null,
            LaterActEncounterBag = nextBag.Fork(),
            PreparedLaterActEncounterBags = prepared
                .Where(bag => bag.Act != nextAct).Select(bag => bag.Fork()).ToArray(),
            UnknownRoomOdds = null
        };
        var continued = state with
        {
            Player = spoilsMap.Player,
            World = nextWorld,
            Phase = RunPhase.MapChoice
        };

        // An assigned Ancient executes at the act boundary before map choice.
        // Run-start assignment and its RNG policy remain separate.
        if (nextBag.AssignedAncientId is { } ancientId)
        {
            return (nextAct, ancientId) switch
            {
                (2, NativePaelRewardsV111.PaelId) =>
                    NativePaelRewardsV111.BeginPaelOpening(continued),
                (2, NativeAncientRewardsV111.TezcataraId) =>
                    NativeAncientRewardsV111.BeginTezcataraOpening(continued),
                (2, NativeOrobasEventV111.EventId) =>
                    NativeOrobasEventV111.BeginOpening(continued),
                (2 or 3, NativeDarvEventV111.EventId) =>
                    NativeDarvEventV111.BeginOpening(continued),
                (3, NativeTanxEventV111.EventId) =>
                    NativeTanxEventV111.BeginOpening(continued),
                (3, NativeVakuuEventV111.EventId) =>
                    NativeVakuuEventV111.BeginOpening(continued),
                (3, NativeNonupeipeEventV111.EventId) =>
                    NativeNonupeipeEventV111.BeginOpening(continued),
                _ => throw new NotSupportedException(
                    $"Assigned Act {nextAct} Ancient '{ancientId}' is not implemented.")
            };
        }

        return continued;
    }

    /// <summary>
    /// Consume the next pre-generated regional encounter. Entering a room
    /// does not re-roll its identity, matching ActModel.PullNextEncounter.
    /// The first two ordinary combats consume weak entries; the subsequent
    /// ones consume the normal entries. Queues survive exact snapshots.
    /// </summary>
    public static (PrototypeEncounterDefinition Encounter,
        PrototypeLaterActEncounterBagV111 Bag) SelectEncounter(
        RunWorldState world, PrototypeRoomType room, RngBundle rng)
    {
        var bag = world.LaterActEncounterBag
            ?? throw new InvalidOperationException(
                "Later-act encounter selection requires a persisted bag.");
        if (world.Act is < 2 or > 3 || bag.Act != world.Act)
            throw new InvalidOperationException(
                "Later-act encounter bag belongs to a different act.");
        if (room == PrototypeRoomType.Boss)
        {
            var firstBossFloor = PrototypeNativeLaterActMap.RoomRows(world.Act) + 1;
            var encounterId = world.Act == 3
                && bag.SecondBossEncounterId is not null
                && world.Floor == firstBossFloor + 1
                ? bag.SecondBossEncounterId
                : bag.BossEncounterId;
            return (PrototypeContent.Encounter(encounterId), bag);
        }
        if (room is not (PrototypeRoomType.Combat or PrototypeRoomType.Elite))
            throw new ArgumentOutOfRangeException(nameof(room));

        var weak = room == PrototypeRoomType.Combat
            && bag.OrdinaryCombatsStarted < 2;
        var remaining = room == PrototypeRoomType.Elite
            ? bag.RemainingElite
            : weak ? bag.RemainingWeak : bag.RemainingNormal;
        if (remaining.Length == 0)
            throw new InvalidOperationException(
                "The pre-generated regional encounter sequence is exhausted.");
        var selected = remaining[0];
        var tail = remaining[1..];
        var updatedBag = room == PrototypeRoomType.Elite
            ? bag with { RemainingElite = tail }
            : weak
                ? bag with
                {
                    OrdinaryCombatsStarted = bag.OrdinaryCombatsStarted + 1,
                    RemainingWeak = tail
                }
                : bag with
                {
                    OrdinaryCombatsStarted = bag.OrdinaryCombatsStarted + 1,
                    RemainingNormal = tail
                };
        var encounter = PrototypeContent.Encounter(selected);
        if (encounter.RoomType != room
            || encounter.MinAct > world.Act
            || encounter.MaxAct < world.Act
            || encounter.MinFloor > world.Floor
            || encounter.MaxFloor < world.Floor)
            throw new InvalidOperationException(
                "Regional encounter registration is ineligible here.");
        return (encounter, updatedBag);
    }

}

public sealed record PrototypeLaterActEncounterBagV111(
    int Act,
    int OrdinaryCombatsStarted,
    string[] RemainingWeak,
    string[] RemainingNormal,
    string[] RemainingElite,
    string BossEncounterId,
    string? SecondBossEncounterId = null,
    string? AssignedAncientId = null)
{
    public PrototypeLaterActEncounterBagV111 Fork() => this with
    {
        RemainingWeak = (string[])RemainingWeak.Clone(),
        RemainingNormal = (string[])RemainingNormal.Clone(),
        RemainingElite = (string[])RemainingElite.Clone()
    };
}
