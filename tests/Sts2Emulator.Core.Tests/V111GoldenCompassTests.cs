using Sts2Emulator.Core;
using System.Reflection;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GoldenCompassTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState HiveOpening()
    {
        var configuration = RunConfiguration.Create() with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        var state = Engine.Step(V111RunFactory.Create("golden-compass-route", configuration),
            GameAction.Empty("start_run")).State;
        for (var i = 0; i < 30 && state.Phase is RunPhase.Event or RunPhase.Reward; i++)
            state = Engine.Step(state, Engine.GetLegalActions(state).First()).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        // This named fixture deliberately assigns Tezcatara rather than the
        // seed's normally sampled Ancient.
        state = state with
        {
            World = state.World! with
            {
                PreparedLaterActEncounterBags = state.World.PreparedLaterActEncounterBags!
                    .Select(bag => bag.Act == 2
                        ? bag with { AssignedAncientId = NativeAncientRewardsV111.TezcataraId }
                        : bag.Fork()).ToArray()
            }
        };
        state = state with { Phase = RunPhase.ActTransition };
        state = Engine.Step(state, GameAction.Empty("continue_act")).State;
        return state with
        {
            Phase = RunPhase.Event,
            World = state.World! with
            {
                Event = new EventState(NativeAncientRewardsV111.TezcataraId,
                    OfferedChoiceIds: ["take_" + NativeAncientRewardsV111.GoldenCompassId]),
                EventHistory = state.World.EventIds
            }
        };
    }

    private static RunState Acquire()
    {
        var state = HiveOpening();
        return Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
    }

    private static RunState AtFloor(RunState state, int completed)
    {
        var nodes = state.World!.Map.Nodes;
        return state with
        {
            World = state.World with
            {
                Floor = completed,
                Map = state.World.Map with { CurrentNodeId = nodes[completed - 1].NodeId },
                CompletedRoomHistory = nodes.Take(completed).Select(node => new PrototypeCompletedRoomRecord(
                    2, node.Floor, node.NodeId,
                    node.RoomType == PrototypeRoomType.Unknown ? PrototypeRoomType.Event : node.RoomType)).ToArray()
            }
        };
    }

    [Fact]
    public void AcquisitionReplacesMapOnceAndPreservesPreparedEncounterQueues()
    {
        var opening = HiveOpening();
        var hash = CanonicalJson.Sha256(opening);
        var action = Assert.Single(Engine.GetLegalActions(opening));
        var acquired = Engine.Step(opening, action).State;
        Assert.Equal(hash, CanonicalJson.Sha256(opening));
        Assert.Equal(CanonicalJson.Sha256(acquired), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(opening)), action).State));
        Assert.Equal(RunPhase.MapChoice, acquired.Phase);
        Assert.Equal(0, acquired.World!.Floor);
        Assert.Empty(acquired.World.CompletedRooms);
        Assert.Equal(PrototypeGoldenPathMapV111.ProfileId, acquired.World.Map.GenerationProfileId);
        Assert.Equal(17, acquired.World.Map.Nodes.Length);
        Assert.Equal(1, PrototypeGoldenPathMapV111.AcquisitionActIndex(acquired.Player.Relics
            .Single(relic => relic.RelicId == NativeAncientRewardsV111.GoldenCompassId)));
        Assert.Equal(CanonicalJson.Sha256(opening.Rng), CanonicalJson.Sha256(acquired.Rng));
        Assert.Equal(CanonicalJson.Sha256(opening.World!.LaterActEncounterBag),
            CanonicalJson.Sha256(acquired.World.LaterActEncounterBag));
        Assert.Equal(CanonicalJson.Sha256(opening.World.PreparedLaterActEncounterBags),
            CanonicalJson.Sha256(acquired.World.PreparedLaterActEncounterBags));
        Assert.Equal([1, 3, 5], acquired.World.Map.Nodes.Where(node =>
            node.RoomType == PrototypeRoomType.Combat).Select(node => node.Floor));
        Assert.Equal([8, 10], acquired.World.Map.Nodes.Where(node =>
            node.RoomType == PrototypeRoomType.Treasure).Select(node => node.Floor));
        Assert.Equal([13, 15], acquired.World.Map.Nodes.Where(node =>
            node.RoomType == PrototypeRoomType.Elite).Select(node => node.Floor));
        PrototypeStateInvariants.Validate(acquired);
        var first = Engine.Step(acquired, Assert.Single(Engine.GetLegalActions(acquired))).State;
        Assert.Equal(RunPhase.Combat, first.Phase);
        Assert.Equal(PrototypeGoldenPathMapV111.ProfileId, first.World!.Map.GenerationProfileId);
        PrototypeStateInvariants.Validate(first);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(11)]
    public void UnknownRoomsBecomeEventsWithoutUpdatingUnknownRoomOdds(int floor)
    {
        var state = AtFloor(Acquire(), floor - 1);
        PrototypeStateInvariants.Validate(state);
        var odds = CanonicalJson.Sha256(state.World!.UnknownRoomOdds);
        var action = Assert.Single(Engine.GetLegalActions(state));
        var entered = Engine.Step(state, action).State;
        Assert.Equal(PrototypeRoomType.Event, entered.World!.ActiveRoom);
        Assert.Equal(RunPhase.Event, entered.Phase);
        Assert.Equal(floor, entered.World.Floor);
        Assert.Equal(odds, CanonicalJson.Sha256(entered.World.UnknownRoomOdds));
        Assert.Equal(CanonicalJson.Sha256(entered), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        PrototypeStateInvariants.Validate(entered);
    }

    [Fact]
    public void BossUsesFloorSeventeenAndGloryRestoresItsOrdinaryMap()
    {
        var state = AtFloor(Acquire(), 16);
        var boss = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        Assert.Equal(17, boss.World!.Floor);
        Assert.Equal(PrototypeRoomType.Boss, boss.World.ActiveRoom);
        PrototypeStateInvariants.Validate(boss);
        // Isolate the act transfer from boss mechanics, which have their own fixtures.
        var finished = AtFloor(Acquire(), 17) with { Phase = RunPhase.ActTransition };
        PrototypeStateInvariants.Validate(finished);
        var glory = Engine.Step(finished, GameAction.Empty("continue_act")).State;
        Assert.Equal(PrototypeNativeLaterActMap.GloryProfileId, glory.World!.Map.GenerationProfileId);
        Assert.False(PrototypeGoldenPathMapV111.IsActive(glory.Player, 3));
        Assert.True(PrototypeGoldenPathMapV111.IsActive(glory.Player, 2));
        PrototypeStateInvariants.Validate(glory);
        Assert.Equal(CanonicalJson.Sha256(glory), CanonicalJson.Sha256(RunSnapshot.Load(RunSnapshot.Save(glory))));
    }

    [Fact]
    public void ForcedUnknownRoomStillConsumesOneRollAndRetainsOdds()
    {
        var state = Acquire();
        var method = typeof(PrototypeGameEngine).GetMethod("ResolveUnknownMapRoom",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var rng = state.Rng.Fork();
        var expected = rng.Fork();
        _ = PrototypeRng.NextInt(expected, "event", PrototypeUnknownRoomOddsState.Scale);
        object[] args = [state.World!, rng, true];
        var result = method.Invoke(null, args);
        Assert.Equal(PrototypeRoomType.Event, result);
        Assert.Equal(CanonicalJson.Sha256(expected), CanonicalJson.Sha256(rng));
        Assert.Equal(CanonicalJson.Sha256(state.World), CanonicalJson.Sha256(args[0]));
    }

    [Fact]
    public void GoldenPathRejectsCorruptGeometryOrWrongActOwnership()
    {
        var state = Acquire();
        var nodes = state.World!.Map.Nodes.Select(node => node.Fork()).ToArray();
        nodes[1] = nodes[1] with { RoomType = PrototypeRoomType.Shop };
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Map = state.World.Map with { Nodes = nodes } } }));
        Assert.Throws<InvalidDataException>(() => PrototypeStateInvariants.Validate(state with
        { Player = state.Player with { Relics = state.Player.Relics.Where(relic =>
            relic.RelicId != NativeAncientRewardsV111.GoldenCompassId).ToArray() } }));
    }
}
