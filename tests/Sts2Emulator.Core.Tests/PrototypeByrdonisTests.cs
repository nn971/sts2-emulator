using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeByrdonisTests
{
    [Fact]
    public void DefinitionMatchesPinnedOvergrowthData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.byrdonis");

        Assert.Equal("Byrdonis", enemy.Name);
        Assert.Equal((81, 84), enemy.HpRangeAt(1, 0));
        Assert.Equal((90, 90), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            swoop =>
            {
                Assert.Equal("swoop", swoop.Id);
                var damage = Assert.Single(swoop.Effects);
                Assert.Equal(17, damage.AmountAt(1, 8));
                Assert.Equal(19, damage.AmountAt(1, 9));
            },
            peck =>
            {
                Assert.Equal("peck", peck.Id);
                var damage = Assert.Single(peck.Effects);
                Assert.Equal(3, damage.Repetitions);
                Assert.Equal(3, damage.AmountAt(1, 8));
                Assert.Equal(4, damage.AmountAt(1, 9));
            });

        var territorial = Assert.Single(
            enemy.StartingPowers!);
        Assert.Equal(
            "proto.power.territorial",
            territorial.PowerId);
        Assert.Equal(1, territorial.Stacks);
        Assert.Equal(
            1,
            PrototypeContent.Power(
                    "proto.power.territorial")
                .EnemyStrengthGainAtTurnEndPerStack);
    }

    [Fact]
    public void SwoopPeckAlternationGainsStrengthAfterEveryTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(83, state.Player.Hp);
        AssertEnemy(state, "swoop", strength: 1);

        state = EndTurn(engine, state);
        Assert.Equal(71, state.Player.Hp);
        AssertEnemy(state, "peck", strength: 2);

        state = EndTurn(engine, state);
        Assert.Equal(52, state.Player.Hp);
        AssertEnemy(state, "swoop", strength: 3);
    }

    [Fact]
    public void A9ScalesBaseAttacksBeforeTerritorialStrength()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(ascension: 9);

        state = EndTurn(engine, state);
        Assert.Equal(81, state.Player.Hp);

        state = EndTurn(engine, state);
        Assert.Equal(66, state.Player.Hp);
    }

    [Fact]
    public void ReferenceEliteFormationIsSingleton()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.byrdonis_elite")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    "byrdonis-formation"));

        var spec = Assert.Single(specs);
        Assert.Equal("proto.enemy.byrdonis", spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
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
            enemy.PowerStates
                .Single(power =>
                    power.PowerId == "proto.power.strength")
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
                    "proto.enemy.byrdonis",
                    ascension >= 8 ? 90 : 84,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    Powers:
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.territorial",
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
            "byrdonis-test",
            "byrdonis-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "byrdonis-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                6,
                5000,
                PrototypeRoomType.Elite,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null),
            Ascension: ascension);
    }
}
