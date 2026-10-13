using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTheKinTests
{
    [Fact]
    public void DefinitionsMatchPinnedV01110Data()
    {
        var follower = PrototypeContent.Enemy(
            "proto.enemy.kin_follower");
        Assert.Equal((58, 59), follower.HpRangeAt(1, 0));
        Assert.Equal((62, 63), follower.HpRangeAt(1, 8));
        Assert.True(follower.IsMinion);

        Assert.Collection(
            follower.Moves,
            slash =>
            {
                Assert.Equal("quick_slash", slash.Id);
                Assert.Equal(5, Assert.Single(slash.Effects).AmountAt(1, 9));
            },
            boomerang =>
            {
                Assert.Equal("boomerang", boomerang.Id);
                var damage = Assert.Single(boomerang.Effects);
                Assert.Equal(2, damage.AmountAt(1, 9));
                Assert.Equal(2, damage.Repetitions);
            },
            dance =>
            {
                Assert.Equal("power_dance", dance.Id);
                var strength = Assert.Single(dance.Effects);
                Assert.Equal(2, strength.AmountAt(1, 8));
                Assert.Equal(3, strength.AmountAt(1, 9));
            });

        var priest = PrototypeContent.Enemy(
            "proto.enemy.kin_priest");
        Assert.Equal((190, 190), priest.HpRangeAt(1, 0));
        Assert.Equal((199, 199), priest.HpRangeAt(1, 8));

        Assert.Collection(
            priest.Moves,
            frailty =>
            {
                Assert.Equal("orb_of_frailty", frailty.Id);
                Assert.Equal(8, frailty.Effects[0].AmountAt(1, 8));
                Assert.Equal(9, frailty.Effects[0].AmountAt(1, 9));
                Assert.Equal("proto.power.frail", frailty.Effects[1].PowerId);
            },
            weakness =>
            {
                Assert.Equal("orb_of_weakness", weakness.Id);
                Assert.Equal(8, weakness.Effects[0].AmountAt(1, 8));
                Assert.Equal(9, weakness.Effects[0].AmountAt(1, 9));
                Assert.Equal("proto.power.weak", weakness.Effects[1].PowerId);
            },
            beam =>
            {
                Assert.Equal("soul_beam", beam.Id);
                var damage = Assert.Single(beam.Effects);
                Assert.Equal(3, damage.Amount);
                Assert.Equal(3, damage.Repetitions);
            },
            ritual =>
            {
                Assert.Equal("dark_ritual", ritual.Id);
                var strength = Assert.Single(ritual.Effects);
                Assert.Equal(2, strength.AmountAt(1, 8));
                Assert.Equal(3, strength.AmountAt(1, 9));
            });
    }

    [Fact]
    public void FormationCarriesFollowerOffsetsAndPriestLeadership()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.the_kin_boss")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle("kin-formation"));

        Assert.Collection(
            specs,
            left =>
            {
                Assert.Equal("proto.enemy.kin_follower", left.EnemyId);
                Assert.Equal(0, left.FormationPosition);
                Assert.Equal("quick", left.SlotName);
                Assert.Equal(1, left.LeaderFormationPosition);
            },
            priest =>
            {
                Assert.Equal("proto.enemy.kin_priest", priest.EnemyId);
                Assert.Equal(1, priest.FormationPosition);
                Assert.Null(priest.LeaderFormationPosition);
            },
            right =>
            {
                Assert.Equal("proto.enemy.kin_follower", right.EnemyId);
                Assert.Equal(2, right.FormationPosition);
                Assert.Equal("dance", right.SlotName);
                Assert.Equal(1, right.LeaderFormationPosition);
            });
    }

    [Fact]
    public void FollowersUseOffsetCyclesAroundPriest()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateKinState();

        state = EndTurn(engine, state);
        Assert.Equal(87, state.Player.Hp);
        AssertMoves(
            state,
            "quick_slash",
            "orb_of_frailty",
            "power_dance");

        state = EndTurn(engine, state);
        Assert.Equal(68, state.Player.Hp);
        AssertMoves(
            state,
            "boomerang",
            "orb_of_weakness",
            "quick_slash");

        state = EndTurn(engine, state);
        Assert.Equal(51, state.Player.Hp);
        AssertMoves(
            state,
            "power_dance",
            "soul_beam",
            "boomerang");
    }

    [Fact]
    public void KillingPriestRemovesBothFollowers()
    {
        var strike = new CombatCardInstance(
            1,
            null,
            "proto.silent.strike",
            0,
            true,
            PrototypeJson.EmptyObject());
        var state = CreateKinState(
            priestHp: 6,
            cards: [strike],
            includeWitnessEnemy: true);
        var engine = new PrototypeGameEngine();

        state = PlayCard(
            engine,
            state,
            strike.InstanceId,
            enemyId: 2);

        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(
            0,
            enemies.Single(enemy =>
                enemy.InstanceId == 2).Hp);
        Assert.Equal(
            0,
            enemies.Single(enemy =>
                enemy.InstanceId == 1).Hp);
        Assert.Equal(
            0,
            enemies.Single(enemy =>
                enemy.InstanceId == 3).Hp);
        Assert.True(
            enemies.Single(enemy =>
                enemy.InstanceId == 4).Hp > 0);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int enemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(candidate =>
            {
                if (candidate.Kind != "play_card")
                {
                    return false;
                }

                var payload =
                    candidate.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == cardInstanceId
                    && payload.TargetEnemyId == enemyId;
            });

        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static void AssertMoves(
        RunState state,
        string left,
        string priest,
        string right)
    {
        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(
            left,
            enemies.Single(enemy =>
                enemy.InstanceId == 1).LastMoveId);
        Assert.Equal(
            priest,
            enemies.Single(enemy =>
                enemy.InstanceId == 2).LastMoveId);
        Assert.Equal(
            right,
            enemies.Single(enemy =>
                enemy.InstanceId == 3).LastMoveId);
    }

    private static RunState CreateKinState(
        int priestHp = 190,
        CombatCardInstance[]? cards = null,
        bool includeWitnessEnemy = false)
    {
        cards ??= [];

        var minionPower = new PrototypePowerInstanceState(
            "proto.power.minion",
            1,
            1);

        var enemies = new List<EnemyCombatState>
        {
            new(
                1,
                "proto.enemy.kin_follower",
                59,
                0,
                0,
                new Dictionary<string, int>(
                    StringComparer.Ordinal),
                Powers: [minionPower],
                AiStateId: "init",
                FormationPosition: 0,
                SlotName: "quick",
                LeaderEnemyInstanceId: 2),
            new(
                2,
                "proto.enemy.kin_priest",
                priestHp,
                0,
                0,
                new Dictionary<string, int>(
                    StringComparer.Ordinal),
                FormationPosition: 1),
            new(
                3,
                "proto.enemy.kin_follower",
                59,
                0,
                0,
                new Dictionary<string, int>(
                    StringComparer.Ordinal),
                Powers:
                [
                    new PrototypePowerInstanceState(
                        "proto.power.minion",
                        1,
                        2)
                ],
                AiStateId: "init",
                FormationPosition: 2,
                SlotName: "dance",
                LeaderEnemyInstanceId: 2)
        };

        if (includeWitnessEnemy)
        {
            enemies.Add(
                new EnemyCombatState(
                    4,
                    "proto.enemy.twig_slime_s",
                    11,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    FormationPosition: 3));
        }

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
            Hand: cards
                .Select(card => card.InstanceId)
                .ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies.ToArray(),
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 3,
            Act: 1,
            Ascension: 0);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "the-kin-test",
            "the-kin-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "the-kin-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Boss,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
