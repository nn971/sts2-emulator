namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in, snapshot-owned native v0.111 combat random state.
/// Run-owned streams are copied for isolation; encounter and creature-local
/// streams are independently owned in Formation. All mutable byte arrays
/// are deep-forked. The default six-stream prototype engine is unaffected.
/// </summary>
public sealed record NativeCombatRngV111(
    RngBundle RunStreams,
    NativeEncounterFormationV111 Formation)
{
    public NativeCombatRngV111 Fork() => this with
    {
        RunStreams = RunStreams.Fork(),
        Formation = Formation.Fork()
    };
}

/// <summary>
/// Starts an isolated native-RNG boundary on an existing typed combat state.
/// This is deliberately opt-in: it does not change the default prototype
/// room-to-combat transition, select an encounter or claim full native parity.
/// Native Creature.SetUniqueMonsterHpValue uses run.niche, whereas native
/// MonsterModel.RollMove uses run.monster_ai; these are different from the
/// encounter's local formation RNG and each MonsterModel's own Rng.
/// </summary>
public sealed record NativeInitialHpAuditV111(
    CombatState Combat, RandomIndexDecisionV111[] Choices, string ProviderMode)
{
    private CombatState? _combat = Combat.Fork();
    private RandomIndexDecisionV111[]? _choices = CloneDecisions(Choices);

    public CombatState Combat
    {
        get => (_combat ?? throw new InvalidDataException(
            "Native initial-HP audit has no combat state.")).Fork();
        init => _combat = value.Fork();
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public NativeInitialHpAuditV111 Fork() => this with
    {
        Combat = _combat ?? throw new InvalidDataException(
            "Native initial-HP audit has no combat state."),
        Choices = _choices!
    };

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();
}

/// <summary>One Native initial-HP ticket paired with the enemy identity and
/// HP value committed at the source-route player boundary.</summary>
public sealed record NativeInitialHpOutcomeV111(
    long CombatId, string EnemyId, int[] OrderedHpSupport, int SelectedHp,
    RandomIndexDecisionV111 ProviderDecision)
{
    private int[]? _orderedHpSupport = Copy(OrderedHpSupport);
    private RandomIndexDecisionV111? _providerDecision =
        CloneDecision(ProviderDecision);

    public int[] OrderedHpSupport
    {
        get => Copy(_orderedHpSupport)!;
        init => _orderedHpSupport = Copy(value);
    }

    public RandomIndexDecisionV111 ProviderDecision
    {
        get => CloneDecision(_providerDecision)!;
        init => _providerDecision = CloneDecision(value);
    }

    public NativeInitialHpOutcomeV111 Fork() => this with
    {
        OrderedHpSupport = _orderedHpSupport!,
        ProviderDecision = _providerDecision!
    };

    public void Validate()
    {
        var support = _orderedHpSupport;
        var decision = _providerDecision;
        if (CombatId < 0 || string.IsNullOrWhiteSpace(EnemyId) ||
            support is null || support.Length == 0 ||
            support.Any(hp => hp <= 0) ||
            support.Distinct().Count() != support.Length ||
            decision is null)
            throw new InvalidDataException(
                "Native initial-HP outcome has invalid identity or support.");

        decision.Validate();
        var request = decision.Request;
        if (request.SiteId != NativeCombatRngEntryV111.InitialUniqueHpSiteId ||
            request.StreamId != "run.niche" ||
            request.OwnerId != "creature:" + CombatId ||
            !request.OrderedCandidates.SequenceEqual(support) ||
            NativeSemanticOutcomeV111.SelectByChooseIndex(
                decision, support) != SelectedHp)
            throw new InvalidDataException(
                "Native initial-HP result disagrees with its provider ticket.");
    }

    private static int[]? Copy(int[]? values) =>
        values is null ? null : (int[])values.Clone();

    private static RandomIndexDecisionV111? CloneDecision(
        RandomIndexDecisionV111? value) => value is null ? null : value with
    {
        Request = value.Request with
        {
            OrderedCandidates = value.Request.OrderedCandidates
        }
    };
}

public sealed record NativeIntentAuditV111(
    CombatState Combat, RandomIndexDecisionV111[] Choices, string ProviderMode,
    SemanticIntentAuditEntryV111[] WeightedOutcomes)
{
    public NativeIntentAuditV111(
        CombatState Combat, RandomIndexDecisionV111[] Choices,
        string ProviderMode)
        : this(Combat, Choices, ProviderMode,
            Array.Empty<SemanticIntentAuditEntryV111>())
    {
    }

    private CombatState? _combat = Combat.Fork();
    private RandomIndexDecisionV111[]? _choices = CloneDecisions(Choices);
    private SemanticIntentAuditEntryV111[]? _weightedOutcomes =
        CloneOutcomes(WeightedOutcomes);

    public CombatState Combat
    {
        get => (_combat ?? throw new InvalidDataException(
            "Native intent audit has no combat state.")).Fork();
        init => _combat = value.Fork();
    }

    public RandomIndexDecisionV111[] Choices
    {
        get => CloneDecisions(_choices)!;
        init => _choices = CloneDecisions(value);
    }

    public SemanticIntentAuditEntryV111[] WeightedOutcomes
    {
        get => CloneOutcomes(_weightedOutcomes)!;
        init => _weightedOutcomes = CloneOutcomes(value);
    }

    public NativeIntentAuditV111 Fork() => this with
    {
        Combat = _combat ?? throw new InvalidDataException(
            "Native intent audit has no combat state."),
        Choices = _choices!,
        WeightedOutcomes = _weightedOutcomes!
    };

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();

    private static SemanticIntentAuditEntryV111[]? CloneOutcomes(
        SemanticIntentAuditEntryV111[]? values) => values?.Select(outcome =>
            outcome is null ? null! : outcome.Fork()).ToArray();
}

public static class NativeCombatRngEntryV111
{
    public const string DiagnosticMonsterLocalSiteId =
        "diagnostic.monster_local.index_sample";
    public const string InitialUniqueHpSiteId =
        "combat.initial_unique_hp";
    public const string CommittedIntentSiteId =
        "combat.committed_intent.index_draw";
    public const string NextTurnIntentSiteId =
        "combat.next_turn_intent.index_draw";
    public const string ToughEggHatchHpSiteId =
        "combat.tough_egg.hatchling_hp";

    /// <summary>
    /// Attach a native RNG sidecar, sampling initial HP in creature creation
    /// order from run.niche. All run stream cursors are preserved after HP
    /// draws; both independent encounter/monster cursors remain untouched.
    ///
    /// Pinned Creature.SetUniqueMonsterHpValue constructs the full inclusive
    /// HP range, excludes all earlier monster maxima, and samples from the
    /// remaining ordered set; if exhausted, it falls back to the full range.
    /// Even a singleton range makes one NextInt draw in the native code.
    /// This emulates single-player non-mutated baseline HP; encounter-specific
    /// AfterAddedToRoom HP hooks and later scaling are separate boundaries.
    /// </summary>
    public static CombatState AttachAndRollInitialHp(
        CombatState combat,
        NativeEncounterFormationV111 formation,
        RngBundle nativeRunStreams) =>
        AttachAndRollInitialHpWithAudit(combat, formation, nativeRunStreams).Combat;

    public static NativeInitialHpAuditV111 AttachAndRollInitialHpWithAudit(
        CombatState combat,
        NativeEncounterFormationV111 formation,
        RngBundle nativeRunStreams)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(nativeRunStreams);
        if (combat.Stochastic.NativeRng is not null)
            throw new InvalidOperationException(
                "Native RNG ownership is already attached to this combat.");

        var monsters = formation.MonsterContexts;
        if (combat.Enemies.Length != formation.Enemies.Length
            || monsters.Length != formation.Enemies.Length)
            throw new InvalidOperationException(
                "Native combat RNG ownership requires one context per combat enemy.");

        var fresh = new NativeCombatRngV111(
            nativeRunStreams.Fork(), formation.Fork());
        // Validate these streams before any RNG draw.
        _ = NativeRngV111.ToSnapshot(
            fresh.RunStreams.GetStream("run.niche"));
        _ = NativeRngV111.ToSnapshot(
            fresh.RunStreams.GetStream("run.monster_ai"));
        _ = NativeRngV111.ToSnapshot(fresh.Formation.EncounterRng);

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var usedHp = new HashSet<int>();
        var chooser = new NativeRandomIndexProviderV111();
        var decisions = new List<RandomIndexDecisionV111>(enemies.Length);
        for (var index = 0; index < enemies.Length; index++)
        {
            var enemy = enemies[index];
            var context = monsters[index];
            var spec = formation.Enemies[index];

            if (enemy.EnemyId != spec.EnemyId
                || enemy.FormationPosition != spec.FormationPosition
                || enemy.SlotName != spec.SlotName
                || context.Enemy != spec
                || context.CombatId < 0)
            {
                throw new InvalidOperationException(
                    "Native combat RNG and existing enemy formation disagree.");
            }
            _ = NativeRngV111.ToSnapshot(context.Rng);
            var pool = InitialHpPool(combat, enemy, usedHp);
            // Native RNG must consume even singleton pools.
            var choice = StochasticProviderScopeV111.ResolveIndex(
                chooser, new RandomIndexRequestV111(
                    InitialUniqueHpSiteId, "run.niche",
                    "creature:" + context.CombatId, index, pool),
                fresh.RunStreams);
            decisions.Add(choice);
            var hp = choice.SelectedValue;
            enemies[index] = enemy with { Hp = hp, MaxHp = hp };
            usedHp.Add(hp);
        }

        var attached = combat with
        {
            Enemies = enemies,
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic, fresh)
        };
        Validate(attached);
        return new NativeInitialHpAuditV111(
            attached, decisions.ToArray(), chooser.Mode);
    }

    /// <summary>Shared source-shaped eligibility for native and controlled HP.</summary>
    internal static int[] InitialHpPool(
        CombatState combat, EnemyCombatState enemy, HashSet<int> usedHp)
    {
        var range = PrototypeContent.Enemy(enemy.EnemyId)
            .HpRangeAt(combat.Act, combat.Ascension);
        var all = Enumerable.Range(
            range.Min, checked(range.Max - range.Min + 1)).ToArray();
        var available = all.Where(hp => !usedHp.Contains(hp)).ToArray();
        return available.Length > 0 ? available : all;
    }

    /// <summary>Source-ordered HP support for ToughEgg.Hatch.</summary>
    internal static int[] ToughEggHatchHpSupport(int ascension)
    {
        if (ascension < 0)
            throw new ArgumentOutOfRangeException(nameof(ascension));
        var minHp = ascension >= 8 ? 20 : 19;
        return Enumerable.Range(minHp, 4).ToArray();
    }

    /// <summary>
    /// Validate the separate ownership hierarchy and combat-formation
    /// binding. This can be called by the whole-run state invariant checker
    /// after deserializing a native combat snapshot.
    /// </summary>
    public static void Validate(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        Validate(combat, combat.Stochastic.NativeRng);
    }

    internal static void Validate(
        CombatState combat, NativeCombatRngV111? native)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (native is null)
            return;
        var expected = NativeRngV111.CreateBundle("validation-schema")
            .Streams.Select(stream => stream.StreamId)
            .Order(StringComparer.Ordinal).ToArray();
        var actual = native.RunStreams.Streams
            .Select(stream => stream.StreamId)
            .Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Native combat RNG must own all 12 run and 3 player streams exactly once.");
        foreach (var stream in native.RunStreams.Streams)
            _ = NativeRngV111.ToSnapshot(stream);

        var formation = native.Formation;
        if (formation.TotalFloor < 0
            || string.IsNullOrWhiteSpace(formation.NativeEncounterEntry)
            || formation.Enemies.Length != combat.Enemies.Length
            || formation.MonsterContexts.Length != combat.Enemies.Length)
            throw new InvalidOperationException(
                "Native encounter RNG context has an invalid formation.");

        // Encounter and creature RNG stream names encode their original
        // owners. A valid xoshiro state from *another* encounter or
        // monster must never be accepted as the current owner's cursor.
        // Source provenance is optional only for older isolated fixtures.
        // When present, it must be complete and agree with the combat act.
        if ((formation.RunSeed is null) !=
                (formation.NativeActIndex is null)
            || formation.NativeActIndex is < 0 or > 2
            || (formation.NativeActIndex is { } actIndex
                && actIndex != combat.Act - 1)
            || formation.MapColumn is < 0 or > 6
            || formation.MapRow is < 0)
            throw new InvalidOperationException(
                "Native encounter creation provenance is invalid.");
        var expectedEncounterStreamId =
            $"context.encounter.{formation.NativeEncounterEntry}.floor_{formation.TotalFloor}";
        if (formation.EncounterRng.StreamId != expectedEncounterStreamId)
            throw new InvalidOperationException(
                "Native encounter RNG belongs to a different encounter or floor.");
        _ = NativeRngV111.ToSnapshot(formation.EncounterRng);
        if (formation.MonsterContexts.Select(context => context.CombatId)
                .Distinct().Count() != formation.MonsterContexts.Length)
            throw new InvalidOperationException(
                "Native monster combat ids must be unique.");
        var ownedMonsterStreamIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (var i = 0; i < combat.Enemies.Length; i++)
        {
            var spec = formation.Enemies[i];
            var context = formation.MonsterContexts[i];
            var enemy = combat.Enemies[i];
            // ToughEgg.Hatch mutates its original MonsterModel in
            // place. The prototype swaps the effective move proxy to
            // HatchlingId; the native formation/spec/context must keep
            // the original ToughEgg model and CombatId.
            var hatchedToughEgg =
                enemy.NativeToughEggHatched
                && formation.NativeEncounterEntry == "OvicopterNormal"
                && spec.EnemyId ==
                    PrototypeNativeHiveSummoningNormals.ToughEggId
                && enemy.EnemyId ==
                    PrototypeNativeHiveSummoningNormals.HatchlingId;
            if (context.CombatId < 0 || context.Enemy != spec
                || (!hatchedToughEgg && enemy.EnemyId != spec.EnemyId)
                || (enemy.NativeToughEggHatched && !hatchedToughEgg)
                || enemy.FormationPosition != spec.FormationPosition
                || enemy.SlotName != spec.SlotName)
                throw new InvalidOperationException(
                    "Native monster RNG context does not match combat formation.");
            if (!ownedMonsterStreamIds.Add(context.Rng.StreamId))
                throw new InvalidOperationException(
                    "Native monster-local RNG streams must have distinct owners.");
            if (formation.NativeActIndex is { } nativeActIndex
                && context.Rng.StreamId !=
                    $"context.monster.{context.CombatId}.act_{nativeActIndex}.map_{formation.MapColumn ?? 0}_{formation.MapRow ?? 0}")
                throw new InvalidOperationException(
                    "Native monster-local RNG belongs to a different creature or map.");
            _ = NativeRngV111.ToSnapshot(context.Rng);
        }
    }

    /// <summary>
    /// Source ToughEgg.Hatch is NOT a monster replacement. It uses
    /// RunRng.Niche.NextInt(HatchlingMinHp, HatchlingMaxHp + 1),
    /// sets current/max HP in-place and removes all powers except
    /// Minion. The existing monster-local RNG, creature CombatId,
    /// formation spec and model identity are preserved. HatchlingId
    /// remains the prototype's proxy for the post-hatch Nibble move.
    /// This source path is allowed ONLY in the explicit Ovicopter
    /// isolated native combat fixture; native Hive route execution
    /// remains gated.
    /// </summary>
    public static CombatState HatchToughEgg(
        CombatState combat, EnemyCombatState currentEgg,
        out EnemyCombatState hatched)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(currentEgg);
        var native = combat.Stochastic.NativeRng?.Fork() ??
            throw new InvalidOperationException(
                "Native Tough Egg hatching requires native combat ownership.");
        if (native.Formation.NativeEncounterEntry != "OvicopterNormal"
            || combat.Act != 2
            || currentEgg.EnemyId !=
                PrototypeNativeHiveSummoningNormals.ToughEggId
            || currentEgg.NativeToughEggHatched)
            throw new NotSupportedException(
                "Only a source-unhatched native Ovicopter Tough Egg can hatch.");
        Validate(combat);
        var index = Array.FindIndex(combat.Enemies, enemy =>
            enemy.InstanceId == currentEgg.InstanceId);
        if (index < 0
            || combat.Enemies[index].EnemyId != currentEgg.EnemyId
            || native.Formation.Enemies[index].EnemyId
                != PrototypeNativeHiveSummoningNormals.ToughEggId
            || native.Formation.MonsterContexts[index].Enemy
                != native.Formation.Enemies[index]
            || currentEgg.Hp <= 0)
            throw new InvalidOperationException(
                "Native Tough Egg must retain its original live combat identity.");

        // ToughEgg.Hatch direct NextInt is *not* the unique HP sampler:
        // no surrounding monster HP exclusions, exactly one niche draw.
        // Ascension 8+ uses [20,23] instead of [19,22].
        var hatchHpCandidates = ToughEggHatchHpSupport(combat.Ascension);
        var monsterContext = native.Formation.MonsterContexts[index];
        var hatchHpChoice = StochasticProviderScopeV111.ResolveIndex(
            new NativeRandomIndexProviderV111(), new RandomIndexRequestV111(
                ToughEggHatchHpSiteId, "run.niche",
                "creature:" + monsterContext.CombatId, 0,
                hatchHpCandidates), native.RunStreams);
        var hp = hatchHpChoice.SelectedValue;
        hatched = currentEgg with
        {
            EnemyId = PrototypeNativeHiveSummoningNormals.HatchlingId,
            NativeToughEggHatched = true,
            Hp = hp,
            MaxHp = hp,
            Powers = currentEgg.PowerStates.Where(power =>
                power.PowerId == "proto.power.minion").ToArray(),
            AiStateId = null
        };
        // C# out parameters cannot be captured by LINQ lambdas.
        var hatchedSnapshot = hatched.Fork();
        var updatedEnemies = combat.Enemies.Select((enemy, i) =>
            i == index ? hatchedSnapshot : enemy.Fork()).ToArray();
        var updated = combat with
        {
            Enemies = updatedEnemies,
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic, native)
        };
        Validate(updated);
        return updated;
    }

    /// <summary>
    /// Append a creature created by an already-audited native normal
    /// encounter effect. Native CombatState.CreateCreature rolls HP from
    /// run.niche, then assigns the next lifetime CombatId, and creates a
    /// per-creature RNG from the run seed, current map coordinate, act
    /// index and newly assigned id. The typed emulator retains defeated
    /// enemies, so their identities must never be recycled.
    ///
    /// This handles creation only. It intentionally does not implement
    /// source-specific summon eligibility, spawn animations, on-spawn
    /// power interactions or model transformations.
    /// </summary>
    public static CombatState AppendSummonedEnemy(
        CombatState combat,
        IReadOnlyList<EnemyCombatState> currentEnemies,
        EnemyCombatState spawned,
        int? precreatedUnattachedId = null,
        int? dyingStillAttachedId = null)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(currentEnemies);
        ArgumentNullException.ThrowIfNull(spawned);
        var nativeOwner = combat.Stochastic.NativeRng;
        if (nativeOwner is null)
            throw new InvalidOperationException(
                "Native creature creation requires native combat ownership.");

        var before = combat with
        {
            Enemies = currentEnemies.Select(enemy => enemy.Fork()).ToArray()
        };
        Validate(before);
        var native = nativeOwner.Fork();
        var formation = native.Formation;
        if (formation.RunSeed is null
            || formation.NativeActIndex is null
            || formation.MapColumn is null
            || formation.MapRow is null)
            throw new NotSupportedException(
                "Native summon requires pinned run seed and map-coordinate provenance.");
        if (precreatedUnattachedId is not null
            && (native.Formation.NativeEncounterEntry != "GremlinMercNormal"
                || spawned.EnemyId != "proto.enemy.sneaky_gremlin"
                || currentEnemies.Count(enemy =>
                    enemy.InstanceId == precreatedUnattachedId
                    && enemy.EnemyId == "proto.enemy.fat_gremlin") != 1))
            throw new NotSupportedException(
                "Native precreated-unattached HP exclusion is audited only for SurprisePower.");
        if (dyingStillAttachedId is not null
            && currentEnemies.Count(enemy =>
                enemy.InstanceId == dyingStillAttachedId
                && enemy.Hp <= 0) != 1)
            throw new InvalidOperationException(
                "Native AfterDeath HP pool must identify exactly one dying attached creature.");
        if (spawned.InstanceId <= 0
            || currentEnemies.Any(enemy =>
                enemy.InstanceId == spawned.InstanceId)
            || formation.MonsterContexts.Any(context =>
                context.CombatId >= spawned.InstanceId))
            throw new InvalidOperationException(
                "Native spawn must have a new monotonically increasing combat ID.");
        if (spawned.Hp <= 0)
            throw new InvalidOperationException(
                "Native spawn must create a living creature.");
        var range = PrototypeContent.Enemy(spawned.EnemyId)
            .HpRangeAt(combat.Act, combat.Ascension);
        // Creature.SetUniqueMonsterHpValue excludes maxima belonging
        // to *attached* enemy creatures. The prototype retains dead
        // monsters forever in its history; native combat removes them
        // after their death hooks. Include the dying source while its
        // AfterDeath hook is executing, but never stale historical dead
        // bodies. SurprisePower precreates Fat without attaching it
        // before Sneaky's HP roll, so that one model is excluded.
        var usedHp = currentEnemies
            .Where(enemy =>
                (enemy.Hp > 0
                    || enemy.InstanceId == dyingStillAttachedId)
                && enemy.InstanceId != precreatedUnattachedId)
            .Select(enemy => enemy.MaxHp).ToHashSet();
        var possible = Enumerable.Range(
                range.Min, checked(range.Max - range.Min + 1))
            .Where(hp => !usedHp.Contains(hp))
            .ToArray();
        var values = possible.Length > 0
            ? possible
            : Enumerable.Range(
                range.Min, checked(range.Max - range.Min + 1)).ToArray();
        // Even the fixed-range/singleton case advances run.niche.
        var spawnHpChoice = StochasticProviderScopeV111.ResolveIndex(
            new NativeRandomIndexProviderV111(), new RandomIndexRequestV111(
                InitialUniqueHpSiteId, "run.niche",
                "creature:" + spawned.InstanceId, 0, values),
            native.RunStreams);
        var hp = spawnHpChoice.SelectedValue;
        var updatedSpawn = spawned with { Hp = hp, MaxHp = hp };
        var spec = new PrototypeEncounterEnemySpec(
            updatedSpawn.EnemyId, updatedSpawn.FormationPosition,
            updatedSpawn.SlotName);
        var context = new NativeEncounterMonsterContextV111(
            updatedSpawn.InstanceId, spec,
            NativeRngV111Contexts.ForMonster(
                formation.RunSeed,
                formation.NativeActIndex.Value,
                updatedSpawn.InstanceId,
                formation.MapColumn, formation.MapRow));
        native = native with
        {
            Formation = formation with
            {
                Enemies = formation.Enemies.Append(spec).ToArray(),
                MonsterContexts =
                    formation.MonsterContexts.Append(context).ToArray()
            }
        };
        var result = combat with
        {
            Enemies = before.Enemies.Append(updatedSpawn).ToArray(),
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic, native)
        };
        Validate(result);
        return result;
    }

    /// <summary>
    /// Gremlin Merc's SurprisePower.AfterDeath calls CreateCreature on
    /// Fat Gremlin BEFORE CreatureCmd.Add of Sneaky Gremlin, then calls
    /// CreatureCmd.Add for the already-created Fat Gremlin. The native
    /// CombatIds/RNG seeds follow creation order while their combat turn
    /// order follows addition/slot order. Rearrange the three typed
    /// enemies and their attached native contexts as an atomic permutation
    /// without exchanging or re-seeding either native monster stream.
    /// </summary>
    public static CombatState SortGremlinMercDeathPair(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.Stochastic.NativeRng is not { } currentNative ||
            currentNative.Formation.NativeEncounterEntry
                != "GremlinMercNormal")
            throw new NotSupportedException(
                "Only native GremlinMercNormal's audited death pair can reorder.");
        Validate(combat);
        if (combat.Enemies.Length != 3
            || combat.Enemies.Count(enemy =>
                enemy.EnemyId == "proto.enemy.gremlin_merc") != 1
            || combat.Enemies.Count(enemy =>
                enemy.EnemyId == "proto.enemy.sneaky_gremlin") != 1
            || combat.Enemies.Count(enemy =>
                enemy.EnemyId == "proto.enemy.fat_gremlin") != 1
            || combat.Enemies[1].EnemyId != "proto.enemy.fat_gremlin"
            || combat.Enemies[2].EnemyId != "proto.enemy.sneaky_gremlin")
            throw new InvalidOperationException(
                "Gremlin Merc pair must be created Fat then Sneaky.");
        var native = currentNative.Fork();
        var permutation = Enumerable.Range(0, combat.Enemies.Length)
            .OrderBy(index => combat.Enemies[index].FormationPosition)
            .ToArray();
        var reordered = combat with
        {
            Enemies = permutation.Select(index =>
                combat.Enemies[index].Fork()).ToArray(),
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic, native with
            {
                Formation = native.Formation with
                {
                    Enemies = permutation.Select(index =>
                        native.Formation.Enemies[index] with { }).ToArray(),
                    MonsterContexts = permutation.Select(index =>
                        native.Formation.MonsterContexts[index].Fork()).ToArray()
                }
            })
        };
        Validate(reordered);
        if (reordered.Enemies[1].EnemyId
                != "proto.enemy.sneaky_gremlin"
            || reordered.Enemies[2].EnemyId
                != "proto.enemy.fat_gremlin"
            || reordered.Stochastic.NativeRng!.Formation.MonsterContexts[1]
                .CombatId
                != currentNative.Formation.MonsterContexts[2].CombatId)
            throw new InvalidOperationException(
                "Gremlin Merc native addition order was not preserved.");
        return reordered;
    }

    /// <summary>
    /// Commit source-shaped enemy intents using run.monster_ai, returning
    /// a forked combat. This API deliberately does not execute the entire
    /// game turn, which still contains unmigrated RNG sources.
    /// </summary>
    public static CombatState CommitIntents(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.Stochastic.NativeRng is null)
            throw new InvalidOperationException(
                "Only a native-RNG combat can use this isolated intent API.");
        return PrototypeGameEngine.CommitEnemyIntents(
            combat, RngBundle.Empty, CommittedIntentSiteId);
    }

    /// <summary>
    /// Commit the supported initial or next-turn intent site and retain both
    /// primitive provider decisions and their weighted semantic outcomes.
    /// </summary>
    public static NativeIntentAuditV111 CommitIntentsWithAudit(
        CombatState combat,
        string siteId = CommittedIntentSiteId)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.Stochastic.NativeRng is null)
            throw new InvalidOperationException("Native intent audit requires attached native RNG.");
        var result = PrototypeGameEngine.CommitEnemyIntentsWithAudit(
            combat, RngBundle.Empty, siteId);
        return new NativeIntentAuditV111(
            result.Combat, result.Choices, "native-v111",
            result.WeightedOutcomes);
    }

    /// <summary>
    /// Audit/restore a separate monster-local stream without consuming the
    /// combat's run.monster_ai stream or the encounter formation cursor.
    /// Work is performed on a deep fork, never on the input combat.
    /// </summary>
    public static CombatState AdvanceMonsterLocal(
        CombatState combat, long combatId, int maxExclusive,
        out int sampled) =>
        AdvanceMonsterLocal(
            combat,
            combatId,
            DiagnosticMonsterLocalSiteId,
            "creature:" + combatId,
            maxExclusive,
            ordinal: 0,
            out sampled);

    /// <summary>
    /// Resolve one audited bounded operation on a creature-local stream and
    /// persist the advanced cursor on that creature's native CombatId owner.
    /// </summary>
    public static CombatState AdvanceMonsterLocal(
        CombatState combat, long combatId, string siteId, string ownerId,
        int maxExclusive, int ordinal, out int sampled)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (siteId is not (DiagnosticMonsterLocalSiteId or
            PrototypeNativePunchOff.ConstructHpOffsetSiteId))
            throw new NotSupportedException(
                $"UNSUPPORTED_RANDOM_SITE site={siteId} " +
                "operation=monster_local_advance.");
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        Validate(combat);
        var native = combat.Stochastic.NativeRng?.Fork() ??
            throw new InvalidOperationException(
                "Combat has no native RNG owner.");
        var index = Array.FindIndex(native.Formation.MonsterContexts,
            context => context.CombatId == combatId);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(combatId),
                "Combat id has no native monster RNG context.");
        var owned = native.Formation.MonsterContexts[index];
        var bundle = new RngBundle([owned.Rng.Fork()]);
        var decision = StochasticProviderScopeV111.ResolveIndex(
            new NativeRandomIndexProviderV111(),
            new RandomIndexRequestV111(
                siteId,
                owned.Rng.StreamId,
                ownerId,
                ordinal,
                Array.Empty<int>(),
                OperationKind: "bounded_integer",
                BoundedCount: maxExclusive),
            bundle);
        sampled = decision.SelectedValue;
        var formation = native.Formation;
        var contexts = formation.MonsterContexts;
        contexts[index] = owned with
        {
            Rng = bundle.GetStream(owned.Rng.StreamId)
        };
        native = native with
        {
            Formation = formation with { MonsterContexts = contexts }
        };
        var advanced = combat with
        {
            Stochastic = CombatStochasticStateV111.WithNativeRng(
                combat.Stochastic, native)
        };
        Validate(advanced);
        return advanced;
    }
}
