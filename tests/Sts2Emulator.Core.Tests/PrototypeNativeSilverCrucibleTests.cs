using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeSilverCrucibleTests
{
    private static RunState ObtainCrucible(string seed)
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
                            "GoldenPearl"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "ArcaneScroll"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "SilverCrucible")
                    ]
                }
            }
        };
        var engine = new PrototypeGameEngine();
        var choice = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "SilverCrucible"));
        state = engine.Step(state, choice).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState StartAndWinFirstCombat(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var choice = engine.GetLegalActions(state)[0];
        state = engine.Step(state, choice).State;
        Assert.Equal(RunPhase.Combat, state.Phase);

        var combat = state.World!.Combat!;
        var attack = combat.Cards.Single(card =>
            card.CardId == "proto.silent.neutralize");
        var others = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != attack.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [attack.InstanceId],
            DrawPile = others,
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        var play = engine.GetLegalActions(state).First(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == attack.InstanceId);
        state = engine.Step(state, play).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void SilverCrucibleUpgradesFirstThreeRewardGroupsEvenWhenSkipped(
        int alreadyUsed, bool shouldUpgrade)
    {
        var state = ObtainCrucible(
            "silver-reward-group-" + alreadyUsed);
        var silverId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "SilverCrucible");
        state = state with
        {
            Player = state.Player with
            {
                Relics = state.Player.Relics.Select(relic =>
                    relic.RelicId == silverId
                        ? relic with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { TimesUsed = alreadyUsed,
                                    TreasureRoomsEntered = 0 })
                        }
                        : relic).ToArray()
            }
        };
        state = StartAndWinFirstCombat(state);
        var reward = state.World!.Reward!;
        Assert.Equal(shouldUpgrade, reward.CardOptionsUpgraded);
        Assert.Equal(0,
            reward.ExtraCardOptionsUpgraded?.Length);
        var relic = state.Player.Relics.Single(relic =>
            relic.RelicId == silverId);
        Assert.Equal(Math.Min(3, alreadyUsed + 1),
            relic.PersistentState.GetProperty("TimesUsed").GetInt32());

        var engine = new PrototypeGameEngine();
        var take = engine.GetLegalActions(state).First(action =>
            action.Kind == "take_reward_card");
        var picked = engine.Step(state, take).State;
        Assert.Equal(shouldUpgrade ? 1 : 0,
            picked.Player.Deck[^1].UpgradeLevel);
        PrototypeStateInvariants.Validate(picked);

        // The counter advances at offer-generation time even if skipped.
        var skip = engine.GetLegalActions(state).Single(action =>
            action.Kind == "skip_reward_card");
        var skipped = engine.Step(state, skip).State;
        var sameRelic = skipped.Player.Relics.Single(relic =>
            relic.RelicId == silverId);
        Assert.Equal(Math.Min(3, alreadyUsed + 1),
            sameRelic.PersistentState.GetProperty("TimesUsed").GetInt32());
        Assert.Equal(13, skipped.Player.Deck.Length);
        PrototypeStateInvariants.Validate(skipped);
    }

    [Fact]
    public void SilverCrucibleSuppressesFirstTreasureButNotLaterTreasure()
    {
        var first = ObtainCrucible("silver-treasure-first");
        first = BeforeTreasure(first);
        var engine = new PrototypeGameEngine();
        var treasureNode = first.World!.Map.AvailableNodes().Single(
            node => node.RoomType == PrototypeRoomType.Treasure);
        var choice = GameAction.Create(
            "choose_map_node",
            new ChooseMapNodePayload(treasureNode.NodeId));
        first = engine.Step(first, choice).State;
        Assert.Equal(RunPhase.MapChoice, first.Phase);
        Assert.Null(first.World!.Reward);
        Assert.Contains(first.World.CompletedRooms,
            completed => completed.NodeId == treasureNode.NodeId
                && completed.RoomType == PrototypeRoomType.Treasure);
        var silverId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "SilverCrucible");
        var relic = first.Player.Relics.Single(item =>
            item.RelicId == silverId);
        Assert.Equal(1, relic.PersistentState
            .GetProperty("TreasureRoomsEntered").GetInt32());
        PrototypeStateInvariants.Validate(first);

        var second = ObtainCrucible("silver-treasure-second");
        second = second with
        {
            Player = second.Player with
            {
                Relics = second.Player.Relics.Select(item =>
                    item.RelicId == silverId
                        ? item with
                        {
                            PersistentState = JsonSerializer.SerializeToElement(
                                new { TimesUsed = 0,
                                    TreasureRoomsEntered = 1 })
                        }
                        : item).ToArray()
            }
        };
        second = BeforeTreasure(second);
        treasureNode = second.World!.Map.AvailableNodes().Single(
            node => node.RoomType == PrototypeRoomType.Treasure);
        second = engine.Step(second, GameAction.Create(
            "choose_map_node",
            new ChooseMapNodePayload(treasureNode.NodeId))).State;
        Assert.Equal(RunPhase.Reward, second.Phase);
        Assert.NotNull(second.World!.Reward!.RelicOption);
        relic = second.Player.Relics.Single(item =>
            item.RelicId == silverId);
        Assert.Equal(2, relic.PersistentState
            .GetProperty("TreasureRoomsEntered").GetInt32());
        PrototypeStateInvariants.Validate(second);
    }

    private static RunState BeforeTreasure(RunState state)
    {
        var map = state.World!.Map;
        var treasure = map.Nodes.First(node =>
            node.Floor == PrototypeNativeOvergrowthMap.FirstTreasureFloor
            && node.RoomType == PrototypeRoomType.Treasure);
        var predecessor = map.Nodes.First(node =>
            node.NextNodeIds?.Contains(treasure.NodeId,
                StringComparer.Ordinal) == true);
        var history = Enumerable.Range(1, predecessor.Floor)
            .Select(floor =>
            {
                var node = floor == predecessor.Floor
                    ? predecessor
                    : map.Nodes.First(item => item.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, node.NodeId, node.RoomType);
            }).ToArray();

        state = state with
        {
            World = state.World with
            {
                Event = null,
                Floor = predecessor.Floor,
                CompletedRoomHistory = history,
                Map = map with { CurrentNodeId = predecessor.NodeId }
            }
        };
        PrototypeStateInvariants.Validate(state);
        return state;
    }
}
