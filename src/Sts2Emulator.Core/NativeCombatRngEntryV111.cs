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
public static class NativeCombatRngEntryV111
{
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
        RngBundle nativeRunStreams)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(nativeRunStreams);
        if (combat.NativeRng is not null)
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
        _ = NativeRngV111.ToSnapshot(fresh.RunStreams.Streams.Single(
            stream => stream.StreamId == "run.niche"));
        _ = NativeRngV111.ToSnapshot(fresh.RunStreams.Streams.Single(
            stream => stream.StreamId == "run.monster_ai"));
        _ = NativeRngV111.ToSnapshot(fresh.Formation.EncounterRng);

        var enemies = combat.Enemies.Select(enemy => enemy.Fork()).ToArray();
        var usedHp = new HashSet<int>();
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
            var range = PrototypeContent.Enemy(enemy.EnemyId)
                .HpRangeAt(combat.Act, combat.Ascension);
            var possible = Enumerable.Range(
                    range.Min, checked(range.Max - range.Min + 1))
                .Where(hp => !usedHp.Contains(hp))
                .ToArray();
            var pool = possible.Length > 0
                ? possible
                : Enumerable.Range(
                    range.Min, checked(range.Max - range.Min + 1)).ToArray();
            // The native NextItem also consumes one draw for a singleton.
            var roll = NativeRngV111.NextInt(
                fresh.RunStreams, "run.niche", pool.Length);
            var hp = pool[roll];
            enemies[index] = enemy with { Hp = hp, MaxHp = hp };
            usedHp.Add(hp);
        }

        return combat with { Enemies = enemies, NativeRng = fresh };
    }

    /// <summary>
    /// Validate the separate ownership hierarchy and combat-formation
    /// binding. This can be called by the whole-run state invariant checker
    /// after deserializing a native combat snapshot.
    /// </summary>
    public static void Validate(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var native = combat.NativeRng;
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
        _ = NativeRngV111.ToSnapshot(formation.EncounterRng);
        if (formation.MonsterContexts.Select(context => context.CombatId)
                .Distinct().Count() != formation.MonsterContexts.Length)
            throw new InvalidOperationException(
                "Native monster combat ids must be unique.");

        for (var i = 0; i < combat.Enemies.Length; i++)
        {
            var spec = formation.Enemies[i];
            var context = formation.MonsterContexts[i];
            var enemy = combat.Enemies[i];
            if (context.CombatId < 0 || context.Enemy != spec
                || enemy.EnemyId != spec.EnemyId
                || enemy.FormationPosition != spec.FormationPosition
                || enemy.SlotName != spec.SlotName)
                throw new InvalidOperationException(
                    "Native monster RNG context does not match combat formation.");
            _ = NativeRngV111.ToSnapshot(context.Rng);
        }
    }

    /// <summary>
    /// Commit source-shaped enemy intents using run.monster_ai, returning
    /// a forked combat. This API deliberately does not execute the entire
    /// game turn, which still contains unmigrated RNG sources.
    /// </summary>
    public static CombatState CommitIntents(CombatState combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        if (combat.NativeRng is null)
            throw new InvalidOperationException(
                "Only a native-RNG combat can use this isolated intent API.");
        return PrototypeGameEngine.CommitEnemyIntents(
            combat, RngBundle.Empty);
    }

    /// <summary>
    /// Audit/restore a separate monster-local stream without consuming the
    /// combat's run.monster_ai stream or the encounter formation cursor.
    /// Work is performed on a deep fork, never on the input combat.
    /// </summary>
    public static CombatState AdvanceMonsterLocal(
        CombatState combat, long combatId, int maxExclusive,
        out int sampled)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var native = combat.NativeRng?.Fork() ??
            throw new InvalidOperationException(
                "Combat has no native RNG owner.");
        var index = Array.FindIndex(native.Formation.MonsterContexts,
            context => context.CombatId == combatId);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(combatId),
                "Combat id has no native monster RNG context.");
        var owned = native.Formation.MonsterContexts[index];
        var bundle = new RngBundle([owned.Rng.Fork()]);
        sampled = NativeRngV111.NextInt(
            bundle, owned.Rng.StreamId, maxExclusive);
        native.Formation.MonsterContexts[index] = owned with
        {
            Rng = bundle.Streams[0]
        };
        return combat with { NativeRng = native };
    }
}
