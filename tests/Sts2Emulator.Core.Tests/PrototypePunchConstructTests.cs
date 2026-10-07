using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePunchConstructTests
{
    [Fact]
    public void DefinitionMatchesPinnedV01110Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.punch_construct");

        Assert.Equal("Punch Construct", enemy.Name);
        Assert.Equal((55, 55), enemy.HpRangeAt(1, 0));
        Assert.Equal((60, 60), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            PrototypeEnemyMovePolicy.SequentialLoop,
            enemy.MovePolicy);

        var artifact = Assert.Single(
            enemy.StartingPowers!);
        Assert.Equal("proto.power.artifact", artifact.PowerId);
        Assert.Equal(1, artifact.Stacks);

        Assert.Collection(
            enemy.Moves,
            ready =>
            {
                Assert.Equal("ready", ready.Id);
                var block = Assert.Single(ready.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.GainBlock,
                    block.Kind);
                Assert.Equal(10, block.Amount);
            },
            fast =>
            {
                Assert.Equal("fast_punch", fast.Id);
                Assert.Collection(
                    fast.Effects,
                    damage =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.DamagePlayer,
                            damage.Kind);
                        Assert.Equal(2, damage.Repetitions);
                        Assert.Equal(5, damage.AmountAt(1, 8));
                        Assert.Equal(6, damage.AmountAt(1, 9));
                    },
                    frail =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.ApplyPlayerPower,
                            frail.Kind);
                        Assert.Equal("proto.power.frail", frail.PowerId);
                        Assert.Equal(1, frail.Amount);
                    });
            },
            strong =>
            {
                Assert.Equal("strong_punch", strong.Id);
                var damage = Assert.Single(strong.Effects);
                Assert.Equal(14, damage.AmountAt(1, 8));
                Assert.Equal(16, damage.AmountAt(1, 9));
            });
    }

    [Fact]
    public void UsesReadyFastPunchStrongPunchCycle()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 0);

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        Assert.Equal("ready", enemy.LastMoveId);
        Assert.Equal(10, enemy.Block);
        Assert.Equal(100, state.Player.Hp);

        state = EndTurn(engine, state);

        combat = state.World!.Combat!;
        enemy = Assert.Single(combat.Enemies);
        Assert.Equal("fast_punch", enemy.LastMoveId);
        Assert.Equal(0, enemy.Block);
        Assert.Equal(90, state.Player.Hp);
        Assert.Equal(
            1,
            Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.frail")
                .Stacks);

        state = EndTurn(engine, state);

        combat = state.World!.Combat!;
        enemy = Assert.Single(combat.Enemies);
        Assert.Equal("strong_punch", enemy.LastMoveId);
        Assert.Equal(76, state.Player.Hp);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId
                == "proto.power.frail");

        state = EndTurn(engine, state);

        enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal("ready", enemy.LastMoveId);
        Assert.Equal(10, enemy.Block);
    }

    [Fact]
    public void A9UsesScaledAttackValues()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);

        Assert.Equal(88, state.Player.Hp);

        state = EndTurn(engine, state);

        Assert.Equal(72, state.Player.Hp);
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
                    "proto.enemy.punch_construct",
                    ascension >= 8 ? 60 : 55,
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
            "punch-construct-test",
            "punch-construct-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "punch-construct-test"),
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
