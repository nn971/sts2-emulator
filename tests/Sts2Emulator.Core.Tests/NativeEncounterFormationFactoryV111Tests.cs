using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeEncounterFormationFactoryV111Tests
{
    private static PrototypeEncounterDefinition VariableEncounter() =>
        new("emulator.test.native.variable_formation",
            PrototypeRoomType.Combat, [],
            FormationVariants:
            [
                new(SelectionGroups:
                [
                    new(["bowlbug", "silk", "rock", "nectar"],
                        [0, 2], ChooseDistinct: true,
                        SlotNames: ["left", "right"])
                ]),
                new(Formation: [new("queen", 3, "middle")],
                    SelectionGroups:
                    [
                        new(["drone", "spider"], [4, 5],
                            SlotNames: ["top", "bottom"])
                    ])
            ]);

    [Theory]
    [InlineData("native-encounter-variable-0", 10, 0)]
    [InlineData("native-encounter-variable-1", 25, 1)]
    [InlineData("native-encounter-variable-2", 36, 2)]
    public void NativeFormationUsesOnlyItsOwnExactSeededStream(
        string seed, int totalFloor, int actIndex)
    {
        var definition = VariableEncounter();
        var result = NativeEncounterFormationFactoryV111.Generate(
            seed, definition, "BowlbugsNormal", totalFloor, actIndex,
            firstMonsterCombatId: 1, mapColumn: 4, mapRow: 6);

        var stream = NativeRngV111Contexts.ForEncounterFormation(
            seed, "BowlbugsNormal", totalFloor);
        var bundle = new RngBundle([stream]);
        var manuallySelected = definition.ResolveEnemySpecs(max =>
            NativeRngV111.NextInt(bundle, stream.StreamId, max));

        Assert.Equal(CanonicalJson.Sha256(manuallySelected),
            CanonicalJson.Sha256(result.Enemies));
        Assert.Equal(
            NativeRngV111.ToSnapshot(bundle.Streams[0]),
            NativeRngV111.ToSnapshot(result.EncounterRng));
        Assert.True(NativeRngV111.ToSnapshot(result.EncounterRng).Counter > 0);

        Assert.Equal(result.Enemies.Length, result.MonsterContexts.Length);
        for (var i = 0; i < result.MonsterContexts.Length; i++)
        {
            var monster = result.MonsterContexts[i];
            Assert.Equal(1 + i, monster.CombatId);
            Assert.Equal(result.Enemies[i], monster.Enemy);
            var expected = NativeRngV111Contexts.ForMonster(
                seed, actIndex, 1 + i, 4, 6);
            Assert.Equal(
                NativeRngV111.ToSnapshot(expected),
                NativeRngV111.ToSnapshot(monster.Rng));
            Assert.Equal(0, NativeRngV111.ToSnapshot(monster.Rng).Counter);
        }
    }

    [Fact]
    public void FormationHasIndependentFloorAndEncounterIdentity()
    {
        var definition = VariableEncounter();
        var a = NativeEncounterFormationFactoryV111.Generate(
            "native-context-independent", definition, "BowlbugsNormal",
            12, 1, 1);
        var b = NativeEncounterFormationFactoryV111.Generate(
            "native-context-independent", definition, "BowlbugsNormal",
            13, 1, 1);
        var c = NativeEncounterFormationFactoryV111.Generate(
            "native-context-independent", definition, "ChompersNormal",
            12, 1, 1);

        Assert.NotEqual(
            NativeRngV111.ToSnapshot(a.EncounterRng),
            NativeRngV111.ToSnapshot(b.EncounterRng));
        Assert.NotEqual(
            NativeRngV111.ToSnapshot(a.EncounterRng),
            NativeRngV111.ToSnapshot(c.EncounterRng));
        // The monster-local seed is *not* mixed with encounter identity or
        // total floor: only combat id, act index, map coordinates and run seed.
        Assert.Equal(
            NativeRngV111.ToSnapshot(a.MonsterContexts[0].Rng),
            NativeRngV111.ToSnapshot(b.MonsterContexts[0].Rng));
        Assert.Equal(
            NativeRngV111.ToSnapshot(a.MonsterContexts[0].Rng),
            NativeRngV111.ToSnapshot(c.MonsterContexts[0].Rng));
    }

    [Fact]
    public void FormationSnapshotForkAndJsonRestoreRetainIndependentCursors()
    {
        var original = NativeEncounterFormationFactoryV111.Generate(
            "native-encounter-fork", VariableEncounter(),
            "OvicopterNormal", 22, 1, 5, 2, 7);
        var hash = CanonicalJson.Sha256(original);
        var fork = original.Fork();
        var restored = JsonSerializer.Deserialize<NativeEncounterFormationV111>(
            JsonSerializer.Serialize(original))!;

        Assert.Equal(hash, CanonicalJson.Sha256(fork));
        Assert.Equal(hash, CanonicalJson.Sha256(restored));

        var forkBundle = new RngBundle([fork.EncounterRng]);
        var sourceBundle = new RngBundle([original.EncounterRng]);
        Assert.Equal(
            NativeRngV111.NextInt(forkBundle, fork.EncounterRng.StreamId, 12),
            NativeRngV111.NextInt(sourceBundle, original.EncounterRng.StreamId, 12));

        Assert.Equal(
            NativeRngV111.ToSnapshot(forkBundle.Streams[0]),
            NativeRngV111.ToSnapshot(sourceBundle.Streams[0]));
        Assert.Equal(hash, CanonicalJson.Sha256(original));
        Assert.Equal(hash, CanonicalJson.Sha256(restored));

        fork.Enemies[0] = fork.Enemies[0] with { EnemyId = "mutated" };
        fork.MonsterContexts[0].Rng.StateBytes[0] ^= 0xff;
        Assert.Equal(hash, CanonicalJson.Sha256(original));
        Assert.NotEqual(hash, CanonicalJson.Sha256(fork));
    }

    [Fact]
    public void FixedFormationDoesNotConsumeEncounterOrMonsterStream()
    {
        var definition = new PrototypeEncounterDefinition(
            "emulator.test.fixed",
            PrototypeRoomType.Elite, ["one", "two"],
            Formation:
            [
                new("one", 1, "a"),
                new("two", 2, "b")
            ]);
        var result = NativeEncounterFormationFactoryV111.Generate(
            "fixed-native-formation", definition,
            "DecimillipedeElite", 15, 1, 4);
        Assert.Equal(0, NativeRngV111.ToSnapshot(result.EncounterRng).Counter);
        Assert.All(result.MonsterContexts,
            monster => Assert.Equal(0,
                NativeRngV111.ToSnapshot(monster.Rng).Counter));
        Assert.Equal(
            NativeRngV111.ToSnapshot(
                NativeRngV111Contexts.ForEncounterFormation(
                    "fixed-native-formation", "DecimillipedeElite", 15)),
            NativeRngV111.ToSnapshot(result.EncounterRng));
        Assert.Equal(new[] { "a", "b" },
            result.Enemies.Select(x => x.SlotName).ToArray());
    }

    [Fact]
    public void LegacyFormationStillConsumesOnlyPrototypeCombatStream()
    {
        var encounter = VariableEncounter();
        var first = PrototypeRng.CreateBundle("formation-legacy-stays-legacy");
        var second = first.Fork();
        var otherBefore = first.Streams
            .Where(s => s.StreamId != "combat")
            .Select(CanonicalJson.Sha256).ToArray();
        var specs = encounter.ResolveEnemySpecs(first);
        var replay = encounter.ResolveEnemySpecs(second);
        Assert.Equal(CanonicalJson.Sha256(specs),
            CanonicalJson.Sha256(replay));
        Assert.Equal(CanonicalJson.Sha256(first),
            CanonicalJson.Sha256(second));
        Assert.Equal(otherBefore, first.Streams
            .Where(s => s.StreamId != "combat")
            .Select(CanonicalJson.Sha256).ToArray());
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(3, 1)]
    [InlineData(1, -1)]
    public void NativeFormationRejectsInvalidContext(int actIndex, long combatId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NativeEncounterFormationFactoryV111.Generate(
                "bad", VariableEncounter(), "BowlbugsNormal", 10,
                actIndex, combatId));
    }

    [Fact]
    public void MonsterCombatIdMustBeAvailableWithoutOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NativeEncounterFormationFactoryV111.Generate(
                "overflow", VariableEncounter(), "BowlbugsNormal", 10, 1,
                long.MaxValue));
    }
}
