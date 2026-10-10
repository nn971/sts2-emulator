using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeShopRelicDeckChoiceTests
{
    [Fact]
    public void EmptyCagePurchasePaysOnceThenRemovesTwoPersistentCards()
    {
        var engine = new PrototypeGameEngine();
        var state = ShopStateForRelic(
            "proto.relic.empty_cage",
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.neutralize")
            ]);

        state = BuyRelic(engine, state);
        Assert.Equal(RunPhase.Shop, state.Phase);
        Assert.Equal(380, state.Player.Gold);
        Assert.Contains(state.Player.Relics,
            relic => relic.RelicId == "proto.relic.empty_cage");
        Assert.True(state.World!.Shop!.RelicOffer!.Sold);
        Assert.Equal(2,
            state.World.Shop.PendingDeckChoice!.RemainingSelections);
        Assert.Equal(new long[] { 1, 2, 3 },
            state.World.Shop.PendingDeckChoice.CandidateCardInstanceIds);
        Assert.Equal(
            PrototypePersistentDeckChoiceKind.Remove,
            state.World.Shop.PendingDeckChoice.Kind);

        var observation = new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            "proto.relic.empty_cage",
            observation.Observation.Shop!.PendingDeckChoice!.SourceRelicId);
        Assert.Equal(3, observation.LegalActions.Count(action =>
            action.Kind == "choose_shop_relic_deck_card"));
        Assert.DoesNotContain(observation.LegalActions,
            action => action.Kind is "leave_shop" or "buy_card" or "buy_relic");

        Assert.Throws<InvalidOperationException>(() =>
            engine.Step(state, GameAction.Empty("leave_shop")));

        var fork = state.Fork();
        fork.World!.Shop!.PendingDeckChoice!.CandidateCardInstanceIds[0] = 999;
        Assert.Equal(1,
            state.World.Shop.PendingDeckChoice.CandidateCardInstanceIds[0]);

        state = ChooseDeckCard(engine, state, 1);
        Assert.Equal(380, state.Player.Gold);
        Assert.Equal(1,
            state.World!.Shop!.PendingDeckChoice!.RemainingSelections);
        Assert.DoesNotContain(1,
            state.World.Shop.PendingDeckChoice.CandidateCardInstanceIds);

        state = ChooseDeckCard(engine, state, 2);
        Assert.Null(state.World!.Shop!.PendingDeckChoice);
        Assert.Equal(new long[] { 3 },
            state.Player.Deck.Select(card => card.InstanceId).ToArray());
        Assert.Equal(380, state.Player.Gold);
        Assert.Single(state.Player.Relics);

        // The shop resumes after the purchase continuation and remains usable.
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "leave_shop");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void AstrolabePurchaseTransformsAndUpgradesBeforeShopResumes()
    {
        var engine = new PrototypeGameEngine();
        var deck = new[]
        {
            Card(1, "proto.silent.strike"),
            Card(2, "proto.silent.defend"),
            Card(3, "proto.silent.survivor"),
            Card(4, "proto.silent.neutralize")
        };
        var state = BuyRelic(engine,
            ShopStateForRelic("proto.relic.astrolabe", deck));
        Assert.Equal(3,
            state.World!.Shop!.PendingDeckChoice!.RemainingSelections);

        foreach (var id in new long[] { 1, 2, 3 })
        {
            state = ChooseDeckCard(engine, state, id);
            PrototypeStateInvariants.Validate(state);
        }

        Assert.Null(state.World!.Shop!.PendingDeckChoice);
        foreach (var id in new long[] { 1, 2, 3 })
        {
            var transformed = state.Player.Deck
                .Single(card => card.InstanceId == id);
            Assert.NotEqual(
                deck.Single(card => card.InstanceId == id).CardId,
                transformed.CardId);
            Assert.Contains(transformed.CardId, PrototypeContent.RewardCardPool);
            Assert.Equal(1, transformed.UpgradeLevel);
        }

        var unchanged = state.Player.Deck.Single(card => card.InstanceId == 4);
        Assert.Equal("proto.silent.neutralize", unchanged.CardId);
        Assert.Equal(0, unchanged.UpgradeLevel);
        Assert.Equal(380, state.Player.Gold);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "leave_shop");
    }

    [Fact]
    public void NoEligibleCardsSkipsShopContinuationWithoutChargingAgain()
    {
        var engine = new PrototypeGameEngine();
        var state = BuyRelic(engine,
            ShopStateForRelic("proto.relic.empty_cage", []));

        Assert.Equal(380, state.Player.Gold);
        Assert.Null(state.World!.Shop!.PendingDeckChoice);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "leave_shop");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void InvariantsRejectStaleShopDeckChoiceCandidates()
    {
        var engine = new PrototypeGameEngine();
        var state = BuyRelic(engine,
            ShopStateForRelic("proto.relic.empty_cage",
            [
                Card(1, "proto.silent.strike")
            ]));
        Assert.NotNull(state.World!.Shop!.PendingDeckChoice);
        var corrupted = state with
        {
            World = state.World with
            {
                Shop = state.World.Shop with
                {
                    PendingDeckChoice = state.World.Shop.PendingDeckChoice with
                    {
                        CandidateCardInstanceIds = [999]
                    }
                }
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(corrupted));
    }

    private static RunState BuyRelic(
        PrototypeGameEngine engine, RunState state)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "buy_relic");
        return engine.Step(state, action).State;
    }

    private static RunState ChooseDeckCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "choose_shop_relic_deck_card"
            && item.ReadPayload<ChooseDeckCardPayload>().CardInstanceId
                == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static CardInstance Card(long id, string cardId) =>
        new(id, cardId, 0, PrototypeJson.EmptyObject());

    private static RunState ShopStateForRelic(
        string relicId,
        CardInstance[] deck)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70, 70, 500, deck, [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "shop-relic-choice",
            "shop-relic-choice",
            0,
            RunPhase.Shop,
            player,
            PrototypeRng.CreateBundle("shop-relic-choice"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                deck.Select(card => card.InstanceId)
                    .DefaultIfEmpty(0).Max() + 1,
                PrototypeRoomType.Shop,
                new MapState([]),
                null,
                null,
                new ShopState(
                    [],
                    null,
                    new ShopOffer(200, relicId, 120, Sold: false)),
                null,
                null));
    }
}
