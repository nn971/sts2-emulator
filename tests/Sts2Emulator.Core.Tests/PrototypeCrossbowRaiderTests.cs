using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCrossbowRaiderTests
{
    [Fact]
    public void DefinitionMatchesPinnedAct1Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.crossbow_ruby_raider");

        Assert.Equal("Crossbow Raider", enemy.Name);
        Assert.Equal((18, 21), enemy.HpRangeAt(1, 0));
        Assert.Equal((19, 22), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.SequentialLoop,
            enemy.MovePolicy);

        Assert.Collection(
            enemy.Moves,
            reload =>
            {
                Assert.Equal("reload", reload.Id);
                var block = Assert.Single(reload.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.GainBlock,
                    block.Kind);
                Assert.Equal(3, block.Amount);
            },
            fire =>
            {
                Assert.Equal("fire", fire.Id);
                var damage = Assert.Single(fire.Effects);
                Assert.Equal(14, damage.AmountAt(1, 8));
                Assert.Equal(16, damage.AmountAt(1, 9));
            });
    }

    [Fact]
    public void AlternatesReloadAndFireStartingWithReload()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        AssertEnemy(state, "reload", block: 3);

        state = EndTurn(engine, state);
        Assert.Equal(84, state.Player.Hp);
        AssertEnemy(state, "fire", block: 0);

        state = EndTurn(engine, state);
        Assert.Equal(84, state.Player.Hp);
        AssertEnemy(state, "reload", block: 3);
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
                    "proto.enemy.crossbow_ruby_raider",
                    ascension >= 8 ? 22 : 21,
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
            "crossbow-raider-test",
            "crossbow-raider-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "crossbow-raider-test"),
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
