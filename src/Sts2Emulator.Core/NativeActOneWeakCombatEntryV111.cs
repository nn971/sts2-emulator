namespace Sts2Emulator.Core;

/// <summary>
/// Source-audited native v0.111.0 Act 1 weak encounter formation
/// generation for opt-in live combat entry. Native encounter identity
/// is explicit, never guessed from the emulator registry ID.
/// Includes source-specific randomized Slimes and Corpse Slugs.
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
            ["proto.encounter.toadpoles_weak"] = "ToadpolesWeak",
            ["proto.encounter.slimes_weak"] = "SlimesWeak",
            ["proto.encounter.corpse_slugs_weak"] = "CorpseSlugsWeak"
        };

    internal static string? ResearchNativeEntry(string encounterId) =>
        NativeNames.TryGetValue(encounterId, out var entry) ? entry : null;

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
        var expectedRegion = encounter.Id is
            "proto.encounter.seapunk_weak"
            or "proto.encounter.sludge_spinner_weak"
            or "proto.encounter.toadpoles_weak"
            or "proto.encounter.corpse_slugs_weak"
                ? PrototypeActOneRegion.Underdocks
                : PrototypeActOneRegion.Overgrowth;
        var expectedMapProfileMatches = expectedRegion
            == PrototypeActOneRegion.Underdocks
                ? world.Map.GenerationProfileId ==
                    PrototypeNativeUnderdocks.GenerationProfileId
                : PrototypeNativeOvergrowthMap.IsGenerationProfile(
                    world.Map.GenerationProfileId);
        if (world.ActOneRegion != expectedRegion
            || !expectedMapProfileMatches)
            throw new NotSupportedException(
                "Native weak encounter must belong to the active pinned Act 1 region.");
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

        Func<Func<int, int>,
            (PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)>?
            sourceResolver = encounter.Id switch
            {
                "proto.encounter.slimes_weak" => ResolveSlimesWeak,
                "proto.encounter.corpse_slugs_weak" =>
                    nextIndex => ResolveCorpseSlugsWeak(encounter, nextIndex),
                _ => null
            };

        var result = NativeEncounterFormationFactoryV111.Generate(
            state.RunSeed, encounter, nativeName,
            totalFloor: world.Floor, currentActIndex: 0,
            // CombatState.AddPlayer attaches the player as id 0 before
            // any monster is created; the first enemy's CombatId is 1.
            firstMonsterCombatId: 1,
            mapColumn: column, mapRow: floor - 1,
            sourceResolver: sourceResolver);

        if (sourceResolver is null
            && (result.Enemies.Length != encounter.FixedEnemySpecs.Length
                || !result.Enemies.SequenceEqual(encounter.FixedEnemySpecs)
                || result.EncounterOpeningOffset is not null))
            throw new InvalidOperationException(
                "Source-audited fixed weak encounter unexpectedly resolved a different formation.");
        if (encounter.Id == "proto.encounter.slimes_weak"
            && (result.Enemies.Length != 3
                || result.Enemies[0].FormationPosition != 0
                || result.Enemies[1].FormationPosition != 1
                || result.Enemies[2].FormationPosition != 2
                || result.EncounterOpeningOffset is not null))
            throw new InvalidOperationException(
                "Native SlimesWeak source formation is invalid.");
        if (encounter.Id == "proto.encounter.corpse_slugs_weak"
            && (result.Enemies.Length != 2
                || result.EncounterOpeningOffset is not (>= 0 and < 3)))
            throw new InvalidOperationException(
                "Native CorpseSlugsWeak opening rotation is invalid.");
        return result;
    }

    /// <summary>
    /// Source: SlimesWeak.GenerateMonsters(). NextItem is called THREE
    /// times, including the one-item remaining-small list. The second
    /// small slime is only added after the medium slime: [small, medium,
    /// other small]. The generic grouped emulator resolver skips the
    /// singleton roll and has a different draw order, so must not be used.
    /// </summary>
    internal static (
        PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)
        ResolveSlimesWeak(Func<int, int> nextIndex)
    {
        string[] small =
        [
            "proto.enemy.leaf_slime_s",
            "proto.enemy.twig_slime_s"
        ];
        string[] medium =
        [
            "proto.enemy.leaf_slime_m",
            "proto.enemy.twig_slime_m"
        ];
        var firstSmall = nextIndex(small.Length);
        _ = nextIndex(1); // Rng.NextItem(singleton) still advances.
        var chosenMedium = nextIndex(medium.Length);
        return (
        [
            new(small[firstSmall], 0),
            new(medium[chosenMedium], 1),
            new(small[1 - firstSmall], 2)
        ], null);
    }

    /// <summary>
    /// Source: CorpseSlugsWeak.GenerateMonsters() invokes
    /// CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves exactly once,
    /// rolling NextInt(3), then assigns successive indices modulo 3.
    /// The offset belongs to encounter RNG (NOT run.monster_ai).
    /// The combat opening may not sample another rotation.
    /// </summary>
    private static (
        PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)
        ResolveCorpseSlugsWeak(
            PrototypeEncounterDefinition encounter, Func<int, int> nextIndex)
    {
        var specs = encounter.FixedEnemySpecs;
        if (specs.Length != 2
            || specs.Any(spec => spec.EnemyId != "proto.enemy.corpse_slug")
            || encounter.CyclicOpeningAiStateIds is not { Length: 3 })
            throw new InvalidOperationException(
                "CorpseSlugsWeak source-shaped definition has changed.");
        return (specs.Select(spec => spec with { }).ToArray(),
            nextIndex(3));
    }
}
