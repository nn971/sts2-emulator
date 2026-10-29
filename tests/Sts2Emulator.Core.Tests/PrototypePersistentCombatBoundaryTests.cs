using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// H14 regression: the public relic-state residue must survive an actual
/// combat victory, reward settlement and the next map combat entry.
/// </summary>
public sealed class PrototypePersistentCombatBoundaryTests
{
    [Fact]
    public void PenNibCopiesPersistSeparatelyAcrossRewardAndNextCombat()
    {
        var engine = new PrototypeGameEngine();
        var empty = PrototypeJson.EmptyObject();
        var state = PrototypeGameFactory.Create("h14-two-combats");
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var world = state.World!;

        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance(PrototypeNativeSharedRelics.PenNibId, empty),
                    new RelicInstance(
                        PrototypeNativeSharedRelics.PenNibId,
                        JsonSerializer.SerializeToElement(
                            new { AttacksPlayed = 8 }))
                ]
            },
            World = world with
            {
                Map = new MapState(
                [
                    new MapNodeState(
                        "h14-first", 1, 1, PrototypeRoomType.Combat,
                        ["h14-second"]),
                    new MapNodeState(
                        "h14-second", 1, 2, PrototypeRoomType.Combat,
                        [])
                ], EntryNodeIds: ["h14-first"])
            }
        };

        state = engine.Step(state, GameAction.Create(
            "choose_map_node", new ChooseMapNodePayload("h14-first"))).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal([0, 8], CounterValues(state));
        Assert.Equal([0, 8], PersistentValues(state));

        state = PlayLethalStrike(engine, state);
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal([1, 9], PersistentValues(state));

        for (var n = 0; n < 32 && state.Phase == RunPhase.Reward; n++)
        {
            var actions = engine.GetLegalActions(state);
            var choice = actions.FirstOrDefault(action =>
                action.Kind.StartsWith("skip_reward", StringComparison.Ordinal))
                ?? actions.First();
            state = engine.Step(state, choice).State;
        }
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal([1, 9], PersistentValues(state));

        state = engine.Step(state, GameAction.Create(
            "choose_map_node", new ChooseMapNodePayload("h14-second"))).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal([1, 9], CounterValues(state));
        Assert.Equal([1, 9], PersistentValues(state));

        state = PlayLethalStrike(engine, state);
        Assert.Equal([2, 0], PersistentValues(state));

        var published = new PrototypeAiEnvironment().Observe(state).Observation;
        Assert.Equal(
            [2, 0],
            published.Relics
                .Select(relic => relic.State.GetProperty("AttacksPlayed").GetInt32())
                .ToArray());
    }

    [Fact]
    public void PendulumCounterIsPublishedAndResumedAtCombatEntry()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create("h14-pendulum");
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;

        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance(PrototypeNativeSharedRelics.PendulumId, empty),
                    new RelicInstance(PrototypeNativeSharedRelics.PendulumId,
                        JsonSerializer.SerializeToElement(new { TurnsSeen = 1 }))
                ]
            },
            World = state.World! with
            {
                Map = new MapState(
                [
                    new MapNodeState(
                        "h14-pendulum-first", 1, 1, PrototypeRoomType.Combat,
                        [])
                ], EntryNodeIds: ["h14-pendulum-first"])
            }
        };
        state = engine.Step(state, GameAction.Create(
            "choose_map_node",
            new ChooseMapNodePayload("h14-pendulum-first"))).State;

        // Opening-hand draws may increment either counter; independently
        // persisted residues must agree with the live combat trigger counts.
        Assert.Equal(PersistentValues(state, "TurnsSeen"),
            CounterValues(state).Select(count => count % 3).ToArray());
        var fair = new PrototypeAiEnvironment().Observe(state).Observation;
        Assert.Equal(
            PersistentValues(state, "TurnsSeen"),
            fair.Relics.Select(relic =>
                relic.State.TryGetProperty("TurnsSeen", out var count)
                    ? count.GetInt32() : 0).ToArray());
    }

    private static int[] CounterValues(RunState state) =>
        state.World!.Combat!.RelicStates
            .Select(relic => relic.TriggerCounts[0]).ToArray();

    private static int[] PersistentValues(
        RunState state, string property = "AttacksPlayed") =>
        state.Player.Relics.Select(relic =>
        {
            if (relic.PersistentState.TryGetProperty(property, out var count))
                return count.GetInt32();
            return 0;
        }).ToArray();

    private static RunState PlayLethalStrike(
        PrototypeGameEngine engine, RunState state)
    {
        var combat = state.World!.Combat!;
        var strike = combat.Cards.First(card =>
            card.CardId == "proto.silent.strike");
        var target = combat.Enemies.First(enemy => enemy.Hp > 0);
        // Keep the test about persistence independent from encounter HP and
        // randomized opening-hand order; preserve card-zone consistency.
        combat = combat with
        {
            Hand = [strike.InstanceId],
            DrawPile = combat.DrawPile.Where(id => id != strike.InstanceId).ToArray(),
            DiscardPile = combat.DiscardPile.Where(id => id != strike.InstanceId).ToArray(),
            Enemies = combat.Enemies.Select(enemy =>
                enemy.InstanceId == target.InstanceId
                    ? enemy with { Hp = 1 }
                    : enemy with { Hp = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId == strike.InstanceId
            && action.ReadPayload<PlayCardPayload>().TargetEnemyId == target.InstanceId);
        return engine.Step(state, play).State;
    }
}
