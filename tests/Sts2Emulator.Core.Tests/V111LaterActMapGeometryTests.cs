using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActMapGeometryTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    public void PinnedHiveAndGloryGeometryHasRegionalFloorCounts(
        int act, int ascension)
    {
        var state = PrototypeNativeLaterActFixture.Create(
            $"late-geometry-{act}-{ascension}", act, ascension);
        var map = state.World!.Map;
        var rows = PrototypeNativeLaterActMap.RoomRows(act);
        Assert.Equal(PrototypeNativeLaterActMap.ProfileId(act),
            map.GenerationProfileId);
        Assert.Equal(act, state.World.Act);
        Assert.Null(map.CurrentNodeId);
        Assert.Equal(0, state.World.Floor);
        Assert.All(map.Nodes, node => Assert.Equal(act, node.Act));

        var boss = Assert.Single(map.Nodes,
            node => node.RoomType == PrototypeRoomType.Boss);
        Assert.Equal(rows + 1, boss.Floor);
        Assert.Empty(boss.NextNodeIds!);
        Assert.Equal(3, int.Parse(boss.NodeId.Split(':')[2]));
        Assert.All(map.Nodes.Where(n => n.Floor == 1),
            n => Assert.Equal(PrototypeRoomType.Combat, n.RoomType));
        Assert.All(map.Nodes.Where(n => n.Floor == rows - 6),
            n => Assert.Equal(PrototypeRoomType.Treasure, n.RoomType));
        Assert.All(map.Nodes.Where(n => n.Floor == rows),
            n => Assert.Equal(PrototypeRoomType.Rest, n.RoomType));
        Assert.Equal(rows + 1, map.Nodes.Max(n => n.Floor));
        Assert.Equal(rows + 1,
            map.Nodes.Select(n => n.Floor).Distinct().Count());
        Assert.Contains(map.Nodes,
            n => n.RoomType == PrototypeRoomType.Unknown);
        Assert.All(map.Nodes.Where(n => n.Floor < rows + 1),
            n => Assert.NotEmpty(n.NextNodeIds!));
        Assert.InRange(map.EntryNodeIds!.Length, 2, 7);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void MultipleSeedsKeepStructuralInvariantsAndDeterminism(int act)
    {
        for (var i = 0; i < 20; i++)
        {
            var seed = $"v111-late-map-{act}-{i}";
            var left = PrototypeNativeLaterActFixture.Create(
                seed, act, ascension: i % 2);
            var right = PrototypeNativeLaterActFixture.Create(
                seed, act, ascension: i % 2);
            Assert.Equal(CanonicalJson.Sha256(left),
                CanonicalJson.Sha256(right));
            Assert.Equal(CanonicalJson.Sha256(left),
                CanonicalJson.Sha256(left.Fork()));
            PrototypeStateInvariants.Validate(left);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeLaterActFirstRoomUsesSourceWeakEncounter(int act)
    {
        var state = PrototypeNativeLaterActFixture.Create(
            "v111-gated-later-room", act);
        var first = state.World!.Map.AvailableNodes()[0];
        var hash = CanonicalJson.Sha256(state);
        var entered = ChooseNode(state, first.NodeId);
        Assert.Equal(RunPhase.Combat, entered.Phase);
        Assert.Contains(entered.World!.EncounterIds[^1],
            PrototypeNativeLaterActCombatPool.Names(
                act, PrototypeRoomType.Combat, weak: true));
        PrototypeStateInvariants.Validate(entered);
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void FullGloryMapRedirectsReachableUnknownToWarHistorianRepy()
    {
        var start = PrototypeNativeLaterActFixture.Create(
            "v111-glory-key-full-map", 3, lanternKeys: 2);
        var precursor = FindUnknownPrecursor(start.World!.Map);
        var state = AtNode(start, precursor);
        var currentHash = CanonicalJson.Sha256(state);
        var result = ChooseNode(state,
            state.World!.Map.AvailableNodes()
                .First(n => n.RoomType == PrototypeRoomType.Unknown)
                .NodeId);
        Assert.Equal(RunPhase.Event, result.Phase);
        Assert.Equal(PrototypeRoomType.Event, result.World!.ActiveRoom);
        Assert.Equal(PrototypeNativeHiveEvents.WarHistorianRepyId,
            result.World.Event!.EventId);
        Assert.Equal(2, result.Player.Deck.Count(card => card.CardId
            == PrototypeNativeHiveEvents.LanternKeyCardId));
        Assert.Equal(currentHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(ChooseNode(state.Fork(),
                result.World.Map.CurrentNodeId!)));
        PrototypeStateInvariants.Validate(result);

        var first = ChooseEvent(result, "unlock_cage");
        Assert.Equal(RunPhase.Event, first.Phase);
        Assert.True(first.World!.FreedRepy);
        Assert.Single(first.Player.Deck, card => card.CardId
            == PrototypeNativeHiveEvents.LanternKeyCardId);
        Assert.Single(Engine.GetLegalActions(first),
            a => a.Kind == "event_choice");
        PrototypeStateInvariants.Validate(first);
    }

    [Fact]
    public void FullGloryMapWithoutKeyResolvesOnlySupportedUnknownRooms()
    {
        var start = PrototypeNativeLaterActFixture.Create(
            "v111-no-key-full-glory", 3);
        var state = AtNode(start, FindUnknownPrecursor(start.World!.Map));
        var option = state.World!.Map.AvailableNodes()
            .First(n => n.RoomType == PrototypeRoomType.Unknown);
        var sourceHash = CanonicalJson.Sha256(state);
        try
        {
            var result = ChooseNode(state, option.NodeId);
            Assert.Contains(result.Phase,
                new[] { RunPhase.Event, RunPhase.Combat });
            if (result.Phase == RunPhase.Event)
                Assert.Contains(result.World!.Event!.EventId,
                    PrototypeNativeLaterActEventPool.GloryEventIds);
            else
                Assert.Contains(result.World!.EncounterIds[^1],
                    PrototypeNativeLaterActCombatPool.GloryWeak);
            PrototypeStateInvariants.Validate(result);
        }
        catch (NotSupportedException error)
        {
            // Monster/Shop/Treasure Unknown rolls still fail closed.
            Assert.Contains("gated", error.Message);
        }
        Assert.Equal(sourceHash, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void LaterActProfileIsNotAcceptableInWrongAct()
    {
        var state = PrototypeNativeLaterActFixture.Create(
            "v111-map-mismatch", 3);
        var invalid = state with
        {
            World = state.World! with { Act = 2 }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(invalid));
    }

    [Fact]
    public void MalformedLaterActFixedTreasureRowIsRejected()
    {
        var state = PrototypeNativeLaterActFixture.Create(
            "v111-fixed-treasure", 3);
        var map = state.World!.Map;
        var treasure = map.Nodes.First(n =>
            n.Floor == PrototypeNativeLaterActMap.GloryRoomRows - 6);
        var corrupted = state with
        {
            World = state.World with
            {
                Map = map with
                {
                    Nodes = map.Nodes.Select(n => n.NodeId ==
                        treasure.NodeId
                        ? n with { RoomType = PrototypeRoomType.Elite }
                        : n).ToArray()
                }
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeStateInvariants.Validate(corrupted));
    }

    private static MapNodeState FindUnknownPrecursor(MapState map)
    {
        var target = map.Nodes.First(node =>
            node.RoomType == PrototypeRoomType.Unknown);
        return map.Nodes.First(node => node.NextNodeIds!
            .Contains(target.NodeId, StringComparer.Ordinal));
    }

    private static RunState AtNode(RunState state, MapNodeState node) =>
        state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = node.Floor,
                Map = state.World.Map with
                {
                    CurrentNodeId = node.NodeId
                }
            }
        };

    private static RunState ChooseNode(RunState state, string nodeId) =>
        Engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(nodeId))).State;

    private static RunState ChooseEvent(RunState state, string id) =>
        Engine.Step(state, Engine.GetLegalActions(state)
            .Single(a => a.Kind == "event_choice"
                && a.ReadPayload<EventChoicePayload>().ChoiceId == id))
            .State;
}
