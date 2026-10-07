using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeFlailKnightTests
{
    [Fact]
    public void DefinitionMatchesPinnedA0RandomPolicy()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.flail_knight");

        Assert.Equal("Flail Knight", enemy.Name);
        Assert.Equal(101, enemy.MaxHp);
        Assert.Equal(0, enemy.HpPerAct);
        Assert.Equal(
            PrototypeEnemyMovePolicy.UniformRandomAfterOpener,
            enemy.MovePolicy);
        Assert.Equal(2, enemy.OpeningMoveIndex);
        Assert.Equal(
            ["war_chant", "flail", "ram"],
            enemy.Moves.Select(move => move.Id).ToArray());
        Assert.Equal(
            [1, 2, 2],
            enemy.Moves
                .Select(move => move.MaxConsecutiveUses)
                .ToArray());

        var chant = Assert.Single(enemy.Moves[0].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.ApplyEnemyPower,
            chant.Kind);
        Assert.Equal(3, chant.Amount);
        Assert.Equal(
            "proto.power.strength",
            chant.PowerId);

        var flail = Assert.Single(enemy.Moves[1].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.DamagePlayer,
            flail.Kind);
        Assert.Equal(9, flail.Amount);
        Assert.Equal(2, flail.Repetitions);

        var ram = Assert.Single(enemy.Moves[2].Effects);
        Assert.Equal(15, ram.Amount);
    }

    [Fact]
    public void RandomPolicyIsDeterministicAndRespectsRepeatLimits()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        var observed = new List<string>();
        for (var turn = 0; turn < 20; turn++)
        {
            state = EndTurn(engine, state);
            observed.Add(
                Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId!);
        }

        Assert.Equal(
            "ram",
            observed[0]);
        Assert.Equal(
            [
                "ram",
                "war_chant",
                "ram",
                "war_chant",
                "ram",
                "flail",
                "ram",
                "flail",
                "war_chant",
                "flail"
            ],
            observed.Take(10).ToArray());

        for (var index = 0; index < observed.Count;)
        {
            var move = observed[index];
            var end = index + 1;
            while (end < observed.Count
                && StringComparer.Ordinal.Equals(
                    observed[end],
                    move))
            {
                end++;
            }

            var runLength = end - index;
            var maximum = move == "war_chant"
                ? 1
                : 2;
            Assert.InRange(
                runLength,
                1,
                maximum);
            index = end;
        }
    }

    [Fact]
    public void WarChantStrengthAddsToEveryLaterHit()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        for (var turn = 0; turn < 10; turn++)
        {
            state = EndTurn(engine, state);
        }

        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        var strength = Assert.Single(
            enemy.PowerStates,
            power => power.PowerId
                == "proto.power.strength");

        Assert.Equal(9, strength.Stacks);
        Assert.Equal(9829, state.Player.Hp);
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
            10000,
            10000,
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
                    "proto.enemy.flail_knight",
                    101,
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
            "flail-knight-test",
            "flail-knight-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "flail-knight-test"),
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
