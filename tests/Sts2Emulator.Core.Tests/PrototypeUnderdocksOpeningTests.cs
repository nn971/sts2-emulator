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
        Assert.Equal(3, PrototypeNativeUnderdocks.SupportedWeakEncounterIds.Length);
        Assert.All(PrototypeNativeUnderdocks.SupportedWeakEncounterIds, id =>
        {
            Assert.Contains(id, inventory);
            var encounter = PrototypeContent.Encounter(id);
            Assert.Equal(PrototypeRoomType.Combat, encounter.RoomType);
            Assert.Equal(0, encounter.Weight);
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
        Assert.Empty(pool.RemainingNormalEncounterIds!);
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
            Assert.Equal(2 - index,
                state.World.ActOneEncounterPool.RemainingWeakEncounterIds.Length);
            Assert.All(state.World.Combat!.Enemies, enemy =>
                Assert.Contains(enemy.EnemyId, new[]
                {
                    "proto.enemy.seapunk",
                    "proto.enemy.sludge_spinner",
                    "proto.enemy.toadpole"
                }));
            PrototypeStateInvariants.Validate(state);
        }

        Assert.Equal(3, seen.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(state.World!.ActOneEncounterPool!
            .RemainingWeakEncounterIds);

        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Combat, 4));
        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Elite, 5));
        Assert.Throws<NotSupportedException>(() =>
            StartRoom(engine, state, PrototypeRoomType.Boss, 16));
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
