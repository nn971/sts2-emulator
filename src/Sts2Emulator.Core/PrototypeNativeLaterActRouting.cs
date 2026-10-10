namespace Sts2Emulator.Core;

/// <summary>
/// Source-shaped single-player Hive/Glory combat routing for v0.111.0.
/// Pinned inventories: Hive.GenerateAllEncounters (20), Glory (18);
/// weak counts 2/2; bosses from BossDiscoveryOrder. This remains a
/// prototype-RNG approximation of native RoomSet bag shuffling and does
/// not claim RNG/call-order parity.
/// </summary>
public static class PrototypeNativeLaterActRouting
{
    public const string HiveMapProfile =
        "native-hive-map-structure-v0.111.0-v1";
    public const string GloryMapProfile =
        "native-glory-map-structure-v0.111.0-v1";

    private static readonly string[] HiveBossDiscoveryOrder =
    [
        "proto.native.hive.encounter.the_insatiable_boss",
        "proto.native.hive.encounter.knowledge_demon_boss",
        "proto.native.hive.encounter.kaiser_crab_boss"
    ];
    private static readonly string[] GloryBossDiscoveryOrder =
    [
        PrototypeNativeGloryBosses.QueenEncounterId,
        PrototypeNativeGloryBosses.TestSubjectEncounterId,
        PrototypeNativeGloryBosses.AeonglassEncounterId
    ];

    public static MapState GenerateMap(
        int act, RngBundle rng, int ascension)
    {
        if (act is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(act));
        return PrototypeNativeOvergrowthMap.GenerateLaterAct(
            rng, ascension, act);
    }

    /// <summary>
    /// Typed v111 encounter IDs computed from the pinned source inventory.
    /// Never sample arbitrary registered prototype Act 2/3 encounters.
    /// </summary>
    public static string[] EncounterIds(int act, PrototypeRoomType room)
    {
        var names = act switch
        {
            2 => PrototypeNativeLaterActs.HiveEncounterNames,
            3 => PrototypeNativeLaterActs.GloryEncounterNames,
            _ => throw new ArgumentOutOfRangeException(nameof(act))
        };
        var suffix = room switch
        {
            PrototypeRoomType.Combat => "Normal",
            PrototypeRoomType.Elite => "Elite",
            PrototypeRoomType.Boss => "Boss",
            _ => throw new ArgumentOutOfRangeException(nameof(room))
        };
        var selected = names.Where(name => name.EndsWith(
            suffix, StringComparison.Ordinal));
        return selected.Select(name => Resolve(act, name, room)).ToArray();
    }

    public static string[] WeakEncounterIds(int act)
    {
        var names = act switch
        {
            2 => PrototypeNativeLaterActs.HiveEncounterNames,
            3 => PrototypeNativeLaterActs.GloryEncounterNames,
            _ => throw new ArgumentOutOfRangeException(nameof(act))
        };
        return names.Where(name => name.EndsWith(
            "Weak", StringComparison.Ordinal))
            .Select(name => Resolve(act, name, PrototypeRoomType.Combat))
            .ToArray();
    }

    private static string Resolve(int act, string nativeName,
        PrototypeRoomType expectedRoom)
    {
        var snake = string.Concat(nativeName.Select((c, i) =>
            char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c)
                : char.ToLowerInvariant(c).ToString()));
        var id = "proto.native." + (act == 2 ? "hive" : "glory")
            + ".encounter." + snake;
        var encounter = PrototypeContent.Encounter(id);
        if (encounter.RoomType != expectedRoom
            || encounter.MinAct > act || encounter.MaxAct < act)
            throw new InvalidOperationException(
                $"Pinned later-act encounter '{nativeName}' has invalid act/room registration.");
        return id;
    }

    public static PrototypeLaterActEncounterPoolState Create(
        int act, RngBundle rng)
    {
        var bosses = act switch
        {
            2 => HiveBossDiscoveryOrder,
            3 => GloryBossDiscoveryOrder,
            _ => throw new ArgumentOutOfRangeException(nameof(act))
        };
        var actualBosses = EncounterIds(act, PrototypeRoomType.Boss);
        if (!actualBosses.OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(bosses.OrderBy(id => id, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Pinned later-act boss discovery order is inconsistent.");
        return new PrototypeLaterActEncounterPoolState(
            Act: act,
            OrdinaryCombatsStarted: 0,
            RemainingWeakEncounterIds: WeakEncounterIds(act),
            RemainingNormalEncounterIds: EncounterIds(act, PrototypeRoomType.Combat),
            RemainingEliteEncounterIds: EncounterIds(act, PrototypeRoomType.Elite),
            BossEncounterId: bosses[
                PrototypeRng.NextInt(rng, "combat", bosses.Length)]);
    }

    public static (PrototypeEncounterDefinition Encounter,
        PrototypeLaterActEncounterPoolState NextPool) Pick(
        PrototypeLaterActEncounterPoolState pool, PrototypeRoomType room,
        string? previousEncounterId, RngBundle rng)
    {
        if (pool.Act is not (2 or 3))
            throw new InvalidOperationException(
                "Later-act encounter pool cannot be used for Act 1.");
        if (room == PrototypeRoomType.Boss)
        {
            return (PrototypeContent.Encounter(pool.BossEncounterId),
                pool);
        }
        if (room is not (PrototypeRoomType.Combat or PrototypeRoomType.Elite))
            throw new InvalidOperationException(
                "Only combat/elite/boss rooms have encounter bags.");

        var weak = room == PrototypeRoomType.Combat
            && pool.OrdinaryCombatsStarted < 2;
        var fullBag = weak ? WeakEncounterIds(pool.Act)
            : room == PrototypeRoomType.Combat
                ? EncounterIds(pool.Act, PrototypeRoomType.Combat)
                : EncounterIds(pool.Act, PrototypeRoomType.Elite);
        var remaining = weak ? pool.RemainingWeakEncounterIds
            : room == PrototypeRoomType.Combat
                ? pool.RemainingNormalEncounterIds
                : pool.RemainingEliteEncounterIds;
        // Weak slots are drawn without replacement; normal and elite
        // bags refill deterministically on exhaustion. Suppress immediate
        // repeats whenever another eligible candidate exists.
        if (remaining.Length == 0)
        {
            if (weak)
                throw new InvalidOperationException(
                    "Later-act weak bag exhausted before its two encounters.");
            remaining = fullBag;
        }

        var eligible = remaining.Length > 1 && previousEncounterId is not null
            ? remaining.Where(id => id != previousEncounterId).ToArray()
            : remaining;
        if (eligible.Length == 0)
            eligible = remaining;
        var selectedId = eligible[
            PrototypeRng.NextInt(rng, "combat", eligible.Length)];
        var next = remaining.Where(id => id != selectedId).ToArray();
        var nextPool = weak
            ? pool with
            {
                OrdinaryCombatsStarted = pool.OrdinaryCombatsStarted + 1,
                RemainingWeakEncounterIds = next
            }
            : room == PrototypeRoomType.Combat
                ? pool with
                {
                    OrdinaryCombatsStarted = pool.OrdinaryCombatsStarted + 1,
                    RemainingNormalEncounterIds = next
                }
                : pool with { RemainingEliteEncounterIds = next };
        return (PrototypeContent.Encounter(selectedId), nextPool);
    }
}
