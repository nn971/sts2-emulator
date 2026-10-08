using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativePotionShopTests
{
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
