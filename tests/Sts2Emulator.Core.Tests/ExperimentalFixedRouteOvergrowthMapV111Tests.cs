using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ExperimentalFixedRouteOvergrowthMapV111Tests
{
    [Fact]
    public void ConfiguredSingleRouteHasExpectedRoomsAndNativeStructureActLength()
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
            PrototypeRoomType.Combat,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Shop,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Elite,
            PrototypeRoomType.Treasure,
            PrototypeRoomType.Unknown,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Rest,
            PrototypeRoomType.Combat,
            PrototypeRoomType.Elite,
            PrototypeRoomType.Unknown,
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
                node.NextNodeIds);
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
        // Neow relic choices can have follow-up event decisions; finish
        // those before asserting that the fixed map becomes selectable.
        var afterNeow = CompleteNeow(engine, a);
        var mapActions = engine.GetLegalActions(afterNeow);
        var entry = Assert.Single(
            mapActions, action => action.Kind == "choose_map_node");
        var combat = engine.Step(afterNeow, entry).State;
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
        var afterNeow = CompleteNeow(engine, opening);
        Assert.Throws<InvalidOperationException>(() =>
            ExperimentalFixedRouteOvergrowthMapV111.OverrideOpening(afterNeow));

        // A room must really be entered before testing the late-override
        // guard: one Neow choice may leave the game in RunPhase.Event.
        var entry = Assert.Single(
            engine.GetLegalActions(afterNeow),
            action => action.Kind == "choose_map_node");
        var entered = engine.Step(afterNeow, entry).State;
        Assert.Equal(RunPhase.Combat, entered.Phase);
        Assert.Equal(1, entered.World!.Floor);
        Assert.Throws<InvalidOperationException>(() =>
            ExperimentalFixedRouteOvergrowthMapV111.OverrideOpening(entered));
    }

    private static RunState CompleteNeow(PrototypeGameEngine engine, RunState state)
    {
        for (var step = 0; step < 32 && state.Phase == RunPhase.Event; step++)
        {
            var actions = engine.GetLegalActions(state);
            Assert.NotEmpty(actions);
            // An optional out-of-combat potion action does not complete Neow.
            var choice = actions.FirstOrDefault(action => action.Kind != "use_potion")
                ?? throw new InvalidOperationException(
                    "Neow offered no non-potion continuation action.");
            state = engine.Step(state, choice).State;
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }
}
