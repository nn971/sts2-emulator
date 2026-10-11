namespace Sts2Emulator.Core;

/// <summary>
/// Deliberately small, opt-in, end-to-end Silent route integration slice.
/// The Hive/Glory maps and enemy/event models are source-backed; encounter
/// selection, unknown rooms, combat RNG, rewards and shops remain explicitly
/// provisional. Default configured v111 and legacy training are unchanged.
/// No native content is silently replaced by an unrelated prototype enemy.
/// </summary>
public static class ExperimentalSilentThreeActRouteV111
{
    private static readonly string[] HiveWeak =
    [
        "proto.native.hive.encounter.tunneler_weak",
        "proto.native.hive.encounter.exoskeletons_weak"
    ];
    private static readonly string[] HiveNormal =
    [
        PrototypeNativeHiveNormals.SpinyToadNormalId,
        PrototypeNativeHiveNormals.MytesNormalId
    ];
    private static readonly string[] GloryWeak =
    [
        PrototypeNativeLaterActs.GlorySculptorWeakId,
        PrototypeNativeGloryWeak.TurretOperatorWeakId
    ];
    private static readonly string[] GloryNormal =
    [
        PrototypeNativeGloryNormals.FrogKnightEncounterId,
        PrototypeNativeGloryNormals.SlimedBerserkerEncounterId
    ];

    public static RunState ContinueAct(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Configuration?.UsesExperimentalSilentThreeActRoute != true
            || state.Configuration.CharacterId != "silent"
            || state.Phase != RunPhase.ActTransition
            || state.World is not { } world
            || world.Act is < 1 or >= 3
            || world.Combat is not null
            || world.Reward is not null)
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
            ActiveRoom = null,
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null,
            Map = map,
            ActOneRegion = null,
            ActOneEncounterPool = null,
            UnknownRoomOdds = null
        };
        return state with
        {
            World = nextWorld,
            Phase = RunPhase.MapChoice
        };
    }

    /// <summary>
    /// A deliberately bounded experiment: the first Unknown of each later
    /// act becomes an implemented event; subsequent Unknowns become ordinary
    /// fights or shops. The game-specific Unknown bag remains a release gap.
    /// </summary>
    public static PrototypeRoomType ResolveUnknown(
        RunWorldState world, RngBundle rng)
    {
        if (world.Act is < 2 or > 3)
            throw new NotSupportedException("Later-act Unknown is supported in Hive/Glory only.");
        if (!world.CompletedRooms.Any(room =>
            room.Act == world.Act && room.RoomType == PrototypeRoomType.Event))
            return PrototypeRoomType.Event;
        return PrototypeRng.NextInt(rng, "event", 4) == 0
            ? PrototypeRoomType.Shop
            : PrototypeRoomType.Combat;
    }

    /// <summary>
    /// One source-backed event per act avoids admitting incomplete
    /// region-wide event pools. Their outcome pools still use the existing
    /// prototype RNG, and native event-bag frequencies are unverified.
    /// </summary>
    public static RunState BeginEvent(RunState state)
    {
        var world = state.World
            ?? throw new InvalidOperationException("No active world.");
        if (state.Configuration?.UsesExperimentalSilentThreeActRoute != true
            || world.ActiveRoom != PrototypeRoomType.Event
            || world.Act is < 2 or > 3
            || world.CompletedRooms.Any(room => room.Act == world.Act
                && room.RoomType == PrototypeRoomType.Event))
            throw new NotSupportedException(
                "The experimental route admits only one ordinary Hive/Glory event per act.");
        var id = world.Act == 2
            ? PrototypeNativeHiveEvents.InfestedAutomatonId
            : PrototypeNativeGloryEvents.HungryForMushroomsId;
        var definition = PrototypeContent.Event(id);
        if (world.Act < definition.MinAct || world.Act > definition.MaxAct)
            throw new InvalidOperationException(
                "The route's source event belongs to a different act.");
        return state with
        {
            World = world with
            {
                Event = new EventState(id),
                EventHistory = world.EventIds.Append(id).ToArray()
            },
            Phase = RunPhase.Event
        };
    }

    /// <summary>
    /// Select from explicit verified-to-execute fixture registrations.
    /// This is provisional deterministic pool sampling, not the native
    /// weighted encounter bag, history, difficulty or RNG call order.
    /// </summary>
    public static PrototypeEncounterDefinition SelectEncounter(
        RunWorldState world, PrototypeRoomType room, RngBundle rng)
    {
        if (world.Act is < 2 or > 3
            || room is not (PrototypeRoomType.Combat
                or PrototypeRoomType.Elite or PrototypeRoomType.Boss))
            throw new NotSupportedException(
                "Experimental route selection requires a Hive/Glory combat room.");
        var ordinaryFights = world.EncounterIds.Count(id =>
        {
            var encounter = PrototypeContent.Encounter(id);
            return encounter.MinAct <= world.Act && encounter.MaxAct >= world.Act
                && encounter.RoomType == PrototypeRoomType.Combat;
        });
        string[] choices = (world.Act, room) switch
        {
            (2, PrototypeRoomType.Combat) when ordinaryFights < 3 => HiveWeak,
            (2, PrototypeRoomType.Combat) => HiveNormal,
            (2, PrototypeRoomType.Elite) =>
                [PrototypeNativeHiveElites.EntomancerEncounterId],
            (2, PrototypeRoomType.Boss) =>
                [PrototypeNativeHiveBosses.KaiserEncounterId],
            (3, PrototypeRoomType.Combat) when ordinaryFights < 3 => GloryWeak,
            (3, PrototypeRoomType.Combat) => GloryNormal,
            (3, PrototypeRoomType.Elite) =>
                [PrototypeNativeGloryElites.SoulNexusEncounterId],
            (3, PrototypeRoomType.Boss) =>
                [PrototypeNativeGloryBosses.QueenEncounterId],
            _ => throw new NotSupportedException("Unsupported later-act route room.")
        };
        var selected = PrototypeContent.Encounter(
            choices[PrototypeRng.NextInt(rng, "combat", choices.Length)]);
        if (selected.RoomType != room || selected.MinAct > world.Act
            || selected.MaxAct < world.Act
            || selected.MinFloor > world.Floor
            || selected.MaxFloor < world.Floor)
            throw new InvalidOperationException(
                "The experimental route's encounter fixture is out of scope.");
        return selected;
    }

    public static bool ContainsEncounter(string encounterId, int act) =>
        act switch
        {
            2 => HiveWeak.Concat(HiveNormal).Concat(
                [PrototypeNativeHiveElites.EntomancerEncounterId,
                 PrototypeNativeHiveBosses.KaiserEncounterId])
                .Contains(encounterId, StringComparer.Ordinal),
            3 => GloryWeak.Concat(GloryNormal).Concat(
                [PrototypeNativeGloryElites.SoulNexusEncounterId,
                 PrototypeNativeGloryBosses.QueenEncounterId])
                .Contains(encounterId, StringComparer.Ordinal),
            _ => false
        };
}
