using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeOvergrowthMapTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public void NativeStructureHas15RoomRowsAndOneBoss(int ascension)
    {
        for (var index = 0; index < 25; index++)
        {
            var state = PrototypeNativeOvergrowthRunFactory.Create(
                $"native-overgrowth-{ascension}-{index}",
                ascension);
            var map = state.World!.Map;

            Assert.Equal(
                PrototypeNativeOvergrowthMap.GenerationProfileId,
                map.GenerationProfileId);
            Assert.Equal(0, state.World.Floor);
            Assert.All(map.Nodes.Where(node => node.Floor == 1),
                node => Assert.Equal(PrototypeRoomType.Combat, node.RoomType));
            Assert.All(map.Nodes.Where(node => node.Floor == 9),
                node => Assert.Equal(PrototypeRoomType.Treasure, node.RoomType));
            Assert.All(map.Nodes.Where(node => node.Floor == 15),
                node => Assert.Equal(PrototypeRoomType.Rest, node.RoomType));
            var boss = Assert.Single(
                map.Nodes, node => node.RoomType == PrototypeRoomType.Boss);
            Assert.Equal(16, boss.Floor);
            Assert.Equal(16, map.Nodes.Select(node => node.Floor).Max());
            Assert.All(Enumerable.Range(1, 16), floor =>
                Assert.Contains(map.Nodes, node => node.Floor == floor));
            Assert.InRange(map.EntryNodeIds!.Length, 2, 7);
            Assert.Equal(3, map.Nodes.Count(node =>
                node.RoomType == PrototypeRoomType.Shop));
            PrototypeStateInvariants.Validate(state);
        }
    }

    [Fact]
    public void NativeMapIsDeterministicAcrossIndependentInitialization()
    {
        var a = PrototypeNativeOvergrowthRunFactory.Create("same-seed");
        var b = PrototypeNativeOvergrowthRunFactory.Create("same-seed");
        Assert.Equal(
            CanonicalJson.Sha256(a),
            CanonicalJson.Sha256(b));
        Assert.NotSame(a.World!.Map.Nodes, b.World!.Map.Nodes);
        Assert.Equal(
            a.World.Map.EntryNodeIds,
            b.World.Map.EntryNodeIds);
    }

    [Fact]
    public void FirstRoomEntersCombatAndPreservesNativeMapIdentity()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create("native-entry");
        var legal = engine.GetLegalActions(state);
        Assert.All(legal, action =>
            Assert.Equal("choose_map_node", action.Kind));
        Assert.Equal(state.World!.Map.EntryNodeIds!.Length, legal.Count);

        state = engine.Step(state, legal[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(1, state.World!.Floor);
        Assert.Equal(
            PrototypeNativeOvergrowthMap.GenerationProfileId,
            state.World.Map.GenerationProfileId);
        Assert.Equal(PrototypeRoomType.Combat, state.World.ActiveRoom);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UnknownOddsAccumulateDeterministicallyAndDeepFork()
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create("unknown-odds");
        var odds = Assert.IsType<PrototypeUnknownRoomOddsState>(
            state.World!.UnknownRoomOdds);
        Assert.Equal(100, odds.MonsterWeight);
        Assert.Equal(20, odds.TreasureWeight);
        Assert.Equal(30, odds.ShopWeight);

        var missed = odds.After(PrototypeRoomType.Event);
        Assert.Equal(200, missed.MonsterWeight);
        Assert.Equal(40, missed.TreasureWeight);
        Assert.Equal(60, missed.ShopWeight);
        Assert.Equal(100, missed.After(PrototypeRoomType.Combat).MonsterWeight);
        Assert.Equal(90, missed.After(PrototypeRoomType.Combat).ShopWeight);
        Assert.Equal(20, missed.After(PrototypeRoomType.Treasure).TreasureWeight);

        var copy = state.Fork();
        Assert.Equal(odds, copy.World!.UnknownRoomOdds);
        Assert.NotSame(state.World.Map.Nodes, copy.World.Map.Nodes);
    }
}
