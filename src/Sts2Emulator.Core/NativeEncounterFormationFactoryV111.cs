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
    NativeEncounterMonsterContextV111[] MonsterContexts,
    // Encounter-specific initial AI state rotations are sampled during
    // GenerateMonsters(), not by the combat's run.monster_ai stream.
    int? EncounterOpeningOffset = null,
    // Immutable provenance for native mid-combat creature creation.
    // Older serialized fixtures can omit these nullable fields.
    string? RunSeed = null,
    int? NativeActIndex = null,
    int? MapColumn = null,
    int? MapRow = null)
{
    private PrototypeEncounterEnemySpec[] _enemies = Clone(Enemies);
    private NativeEncounterMonsterContextV111[] _monsterContexts =
        CloneContexts(MonsterContexts);

    public PrototypeEncounterEnemySpec[] Enemies
    {
        get => Clone(_enemies);
        init => _enemies = Clone(value);
    }

    public NativeEncounterMonsterContextV111[] MonsterContexts
    {
        get => CloneContexts(_monsterContexts);
        init => _monsterContexts = CloneContexts(value);
    }

    public NativeEncounterFormationV111 Fork() => this with
    {
        Enemies = _enemies,
        EncounterRng = EncounterRng.Fork(),
        MonsterContexts = _monsterContexts
    };

    private static PrototypeEncounterEnemySpec[] Clone(
        PrototypeEncounterEnemySpec[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (PrototypeEncounterEnemySpec[])values.Clone();
    }

    private static NativeEncounterMonsterContextV111[] CloneContexts(
        NativeEncounterMonsterContextV111[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(context => context.Fork()).ToArray();
    }
}

/// <summary>Formation output plus the typed native RNG requests made while
/// selecting its source-defined enemy indices.</summary>
public sealed record NativeEncounterFormationAuditV111(
    NativeEncounterFormationV111 Formation,
    RandomIndexDecisionV111[] Choices, string ProviderMode)
{
    private RandomIndexDecisionV111[] _choices = CloneChoices(Choices);

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneChoices(_choices);
        init => _choices = CloneChoices(value);
    }

    private static RandomIndexDecisionV111[] CloneChoices(
        RandomIndexDecisionV111[] choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return choices.Select(choice => choice with
        {
            Request = choice.Request with
            {
                OrderedCandidates =
                    (int[])choice.Request.OrderedCandidates.Clone()
            }
        }).ToArray();
    }
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
    public const string FormationSiteId =
        "combat.encounter.formation.index_draw";

    public static NativeEncounterFormationV111 Generate(
        string seed,
        PrototypeEncounterDefinition encounter,
        string nativeEncounterEntry,
        int totalFloor,
        int currentActIndex,
        long firstMonsterCombatId,
        int? mapColumn = null,
        int? mapRow = null,
        Func<Func<int, int>,
            (PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)>?
            sourceResolver = null) =>
        GenerateWithAudit(seed, encounter, nativeEncounterEntry,
            totalFloor, currentActIndex, firstMonsterCombatId,
            mapColumn, mapRow, sourceResolver).Formation;

    public static NativeEncounterFormationAuditV111 GenerateWithAudit(
        string seed,
        PrototypeEncounterDefinition encounter,
        string nativeEncounterEntry,
        int totalFloor,
        int currentActIndex,
        long firstMonsterCombatId,
        int? mapColumn = null,
        int? mapRow = null,
        Func<Func<int, int>,
            (PrototypeEncounterEnemySpec[] Enemies, int? OpeningOffset)>?
            sourceResolver = null)
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
        var provider = new NativeRandomIndexProviderV111();
        var choices = new List<RandomIndexDecisionV111>();
        var ordinal = 0;
        Func<int, int> nextIndex = maxExclusive =>
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var request = new RandomIndexRequestV111(
                FormationSiteId, ownedStream.StreamId,
                $"encounter:{nativeEncounterEntry}:floor:{totalFloor}",
                ordinal, Enumerable.Range(0, maxExclusive).ToArray());
            var decision = StochasticProviderScopeV111.ResolveIndex(
                provider, request, ownedBundle);
            choices.Add(decision);
            ordinal++;
            return decision.SelectedIndex;
        };
        var (formation, openingOffset) = sourceResolver is null
            ? (encounter.ResolveEnemySpecs(nextIndex), (int?)null)
            : sourceResolver(nextIndex);
        if (formation is null || openingOffset is < 0)
            throw new InvalidOperationException(
                "Native encounter source resolver returned an invalid formation or opener.");

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

        var result = new NativeEncounterFormationV111(
            nativeEncounterEntry, totalFloor,
            formation, ownedBundle.Streams[0].Fork(), monsters,
            openingOffset, seed, currentActIndex, mapColumn, mapRow);
        return new NativeEncounterFormationAuditV111(
            result, choices.ToArray(), provider.Mode);
    }
}
