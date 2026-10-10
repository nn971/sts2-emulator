using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativePotionRewardOddsTests
{
    [Fact]
    public void PityStartsAtFortyPercentAndMovesTenPointsPerCombat()
    {
        var rng = PrototypeRng.CreateBundle("potion-pity-transitions");
        var expectedRng = rng.Fork();
        var current = PrototypeNativePotionRewardOdds.InitialThousandths;
        Assert.Equal(400, current);

        for (var i = 0; i < 100; i++)
        {
            var roll = PrototypeRng.NextInt(expectedRng, "reward", 1000);
            var expected = roll < current;
            var (offered, next) =
                PrototypeNativePotionRewardOdds.Roll(
                    current, elite: false, rng);
            Assert.Equal(expected, offered);
            Assert.Equal(current + (offered ? -100 : 100), next);
            current = next;
        }
        Assert.Equal(
            expectedRng.Streams.Single(stream => stream.StreamId == "reward").CallCount,
            rng.Streams.Single(stream => stream.StreamId == "reward").CallCount);
    }

    [Fact]
    public void EliteBonusOnlyAffectsThresholdAndDoesNotAccumulate()
    {
        var rng = PrototypeRng.CreateBundle("potion-pity-elite");
        var expectedRng = rng.Fork();
        var firstRoll = PrototypeRng.NextInt(expectedRng, "reward", 1000);
        var (offered, next) = PrototypeNativePotionRewardOdds.Roll(
            400, elite: true, rng);
        Assert.Equal(firstRoll < 525, offered);
        Assert.Equal(offered ? 300 : 500, next);

        var secondRoll = PrototypeRng.NextInt(expectedRng, "reward", 1000);
        var (secondOffered, secondNext) =
            PrototypeNativePotionRewardOdds.Roll(
                next, elite: false, rng);
        Assert.Equal(secondRoll < next, secondOffered);
        Assert.Equal(next + (secondOffered ? -100 : 100), secondNext);
    }

    [Fact]
    public void PityIsNotArtificiallyClampedAndRemainsDeterministic()
    {
        var rng = PrototypeRng.CreateBundle("potion-pity-boundaries");
        Assert.Equal((false, 100), PrototypeNativePotionRewardOdds.Roll(
            0, elite: false, rng));
        Assert.Equal((true, 900), PrototypeNativePotionRewardOdds.Roll(
            1000, elite: false, rng));
        Assert.Equal((false, -100), PrototypeNativePotionRewardOdds.Roll(
            -200, elite: true, rng));
        Assert.Equal((true, 1000), PrototypeNativePotionRewardOdds.Roll(
            1100, elite: false, rng));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForcedPotionSkipsRngAndPreservesPity(bool elite)
    {
        var rng = PrototypeRng.CreateBundle(
            "forced-potion-" + elite);
        var before = rng.Streams.Single(
            stream => stream.StreamId == "reward").CallCount;
        foreach (var current in new[] { -200, 0, 400, 900, 1200 })
        {
            var (offered, updated) =
                PrototypeNativePotionRewardOdds.Roll(
                    current, elite, rng, forced: true);
            Assert.True(offered);
            Assert.Equal(current, updated);
        }
        Assert.Equal(before, rng.Streams.Single(
            stream => stream.StreamId == "reward").CallCount);
    }

    [Fact]
    public void RunWorldForkPreservesPityAndInvariantRejectsFractionalSteps()
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            "potion-pity-fork");
        Assert.Equal(400, state.World!.PotionRewardOddsThousandths);
        var changed = state with
        {
            World = state.World with
            {
                PotionRewardOddsThousandths = 700
            }
        };
        PrototypeStateInvariants.Validate(changed);
        var fork = changed.Fork();
        Assert.Equal(700, fork.World!.PotionRewardOddsThousandths);
        Assert.Equal(CanonicalJson.Sha256(changed),
            CanonicalJson.Sha256(fork));

        var invalid = changed with
        {
            World = changed.World with
            {
                PotionRewardOddsThousandths = 725
            }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(invalid));
    }
}
