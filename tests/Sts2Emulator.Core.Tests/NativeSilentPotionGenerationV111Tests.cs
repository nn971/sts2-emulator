using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeSilentPotionGenerationV111Tests
{
    [Fact]
    public void SourceOrderedFullUnlockPoolReproducesPinnedPhialHolsterDraws()
    {
        var pool = NativeSilentPotionGenerationV111.FullyUnlockedPool;
        Assert.Equal(48, pool.Length);
        Assert.Equal(48, pool.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(pool, p => Assert.Contains(p.Id, PrototypeContent.Potions.Keys));
        Assert.Equal("proto.potion.poison", pool[0].Id);
        Assert.Equal("proto.potion.ghost_in_a_jar", pool[1].Id);
        Assert.Equal("proto.potion.cunning", pool[2].Id);
        Assert.Equal("proto.potion.attack", pool[3].Id);
        Assert.Equal("proto.potion.weak", pool[^1].Id);
        var rng = NativeRngV111.CreateBundle("6JK8MD0VQQTA");
        var untouched = rng.Fork();
        var first = NativeSilentPotionGenerationV111.Generate(rng, 2);
        Assert.Equal(
            ["proto.potion.fortifier", "proto.potion.vulnerable"],
            first.PotionIds);
        Assert.Equal(
            [NativeSilentPotionGenerationV111.Rarity.Uncommon,
             NativeSilentPotionGenerationV111.Rarity.Common],
            first.RolledRarities);
        Assert.Equal(new NativeRngSnapshot(
            4, 247483884377393130UL, 5718769650147259872UL,
            2262233218431618893UL, 7728349850348698365UL),
            first.CursorAfter);
        Assert.Equal(first.PotionIds,
            NativeSilentPotionGenerationV111.Generate(untouched.Fork(), 2).PotionIds);
        foreach (var stream in untouched.Streams.Where(
            stream => stream.StreamId != "run.combat_potion_generation"))
            Assert.Equal(NativeRngV111.ToSnapshot(stream),
                NativeRngV111.ToSnapshot(rng.Streams.Single(
                    actual => actual.StreamId == stream.StreamId)));
    }

    [Fact]
    public void BatchDrawRemovesEachPotionExactlyOnceAndUsesTwoCallsPerSelection()
    {
        foreach (var seed in Enumerable.Range(0, 12).Select(index =>
                     $"native-potion-batch-{index}"))
        {
            var rng = NativeRngV111.CreateBundle(seed);
            var draw = NativeSilentPotionGenerationV111.Generate(rng, 3);
            Assert.Equal(3, draw.PotionIds.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(6, draw.CursorAfter.Counter);
            Assert.All(draw.PotionIds, id => Assert.Contains(id,
                NativeSilentPotionGenerationV111.FullyUnlockedPool.Select(p => p.Id)));
        }
    }

    [Fact]
    public void PhialHolsterAppendsCapacityAndRequiresTwoFreeSlots()
    {
        var seed = "native-phial-holster-capacity";
        var run = NativeRngV111.CreateBundle(seed);
        var empty = new PlayerState(56, 70, 99, [], [], new PotionInstance?[2]);
        var obtained = NativeSilentPotionGenerationV111.ApplyPhialHolster(empty, run);
        Assert.Equal(3, obtained.PotionSlots.Length);
        Assert.Equal(2, obtained.PotionSlots.Count(p => p is not null));
        Assert.Equal(4, NativeRngV111.ToSnapshot(run.Streams.Single(
            stream => stream.StreamId == "run.combat_potion_generation")).Counter);
        Assert.All(empty.PotionSlots, p => Assert.Null(p));
        var full = empty with { PotionSlots = [
            new PotionInstance("proto.potion.fire", PrototypeJson.EmptyObject()),
            new PotionInstance("proto.potion.weak", PrototypeJson.EmptyObject())
        ] };
        Assert.Throws<NotSupportedException>(() =>
            NativeSilentPotionGenerationV111.ApplyPhialHolster(
                full, NativeRngV111.CreateBundle(seed)));
    }
}
