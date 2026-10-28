namespace Sts2Emulator.Core;

/// <summary>
/// Isolated source-pinned Act 1 fixed single-enemy entry for Nature H1/H2.
/// H2 adds selected native normal encounters, preserving H1 weak semantics. The separate later-act fixture keeps
/// its original Hive/Glory-only public contract. This object produces
/// only a pre-playable source-shaped RNG formation and starter combat;
/// the controlled opening attaches the real starting Silent player.
/// </summary>
public static class PrototypeNativeActOneWeakNatureFixtureV111
{
    public static (
        CombatState Combat, NativeEncounterFormationV111 Formation,
        RngBundle NativeStreams) CreateCombatRngPreEntry(
        string seed, string encounterId, string nativeEntry,
        int totalFloor, int ascension = 0, long firstMonsterCombatId = 1,
        int? mapColumn = null, int? mapRow = null,
        bool richResearch = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        if (ascension is < 0 or > 10 || totalFloor < 1 ||
            firstMonsterCombatId < 1)
            throw new ArgumentOutOfRangeException(nameof(totalFloor),
                "Act 1 Nature fixture requires a positive native floor/ID and valid ascension.");
        // The original H1/H2 and H9 v1 route remains a pinned
        // single-enemy fixture. Rich research fixtures additionally support
        // real source-shaped multi-enemy and elite formations.
        var encounter = PrototypeContent.Encounter(encounterId);
        NativeEncounterFormationV111 formation;
        if (richResearch)
        {
            var allowed = ResearchCombatFixtureGeneratorV111.RichSupportedEncounters
                .Any(item =>
                    item.EncounterId == encounterId &&
                    item.NativeEntry == nativeEntry &&
                    item.TotalFloor == totalFloor &&
                    item.MapRow == mapRow);
            if (!allowed || encounter.MinAct != 1 || encounter.MaxAct != 1 ||
                encounter.RoomType is not (
                    PrototypeRoomType.Combat or
                    PrototypeRoomType.Elite or
                    PrototypeRoomType.Boss) ||
                ((encounter.RoomType == PrototypeRoomType.Boss) !=
                    (totalFloor == 6)))
                throw new NotSupportedException(
                    "UNSUPPORTED_RESEARCH_OPENING encounter=" + encounterId);

            Func<Func<int, int>,
                (PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)>?
                resolver = null;
            if (NativeActOneNormalCombatEntryV111.IsSupported(encounterId))
                resolver = next =>
                    NativeActOneNormalCombatEntryV111.ResolveSourceFormation(
                        encounterId, next);
            else if (encounterId == "proto.encounter.slimes_weak")
                resolver = NativeActOneWeakCombatEntryV111.ResolveSlimesWeak;
            else if (NativeActOneEliteBossCombatEntryV111.IsSupported(encounterId))
                resolver = _ => (
                    NativeActOneEliteBossCombatEntryV111.ResearchSourceFormation(
                        encounterId), null);

            formation = NativeEncounterFormationFactoryV111.Generate(
                seed, encounter, nativeEntry, totalFloor,
                currentActIndex: 0, firstMonsterCombatId: firstMonsterCombatId,
                mapColumn: mapColumn, mapRow: mapRow,
                sourceResolver: resolver);
            if (formation.Enemies.Length == 0 ||
                formation.EncounterOpeningOffset is not null ||
                formation.Enemies.GroupBy(spec => spec.FormationPosition)
                    .Any(group => group.Count() != 1) ||
                formation.Enemies.Any(spec => spec.LeaderFormationPosition is
                    { } leader && !formation.Enemies.Any(other =>
                        other.FormationPosition == leader)))
                throw new InvalidDataException(
                    "Invalid rich research native encounter formation.");
        }
        else
        {
            var requiredName = encounterId switch
            {
                "proto.encounter.shrinker_beetle_weak" => "ShrinkerBeetleWeak",
                "proto.encounter.nibbits_weak" => "NibbitsWeak",
                "proto.encounter.mawler_normal" => "MawlerNormal",
                "proto.encounter.cubex_construct_normal" => "CubexConstructNormal",
                "proto.encounter.vine_shambler_normal" => "VineShamblerNormal",
                "proto.encounter.ceremonial_beast_boss" => "CeremonialBeastBoss",
                "proto.encounter.vantom_boss" => "VantomBoss",
                _ => throw new NotSupportedException(
                    "UNSUPPORTED_SEMANTIC_OPENING site=act1.fixed_single_formation operation=" +
                    encounterId)
            };
            if (nativeEntry != requiredName)
                throw new ArgumentException(
                    "Act 1 fixture requires its source-pinned native entry name.",
                    nameof(nativeEntry));
            var boss = encounter.RoomType == PrototypeRoomType.Boss;
            if (encounter.MinAct != 1 || encounter.MaxAct != 1 ||
                encounter.RoomType is not (
                    PrototypeRoomType.Combat or PrototypeRoomType.Boss) ||
                encounter.FixedEnemySpecs.Length != 1 ||
                (boss && (totalFloor != 6 || mapRow != 5)) ||
                (!boss && totalFloor == 6))
                throw new InvalidDataException(
                    "Pinned Act 1 conditioned encounter/floor has changed.");
            if (boss && encounterId is not (
                "proto.encounter.ceremonial_beast_boss" or
                "proto.encounter.vantom_boss"))
                throw new InvalidDataException("Unpinned boss research formation.");

            formation = NativeEncounterFormationFactoryV111.Generate(
                seed, encounter, nativeEntry, totalFloor,
                currentActIndex: 0, firstMonsterCombatId: firstMonsterCombatId,
                mapColumn: mapColumn, mapRow: mapRow);
            if (formation.EncounterOpeningOffset is not null ||
                !formation.Enemies.SequenceEqual(encounter.FixedEnemySpecs))
                throw new InvalidDataException(
                    "Pinned Act 1 fixed single-enemy formation no longer matches the source.");
        }

        // Application order is global to the combat, including powers on
        // different enemies. The old single-formation fixture keeps its
        // original ordering and serialized behavior.
        long nextPowerOrder = 1;
        var enemies = formation.Enemies.Select((spec, index) =>
        {
            var definition = PrototypeContent.Enemy(spec.EnemyId);
            var starting = definition.HpRangeAt(1, ascension).Max;
            var powers = (definition.StartingPowers ??
                          Array.Empty<PrototypeStartingPowerSpec>())
                .Select((power, powerIndex) => new PrototypePowerInstanceState(
                    power.PowerId, power.StacksAt(ascension),
                    richResearch ? nextPowerOrder++ : powerIndex + 1))
                .Where(power => power.Stacks > 0).ToArray();
            return new EnemyCombatState(
                InstanceId: index + 1,
                EnemyId: spec.EnemyId,
                Hp: starting,
                MaxHp: starting,
                Block: powers.Sum(power => power.Stacks *
                    PrototypeContent.Power(power.PowerId)
                        .EnemyStartingBlockPerStack),
                MoveIndex: 0,
                Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                Powers: powers,
                FormationPosition: spec.FormationPosition,
                SlotName: spec.SlotName,
                LeaderEnemyInstanceId: spec.LeaderFormationPosition is { } leader
                    ? Array.FindIndex(formation.Enemies, other =>
                        other.FormationPosition == leader) + 1
                    : null,
                NonSummonMovesUntilEligible: definition.NonSummonMovesBeforeEligible);
        }).ToArray();

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: richResearch ? nextPowerOrder : 1,
            Act: 1,
            Ascension: ascension);
        return (combat, formation, NativeRngV111.CreateBundle(seed));
    }
}
