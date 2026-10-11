using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeCreatureCreationV111Tests
{
    private static CombatState Fixture(string seed) =>
        PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", totalFloor: 20,
            mapColumn: 3, mapRow: 5, commitInitialIntents: false);

    private static NativeRngSnapshot Cursor(
        CombatState combat, string name) =>
        NativeRngV111.ToSnapshot(combat.NativeRng!.RunStreams
            .Streams.Single(stream => stream.StreamId == name));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(73)]
    public void NativeSummonsAdvanceNicheAndAllocateNewIndependentMonsterContexts(
        int seedIndex)
    {
        var seed = "native-creature-spawn-" + seedIndex;
        var combat = Fixture(seed);
        var original = CanonicalJson.Sha256(combat);
        var beforeNiche = Cursor(combat, "run.niche");
        var initialAi = Cursor(combat, "run.monster_ai");
        var existing = combat.Enemies;
        var id = existing.Max(enemy => enemy.InstanceId) + 1;
        var spawned = new EnemyCombatState(
            InstanceId: id,
            EnemyId: "proto.enemy.gas_bomb",
            Hp: 1, Block: 0, MoveIndex: 0,
            Statuses: new Dictionary<string, int>(),
            FormationPosition: 10, SlotName: "bomb1");
        var first = NativeCombatRngEntryV111.AppendSummonedEnemy(
            combat, existing, spawned);
        Assert.Equal(original, CanonicalJson.Sha256(combat));
        Assert.Equal(beforeNiche.Counter + 1,
            Cursor(first, "run.niche").Counter);
        Assert.Equal(initialAi, Cursor(first, "run.monster_ai"));
        Assert.Equal(existing.Length + 1, first.Enemies.Length);
        var created = first.Enemies[^1];
        var range = PrototypeContent.Enemy(created.EnemyId)
            .HpRangeAt(combat.Act, combat.Ascension);
        Assert.InRange(created.Hp, range.Min, range.Max);
        Assert.Equal(created.Hp, created.MaxHp);
        Assert.Equal(id, created.InstanceId);
        var local = first.NativeRng!.Formation.MonsterContexts[^1];
        Assert.Equal(id, local.CombatId);
        Assert.Equal(
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForMonster(
                seed, 1, id, 3, 5)),
            NativeRngV111.ToSnapshot(local.Rng));
        Assert.Equal(created.EnemyId, local.Enemy.EnemyId);
        Assert.Equal(10, local.Enemy.FormationPosition);
        Assert.Equal("bomb1", local.Enemy.SlotName);
        NativeCombatRngEntryV111.Validate(first);

        var second = NativeCombatRngEntryV111.AppendSummonedEnemy(
            first, first.Enemies,
            spawned with
            {
                InstanceId = id + 1, SlotName = "bomb2",
                FormationPosition = 11
            });
        Assert.Equal(beforeNiche.Counter + 2,
            Cursor(second, "run.niche").Counter);
        Assert.Equal(existing.Length + 2, second.Enemies.Length);
        Assert.Equal(first.NativeRng.Formation.MonsterContexts.Length + 1,
            second.NativeRng!.Formation.MonsterContexts.Length);
        Assert.Equal(CanonicalJson.Sha256(first),
            CanonicalJson.Sha256(first.Fork()));
        Assert.Equal(CanonicalJson.Sha256(second),
            CanonicalJson.Sha256(second.Fork()));
        NativeCombatRngEntryV111.Validate(second);
        Assert.Equal(original, CanonicalJson.Sha256(combat));
    }

    [Fact]
    public void NativeSpawnRejectsReusedIdAndDoesNotTouchInputs()
    {
        var combat = Fixture("native-spawn-invalid");
        var original = CanonicalJson.Sha256(combat);
        var spawned = new EnemyCombatState(
            combat.Enemies[0].InstanceId,
            "proto.enemy.gas_bomb", 1, 0, 0,
            new Dictionary<string, int>(),
            FormationPosition: 10, SlotName: "bomb1");
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.AppendSummonedEnemy(
                combat, combat.Enemies, spawned));
        Assert.Equal(original, CanonicalJson.Sha256(combat));
    }

    [Fact]
    public void SpawnedNativeMonsterRngAdvancesWithoutAdvancingRunAi()
    {
        var combat = Fixture("native-spawn-local-isolation");
        var id = combat.Enemies.Max(enemy => enemy.InstanceId) + 1;
        var added = NativeCombatRngEntryV111.AppendSummonedEnemy(
            combat, combat.Enemies, new EnemyCombatState(
                id, "proto.enemy.gas_bomb", 1, 0, 0,
                new Dictionary<string, int>(),
                FormationPosition: 10, SlotName: "bomb1"));
        var niche = Cursor(added, "run.niche");
        var ai = Cursor(added, "run.monster_ai");
        var sampled = NativeCombatRngEntryV111.AdvanceMonsterLocal(
            added, id, 7, out var value);
        Assert.InRange(value, 0, 6);
        Assert.Equal(niche, Cursor(sampled, "run.niche"));
        Assert.Equal(ai, Cursor(sampled, "run.monster_ai"));
        Assert.Equal(1, NativeRngV111.ToSnapshot(
            sampled.NativeRng!.Formation.MonsterContexts[^1].Rng).Counter);
        Assert.Equal(0, NativeRngV111.ToSnapshot(
            added.NativeRng!.Formation.MonsterContexts[^1].Rng).Counter);
        NativeCombatRngEntryV111.Validate(sampled);
    }
}
