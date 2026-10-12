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
    [InlineData("proto.potion.explosive_ampoule", 50)]
    [InlineData("proto.potion.colorless", 50)]
    [InlineData("proto.potion.energy", 50)]
    [InlineData("proto.potion.regen", 75)]
    [InlineData("proto.potion.cunning", 75)]
    [InlineData("proto.potion.fysh_oil", 75)]
    [InlineData("proto.potion.binding", 75)]
    [InlineData("proto.potion.heart_of_iron", 75)]
    [InlineData("proto.potion.clarity", 75)]
    [InlineData("proto.potion.powdered_demise", 75)]
    [InlineData("proto.potion.radiant_tincture", 75)]
    [InlineData("proto.potion.duplicator", 75)]
    [InlineData("proto.potion.touch_of_insanity", 75)]
    [InlineData("proto.potion.entropic_brew", 100)]
    [InlineData("proto.potion.fairy_in_a_bottle", 100)]
    [InlineData("proto.potion.distilled_chaos", 100)]
    [InlineData("proto.potion.ship_in_a_bottle", 100)]
    [InlineData("proto.potion.droplet_of_precognition", 100)]
    [InlineData("proto.potion.bottled_potential", 100)]
    [InlineData("proto.potion.shackling", 100)]
    [InlineData("proto.potion.mazaleths_gift", 100)]
    [InlineData("proto.potion.beetle_juice", 100)]
    [InlineData("proto.potion.gigantification", 100)]
    public void NativeMerchantPotionPricesFollowPinnedRarities(
        string potionId, int expected)
    {
        Assert.Equal(expected,
            PrototypeNativePotionShop.MerchantBaseCost(potionId));
    }

    [Fact]
    public void EverySupportedShopPotionHasExactlyOneNativeRarity()
    {
        Assert.Equal(46, PrototypeContent.PotionPool.Length);
        Assert.Contains("proto.potion.cunning", PrototypeContent.PotionPool);
        Assert.DoesNotContain("proto.potion.blood", PrototypeContent.PotionPool);
        Assert.DoesNotContain(
            "proto.potion.explosive", PrototypeContent.PotionPool);
        Assert.False(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.explosive", out _));
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.cunning", out var cunningRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, cunningRarity);
        Assert.Contains("proto.potion.fysh_oil", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.fysh_oil", out var fyshOilRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, fyshOilRarity);
        Assert.Contains("proto.potion.binding", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.binding", out var bindingRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, bindingRarity);
        Assert.Contains(
            "proto.potion.ship_in_a_bottle", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.ship_in_a_bottle", out var shipRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare, shipRarity);
        Assert.Contains(
            "proto.potion.droplet_of_precognition",
            PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.droplet_of_precognition", out var dropletRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare, dropletRarity);
        Assert.Contains(
            "proto.potion.explosive_ampoule", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.explosive_ampoule", out var ampouleRarity));
        Assert.Equal(PrototypeNativePotionRarity.Common, ampouleRarity);
        Assert.Contains(
            "proto.potion.bottled_potential", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.bottled_potential", out var bottledRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare, bottledRarity);
        Assert.Contains("proto.potion.heart_of_iron", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.heart_of_iron", out var heartRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, heartRarity);
        Assert.Contains("proto.potion.shackling", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.shackling", out var shacklingRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare, shacklingRarity);
        Assert.Contains(
            "proto.potion.mazaleths_gift", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.mazaleths_gift", out var mazalethRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare, mazalethRarity);
        Assert.Contains(
            "proto.potion.radiant_tincture", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.radiant_tincture", out var radiantRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, radiantRarity);
        Assert.Contains("proto.potion.beetle_juice", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.beetle_juice", out var beetleRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare, beetleRarity);
        Assert.Contains(
            "proto.potion.gigantification", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.gigantification", out var gigantificationRarity));
        Assert.Equal(PrototypeNativePotionRarity.Rare,
            gigantificationRarity);
        Assert.Contains("proto.potion.clarity", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.clarity", out var clarityRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, clarityRarity);
        Assert.Contains("proto.potion.colorless", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.colorless", out var colorlessRarity));
        Assert.Equal(PrototypeNativePotionRarity.Common, colorlessRarity);
        Assert.Contains(
            "proto.potion.powdered_demise", PrototypeContent.PotionPool);
        Assert.True(PrototypeNativePotionShop.TryGetRarity(
            "proto.potion.powdered_demise", out var demiseRarity));
        Assert.Equal(PrototypeNativePotionRarity.Uncommon, demiseRarity);
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
