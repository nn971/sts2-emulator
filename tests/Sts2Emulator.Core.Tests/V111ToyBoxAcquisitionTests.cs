using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111ToyBoxAcquisitionTests
{
    [Theory]
    [InlineData("shop", false)]
    [InlineData("reward", false)]
    [InlineData("extra", false)]
    [InlineData("independent", false)]
    [InlineData("boss", false)]
    [InlineData("reward", true)]
    public void WaxRewardsSuspendAndReturnToAcquisitionContext(string route, bool takeOne)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("toy-box-acquisition-" + route);
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var reward = new RewardState(route == "boss" ? "Boss" : "Combat", [], null,
            route is "reward" or "boss" ? NativeAncientRewardsV111.ToyBoxId : null,
            true, true, route is not ("reward" or "boss"), route == "boss",
            ExtraRelicRewardIds: route == "independent" ? ["proto.relic.anchor", NativeAncientRewardsV111.ToyBoxId]
                : route == "extra" ? [NativeAncientRewardsV111.ToyBoxId] : null,
            IndependentSelection: route == "independent",
            ExtraCardGroupsResolved: route == "independent" ? [] : null,
            ExtraRelicGroupsResolved: route == "independent" ? [false, false] : null);
        state = state with
        {
            Phase = route == "shop" ? RunPhase.Shop : RunPhase.Reward,
            Player = state.Player with { Gold = 100 },
            World = state.World! with
            {
                Floor = 1, Map = new MapState([]),
                ActiveRoom = route == "shop" ? PrototypeRoomType.Shop
                    : route == "boss" ? PrototypeRoomType.Boss : PrototypeRoomType.Combat,
                Reward = route == "shop" ? null : reward,
                Shop = route == "shop" ? new ShopState([], null,
                    new ShopOffer(7, NativeAncientRewardsV111.ToyBoxId, 50, false)) : null
            }
        };
        var originalHash = CanonicalJson.Sha256(state);
        var action = engine.GetLegalActions(state).Single(action => action.Kind == (route switch
        {
            "shop" => "buy_relic",
            "independent" => "take_reward_extra_relic",
            _ => "take_reward_relic"
        }));
        var offered = engine.Step(state, action).State;
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(offered), CanonicalJson.Sha256(
            engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        Assert.Equal(RunPhase.Reward, offered.Phase);
        Assert.Equal("Relic:ToyBox", offered.World!.Reward!.SourceRoom);
        Assert.Equal(5, offered.World.Reward.ExtraRelicRewardIds!.Length);
        Assert.All(offered.World.Reward.ExtraRelicWaxFlags!, Assert.True);
        Assert.Empty(offered.World.CompletedRooms);
        Assert.Equal(state.World.NextCardInstanceId, offered.World.NextCardInstanceId);
        var saved = offered.World.CustomRewardReturn!;
        Assert.Equal(route == "shop" ? RunPhase.Shop : RunPhase.Reward, saved.Phase);
        var fork = offered.Fork();
        if (saved.Reward?.ExtraRelicGroupsResolved is not null)
        {
            fork.World!.CustomRewardReturn!.Reward!.ExtraRelicGroupsResolved![0] = true;
            Assert.False(saved.Reward.ExtraRelicGroupsResolved[0]);
        }
        PrototypeStateInvariants.Validate(offered);
        if (takeOne)
        {
            var take = engine.GetLegalActions(offered).First(action => action.Kind == "take_reward_relic");
            offered = engine.Step(offered, take).State;
            Assert.True(offered.Player.Relics.Last().IsWax);
        }
        var leave = GameAction.Empty("leave_reward");
        var restored = engine.Step(offered, leave).State;
        Assert.Equal(CanonicalJson.Sha256(restored), CanonicalJson.Sha256(
            engine.Step(RunSnapshot.Load(RunSnapshot.Save(offered)), leave).State));
        Assert.Null(restored.World!.CustomRewardReturn);
        Assert.Equal(saved.Phase, restored.Phase);
        Assert.Equal(offered.Player.Gold, restored.Player.Gold);
        Assert.Equal(takeOne ? 1 : 0, restored.Player.Relics.Count(relic => relic.IsWax));
        Assert.Equal(offered.World!.RelicBag!.Remaining, restored.World.RelicBag!.Remaining);
        if (route == "shop")
            Assert.True(restored.World.Shop!.RelicOffer!.Sold);
        else
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(saved.Reward),
                System.Text.Json.JsonSerializer.Serialize(restored.World.Reward));
        PrototypeStateInvariants.Validate(restored);
        if (route == "boss")
        {
            Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(offered with
            {
                World = offered.World! with
                { CustomRewardReturn = offered.World!.CustomRewardReturn! with { Reward = null } }
            }));
            var transitioned = engine.Step(restored, leave).State;
            Assert.Equal(RunPhase.ActTransition, transitioned.Phase);
            Assert.Equal(restored.World.Act, transitioned.World!.Act);
        }
    }
}
