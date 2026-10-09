using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksOpeningTests
{
    [Fact]
    public void SourceInventoryHasExactlyTwentyDistinctEncounters()
    {
        var inventory = PrototypeNativeUnderdocks.NativeWeakEncounterIds
            .Concat(PrototypeNativeUnderdocks.NativeNormalEncounterIds)
            .Concat(PrototypeNativeUnderdocks.NativeEliteEncounterIds)
            .Concat(PrototypeNativeUnderdocks.NativeBossEncounterIds)
            .ToArray();
        Assert.Equal(20, inventory.Length);
        Assert.Equal(20, inventory.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, PrototypeNativeUnderdocks.NativeWeakEncounterIds.Length);
        Assert.Equal(4, PrototypeNativeUnderdocks.SupportedWeakEncounterIds.Length);
        Assert.All(PrototypeNativeUnderdocks.SupportedWeakEncounterIds, id =>
        {
            Assert.Contains(id, inventory);
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
            Assert.Equal(0, encounter.Weight);
        });
        Assert.Equal(10, PrototypeNativeUnderdocks.NativeNormalEncounterIds.Length);
        Assert.Equal(10, PrototypeNativeUnderdocks.SupportedNormalEncounterIds.Length);
        Assert.All(PrototypeNativeUnderdocks.SupportedNormalEncounterIds, id =>
        {
            Assert.Contains(id,
                PrototypeNativeUnderdocks.NativeNormalEncounterIds);
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
            Assert.Equal(0, encounter.Weight);
            Assert.DoesNotContain(id,
                PrototypeContent.OvergrowthNormalEncounterPool);
        });
        Assert.All(PrototypeNativeUnderdocks.NativeBossEncounterIds, id =>
            Assert.DoesNotContain(id,
                PrototypeNativeUnderdocks.SupportedWeakEncounterIds));
    }

    [Fact]
    public void OptInRunHasUnderdocksRegionSharedNeowAndStableMap()
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-starter", ascension: 0);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(PrototypeActOneRegion.Underdocks,
            state.World!.ActOneRegion);
        Assert.Equal(PrototypeNativeUnderdocks.GenerationProfileId,
            state.World.Map.GenerationProfileId);
        Assert.Equal(PrototypeNativeOvergrowthEvents.NeowEventId,
            state.World.Event!.EventId);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(3, state.Player.PotionSlots.Length);
        Assert.Equal(16, state.World.Map.Nodes.Max(n => n.Floor));
        Assert.Equal(1, state.World.Map.Nodes.Min(n => n.Floor));
        var pool = state.World.ActOneEncounterPool!;
        Assert.Equal(0, pool.OrdinaryCombatsStarted);
        Assert.Equal(PrototypeNativeUnderdocks.SupportedWeakEncounterIds,
            pool.RemainingWeakEncounterIds);
        Assert.Equal(PrototypeNativeUnderdocks.SupportedNormalEncounterIds,
            pool.RemainingNormalEncounterIds!);
        Assert.Empty(pool.RemainingEliteEncounterIds!);
        Assert.Contains(pool.BossEncounterId,
            PrototypeNativeUnderdocks.NativeBossEncounterIds);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FirstThreeOrdinaryFightsAreRegionSpecificAndNonRepeating()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-first-three");
        var seen = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            state = StartRoom(engine, state,
                PrototypeRoomType.Combat, index + 1);
            var id = state.World!.EncounterIds[^1];
            seen.Add(id);
            Assert.Contains(id,
                PrototypeNativeUnderdocks.SupportedWeakEncounterIds);
            Assert.Equal(index + 1,
                state.World.ActOneEncounterPool!.OrdinaryCombatsStarted);
            Assert.Equal(3 - index,
                state.World.ActOneEncounterPool.RemainingWeakEncounterIds.Length);
            Assert.All(state.World.Combat!.Enemies, enemy =>
                Assert.Contains(enemy.EnemyId, new[]
                {
                    "proto.enemy.corpse_slug",
                    "proto.enemy.seapunk",
                    "proto.enemy.sludge_spinner",
                    "proto.enemy.toadpole"
                }));
            // The isolated combat test uses a synthetic one-node map
            // to enter a specific room without playing prior floors.
            // Full native-map invariants are exercised by the opener test.
        }

        Assert.Equal(3, seen.Distinct(StringComparer.Ordinal).Count());
        Assert.Single(state.World!.ActOneEncounterPool!
            .RemainingWeakEncounterIds);

        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Elite, 5));
        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Boss, 16));
    }

    [Fact]
    public void NormalEncounterBagDrawsTenDistinctSupportedFights()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-normal-bag");
        for (var floor = 1; floor <= 3; floor++)
        {
            state = StartRoom(engine, state, PrototypeRoomType.Combat, floor);
        }

        Assert.Equal(3,
            state.World!.ActOneEncounterPool!.OrdinaryCombatsStarted);
        Assert.Equal(PrototypeNativeUnderdocks.SupportedNormalEncounterIds,
            state.World.ActOneEncounterPool.RemainingNormalEncounterIds);

        var selected = new List<string>();
        for (var floor = 4; floor <= 13; floor++)
        {
            var before = CanonicalJson.Sha256(state);
            var replay = state.Fork();
            var original = StartRoom(
                engine, state, PrototypeRoomType.Combat, floor);
            var repeated = StartRoom(
                engine, replay, PrototypeRoomType.Combat, floor);
            Assert.Equal(CanonicalJson.Sha256(original),
                CanonicalJson.Sha256(repeated));
            Assert.Equal(before, CanonicalJson.Sha256(state));
            state = original;

            var encounterId = state.World!.EncounterIds[^1];
            selected.Add(encounterId);
            Assert.Contains(encounterId,
                PrototypeNativeUnderdocks.SupportedNormalEncounterIds);
            Assert.Equal(floor,
                state.World.ActOneEncounterPool!.OrdinaryCombatsStarted);
            Assert.Equal(13 - floor,
                state.World.ActOneEncounterPool.RemainingNormalEncounterIds!.Length);

            var combat = state.World.Combat!;
            if (encounterId == "proto.encounter.corpse_slugs_normal")
            {
                Assert.Equal(3, combat.Enemies.Length);
                Assert.All(combat.Enemies, enemy =>
                    Assert.Equal("proto.enemy.corpse_slug", enemy.EnemyId));
                Assert.Equal(3,
                    combat.Enemies.Select(enemy => enemy.AiStateId)
                        .Distinct().Count());
            }
            else if (encounterId == "proto.encounter.punch_construct_normal")
            {
                Assert.Equal("proto.enemy.punch_construct",
                    Assert.Single(combat.Enemies).EnemyId);
                Assert.Single(Assert.Single(combat.Enemies).PowerStates,
                    power => power.PowerId == "proto.power.artifact");
            }
            else if (encounterId == "proto.encounter.cultists_normal")
            {
                Assert.Equal(new[]
                {
                    "proto.enemy.calcified_cultist",
                    "proto.enemy.damp_cultist"
                }, combat.Enemies.Select(enemy => enemy.EnemyId));
                Assert.All(combat.Enemies, enemy =>
                    Assert.Empty(enemy.PowerStates));
            }
            else if (encounterId == "proto.encounter.fossil_stalker_normal")
            {
                Assert.Equal("proto.enemy.fossil_stalker",
                    Assert.Single(combat.Enemies).EnemyId);
                Assert.Single(Assert.Single(combat.Enemies).PowerStates,
                    power => power.PowerId == "proto.power.suck");
            }
            else if (encounterId == "proto.encounter.seapunk_normal")
            {
                Assert.Equal(new[]
                {
                    "proto.enemy.calcified_cultist",
                    "proto.enemy.seapunk"
                }, combat.Enemies.Select(enemy => enemy.EnemyId));
                Assert.All(combat.Enemies, enemy =>
                    Assert.Empty(enemy.PowerStates));
            }
            else
            {
                if (encounterId == "proto.encounter.haunted_ship_normal")
                {
                    Assert.Equal("proto.enemy.haunted_ship",
                        Assert.Single(combat.Enemies).EnemyId);
                    Assert.Empty(Assert.Single(combat.Enemies).PowerStates);
                }
                else if (encounterId == "proto.encounter.sewer_clam_normal")
                {
                    var clam = Assert.Single(combat.Enemies);
                    Assert.Equal("proto.enemy.sewer_clam", clam.EnemyId);
                    Assert.Equal(8, clam.Block);
                    Assert.Equal(8, Assert.Single(clam.PowerStates).Stacks);
                }
                else if (encounterId == "proto.encounter.living_fog_normal")
                {
                    var fog = Assert.Single(combat.Enemies);
                    Assert.Equal("proto.enemy.living_fog", fog.EnemyId);
                    Assert.Equal("livingFog", fog.SlotName);
                }
                else if (encounterId
                    == "proto.encounter.gremlin_merc_normal")
                {
                    Assert.Equal("proto.enemy.gremlin_merc",
                        Assert.Single(combat.Enemies).EnemyId);
                    Assert.Equal("merc", Assert.Single(combat.Enemies).SlotName);
                    Assert.Equal(2, Assert.Single(combat.Enemies).PowerStates.Length);
                }
                else
                {
                    Assert.Equal("proto.encounter.two_tailed_rats_normal",
                        encounterId);
                    Assert.Equal(3, combat.Enemies.Length);
                    Assert.All(combat.Enemies, rat =>
                        Assert.Equal("proto.enemy.two_tailed_rat", rat.EnemyId));
                    Assert.Equal(3, combat.Enemies.Select(rat => rat.AiStateId)
                        .Distinct().Count());
                    Assert.All(combat.Enemies, rat =>
                        Assert.Equal(2, rat.NonSummonMovesUntilEligible));
                }
            }
        }

        Assert.Equal(10, selected.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(state.World!.ActOneEncounterPool!
            .RemainingNormalEncounterIds!);
        Assert.Single(state.World.ActOneEncounterPool.RemainingWeakEncounterIds);
        var finalHash = CanonicalJson.Sha256(state);
        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Combat, 14));
        Assert.Equal(finalHash, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void SourceBackedNormalFormationsUseExistingEnemyMechanics()
    {
        var slugs = PrototypeContent.Encounter(
            "proto.encounter.corpse_slugs_normal");
        Assert.Equal(3, slugs.FixedEnemySpecs.Length);
        Assert.Equal(new[] { "whip", "glomp", "goop" },
            slugs.CyclicOpeningAiStateIds);

        var punch = PrototypeContent.Enemy("proto.enemy.punch_construct");
        Assert.Equal((55, 55), punch.HpRangeAt(1, 0));
        Assert.Equal((60, 60), punch.HpRangeAt(1, 8));
        Assert.Equal(new[] { "ready", "fast_punch", "strong_punch" },
            punch.Moves.Select(move => move.Id));
        Assert.Equal(1,
            punch.StartingPowers!.Single().Stacks);
        Assert.Equal("proto.power.artifact",
            punch.StartingPowers!.Single().PowerId);
        Assert.Equal(2, punch.Moves[1].Effects[0].Repetitions);
    }

    [Fact]
    public void SludgeSpinnerIsSourceBackedAtAscensionBoundaries()
    {
        var spinner = PrototypeContent.Enemy("proto.enemy.sludge_spinner");
        Assert.Equal(37, spinner.HpRangeAt(1, 0).Min);
        Assert.Equal(39, spinner.HpRangeAt(1, 0).Max);
        Assert.Equal(41, spinner.HpRangeAt(1, 8).Min);
        Assert.Equal(42, spinner.HpRangeAt(1, 8).Max);
        Assert.Equal(3, spinner.Moves.Length);
        Assert.Equal(PrototypeEnemyMovePolicy.StateMachine,
            spinner.MovePolicy);
        Assert.NotNull(spinner.Ai);
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.sludge_spinner_weak");
        Assert.Single(encounter.EnemyIds);
    }

    [Fact]
    public void StandardPrototypeOvergrowthResetRemainsUnchanged()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("underdocks-is-opt-in");
        state = engine.Step(state, GameAction.Empty("start_run")).State;
        Assert.Equal(PrototypeActOneRegion.Overgrowth,
            state.World!.ActOneRegion);
        Assert.Equal(PrototypeContent.MapGenerationProfileId,
            state.World.Map.GenerationProfileId);
        PrototypeStateInvariants.Validate(state);
    }

    private static RunState StartRoom(
        PrototypeGameEngine engine,
        RunState state,
        PrototypeRoomType kind,
        int floor)
    {
        var node = new MapNodeState(
            $"underdocks-test:{floor}:{kind}",
            1, floor, kind, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = floor - 1,
                ActiveRoom = null,
                Map = new MapState(
                    [node], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Combat = null,
                Reward = null,
                Shop = null,
                Event = null
            }
        };
        var action = Assert.Single(engine.GetLegalActions(state));
        return engine.Step(state, action).State;
    }
}
