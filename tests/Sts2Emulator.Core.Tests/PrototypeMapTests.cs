using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeMapTests
{
    [Fact]
    public void ActMapIsGeneratedUpFrontAndPersistsAfterChoosingARoute()
    {
        var engine =
            new PrototypeGameEngine();
        var state =
            PrototypeGameFactory.Create(
                "map-test");

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(
                    state))).State;
        PrototypeStateInvariants.Validate(
            state);

        var map = state.World!.Map;
        Assert.Equal(
            PrototypeContent
                .MapGenerationProfileId,
            map.GenerationProfileId);
        Assert.Equal(
            3,
            map.EntryNodeIds!.Length);
        Assert.Equal(
            3,
            map.AvailableNodes().Length);
        Assert.Single(
            map.Nodes,
            node =>
                node.RoomType
                    == PrototypeRoomType.Boss);
        Assert.All(
            map.Nodes.Where(node =>
                node.Floor == 1),
            node => Assert.Equal(
                PrototypeRoomType.Combat,
                node.RoomType));
        Assert.All(
            map.Nodes.Where(node =>
                node.Floor == 5),
            node => Assert.Equal(
                PrototypeRoomType.Rest,
                node.RoomType));
        Assert.All(
            map.Nodes.Where(node =>
                node.Floor == 6),
            node => Assert.Equal(
                PrototypeRoomType.Boss,
                node.RoomType));

        foreach (var floorGroup in
                 map.Nodes.GroupBy(node =>
                     node.Floor))
        {
            var rule = Assert.Single(
                PrototypeContent
                    .Rules.MapFloorRules,
                item =>
                    floorGroup.Key
                        >= item.MinFloor
                    && floorGroup.Key
                        <= item.MaxFloor);

            Assert.InRange(
                floorGroup.Count(),
                rule.MinNodes,
                rule.MaxNodes);
            foreach (var requiredRoom in
                     rule.RequiredRooms)
            {
                Assert.Contains(
                    floorGroup,
                    node =>
                        node.RoomType
                            == requiredRoom);
            }

            if (rule
                .AllowDuplicateSpecialRooms)
            {
                continue;
            }

            var duplicateSpecials =
                floorGroup
                    .Where(node =>
                        node.RoomType
                            != PrototypeRoomType.Combat)
                    .GroupBy(node =>
                        node.RoomType)
                    .Where(group =>
                        group.Count() > 1)
                    .ToArray();
            Assert.Empty(
                duplicateSpecials);
        }

        var incoming =
            map.Nodes.ToDictionary(
                node => node.NodeId,
                _ => 0,
                StringComparer.Ordinal);
        foreach (var node in map.Nodes)
        {
            var nextIds =
                node.NextNodeIds
                ?? Array.Empty<string>();
            if (node.Floor
                < PrototypeContent
                    .Rules.FloorsPerAct)
            {
                Assert.InRange(
                    nextIds.Length,
                    1,
                    2);
            }
            else
            {
                Assert.Empty(
                    nextIds);
            }

            foreach (var nextId in
                     nextIds)
            {
                var next = map.Nodes.Single(
                    candidate =>
                        candidate.NodeId
                            == nextId);
                Assert.Equal(
                    node.Floor + 1,
                    next.Floor);
                incoming[nextId]++;
            }
        }

        Assert.All(
            map.Nodes.Where(node =>
                node.Floor > 1),
            node => Assert.True(
                incoming[node.NodeId] > 0));
        Assert.Contains(
            map.Nodes,
            node =>
                node.Floor < 5
                && (node.NextNodeIds?.Length
                    ?? 0) == 2);
        Assert.Contains(
            map.Nodes,
            node =>
                node.Floor is > 1 and < 6
                && incoming[node.NodeId] > 1);

        foreach (var rule in
                 PrototypeContent
                     .Rules.MapFloorRules
                     .Where(rule =>
                         rule
                             .AvoidMatchingSpecialPredecessors))
        {
            foreach (var node in
                     map.Nodes.Where(node =>
                         node.Floor
                             >= rule.MinFloor
                         && node.Floor
                             <= rule.MaxFloor
                         && node.RoomType
                             != PrototypeRoomType.Combat))
            {
                Assert.DoesNotContain(
                    map.Nodes,
                    predecessor =>
                        predecessor.RoomType
                            == node.RoomType
                        && (predecessor
                                .NextNodeIds
                            ?? Array.Empty<string>())
                            .Contains(
                                node.NodeId,
                                StringComparer.Ordinal));
            }
        }

        var graphHash =
            CanonicalJson.Sha256(
                map.Nodes);
        var choose =
            engine.GetLegalActions(
                state)[0];
        var chosenNodeId =
            choose.ReadPayload<
                    ChooseMapNodePayload>()
                .NodeId;

        state = engine.Step(
            state,
            choose).State;
        PrototypeStateInvariants.Validate(
            state);

        Assert.Equal(
            chosenNodeId,
            state.World!.Map
                .CurrentNodeId);
        Assert.Equal(
            graphHash,
            CanonicalJson.Sha256(
                state.World.Map.Nodes));
    }

    [Fact]
    public void StrategicMapTopologyVariesAcrossSeeds()
    {
        var signatures =
            new HashSet<string>(
                StringComparer.Ordinal);

        for (var seed = 0;
             seed < 30;
             seed++)
        {
            var engine =
                new PrototypeGameEngine();
            var state =
                PrototypeGameFactory.Create(
                    $"map-diversity-{seed}");
            state = engine.Step(
                state,
                Assert.Single(
                    engine.GetLegalActions(
                        state))).State;
            PrototypeStateInvariants.Validate(
                state);

            var map = state.World!.Map;
            var topology = map.Nodes
                .OrderBy(node =>
                    node.Floor)
                .ThenBy(node =>
                    node.NodeId,
                    StringComparer.Ordinal)
                .Select(node =>
                    new
                    {
                        node.NodeId,
                        node.RoomType,
                        Next = node.NextNodeIds
                            ?? Array.Empty<string>()
                    })
                .ToArray();
            signatures.Add(
                CanonicalJson.Sha256(
                    topology));
        }

        Assert.True(
            signatures.Count >= 10,
            $"Expected substantial seeded map diversity, got {signatures.Count} distinct maps.");
    }

    [Fact]
    public void EveryGeneratedMapRemainsStrategicallyConnected()
    {
        for (var seed = 0;
             seed < 100;
             seed++)
        {
            var engine =
                new PrototypeGameEngine();
            var state =
                PrototypeGameFactory.Create(
                    $"map-connectivity-{seed}");
            state = engine.Step(
                state,
                Assert.Single(
                    engine.GetLegalActions(
                        state))).State;

            PrototypeStateInvariants.Validate(
                state);

            var map = state.World!.Map;
            var boss = Assert.Single(
                map.Nodes,
                node =>
                    node.RoomType
                        == PrototypeRoomType.Boss);
            var canReachBoss =
                new HashSet<string>(
                    [boss.NodeId],
                    StringComparer.Ordinal);

            for (var floor =
                     PrototypeContent
                         .Rules.FloorsPerAct - 1;
                 floor >= 1;
                 floor--)
            {
                foreach (var node in
                         map.Nodes.Where(node =>
                             node.Floor == floor))
                {
                    if ((node.NextNodeIds
                            ?? Array.Empty<string>())
                        .Any(canReachBoss
                            .Contains))
                    {
                        canReachBoss.Add(
                            node.NodeId);
                    }
                }
            }

            Assert.Equal(
                map.Nodes.Length,
                canReachBoss.Count);
        }
    }
}
