using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111StorybookAcquisitionTests
{
    [Theory]
    [InlineData("reward", false)]
    [InlineData("reward", true)]
    [InlineData("extra", false)]
    [InlineData("independent", false)]
    [InlineData("shop", false)]
    [InlineData("shop", true)]
    public void PickupGrantsOnePhysicalCardThroughSharedCardAddedHooks(string route, bool alreadyOwned)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("storybook-acquisition-" + route);
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        var relics = new List<RelicInstance> { new("proto.relic.toxic_egg", empty) };
        var deck = state.Player.Deck;
        var nextId = state.World!.NextCardInstanceId;
        CardInstance? oldFlame = null;
        if (alreadyOwned)
        {
            relics.Add(new(NativeAncientRewardsV111.StorybookId, empty));
            oldFlame = new(nextId++, NativeAncientRewardsV111.BrightestFlameId, 0, empty);
            deck = [..deck, oldFlame];
        }
        var reward = new RewardState("Combat", [], null,
            route == "reward" ? NativeAncientRewardsV111.StorybookId : null,
            CardResolved: true, PotionResolved: true, RelicResolved: route != "reward", EndsAct: false,
            ExtraRelicRewardIds: route == "independent" ? ["proto.relic.anchor", NativeAncientRewardsV111.StorybookId]
                : route == "extra" ? [NativeAncientRewardsV111.StorybookId] : null,
            IndependentSelection: route == "independent",
            ExtraCardGroupsResolved: route == "independent" ? [] : null,
            ExtraRelicGroupsResolved: route == "independent" ? [false, false] : null);
        state = state with
        {
            Phase = route == "shop" ? RunPhase.Shop : RunPhase.Reward,
            Player = state.Player with { Deck = deck, Relics = relics.ToArray(), Gold = 100 },
            World = state.World with
            {
                Floor = 1, NextCardInstanceId = nextId,
                Map = new MapState([]),
                ActiveRoom = route == "shop" ? PrototypeRoomType.Shop : PrototypeRoomType.Combat,
                Reward = route == "shop" ? null : reward,
                Shop = route == "shop" ? new ShopState([], null,
                    new ShopOffer(7, NativeAncientRewardsV111.StorybookId, 50, false)) : null
            }
        };
        var hash = CanonicalJson.Sha256(state);
        var action = engine.GetLegalActions(state).Single(action => action.Kind == (route switch
        {
            "shop" => "buy_relic",
            "independent" => "take_reward_extra_relic",
            _ => "take_reward_relic"
        }));
        var result = engine.Step(state, action).State;
        Assert.Equal(hash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        var granted = Assert.Single(result.Player.Deck, card => card.InstanceId == nextId);
        Assert.Equal(NativeAncientRewardsV111.BrightestFlameId, granted.CardId);
        Assert.Equal(1, granted.UpgradeLevel);
        Assert.Equal(nextId + 1, result.World!.NextCardInstanceId);
        Assert.Equal(state.Player.Deck.Length + 1, result.Player.Deck.Length);
        Assert.Equal(alreadyOwned ? 2 : 1, result.Player.Relics.Count(relic => relic.RelicId == NativeAncientRewardsV111.StorybookId));
        if (oldFlame is not null)
            Assert.Equal(oldFlame, result.Player.Deck.Single(card => card.InstanceId == oldFlame.InstanceId));
        Assert.Equal(route == "shop" ? 50 : 100, result.Player.Gold);
        PrototypeStateInvariants.Validate(result);
    }
}
