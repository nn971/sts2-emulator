using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeAct1SimpleEnemyTests
{
    [Fact]
    public void AssassinRaiderMatchesPinnedData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.assassin_ruby_raider");

        Assert.Equal("Assassin Raider", enemy.Name);
        Assert.Equal((18, 23), enemy.HpRangeAt(1, 0));
        Assert.Equal((19, 24), enemy.HpRangeAt(1, 8));

        var move = Assert.Single(enemy.Moves);
        Assert.Equal("killshot", move.Id);
        var effect = Assert.Single(move.Effects);
        Assert.Equal(10, effect.AmountAt(1, 8));
        Assert.Equal(11, effect.AmountAt(1, 9));

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemy.Id,
            enemy.HpRangeAt(1, 0).Max);

        for (var turn = 1; turn <= 4; turn++)
        {
            state = EndTurn(engine, state);
            Assert.Equal(
                100 - (10 * turn),
                state.Player.Hp);
            Assert.Equal(
                "killshot",
                Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId);
        }
    }

    [Fact]
    public void SnappingJaxfruitRepeatedOrbAccumulatesStrength()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.snapping_jaxfruit");

        Assert.Equal("Snapping Jaxfruit", enemy.Name);
        Assert.Equal((31, 33), enemy.HpRangeAt(1, 0));
        Assert.Equal((34, 36), enemy.HpRangeAt(1, 8));

        var move = Assert.Single(enemy.Moves);
        Assert.Equal("energy_orb", move.Id);
        Assert.Collection(
            move.Effects,
            damage =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.DamagePlayer,
                    damage.Kind);
                Assert.Equal(3, damage.AmountAt(1, 8));
                Assert.Equal(4, damage.AmountAt(1, 9));
            },
            strength =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal(2, strength.Amount);
                Assert.Equal(
                    "proto.power.strength",
                    strength.PowerId);
            });

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemy.Id,
            enemy.HpRangeAt(1, 0).Max);

        state = EndTurn(engine, state);
        Assert.Equal(97, state.Player.Hp);
        AssertStrength(state, 2);

        state = EndTurn(engine, state);
        Assert.Equal(92, state.Player.Hp);
        AssertStrength(state, 4);

        state = EndTurn(engine, state);
        Assert.Equal(85, state.Player.Hp);
        AssertStrength(state, 6);
    }

    [Fact]
    public void SnappingJaxfruitA9ScalesBaseAttackBeforeStrength()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.snapping_jaxfruit");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            enemy.Id,
            enemy.HpRangeAt(1, 9).Max,
            ascension: 9);

        state = EndTurn(engine, state);
        Assert.Equal(96, state.Player.Hp);
        AssertStrength(state, 2);

        state = EndTurn(engine, state);
        Assert.Equal(90, state.Player.Hp);
        AssertStrength(state, 4);
    }

    private static void AssertStrength(
        RunState state,
        int expected)
    {
        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(
            expected,
            Assert.Single(
                enemy.PowerStates,
                power => power.PowerId
                    == "proto.power.strength")
                .Stacks);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        string enemyId,
        int enemyHp,
        int ascension = 0)
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
                    enemyId,
                    enemyHp,
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
            "act1-simple-enemy-test",
            "act1-simple-enemy-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "act1-simple-enemy-test"),
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
}
