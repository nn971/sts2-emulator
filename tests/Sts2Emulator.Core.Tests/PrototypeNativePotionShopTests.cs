using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativePotionShopTests
{

    [Fact]
    public void NativeRarityRollMatchesTenTwentyFiveSixtyFiveDistribution()
    {
        var rng = PrototypeRng.CreateBundle("native-rarity-weights-10000");
        var outcomes = Enumerable.Range(0, 10000)
            .Select(_ => PrototypeNativePotionShop.RollRarity(rng, "shop"))
            .ToArray();
        Assert.InRange(outcomes.Count(r =>
            r == PrototypeNativePotionRarity.Rare), 830, 1170);
        Assert.InRange(outcomes.Count(r =>
            r == PrototypeNativePotionRarity.Uncommon), 2250, 2750);
        Assert.InRange(outcomes.Count(r =>
            r == PrototypeNativePotionRarity.Common), 6200, 6800);
    }

    [Fact]
    public void MerchantBatchHasThreeDistinctPotionsAndReplaysFromFork()
    {
        var rng = PrototypeRng.CreateBundle("native-potion-three");
        var fork = rng.Fork();
        var first = PrototypeNativePotionShop.PickWeightedDistinct(
            rng, "shop", 3);
        var second = PrototypeNativePotionShop.PickWeightedDistinct(
            fork, "shop", 3);
        Assert.Equal(3, first.Length);
        Assert.Equal(3, first.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first, second);
        Assert.All(first, id =>
            Assert.Contains(id, PrototypeContent.PotionPool));
        Assert.Empty(PrototypeNativePotionShop.PickWeightedDistinct(
            rng, "shop", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativePotionShop.PickWeightedDistinct(
                rng, "shop", -1));
    }

    [Fact]
    public void UniformEventSamplingUsesFullCatalogWithoutRarityWeighting()
    {
        var left = PrototypeRng.CreateBundle("native-wellspring-uniform");
        var right = left.Fork();
        var fromHelper = PrototypeNativePotionShop.PickUniform(
            left, "reward");
        var expected = PrototypeContent.PotionPool[
            PrototypeRng.NextInt(
                right, "reward", PrototypeContent.PotionPool.Length)];
        Assert.Equal(expected, fromHelper);
    }

    [Fact]
    public void WeightedRollDoesNotSubstituteMissingRareTier()
    {
        var rng = PrototypeRng.CreateBundle("native-rare-exhaustion");
        var excluded = PrototypeContent.PotionPool
            .Where(id => PrototypeNativePotionShop.TryGetRarity(
                id, out var rarity)
                && rarity == PrototypeNativePotionRarity.Rare)
            .ToArray();
        var sawExhaustion = false;
        for (var i = 0; i < 100; i++)
        {
            try
            {
                _ = PrototypeNativePotionShop.PickWeighted(
                    rng, "shop", excluded);
            }
            catch (InvalidOperationException)
            {
                sawExhaustion = true;
                break;
            }
        }
        Assert.True(sawExhaustion);
    }

    [Theory]
    [InlineData("proto.potion.block", 50)]
    [InlineData("proto.potion.energy", 50)]
    [InlineData("proto.potion.blood", 50)]
    [InlineData("proto.potion.regen", 75)]
    [InlineData("proto.potion.duplicator", 75)]
    [InlineData("proto.potion.touch_of_insanity", 75)]
    [InlineData("proto.potion.entropic_brew", 100)]
    [InlineData("proto.potion.fairy_in_a_bottle", 100)]
    [InlineData("proto.potion.distilled_chaos", 100)]
    public void NativeMerchantPotionPricesFollowPinnedRarities(
        string potionId, int expected)
    {
        Assert.Equal(expected,
            PrototypeNativePotionShop.MerchantBaseCost(potionId));
    }

    [Fact]
    public void EverySupportedShopPotionHasExactlyOneNativeRarity()
    {
        Assert.Equal(32, PrototypeContent.PotionPool.Length);
        Assert.All(PrototypeContent.PotionPool, id =>
        {
            Assert.True(PrototypeNativePotionShop.TryGetRarity(id, out _),
                $"Potion {id} missing native rarity.");
            Assert.Contains(
                PrototypeNativePotionShop.MerchantBaseCost(id),
                new[] { 50, 75, 100 });
        });
        Assert.False(PrototypeNativePotionShop.TryGetRarity(
            "proto.native.neow.ambergris", out _));
    }

    [Fact]
    public void NativeShopPotionPriceRollsRemainInRarityBands()
    {
        var rng = PrototypeRng.CreateBundle("native-potion-prices");
        foreach (var id in PrototypeContent.PotionPool)
        {
            var value = PrototypeNativePotionShop.MerchantBaseCost(id);
            var minimum = (int)Math.Round(value * 0.95);
            var maximum = (int)Math.Round(value * 1.05);
            for (var i = 0; i < 48; i++)
            {
                Assert.InRange(PrototypeNativePotionShop.MerchantPrice(id, rng),
                    minimum, maximum);
            }
        }

        Assert.Throws<InvalidOperationException>(() =>
            PrototypeNativePotionShop.MerchantBaseCost(
                "proto.native.neow.ambergris"));
    }
}
