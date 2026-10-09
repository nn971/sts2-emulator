using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksSkulkingColonyTests
{
    [Fact]
    public void EliteHpAndFourMovesMatchPinnedNativeSource()
    {
        var colony = PrototypeContent.Enemy(
            "proto.enemy.skulking_colony");
        Assert.Equal((75, 75), colony.HpRangeAt(1, 0));
        Assert.Equal((80, 80), colony.HpRangeAt(1, 8));
        Assert.Equal(new[]
        {
            "zoom", "zoom_2", "inertia", "piercing_stabs"
        }, colony.Moves.Select(move => move.Id));
        Assert.Equal(14, colony.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(16, colony.Moves[0].Effects[0].AmountAt(1, 9));
        Assert.Equal(9, colony.Moves[2].Effects[0].AmountAt(1, 0));
        Assert.Equal(11, colony.Moves[2].Effects[0].AmountAt(1, 9));
        Assert.Equal(2, colony.Moves[2].Effects[1].AmountAt(1, 0));
        Assert.Equal(4, colony.Moves[2].Effects[1].AmountAt(1, 9));
        Assert.Equal(2, colony.Moves[3].Effects[0].Repetitions);
        Assert.Equal(20, Assert.Single(colony.StartingPowers!).Stacks);
        Assert.True(PrototypeContent.Power("proto.power.hardened_shell")
            .EnemyHpLossLimitedPerSideTurnByStacks);
        Assert.Equal(PrototypeRoomType.Elite, PrototypeContent.Encounter(
            "proto.encounter.skulking_colony_elite").RoomType);
    }

    [Fact]
    public void HardenedShellCapsRepeatedHpLossAndResetsNextTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = StartElite(engine);
        var colony = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(75, colony.Hp);
        Assert.Equal(20, Assert.Single(colony.PowerStates).Stacks);

        for (var hit = 0; hit < 6; hit++)
        {
            state = PlayOneAttack(engine, state);
        }

        colony = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(55, colony.Hp);
        Assert.Equal(20, colony.HpLossBudgetUsed);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));

        state = EndTurn(engine, state);
        colony = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(0, colony.HpLossBudgetUsed);
        Assert.Equal("zoom", colony.LastMoveId);
        Assert.Equal(56, state.Player.Hp);

        state = PlayOneAttack(engine, state);
        colony = Assert.Single(state.World!.Combat!.Enemies);
        Assert.True(colony.Hp < 55);
        Assert.True(colony.HpLossBudgetUsed > 0);
    }

    private static RunState PlayOneAttack(
        PrototypeGameEngine engine, RunState state)
    {
        var combat = state.World!.Combat!;
        var card = combat.Cards.First(item =>
            PrototypeContent.Card(item.CardId).Type
                == PrototypeCardType.Attack
            && !PrototypeContent.Card(item.CardId).Unplayable);
        combat = combat with
        {
            Energy = 9,
            Hand = [card.InstanceId],
            DiscardPile = combat.DiscardPile
                .Where(id => id != card.InstanceId).ToArray(),
            DrawPile = combat.DrawPile
                .Where(id => id != card.InstanceId).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().TargetEnemyId
                == 1);
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn")).State;

    private static RunState StartElite(PrototypeGameEngine engine)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-skulking-regression");
        var node = new MapNodeState(
            "skulking-elite", 1, 1, PrototypeRoomType.Elite, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = 0,
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
        return engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
    }
}
