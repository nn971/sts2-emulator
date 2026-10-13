using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelWingTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Rewards(bool independent)
    {
        var fixture = NativePaelRewardsV111.CreateRewardFixture("wing-acquisition", NativePaelRewardsV111.WingId);
        var acquired = Engine.Step(fixture, Assert.Single(Engine.GetLegalActions(fixture))).State;
        Assert.Equal(0, NativePaelRewardsV111.RewardsSacrificed(acquired.Player.Relics.Last()));
        var state = PrototypeGameFactory.Create("wing-rewards");
        state = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        return state with
        {
            Phase = RunPhase.Reward,
            Player = acquired.Player,
            World = state.World! with
            {
                Floor = 1, Map = new MapState([]), ActiveRoom = PrototypeRoomType.Combat,
                RelicBag = new([], [], ["proto.relic.old_coin"], []),
                Reward = new("Combat", [PrototypeContent.RewardCardPool[0]], null, null, false, true, true, false,
                    ExtraCardOptions: [[PrototypeContent.RewardCardPool[1]], [PrototypeContent.RewardCardPool[2]]],
                    IndependentSelection: independent,
                    ExtraCardGroupsResolved: independent ? [false, false] : null,
                    ExtraRelicGroupsResolved: independent ? [] : null)
            }
        };
    }

    private static RunState Step(RunState state, GameAction action)
    {
        var hash = CanonicalJson.Sha256(state);
        var next = Engine.Step(state, action).State;
        Assert.Equal(hash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(next), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        PrototypeStateInvariants.Validate(next);
        return next;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EverySecondSacrificeObtainsFrontRelicWithItsPickupEffect(bool independent)
    {
        var state = Rewards(independent);
        var gold = state.Player.Gold;
        var deck = state.Player.Deck.Length;
        var rng = JsonSerializer.Serialize(state.Rng);
        state = Step(state, Engine.GetLegalActions(state).Single(action => action.Kind == "sacrifice_reward_card"));
        Assert.Equal(1, NativePaelRewardsV111.RewardsSacrificed(state.Player.Relics.Last()));
        Assert.Equal(rng, JsonSerializer.Serialize(state.Rng));
        var second = Engine.GetLegalActions(state).Single(action => action.Kind ==
            (independent ? "sacrifice_reward_card_group" : "sacrifice_reward_card"));
        state = Step(state, second);
        Assert.Equal(gold + 300, state.Player.Gold);
        Assert.Equal("proto.relic.old_coin", state.Player.Relics.Last().RelicId);
        Assert.False(state.Player.Relics.Last().IsWax);
        Assert.Equal(2, NativePaelRewardsV111.RewardsSacrificed(
            state.Player.Relics.Single(relic => relic.RelicId == NativePaelRewardsV111.WingId)));
        Assert.Empty(state.World!.RelicBag!.Remaining);
        Assert.Equal(deck, state.Player.Deck.Length);
        Assert.Equal(independent ? new[] { false, true } : new[] { true, false },
            Enumerable.Range(0, 2).Select(state.World.Reward!.IsExtraCardGroupResolved));
        state = Step(state, GameAction.Empty("skip_reward_card"));
        Assert.Equal(2, NativePaelRewardsV111.RewardsSacrificed(
            state.Player.Relics.Single(relic => relic.RelicId == NativePaelRewardsV111.WingId)));
    }

    [Fact]
    public void MissingOrMeltedWingCannotSacrificeAndMalformedCounterCannotLoad()
    {
        var state = Rewards(false);
        var index = Array.FindIndex(state.Player.Relics, relic => relic.RelicId == NativePaelRewardsV111.WingId);
        var relics = (RelicInstance[])state.Player.Relics.Clone();
        relics[index] = relics[index] with { IsWax = true, IsMelted = true };
        var melted = state with { Player = state.Player with { Relics = relics } };
        Assert.DoesNotContain(Engine.GetLegalActions(melted), action => action.Kind.StartsWith("sacrifice_"));
        Assert.Throws<InvalidOperationException>(() => Engine.Step(melted, GameAction.Empty("sacrifice_reward_card")));
        relics[index] = relics[index] with
        { PersistentState = JsonSerializer.SerializeToElement(new { RewardsSacrificed = -1 }) };
        Assert.Throws<InvalidOperationException>(() => RunSnapshot.Save(
            state with { Player = state.Player with { Relics = relics } }));
    }
}
