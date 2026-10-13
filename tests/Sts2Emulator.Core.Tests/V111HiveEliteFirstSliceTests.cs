using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveEliteFirstSliceTests
{
    [Theory]
    [InlineData(0, 145, 7, 18, 161, 15, 11, 5, 8, 20, 2)]
    [InlineData(8, 165, 7, 18, 171, 15, 11, 5, 8, 22, 2)]
    [InlineData(9, 165, 8, 20, 171, 17, 13, 6, 10, 22, 3)]
    public void EntomancerAndPrismScalePerPinnedNativeSource(
        int asc, int entHp, int bees, int spear,
        int prismHp, int jab, int radiate, int whirlwind,
        int pulsate, int block, int spark)
    {
        var ent = PrototypeContent.Enemy(
            PrototypeNativeHiveElites.EntomancerId);
        Assert.Equal((entHp, entHp), ent.HpRangeAt(2, asc));
        Assert.Equal(new[] { "bees", "spear", "pheromone_spit" },
            ent.Moves.Select(move => move.Id));
        Assert.Equal(bees, ent.Moves[0].Effects[0].RepetitionsAt(asc));
        Assert.Equal(3, ent.Moves[0].Effects[0].AmountAt(2, asc));
        Assert.Equal(spear, ent.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Contains(ent.StartingPowers!, p =>
            p.PowerId == PrototypeNativeHiveElites.PersonalHiveId);

        var prism = PrototypeContent.Enemy(PrototypeNativeHiveElites.PrismId);
        Assert.Equal((prismHp, prismHp), prism.HpRangeAt(2, asc));
        Assert.Equal(new[] { "jab", "radiate", "whirlwind", "pulsate" },
            prism.Moves.Select(move => move.Id));
        Assert.Equal(jab, prism.Moves[0].Effects[0].AmountAt(2, asc));
        Assert.Equal(radiate, prism.Moves[1].Effects[0].AmountAt(2, asc));
        Assert.Equal(radiate, prism.Moves[1].Effects[1].AmountAt(2, asc));
        Assert.Equal(3, prism.Moves[2].Effects[0].RepetitionsAt(asc));
        Assert.Equal(whirlwind, prism.Moves[2].Effects[0].AmountAt(2, asc));
        Assert.Equal(pulsate, prism.Moves[3].Effects[0].AmountAt(2, asc));
        Assert.Equal(block, prism.Moves[3].Effects[1].AmountAt(2, asc));
        Assert.Equal(spark, prism.StartingPowers!.Single().StacksAt(asc));
        Assert.Equal(spark, prism.Moves[3].Effects[2].AmountAt(2, asc));
    }

    [Theory]
    [InlineData(0, 40, 46, 5, 6, 8)]
    [InlineData(8, 46, 52, 5, 6, 8)]
    [InlineData(9, 46, 52, 6, 7, 9)]
    public void SegmentRegistryContainsThreeIndependentModels(
        int asc, int minHp, int maxHp, int writhe, int bulk, int constrict)
    {
        foreach (var id in new[]
        {
            PrototypeNativeHiveElites.FrontId,
            PrototypeNativeHiveElites.MiddleId,
            PrototypeNativeHiveElites.BackId
        })
        {
            var segment = PrototypeContent.Enemy(id);
            Assert.Equal((minHp, maxHp), segment.HpRangeAt(2, asc));
            Assert.Equal(2, segment.Moves[0].Effects[0].RepetitionsAt(asc));
            Assert.Equal(writhe, segment.Moves[0].Effects[0].AmountAt(2, asc));
            Assert.Equal(bulk, segment.Moves[1].Effects[0].AmountAt(2, asc));
            Assert.Equal(2, segment.Moves[1].Effects[1].Amount);
            Assert.Equal(constrict,
                segment.Moves[2].Effects[0].AmountAt(2, asc));
            Assert.Equal(25, Assert.Single(segment.StartingPowers!).Stacks);
        }
    }

    [Fact]
    public void AllThreeElitesStayGatedAndHaveNativeFormations()
    {
        var entries = new[]
        {
            (PrototypeNativeHiveElites.EntomancerEncounterId, 1),
            (PrototypeNativeHiveElites.PrismEncounterId, 1),
            (PrototypeNativeHiveElites.DecimillipedeEncounterId, 3)
        };
        foreach (var (id, count) in entries)
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Elite, encounter.RoomType);
            Assert.Equal((2, 2, 0),
                (encounter.MinAct, encounter.MaxAct, encounter.Weight));
            Assert.Equal(count, encounter.FixedEnemySpecs.Length);
        }
        var trio = PrototypeContent.Encounter(
            PrototypeNativeHiveElites.DecimillipedeEncounterId);
        Assert.Equal(new[] { "segment1", "segment2", "segment3" },
            trio.FixedEnemySpecs.Select(s => s.SlotName));
    }

    [Fact]
    public void EveryEliteMoveHasRegisteredPowerAndDamageReferences()
    {
        foreach (var enemy in PrototypeNativeHiveElites.Enemies)
        {
            foreach (var starting in enemy.StartingPowers
                ?? Array.Empty<PrototypeStartingPowerSpec>())
                _ = PrototypeContent.Power(starting.PowerId);
            foreach (var effect in enemy.Moves.SelectMany(m => m.Effects))
            {
                if (effect.PowerId is not null)
                    _ = PrototypeContent.Power(effect.PowerId);
                if (effect.EnemyId is not null)
                    _ = PrototypeContent.Enemy(effect.EnemyId);
            }
        }
    }
}
