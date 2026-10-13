using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeMagiKnightTests
{
    [Fact]
    public void DefinitionMatchesPinnedA0Sequence()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.magi_knight");

        Assert.Equal("Magi Knight", enemy.Name);
        Assert.Equal(82, enemy.MaxHp);
        Assert.Equal(0, enemy.HpPerAct);
        Assert.Equal(2, enemy.MoveLoopStartIndex);
        Assert.Equal(
            [
                "power_shield",
                "dampen",
                "ram",
                "prep",
                "magic_bomb"
            ],
            enemy.Moves.Select(move => move.Id).ToArray());

        Assert.Equal(
            [6, 5],
            enemy.Moves[0].Effects
                .Select(effect => effect.Amount)
                .ToArray());

        var dampen = Assert.Single(
            enemy.Moves[1].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.ApplyPlayerPower,
            dampen.Kind);
        Assert.Equal(
            "proto.power.dampen",
            dampen.PowerId);

        Assert.Equal(
            10,
            Assert.Single(enemy.Moves[2].Effects)
                .Amount);
        Assert.Equal(
            5,
            Assert.Single(enemy.Moves[3].Effects)
                .Amount);
        Assert.Equal(
            35,
            Assert.Single(enemy.Moves[4].Effects)
                .Amount);
    }

    [Fact]
    public void CombatRunsPowerShieldDampenThenRamPrepBombLoop()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(94, state.Player.Hp);
        Assert.Equal(
            5,
            Assert.Single(
                state.World!.Combat!.Enemies)
                .Block);

        state = EndTurn(engine, state);
        Assert.Equal(94, state.Player.Hp);
        Assert.Contains(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId
                == "proto.power.dampen");

        state = EndTurn(engine, state);
        Assert.Equal(84, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal(84, state.Player.Hp);
        Assert.Equal(
            5,
            Assert.Single(
                state.World!.Combat!.Enemies)
                .Block);

        state = EndTurn(engine, state);
        Assert.Equal(49, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal(39, state.Player.Hp);

        Assert.Equal(
            6,
            Assert.Single(
                state.World!.Combat!.Enemies)
                .MoveIndex);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState()
    {
        var empty = PrototypeJson.EmptyObject();
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
                    "proto.enemy.magi_knight",
                    82,
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
            "magi-knight-test",
            "magi-knight-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "magi-knight-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                3,
                1,
                5000,
                PrototypeRoomType.Elite,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
