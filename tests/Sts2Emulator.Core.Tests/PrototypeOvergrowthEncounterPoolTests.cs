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
        Assert.Equal(
            PrototypeContent.OvergrowthNormalEncounterPool
                .Order(StringComparer.Ordinal),
            Assert.IsType<string[]>(
                    pool.RemainingNormalEncounterIds)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            PrototypeContent.OvergrowthEliteEncounterPool
                .Order(StringComparer.Ordinal),
            Assert.IsType<string[]>(
                    pool.RemainingEliteEncounterIds)
                .Order(StringComparer.Ordinal));
        Assert.Contains(
            pool.BossEncounterId,
            PrototypeContent.OvergrowthBossEncounterPool);

        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FullUnlockBossSelectionIsPinnedAtRunStart()
    {
        var engine = new PrototypeGameEngine();
        var seen = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seedIndex = 0;
             seedIndex < 96;
             seedIndex++)
        {
            var state = StartRun(
                $"overgrowth-boss-{seedIndex}",
                engine);
            var selected =
                state.World!.ActOneEncounterPool!
                    .BossEncounterId!;
            Assert.Contains(
                selected,
                PrototypeContent.OvergrowthBossEncounterPool);
            seen.Add(selected);

            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Boss,
                floor: 17);

            Assert.Equal(
                selected,
                state.World!.EncounterIds[^1]);
        }

        Assert.Equal(
            PrototypeContent.OvergrowthBossEncounterPool
                .Order(StringComparer.Ordinal),
            seen.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BossRoomDoesNotConsumeOrdinaryOrEliteBags()
    {
        var engine = new PrototypeGameEngine();
        var state = StartRun(
            "overgrowth-boss-does-not-consume",
            engine);
        var before =
            state.World!.ActOneEncounterPool!.Fork();

        state = StartRoom(
            engine,
            state,
            PrototypeRoomType.Boss,
            floor: 17);

        var after =
            state.World!.ActOneEncounterPool!;
        Assert.Equal(
            before.OrdinaryCombatsStarted,
            after.OrdinaryCombatsStarted);
        Assert.Equal(
            before.RemainingWeakEncounterIds,
            after.RemainingWeakEncounterIds);
        Assert.Equal(
            before.RemainingNormalEncounterIds,
            after.RemainingNormalEncounterIds);
        Assert.Equal(
            before.RemainingEliteEncounterIds,
            after.RemainingEliteEncounterIds);
        Assert.Equal(
            before.BossEncounterId,
            after.BossEncounterId);
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
    public void EliteDoesNotConsumeOrdinaryEncounterQueues()
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
            PrototypeContent.OvergrowthNormalEncounterPool
                .Order(StringComparer.Ordinal),
            pool.RemainingNormalEncounterIds!
                .Order(StringComparer.Ordinal));
        Assert.Contains(
            state.World.EncounterIds[^1],
            PrototypeContent.OvergrowthEliteEncounterPool);
        Assert.Equal(
            PrototypeContent.OvergrowthEliteEncounterPool.Length - 1,
            pool.RemainingEliteEncounterIds!.Length);
    }

    [Fact]
    public void FirstThreeElitesAreDistinctAndFourthCannotRepeatThird()
    {
        var engine = new PrototypeGameEngine();
        var state = StartRun(
            "overgrowth-elite-bag",
            engine);

        var selected = new List<string>();
        for (var eliteIndex = 0;
             eliteIndex < 3;
             eliteIndex++)
        {
            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Elite,
                floor: eliteIndex + 3);
            selected.Add(state.World!.EncounterIds[^1]);

            var pool = state.World.ActOneEncounterPool!;
            Assert.Equal(
                2 - eliteIndex,
                pool.RemainingEliteEncounterIds!.Length);
        }

        Assert.Equal(
            3,
            selected.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            selected,
            id => Assert.Contains(
                id,
                PrototypeContent.OvergrowthEliteEncounterPool));

        var third = selected[^1];
        state = StartRoom(
            engine,
            state,
            PrototypeRoomType.Elite,
            floor: 6);
        var fourth = state.World!.EncounterIds[^1];

        Assert.NotEqual(third, fourth);
        Assert.Contains(
            fourth,
            PrototypeContent.OvergrowthEliteEncounterPool);
        Assert.Equal(
            2,
            state.World.ActOneEncounterPool!
                .RemainingEliteEncounterIds!.Length);
    }

    [Fact]
    public void EliteRepeatGuardSurvivesInterveningOrdinaryCombat()
    {
        var engine = new PrototypeGameEngine();

        for (var seedIndex = 0;
             seedIndex < 96;
             seedIndex++)
        {
            var state = StartRun(
                $"overgrowth-elite-gap-{seedIndex}",
                engine);

            string? previousElite = null;
            for (var eliteIndex = 0;
                 eliteIndex < 3;
                 eliteIndex++)
            {
                state = StartRoom(
                    engine,
                    state,
                    PrototypeRoomType.Elite,
                    floor: eliteIndex + 3);
                previousElite =
                    state.World!.EncounterIds[^1];
            }

            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Combat,
                floor: 6);

            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Elite,
                floor: 7);

            Assert.NotEqual(
                previousElite,
                state.World!.EncounterIds[^1]);
        }
    }

    [Fact]
    public void FourthOrdinaryCombatStartsNormalBagWithoutTouchingWeakPool()
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
        Assert.Equal(4, after.OrdinaryCombatsStarted);
        Assert.Equal(remainingBefore, after.RemainingWeakEncounterIds);
        Assert.DoesNotContain(
            state.World.EncounterIds[^1],
            PrototypeContent.OvergrowthWeakEncounterPool);
        Assert.Contains(
            state.World.EncounterIds[^1],
            PrototypeContent.OvergrowthNormalEncounterPool);
        Assert.Equal(
            PrototypeContent.OvergrowthNormalEncounterPool.Length - 1,
            Assert.IsType<string[]>(
                    after.RemainingNormalEncounterIds)
                .Length);
        Assert.DoesNotContain(
            state.World.EncounterIds[^1],
            after.RemainingNormalEncounterIds!);
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
        Assert.Equal(
            PrototypeContent.OvergrowthNormalEncounterPool.Length,
            original.World.ActOneEncounterPool
                .RemainingNormalEncounterIds!.Length);
        Assert.Equal(
            PrototypeContent.OvergrowthNormalEncounterPool.Length,
            branch.World.ActOneEncounterPool
                .RemainingNormalEncounterIds!.Length);
        Assert.NotSame(
            original.World.ActOneEncounterPool
                .RemainingNormalEncounterIds,
            branch.World.ActOneEncounterPool
                .RemainingNormalEncounterIds);
        Assert.Equal(
            PrototypeContent.OvergrowthEliteEncounterPool.Length,
            original.World.ActOneEncounterPool
                .RemainingEliteEncounterIds!.Length);
        Assert.Equal(
            PrototypeContent.OvergrowthEliteEncounterPool.Length,
            branch.World.ActOneEncounterPool
                .RemainingEliteEncounterIds!.Length);
        Assert.NotSame(
            original.World.ActOneEncounterPool
                .RemainingEliteEncounterIds,
            branch.World.ActOneEncounterPool
                .RemainingEliteEncounterIds);
    }

    [Fact]
    public void NormalBagDrawsWithoutReplacement()
    {
        var engine = new PrototypeGameEngine();
        var state = StartRun(
            "overgrowth-normal-bag",
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

        var selected = new List<string>();
        for (var normalIndex = 0;
             normalIndex < 5;
             normalIndex++)
        {
            state = StartRoom(
                engine,
                state,
                PrototypeRoomType.Combat,
                floor: normalIndex + 4);
            selected.Add(state.World!.EncounterIds[^1]);

            var pool = state.World.ActOneEncounterPool!;
            Assert.Equal(
                4 + normalIndex,
                pool.OrdinaryCombatsStarted);
            Assert.Equal(
                PrototypeContent.OvergrowthNormalEncounterPool.Length
                    - normalIndex - 1,
                pool.RemainingNormalEncounterIds!.Length);
            Assert.DoesNotContain(
                selected[^1],
                pool.RemainingNormalEncounterIds);
        }

        Assert.Equal(
            selected.Count,
            selected.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            selected,
            id => Assert.Contains(
                id,
                PrototypeContent.OvergrowthNormalEncounterPool));
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
