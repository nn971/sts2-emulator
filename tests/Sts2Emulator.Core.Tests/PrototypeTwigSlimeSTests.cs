using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTwigSlimeSTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.twig_slime_s");

        Assert.Equal("Twig Slime (S)", enemy.Name);
        Assert.Equal((7, 11), enemy.HpRangeAt(1, 0));
        Assert.Equal((8, 12), enemy.HpRangeAt(1, 8));

        var move = Assert.Single(enemy.Moves);
        Assert.Equal("tackle", move.Id);
        var effect = Assert.Single(move.Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.DamagePlayer,
            effect.Kind);
        Assert.Equal(4, effect.AmountAt(1, 8));
        Assert.Equal(5, effect.AmountAt(1, 9));
    }

    [Fact]
    public void TackleRepeatsForever()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        for (var turn = 1; turn <= 5; turn++)
        {
            state = EndTurn(engine, state);

            Assert.Equal(
                100 - (4 * turn),
                state.Player.Hp);
            var enemy = Assert.Single(
                state.World!.Combat!.Enemies);
            Assert.Equal("tackle", enemy.LastMoveId);
            Assert.Equal(turn, enemy.MoveIndex);
        }
    }

    [Fact]
    public void A9TackleUsesScaledDamage()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);

        Assert.Equal(95, state.Player.Hp);
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

        var enemyDefinition =
            PrototypeContent.Enemy(
                "proto.enemy.twig_slime_s");

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
                    enemyDefinition.Id,
                    enemyDefinition
                        .HpRangeAt(1, ascension)
                        .Max,
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
            "twig-slime-s-test",
            "twig-slime-s-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "twig-slime-s-test"),
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
