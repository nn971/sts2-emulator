using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NatureH3BossResearchFixtureTests
{
    [Theory]
    [InlineData("proto.encounter.ceremonial_beast_boss", "CeremonialBeastBoss",
        SemanticConditionedDeckPresetV111.BossCycle20)]
    [InlineData("proto.encounter.vantom_boss", "VantomBoss",
        SemanticConditionedDeckPresetV111.BossPoison20)]
    public void SeededA10BossOpensWithRealTwentyCardDeckAndTwoExtraRelics(
        string encounterId, string nativeEntry, string preset)
    {
        var source = new SemanticSeededEntryV111(
            "h3-boss-fixture-test-" + nativeEntry, 1, encounterId,
            nativeEntry, 6, 10, 1, 3, 5,
            ReplayOpeningDrawHooks: true, FixtureDeckPresetId: preset);
        var first = SemanticPilotCombatEngineV111.BeginSeeded(source);
        var second = SemanticPilotCombatEngineV111.BeginSeeded(source);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(first.AtPlayerBoundary);
        Assert.Equal(PrototypeRoomType.Boss, first.Run.World!.ActiveRoom);
        Assert.Equal(6, first.Run.World.Floor);
        Assert.Equal(20, first.Run.Player.Deck.Length);
        Assert.Equal(20, first.Run.World.Combat!.Cards.Length);
        Assert.Equal(20, first.Run.Player.Deck.Select(c => c.InstanceId)
            .Distinct().Count());
        Assert.Equal(3, first.Run.Player.Relics.Length);
        Assert.Equal("proto.relic.silent_ring",
            first.Run.Player.Relics[0].RelicId);
        Assert.Equal(first.Fingerprint, SemanticPilotCombatEngineV111.Restore(
            SemanticPilotCombatEngineV111.Save(first)).Fingerprint);
        Assert.Equal(first.Run.Player.Deck.Select(c => c.CardId),
            second.Run.Player.Deck.Select(c => c.CardId));
        Assert.Equal(first.Run.Player.Relics.Select(r => r.RelicId),
            second.Run.Player.Relics.Select(r => r.RelicId));
        Assert.True(first.Run.World!.NextCardInstanceId >= 21);
    }

    [Fact]
    public void BossRewardShuffleIsEngineOwnedOnlyAfterCombatWin()
    {
        var streams = PrototypeRng.CreateBundle("h3-boss-postcombat-reward");
        var items = new List<int> { 1, 2, 3, 4 };
        var site = StochasticSiteRegistryV111.RewardBossRelicOfferShuffleSiteId;
        using (ControlledChanceGuardV111.Enter())
        {
            Assert.Throws<NotSupportedException>(() =>
                LegacyFisherYatesV111.ShuffleInPlace(
                    items, streams, "reward", site, "boss:reward"));
            using (ControlledChanceGuardV111.AllowEngineOwnedPrototypeReward())
            {
                _ = LegacyFisherYatesV111.ShuffleInPlace(
                    items, streams, "reward", site, "boss:reward");
                Assert.Equal(new[] { 1, 2, 3, 4 },
                    items.Order().ToArray());
                Assert.Throws<NotSupportedException>(() =>
                    LegacyFisherYatesV111.ShuffleInPlace(
                        items, streams, "reward",
                        StochasticSiteRegistryV111.RewardLavaRockRelicOfferShuffleSiteId,
                        "unrelated:reward"));
            }
        }
    }

    [Fact]
    public void BossResearchPresetIsDistinctFromStarterWithoutNativeRngClaims()
    {
        var starter = PrototypeContent.StartingDeck
            .Select((name, index) => new CardInstance(
                index + 1, name, 0, PrototypeJson.EmptyObject())).ToArray();
        foreach (var preset in new[]
                 {
                     SemanticConditionedDeckPresetV111.BossCycle20,
                     SemanticConditionedDeckPresetV111.BossPoison20
                 })
        {
            var cards = SemanticConditionedDeckPresetV111.Apply(starter, preset);
            Assert.Equal(20, cards.Length);
            Assert.Equal(12, cards.Count(c =>
                c.CardId is "proto.silent.strike" or "proto.silent.defend" or
                    "proto.silent.neutralize" or "proto.silent.survivor"));
            Assert.All(cards, c => Assert.Equal(0, c.UpgradeLevel));
            Assert.Equal(cards.Length,
                cards.Select(c => c.InstanceId).Distinct().Count());
        }
    }
}
