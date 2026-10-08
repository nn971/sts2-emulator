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


    [Theory]
    [InlineData(0, 0, 75)]
    [InlineData(1, 0, 100)]
    [InlineData(3, 0, 150)]
    [InlineData(0, 6, 100)]
    [InlineData(1, 6, 150)]
    [InlineData(3, 6, 250)]
    [InlineData(1, 10, 150)]
    public void GeneratedShopRemovalPriceUsesRunWideCountAndInflation(
        int priorRemovals, int ascension, int expectedPrice)
    {
        var state = EnterGeneratedShop(
            gold: 999,
            relicIds: [],
            priorRemovals: priorRemovals,
            ascension: ascension,
            removalCards: true);

        Assert.Equal(expectedPrice, state.World!.Shop!.RemovalPrice);
        Assert.Equal(expectedPrice, state.World.Shop.BaseRemovalPrice);
        Assert.Equal(priorRemovals, state.World.ShopRemovalsUsed);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ShopRemovalAdvancesRunWideCountExactlyOnce()
    {
        var engine = new PrototypeGameEngine();
        var state = EnterGeneratedShop(
            gold: 999,
            relicIds: [],
            priorRemovals: 1,
            removalCards: true);
        var oldCardCount = state.Player.Deck.Length;
        var oldPrice = state.World!.Shop!.RemovalPrice;
        var initialGold = state.Player.Gold;
        var remove = engine.GetLegalActions(state)
            .Single(action => action.Kind == "remove_card"
                && action.ReadPayload<RemoveCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, remove).State;

        Assert.Equal(2, state.World!.ShopRemovalsUsed);
        Assert.True(state.World.Shop!.RemovalUsed);
        Assert.Equal(initialGold - oldPrice, state.Player.Gold);
        Assert.Equal(oldCardCount - 1, state.Player.Deck.Length);
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind == "remove_card");
        Assert.Throws<InvalidOperationException>(() => engine.Step(state, remove));
        Assert.Equal(2, state.World.ShopRemovalsUsed);
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(
            state, GameAction.Empty("leave_shop")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(2, state.World!.ShopRemovalsUsed);
        PrototypeStateInvariants.Validate(state);

        // This is the next merchant's generation state, with the
        // run-wide counter carried over rather than an act multiplier.
        var later = EnterGeneratedShop(
            gold: 999,
            relicIds: [],
            priorRemovals: state.World.ShopRemovalsUsed,
            removalCards: true);
        Assert.Equal(125, later.World!.Shop!.RemovalPrice);
    }

    [Fact]
    public void NegativeRunWideShopRemovalCountIsInvalid()
    {
        var state = EnterGeneratedShop(
            gold: 999, relicIds: []);
        state = state with
        {
            World = state.World! with { ShopRemovalsUsed = -1 }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(state));
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
        string[] relicIds,
        int priorRemovals = 0,
        int ascension = 0,
        bool removalCards = false)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            gold,
            removalCards
                ? [
                    new CardInstance(1, "proto.silent.strike", 0, empty),
                    new CardInstance(2, "proto.silent.defend", 0, empty)
                ]
                : [],
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
                removalCards ? 3 : 1,
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
                            ["floor3"]),
                        new MapNodeState(
                            "floor3",
                            1,
                            3,
                            PrototypeRoomType.Combat,
                            ["floor4"]),
                        new MapNodeState(
                            "floor4",
                            1,
                            4,
                            PrototypeRoomType.Combat,
                            ["rest"]),
                        new MapNodeState(
                            "rest",
                            1,
                            5,
                            PrototypeRoomType.Rest,
                            ["boss"]),
                        new MapNodeState(
                            "boss",
                            1,
                            6,
                            PrototypeRoomType.Boss,
                            [])
                    ],
                    CurrentNodeId: "entry",
                    EntryNodeIds: ["entry"]),
                null,
                null,
                null,
                null,
                null,
                ShopRemovalsUsed: priorRemovals),
            Ascension: ascension);

        var engine = new PrototypeGameEngine();
        return engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("shop"))).State;
    }
}
