namespace Sts2Emulator.Core;

/// <summary>
/// A generated monster's native creature-local RNG context. The combat id is
/// supplied by the owning CombatState, which includes the player creature and
/// all earlier spawns; formation position is not the native combat id.
/// </summary>
public sealed record NativeEncounterMonsterContextV111(
    long CombatId,
    PrototypeEncounterEnemySpec Enemy,
    RngStreamState Rng)
{
    public NativeEncounterMonsterContextV111 Fork() =>
        this with { Enemy = Enemy with { }, Rng = Rng.Fork() };
}

/// <summary>
/// A complete hand-off from encounter formation to combat creation. The
/// encounter RNG is retained *after* generating the formation so encounter
/// hooks can continue its sequence. No run, player, prototype combat, or
/// monster-specific stream has been consumed by formation.
/// </summary>
public sealed record NativeEncounterFormationV111(
    string NativeEncounterEntry,
    int TotalFloor,
    PrototypeEncounterEnemySpec[] Enemies,
    RngStreamState EncounterRng,
    NativeEncounterMonsterContextV111[] MonsterContexts)
{
    public NativeEncounterFormationV111 Fork() => this with
    {
        Enemies = Enemies.Select(enemy => enemy with { }).ToArray(),
        EncounterRng = EncounterRng.Fork(),
        MonsterContexts = MonsterContexts.Select(context => context.Fork()).ToArray()
    };
}

/// <summary>
/// Opt-in native RNG boundary for source-shaped encounter formations.
/// Unlike the legacy ResolveEnemySpecs(RngBundle) path, this consumes
/// EncounterModel's independent RNG seeded from run seed, total floor and
/// the exact native EncounterModel.Id.Entry. Generates untouched initial
/// monster-local streams from their real combat ids. Monster HP, per-monster
/// AI draws, and exact source-specific GenerateMonsters call order are NOT
/// claimed or simulated here.
/// </summary>
public static class NativeEncounterFormationFactoryV111
{
    public static NativeEncounterFormationV111 Generate(
        string seed,
        PrototypeEncounterDefinition encounter,
        string nativeEncounterEntry,
        int totalFloor,
        int currentActIndex,
        long firstMonsterCombatId,
        int? mapColumn = null,
        int? mapRow = null)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeEncounterEntry);
        if (currentActIndex is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(currentActIndex));
        if (firstMonsterCombatId < 0)
            throw new ArgumentOutOfRangeException(nameof(firstMonsterCombatId));
        if (mapColumn is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(mapColumn));
        if (mapRow is < 0)
            throw new ArgumentOutOfRangeException(nameof(mapRow));

        // The native encounter RNG is not part of the persistent 12 run
        // streams, and is not the native run.monster_ai stream either.
        var ownedStream = NativeRngV111Contexts.ForEncounterFormation(
            seed, nativeEncounterEntry, totalFloor);
        var ownedBundle = new RngBundle([ownedStream]);
        var formation = encounter.ResolveEnemySpecs(maxExclusive =>
            NativeRngV111.NextInt(
                ownedBundle, ownedStream.StreamId, maxExclusive));

        if (formation.Length > long.MaxValue - firstMonsterCombatId)
            throw new ArgumentOutOfRangeException(nameof(firstMonsterCombatId));

        var monsters = formation.Select((enemy, index) =>
        {
            var combatId = firstMonsterCombatId + index;
            return new NativeEncounterMonsterContextV111(
                combatId,
                enemy with { },
                NativeRngV111Contexts.ForMonster(
                    seed, currentActIndex, combatId, mapColumn, mapRow));
        }).ToArray();

        return new NativeEncounterFormationV111(
            nativeEncounterEntry, totalFloor,
            formation, ownedBundle.Streams[0].Fork(), monsters);
    }
}
