using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActRoutingTests
{
    [Theory]
    [InlineData(2, 20, 4, 10, 3, 3)]
    [InlineData(3, 18, 3, 9, 3, 3)]
    public void PinnedInventoriesMatchAllRegisteredNativeEncounterIds(
        int act, int total, int weak, int normal, int elite, int boss)
    {
        var weakIds = PrototypeNativeLaterActRouting.WeakEncounterIds(act);
        var normalIds = PrototypeNativeLaterActRouting.EncounterIds(
            act, PrototypeRoomType.Combat);
        var eliteIds = PrototypeNativeLaterActRouting.EncounterIds(
            act, PrototypeRoomType.Elite);
        var bossIds = PrototypeNativeLaterActRouting.EncounterIds(
            act, PrototypeRoomType.Boss);

        Assert.Equal(weak, weakIds.Length);
        Assert.Equal(normal, normalIds.Length);
        Assert.Equal(elite, eliteIds.Length);
        Assert.Equal(boss, bossIds.Length);
        Assert.Equal(total, weakIds.Concat(normalIds).Concat(eliteIds)
            .Concat(bossIds).Distinct(StringComparer.Ordinal).Count());
        Assert.All(weakIds.Concat(normalIds), id =>
        {
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
            Assert.Equal(0, encounter.Weight);
        });
        Assert.All(eliteIds, id =>
        {
            Assert.Equal(PrototypeRoomType.Elite,
                PrototypeContent.Encounter(id).RoomType);
            Assert.Equal(0, PrototypeContent.Encounter(id).Weight);
        });
        Assert.All(bossIds, id =>
        {
            Assert.Equal(PrototypeRoomType.Boss,
                PrototypeContent.Encounter(id).RoomType);
            Assert.Equal(0, PrototypeContent.Encounter(id).Weight);
        });
    }

    [Theory]
    [InlineData(2, 14, 15,
        PrototypeNativeLaterActRouting.HiveMapProfile)]
    [InlineData(3, 13, 14,
        PrototypeNativeLaterActRouting.GloryMapProfile)]
    public void MapsHaveSourcePinnedFloorsAndDeterministicConnectivity(
        int act, int rows, int bossFloor, string profile)
    {
        foreach (var ascension in new[] { 0, 1, 10 })
        for (var index = 0; index < 12; index++)
        {
            var seed = $"later-map-{act}-{ascension}-{index}";
            var map = PrototypeNativeLaterActRouting.GenerateMap(
                act, PrototypeRng.CreateBundle(seed), ascension);
            var copy = PrototypeNativeLaterActRouting.GenerateMap(
                act, PrototypeRng.CreateBundle(seed), ascension);
            Assert.Equal(profile, map.GenerationProfileId);
            Assert.Equal(CanonicalJson.Sha256(map), CanonicalJson.Sha256(copy));
            Assert.Equal(bossFloor, map.Nodes.Max(node => node.Floor));
            Assert.All(Enumerable.Range(1, bossFloor), floor =>
                Assert.Contains(map.Nodes, node => node.Floor == floor));
            Assert.All(map.Nodes, node => Assert.Equal(act, node.Act));
            Assert.All(map.Nodes.Where(node => node.Floor == 1),
                node => Assert.Equal(PrototypeRoomType.Combat, node.RoomType));
            Assert.All(map.Nodes.Where(node => node.Floor == 9),
                node => Assert.Equal(PrototypeRoomType.Treasure, node.RoomType));
            Assert.All(map.Nodes.Where(node => node.Floor == rows),
                node => Assert.Equal(PrototypeRoomType.Rest, node.RoomType));
            Assert.Equal(1, map.Nodes.Count(node =>
                node.RoomType == PrototypeRoomType.Boss));
            Assert.Equal(bossFloor,
                Assert.Single(map.Nodes, node =>
                    node.RoomType == PrototypeRoomType.Boss).Floor);
            Assert.Equal(3, map.Nodes.Count(node =>
                node.RoomType == PrototypeRoomType.Shop));
            var nodesById = map.Nodes.ToDictionary(node => node.NodeId,
                StringComparer.Ordinal);
            Assert.All(map.EntryNodeIds!, id =>
                Assert.Equal(1, nodesById[id].Floor));
            Assert.All(map.Nodes, node =>
            {
                Assert.StartsWith($"{act}:", node.NodeId);
                Assert.All(node.NextNodeIds ?? [],
                    id => Assert.Equal(node.Floor + 1,
                        nodesById[id].Floor));
            });
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void WeakNormalEliteBossBagsAreIndependentAndForkable(int act)
    {
        var rng = PrototypeRng.CreateBundle($"bags-{act}");
        var initial = PrototypeNativeLaterActRouting.Create(act, rng);
        Assert.Equal(0, initial.OrdinaryCombatsStarted);
        Assert.Contains(initial.BossEncounterId,
            PrototypeNativeLaterActRouting.EncounterIds(
                act, PrototypeRoomType.Boss));
        Assert.NotSame(initial.RemainingWeakEncounterIds,
            initial.Fork().RemainingWeakEncounterIds);

        var first = PrototypeNativeLaterActRouting.Pick(initial,
            PrototypeRoomType.Combat, null, rng);
        var second = PrototypeNativeLaterActRouting.Pick(first.NextPool,
            PrototypeRoomType.Combat, first.Encounter.Id, rng);
        Assert.NotEqual(first.Encounter.Id, second.Encounter.Id);
        Assert.Empty(second.NextPool.RemainingWeakEncounterIds);
        Assert.Equal(2, second.NextPool.OrdinaryCombatsStarted);
        Assert.Equal(initial.RemainingNormalEncounterIds,
            second.NextPool.RemainingNormalEncounterIds);

        var third = PrototypeNativeLaterActRouting.Pick(second.NextPool,
            PrototypeRoomType.Combat, second.Encounter.Id, rng);
        Assert.Contains(third.Encounter.Id,
            initial.RemainingNormalEncounterIds);
        Assert.Equal(3, third.NextPool.OrdinaryCombatsStarted);
        Assert.Equal(initial.RemainingNormalEncounterIds.Length - 1,
            third.NextPool.RemainingNormalEncounterIds.Length);

        var elite = PrototypeNativeLaterActRouting.Pick(third.NextPool,
            PrototypeRoomType.Elite, third.Encounter.Id, rng);
        Assert.Contains(elite.Encounter.Id,
            initial.RemainingEliteEncounterIds);
        Assert.Equal(initial.RemainingEliteEncounterIds.Length - 1,
            elite.NextPool.RemainingEliteEncounterIds.Length);
        Assert.Equal(third.NextPool.OrdinaryCombatsStarted,
            elite.NextPool.OrdinaryCombatsStarted);

        var boss = PrototypeNativeLaterActRouting.Pick(elite.NextPool,
            PrototypeRoomType.Boss, elite.Encounter.Id, rng);
        Assert.Equal(initial.BossEncounterId, boss.Encounter.Id);
        Assert.Equal(elite.NextPool, boss.NextPool);

        var replayRng = PrototypeRng.CreateBundle($"replay-{act}");
        var replayPool = PrototypeNativeLaterActRouting.Create(act, replayRng);
        var replayFork = replayRng.Fork();
        var bagFork = replayPool.Fork();
        var left = PrototypeNativeLaterActRouting.Pick(replayPool,
            PrototypeRoomType.Combat, null, replayRng);
        var right = PrototypeNativeLaterActRouting.Pick(bagFork,
            PrototypeRoomType.Combat, null, replayFork);
        Assert.Equal(left.Encounter.Id, right.Encounter.Id);
        Assert.Equal(left.NextPool.RemainingWeakEncounterIds,
            right.NextPool.RemainingWeakEncounterIds);
        Assert.Equal(CanonicalJson.Sha256(replayRng),
            CanonicalJson.Sha256(replayFork));
    }

    [Theory]
    [InlineData(ActIdentity.Overgrowth)]
    [InlineData(ActIdentity.Underdocks)]
    public void ConfiguredSilentRunsContinueAcrossBothNativeLaterActs(
        ActIdentity firstAct)
    {
        var engine = new PrototypeGameEngine();
        var seed = "act-transitions-" + firstAct;
        var state = V111RunFactory.Create(seed,
            RunConfiguration.Create(firstAct: firstAct));
        state = engine.Step(state, GameAction.Empty("start_run")).State;
        Assert.Equal(1, state.World!.Act);
        Assert.Null(state.World.LaterActEncounterPool);

        // A controlled act-boundary fixture. The boss/reward pipeline
        // enters ActTransition only after actually clearing the boss.
        state = state with { Phase = RunPhase.ActTransition };
        state = engine.Step(state, GameAction.Empty("continue_act")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(2, state.World!.Act);
        Assert.Equal(ActIdentity.Hive, state.World.ActIdentity);
        Assert.Null(state.World.ActOneEncounterPool);
        Assert.Equal(PrototypeNativeLaterActRouting.HiveMapProfile,
            state.World.Map.GenerationProfileId);
        Assert.Equal(2, state.World.LaterActEncounterPool!.Act);
        var firstChoice = engine.GetLegalActions(state)[0];
        var fork = state.Fork();
        var hive = engine.Step(state, firstChoice).State;
        Assert.Equal(RunPhase.Combat, hive.Phase);
        Assert.Contains(hive.World!.EncounterIds[^1],
            PrototypeNativeLaterActRouting.WeakEncounterIds(2));
        Assert.Equal(CanonicalJson.Sha256(hive),
            CanonicalJson.Sha256(engine.Step(fork, firstChoice).State));
        Assert.Equal(1,
            hive.World.LaterActEncounterPool!.OrdinaryCombatsStarted);
        Assert.Null(hive.World.ActOneEncounterPool);

        state = hive with { Phase = RunPhase.ActTransition };
        state = engine.Step(state, GameAction.Empty("continue_act")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(3, state.World!.Act);
        Assert.Equal(ActIdentity.Glory, state.World.ActIdentity);
        Assert.Equal(PrototypeNativeLaterActRouting.GloryMapProfile,
            state.World.Map.GenerationProfileId);
        Assert.Equal(0, state.World.LaterActEncounterPool!.OrdinaryCombatsStarted);
        var glory = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, glory.Phase);
        Assert.Contains(glory.World!.EncounterIds[^1],
            PrototypeNativeLaterActRouting.WeakEncounterIds(3));
        Assert.Equal(1,
            glory.World.LaterActEncounterPool!.OrdinaryCombatsStarted);
        Assert.Contains(hive.World.EncounterIds[^1],
            glory.World.EncounterIds);
    }

    [Fact]
    public void LaterActBossChoiceIsRolledOnceAndCannotConsumeOtherBags()
    {
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create("boss-commit");
        state = engine.Step(state, GameAction.Empty("start_run")).State;
        state = engine.Step(state with { Phase = RunPhase.ActTransition },
            GameAction.Empty("continue_act")).State;
        var initial = state.World!.LaterActEncounterPool!.Fork();
        var boss = new MapNodeState("2:15:3", 2, 15,
            PrototypeRoomType.Boss);
        state = state with
        {
            World = state.World with
            {
                Map = new MapState([boss], EntryNodeIds: [boss.NodeId],
                    GenerationProfileId:
                        PrototypeNativeLaterActRouting.HiveMapProfile)
            }
        };
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(initial.BossEncounterId, state.World!.EncounterIds[^1]);
        var unchanged = state.World.LaterActEncounterPool!;
        Assert.Equal(initial.BossEncounterId, unchanged.BossEncounterId);
        Assert.Equal(initial.OrdinaryCombatsStarted,
            unchanged.OrdinaryCombatsStarted);
        Assert.Equal(initial.RemainingWeakEncounterIds,
            unchanged.RemainingWeakEncounterIds);
        Assert.Equal(initial.RemainingNormalEncounterIds,
            unchanged.RemainingNormalEncounterIds);
        Assert.Equal(initial.RemainingEliteEncounterIds,
            unchanged.RemainingEliteEncounterIds);
    }

    [Fact]
    public void UnsupportedHiveAndGloryUnknownRoomsNeverFallBackToActOneEvents()
    {
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create("no-legacy-event");
        state = engine.Step(state, GameAction.Empty("start_run")).State;
        state = engine.Step(state with { Phase = RunPhase.ActTransition },
            GameAction.Empty("continue_act")).State;

        foreach (var act in new[] { 2, 3 })
        {
            var world = state.World!;
            var node = new MapNodeState($"{act}:2:0", act, 2,
                PrototypeRoomType.Unknown);
            var fixture = state with
            {
                World = world with
                {
                    Map = new MapState([node], EntryNodeIds: [node.NodeId],
                        GenerationProfileId: world.Map.GenerationProfileId)
                }
            };
            var hash = CanonicalJson.Sha256(fixture);
            var ex = Assert.Throws<NotSupportedException>(() =>
                engine.Step(fixture, engine.GetLegalActions(fixture)[0]));
            Assert.Contains("No prototype event fallback", ex.Message);
            Assert.Equal(hash, CanonicalJson.Sha256(fixture));
            if (act == 2)
                state = engine.Step(state with
                {
                    Phase = RunPhase.ActTransition
                }, GameAction.Empty("continue_act")).State;
        }
    }
}
