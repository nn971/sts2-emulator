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

        var flailMoves = new List<string>
        {
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 1).LastMoveId!
        };
        var spectralMoves = new List<string>
        {
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 2).LastMoveId!
        };

        // Turn 2 has two fixed pieces: Spectral's forced Soul Slash
        // and Magi's Dampen. Flail has already entered its random pool.
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        flailMoves.Add(
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 1).LastMoveId!);
        spectralMoves.Add(
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 2).LastMoveId!);
        Assert.Equal("soul_slash", spectralMoves[^1]);
        Assert.Equal(
            "dampen",
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);
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

        // The remaining turns combine two constrained-random enemies
        // with Magi's fixed Ram / Prep / Magic Bomb suffix. Do not pin
        // one prototype-RNG realization as if it were native RNG parity.
        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        flailMoves.Add(EnemyMove(combat, 1));
        spectralMoves.Add(EnemyMove(combat, 2));
        Assert.Equal("ram", EnemyMove(combat, 3));

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        flailMoves.Add(EnemyMove(combat, 1));
        spectralMoves.Add(EnemyMove(combat, 2));
        Assert.Equal("prep", EnemyMove(combat, 3));
        Assert.Equal(
            5,
            combat.Enemies.Single(enemy =>
                enemy.InstanceId == 3).Block);

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        flailMoves.Add(EnemyMove(combat, 1));
        spectralMoves.Add(EnemyMove(combat, 2));
        Assert.Equal("magic_bomb", EnemyMove(combat, 3));

        AssertFlailPolicy(flailMoves);
        AssertSpectralPolicy(spectralMoves);
        Assert.All(
            combat.Enemies,
            enemy => Assert.Equal(5, enemy.MoveIndex));
    }

    private static string EnemyMove(
        CombatState combat,
        int instanceId) =>
        combat.Enemies.Single(enemy =>
            enemy.InstanceId == instanceId)
            .LastMoveId!;

    private static void AssertFlailPolicy(
        IReadOnlyList<string> moves)
    {
        Assert.Equal("ram", moves[0]);
        AssertRepeatLimit(moves, "war_chant", 1);
        AssertRepeatLimit(moves, "flail", 2);
        AssertRepeatLimit(moves, "ram", 2);
    }

    private static void AssertSpectralPolicy(
        IReadOnlyList<string> moves)
    {
        Assert.Equal("hex", moves[0]);
        Assert.Equal("soul_slash", moves[1]);
        AssertRepeatLimit(moves.Skip(1).ToArray(), "soul_slash", 2);
        AssertRepeatLimit(moves.Skip(1).ToArray(), "soul_flame", 1);
    }

    private static void AssertRepeatLimit(
        IEnumerable<string> moves,
        string moveId,
        int maximum)
    {
        var consecutive = 0;
        foreach (var move in moves)
        {
            consecutive = StringComparer.Ordinal.Equals(
                    move,
                    moveId)
                ? consecutive + 1
                : 0;
            Assert.True(
                consecutive <= maximum,
                $"{moveId} repeated {consecutive} times");
        }
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
