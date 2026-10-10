using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeAscensionScalingTests
{
    [Fact]
    public void FactoryCarriesTypedAscension()
    {
        var state = PrototypeGameFactory.Create(
            "ascension-test",
            ascension: 9);

        Assert.Equal(9, state.Ascension);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PrototypeGameFactory.Create(
                "invalid-ascension",
                ascension: -1));
    }

    [Fact]
    public void KnightGangDefinitionsMatchA8AndA9Breakpoints()
    {
        var flail = PrototypeContent.Enemy(
            "proto.enemy.flail_knight");
        var spectral = PrototypeContent.Enemy(
            "proto.enemy.spectral_knight");
        var magi = PrototypeContent.Enemy(
            "proto.enemy.magi_knight");

        Assert.Equal(101, flail.HpAt(3, 7));
        Assert.Equal(108, flail.HpAt(3, 8));
        Assert.Equal(93, spectral.HpAt(3, 7));
        Assert.Equal(97, spectral.HpAt(3, 8));
        Assert.Equal(82, magi.HpAt(3, 7));
        Assert.Equal(89, magi.HpAt(3, 8));

        var flailAttack = Assert.Single(
            flail.Moves.Single(move =>
                move.Id == "flail").Effects);
        Assert.Equal(9, flailAttack.AmountAt(3, 8));
        Assert.Equal(10, flailAttack.AmountAt(3, 9));

        var flailRam = Assert.Single(
            flail.Moves.Single(move =>
                move.Id == "ram").Effects);
        Assert.Equal(15, flailRam.AmountAt(3, 8));
        Assert.Equal(17, flailRam.AmountAt(3, 9));

        var slash = Assert.Single(
            spectral.Moves.Single(move =>
                move.Id == "soul_slash").Effects);
        Assert.Equal(15, slash.AmountAt(3, 8));
        Assert.Equal(17, slash.AmountAt(3, 9));

        var flame = Assert.Single(
            spectral.Moves.Single(move =>
                move.Id == "soul_flame").Effects);
        Assert.Equal(3, flame.AmountAt(3, 8));
        Assert.Equal(4, flame.AmountAt(3, 9));

        var powerShield = magi.Moves.Single(move =>
            move.Id == "power_shield");
        Assert.Equal(
            6,
            powerShield.Effects.Single(effect =>
                effect.Kind
                    == PrototypeEnemyEffectKind.DamagePlayer)
                .AmountAt(3, 8));
        Assert.Equal(
            7,
            powerShield.Effects.Single(effect =>
                effect.Kind
                    == PrototypeEnemyEffectKind.DamagePlayer)
                .AmountAt(3, 9));
        Assert.Equal(
            9,
            powerShield.Effects.Single(effect =>
                effect.Kind
                    == PrototypeEnemyEffectKind.GainBlock)
                .AmountAt(3, 8));

        var prep = Assert.Single(
            magi.Moves.Single(move =>
                move.Id == "prep").Effects);
        Assert.Equal(9, prep.AmountAt(3, 8));

        var ram = Assert.Single(
            magi.Moves.Single(move =>
                move.Id == "ram").Effects);
        Assert.Equal(10, ram.AmountAt(3, 8));
        Assert.Equal(11, ram.AmountAt(3, 9));

        var bomb = Assert.Single(
            magi.Moves.Single(move =>
                move.Id == "magic_bomb").Effects);
        Assert.Equal(35, bomb.AmountAt(3, 8));
        Assert.Equal(40, bomb.AmountAt(3, 9));
    }

    [Fact]
    public void A9KnightGangUsesScaledDamageDuringCombat()
    {
        var player = new PlayerState(
            500,
            500,
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
                Enemy(
                    1,
                    "proto.enemy.flail_knight",
                    PrototypeContent.Enemy(
                        "proto.enemy.flail_knight")
                        .HpAt(3, 9)),
                Enemy(
                    2,
                    "proto.enemy.spectral_knight",
                    PrototypeContent.Enemy(
                        "proto.enemy.spectral_knight")
                        .HpAt(3, 9)),
                Enemy(
                    3,
                    "proto.enemy.magi_knight",
                    PrototypeContent.Enemy(
                        "proto.enemy.magi_knight")
                        .HpAt(3, 9))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "knight-gang-a9-test",
            "knight-gang-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "knight-gang-test"),
            PrototypeJson.EmptyObject(),
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
                null),
            Ascension: 9);

        var engine = new PrototypeGameEngine();

        // Turn 1 is fully fixed: Flail Ram 17 + Magi Power Shield 7.
        // Spectral opens with Hex and deals no damage.
        state = EndTurn(engine, state);
        Assert.Equal(476, state.Player.Hp);
        Assert.Equal(
            9,
            state.World!.Combat!.Enemies.Single(enemy =>
                enemy.InstanceId == 3).Block);

        // Turn 2 still has fixed Spectral Slash and Magi Dampen, but
        // Flail is already random. The definition-level assertions above
        // pin all A9 numeric values; this integration test only verifies
        // that the scaled values flow through combined combat state.
        state = EndTurn(engine, state);
        var hpAfterTurn2 = state.Player.Hp;
        Assert.True(hpAfterTurn2 < 476);
        Assert.Equal(
            "soul_slash",
            state.World!.Combat!.Enemies.Single(enemy =>
                enemy.InstanceId == 2).LastMoveId);
        Assert.Equal(
            "dampen",
            state.World.Combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);

        state = EndTurn(engine, state);
        Assert.True(state.Player.Hp < hpAfterTurn2);
        Assert.Equal(
            "ram",
            state.World!.Combat!.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);

        state = EndTurn(engine, state);
        Assert.Equal(
            "prep",
            state.World!.Combat!.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);
        Assert.Equal(
            9,
            state.World.Combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).Block);

        var hpBeforeBomb = state.Player.Hp;
        state = EndTurn(engine, state);
        Assert.True(state.Player.Hp <= hpBeforeBomb - 40);
        Assert.Equal(
            "magic_bomb",
            state.World!.Combat!.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static EnemyCombatState Enemy(
        int instanceId,
        string enemyId,
        int hp) =>
        new(
            instanceId,
            enemyId,
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));
}
