using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeMapTests
{
    [Fact]
    public void ActMapIsGeneratedUpFrontAndPersistsAfterChoosingARoute()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("map-test");

        state = engine.Step(
            state,
            Assert.Single(engine.GetLegalActions(state))).State;
        PrototypeStateInvariants.Validate(state);

        var map = state.World!.Map;
        Assert.Equal(14, map.Nodes.Length);
        Assert.Equal(2, map.EntryNodeIds!.Length);
        Assert.Equal(2, map.AvailableNodes().Length);
        Assert.Single(map.Nodes, node => node.RoomType == PrototypeRoomType.Boss);

        foreach (var node in map.Nodes)
        {
            foreach (var nextId in node.NextNodeIds ?? Array.Empty<string>())
            {
                var next = map.Nodes.Single(candidate => candidate.NodeId == nextId);
                Assert.Equal(node.Floor + 1, next.Floor);
            }
        }

        var graphHash = CanonicalJson.Sha256(map.Nodes);
        var choose = engine.GetLegalActions(state)[0];
        var chosenNodeId = choose.ReadPayload<ChooseMapNodePayload>().NodeId;

        state = engine.Step(state, choose).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(chosenNodeId, state.World!.Map.CurrentNodeId);
        Assert.Equal(graphHash, CanonicalJson.Sha256(state.World.Map.Nodes));
    }
}
