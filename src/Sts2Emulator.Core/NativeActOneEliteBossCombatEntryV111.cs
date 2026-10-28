namespace Sts2Emulator.Core;

/// <summary>
/// Pinned v0.111.0 GenerateMonsters() formations for all six Act 1 elites
/// and all six Act 1 bosses. This is an opt-in native *combat entry* boundary,
/// not a claim of complete native combat, summoning, phase or reward parity.
/// Fixed formations consume no encounter-local RNG.
/// </summary>
public static class NativeActOneEliteBossCombatEntryV111
{
    private sealed record Entry(
        string NativeName,
        PrototypeActOneRegion Region,
        PrototypeRoomType Room,
        PrototypeEncounterEnemySpec[] Formation);

    private static readonly IReadOnlyDictionary<string, Entry> Entries =
        new Dictionary<string, Entry>(StringComparer.Ordinal)
        {
            ["proto.encounter.bygone_effigy_elite"] = new(
                "BygoneEffigyElite", PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Elite,
                [new("proto.enemy.bygone_effigy", 0)]),
            ["proto.encounter.byrdonis_elite"] = new(
                "ByrdonisElite", PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Elite,
                [new("proto.enemy.byrdonis", 0)]),
            ["proto.encounter.phrog_parasite_elite"] = new(
                "PhrogParasiteElite", PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Elite,
                [new("proto.enemy.phrog_parasite", 0, "phrog")]),
            ["proto.encounter.ceremonial_beast_boss"] = new(
                "CeremonialBeastBoss", PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Boss,
                [new("proto.enemy.ceremonial_beast", 0)]),
            ["proto.encounter.the_kin_boss"] = new(
                "TheKinBoss", PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Boss,
                [
                    // Source TheKinBoss.GenerateMonsters sets
                    // StartsWithDance=true on the FIRST KinFollower,
                    // before creating the default second follower.
                    // KinPriest is created last; all three have named slots.
                    new("proto.enemy.kin_follower", 0, "slot1",
                        LeaderFormationPosition: 2),
                    new("proto.enemy.kin_follower", 1, "slot2",
                        LeaderFormationPosition: 2),
                    new("proto.enemy.kin_priest", 2, "leaderSlot")
                ]),
            ["proto.encounter.vantom_boss"] = new(
                "VantomBoss", PrototypeActOneRegion.Overgrowth,
                PrototypeRoomType.Boss,
                [new("proto.enemy.vantom", 0)]),

            ["proto.encounter.phantasmal_gardeners_elite"] = new(
                "PhantasmalGardenersElite", PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Elite,
                [
                    new("proto.enemy.phantasmal_gardener", 0, "first"),
                    new("proto.enemy.phantasmal_gardener", 1, "second"),
                    new("proto.enemy.phantasmal_gardener", 2, "third"),
                    new("proto.enemy.phantasmal_gardener", 3, "fourth")
                ]),
            ["proto.encounter.skulking_colony_elite"] = new(
                "SkulkingColonyElite", PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Elite,
                [new("proto.enemy.skulking_colony", 0)]),
            ["proto.encounter.terror_eel_elite"] = new(
                "TerrorEelElite", PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Elite,
                [new("proto.enemy.terror_eel", 0)]),
            ["proto.encounter.lagavulin_matriarch_boss"] = new(
                "LagavulinMatriarchBoss", PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Boss,
                [new("proto.enemy.lagavulin_matriarch", 0)]),
            ["proto.encounter.soul_fysh_boss"] = new(
                "SoulFyshBoss", PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Boss,
                [new("proto.enemy.soul_fysh", 0)]),
            ["proto.encounter.waterfall_giant_boss"] = new(
                "WaterfallGiantBoss", PrototypeActOneRegion.Underdocks,
                PrototypeRoomType.Boss,
                [new("proto.enemy.waterfall_giant", 0)])
        };

    public static string[] SupportedEncounterIds =>
        Entries.Keys.Order(StringComparer.Ordinal).ToArray();

    public static bool IsSupported(string encounterId) =>
        Entries.ContainsKey(encounterId);

    // Reuse the pinned native fixed formations in isolated research openings.
    // These contain source slot and leader references absent from some legacy
    // encounter definitions. Do not consume additional encounter RNG.
    internal static PrototypeEncounterEnemySpec[] ResearchSourceFormation(
        string encounterId)
    {
        if (!Entries.TryGetValue(encounterId, out var entry))
            throw new NotSupportedException(
                "Unsupported research elite/boss formation: " + encounterId);
        return entry.Formation.Select(spec => spec with { }).ToArray();
    }

    public static NativeEncounterFormationV111 Prepare(
        RunState state, RunWorldState world,
        PrototypeEncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(encounter);
        if (state.Configuration?.UsesExperimentalNativeWeakCombatEntry != true)
            throw new InvalidOperationException(
                "Native elite/boss entry requires the experimental fidelity opt-in.");
        if (!Entries.TryGetValue(encounter.Id, out var entry)
            || entry is null
            || world.Act != 1
            || world.ActiveRoom != entry.Room
            || encounter.RoomType != entry.Room)
            throw new NotSupportedException(
                $"Experimental native elite/boss entry does not support '{encounter.Id}'.");

        var mapProfileMatches = entry.Region == PrototypeActOneRegion.Overgrowth
            ? PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId)
            : world.Map.GenerationProfileId ==
                PrototypeNativeUnderdocks.GenerationProfileId;
        if (world.ActOneRegion != entry.Region
            || !mapProfileMatches)
            throw new NotSupportedException(
                "Native elite/boss encounter is not in its pinned Act 1 region.");
        if (state.NativeRunRng is null)
            throw new InvalidOperationException(
                "Native elite/boss entry requires persistent run RNG.");
        var coord = world.Map.CurrentNodeId?.Split(':');
        if (coord is not { Length: 3 }
            || coord[0] != "1"
            || !int.TryParse(coord[1], out var floor)
            || !int.TryParse(coord[2], out var column)
            || floor != world.Floor
            || floor is < 1 or > 16
            || (entry.Room == PrototypeRoomType.Boss && floor != 16)
            || (entry.Room == PrototypeRoomType.Elite && floor >= 16)
            || column is < 0 or >= PrototypeNativeOvergrowthMap.MapWidth)
            throw new NotSupportedException(
                "Native elite/boss entry requires a valid Act 1 map coordinate.");

        var source = NativeEncounterFormationFactoryV111.Generate(
            state.RunSeed, encounter, entry.NativeName,
            totalFloor: world.Floor, currentActIndex: 0,
            firstMonsterCombatId: 1, mapColumn: column, mapRow: floor - 1,
            sourceResolver: _ => (
                entry.Formation.Select(spec => spec with { }).ToArray(),
                (int?)null));
        if (source.EncounterOpeningOffset is not null
            || NativeRngV111.ToSnapshot(source.EncounterRng).Counter != 0
            || !source.Enemies.SequenceEqual(entry.Formation)
            || source.Enemies.Length != source.MonsterContexts.Length
            || source.Enemies.GroupBy(spec => spec.FormationPosition)
                .Any(group => group.Count() != 1))
            throw new InvalidOperationException(
                "Pinned elite/boss formation or encounter RNG changed.");
        return source;
    }
}
