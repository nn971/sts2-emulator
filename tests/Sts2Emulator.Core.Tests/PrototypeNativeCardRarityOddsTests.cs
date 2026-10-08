using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeCardRarityOddsTests
{
    [Theory]
    [InlineData(0, PrototypeNativeCardOddsSource.RegularEncounter, 300, 3700)]
    [InlineData(0, PrototypeNativeCardOddsSource.EliteEncounter, 1000, 4000)]
    [InlineData(0, PrototypeNativeCardOddsSource.BossEncounter, 10000, 0)]
    [InlineData(0, PrototypeNativeCardOddsSource.Shop, 900, 3700)]
    [InlineData(7, PrototypeNativeCardOddsSource.RegularEncounter, 149, 3700)]
    [InlineData(7, PrototypeNativeCardOddsSource.EliteEncounter, 500, 4000)]
    [InlineData(7, PrototypeNativeCardOddsSource.Shop, 450, 3700)]
    public void PinnedThresholdsAndRarityTransitions(
        int ascension, PrototypeNativeCardOddsSource source,
        int baseRare, int baseUncommon)
    {
        var rng = PrototypeRng.CreateBundle(
            $"card-odds-{ascension}-{source}");
        var expected = rng.Fork();
        var current = PrototypeNativeCardRarityOdds.InitialOffsetBasisPoints;
        for (var i = 0; i < 120; i++)
        {
            var roll = PrototypeRng.NextInt(expected, "reward", 10000);
            var rare = baseRare + (source ==
                PrototypeNativeCardOddsSource.BossEncounter ? 0 : current);
            var rarity = roll < rare
                ? PrototypeCardRarity.Rare
                : roll < rare + baseUncommon
                    ? PrototypeCardRarity.Uncommon
                    : PrototypeCardRarity.Common;
            var (actual, next) = PrototypeNativeCardRarityOdds.Roll(
                current, ascension, source, rng);
            Assert.Equal(rarity, actual);
            var expectedNext = source == PrototypeNativeCardOddsSource.Shop
                ? current
                : rarity == PrototypeCardRarity.Rare
                    ? -500
                    : Math.Min(4000,
                        current + (ascension >= 7 ? 50 : 100));
            Assert.Equal(expectedNext, next);
            current = next;
        }
        Assert.Equal(expected.Streams.Single(s => s.StreamId == "reward").CallCount,
            rng.Streams.Single(s => s.StreamId == "reward").CallCount);
    }

    [Fact]
    public void BossRarityResetsPityEvenAfterLongDryStreak()
    {
        var rng = PrototypeRng.CreateBundle("card-pity-boss");
        var (rarity, next) = PrototypeNativeCardRarityOdds.Roll(
            4000, 0, PrototypeNativeCardOddsSource.BossEncounter, rng);
        Assert.Equal(PrototypeCardRarity.Rare, rarity);
        Assert.Equal(-500, next);
    }

    [Fact]
    public void FallbackCyclesCommonUncommonRareAndWraps()
    {
        Assert.Equal(PrototypeCardRarity.Uncommon,
            PrototypeNativeCardRarityOdds.NextAvailableRarity(
                PrototypeCardRarity.Common,
                [PrototypeCardRarity.Uncommon]));
        Assert.Equal(PrototypeCardRarity.Common,
            PrototypeNativeCardRarityOdds.NextAvailableRarity(
                PrototypeCardRarity.Rare,
                [PrototypeCardRarity.Common]));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeNativeCardRarityOdds.NextAvailableRarity(
                PrototypeCardRarity.Rare,
                Array.Empty<PrototypeCardRarity>()));
    }

    [Fact]
    public void EncounterCardChoicesAreDistinctAndPersistentOddsReplay()
    {
        var left = PrototypeRng.CreateBundle("card-odds-encounter");
        var right = left.Fork();
        var a = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
            3, 0, PrototypeRoomType.Elite, 1500, left);
        var b = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
            3, 0, PrototypeRoomType.Elite, 1500, right);
        Assert.Equal(a.Cards, b.Cards);
        Assert.Equal(a.NextOffsetBasisPoints, b.NextOffsetBasisPoints);
        Assert.Equal(3, a.Cards.Distinct(StringComparer.Ordinal).Count());
        Assert.All(a.Cards, id =>
            Assert.Contains(id, PrototypeContent.RewardCardPool));
        Assert.Equal(9,
            left.Streams.Single(s => s.StreamId == "reward").CallCount);
    }

    [Fact]
    public void MerchantRarityUsesRewardStreamButLeavesPityUnchanged()
    {
        var left = PrototypeRng.CreateBundle("card-odds-merchant");
        var right = left.Fork();
        var a = PrototypeNativeCardRarityOdds.GenerateMerchantCards(
            5, 0, PrototypeCardType.Attack, 2500, left);
        var b = PrototypeNativeCardRarityOdds.GenerateMerchantCards(
            5, 0, PrototypeCardType.Attack, 2500, right);
        Assert.Equal(a, b);
        Assert.Equal(5, left.Streams.Single(
            s => s.StreamId == "reward").CallCount);
        Assert.Equal(10, left.Streams.Single(
            s => s.StreamId == "shop").CallCount);
        Assert.All(a, id => Assert.Equal(PrototypeCardType.Attack,
            PrototypeContent.Card(id).Type));
    }

    [Fact]
    public void WorldStateForkPreservesNativeCardOdds()
    {
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "card-odds-fork");
        Assert.Equal(-500, initial.World!.CardRarityOffsetBasisPoints);
        var state = initial with
        {
            World = initial.World with { CardRarityOffsetBasisPoints = 1550 }
        };
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(1550, state.Fork().World!.CardRarityOffsetBasisPoints);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));

        var invalid = state with
        {
            World = state.World with { CardRarityOffsetBasisPoints = 1525 }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(invalid));
    }
}
