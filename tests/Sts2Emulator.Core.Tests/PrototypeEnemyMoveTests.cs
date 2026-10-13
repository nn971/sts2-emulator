using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEnemyMoveTests
{
    [Fact]
    public void EnemyGuardMoveRunsAfterRulesetBlockReset()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(60, "proto.silent.defend", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: Array.Empty<long>(),
            DrawPile: [1],
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.raider",
                    32,
                    5,
                    1,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 60, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "enemy-move-test",
            "enemy-move-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("enemy-move-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                61,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        var endTurn = engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn");

        state = engine.Step(state, endTurn).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        var raider = Assert.Single(combat.Enemies);
        Assert.Equal(6, raider.Block);
        Assert.Equal(2, raider.MoveIndex);
        Assert.Equal(64, state.Player.Hp);
        Assert.Equal(2, combat.Turn);
        Assert.Equal(new long[] { 1 }, combat.Hand);
    }
}
