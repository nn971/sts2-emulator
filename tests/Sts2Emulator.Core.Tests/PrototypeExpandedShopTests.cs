using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeExpandedShopTests
{
    [Fact]
    public void GeneratedShopHasTypedCardsAndThreePotionAndRelicOffers()
    {
        var engine = new PrototypeGameEngine();
        var state = EnterGeneratedShop(
            gold: 999,
            relicIds: []);

        var shop = state.World!.Shop!;
        Assert.Equal(5, shop.CardOffers.Length);
        Assert.Equal(
            2,
            shop.CardOffers.Count(offer =>
                PrototypeContent.Card(offer.ItemId).Type
                    == PrototypeCardType.Attack));
        Assert.Equal(
            2,
            shop.CardOffers.Count(offer =>
                PrototypeContent.Card(offer.ItemId).Type
                    == PrototypeCardType.Skill));
        Assert.Equal(
            1,
            shop.CardOffers.Count(offer =>
                PrototypeContent.Card(offer.ItemId).Type
                    == PrototypeCardType.Power));

        Assert.Equal(3, shop.PotionOffers.Length);
        Assert.Equal(
            3,
            shop.PotionOffers
                .Select(offer => offer.ItemId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(3, shop.RelicOffers.Length);
        Assert.Equal(
            3,
            shop.RelicOffers
                .Select(offer => offer.ItemId)
                .Distinct(StringComparer.Ordinal)
                .Count());

        var allOfferIds = shop.CardOffers
            .Concat(shop.PotionOffers)
            .Concat(shop.RelicOffers)
            .Select(offer => offer.OfferId)
            .ToArray();
        Assert.Equal(
            allOfferIds.Length,
            allOfferIds.Distinct().Count());

        var frame =
            new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            shop.PotionOffers,
            frame.Observation.Shop!.PotionOffers);
        Assert.Equal(
            shop.RelicOffers,
            frame.Observation.Shop.RelicOffers);

        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SecondaryPotionOfferCanBeBoughtIndependently()
    {
        var engine = new PrototypeGameEngine();
        var state = EnterGeneratedShop(
            gold: 999,
            relicIds: []);
        var before = state.World!.Shop!;
        var selected = before.PotionOffers[1];

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action =>
                    action.Kind == "buy_potion"
                    && action.ReadPayload<BuyOfferPayload>()
                        .OfferId == selected.OfferId)).State;

        Assert.Equal(
            999 - selected.Price,
            state.Player.Gold);
        Assert.Equal(
            selected.ItemId,
            Assert.Single(
                state.Player.PotionSlots,
                potion => potion is not null)!
                .PotionId);

        var after = state.World!.Shop!;
        Assert.True(
            after.PotionOffers
                .Single(offer =>
                    offer.OfferId == selected.OfferId)
                .Sold);
        Assert.False(after.PotionOffers[0].Sold);
        Assert.False(after.PotionOffers[2].Sold);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BuyingSecondaryMembershipCardRepricesAllRemainingOffers()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            1000,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var engine = new PrototypeGameEngine();
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "expanded-shop-reprice",
            "expanded-shop-reprice",
            0,
            RunPhase.Shop,
            player,
            PrototypeRng.CreateBundle(
                "expanded-shop-reprice"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                PrototypeRoomType.Shop,
                new MapState([]),
                null,
                null,
                new ShopState(
                    [
                        Offer(
                            1,
                            "proto.silent.backflip",
                            100)
                    ],
                    Offer(
                        100,
                        "proto.potion.strength",
                        60),
                    Offer(
                        200,
                        "proto.relic.anchor",
                        120),
                    RemovalPrice: 80,
                    BaseRemovalPrice: 80,
                    AdditionalPotionOffers:
                    [
                        Offer(
                            101,
                            "proto.potion.block",
                            50)
                    ],
                    AdditionalRelicOffers:
                    [
                        Offer(
                            201,
                            "proto.relic.membership_card",
                            140),
                        Offer(
                            202,
                            "proto.relic.vajra",
                            110)
                    ]),
                null,
                null));

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action =>
                    action.Kind == "buy_relic"
                    && action.ReadPayload<BuyOfferPayload>()
                        .OfferId == 201)).State;

        var shop = state.World!.Shop!;
        Assert.True(
            shop.RelicOffers
                .Single(offer => offer.OfferId == 201)
                .Sold);
        Assert.Equal(
            50,
            Assert.Single(shop.CardOffers).Price);
        Assert.Equal(
            new[] { 30, 25 },
            shop.PotionOffers
                .Select(offer => offer.Price)
                .ToArray());
        Assert.Equal(
            60,
            shop.RelicOffers
                .Single(offer => offer.OfferId == 200)
                .Price);
        Assert.Equal(
            55,
            shop.RelicOffers
                .Single(offer => offer.OfferId == 202)
                .Price);
        Assert.Equal(40, shop.RemovalPrice);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void OwnedRelicsAreExcludedFromGeneratedShopRelicOffers()
    {
        var owned = PrototypeContent.RelicPool
            .Take(3)
            .ToArray();
        var state = EnterGeneratedShop(
            gold: 999,
            relicIds: owned);

        Assert.DoesNotContain(
            state.World!.Shop!.RelicOffers,
            offer => owned.Contains(
                offer.ItemId,
                StringComparer.Ordinal));
    }

    private static ShopOffer Offer(
        int id,
        string itemId,
        int basePrice) =>
        new(
            id,
            itemId,
            basePrice,
            false,
            BasePrice: basePrice);

    private static RunState EnterGeneratedShop(
        int gold,
        string[] relicIds)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            gold,
            [],
            relicIds
                .Select(id =>
                    new RelicInstance(id, empty))
                .ToArray(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "expanded-shop",
            "expanded-shop",
            0,
            RunPhase.MapChoice,
            player,
            PrototypeRng.CreateBundle(
                "expanded-shop"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                null,
                new MapState(
                    [
                        new MapNodeState(
                            "entry",
                            1,
                            1,
                            PrototypeRoomType.Combat,
                            ["shop"]),
                        new MapNodeState(
                            "shop",
                            1,
                            2,
                            PrototypeRoomType.Shop,
                            [])
                    ],
                    CurrentNodeId: "entry",
                    EntryNodeIds: ["entry"]),
                null,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        return engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("shop"))).State;
    }
}
