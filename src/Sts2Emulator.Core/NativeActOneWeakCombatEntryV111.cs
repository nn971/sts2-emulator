namespace Sts2Emulator.Core;

/// <summary>
/// Only those native v0.111.0 Act 1 weak encounter models whose
/// GenerateMonsters() is a fixed, source-audited formation are enabled
/// for experimental *live* combat entry. Encounter identity is explicit,
/// never derived heuristically from the emulator registry id.
/// </summary>
public static class NativeActOneWeakCombatEntryV111
{
    private static readonly IReadOnlyDictionary<string, string> NativeNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["proto.encounter.nibbits_weak"] = "NibbitsWeak",
            ["proto.encounter.fuzzy_wurm_crawler_weak"] =
                "FuzzyWurmCrawlerWeak",
            ["proto.encounter.shrinker_beetle_weak"] =
                "ShrinkerBeetleWeak",
            ["proto.encounter.seapunk_weak"] = "SeapunkWeak",
            ["proto.encounter.sludge_spinner_weak"] = "SludgeSpinnerWeak",
            ["proto.encounter.toadpoles_weak"] = "ToadpolesWeak"
        };

    public static string[] SupportedEncounterIds =>
        NativeNames.Keys.Order(StringComparer.Ordinal).ToArray();

    public static NativeEncounterFormationV111 Prepare(
        RunState state, RunWorldState world,
        PrototypeEncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(encounter);
        if (state.Configuration?.UsesExperimentalNativeWeakCombatEntry != true)
            throw new InvalidOperationException(
                "Live native encounter RNG requires the experimental fidelity opt-in.");
        if (world.Act != 1 || world.ActiveRoom != PrototypeRoomType.Combat
            || encounter.RoomType != PrototypeRoomType.Combat
            || !NativeNames.TryGetValue(encounter.Id, out var nativeName))
        {
            throw new NotSupportedException(
                $"Experimental native combat entry does not support '{encounter.Id}'. " +
                "No prototype encounter-formation or HP fallback is allowed.");
        }
        if (state.NativeRunRng is null)
            throw new InvalidOperationException(
                "Persistent native RNG must be enabled before live combat entry.");

        // Act 1's native-shaped map IDs are "1:Floor:Column".
        // Native CurrentMapCoord.row is zero-indexed; Floor is 1-indexed.
        var parts = world.Map.CurrentNodeId?.Split(':');
        if (parts is not { Length: 3 }
            || parts[0] != "1"
            || !int.TryParse(parts[1], out var floor)
            || !int.TryParse(parts[2], out var column)
            || floor != world.Floor || floor < 1
            || column is < 0 or >= PrototypeNativeOvergrowthMap.MapWidth)
            throw new NotSupportedException(
                "Experimental native combat entry requires a source-shaped Act 1 map coordinate.");

        var result = NativeEncounterFormationFactoryV111.Generate(
            state.RunSeed, encounter, nativeName,
            totalFloor: world.Floor, currentActIndex: 0,
            // CombatState.AddPlayer attaches the player as id 0 before
            // any monster is created; the first enemy's CombatId is 1.
            firstMonsterCombatId: 1,
            mapColumn: column, mapRow: floor - 1);

        if (result.Enemies.Length != encounter.FixedEnemySpecs.Length
            || !result.Enemies.SequenceEqual(encounter.FixedEnemySpecs))
            throw new InvalidOperationException(
                "Source-audited fixed weak encounter unexpectedly resolved a different formation.");
        return result;
    }
}
