using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCubexConstructTests
{
    [Fact]
    public void DefinitionMatchesPinnedV01110Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.cubex_construct");

        Assert.Equal("Cubex Construct", enemy.Name);
        Assert.Equal((65, 65), enemy.HpRangeAt(1, 0));
        Assert.Equal((70, 70), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.StateMachine,
            enemy.MovePolicy);

        var artifact = Assert.Single(
            enemy.StartingPowers!);
        Assert.Equal("proto.power.artifact", artifact.PowerId);
        Assert.Equal(1, artifact.Stacks);

        Assert.Collection(
            enemy.Moves,
            charge =>
            {
                Assert.Equal("charge_up", charge.Id);
                var strength = Assert.Single(charge.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal("proto.power.strength", strength.PowerId);
                Assert.Equal(2, strength.Amount);
            },
            blast =>
            {
                Assert.Equal("repeater_blast", blast.Id);
                AssertRepeaterBlast(blast);
            },
            blast =>
            {
                Assert.Equal("repeater_blast_2", blast.Id);
                AssertRepeaterBlast(blast);
            },
            expel =>
            {
                Assert.Equal("expel", expel.Id);
                var damage = Assert.Single(expel.Effects);
                Assert.Equal(2, damage.Repetitions);
                Assert.Equal(5, damage.AmountAt(1, 8));
                Assert.Equal(6, damage.AmountAt(1, 9));
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("charge_up", ai.InitialStateId);
        Assert.Equal(
            "repeater_blast",
            Assert.Single(
                ai.States,
                state => state.Id == "expel")
                .NextStateId);
    }

    [Fact]
    public void ChargeOccursOnceThenRepeaterRepeaterExpelLoops()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 0);

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        AssertEnemy(state, "charge_up", strength: 2);

        state = EndTurn(engine, state);
        Assert.Equal(91, state.Player.Hp);
        AssertEnemy(state, "repeater_blast", strength: 4);

        state = EndTurn(engine, state);
        Assert.Equal(80, state.Player.Hp);
        AssertEnemy(state, "repeater_blast_2", strength: 6);

        state = EndTurn(engine, state);
        Assert.Equal(58, state.Player.Hp);
        AssertEnemy(state, "expel", strength: 6);

        state = EndTurn(engine, state);
        Assert.Equal(45, state.Player.Hp);
        AssertEnemy(state, "repeater_blast", strength: 8);
    }

    [Fact]
    public void A9UsesScaledAttackValues()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        for (var turn = 0; turn < 4; turn++)
        {
            state = EndTurn(engine, state);
        }

        Assert.Equal(54, state.Player.Hp);
        AssertEnemy(state, "expel", strength: 6);
    }

    private static void AssertRepeaterBlast(
        PrototypeEnemyMoveDefinition move)
    {
        Assert.Collection(
            move.Effects,
            damage =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.DamagePlayer,
                    damage.Kind);
                Assert.Equal(7, damage.AmountAt(1, 8));
                Assert.Equal(8, damage.AmountAt(1, 9));
            },
            strength =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal("proto.power.strength", strength.PowerId);
                Assert.Equal(2, strength.Amount);
            });
    }

    private static void AssertEnemy(
        RunState state,
        string moveId,
        int strength)
    {
        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(moveId, enemy.LastMoveId);
        Assert.Equal(
            strength,
            Assert.Single(
                enemy.PowerStates,
                power => power.PowerId
                    == "proto.power.strength")
                .Stacks);
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
                    "proto.enemy.cubex_construct",
                    ascension >= 8 ? 70 : 65,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    Powers:
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.artifact",
                            1,
                            1)
                    ])
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 2);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "cubex-construct-test",
            "cubex-construct-test",
            ascension,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "cubex-construct-test"),
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
                null));
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
