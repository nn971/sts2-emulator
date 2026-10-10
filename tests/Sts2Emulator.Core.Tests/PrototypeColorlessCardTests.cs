using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeColorlessCardTests
{
    [Fact]
    public void NativeColorlessCatalogRecordsAllSixtyFiveAndKeepsRewardsSilent()
    {
        Assert.Equal(65, PrototypeColorlessCards.NativePoolIds.Length);
        Assert.Equal(65, PrototypeColorlessCards.NativePoolIds
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(33, PrototypeColorlessCards.ImplementedShopPool.Length);
        Assert.All(PrototypeColorlessCards.ImplementedShopPool, id =>
        {
            Assert.Contains(id, PrototypeColorlessCards.NativePoolIds);
            var card = PrototypeContent.Card(id);
            Assert.True(card.MechanicsImplemented);
            Assert.False(card.MultiplayerOnly);
            Assert.True(card.Rarity is PrototypeCardRarity.Uncommon
                or PrototypeCardRarity.Rare);
            Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
        });
        Assert.Equal(24, PrototypeColorlessCards.Implemented.Count(
            card => card.Rarity == PrototypeCardRarity.Uncommon));
        Assert.Equal(9, PrototypeColorlessCards.Implemented.Count(
            card => card.Rarity == PrototypeCardRarity.Rare));
    }

    [Fact]
    public void MerchantColorlessSelectionUsesRarityAndForkableRng()
    {
        var rng = PrototypeRng.CreateBundle("colorless-shop-draw");
        var fork = rng.Fork();
        var a = PrototypeColorlessCards.PickMerchantCard(
            PrototypeCardRarity.Uncommon, rng);
        var b = PrototypeColorlessCards.PickMerchantCard(
            PrototypeCardRarity.Uncommon, fork);
        Assert.Equal(a, b);
        Assert.Equal(PrototypeCardRarity.Uncommon, PrototypeContent.Card(a).Rarity);

        var rare = PrototypeColorlessCards.PickMerchantCard(
            PrototypeCardRarity.Rare, rng);
        Assert.Equal(PrototypeCardRarity.Rare, PrototypeContent.Card(rare).Rarity);
        Assert.Contains(rare, PrototypeColorlessCards.ImplementedShopPool);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeColorlessCards.PickMerchantCard(
                PrototypeCardRarity.Common, rng));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeColorlessCards.PickMerchantCard(
                PrototypeCardRarity.Rare, rng,
                PrototypeColorlessCards.Implemented
                    .Where(card => card.Rarity == PrototypeCardRarity.Rare)
                    .Select(card => card.Id).ToArray()));
    }

    [Theory]
    [InlineData("proto.colorless.finesse", 0, 4, 1, 3, false)]
    [InlineData("proto.colorless.flash_of_steel", 5, 0, 1, 3, false)]
    [InlineData("proto.colorless.dramatic_entrance", 11, 0, 0, 3, true)]
    [InlineData("proto.colorless.ultimate_strike", 14, 0, 0, 2, false)]
    [InlineData("proto.colorless.ultimate_defend", 0, 11, 0, 2, false)]
    [InlineData("proto.colorless.master_of_strategy", 0, 0, 3, 3, true)]
    public void SourceBackedColorlessCardResolvesUsingGenericCombatEffects(
        string cardId, int damage, int block, int drawn,
        int expectedEnergy, bool exhausted)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = new[]
        {
            new CardInstance(1, cardId, 0, empty),
            new CardInstance(2, "proto.silent.strike", 0, empty),
            new CardInstance(3, "proto.silent.defend", 0, empty),
            new CardInstance(4, "proto.silent.backflip", 0, empty)
        };
        var cards = deck.Select((card, index) =>
            new CombatCardInstance(
                index + 1, card.InstanceId, card.CardId, 0, false, empty))
            .ToArray();
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: [2, 3, 4],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 5,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);
        var seed = "colorless-effect-" + cardId;
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            new PlayerState(70, 70, 100, deck, [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat, null, null, null, null));

        var engine = new PrototypeGameEngine();
        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        var result = state.World!.Combat!;
        Assert.Equal(100 - damage, Assert.Single(result.Enemies).Hp);
        Assert.Equal(block, result.PlayerBlock);
        Assert.Equal(expectedEnergy, result.Energy);
        Assert.Equal(drawn, result.Hand.Length);
        Assert.Equal(exhausted, result.ExhaustPile.Contains(1));
        Assert.Equal(!exhausted, result.DiscardPile.Contains(1));
    }

    [Fact]
    public void NativeColorlessUpgradesAreDeclarativeAndInnateIsPreserved()
    {
        Assert.True(PrototypeContent.Card(
            "proto.colorless.dramatic_entrance").Innate);
        Assert.True(PrototypeContent.Card(
            "proto.colorless.dramatic_entrance").ExhaustOnUse);
        Assert.Equal(3, PrototypeContent.Card(
            "proto.colorless.finesse").Effects[0].UpgradeDelta);
        Assert.Equal(4, PrototypeContent.Card(
            "proto.colorless.master_of_strategy").Effects[0].Amount
            + PrototypeContent.Card(
                "proto.colorless.master_of_strategy").Effects[0].UpgradeDelta);
    }
}
