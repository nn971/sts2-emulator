using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksSeapunkHauntedShipTests
{
    [Fact]
    public void NativeNormalFormationsAndAscensionValuesMatchPinnedSource()
    {
        var seapunk = PrototypeContent.Encounter(
            "proto.encounter.seapunk_normal");
        Assert.Equal(new[]
        {
            "proto.enemy.calcified_cultist",
            "proto.enemy.seapunk"
        }, seapunk.FixedEnemySpecs.Select(spec => spec.EnemyId));
        Assert.Equal(new[] { 0, 1 },
            seapunk.FixedEnemySpecs.Select(spec => spec.FormationPosition));

        var shipEncounter = PrototypeContent.Encounter(
            "proto.encounter.haunted_ship_normal");
        Assert.Equal("proto.enemy.haunted_ship",
            Assert.Single(shipEncounter.FixedEnemySpecs).EnemyId);
        var ship = PrototypeContent.Enemy("proto.enemy.haunted_ship");
        Assert.Equal((63, 63), ship.HpRangeAt(1, 0));
        Assert.Equal((67, 67), ship.HpRangeAt(1, 8));
        Assert.Equal(PrototypeEnemyMovePolicy.StateMachine, ship.MovePolicy);
        Assert.Equal(new[] { "haunt", "swipe", "stomp" },
            ship.Moves.Select(move => move.Id));
        Assert.Equal("haunt", ship.Ai!.InitialStateId);
        Assert.Equal("swipe", ship.Ai.States.Single(state =>
            state.Id == "haunt").NextStateId);
        Assert.Equal("stomp", ship.Ai.States.Single(state =>
            state.Id == "swipe").NextStateId);
        Assert.Equal("swipe", ship.Ai.States.Single(state =>
            state.Id == "stomp").NextStateId);
        Assert.Equal(3, ship.Moves[0].Effects[0].Amount);
        Assert.Equal("proto.power.weak",
            ship.Moves[0].Effects[0].PowerId);
        Assert.Equal(5, ship.Moves[0].Effects[1].Amount);
        Assert.Equal("proto.status.dazed",
            ship.Moves[0].Effects[1].CardId);
        Assert.Equal(13, ship.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(14, ship.Moves[1].Effects[0].AmountAt(1, 9));
        Assert.Equal(3, ship.Moves[2].Effects[0].Repetitions);
        Assert.Equal(4, ship.Moves[2].Effects[0].AmountAt(1, 0));
        Assert.Equal(5, ship.Moves[2].Effects[0].AmountAt(1, 9));
    }

    [Fact]
    public void HauntedShipOpensWithWeakAndDazedThenAlternatesAttacks()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine, "proto.encounter.haunted_ship_normal");
        Assert.Equal(70, state.Player.Hp);
        Assert.Empty(state.World!.Combat!.DiscardPile);
        Assert.Equal("proto.enemy.haunted_ship",
            Assert.Single(state.World.Combat.Enemies).EnemyId);

        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        Assert.Equal(70, state.Player.Hp);
        Assert.Equal("haunt", Assert.Single(combat.Enemies).LastMoveId);
        Assert.Equal(3, combat.PlayerPowers.Single(power =>
            power.PowerId == "proto.power.weak").Stacks);
        var dazed = combat.DiscardPile
            .Select(instanceId => combat.Cards.Single(item =>
                item.InstanceId == instanceId))
            .Where(card => card.CardId == "proto.status.dazed")
            .ToArray();
        // The player's cleaned-up hand also moves to discard at end turn.
        // Count generated Dazed cards, not the entire mixed discard pile.
        Assert.Equal(5, dazed.Length);
        Assert.All(dazed, card =>
            Assert.True(PrototypeContent.Card(card.CardId).Ethereal));

        state = EndTurn(engine, state);
        Assert.Equal("swipe",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(57, state.Player.Hp);
        state = EndTurn(engine, state);
        Assert.Equal("stomp",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(45, state.Player.Hp);
        state = EndTurn(engine, state);
        Assert.Equal("swipe",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(32, state.Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void SeapunkNormalRetainsIndependentCultistRitualAndSeapunkCycle()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine, "proto.encounter.seapunk_normal");
        var combat = state.World!.Combat!;
        Assert.Equal(new[]
        {
            "proto.enemy.calcified_cultist",
            "proto.enemy.seapunk"
        }, combat.Enemies.Select(enemy => enemy.EnemyId));
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal("incantation", combat.Enemies[0].LastMoveId);
        Assert.Equal("sea_kick", combat.Enemies[1].LastMoveId);
        Assert.Equal(2, combat.Enemies[0].PowerStates.Single(power =>
            power.PowerId == "proto.power.ritual").Stacks);
        Assert.Equal(0, combat.Enemies[0].PowerStates.Where(power =>
            power.PowerId == "proto.power.strength")
                .Sum(power => power.Stacks));
        Assert.Equal(59, state.Player.Hp);

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal("dark_strike", combat.Enemies[0].LastMoveId);
        Assert.Equal("spinning_kick", combat.Enemies[1].LastMoveId);
        Assert.Equal(2, combat.Enemies[0].PowerStates.Single(power =>
            power.PowerId == "proto.power.strength").Stacks);
        Assert.Equal(42, state.Player.Hp);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state)
    {
        var end = engine.GetLegalActions(state).Single(action =>
            action.Kind == "end_turn");
        return engine.Step(state, end).State;
    }

    private static RunState FindEncounter(
        PrototypeGameEngine engine, string encounterId)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "seapunk-haunted-regression");
        for (var floor = 1; floor <= 12; floor++)
        {
            var node = new MapNodeState(
                "underdocks-new-normal:" + floor,
                1, floor, PrototypeRoomType.Combat, []);
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
            state = engine.Step(state,
                Assert.Single(engine.GetLegalActions(state))).State;
            if (state.World!.EncounterIds[^1] == encounterId)
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "The encounter was not drawn from the supported normal bag.");
    }
}
