namespace Sts2Emulator.Core;

/// <summary>
/// Explicit source-to-emulator normal encounter mapping for the experimental
/// v0.111.0 live combat RNG path. This audits *initial formation and its
/// encounter-local RNG call order*, not other monster lifecycle effects.
/// The legacy prototype selection, formation and HP rules are unchanged.
/// </summary>
public static class NativeActOneNormalCombatEntryV111
{
    private static readonly IReadOnlyDictionary<string, (string NativeName,
        PrototypeActOneRegion Region)> Entries =
        new Dictionary<string, (string, PrototypeActOneRegion)>(
            StringComparer.Ordinal)
        {
            ["proto.encounter.cubex_construct_normal"] =
                ("CubexConstructNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.flyconid_normal"] =
                ("FlyconidNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.fogmog_normal"] =
                ("FogmogNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.inklets_normal"] =
                ("InkletsNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.mawler_normal"] =
                ("MawlerNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.nibbits_normal"] =
                ("NibbitsNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.overgrowth_crawlers"] =
                ("OvergrowthCrawlers", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.ruby_raiders_normal"] =
                ("RubyRaidersNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.slimes_normal"] =
                ("SlimesNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.slithering_strangler_normal"] =
                ("SlitheringStranglerNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.snapping_jaxfruit_normal"] =
                ("SnappingJaxfruitNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.vine_shambler_normal"] =
                ("VineShamblerNormal", PrototypeActOneRegion.Overgrowth),
            ["proto.encounter.corpse_slugs_normal"] =
                ("CorpseSlugsNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.punch_construct_normal"] =
                ("PunchConstructNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.cultists_normal"] =
                ("CultistsNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.fossil_stalker_normal"] =
                ("FossilStalkerNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.seapunk_normal"] =
                ("SeapunkNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.haunted_ship_normal"] =
                ("HauntedShipNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.sewer_clam_normal"] =
                ("SewerClamNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.living_fog_normal"] =
                ("LivingFogNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.two_tailed_rats_normal"] =
                ("TwoTailedRatsNormal", PrototypeActOneRegion.Underdocks),
            ["proto.encounter.gremlin_merc_normal"] =
                ("GremlinMercNormal", PrototypeActOneRegion.Underdocks)
        };

    internal static string? ResearchNativeEntry(string encounterId) =>
        Entries.TryGetValue(encounterId, out var entry)
            ? entry.NativeName : null;

    public static string[] SupportedEncounterIds =>
        Entries.Keys.Order(StringComparer.Ordinal).ToArray();

    public static bool IsSupported(string id) => Entries.ContainsKey(id);

    public static NativeEncounterFormationV111 Prepare(
        RunState state, RunWorldState world,
        PrototypeEncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(encounter);
        if (state.Configuration?.UsesExperimentalNativeWeakCombatEntry != true)
            throw new InvalidOperationException(
                "Native normal combat entry requires the experimental fidelity opt-in.");
        if (!Entries.TryGetValue(encounter.Id, out var entry)
            || world.Act != 1
            || world.ActiveRoom != PrototypeRoomType.Combat
            || encounter.RoomType != PrototypeRoomType.Combat)
            throw new NotSupportedException(
                $"Experimental normal combat entry does not support '{encounter.Id}'.");
        var profileMatches = entry.Region == PrototypeActOneRegion.Overgrowth
            ? PrototypeNativeOvergrowthMap.IsGenerationProfile(
                world.Map.GenerationProfileId)
            : world.Map.GenerationProfileId ==
                PrototypeNativeUnderdocks.GenerationProfileId;
        if (world.ActOneRegion != entry.Region
            || !profileMatches)
            throw new NotSupportedException(
                "Native normal encounter is not in its pinned Act 1 region.");
        if (state.NativeRunRng is null)
            throw new InvalidOperationException(
                "Native normal combat entry requires persistent run RNG.");

        var coord = world.Map.CurrentNodeId?.Split(':');
        if (coord is not { Length: 3 }
            || coord[0] != "1"
            || !int.TryParse(coord[1], out var floor)
            || !int.TryParse(coord[2], out var column)
            || floor != world.Floor
            || floor is < 1 or > 16
            || column is < 0 or >= PrototypeNativeOvergrowthMap.MapWidth)
            throw new NotSupportedException(
                "Native normal combat entry requires a valid Act 1 map coordinate.");

        var result = NativeEncounterFormationFactoryV111.Generate(
            state.RunSeed, encounter, entry.NativeName,
            totalFloor: world.Floor, currentActIndex: 0,
            firstMonsterCombatId: 1, mapColumn: column, mapRow: floor - 1,
            sourceResolver: next => ResolveSourceFormation(encounter.Id, next));
        if (result.Enemies.Length == 0
            || result.Enemies.GroupBy(spec => spec.FormationPosition)
                .Any(group => group.Count() != 1))
            throw new InvalidOperationException(
                "Source normal encounter generated an invalid monster layout.");
        var expectedRotation = encounter.Id is
            "proto.encounter.corpse_slugs_normal"
            or "proto.encounter.two_tailed_rats_normal";
        if (expectedRotation != result.EncounterOpeningOffset.HasValue
            || result.EncounterOpeningOffset is < 0 or >= 3)
            throw new InvalidOperationException(
                "Source normal encounter's opening rotation is invalid.");
        return result;
    }

    private static PrototypeEncounterEnemySpec[] Specs(params string[] ids) =>
        ids.Select((id, i) => new PrototypeEncounterEnemySpec(id, i)).ToArray();

    private static PrototypeEncounterEnemySpec Spec(
        string enemy, int position, string? slot = null) =>
        new(enemy, position, slot);

    /// <summary>
    /// Exact GenerateMonsters() logic from the pinned source encounter
    /// classes. Fixed formations do not consume any encounter RNG;
    /// selections consume exactly the source's NextItem / NextBool calls.
    /// Order and null-vs-named slots intentionally differ from certain
    /// legacy prototype definitions.
    /// </summary>
    internal static (
        PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)
        ResolveSourceFormation(string id, Func<int, int> nextIndex)
    {
        const string smallLeaf = "proto.enemy.leaf_slime_s";
        const string smallTwig = "proto.enemy.twig_slime_s";
        const string mediumLeaf = "proto.enemy.leaf_slime_m";
        const string mediumTwig = "proto.enemy.twig_slime_m";
        string[] small = [smallLeaf, smallTwig];
        string[] medium = [mediumLeaf, mediumTwig];
        switch (id)
        {
            case "proto.encounter.cubex_construct_normal":
                return (Specs("proto.enemy.cubex_construct"), null);
            case "proto.encounter.flyconid_normal":
                return (Specs(medium[nextIndex(2)], "proto.enemy.flyconid"), null);
            case "proto.encounter.fogmog_normal":
                return ([Spec("proto.enemy.fogmog", 0, "fogmog")], null);
            case "proto.encounter.inklets_normal":
                // Native passes null slots for all three and separately
                // sets MiddleInklet=true on the second monster.
                return (Specs("proto.enemy.inklet", "proto.enemy.inklet",
                    "proto.enemy.inklet"), null);
            case "proto.encounter.mawler_normal":
                return (Specs("proto.enemy.mawler"), null);
            case "proto.encounter.nibbits_normal":
                return (
                [
                    Spec("proto.enemy.nibbit", 0, "front"),
                    Spec("proto.enemy.nibbit", 1, "back")
                ], null);
            case "proto.encounter.overgrowth_crawlers":
                return (Specs("proto.enemy.shrinker_beetle",
                    "proto.enemy.fuzzy_wurm_crawler"), null);
            case "proto.encounter.ruby_raiders_normal":
            {
                // The source's Dictionary<MonsterModel,int> inserts keys
                // in this order, each with valid count 1.
                var available = new List<string>
                {
                    "proto.enemy.axe_ruby_raider",
                    "proto.enemy.assassin_ruby_raider",
                    "proto.enemy.brute_ruby_raider",
                    "proto.enemy.crossbow_ruby_raider",
                    "proto.enemy.tracker_ruby_raider"
                };
                var selected = new List<string>();
                for (var i = 0; i < 3; i++)
                {
                    var index = nextIndex(available.Count);
                    selected.Add(available[index]);
                    available.RemoveAt(index);
                }
                return (Specs(selected.ToArray()), null);
            }
            case "proto.encounter.slimes_normal":
            {
                // Rng.NextBool is NextInt(2)==0; one encounter draw.
                var leafFirst = nextIndex(2) == 0;
                return (Specs(mediumTwig, mediumLeaf,
                    leafFirst ? smallLeaf : smallTwig,
                    leafFirst ? smallTwig : smallLeaf), null);
            }
            case "proto.encounter.slithering_strangler_normal":
            {
                // Native Enum.GetValues order: Jaxfruit, Medium,
                // SmallSlimes. Source appends Strangler at the END.
                var kind = nextIndex(3);
                var secondary = kind switch
                {
                    0 => new[] { "proto.enemy.snapping_jaxfruit" },
                    1 => new[] { medium[nextIndex(2)] },
                    2 => new[] { small[nextIndex(2)], small[nextIndex(2)] },
                    _ => throw new InvalidOperationException(
                        "Invalid secondary enemy enum choice.")
                };
                return (Specs(secondary.Append(
                    "proto.enemy.slithering_strangler").ToArray()), null);
            }
            case "proto.encounter.snapping_jaxfruit_normal":
                return (Specs("proto.enemy.snapping_jaxfruit",
                    "proto.enemy.flyconid"), null);
            case "proto.encounter.vine_shambler_normal":
                return (Specs("proto.enemy.vine_shambler"), null);
            case "proto.encounter.corpse_slugs_normal":
                return (Specs("proto.enemy.corpse_slug",
                    "proto.enemy.corpse_slug", "proto.enemy.corpse_slug"),
                    nextIndex(3));
            case "proto.encounter.punch_construct_normal":
                return (Specs("proto.enemy.punch_construct"), null);
            case "proto.encounter.cultists_normal":
                return (Specs("proto.enemy.calcified_cultist",
                    "proto.enemy.damp_cultist"), null);
            case "proto.encounter.fossil_stalker_normal":
                return (Specs("proto.enemy.fossil_stalker"), null);
            case "proto.encounter.seapunk_normal":
                return (Specs("proto.enemy.calcified_cultist",
                    "proto.enemy.seapunk"), null);
            case "proto.encounter.haunted_ship_normal":
                return (Specs("proto.enemy.haunted_ship"), null);
            case "proto.encounter.sewer_clam_normal":
                return (Specs("proto.enemy.sewer_clam"), null);
            case "proto.encounter.living_fog_normal":
                return ([Spec("proto.enemy.living_fog", 5, "livingFog")],
                    null);
            case "proto.encounter.two_tailed_rats_normal":
                // Three rats are in slots 3-5. StarterMoveIndex is
                // nextIndex(3), then (offset+1)%3 and (offset+2)%3.
                return (
                [
                    Spec("proto.enemy.two_tailed_rat", 2, "third"),
                    Spec("proto.enemy.two_tailed_rat", 3, "fourth"),
                    Spec("proto.enemy.two_tailed_rat", 4, "fifth")
                ], nextIndex(3));
            case "proto.encounter.gremlin_merc_normal":
                return ([Spec("proto.enemy.gremlin_merc", 0, "merc")],
                    null);
            default:
                throw new NotSupportedException(
                    $"Source formation not audited for encounter '{id}'.");
        }
    }
}
