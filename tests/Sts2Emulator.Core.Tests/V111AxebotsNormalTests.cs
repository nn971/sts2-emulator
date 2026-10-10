using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111AxebotsNormalTests
{
    [Theory]
    [InlineData(0, 70, 78)]
    [InlineData(8, 76, 86)]
    [InlineData(9, 76, 86)]
    public void AxebotStockChainUsesNativeStageRanges(int asc,
        int initialMin, int initialMax)
    {
        var stages = new[]
        {
            PrototypeNativeGloryNewNormals.AxebotId,
            PrototypeNativeGloryNewNormals.AxebotSecondId,
            PrototypeNativeGloryNewNormals.AxebotThirdId
        };
        for (var i = 0; i < 3; i++)
        {
            var enemy = PrototypeContent.Enemy(stages[i]);
            Assert.Equal((initialMin + i * 10,
                initialMax + i * 10), enemy.HpRangeAt(3, asc));
            Assert.Equal(i == 0 ? "uppercut" : "boot",
                enemy.Ai!.InitialStateId);
            var stock = enemy.StartingPowers
                ?.SingleOrDefault(p =>
                    p.PowerId == PrototypeNativeGloryNewNormals.StockId);
            if (i < 2)
                Assert.Equal(2 - i, stock!.StacksAt(asc));
            else
                Assert.Null(stock);
            if (i < 2)
            {
                var summon = Assert.Single(enemy.DeathSummons!);
                Assert.Equal(stages[i + 1], summon.EnemyId);
                Assert.Equal("front", summon.SlotName);
                Assert.Equal(0, summon.SkipEnemyActions);
            }
            else
                Assert.Null(enemy.DeathSummons);
            Assert.Equal(i * (asc >= 9 ? 4 : 3),
                enemy.Moves[0].Effects[1].AmountAt(3, asc));
            Assert.Equal(asc >= 9 ? 15 : 10,
                enemy.Moves[0].Effects[0].AmountAt(3, asc));
            Assert.Equal(asc >= 9 ? 18 : 14,
                enemy.Moves[2].Effects[0].AmountAt(3, asc));
        }
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeGloryNewNormals.AxebotsEncounterId);
        Assert.Equal((3, 3, 0),
            (encounter.MinAct, encounter.MaxAct, encounter.Weight));
        Assert.Equal("front",
            Assert.Single(encounter.FixedEnemySpecs).SlotName);
    }
}
