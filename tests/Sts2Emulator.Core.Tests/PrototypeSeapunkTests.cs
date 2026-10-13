using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSeapunkTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.seapunk");

        Assert.Equal("Seapunk", enemy.Name);
        Assert.Equal((44, 46), enemy.HpRangeAt(1, 0));
        Assert.Equal((47, 49), enemy.HpRangeAt(1, 8));
        Assert.Equal(
            ["sea_kick", "spinning_kick", "bubble_burp"],
            enemy.Moves.Select(move => move.Id).ToArray());

        var seaKick = Assert.Single(enemy.Moves[0].Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.DamagePlayer,
            seaKick.Kind);
        Assert.Equal(11, seaKick.AmountAt(1, 8));
        Assert.Equal(13, seaKick.AmountAt(1, 9));

        var spinningKick = Assert.Single(
            enemy.Moves[1].Effects);
        Assert.Equal(2, spinningKick.Amount);
        Assert.Equal(4, spinningKick.Repetitions);

        Assert.Collection(
            enemy.Moves[2].Effects,
            block =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.GainBlock,
                    block.Kind);
                Assert.Equal(7, block.Amount);
            },
            strength =>
            {
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal(1, strength.Amount);
                Assert.Equal(
                    "proto.power.strength",
                    strength.PowerId);
            });
    }

    [Fact]
    public void CycleAppliesBlockStrengthAndStrengthScalesLaterHits()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(989, state.Player.Hp);
        Assert.Equal(
            "sea_kick",
            Enemy(state).LastMoveId);

        state = EndTurn(engine, state);
        Assert.Equal(981, state.Player.Hp);
        Assert.Equal(
            "spinning_kick",
            Enemy(state).LastMoveId);

        state = EndTurn(engine, state);
        var enemy = Enemy(state);
        Assert.Equal(981, state.Player.Hp);
        Assert.Equal("bubble_burp", enemy.LastMoveId);
        Assert.Equal(7, enemy.Block);
        Assert.Equal(
            1,
            Assert.Single(
                enemy.PowerStates,
                power => power.PowerId
                    == "proto.power.strength")
                .Stacks);

        // Enemy block resets before the next enemy action and Strength
        // adds one damage to the next Sea Kick.
        state = EndTurn(engine, state);
        enemy = Enemy(state);
        Assert.Equal(969, state.Player.Hp);
        Assert.Equal("sea_kick", enemy.LastMoveId);
        Assert.Equal(0, enemy.Block);
    }

    [Fact]
    public void A9UsesNativeSeaKickScaling()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);

        Assert.Equal(987, state.Player.Hp);
    }

    private static EnemyCombatState Enemy(
        RunState state) =>
        Assert.Single(state.World!.Combat!.Enemies);

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
            1000,
            1000,
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
                    "proto.enemy.seapunk",
                    PrototypeContent.Enemy(
                        "proto.enemy.seapunk")
                        .HpRangeAt(1, ascension).Max,
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
            "seapunk-test",
            "seapunk-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "seapunk-test"),
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
