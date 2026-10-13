using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeLavaRockBossRewardTests
{
    private static RunState ObtainLavaRock(string seed)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var world = state.World!;
        state = state with
        {
            World = world with
            {
                Event = world.Event! with
                {
                    OfferedChoiceIds =
                    [
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "LavaRock"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "CursedPearl")
                    ]
                }
            }
        };
        var engine = new PrototypeGameEngine();
        var take = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "LavaRock"));
        state = engine.Step(state, take).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState EnterBoss(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var world = state.World!;
        var map = world.Map;
        var boss = map.Nodes.Single(node =>
            node.RoomType == PrototypeRoomType.Boss);
        var parent = map.Nodes.First(node =>
            node.NextNodeIds?.Contains(boss.NodeId,
                StringComparer.Ordinal) == true);
        var history = Enumerable.Range(1, parent.Floor)
            .Select(floor =>
            {
                var node = floor == parent.Floor
                    ? parent
                    : map.Nodes.First(item => item.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, node.NodeId, node.RoomType);
            }).ToArray();
        state = state with
        {
            World = world with
            {
                Floor = parent.Floor,
                ActiveRoom = null,
                Event = null,
                CompletedRoomHistory = history,
                Map = map with { CurrentNodeId = parent.NodeId }
            }
        };
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state,
            GameAction.Create("choose_map_node",
                new ChooseMapNodePayload(boss.NodeId))).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(PrototypeRoomType.Boss, state.World!.ActiveRoom);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState WinBoss(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var combat = state.World!.Combat!;
        var strike = combat.Cards.First(card =>
            card.CardId == "proto.silent.strike");
        var others = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != strike.InstanceId).ToArray();
        var targetId = combat.Enemies[0].InstanceId;
        combat = combat with
        {
            Hand = [strike.InstanceId],
            DrawPile = others,
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with
                {
                    Hp = index == 0 ? 1 : 0,
                    Block = 0
                }).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        var action = engine.GetLegalActions(state).First(a =>
            a.Kind == "play_card"
            && a.ReadPayload<PlayCardPayload>().CardInstanceId
                == strike.InstanceId
            && a.ReadPayload<PlayCardPayload>().TargetEnemyId == targetId);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void LavaRockAddsTwoIndependentBossRelicRewardsExactlyOnce()
    {
        var state = WinBoss(EnterBoss(ObtainLavaRock("lava-rock-boss")));
        var reward = state.World!.Reward!;
        Assert.True(reward.EndsAct);
        Assert.Equal(3, reward.CurrentRelicOptions.Length);
        Assert.Equal(2, reward.ExtraRelicRewardIds?.Length);
        Assert.Equal(0, reward.ExtraRelicsResolved);
        Assert.Equal(2, reward.ExtraRelicRewardIds!
            .Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(reward.ExtraRelicRewardIds!,
            id => reward.CurrentRelicOptions.Contains(id));
        Assert.True(state.Player.Relics.Single(relic =>
            relic.RelicId ==
                PrototypeNativeOvergrowthEvents.NeowRelicId("LavaRock"))
            .PersistentState.GetProperty("HasTriggered").GetBoolean());
        var fork = state.Fork();
        Assert.NotSame(state.World!.Reward!.ExtraRelicRewardIds,
            fork.World!.Reward!.ExtraRelicRewardIds);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var engine = new PrototypeGameEngine();
        var relicsBefore = state.Player.Relics.Length;
        for (var i = 0; i < 30 && state.Phase == RunPhase.Reward; i++)
        {
            var legal = engine.GetLegalActions(state);
            var action = legal.FirstOrDefault(a =>
                a.Kind == "skip_reward_card")
                ?? legal.FirstOrDefault(a =>
                    a.Kind == "skip_reward_potion")
                ?? legal.FirstOrDefault(a =>
                    a.Kind == "take_reward_relic")
                ?? legal.FirstOrDefault(a =>
                    a.Kind == "choose_relic_deck_card")
                ?? legal.Single(a => a.Kind == "leave_reward");
            state = engine.Step(state, action).State;
            if (state.Phase == RunPhase.Reward)
            {
                PrototypeStateInvariants.Validate(state);
            }
        }

        Assert.Equal(RunPhase.ActTransition, state.Phase);
        Assert.Equal(relicsBefore + 3, state.Player.Relics.Length);
        Assert.All(reward.ExtraRelicRewardIds!, id =>
            Assert.Contains(state.Player.Relics, relic =>
                relic.RelicId == id));
        Assert.Equal(16, state.World!.Floor);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LavaRockDoesNotTriggerTwiceIfAlreadyConsumed()
    {
        var state = ObtainLavaRock("lava-rock-consumed");
        var id = PrototypeNativeOvergrowthEvents.NeowRelicId("LavaRock");
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Select(relic =>
                    relic.RelicId == id
                        ? relic with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { HasTriggered = true })
                        }
                        : relic).ToArray()
            }
        };
        state = WinBoss(EnterBoss(state));
        Assert.Empty(state.World!.Reward!.ExtraRelicRewardIds
            ?? Array.Empty<string>());
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CrossCharacterKaleidoscopeIsExcludedFromSilentOnlyOffers()
    {
        Assert.Contains("Kaleidoscope",
            PrototypeNativeOvergrowthEvents.NeowPositiveRelicNames);
        for (var i = 0; i < 300; i++)
        {
            var state = PrototypeNativeOvergrowthRunFactory.Create(
                "silent-no-cross-character-" + i);
            Assert.DoesNotContain(
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "Kaleidoscope"),
                state.World!.Event!.OfferedChoiceIds!);
            PrototypeStateInvariants.Validate(state);
        }
    }
}
