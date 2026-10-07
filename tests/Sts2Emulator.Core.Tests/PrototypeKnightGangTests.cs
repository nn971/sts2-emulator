using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeKnightGangTests
{
    [Fact]
    public void EncounterIsReachableActThreeEliteContent()
    {
        var encounter = Assert.Single(
            PrototypeContent.Encounters,
            item => item.Id
                == "proto.encounter.knight_gang");

        Assert.Equal(
            PrototypeRoomType.Elite,
            encounter.RoomType);
        Assert.Equal(3, encounter.MinAct);
        Assert.Equal(3, encounter.MaxAct);
        Assert.Equal(
            [
                "proto.enemy.flail_knight",
                "proto.enemy.spectral_knight",
                "proto.enemy.magi_knight"
            ],
            encounter.EnemyIds);
        Assert.True(encounter.Weight > 0);
    }

    [Fact]
    public void FirstFiveA0TurnsComposeAllThreeNativePatterns()
    {
        var upgradedDefend = new CombatCardInstance(
            1,
            1001,
            "proto.silent.defend",
            1,
            false,
            PrototypeJson.EmptyObject());

        var player = new PlayerState(
            500,
            500,
            0,
            [
                new CardInstance(
                    1001,
                    upgradedDefend.CardId,
                    1,
                    PrototypeJson.EmptyObject())
            ],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [upgradedDefend.InstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                Enemy(
                    1,
                    "proto.enemy.flail_knight",
                    101),
                Enemy(
                    2,
                    "proto.enemy.spectral_knight",
                    93),
                Enemy(
                    3,
                    "proto.enemy.magi_knight",
                    82)
            ],
            NextCardInstanceId: 2,
            Cards: [upgradedDefend],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "knight-gang-test",
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
                null));

        var engine = new PrototypeGameEngine();

        // Turn 1:
        // Flail Ram 15, Spectral Hex, Magi Power Shield 6 + 5 Block.
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(479, state.Player.Hp);
        Assert.Equal(
            "ram",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 1).LastMoveId);
        Assert.Equal(
            "hex",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 2).LastMoveId);
        Assert.Equal(
            "power_shield",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);
        Assert.Equal(
            5,
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).Block);

        var hex = Assert.Single(
            combat.PlayerPowers,
            power => power.PowerId
                == "proto.power.hex");
        Assert.Equal(2, hex.SourceEnemyInstanceId);

        var card = combat.Cards.Single(
            item => item.InstanceId
                == upgradedDefend.InstanceId);
        Assert.Equal(
            PrototypeCardAfflictionKind.Hexed,
            card.Affliction!.Kind);
        Assert.Equal(
            2,
            card.Affliction.SourceEnemyInstanceId);

        // Turn 2:
        // Seeded Flail roll = Flail 9x2, Soul Slash 15,
        // then Magi applies Dampen.
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(446, state.Player.Hp);
        Assert.Equal(
            "flail",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 1).LastMoveId);
        Assert.Contains(
            combat.PlayerPowers,
            power => power.PowerId
                == "proto.power.dampen"
                && power.SourceEnemyInstanceId == 3);

        card = combat.Cards.Single(
            item => item.InstanceId
                == upgradedDefend.InstanceId);
        Assert.Equal(0, card.UpgradeLevel);
        Assert.Equal(
            1,
            card.SuppressedUpgradeLevels);
        Assert.Contains(
            upgradedDefend.InstanceId,
            combat.ExhaustPile);

        // Turn 3:
        // Flail again 9x2, Soul Flame 3x3, Magi Ram 10.
        state = EndTurn(engine, state);
        Assert.Equal(409, state.Player.Hp);

        // Turn 4:
        // Flail War Chant (+3 Strength), Soul Slash 15, Magi Prep +5 Block.
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(394, state.Player.Hp);
        var flailKnight = combat.Enemies.Single(
            enemy => enemy.InstanceId == 1);
        Assert.Equal(
            "war_chant",
            flailKnight.LastMoveId);
        Assert.Equal(
            3,
            Assert.Single(
                flailKnight.PowerStates,
                power => power.PowerId
                    == "proto.power.strength")
                .Stacks);
        Assert.Equal(
            5,
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).Block);

        // Turn 5:
        // Strength-boosted Flail Ram 18, Soul Flame 3x3,
        // Magi Magic Bomb 35.
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(332, state.Player.Hp);
        Assert.Equal(
            "ram",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 1).LastMoveId);
        Assert.Equal(
            "soul_flame",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 2).LastMoveId);
        Assert.Equal(
            "magic_bomb",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);

        Assert.All(
            combat.Enemies,
            enemy => Assert.Equal(5, enemy.MoveIndex));
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
