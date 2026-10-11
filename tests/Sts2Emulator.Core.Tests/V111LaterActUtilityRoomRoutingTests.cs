using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActUtilityRoomRoutingTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeMerchantPopulatesSevenCardsThreePotionsThreeRelicsAndRemoval(
        int act)
    {
        var start = AtPredecessor(
            PrototypeNativeLaterActFixture.Create($"later-shop-{act}", act),
            PrototypeRoomType.Shop);
        start = start with
        {
            Player = start.Player with { Gold = 5000 }
        };
        var hash = CanonicalJson.Sha256(start);
        var entered = Enter(start, PrototypeRoomType.Shop);
        Assert.Equal(RunPhase.Shop, entered.Phase);
        var shop = Assert.IsType<ShopState>(entered.World!.Shop);
        Assert.Equal(7, shop.CardOffers.Length);
        Assert.Equal(3, shop.PotionOffers.Length);
        Assert.Equal(3, shop.RelicOffers.Length);
        Assert.Equal(1, shop.CardOffers.Count(o => o.OnSale));
        Assert.Equal(5, shop.CardOffers.Count(o => o.OfferId <= 5));
        Assert.Equal(2, shop.CardOffers.Count(o => o.OfferId is 6 or 7));
        Assert.Equal(0, entered.World.ShopRemovalsUsed);
        Assert.Equal(75, shop.RemovalPrice);
        Assert.Equal(hash, CanonicalJson.Sha256(start));
        Assert.Equal(CanonicalJson.Sha256(entered),
            CanonicalJson.Sha256(Enter(start.Fork(), PrototypeRoomType.Shop)));

        var potionOffer = shop.PotionOffers[0];
        var beforeGold = entered.Player.Gold;
        var bought = Step(entered, Engine.GetLegalActions(entered)
            .Single(a => a.Kind == "buy_potion"
                && a.ReadPayload<BuyOfferPayload>().OfferId == potionOffer.OfferId));
        Assert.Equal(RunPhase.Shop, bought.Phase);
        Assert.Equal(beforeGold - potionOffer.Price, bought.Player.Gold);
        Assert.Contains(bought.Player.PotionSlots, x =>
            x?.PotionId == potionOffer.ItemId);
        Assert.Single(bought.World!.Shop!.PotionOffers,
            o => o.OfferId == potionOffer.OfferId && o.Sold);
        var left = Step(bought, GameAction.Empty("leave_shop"));
        Assert.Equal(RunPhase.MapChoice, left.Phase);
        Assert.Single(left.World!.CompletedRooms);
        Assert.Equal(PrototypeRoomType.Shop,
            left.World.CompletedRooms[0].RoomType);
        Assert.Null(left.World.Shop);
        PrototypeStateInvariants.Validate(left);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeMerchantRemovalPricePersistsAcrossActAndAscension(int act)
    {
        var fixture = PrototypeNativeLaterActFixture.Create(
            $"shop-removal-{act}", act, ascension: 6);
        fixture = fixture with
        {
            Player = fixture.Player with { Gold = 900 }
        };
        var entered = Enter(AtPredecessor(fixture, PrototypeRoomType.Shop),
            PrototypeRoomType.Shop);
        Assert.Equal(100, entered.World!.Shop!.RemovalPrice);
        var card = entered.Player.Deck[0];
        var removed = Step(entered, GameAction.Create(
            "remove_card", new RemoveCardPayload(card.InstanceId)));
        Assert.Equal(800, removed.Player.Gold);
        Assert.Equal(1, removed.World!.ShopRemovalsUsed);
        Assert.DoesNotContain(removed.Player.Deck,
            x => x.InstanceId == card.InstanceId);
        PrototypeStateInvariants.Validate(removed);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeRestOnlyOffersHealSmithAndSupportedRelicOptions(int act)
    {
        var fixture = PrototypeNativeLaterActFixture.Create(
            $"rest-heal-{act}", act);
        fixture = fixture with
        {
            Player = fixture.Player with { Hp = 29 }
        };
        var entered = Enter(AtPredecessor(fixture, PrototypeRoomType.Rest),
            PrototypeRoomType.Rest);
        var actions = Engine.GetLegalActions(entered);
        Assert.Equal(RunPhase.Rest, entered.Phase);
        Assert.Contains(actions, a => a.Kind == "rest_heal");
        Assert.Contains(actions, a => a.Kind == "rest_upgrade");
        Assert.DoesNotContain(actions, a => a.Kind == "rest_train");
        Assert.Throws<NotSupportedException>(() =>
            Step(entered, GameAction.Empty("rest_train")));
        var initialHp = entered.Player.Hp;
        var healed = Step(entered, GameAction.Empty("rest_heal"));
        Assert.Equal(initialHp + 21, healed.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, healed.Phase);
        Assert.Single(healed.World!.CompletedRooms);
        Assert.Equal(PrototypeRoomType.Rest,
            healed.World.CompletedRooms[0].RoomType);
        PrototypeStateInvariants.Validate(healed);

        var upgradable = entered.Player.Deck.First(card =>
            card.UpgradeLevel < PrototypeContent.Card(card.CardId).MaxUpgradeLevel);
        var upgraded = Step(entered, GameAction.Create(
            "rest_upgrade", new UpgradeCardPayload(upgradable.InstanceId)));
        Assert.Equal(upgradable.UpgradeLevel + 1,
            upgraded.Player.Deck.Single(c =>
                c.InstanceId == upgradable.InstanceId).UpgradeLevel);
        Assert.Equal(initialHp, upgraded.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, upgraded.Phase);
        Assert.Equal(CanonicalJson.Sha256(upgraded),
            CanonicalJson.Sha256(Step(entered.Fork(),
                GameAction.Create("rest_upgrade",
                    new UpgradeCardPayload(upgradable.InstanceId)))));
        PrototypeStateInvariants.Validate(upgraded);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void FixedTreasureRowUsesPersistentNativeGrabBagAndReward(int act)
    {
        var fixture = PrototypeNativeLaterActFixture.Create(
            $"native-chest-{act}", act);
        var relics = new PrototypeRelicBagState(
            ["proto.relic.anchor"],
            ["proto.relic.nunchaku"],
            ["proto.relic.shuriken"], []);
        fixture = fixture with
        {
            World = fixture.World! with { RelicBag = relics }
        };
        var input = AtPredecessor(fixture, PrototypeRoomType.Treasure);
        var hash = CanonicalJson.Sha256(input);
        var opened = Enter(input, PrototypeRoomType.Treasure);
        Assert.Equal(RunPhase.Reward, opened.Phase);
        var reward = Assert.IsType<RewardState>(opened.World!.Reward);
        Assert.Equal("Treasure", reward.SourceRoom);
        Assert.NotNull(reward.RelicOption);
        Assert.Contains(reward.RelicOption!,
            new[] { "proto.relic.anchor", "proto.relic.nunchaku",
                "proto.relic.shuriken" });
        Assert.DoesNotContain(reward.RelicOption!,
            opened.World.RelicBag!.Remaining);
        Assert.Equal(2, opened.World.RelicBag.Remaining.Length);
        Assert.Equal(hash, CanonicalJson.Sha256(input));
        Assert.Equal(CanonicalJson.Sha256(opened),
            CanonicalJson.Sha256(Enter(input.Fork(), PrototypeRoomType.Treasure)));

        var picked = Step(opened, Engine.GetLegalActions(opened)
            .Single(a => a.Kind == "take_reward_relic"));
        Assert.Contains(picked.Player.Relics,
            r => r.RelicId == reward.RelicOption);
        var finished = Step(picked, GameAction.Empty("leave_reward"));
        Assert.Equal(RunPhase.MapChoice, finished.Phase);
        Assert.Single(finished.World!.CompletedRooms);
        Assert.Equal(PrototypeRoomType.Treasure,
            finished.World.CompletedRooms[0].RoomType);
        Assert.Null(finished.World.Reward);
        PrototypeStateInvariants.Validate(finished);
    }

    [Theory]
    [InlineData(2, "Shop")]
    [InlineData(2, "Treasure")]
    [InlineData(3, "Shop")]
    [InlineData(3, "Treasure")]
    public void NativeUnknownResolutionCanReachSharedUtilityRooms(
        int act, string outcome)
    {
        var room = outcome == "Shop"
            ? PrototypeRoomType.Shop : PrototypeRoomType.Treasure;
        var fixture = PrototypeNativeLaterActFixture.Create(
            $"unknown-{act}-{outcome}", act);
        var staged = AtPredecessor(fixture, PrototypeRoomType.Unknown);
        staged = staged with
        {
            World = staged.World! with
            {
                UnknownRoomOdds = room == PrototypeRoomType.Shop
                    ? new PrototypeUnknownRoomOddsState(0, 0, 1000)
                    : new PrototypeUnknownRoomOddsState(0, 1000, 0)
            }
        };
        var hash = CanonicalJson.Sha256(staged);
        var result = Enter(staged, PrototypeRoomType.Unknown);
        Assert.Equal(room, result.World!.ActiveRoom);
        Assert.Equal(room == PrototypeRoomType.Shop
            ? RunPhase.Shop : RunPhase.Reward, result.Phase);
        Assert.Equal(hash, CanonicalJson.Sha256(staged));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Enter(staged.Fork(),
                PrototypeRoomType.Unknown)));
        PrototypeStateInvariants.Validate(result);
    }

    private static RunState AtPredecessor(RunState initial,
        PrototypeRoomType room)
    {
        var world = initial.World!;
        var target = world.Map.Nodes.First(n => n.RoomType == room
            && world.Map.Nodes.Any(p =>
                p.NextNodeIds?.Contains(n.NodeId) == true));
        var parent = world.Map.Nodes.First(n =>
            n.NextNodeIds?.Contains(target.NodeId) == true);
        return initial with
        {
            World = world with
            {
                Floor = parent.Floor,
                Map = world.Map with { CurrentNodeId = parent.NodeId }
            },
            Phase = RunPhase.MapChoice
        };
    }

    private static RunState Enter(RunState state, PrototypeRoomType room)
    {
        var target = state.World!.Map.AvailableNodes()
            .First(n => n.RoomType == room);
        return Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(target.NodeId)));
    }

    private static RunState Step(RunState state, GameAction action) =>
        Engine.Step(state, action).State;
}
