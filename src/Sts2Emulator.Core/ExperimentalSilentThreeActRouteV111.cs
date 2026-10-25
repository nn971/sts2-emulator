using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// The configured v111 Silent end-to-end integration route. Catalogues all
/// ordinary regional native encounter registrations and deliberately records
/// that selection distributions and combat RNG still require oracle audit.
/// Legacy prototype and default v111 modes are unaffected.
/// </summary>
public static class ExperimentalSilentThreeActRouteV111
{
    public const string DarvHiveAssignmentSiteId =
        "event.later_act.darv_assignment.hive.index_draw";
    public const string DarvGloryAssignmentSiteId =
        "event.later_act.darv_assignment.glory.index_draw";
    public const string AncientAssignmentSiteId =
        "event.later_act.ancient.index_draw";
    public const string BossIndexSiteId =
        "combat.encounter.later_act.boss.index_draw";
    public const string SecondBossIndexSiteId =
        "combat.encounter.later_act.second_boss.index_draw";

    private static readonly string[] HiveWeakValues =
    [
        "proto.native.hive.encounter.bowlbugs_weak",
        "proto.native.hive.encounter.exoskeletons_weak",
        "proto.native.hive.encounter.thieving_hopper_weak",
        "proto.native.hive.encounter.tunneler_weak"
    ];
    private static readonly string[] HiveNormalValues =
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
    private static readonly string[] HiveElitesValues =
    [
        PrototypeNativeHiveElites.DecimillipedeEncounterId,
        PrototypeNativeHiveElites.EntomancerEncounterId,
        PrototypeNativeHiveElites.PrismEncounterId,
    ];
    private static readonly string[] HiveBossesValues =
    [
        PrototypeNativeHiveBosses.KaiserEncounterId,
        PrototypeNativeHiveBosses.KnowledgeEncounterId,
        PrototypeNativeHiveBosses.InsatiableEncounterId
    ];
    // ActModel.BossDiscoveryOrder is independent of GenerateAllEncounters()
    // pool order. The source replaces the randomly preselected boss with the
    // first entry in this sequence the profile has not discovered yet.
    private static readonly string[] HiveBossDiscoveryOrderValues =
    [
        PrototypeNativeHiveBosses.InsatiableEncounterId,
        PrototypeNativeHiveBosses.KnowledgeEncounterId,
        PrototypeNativeHiveBosses.KaiserEncounterId
    ];
    private static readonly string[] GloryWeakValues =
    [
        PrototypeNativeLaterActs.GlorySculptorWeakId,
        PrototypeNativeGloryScrolls.WeakEncounterId,
        PrototypeNativeGloryWeak.TurretOperatorWeakId
    ];
    private static readonly string[] GloryNormalValues =
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
    private static readonly string[] GloryElitesValues =
    [
        PrototypeNativeGloryBosses.KnightsEncounterId,
        PrototypeNativeGloryElites.MechaKnightEncounterId,
        PrototypeNativeGloryElites.SoulNexusEncounterId
    ];
    private static readonly string[] GloryBossesValues =
    [
        PrototypeNativeGloryBosses.AeonglassEncounterId,
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId
    ];
    private static readonly string[] GloryBossDiscoveryOrderValues =
    [
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId,
        PrototypeNativeGloryBosses.AeonglassEncounterId
    ];

    // Colorful Philosophers is still unimplemented, so it is not
    // misrepresented as an executable event despite its native ID.
    private static readonly string[] HiveEventsValues =
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
        PrototypeNativeFutureOfPotions.EventId
    ];
    private static readonly string[] GloryEventsValues =
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
        PrototypeNativeFutureOfPotions.EventId
    ];

    public static string[] HiveWeak =>
        (string[])HiveWeakValues.Clone();
    public static string[] HiveNormal =>
        (string[])HiveNormalValues.Clone();
    public static string[] HiveElites =>
        (string[])HiveElitesValues.Clone();
    public static string[] HiveBosses =>
        (string[])HiveBossesValues.Clone();
    public static string[] HiveBossDiscoveryOrder =>
        (string[])HiveBossDiscoveryOrderValues.Clone();
    public static string[] GloryWeak =>
        (string[])GloryWeakValues.Clone();
    public static string[] GloryNormal =>
        (string[])GloryNormalValues.Clone();
    public static string[] GloryElites =>
        (string[])GloryElitesValues.Clone();
    public static string[] GloryBosses =>
        (string[])GloryBossesValues.Clone();
    public static string[] GloryBossDiscoveryOrder =>
        (string[])GloryBossDiscoveryOrderValues.Clone();
    public static string[] HiveEvents =>
        (string[])HiveEventsValues.Clone();
    public static string[] GloryEvents =>
        (string[])GloryEventsValues.Clone();

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
    /// The default path uses the PROTOTYPE combat RNG for uniform eligible
    /// choices. Configured opt-in runs can replace these existing index
    /// requests with ReferenceNature while retaining the same queue mechanics.
    /// The exact native NextDouble draws and UpFront stream ownership remain
    /// unverified.
    /// </summary>
    private static string[] GenerateSequence(
        string[] pool, int count, RngBundle rng, List<string> history,
        int act, string queueId)
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
            var selected = eligible[LegacyRandomChoiceV111.ChooseIndexRange(
                rng,
                $"combat.encounter.later_act.{queueId}.index_draw",
                "combat",
                $"act:{act}:queue:{queueId}:draw:{i}:pool:"
                    + CanonicalJson.Sha256(eligible),
                eligible.Length).SelectedIndex];
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
            Pool(act, PrototypeRoomType.Combat, true), 2, rng, history,
            act, "weak");
        var normal = GenerateSequence(
            Pool(act, PrototypeRoomType.Combat, false),
            rooms - 2, rng, history, act, "normal");
        // The game's elite encounter sequence is prepared for 15 draws,
        // independently of the weak and normal queues.
        var elite = GenerateSequence(
            Pool(act, PrototypeRoomType.Elite, false), 15, rng,
            new List<string>(15), act, "elite");
        var bosses = Pool(act, PrototypeRoomType.Boss, false);
        var boss = bosses[LegacyRandomChoiceV111.ChooseIndexRange(
            rng,
            BossIndexSiteId,
            "combat",
            $"act:{act}:boss_pool:{CanonicalJson.Sha256(bosses)}",
            bosses.Length).SelectedIndex];
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
            secondBoss = alternatives[LegacyRandomChoiceV111.ChooseIndexRange(
                rng,
                SecondBossIndexSiteId,
                "combat",
                $"act:{act}:first_boss:{boss}:alternatives:"
                    + CanonicalJson.Sha256(alternatives),
                alternatives.Length).SelectedIndex];
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
        // Hive, Glory or neither. The default path uses the prototype event
        // stream; configured opt-in runs can replace these existing index
        // requests with ReferenceNature. Native UpFront ordering remains
        // separate work.
        var hiveReceivesDarv = LegacyRandomChoiceV111.ChooseBoundedInteger(
            rng,
            DarvHiveAssignmentSiteId,
            "event",
            "run:darv_assignment:hive",
            2).SelectedIndex == 1;
        // RunManager draws a count for Glory even if Hive removed the only
        // shared Ancient; that call is a bound-one draw in that case.
        var gloryReceivesDarv = LegacyRandomChoiceV111.ChooseBoundedInteger(
            rng,
            DarvGloryAssignmentSiteId,
            "event",
            "run:darv_assignment:glory",
            hiveReceivesDarv ? 1 : 2).SelectedIndex == 1;
        var sharedAncientAct = hiveReceivesDarv ? 2
            : gloryReceivesDarv ? 3 : -1;
        return [SelectAncient(2, rng, sharedAncientAct == 2),
            SelectAncient(3, rng, sharedAncientAct == 3)];
    }

    public static ReferenceNatureLaterActAssignmentsV111
        PrepareAncientAssignmentsWithReferenceNature(
            RngBundle rng, int ascension, SimulationProfile profile,
            ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        initialNatureState.Validate();
        if (!rng.TryGetStream("event", out var eventStream))
            throw new InvalidDataException(
                "ReferenceNature later-act assignment requires the legacy event stream.");
        var eventCallCount = eventStream.CallCount ?? 0;
        if (eventStream.Codec != PrototypeRng.Codec ||
            eventCallCount > int.MaxValue)
            throw new InvalidDataException(
                "ReferenceNature later-act assignment requires the legacy event stream.");
        var initialEventStream = eventStream.Fork();

        var provider = new ReferenceNatureLaterActAssignmentIndexProviderV111(
            initialNatureState);
        string[] assignments;
        using (StochasticProviderScopeV111.Enter(
                   indexProvider: provider,
                   overrideExplicitProviders: true))
        {
            assignments = PrepareAncientAssignments(
                rng, ascension, profile);
        }
        var finalEventStream = rng.GetStream("event");
        if (provider.InitialEventOrdinal != (int)eventCallCount ||
            CanonicalJson.Sha256(initialEventStream) !=
            CanonicalJson.Sha256(finalEventStream))
            throw new InvalidDataException(
                "ReferenceNature later-act assignment advanced the legacy event cursor.");

        var result = new ReferenceNatureLaterActAssignmentsV111(
            assignments, initialNatureState.Fork(), provider.NatureState,
            provider.InitialEventOrdinal, provider.ChanceLaws,
            provider.DecisionTranscript);
        result.Validate(profile);
        return result;
    }

    private static string SelectAncient(int act, RngBundle rng,
        bool includeDarv = false)
    {
        var pool = AncientPool(act, includeDarv);
        return pool[LegacyRandomChoiceV111.ChooseIndexRange(
            rng,
            AncientAssignmentSiteId,
            "event",
            $"act:{act}:include_darv:{includeDarv}:pool:"
                + CanonicalJson.Sha256(pool),
            pool.Length).SelectedIndex];
    }

    public static RunState ContinueAct(RunState state) =>
        ContinueActCore(state, suppliedMap: null,
            deferAssignedAncientOpening: false);

    internal static RunState ContinueActWithMap(
        RunState state, MapState map, bool deferAssignedAncientOpening)
    {
        ArgumentNullException.ThrowIfNull(map);
        return ContinueActCore(state, map, deferAssignedAncientOpening);
    }

    private static RunState ContinueActCore(
        RunState state, MapState? suppliedMap,
        bool deferAssignedAncientOpening)
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
        var prepared = world.Stochastic.PreparedLaterActEncounterBags
            ?? throw new InvalidOperationException(
                "Three-act continuation requires run-start encounter histories; regenerate old experimental snapshots.");
        var nextBag = prepared.SingleOrDefault(bag => bag.Act == nextAct)
            ?? throw new InvalidOperationException(
                "The next act has no prepared encounter history.");
        var map = suppliedMap ?? PrototypeNativeLaterActMap.GenerateNative(
            state.RunSeed, nextAct, state.Ascension);
        if (!StringComparer.Ordinal.Equals(map.GenerationProfileId,
                PrototypeNativeLaterActMap.ProfileId(nextAct)))
            throw new InvalidDataException(
                "Three-act continuation received a map from a different act generator.");
        var remainingPrepared = prepared
            .Where(bag => bag.Act != nextAct)
            .Select(bag => bag.Fork()).ToArray();
        var nextWorld = world with
        {
            Act = nextAct,
            ActIdentity = state.Configuration.Acts[nextAct - 1],
            Floor = 0,
            ActiveRoom = null, Combat = null, Reward = null,
            Shop = null, Event = null, Map = map.Fork(),
            ActOneRegion = null,
            UnknownRoomOdds = null
        };
        nextWorld = nextWorld with
        {
            Stochastic = RunWorldStochasticStateV111.CommitActTransition(
                state, nextWorld, nextBag, remainingPrepared)
        };
        var continued = state with
        {
            World = nextWorld,
            Phase = RunPhase.MapChoice
        };

        if (deferAssignedAncientOpening)
            return continued;

        // An assigned Ancient executes at the act boundary before map choice.
        // Run-start assignment and its RNG policy remain separate.
        if (nextBag.AssignedAncientId is not null)
            return BeginAssignedAncientOpeningWithAudit(
                continued, new NativeRandomIndexProviderV111()).State;

        return continued;
    }

    internal static (RunState State, RandomIndexDecisionV111[] Decisions)
        BeginAssignedAncientOpeningWithAudit(
            RunState state, IRandomIndexProviderV111 provider)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(provider);
        var world = state.World ?? throw new InvalidDataException(
            "Assigned Ancient opening has no run world.");
        var ancientId = world.Stochastic.LaterActEncounterBag?
            .AssignedAncientId ?? throw new InvalidDataException(
                "Assigned Ancient opening has no assigned Ancient.");
        switch ((world.Act, ancientId))
        {
            case (2, NativePaelRewardsV111.PaelId):
            {
                var audit = NativePaelRewardsV111
                    .BeginPaelOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            case (2, NativeAncientRewardsV111.TezcataraId):
            {
                var audit = NativeAncientRewardsV111
                    .BeginTezcataraOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            case (2, NativeOrobasEventV111.EventId):
            {
                var audit = NativeOrobasEventV111
                    .BeginOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            case (2 or 3, NativeDarvEventV111.EventId):
            {
                var audit = NativeDarvEventV111
                    .BeginOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            case (3, NativeTanxEventV111.EventId):
            {
                var audit = NativeTanxEventV111
                    .BeginOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            case (3, NativeVakuuEventV111.EventId):
            {
                var audit = NativeVakuuEventV111
                    .BeginOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            case (3, NativeNonupeipeEventV111.EventId):
            {
                var audit = NativeNonupeipeEventV111
                    .BeginOpeningWithAudit(state, provider);
                return (audit.State, audit.Choices);
            }
            default:
                throw new NotSupportedException(
                    $"Assigned Act {world.Act} Ancient '{ancientId}' is not implemented.");
        }
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
        var bag = world.Stochastic.LaterActEncounterBag
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

internal sealed class ReferenceNatureLaterActAssignmentIndexProviderV111
    : IRandomIndexProviderV111
{
    private readonly ReferenceNatureDecisionV111[]? _expectedPrefix;
    private readonly bool _suspendAfterPrefix;
    private ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        _stochastic;
    private readonly List<ChanceLawV111> _chanceLaws = [];
    private readonly List<ControlledStochasticDecisionV111>
        _providerDecisions = [];
    private bool? _hiveReceivesDarv;

    public ReferenceNatureLaterActAssignmentIndexProviderV111(
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[]? expectedPrefix = null,
        bool suspendAfterPrefix = false)
    {
        var initial = initialNatureState?.Fork()
            ?? throw new ArgumentNullException(nameof(initialNatureState));
        _stochastic = new(initial, initial.Fork(), []);
        _expectedPrefix = expectedPrefix is null ? null :
            (ReferenceNatureDecisionV111[])expectedPrefix.Clone();
        _suspendAfterPrefix = suspendAfterPrefix;
        if (suspendAfterPrefix && expectedPrefix is null)
            throw new ArgumentException(
                "A resumable assignment provider requires its selected prefix.",
                nameof(expectedPrefix));
    }

    public string Mode => ReferenceNatureV111.ProviderId;
    public ReferenceNatureStateV111 NatureState =>
        _stochastic.NatureState.Fork();
    public int InitialEventOrdinal { get; private set; } = -1;
    public ChanceLawV111[] ChanceLaws => _chanceLaws
        .Select(law => law.Fork()).ToArray();
    public ReferenceNatureDecisionV111[] DecisionTranscript =>
        _stochastic.DecisionTranscript;
    public ControlledStochasticDecisionV111[] ProviderDecisions =>
        _providerDecisions.Select(decision => decision.Fork()).ToArray();
    public RandomIndexRequestV111? PendingRequest { get; private set; }
    public ChanceLawV111? PendingLaw { get; private set; }

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.ValidateForLegacyProvider();
        if (request.StreamId != "event" ||
            !streams.TryGetStream("event", out var eventStream) ||
            eventStream.Codec != PrototypeRng.Codec)
            throw new InvalidDataException(
                "ReferenceNature later-act assignment request differs from its legacy event boundary.");
        var decisions = _stochastic.DecisionTranscript;
        var decisionCount = decisions.Length;
        if (decisionCount == 0)
            InitialEventOrdinal = request.Ordinal;

        ChanceLawV111 law;
        if (request.SiteId == ExperimentalSilentThreeActRouteV111
                .DarvHiveAssignmentSiteId)
        {
            if (request.OwnerId != "run:darv_assignment:hive" ||
                request.OperationKind != "bounded_integer" ||
                request.SupportCount != 2 || decisionCount != 0)
                throw new InvalidDataException(
                    "ReferenceNature Hive Darv request differs from its source boundary.");
            law = ChanceLawEngineV111.QueryLaterActDarvHiveAssignment(
                request.Ordinal);
        }
        else if (request.SiteId == ExperimentalSilentThreeActRouteV111
                     .DarvGloryAssignmentSiteId)
        {
            if (_hiveReceivesDarv is not { } hiveReceivesDarv ||
                request.OwnerId != "run:darv_assignment:glory" ||
                request.OperationKind != "bounded_integer" ||
                request.SupportCount != (hiveReceivesDarv ? 1 : 2))
                throw new InvalidDataException(
                    "ReferenceNature Glory Darv request differs from its conditional source boundary.");
            law = ChanceLawEngineV111.QueryLaterActDarvGloryAssignment(
                hiveReceivesDarv, request.Ordinal);
        }
        else if (request.SiteId == ExperimentalSilentThreeActRouteV111
                     .AncientAssignmentSiteId)
        {
            var (act, includeDarv) = ParseAncientOwner(request.OwnerId);
            var pool = ExperimentalSilentThreeActRouteV111
                .AncientPool(act, includeDarv);
            if (request.OperationKind != "choose_index" ||
                request.SupportCount != pool.Length ||
                !request.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, pool.Length)))
                throw new InvalidDataException(
                    "ReferenceNature Ancient request differs from its ordered source pool.");
            law = ChanceLawEngineV111.QueryLaterActAncientAssignment(
                act, includeDarv, request.Ordinal);
        }
        else
        {
            throw new NotSupportedException(
                $"ReferenceNature later-act assignment does not support '{request.SiteId}'.");
        }

        if (law.SiteId != request.SiteId ||
            law.OwnerId != request.OwnerId ||
            law.OperationKind != request.OperationKind ||
            law.Ordinal != request.Ordinal ||
            law.Outcomes.Length != request.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature later-act Chance Law differs from its source request.");

        if (_suspendAfterPrefix &&
            decisionCount == _expectedPrefix!.Length)
        {
            PendingRequest = request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            };
            PendingLaw = law.Fork();
            throw new ReferenceNatureLaterActPendingChoiceException();
        }

        var selection = ReferenceNatureV111.SampleValidated(
            law, _stochastic.NatureState);
        var natureDecision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        if (_expectedPrefix is not null &&
            (decisionCount >= _expectedPrefix.Length ||
             CanonicalJson.Sha256(natureDecision) != CanonicalJson.Sha256(
                 _expectedPrefix[decisionCount])))
            throw new InvalidDataException(
                "ReferenceNature later-act assignment replay differs from its saved prefix.");
        _stochastic = _stochastic.Advance(selection.NextState,
            natureDecision);
        _chanceLaws.Add(law.Fork());
        if (request.SiteId == ExperimentalSilentThreeActRouteV111
                .DarvHiveAssignmentSiteId)
            _hiveReceivesDarv = selection.OutcomeId ==
                "darv_assignment:hive";

        var randomDecision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selection.OutcomeIndex, Mode);
        randomDecision.Validate();
        _providerDecisions.Add(
            ControlledStochasticDecisionV111.FromIndex(randomDecision));
        return randomDecision;
    }

    private static (int Act, bool IncludeDarv) ParseAncientOwner(
        string ownerId)
    {
        foreach (var act in new[] { 2, 3 })
        foreach (var includeDarv in new[] { false, true })
        {
            var pool = ExperimentalSilentThreeActRouteV111
                .AncientPool(act, includeDarv);
            var expected = $"act:{act}:include_darv:{includeDarv}:pool:"
                + CanonicalJson.Sha256(pool);
            if (ownerId == expected)
                return (act, includeDarv);
        }
        throw new InvalidDataException(
            "ReferenceNature Ancient request has an unknown source pool.");
    }
}

public sealed record ReferenceNatureLaterActAssignmentsV111(
    string[] Assignments,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    int InitialEventOrdinal,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private string[]? _assignments = Copy(Assignments);
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();

    [JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, NatureState,
            DecisionTranscript);

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public string[] Assignments
    {
        get => _assignments is null ? null! : (string[])_assignments.Clone();
        init => _assignments = Copy(value);
    }

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public ReferenceNatureLaterActAssignmentsV111 Fork() => this with
    {
        Assignments = _assignments!,
        Stochastic = Stochastic.Fork(),
        ChanceLaws = _chanceLaws!,
    };

    public void Validate(SimulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(Assignments);
        ArgumentNullException.ThrowIfNull(ChanceLaws);
        Stochastic.Validate();
        var stochastic = Stochastic;
        var transcript = stochastic.DecisionTranscript;
        var hasDarv = profile.RevealedEpochs.Contains(
            NativeDarvEventV111.EpochId, StringComparer.Ordinal);
        var expectedCount = hasDarv ? 4 : 2;
        if (Assignments.Length != 2 || InitialEventOrdinal < 0 ||
            ChanceLaws.Length != expectedCount ||
            transcript.Length != expectedCount)
            throw new InvalidDataException(
                "Invalid ReferenceNature later-act assignment result.");

        var expectedState = stochastic.InitialNatureState;
        var lawIndex = 0;
        var ordinal = InitialEventOrdinal;
        string? hiveAssignment = null;
        string? gloryAssignment = null;
        if (hasDarv)
        {
            Check(ChanceLawEngineV111.QueryLaterActDarvHiveAssignment(
                ordinal++), "darv_assignment:hive", "darv_assignment:none");
            var hiveReceivesDarv = hiveAssignment == "darv_assignment:hive";
            Check(ChanceLawEngineV111.QueryLaterActDarvGloryAssignment(
                hiveReceivesDarv, ordinal++), "darv_assignment:glory",
                "darv_assignment:none");
            var gloryReceivesDarv = gloryAssignment ==
                "darv_assignment:glory";
            CheckAncient(2, hiveReceivesDarv, ordinal++, 0);
            CheckAncient(3, gloryReceivesDarv, ordinal++, 1);
        }
        else
        {
            CheckAncient(2, false, ordinal++, 0);
            CheckAncient(3, false, ordinal, 1);
        }
        if (!expectedState.SameCursor(stochastic.NatureState))
            throw new InvalidDataException(
                "ReferenceNature later-act assignment cursor does not replay.");

        void Check(ChanceLawV111 expectedLaw, params string[] allowedOutcomes)
        {
            var law = ChanceLaws[lawIndex];
            var decision = transcript[lawIndex];
            law.Validate();
            decision.Validate();
            if (CanonicalJson.Sha256(expectedLaw) !=
                CanonicalJson.Sha256(law))
                throw new InvalidDataException(
                    "ReferenceNature later-act Chance Law differs from its source query.");
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(decision) ||
                !allowedOutcomes.Contains(selection.OutcomeId,
                    StringComparer.Ordinal))
                throw new InvalidDataException(
                    "ReferenceNature later-act assignment transcript does not replay.");
            expectedState = selection.NextState;
            if (law.SiteId == ExperimentalSilentThreeActRouteV111
                    .DarvHiveAssignmentSiteId)
                hiveAssignment = selection.OutcomeId;
            else if (law.SiteId == ExperimentalSilentThreeActRouteV111
                         .DarvGloryAssignmentSiteId)
                gloryAssignment = selection.OutcomeId;
            lawIndex++;
        }

        void CheckAncient(int act, bool includeDarv, int drawOrdinal,
            int assignmentIndex)
        {
            var pool = ExperimentalSilentThreeActRouteV111
                .AncientPool(act, includeDarv);
            var law = ChanceLawEngineV111.QueryLaterActAncientAssignment(
                act, includeDarv, drawOrdinal);
            Check(law, pool.Select(id => "ancient_event:" + id).ToArray());
            var selected = transcript[lawIndex - 1].OutcomeId;
            var selectedId = selected["ancient_event:".Length..];
            if (Assignments[assignmentIndex] != selectedId)
                throw new InvalidDataException(
                    "ReferenceNature Ancient result differs from its assignment.");
        }
    }

    private static string[]? Copy(string[]? values) => values is null
        ? null : (string[])values.Clone();
}

internal sealed class ReferenceNatureLaterActEncounterIndexProviderV111
    : IRandomIndexProviderV111
{
    private readonly int _ascension;
    private readonly ReferenceNatureDecisionV111[]? _expectedPrefix;
    private readonly bool _suspendAfterPrefix;
    private ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        _stochastic;
    private readonly List<ChanceLawV111> _chanceLaws = [];
    private readonly List<ControlledStochasticDecisionV111>
        _providerDecisions = [];
    private int _queueDrawCount;
    private int _bossChoiceCount;

    public ReferenceNatureLaterActEncounterIndexProviderV111(
        RngBundle streams, ReferenceNatureStateV111 initialNatureState,
        int ascension, ReferenceNatureDecisionV111[]? expectedPrefix = null,
        bool suspendAfterPrefix = false)
    {
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        if (ascension < 0 || ascension > 10)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        initialNatureState.Validate();
        if (!streams.TryGetStream("combat", out var combatStream) ||
            combatStream.Codec != PrototypeRng.Codec ||
            (combatStream.CallCount ?? 0) > int.MaxValue - 60)
            throw new InvalidDataException(
                "ReferenceNature later-act bosses require a valid legacy combat cursor.");
        var initialOrdinal = combatStream.CallCount ?? 0;
        var initial = initialNatureState.Fork();
        _stochastic = new(initial, initial.Fork(), []);
        _ascension = ascension;
        _expectedPrefix = expectedPrefix is null ? null :
            (ReferenceNatureDecisionV111[])expectedPrefix.Clone();
        _suspendAfterPrefix = suspendAfterPrefix;
        if (suspendAfterPrefix && expectedPrefix is null)
            throw new ArgumentException(
                "A resumable encounter provider requires its selected prefix.",
                nameof(expectedPrefix));
        InitialCombatOrdinal = (int)initialOrdinal;
    }

    public string Mode => ReferenceNatureV111.ProviderId;
    public int InitialCombatOrdinal { get; }
    public int QueueDrawCount => _queueDrawCount;
    public ReferenceNatureStateV111 NatureState =>
        _stochastic.NatureState.Fork();
    public ChanceLawV111[] ChanceLaws => _chanceLaws
        .Select(law => law.Fork()).ToArray();
    public ReferenceNatureDecisionV111[] DecisionTranscript =>
        _stochastic.DecisionTranscript;
    public ControlledStochasticDecisionV111[] ProviderDecisions =>
        _providerDecisions.Select(decision => decision.Fork()).ToArray();
    public RandomIndexRequestV111? PendingRequest { get; private set; }
    public ChanceLawV111? PendingLaw { get; private set; }

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        request.ValidateForLegacyProvider();
        if (request.StreamId != "combat" ||
            !streams.TryGetStream("combat", out var combatStream) ||
            combatStream.Codec != PrototypeRng.Codec)
            throw new InvalidDataException(
                "ReferenceNature later-act encounter request differs from its legacy combat boundary.");
        var decisions = _stochastic.DecisionTranscript;
        var decisionCount = decisions.Length;
        if (request.Ordinal != checked(InitialCombatOrdinal +
                _chanceLaws.Count))
            throw new InvalidDataException(
                "ReferenceNature later-act encounter request has a nonsequential combat ordinal.");

        ChanceLawV111 law;
        if (IsQueueSite(request.SiteId))
        {
            ValidateQueueRequest(request);
            law = ChanceLawEngineV111.QueryLegacyCallerBoundIndex(
                request.SiteId, request.OwnerId, request.Ordinal,
                request.SupportCount);
        }
        else if (request.SiteId == ExperimentalSilentThreeActRouteV111
                .BossIndexSiteId)
        {
            var act = ResolveBossAct(request);
            var expectedDecisionCount = act - 2;
            if (_bossChoiceCount != expectedDecisionCount ||
                request.Ordinal != ExpectedBossOrdinal(act))
                throw new InvalidDataException(
                    "ReferenceNature later-act boss order or combat ordinal differs from run-start preparation.");
            law = ChanceLawEngineV111.QueryLaterActBossPool(
                act, request.Ordinal);
            ValidateOrderedSupport(request, law.Outcomes.Length);
        }
        else if (request.SiteId == ExperimentalSilentThreeActRouteV111
                     .SecondBossIndexSiteId)
        {
            if (_ascension < 10 || _bossChoiceCount != 2 ||
                request.Ordinal != checked(ExpectedBossOrdinal(3) + 1))
                throw new InvalidDataException(
                    "ReferenceNature Glory second-boss request differs from its Ascension 10 run-start boundary.");
            var firstBoss = ResolveFirstBoss(request.OwnerId);
            law = ChanceLawEngineV111.QueryLaterActSecondBoss(
                firstBoss, _ascension, request.Ordinal);
            ValidateOrderedSupport(request, law.Outcomes.Length);
        }
        else
        {
            throw new NotSupportedException(
                $"ReferenceNature later-act bag preparation does not support '{request.SiteId}'.");
        }

        if (law.SiteId != request.SiteId ||
            law.OwnerId != request.OwnerId ||
            law.OperationKind != request.OperationKind ||
            law.Ordinal != request.Ordinal ||
            law.Outcomes.Length != request.SupportCount)
            throw new InvalidDataException(
                "ReferenceNature later-act boss Chance Law differs from its source request.");

        if (_suspendAfterPrefix &&
            decisionCount == _expectedPrefix!.Length)
        {
            PendingRequest = request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            };
            PendingLaw = law.Fork();
            throw new ReferenceNatureLaterActPendingChoiceException();
        }

        var selection = ReferenceNatureV111.SampleValidated(
            law, _stochastic.NatureState);
        var natureDecision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        if (_expectedPrefix is not null &&
            (decisionCount >= _expectedPrefix.Length ||
             CanonicalJson.Sha256(natureDecision) != CanonicalJson.Sha256(
                 _expectedPrefix[decisionCount])))
            throw new InvalidDataException(
                "ReferenceNature later-act encounter replay differs from its saved prefix.");
        _stochastic = _stochastic.Advance(selection.NextState,
            natureDecision);
        _chanceLaws.Add(law.Fork());
        if (request.SiteId == ExperimentalSilentThreeActRouteV111
                .BossIndexSiteId ||
            request.SiteId == ExperimentalSilentThreeActRouteV111
                .SecondBossIndexSiteId)
            _bossChoiceCount++;
        else
            _queueDrawCount++;
        var randomDecision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            }, selection.OutcomeIndex, Mode);
        randomDecision.Validate();
        _providerDecisions.Add(
            ControlledStochasticDecisionV111.FromIndex(randomDecision));
        return randomDecision;
    }

    private void ValidateQueueRequest(RandomIndexRequestV111 request)
    {
        if (_queueDrawCount >=
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15 +
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(3) + 15)
            throw new InvalidDataException(
                "ReferenceNature later-act preparation made too many queue requests.");
        var act = _queueDrawCount <
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15
            ? 2 : 3;
        var actOffset = act == 2 ? 0
            : ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15;
        var draw = _queueDrawCount - actOffset;
        var roomCount = ExperimentalSilentThreeActRouteV111
            .OrdinaryRoomCount(act);
        var queueId = draw < 2 ? "weak"
            : draw < roomCount ? "normal" : "elite";
        var queueDraw = queueId switch
        {
            "weak" => draw,
            "normal" => draw - 2,
            _ => draw - roomCount
        };
        var expectedSite = $"combat.encounter.later_act.{queueId}.index_draw";
        var expectedOwnerPrefix =
            $"act:{act}:queue:{queueId}:draw:{queueDraw}:pool:";
        if (request.SiteId != expectedSite ||
            request.OperationKind != "choose_index" ||
            request.Ordinal != checked(InitialCombatOrdinal +
                _chanceLaws.Count) ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, request.SupportCount)) ||
            !request.OwnerId.StartsWith(expectedOwnerPrefix,
                StringComparison.Ordinal) ||
            request.OwnerId.Length != expectedOwnerPrefix.Length + 64 ||
            !request.OwnerId[expectedOwnerPrefix.Length..].All(
                Uri.IsHexDigit))
            throw new InvalidDataException(
                "ReferenceNature later-act queue request differs from its ordered preparation boundary.");
    }

    private int ResolveBossAct(RandomIndexRequestV111 request)
    {
        foreach (var act in new[] { 2, 3 })
        {
            var pool = ExperimentalSilentThreeActRouteV111.Pool(
                act, PrototypeRoomType.Boss, weak: false);
            var owner = $"act:{act}:boss_pool:"
                + CanonicalJson.Sha256(pool);
            if (request.OwnerId == owner)
                return act;
        }
        throw new InvalidDataException(
            "ReferenceNature later-act boss request has an unknown ordered pool.");
    }

    private string ResolveFirstBoss(string ownerId)
    {
        const string prefix = "act:3:first_boss:";
        const string suffix = ":alternatives:";
        if (!ownerId.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                "ReferenceNature second-boss request has an invalid owner.");
        var suffixIndex = ownerId.IndexOf(suffix, prefix.Length,
            StringComparison.Ordinal);
        if (suffixIndex < 0 || ownerId.Length != suffixIndex +
            suffix.Length + 64 || !ownerId[(suffixIndex + suffix.Length)..]
                .All(Uri.IsHexDigit))
            throw new InvalidDataException(
                "ReferenceNature second-boss request has an invalid alternatives fingerprint.");
        var firstBoss = ownerId[prefix.Length..suffixIndex];
        var expected = ChanceLawEngineV111.QueryLaterActSecondBoss(
            firstBoss, _ascension, 0);
        if (expected.OwnerId != ownerId)
            throw new InvalidDataException(
                "ReferenceNature second-boss request alternatives differ from its source pool.");
        return firstBoss;
    }

    private int ExpectedBossOrdinal(int act)
    {
        var actTwoQueueDraws =
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15;
        return checked(InitialCombatOrdinal + actTwoQueueDraws +
            (act == 2 ? 0 : 1 +
                ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(3) +
                15));
    }

    private static bool IsQueueSite(string siteId) => siteId is
        "combat.encounter.later_act.weak.index_draw" or
        "combat.encounter.later_act.normal.index_draw" or
        "combat.encounter.later_act.elite.index_draw";

    private static void ValidateOrderedSupport(
        RandomIndexRequestV111 request, int expectedCount)
    {
        if (request.OperationKind != "choose_index" ||
            request.SupportCount != expectedCount ||
            !request.OrderedCandidates.SequenceEqual(
                Enumerable.Range(0, expectedCount)))
            throw new InvalidDataException(
                "ReferenceNature later-act boss support differs from the source-ordered pool.");
    }
}

public sealed record ReferenceNatureLaterActEncounterPreparationV111(
    PrototypeLaterActEncounterBagV111[] Bags,
    RngBundle InitialPrototypeRng,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    int InitialCombatOrdinal,
    int Ascension,
    ChanceLawV111[] ChanceLaws,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private PrototypeLaterActEncounterBagV111[]? _bags = Bags?.Select(
        bag => bag.Fork()).ToArray();
    private RngBundle? _initialPrototypeRng = InitialPrototypeRng?.Fork();
    private ChanceLawV111[]? _chanceLaws = ChanceLaws?.Select(law =>
        law.Fork()).ToArray();

    [JsonIgnore]
    public ReferenceNatureStochasticStateV111<ReferenceNatureDecisionV111>
        Stochastic { get; init; } = new(InitialNatureState, NatureState,
            DecisionTranscript);

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public PrototypeLaterActEncounterBagV111[] Bags
    {
        get => _bags?.Select(bag => bag.Fork()).ToArray()!;
        init => _bags = value?.Select(bag => bag.Fork()).ToArray();
    }

    public RngBundle InitialPrototypeRng
    {
        get => _initialPrototypeRng?.Fork()!;
        init => _initialPrototypeRng = value?.Fork();
    }

    public ChanceLawV111[] ChanceLaws
    {
        get => _chanceLaws?.Select(law => law.Fork()).ToArray()!;
        init => _chanceLaws = value?.Select(law => law.Fork()).ToArray();
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public void Validate(SimulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(Bags);
        ArgumentNullException.ThrowIfNull(InitialPrototypeRng);
        ArgumentNullException.ThrowIfNull(ChanceLaws);
        Stochastic.Validate();
        var stochastic = Stochastic;
        var transcript = stochastic.DecisionTranscript;
        var queueDrawCount =
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15 +
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(3) + 15;
        var expectedDecisionCount = queueDrawCount +
            (Ascension >= 10 ? 3 : 2);
        if (Bags.Length != 2 || InitialCombatOrdinal < 0 ||
            Ascension is < 0 or > 10 ||
            ChanceLaws.Length != expectedDecisionCount ||
            transcript.Length != expectedDecisionCount)
            throw new InvalidDataException(
                "Invalid ReferenceNature later-act encounter preparation result.");
        if (!InitialPrototypeRng.TryGetStream(
                "combat", out var initialCombat) ||
            initialCombat.Codec != PrototypeRng.Codec ||
            initialCombat.CallCount != (ulong)InitialCombatOrdinal)
            throw new InvalidDataException(
                "ReferenceNature encounter replay has an invalid prototype combat cursor.");
        var hiveBag = Bags.SingleOrDefault(bag => bag.Act == 2)
            ?? throw new InvalidDataException(
                "ReferenceNature encounter result is missing the Hive bag.");
        var gloryBag = Bags.SingleOrDefault(bag => bag.Act == 3)
            ?? throw new InvalidDataException(
                "ReferenceNature encounter result is missing the Glory bag.");

        var expectedState = stochastic.InitialNatureState;
        var script = new RandomIndexDecisionV111[expectedDecisionCount];
        for (var index = 0; index < ChanceLaws.Length; index++)
        {
            var law = ChanceLaws[index];
            var decision = transcript[index];
            law.Validate();
            decision.Validate();
            var request = RequestFromLaw(law);
            var expectedLaw = QueryExpectedLaw(request, Ascension);
            if (CanonicalJson.Sha256(expectedLaw) !=
                CanonicalJson.Sha256(law))
                throw new InvalidDataException(
                    "ReferenceNature later-act Chance Law differs from its source query.");
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedState);
            var expectedDecision = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                CanonicalJson.Sha256(decision))
                throw new InvalidDataException(
                    "ReferenceNature later-act transcript does not replay.");
            script[index] = new RandomIndexDecisionV111(
                request, selection.OutcomeIndex, "scripted-v111");
            expectedState = selection.NextState;
        }
        if (!expectedState.SameCursor(stochastic.NatureState) ||
            stochastic.NatureState.SampleCount != checked(
                stochastic.InitialNatureState.SampleCount +
                expectedDecisionCount))
            throw new InvalidDataException(
                "ReferenceNature later-act encounter cursor does not replay.");

        var replayRng = InitialPrototypeRng.Fork();
        var scriptedProvider = new ScriptedRandomIndexProviderV111(script);
        PrototypeLaterActEncounterBagV111[] replayedBags;
        using (StochasticProviderScopeV111.Enter(
                   indexProvider: scriptedProvider,
                   overrideExplicitProviders: true))
        {
            replayedBags = new[] { 2, 3 }.Select(act =>
            {
                var savedBag = Bags.Single(bag => bag.Act == act);
                return ExperimentalSilentThreeActRouteV111.InitialBag(
                    act, replayRng, Ascension, profile,
                    assignAncient: false) with
                {
                    AssignedAncientId = savedBag.AssignedAncientId
                };
            }).ToArray();
        }
        scriptedProvider.AssertComplete();
        if (CanonicalJson.Sha256(replayedBags) !=
            CanonicalJson.Sha256(Bags))
            throw new InvalidDataException(
                "ReferenceNature transcript does not reproduce the prepared encounter bags.");

        var actTwoOrdinal = checked(InitialCombatOrdinal +
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(2) + 15);
        var actThreeOrdinal = checked(actTwoOrdinal + 1 +
            ExperimentalSilentThreeActRouteV111.OrdinaryRoomCount(3) + 15);
        if (ChanceLaws[queueDrawCount].Ordinal != actTwoOrdinal ||
            ChanceLaws[queueDrawCount + 1].Ordinal != actThreeOrdinal ||
            Ascension >= 10 && ChanceLaws[^1].Ordinal !=
                checked(actThreeOrdinal + 1))
            throw new InvalidDataException(
                "ReferenceNature later-act request order differs from the prepared bag schedule.");
        var rawHiveBoss = EncounterOutcomeAt(queueDrawCount);
        var rawGloryBoss = EncounterOutcomeAt(queueDrawCount + 1);
        if (hiveBag.BossEncounterId !=
                (FirstUndiscoveredBoss(2, profile) ?? rawHiveBoss) ||
            gloryBag.BossEncounterId !=
                (FirstUndiscoveredBoss(3, profile) ?? rawGloryBoss))
            throw new InvalidDataException(
                "ReferenceNature boss outcomes differ from the prepared bags.");
        if (Ascension >= 10)
        {
            var secondBoss = EncounterOutcomeAt(queueDrawCount + 2);
            if (gloryBag.SecondBossEncounterId != secondBoss)
                throw new InvalidDataException(
                    "ReferenceNature second-boss outcome differs from the prepared Glory bag.");
        }
        else if (gloryBag.SecondBossEncounterId is not null)
        {
            throw new InvalidDataException(
                "Glory has a second boss below Ascension 10.");
        }
    }

    private string EncounterOutcomeAt(int index)
    {
        var law = ChanceLaws[index];
        var outcome = law.Outcomes[
            Stochastic.DecisionTranscript[index].OutcomeIndex]
            .OutcomeId;
        return outcome.StartsWith("encounter:", StringComparison.Ordinal)
            ? outcome["encounter:".Length..]
            : throw new InvalidDataException(
                "ReferenceNature boss outcome is not an encounter identity.");
    }

    private static RandomIndexRequestV111 RequestFromLaw(
        ChanceLawV111 law) => new(
            law.SiteId, "combat", law.OwnerId, law.Ordinal,
            Enumerable.Range(0, law.Outcomes.Length).ToArray(),
            OperationKind: law.OperationKind);

    private static ChanceLawV111 QueryExpectedLaw(
        RandomIndexRequestV111 request, int ascension)
    {
        if (request.SiteId is
            "combat.encounter.later_act.weak.index_draw" or
            "combat.encounter.later_act.normal.index_draw" or
            "combat.encounter.later_act.elite.index_draw")
            return ChanceLawEngineV111.QueryLegacyCallerBoundIndex(
                request.SiteId, request.OwnerId, request.Ordinal,
                request.SupportCount);
        if (request.SiteId == ExperimentalSilentThreeActRouteV111
                .BossIndexSiteId)
        {
            foreach (var act in new[] { 2, 3 })
            {
                var pool = ExperimentalSilentThreeActRouteV111.Pool(
                    act, PrototypeRoomType.Boss, weak: false);
                var owner = $"act:{act}:boss_pool:"
                    + CanonicalJson.Sha256(pool);
                if (request.OwnerId == owner)
                    return ChanceLawEngineV111.QueryLaterActBossPool(
                        act, request.Ordinal);
            }
        }
        else if (request.SiteId == ExperimentalSilentThreeActRouteV111
                     .SecondBossIndexSiteId)
        {
            const string prefix = "act:3:first_boss:";
            const string suffix = ":alternatives:";
            if (request.OwnerId.StartsWith(prefix,
                    StringComparison.Ordinal))
            {
                var suffixIndex = request.OwnerId.IndexOf(suffix,
                    prefix.Length, StringComparison.Ordinal);
                if (suffixIndex > prefix.Length)
                    return ChanceLawEngineV111.QueryLaterActSecondBoss(
                        request.OwnerId[prefix.Length..suffixIndex],
                        ascension, request.Ordinal);
            }
        }
        throw new InvalidDataException(
            "ReferenceNature encounter transcript has an unsupported request.");
    }

    private static string? FirstUndiscoveredBoss(
        int act, SimulationProfile profile)
    {
        var discoveryOrder = act == 2
            ? ExperimentalSilentThreeActRouteV111.HiveBossDiscoveryOrder
            : ExperimentalSilentThreeActRouteV111.GloryBossDiscoveryOrder;
        return discoveryOrder.FirstOrDefault(id =>
            !profile.SeenBossEncounters.Contains(id, StringComparer.Ordinal));
    }
}

public sealed record PrototypeLaterActEncounterBagV111
{
    private string[] _remainingWeak;
    private string[] _remainingNormal;
    private string[] _remainingElite;

    [JsonConstructor]
    public PrototypeLaterActEncounterBagV111(
        int Act,
        int OrdinaryCombatsStarted,
        string[] RemainingWeak,
        string[] RemainingNormal,
        string[] RemainingElite,
        string BossEncounterId,
        string? SecondBossEncounterId = null,
        string? AssignedAncientId = null)
    {
        this.Act = Act;
        this.OrdinaryCombatsStarted = OrdinaryCombatsStarted;
        _remainingWeak = CloneRequired(RemainingWeak, nameof(RemainingWeak));
        _remainingNormal = CloneRequired(
            RemainingNormal, nameof(RemainingNormal));
        _remainingElite = CloneRequired(RemainingElite, nameof(RemainingElite));
        this.BossEncounterId = BossEncounterId;
        this.SecondBossEncounterId = SecondBossEncounterId;
        this.AssignedAncientId = AssignedAncientId;
    }

    public int Act { get; init; }
    public int OrdinaryCombatsStarted { get; init; }
    public string[] RemainingWeak
    {
        get => (string[])_remainingWeak.Clone();
        init => _remainingWeak = CloneRequired(value, nameof(RemainingWeak));
    }
    public string[] RemainingNormal
    {
        get => (string[])_remainingNormal.Clone();
        init => _remainingNormal = CloneRequired(
            value, nameof(RemainingNormal));
    }
    public string[] RemainingElite
    {
        get => (string[])_remainingElite.Clone();
        init => _remainingElite = CloneRequired(value, nameof(RemainingElite));
    }
    public string BossEncounterId { get; init; }
    public string? SecondBossEncounterId { get; init; }
    public string? AssignedAncientId { get; init; }

    public PrototypeLaterActEncounterBagV111 Fork() => this with
    {
        RemainingWeak = _remainingWeak,
        RemainingNormal = _remainingNormal,
        RemainingElite = _remainingElite
    };

    public void Deconstruct(
        out int Act, out int OrdinaryCombatsStarted,
        out string[] RemainingWeak, out string[] RemainingNormal,
        out string[] RemainingElite, out string BossEncounterId,
        out string? SecondBossEncounterId, out string? AssignedAncientId)
    {
        Act = this.Act;
        OrdinaryCombatsStarted = this.OrdinaryCombatsStarted;
        RemainingWeak = this.RemainingWeak;
        RemainingNormal = this.RemainingNormal;
        RemainingElite = this.RemainingElite;
        BossEncounterId = this.BossEncounterId;
        SecondBossEncounterId = this.SecondBossEncounterId;
        AssignedAncientId = this.AssignedAncientId;
    }

    private static string[] CloneRequired(string[] values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return (string[])values.Clone();
    }
}
