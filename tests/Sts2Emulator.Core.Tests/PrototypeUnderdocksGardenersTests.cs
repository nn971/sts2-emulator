using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksGardenersTests
{
    [Fact]
    public void EliteUsesFourSourceBackedSlotOpenersAndAscensionValues()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.phantasmal_gardeners_elite");
        Assert.Equal(new[] { "first", "second", "third", "fourth" },
            encounter.FixedEnemySpecs.Select(spec => spec.SlotName));
        var definition = PrototypeContent.Enemy("proto.enemy.phantasmal_gardener");
        Assert.Equal((26, 31), definition.HpRangeAt(1, 0));
        Assert.Equal((27, 32), definition.HpRangeAt(1, 8));
        Assert.Equal(6, Assert.Single(definition.StartingPowers!).StacksAt(0));
        Assert.Equal(7, Assert.Single(definition.StartingPowers!).StacksAt(8));
        Assert.Equal(3, definition.Moves.Single(move =>
            move.Id == "flail").Effects[0].Repetitions);
        Assert.Equal(2, definition.Moves.Single(move =>
            move.Id == "enlarge").Effects[0].AmountAt(1, 0));
        Assert.Equal(3, definition.Moves.Single(move =>
            move.Id == "enlarge").Effects[0].AmountAt(1, 9));
        Assert.Equal(PrototypeEnemyAiStateKind.Conditional,
            definition.Ai!.States.Single(ai => ai.Id == "opening").Kind);
    }

    [Fact]
    public void EliteUsesFourDifferentFirstActionsAndRepeatingCycles()
    {
        var engine = new PrototypeGameEngine();
        var state = StartElite(engine);
        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(4, enemies.Length);
        Assert.Equal(new[] { "first", "second", "third", "fourth" },
            enemies.Select(enemy => enemy.SlotName));
        Assert.All(enemies, enemy => Assert.Equal(6,
            Assert.Single(enemy.PowerStates).Stacks));

        state = EndTurn(engine, state);
        enemies = state.World!.Combat!.Enemies;
        Assert.Equal(new[] { "flail", "bite", "lash", "enlarge" },
            enemies.Select(enemy => enemy.LastMoveId));
        Assert.Equal(55, state.Player.Hp);
        Assert.Equal(2, enemies[3].PowerStates.Where(power =>
            power.PowerId == "proto.power.strength").Sum(power => power.Stacks));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void SkittishGainIsOncePerPlayerTurnAfterUnblockedCardAttack()
    {
        var engine = new PrototypeGameEngine();
        var state = StartElite(engine);
        var combat = state.World!.Combat!;
        var enemy = combat.Enemies[0];
        Assert.Equal(0, enemy.Block);
        var strike = combat.Hand.Select(id =>
            combat.Cards.Single(card => card.InstanceId == id))
            .First(card => card.CardId == "proto.silent.strike");
        var attack = engine.GetLegalActions(state)
            .First(action => action.Kind == "play_card"
                && action.Payload.Deserialize<PlayCardPayload>()?.CardInstanceId
                    == strike.InstanceId
                && action.Payload.Deserialize<PlayCardPayload>()?.TargetEnemyId
                    == enemy.InstanceId);
        state = engine.Step(state, attack).State;
        enemy = state.World!.Combat!.Enemies[0];
        Assert.True(enemy.GainedReactiveBlockThisTurn);
        Assert.Equal(6, enemy.Block);
        state = EndTurn(engine, state);
        enemy = state.World!.Combat!.Enemies[0];
        Assert.False(enemy.GainedReactiveBlockThisTurn);
        Assert.Equal(0, enemy.Block);
    }

    private static RunState StartElite(PrototypeGameEngine engine)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "phantasmal-gardeners-elite");
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = 5,
                ActiveRoom = null,
                Map = new MapState(
                    [new MapNodeState("gardeners-elite", 1, 6,
                        PrototypeRoomType.Elite, [])],
                    CurrentNodeId: null,
                    EntryNodeIds: ["gardeners-elite"],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Combat = null,
                Event = null,
                Reward = null,
                Shop = null,
                ActOneEncounterPool = state.World.ActOneEncounterPool! with
                {
                    RemainingEliteEncounterIds =
                        ["proto.encounter.phantasmal_gardeners_elite"]
                }
            }
        };
        return engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
    }

    private static RunState EndTurn(PrototypeGameEngine engine, RunState state)
    {
        return engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.Kind == "end_turn")).State;
    }
}
