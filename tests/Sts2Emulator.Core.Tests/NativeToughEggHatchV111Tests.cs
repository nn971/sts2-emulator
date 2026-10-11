using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeToughEggHatchV111Tests
{
    private const string Ovicopter = PrototypeNativeHiveSummoningNormals.OvicopterNormalId;
    private const string Egg = PrototypeNativeHiveSummoningNormals.ToughEggId;
    private const string Hatchling = PrototypeNativeHiveSummoningNormals.HatchlingId;

    private static CombatState WithSpawnedEgg(string seed, int ascension)
    {
        var combat = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, Ovicopter, "OvicopterNormal",
            totalFloor: 20, ascension: ascension,
            mapColumn: 2, mapRow: 5,
            commitInitialIntents: false);
        Assert.Single(combat.Enemies);
        var instanceId = combat.Enemies[0].InstanceId + 1;
        return NativeCombatRngEntryV111.AppendSummonedEnemy(
            combat, combat.Enemies, new EnemyCombatState(
                InstanceId: instanceId,
                EnemyId: Egg,
                Hp: 1, Block: 0, MoveIndex: 0,
                Statuses: new Dictionary<string, int>(),
                Powers:
                [
                    new PrototypePowerInstanceState("proto.power.minion", 1, 1),
                    new PrototypePowerInstanceState(
                        PrototypeNativeHiveSummoningNormals.HatchPowerId, 2, 2)
                ],
                FormationPosition: 1,
                SlotName: "egg1"));
    }

    private static NativeRngSnapshot Cursor(CombatState combat, string name) =>
        NativeRngV111.ToSnapshot(combat.NativeRng!.RunStreams.Streams.Single(
            stream => stream.StreamId == name));

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    public void NativeHatchPreservesOriginalCreatureAndConsumesExactlyOneNicheDraw(
        int ascension)
    {
        for (var trial = 0; trial < 64; trial++)
        {
            var combat = WithSpawnedEgg(
                "tough-egg-inplace-" + ascension + "-" + trial, ascension);
            var egg = combat.Enemies[^1];
            var eggRng = NativeRngV111.ToSnapshot(
                combat.NativeRng!.Formation.MonsterContexts[^1].Rng);
            var nicheBefore = Cursor(combat, "run.niche");
            var aiBefore = Cursor(combat, "run.monster_ai");
            var formationBefore = CanonicalJson.Sha256(
                combat.NativeRng.Formation);
            var originalHash = CanonicalJson.Sha256(combat);

            var enrichedEgg = egg with
            {
                Block = 7,
                Statuses = new Dictionary<string, int>
                {
                    ["post_hatch_marker"] = 2
                },
                Powers = egg.PowerStates.Append(
                    new PrototypePowerInstanceState(
                        "proto.power.strength", 4, 3)).ToArray()
            };
            var enriched = combat with
            {
                Enemies = combat.Enemies
                    .Take(combat.Enemies.Length - 1)
                    .Append(enrichedEgg).ToArray()
            };
            var oracle = enriched.NativeRng!.RunStreams.Fork();
            var min = ascension >= 8 ? 20 : 19;
            var expectedHp = min + NativeRngV111.NextInt(
                oracle, "run.niche", 4);

            var after = NativeCombatRngEntryV111.HatchToughEgg(
                enriched, enrichedEgg, out var hatched);
            Assert.Equal(originalHash, CanonicalJson.Sha256(combat));
            Assert.Equal(expectedHp, hatched.Hp);
            Assert.Equal(expectedHp, hatched.MaxHp);
            Assert.Equal(egg.InstanceId, hatched.InstanceId);
            Assert.Equal(egg.SlotName, hatched.SlotName);
            Assert.Equal(egg.FormationPosition, hatched.FormationPosition);
            Assert.Equal(7, hatched.Block);
            Assert.Equal(2, hatched.Statuses["post_hatch_marker"]);
            Assert.Equal(Hatchling, hatched.EnemyId);
            Assert.True(hatched.NativeToughEggHatched);
            Assert.Single(hatched.PowerStates);
            Assert.Equal("proto.power.minion",
                hatched.PowerStates[0].PowerId);

            Assert.Equal(formationBefore, CanonicalJson.Sha256(
                after.NativeRng!.Formation));
            Assert.Equal(eggRng, NativeRngV111.ToSnapshot(
                after.NativeRng.Formation.MonsterContexts[^1].Rng));
            Assert.Equal(Egg,
                after.NativeRng.Formation.MonsterContexts[^1].Enemy.EnemyId);
            Assert.Equal(Egg, after.NativeRng.Formation.Enemies[^1].EnemyId);
            Assert.Equal(egg.InstanceId,
                after.NativeRng.Formation.MonsterContexts[^1].CombatId);
            Assert.Equal(nicheBefore.Counter + 1,
                Cursor(after, "run.niche").Counter);
            Assert.Equal(aiBefore, Cursor(after, "run.monster_ai"));
            Assert.Equal(NativeRngV111.ToSnapshot(
                oracle.Streams.Single(x => x.StreamId == "run.niche")),
                Cursor(after, "run.niche"));

            NativeCombatRngEntryV111.Validate(after);
            Assert.Equal(CanonicalJson.Sha256(after),
                CanonicalJson.Sha256(after.Fork()));
            var restored = JsonSerializer.Deserialize<CombatState>(
                JsonSerializer.Serialize(after))!;
            NativeCombatRngEntryV111.Validate(restored);
            Assert.Equal(CanonicalJson.Sha256(after),
                CanonicalJson.Sha256(restored));

            Assert.Throws<NotSupportedException>(() =>
                NativeCombatRngEntryV111.HatchToughEgg(
                    after, after.Enemies[^1], out _));
        }
    }

    [Fact]
    public void HatchlingProxyRequiresExplicitFlagAndOriginalNativeToughEgg()
    {
        var combat = WithSpawnedEgg("egg-identity-tampering", 0);
        var egg = combat.Enemies[^1];
        var mismatched = combat with
        {
            Enemies = combat.Enemies
                .Take(combat.Enemies.Length - 1)
                .Append(egg with { EnemyId = Hatchling }).ToArray()
        };
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(mismatched));
        var forged = mismatched with
        {
            Enemies = mismatched.Enemies
                .Take(mismatched.Enemies.Length - 1)
                .Append(mismatched.Enemies[^1] with
                {
                    NativeToughEggHatched = true
                }).ToArray()
        };
        NativeCombatRngEntryV111.Validate(forged);
        var wrongIdentity = forged with
        {
            Enemies = forged.Enemies
                .Take(forged.Enemies.Length - 1)
                .Append(forged.Enemies[^1] with
                {
                    EnemyId = PrototypeNativeHiveSummoningNormals.ParafrightId
                }).ToArray()
        };
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(wrongIdentity));
    }

    [Fact]
    public void HatchAfterEffectsDoNotAdvanceAnyMonsterLocalRng()
    {
        var combat = WithSpawnedEgg("egg-rng-cursor-isolation", 8);
        var originalContexts = combat.NativeRng!.Formation.MonsterContexts
            .Select(x => NativeRngV111.ToSnapshot(x.Rng)).ToArray();
        var hatched = NativeCombatRngEntryV111.HatchToughEgg(
            combat, combat.Enemies[^1], out _);
        Assert.Equal(originalContexts,
            hatched.NativeRng!.Formation.MonsterContexts
                .Select(x => NativeRngV111.ToSnapshot(x.Rng)).ToArray());
        Assert.Equal(Cursor(combat, "run.monster_ai"),
            Cursor(hatched, "run.monster_ai"));
    }
}
