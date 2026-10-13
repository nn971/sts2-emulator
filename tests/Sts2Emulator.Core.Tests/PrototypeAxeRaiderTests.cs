using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeAxeRaiderTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.axe_ruby_raider");

        Assert.Equal("Axe Raider", enemy.Name);
        Assert.Equal((20, 22), enemy.HpRangeAt(1, 0));
        Assert.Equal((21, 23), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.SequentialLoop,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            swing1 => AssertSwing(swing1, "swing_1"),
            swing2 => AssertSwing(swing2, "swing_2"),
            big =>
            {
                Assert.Equal("big_swing", big.Id);
                var damage = Assert.Single(big.Effects);
                Assert.Equal(12, damage.AmountAt(1, 8));
                Assert.Equal(13, damage.AmountAt(1, 9));
            });
    }

    [Fact]
    public void SwingsHitThenLeaveBlockForFollowingPlayerTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        Assert.Equal(94, state.Player.Hp);
        AssertEnemy(state, "swing_1", block: 6);

        state = EndTurn(engine, state);
        Assert.Equal(88, state.Player.Hp);
        AssertEnemy(state, "swing_2", block: 6);

        state = EndTurn(engine, state);
        Assert.Equal(75, state.Player.Hp);
        AssertEnemy(state, "big_swing", block: 0);

        state = EndTurn(engine, state);
        Assert.Equal(69, state.Player.Hp);
        AssertEnemy(state, "swing_1", block: 6);
    }

    private static void AssertSwing(
        PrototypeEnemyMoveDefinition move,
        string id)
    {
        Assert.Equal(id, move.Id);
        Assert.Collection(
            move.Effects,
            damage =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.DamagePlayer,
                    damage.Kind);
                Assert.Equal(5, damage.AmountAt(1, 8));
                Assert.Equal(6, damage.AmountAt(1, 9));
            },
            block =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.GainBlock,
                    block.Kind);
                Assert.Equal(5, block.AmountAt(1, 8));
                Assert.Equal(6, block.AmountAt(1, 9));
            });
    }

    private static void AssertEnemy(
        RunState state,
        string moveId,
        int block)
    {
        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(moveId, enemy.LastMoveId);
        Assert.Equal(block, enemy.Block);
    }

    private static RunState CreateState(int ascension)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.axe_ruby_raider",
                    ascension >= 8 ? 23 : 22,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "axe-raider-test",
            "axe-raider-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "axe-raider-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: ascension);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }
}
