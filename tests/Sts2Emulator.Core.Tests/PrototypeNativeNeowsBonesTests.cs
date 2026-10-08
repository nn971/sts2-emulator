using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeNeowsBonesTests
{
    private static RunState ChooseNeowsBones(string seed)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var world = state.World!;
        var id = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "NeowsBones");
        state = state with
        {
            World = world with
            {
                Event = world.Event! with
                {
                    OfferedChoiceIds =
                    [
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "GoldenPearl"),
                        "take_" + id
                    ]
                }
            }
        };
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + id);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Theory]
    [InlineData("bones-000")]
    [InlineData("bones-001")]
    [InlineData("bones-002")]
    [InlineData("bones-003")]
    [InlineData("bones-004")]
    [InlineData("bones-005")]
    [InlineData("bones-006")]
    [InlineData("bones-007")]
    public void NeowsBonesGrantsTwoDistinctEligibleNeowRelicsThenCurse(
        string seed)
    {
        var state = ChooseNeowsBones(seed);
        var reward = state.World!.Reward!;
        Assert.Equal(2, reward.ExtraRelicRewardIds?.Length);
        Assert.Equal(0, reward.ExtraRelicsResolved);
        Assert.True(reward.CardResolved);
        Assert.True(reward.PotionResolved);
        Assert.True(reward.RelicResolved);
        Assert.Null(reward.RelicOption);
        Assert.Equal("proto.native.neow.injury", reward.AddCardAfterReward);
        Assert.DoesNotContain(state.Player.Deck,
            c => c.CardId == "proto.native.neow.injury");
        Assert.Empty(state.World.CompletedRooms);

        var ids = reward.ExtraRelicRewardIds!;
        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id =>
        {
            Assert.StartsWith("proto.native.neow.", id,
                StringComparison.Ordinal);
            Assert.DoesNotContain(id,
                new[]
                {
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "NeowsBones"),
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "Kaleidoscope"),
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "MassiveScroll")
                });
        });

        var fork = state.Fork();
        Assert.NotSame(state.World.Reward!.ExtraRelicRewardIds,
            fork.World!.Reward!.ExtraRelicRewardIds);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var engine = new PrototypeGameEngine();
        for (var i = 0; i < 20 && state.Phase == RunPhase.Reward; i++)
        {
            var legal = engine.GetLegalActions(state);
            var action = legal.FirstOrDefault(a =>
                a.Kind == "choose_relic_deck_card")
                ?? legal.FirstOrDefault(a =>
                    a.Kind == "take_reward_relic")
                ?? legal.Single(a => a.Kind == "leave_reward");
            state = engine.Step(state, action).State;
            PrototypeStateInvariants.Validate(state);
            if (state.Phase == RunPhase.Reward)
            {
                Assert.DoesNotContain(state.Player.Deck,
                    c => c.CardId == "proto.native.neow.injury");
            }
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Null(state.World.Reward);
        Assert.Equal(0, state.World.Floor);
        Assert.Empty(state.World.CompletedRooms);
        Assert.All(ids, id => Assert.Contains(state.Player.Relics,
            relic => relic.RelicId == id));
        Assert.Equal(4, state.Player.Relics.Length);
        Assert.Single(state.Player.Deck, c =>
            c.CardId == "proto.native.neow.injury");
        Assert.Equal("proto.native.neow.injury",
            state.Player.Deck[^1].CardId);
        PrototypeStateInvariants.Validate(state);
    }
}
