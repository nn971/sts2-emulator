namespace Sts2Emulator.Core;

/// <summary>
/// Fork-owned, source-scoped encounter bag. All 38 act encounters are
/// enumerated in pinned Hive/Glory GenerateAllEncounters order. Weak
/// combats are used for the first two ordinary fight entries, while
/// elites and normal fights drain independent without-replacement
/// queues. This is a prototype bag policy, NOT verified native
/// encounter RNG or replenishment parity.
/// </summary>
public sealed record PrototypeNativeLaterActEncounterPoolState(
    int Act,
    int OrdinaryCombatsStarted,
    string[] RemainingWeakEncounterIds,
    string[] RemainingNormalEncounterIds,
    string[] RemainingEliteEncounterIds,
    string BossEncounterId)
{
    public PrototypeNativeLaterActEncounterPoolState Fork() => this with
    {
        RemainingWeakEncounterIds = (string[])RemainingWeakEncounterIds.Clone(),
        RemainingNormalEncounterIds = (string[])RemainingNormalEncounterIds.Clone(),
        RemainingEliteEncounterIds = (string[])RemainingEliteEncounterIds.Clone()
    };
}

public static class PrototypeNativeLaterActCombatPool
{
    public const string ConstructMenagerieId =
        "proto.native.glory.encounter.construct_menagerie_normal";

    public static string[] HiveWeak { get; } =
    [
        "proto.native.hive.encounter.bowlbugs_weak",
        "proto.native.hive.encounter.exoskeletons_weak",
        "proto.native.hive.encounter.thieving_hopper_weak",
        "proto.native.hive.encounter.tunneler_weak"
    ];

    public static string[] HiveNormal { get; } =
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

    public static string[] HiveElite { get; } =
    [
        PrototypeNativeHiveElites.DecimillipedeEncounterId,
        PrototypeNativeHiveElites.EntomancerEncounterId,
        PrototypeNativeHiveElites.PrismEncounterId
    ];

    public static string[] HiveBoss { get; } =
    [
        PrototypeNativeHiveBosses.KaiserEncounterId,
        PrototypeNativeHiveBosses.KnowledgeEncounterId,
        PrototypeNativeHiveBosses.InsatiableEncounterId
    ];

    public static string[] GloryWeak { get; } =
    [
        PrototypeNativeLaterActs.GlorySculptorWeakId,
        PrototypeNativeGloryScrolls.WeakEncounterId,
        PrototypeNativeGloryWeak.TurretOperatorWeakId
    ];

    public static string[] GloryNormal { get; } =
    [
        PrototypeNativeGloryNewNormals.AxebotsEncounterId,
        ConstructMenagerieId,
        PrototypeNativeGloryNewNormals.FabricatorEncounterId,
        PrototypeNativeGloryNormals.FrogKnightEncounterId,
        PrototypeNativeGloryNewNormals.GlobeHeadEncounterId,
        PrototypeNativeGloryNewNormals.OwlEncounterId,
        PrototypeNativeGloryScrolls.NormalEncounterId,
        PrototypeNativeGloryNormals.SlimedBerserkerEncounterId,
        PrototypeNativeGloryNewNormals.LostAndForgottenEncounterId
    ];

    public static string[] GloryElite { get; } =
    [
        PrototypeNativeGloryBosses.KnightsEncounterId,
        PrototypeNativeGloryElites.MechaKnightEncounterId,
        PrototypeNativeGloryElites.SoulNexusEncounterId
    ];

    public static string[] GloryBoss { get; } =
    [
        PrototypeNativeGloryBosses.AeonglassEncounterId,
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId
    ];

    public static PrototypeEncounterDefinition[] AdditionalEncounters { get; } =
    [
        // ConstructMenagerieNormal.GenerateMonsters creates one mutable
        // PunchConstruct followed by two CubexConstructs, no named slots.
        new(ConstructMenagerieId, PrototypeRoomType.Combat,
            ["proto.enemy.punch_construct",
             "proto.enemy.cubex_construct",
             "proto.enemy.cubex_construct"],
            MinAct: 3, MaxAct: 3, Weight: 0)
    ];

    public static string[] Names(int act, PrototypeRoomType room,
        bool weak = false)
    {
        string[] source = (act, room, weak) switch
        {
            (2, PrototypeRoomType.Combat, true) => HiveWeak,
            (2, PrototypeRoomType.Combat, false) => HiveNormal,
            (2, PrototypeRoomType.Elite, _) => HiveElite,
            (2, PrototypeRoomType.Boss, _) => HiveBoss,
            (3, PrototypeRoomType.Combat, true) => GloryWeak,
            (3, PrototypeRoomType.Combat, false) => GloryNormal,
            (3, PrototypeRoomType.Elite, _) => GloryElite,
            (3, PrototypeRoomType.Boss, _) => GloryBoss,
            _ => throw new ArgumentOutOfRangeException(nameof(room),
                "A native later-act encounter must be Combat, Elite or Boss.")
        };
        return (string[])source.Clone();
    }

    public static PrototypeNativeLaterActEncounterPoolState Create(
        int act, RngBundle rng)
    {
        var bosses = Names(act, PrototypeRoomType.Boss);
        var boss = bosses[PrototypeRng.NextInt(rng, "combat", bosses.Length)];
        return new(act, 0,
            Names(act, PrototypeRoomType.Combat, weak: true),
            Names(act, PrototypeRoomType.Combat),
            Names(act, PrototypeRoomType.Elite),
            boss);
    }

    public static (PrototypeEncounterDefinition Encounter,
        PrototypeNativeLaterActEncounterPoolState Pool)
        Pick(PrototypeNativeLaterActEncounterPoolState pool,
            PrototypeRoomType room, int floor, RngBundle rng)
    {
        if (room == PrototypeRoomType.Boss)
            return (PrototypeContent.Encounter(pool.BossEncounterId), pool);

        if (room is not (PrototypeRoomType.Combat or PrototypeRoomType.Elite))
            throw new NotSupportedException(
                $"No native later-act combat pool for {room}.");
        var weak = room == PrototypeRoomType.Combat
            && pool.OrdinaryCombatsStarted < 2;

        var available = room == PrototypeRoomType.Elite
            ? pool.RemainingEliteEncounterIds
            : weak ? pool.RemainingWeakEncounterIds
                : pool.RemainingNormalEncounterIds;
        var source = Names(pool.Act, room, weak);
        // Remove invalid floor-scoped entries first; an unsupported or
        // empty native pool is an error, never a prototype fallback.
        var eligible = available.Where(id =>
        {
            var e = PrototypeContent.Encounter(id);
            return e.MinAct <= pool.Act && e.MaxAct >= pool.Act
                && e.MinFloor <= floor && e.MaxFloor >= floor;
        }).ToArray();
        if (eligible.Length == 0)
        {
            eligible = source.Where(id =>
            {
                var e = PrototypeContent.Encounter(id);
                return e.MinAct <= pool.Act && e.MaxAct >= pool.Act
                    && e.MinFloor <= floor && e.MaxFloor >= floor;
            }).ToArray();
        }
        if (eligible.Length == 0)
            throw new NotSupportedException(
                $"No native {pool.Act} encounter of type {room} is eligible.");

        var selected = eligible[
            PrototypeRng.NextInt(rng, "combat", eligible.Length)];
        var next = available.Where(id =>
            !StringComparer.Ordinal.Equals(id, selected)).ToArray();
        pool = room switch
        {
            PrototypeRoomType.Elite => pool with
            {
                RemainingEliteEncounterIds = next
            },
            _ when weak => pool with
            {
                RemainingWeakEncounterIds = next,
                OrdinaryCombatsStarted = pool.OrdinaryCombatsStarted + 1
            },
            _ => pool with
            {
                RemainingNormalEncounterIds = next,
                OrdinaryCombatsStarted = pool.OrdinaryCombatsStarted + 1
            }
        };
        return (PrototypeContent.Encounter(selected), pool);
    }
}
