using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ExperimentalFixedRouteOvergrowthMapV111Tests
{
    [Fact]
    public void GoldenPathDerivativeHasExactlyOneRouteAndNativeStructureActLength()
    {
        var map = ExperimentalFixedRouteOvergrowthMapV111.Generate();
        Assert.Equal(ExperimentalFixedRouteOvergrowthMapV111.ProfileId,
            map.GenerationProfileId);
        Assert.Equal(PrototypeNativeOvergrowthMap.BossFloor, map.Nodes.Length);
        Assert.Equal("1:1:3", Assert.Single(map.EntryNodeIds!));
        Assert.Null(map.CurrentNodeId);

        var expected = new[]
        {
            PrototypeRoomType.Combat,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Treasure,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Treasure,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Shop,
            PrototypeRoomType.Elite,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Elite,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Boss
        };
        Assert.Equal(expected, map.Nodes.Select(node => node.RoomType));
        for (var i = 0; i < map.Nodes.Length; i++)
        {
            var node = map.Nodes[i];
            Assert.Equal(i + 1, node.Floor);
            Assert.Equal($"1:{i + 1}:3", node.NodeId);
            Assert.Equal(
                i + 1 == map.Nodes.Length
                    ? Array.Empty<string>()
                    : new[] { $"1:{i + 2}:3" },
                node.ChildNodeIds);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void OverrideChangesOnlyMapAndRetainsNeowAndRng(int ascension)
    {
        var seed = $"fixed-route-overgrowth-{ascension}";
        var native = PrototypeNativeOvergrowthRunFactory.Create(seed, ascension);
        var experimental =
            ExperimentalFixedRouteOvergrowthMapV111.OverrideOpening(native);
        Assert.Equal(RunPhase.Event, experimental.Phase);
        Assert.Equal(0, experimental.World!.Floor);
        Assert.Equal(ExperimentalFixedRouteOvergrowthMapV111.ProfileId,
            experimental.World.Map.GenerationProfileId);

        // Replacing only the map back must reconstruct the exact original
        // state, including all RNG streams, Neow offers, deck and resources.
        var reverted = experimental with
        {
            World = experimental.World with { Map = native.World!.Map }
        };
        Assert.Equal(CanonicalJson.Sha256(native),
            CanonicalJson.Sha256(reverted));
        PrototypeStateInvariants.Validate(experimental);
    }

    [Fact]
    public void RouteIsIndependentOfSeedAndFirstRoomIsPlayable()
    {
        var a = ExperimentalFixedRouteOvergrowthMapV111.Create("single-route-a", 10);
        var b = ExperimentalFixedRouteOvergrowthMapV111.Create("single-route-b", 10);
        Assert.Equal(CanonicalJson.Sha256(a.World!.Map),
            CanonicalJson.Sha256(b.World!.Map));

        var engine = new PrototypeGameEngine();
        var neowAction = engine.GetLegalActions(a)[0];
        var afterNeow = engine.Step(a, neowAction).State;
        Assert.Equal(RunPhase.MapChoice, afterNeow.Phase);
        var mapActions = engine.GetLegalActions(afterNeow);
        Assert.Single(mapActions);
        var combat = engine.Step(afterNeow, mapActions[0]).State;
        Assert.Equal(RunPhase.Combat, combat.Phase);
        Assert.Equal(1, combat.World!.Floor);
        Assert.Equal(ExperimentalFixedRouteOvergrowthMapV111.ProfileId,
            combat.World.Map.GenerationProfileId);
    }

    [Fact]
    public void OverrideRejectsAlreadyEnteredRun()
    {
        var opening = ExperimentalFixedRouteOvergrowthMapV111.Create("invalid-map-change");
        var engine = new PrototypeGameEngine();
        var next = engine.Step(opening, engine.GetLegalActions(opening)[0]).State;
        Assert.Throws<InvalidOperationException>(() =>
            ExperimentalFixedRouteOvergrowthMapV111.OverrideOpening(next));
    }
}
