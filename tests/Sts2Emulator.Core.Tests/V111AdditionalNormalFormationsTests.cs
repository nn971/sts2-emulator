using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111AdditionalNormalFormationsTests
{
    [Fact]
    public void HiveTunnelerNormalUsesScreamFirstChomperAndTunneler()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeAdditionalNormals.TunnelerNormalId);
        Assert.Equal((2, 2, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal(new[] {
            PrototypeNativeHiveNormals.ChomperId,
            "proto.native.hive.tunneler"
        }, encounter.FixedEnemySpecs.Select(e => e.EnemyId));
        Assert.Equal("screech-first",
            encounter.FixedEnemySpecs[0].SlotName);
        Assert.Equal(1, encounter.FixedEnemySpecs[1].FormationPosition);
        Assert.All(encounter.FixedEnemySpecs,
            e => Assert.NotEmpty(PrototypeContent.Enemy(e.EnemyId).Moves));
    }

    [Fact]
    public void GloryMenagerieContainsIndividuallyTargetableThreeMonsters()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeAdditionalNormals.ConstructMenagerieNormalId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal(new[] {
            "proto.enemy.punch_construct",
            "proto.enemy.cubex_construct",
            "proto.enemy.cubex_construct"
        }, encounter.FixedEnemySpecs.Select(e => e.EnemyId));
        Assert.Equal(new[] { 0, 1, 2 },
            encounter.FixedEnemySpecs.Select(e => e.FormationPosition));
        Assert.All(encounter.FixedEnemySpecs,
            e => Assert.NotEmpty(PrototypeContent.Enemy(e.EnemyId).Moves));
    }
}
