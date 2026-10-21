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
        int? mapColumn = null, int? mapRow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        if (ascension is < 0 or > 10 || totalFloor < 1 ||
            firstMonsterCombatId < 1)
            throw new ArgumentOutOfRangeException(nameof(totalFloor),
                "Act 1 Nature fixture requires a positive native floor/ID and valid ascension.");
        var requiredName = encounterId switch
        {
            "proto.encounter.shrinker_beetle_weak" => "ShrinkerBeetleWeak",
            "proto.encounter.nibbits_weak" => "NibbitsWeak",
            // NativeActOneNormalCombatEntryV111 explicitly audits these
            // fixed single-enemy source formations and encounter-local RNG.
            "proto.encounter.mawler_normal" => "MawlerNormal",
            "proto.encounter.cubex_construct_normal" => "CubexConstructNormal",
            "proto.encounter.vine_shambler_normal" => "VineShamblerNormal",
            _ => throw new NotSupportedException(
                "UNSUPPORTED_SEMANTIC_OPENING site=act1.fixed_single_formation operation=" +
                encounterId)
        };
        if (nativeEntry != requiredName)
            throw new ArgumentException(
                "Act 1 fixture requires its source-pinned native entry name.",
                nameof(nativeEntry));

        var encounter = PrototypeContent.Encounter(encounterId);
        if (encounter.MinAct != 1 || encounter.MaxAct != 1 ||
            encounter.RoomType != PrototypeRoomType.Combat ||
            encounter.FixedEnemySpecs.Length != 1)
            throw new InvalidDataException(
                "Pinned Act 1 fixed single-enemy source encounter has changed.");

        var formation = NativeEncounterFormationFactoryV111.Generate(
            seed, encounter, nativeEntry, totalFloor,
            currentActIndex: 0, firstMonsterCombatId,
            mapColumn, mapRow);
        if (formation.EncounterOpeningOffset is not null ||
            !formation.Enemies.SequenceEqual(encounter.FixedEnemySpecs))
            throw new InvalidDataException(
                "Pinned Act 1 fixed single-enemy formation no longer matches the source.");

        var enemies = formation.Enemies.Select((spec, index) =>
        {
            var definition = PrototypeContent.Enemy(spec.EnemyId);
            var starting = definition.HpRangeAt(1, ascension).Max;
            var powers = (definition.StartingPowers ??
                          Array.Empty<PrototypeStartingPowerSpec>())
                .Select((power, powerIndex) => new PrototypePowerInstanceState(
                    power.PowerId, power.StacksAt(ascension), powerIndex + 1))
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
            NextPowerApplicationOrder: 1,
            Act: 1,
            Ascension: ascension);
        return (combat, formation, NativeRngV111.CreateBundle(seed));
    }
}
