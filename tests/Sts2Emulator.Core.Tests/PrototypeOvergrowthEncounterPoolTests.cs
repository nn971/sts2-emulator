using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeOvergrowthEncounterPoolTests
{
    [Fact]
    public void NewRunInitializesOvergrowthWeakPool()
    {
        var state = StartRun("overgrowth-pool-init");

        Assert.Equal(
            PrototypeActOneRegion.Overgrowth,
            state.World!.ActOneRegion);

        var pool = Assert.IsType<
            PrototypeActOneEncounterPoolState>(
            state.World.ActOneEncounterPool);
        Assert.Equal(
            PrototypeActOneRegion.Overgrowth,
            pool.Region);
        Assert.Equal(0, pool.OrdinaryCombatsStarted);
        Assert.Equal(
            PrototypeContent.OvergrowthWeakEncounterPool
                .Order(StringComparer.Ordinal),
            pool.RemainingWeakEncounterIds
                .Order(StringComparer.Ordinal));

        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FirstThreeOrdinaryCombatsConsumeDistinctWeakEncounters()
    {
        var engine = new PrototypeGameEngine();
        var state = StartRun(
            "overgrowth-three-weak",
            engine);

        var selected = new List<string>();
        for (var combatIndex = 0;
             combatIndex < 3;
             combatIndex++)
        {
            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Combat,
                floor: combatIndex + 1);

            selected.Add(
                state.World!.EncounterIds[^1]);

            var pool = Assert.IsType<
                PrototypeActOneEncounterPoolState>(
                state.World.ActOneEncounterPool);
            Assert.Equal(
                combatIndex + 1,
                pool.OrdinaryCombatsStarted);
            Assert.Equal(
                3 - combatIndex,
                pool.RemainingWeakEncounterIds.Length);
            Assert.DoesNotContain(
                selected[^1],
                pool.RemainingWeakEncounterIds);

            PrototypeStateInvariants.Validate(state);
        }

        Assert.Equal(
            3,
            selected.Distinct(
                StringComparer.Ordinal).Count());
        Assert.All(
            selected,
            encounterId => Assert.Contains(
                encounterId,
                PrototypeContent.OvergrowthWeakEncounterPool));

        var finalPool = state.World!.ActOneEncounterPool!;
        Assert.Single(finalPool.RemainingWeakEncounterIds);
        Assert.DoesNotContain(
            finalPool.RemainingWeakEncounterIds[0],
            selected);
    }

    [Fact]
    public void EliteDoesNotConsumeWeakEncounterQueue()
    {
        var engine = new PrototypeGameEngine();
        var state = StartRun(
            "overgrowth-elite-no-consume",
            engine);

        state = StartRoom(
            engine,
            state,
            PrototypeRoomType.Elite,
            floor: 3);

        var pool = state.World!.ActOneEncounterPool!;
        Assert.Equal(0, pool.OrdinaryCombatsStarted);
        Assert.Equal(
            PrototypeContent.OvergrowthWeakEncounterPool
                .Order(StringComparer.Ordinal),
            pool.RemainingWeakEncounterIds
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            "proto.encounter.elite",
            state.World.EncounterIds[^1]);
    }

    [Fact]
    public void FourthOrdinaryCombatLeavesWeakPoolUntouched()
    {
        var engine = new PrototypeGameEngine();
        var state = StartRun(
            "overgrowth-fourth-normal-boundary",
            engine);

        for (var combatIndex = 0;
             combatIndex < 3;
             combatIndex++)
        {
            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Combat,
                floor: combatIndex + 1);
        }

        var before = state.World!.ActOneEncounterPool!;
        var remainingBefore =
            (string[])before.RemainingWeakEncounterIds.Clone();

        state = StartRoom(
            engine,
            state,
            PrototypeRoomType.Combat,
            floor: 4);

        var after = state.World!.ActOneEncounterPool!;
        Assert.Equal(3, after.OrdinaryCombatsStarted);
        Assert.Equal(remainingBefore, after.RemainingWeakEncounterIds);
        Assert.DoesNotContain(
            state.World.EncounterIds[^1],
            PrototypeContent.OvergrowthWeakEncounterPool);
    }

    [Fact]
    public void ForkClonesWeakPoolState()
    {
        var engine = new PrototypeGameEngine();
        var original = StartRun(
            "overgrowth-pool-fork",
            engine);
        var branch = original.Fork();

        branch = StartRoom(
            engine,
            branch,
            PrototypeRoomType.Combat,
            floor: 1);

        Assert.Equal(
            0,
            original.World!.ActOneEncounterPool!
                .OrdinaryCombatsStarted);
        Assert.Equal(
            4,
            original.World.ActOneEncounterPool
                .RemainingWeakEncounterIds.Length);

        Assert.Equal(
            1,
            branch.World!.ActOneEncounterPool!
                .OrdinaryCombatsStarted);
        Assert.Equal(
            3,
            branch.World.ActOneEncounterPool
                .RemainingWeakEncounterIds.Length);
    }

    private static RunState StartRun(
        string seed,
        PrototypeGameEngine? engine = null)
    {
        engine ??= new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(seed);
        return engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state))).State;
    }

    private static RunState StartRoom(
        PrototypeGameEngine engine,
        RunState state,
        PrototypeRoomType roomType,
        int floor)
    {
        var node = new MapNodeState(
            $"overgrowth-test:{floor}:{roomType}",
            Act: 1,
            Floor: floor,
            RoomType: roomType,
            NextNodeIds: []);

        var world = state.World! with
        {
            Act = 1,
            Floor = Math.Max(0, floor - 1),
            ActiveRoom = null,
            Map = new MapState(
                [node],
                CurrentNodeId: null,
                EntryNodeIds: [node.NodeId]),
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        };

        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = world
        };

        var action = Assert.Single(
            engine.GetLegalActions(state));
        return engine.Step(
            state,
            action).State;
    }
}
