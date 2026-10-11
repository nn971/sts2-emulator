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
        PrototypeNativeHiveElites.EntomancerEncounterId,
        PrototypeNativeHiveElites.PrismEncounterId,
        PrototypeNativeHiveElites.DecimillipedeEncounterId
    ];
    public static readonly string[] HiveBosses =
    [
        PrototypeNativeHiveBosses.KaiserEncounterId,
        PrototypeNativeHiveBosses.KnowledgeEncounterId,
        PrototypeNativeHiveBosses.InsatiableEncounterId
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
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId,
        PrototypeNativeGloryBosses.AeonglassEncounterId
    ];

    // Colorful Philosophers is still unimplemented, so it is not
    // misrepresented as an executable event despite its native ID.
    public static readonly string[] HiveEvents =
    [
        PrototypeNativeHiveEvents.AmalgamatorId,
        PrototypeNativeLaterActs.HiveBugslayerId,
        PrototypeNativeHiveEvents.ColossalFlowerId,
        PrototypeNativeHiveEvents.FieldOfManSizedHolesId,
        PrototypeNativeHiveEvents.InfestedAutomatonId,
        PrototypeNativeHiveEvents.LostWispId,
        PrototypeNativeHiveEvents.SpiritGrafterId,
        PrototypeNativeHiveEvents.LanternKeyEventId,
        PrototypeNativeHiveEvents.ZenWeaverId
    ];
    public static readonly string[] GloryEvents =
    [
        PrototypeNativeGloryEvents.HungryForMushroomsId,
        PrototypeNativeGloryEvents.ReflectionsId,
        PrototypeNativeGloryEvents.RoundTeaPartyId,
        PrototypeNativeGloryEvents.GraveOfTheForgottenId,
        PrototypeNativeGloryEvents.TrialId,
        PrototypeNativeGloryEvents.TinkerTimeId,
        PrototypeNativeGloryEvents.BattlewornDummyId
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

    public static PrototypeLaterActEncounterBagV111 InitialBag(
        int act, RngBundle rng)
    {
        if (act is < 2 or > 3)
            throw new ArgumentOutOfRangeException(nameof(act));
        var bosses = Pool(act, PrototypeRoomType.Boss, false);
        return new PrototypeLaterActEncounterBagV111(
            act, 0,
            (string[])Pool(act, PrototypeRoomType.Combat, true).Clone(),
            (string[])Pool(act, PrototypeRoomType.Combat, false).Clone(),
            (string[])Pool(act, PrototypeRoomType.Elite, false).Clone(),
            bosses[PrototypeRng.NextInt(rng, "combat", bosses.Length)]);
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
        var map = PrototypeNativeLaterActMap.GenerateNative(
            state.RunSeed, nextAct, state.Ascension);
        var nextWorld = world with
        {
            Act = nextAct,
            ActIdentity = state.Configuration.Acts[nextAct - 1],
            Floor = 0,
            ActiveRoom = null, Combat = null, Reward = null,
            Shop = null, Event = null, Map = map,
            ActOneRegion = null, ActOneEncounterPool = null,
            LaterActEncounterBag = InitialBag(nextAct, state.Rng),
            UnknownRoomOdds = null
        };
        return state with { World = nextWorld, Phase = RunPhase.MapChoice };
    }

    /// <summary>
    /// Native ?-room odds and room-history restrictions are not audited.
    /// Never substitutes an unrelated prototype regional encounter/event.
    /// </summary>
    public static PrototypeRoomType ResolveUnknown(
        RunWorldState world, RngBundle rng)
    {
        if (world.Act is < 2 or > 3)
            throw new NotSupportedException(
                "Later-act Unknown requires Hive or Glory.");
        // Guarantee an eligible regional event choice early in the act,
        // then distribute further visits among regional events, shops,
        // fights and treasure (approximation awaiting reference traces).
        if (!world.CompletedRooms.Any(room =>
            room.Act == world.Act
            && room.RoomType == PrototypeRoomType.Event))
            return PrototypeRoomType.Event;
        return PrototypeRng.NextInt(rng, "event", 4) switch
        {
            0 => PrototypeRoomType.Event,
            1 => PrototypeRoomType.Shop,
            2 => PrototypeRoomType.Treasure,
            _ => PrototypeRoomType.Combat
        };
    }

    /// <summary>
    /// Select without replacement from the regional weak/normal/elite
    /// bags, with one exhausted-bag refill and an act-pinned preselected
    /// boss. All semantics remain fork/snapshot safe. The contents are
    /// source-authored; selection weights/call order remain provisional.
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
            return (PrototypeContent.Encounter(bag.BossEncounterId), bag);

        var weak = room == PrototypeRoomType.Combat
            && bag.OrdinaryCombatsStarted < 3;
        var pool = Pool(world.Act, room, weak);
        var remaining = room == PrototypeRoomType.Elite
            ? bag.RemainingElite
            : weak ? bag.RemainingWeak : bag.RemainingNormal;
        if (remaining.Length == 0)
            remaining = (string[])pool.Clone();
        var previous = world.EncounterIds.LastOrDefault();
        var eligible = remaining.Length > 1
            ? remaining.Where(id => id != previous).ToArray()
            : remaining;
        if (eligible.Length == 0)
            eligible = remaining;
        var selected = eligible[PrototypeRng.NextInt(
            rng, "combat", eligible.Length)];
        var updatedRemaining = remaining
            .Where(id => id != selected).ToArray();
        var updatedBag = room == PrototypeRoomType.Elite
            ? bag with { RemainingElite = updatedRemaining }
            : weak
                ? bag with
                {
                    OrdinaryCombatsStarted = bag.OrdinaryCombatsStarted + 1,
                    RemainingWeak = updatedRemaining
                }
                : bag with
                {
                    OrdinaryCombatsStarted = bag.OrdinaryCombatsStarted + 1,
                    RemainingNormal = updatedRemaining
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
    string BossEncounterId)
{
    public PrototypeLaterActEncounterBagV111 Fork() => this with
    {
        RemainingWeak = (string[])RemainingWeak.Clone(),
        RemainingNormal = (string[])RemainingNormal.Clone(),
        RemainingElite = (string[])RemainingElite.Clone()
    };
}
