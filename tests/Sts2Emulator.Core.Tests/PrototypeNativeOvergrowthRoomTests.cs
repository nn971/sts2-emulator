using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeOvergrowthRoomTests
{
    [Fact]
    public void FixedTreasureRowGrantsRelicThenResumesFloorTenRouting()
    {
        var engine = new PrototypeGameEngine();
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "native-treasure-room");
        var treasure = initial.World!.Map.Nodes.First(node =>
            node.Floor == 9 && node.RoomType == PrototypeRoomType.Treasure);
        var state = BeforeNode(initial, treasure);
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state, GameAction.Create(
            "choose_map_node",
            new ChooseMapNodePayload(treasure.NodeId))).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal("Treasure", state.World!.Reward!.SourceRoom);
        Assert.Equal(9, state.World.Floor);
        Assert.True(state.World.Reward.CardResolved);
        Assert.True(state.World.Reward.PotionResolved);
        Assert.False(state.World.Reward.RelicResolved);
        PrototypeStateInvariants.Validate(state);

        while (state.Phase == RunPhase.Reward)
        {
            var legal = engine.GetLegalActions(state);
            var action = legal.FirstOrDefault(item =>
                item.Kind == "take_reward_relic")
                ?? legal.FirstOrDefault(item =>
                    item.Kind == "choose_relic_deck_card")
                ?? Assert.Single(legal, item => item.Kind == "leave_reward");
            state = engine.Step(state, action).State;
            PrototypeStateInvariants.Validate(state);
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(9, state.World!.Floor);
        Assert.Contains(state.World.CompletedRooms, room =>
            room.Floor == 9
            && room.NodeId == treasure.NodeId
            && room.RoomType == PrototypeRoomType.Treasure);
        Assert.All(state.World.Map.AvailableNodes(), node =>
            Assert.Equal(10, node.Floor));
    }

    [Fact]
    public void UnknownMapNodePreservesVisibleIdentityButRecordsResolvedRoom()
    {
        var engine = new PrototypeGameEngine();
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "native-question-room");
        var node = initial.World!.Map.Nodes.First(item =>
            item.RoomType == PrototypeRoomType.Unknown);
        var state = BeforeNode(initial, node);
        PrototypeStateInvariants.Validate(state);

        var before = state.World!.UnknownRoomOdds;
        state = engine.Step(state, GameAction.Create(
            "choose_map_node",
            new ChooseMapNodePayload(node.NodeId))).State;
        Assert.Equal(node.NodeId, state.World!.Map.CurrentNodeId);
        Assert.Equal(PrototypeRoomType.Unknown, node.RoomType);
        Assert.NotEqual(PrototypeRoomType.Unknown, state.World.ActiveRoom);
        Assert.NotEqual(before, state.World.UnknownRoomOdds);
        Assert.Contains(
            state.Phase,
            new[] { RunPhase.Event, RunPhase.Combat, RunPhase.Shop, RunPhase.Reward });
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState BeforeNode(
        RunState state,
        MapNodeState node)
    {
        var map = state.World!.Map;
        var parent = map.Nodes.Single(item =>
            item.NextNodeIds?.Contains(node.NodeId, StringComparer.Ordinal)
                == true);
        var history = Enumerable.Range(1, parent.Floor)
            .Select(floor =>
            {
                var selected = floor == parent.Floor
                    ? parent
                    : map.Nodes.First(item => item.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1,
                    floor,
                    selected.NodeId,
                    selected.RoomType);
            })
            .ToArray();

        return state with
        {
            World = state.World with
            {
                Floor = parent.Floor,
                ActiveRoom = null,
                CompletedRoomHistory = history,
                Map = map with { CurrentNodeId = parent.NodeId }
            }
        };
    }
}
