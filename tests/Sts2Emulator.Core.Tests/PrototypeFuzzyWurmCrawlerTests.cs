using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeFuzzyWurmCrawlerTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.fuzzy_wurm_crawler");

        Assert.Equal("Fuzzy Wurm Crawler", enemy.Name);
        Assert.Equal((55, 57), enemy.HpRangeAt(1, 0));
        Assert.Equal((58, 59), enemy.HpRangeAt(1, 8));

        Assert.Collection(
            enemy.Moves,
            acid =>
            {
                Assert.Equal("acid_goop", acid.Id);
                var damage = Assert.Single(acid.Effects);
                Assert.Equal(4, damage.AmountAt(1, 8));
                Assert.Equal(6, damage.AmountAt(1, 9));
            },
            inhale =>
            {
                Assert.Equal("inhale", inhale.Id);
                var strength = Assert.Single(inhale.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal("proto.power.strength", strength.PowerId);
                Assert.Equal(7, strength.Amount);
            });

        var ai = Assert.IsType<
            PrototypeEnemyAiDefinition>(enemy.Ai);
        Assert.Equal("acid_1", ai.InitialStateId);
        Assert.Collection(
            ai.States,
            acid1 =>
            {
                Assert.Equal("acid_1", acid1.Id);
                Assert.Equal(0, acid1.MoveIndex);
                Assert.Equal("inhale", acid1.NextStateId);
            },
            inhale =>
            {
                Assert.Equal("inhale", inhale.Id);
                Assert.Equal(1, inhale.MoveIndex);
                Assert.Equal("acid_2", inhale.NextStateId);
            },
            acid2 =>
            {
                Assert.Equal("acid_2", acid2.Id);
                Assert.Equal(0, acid2.MoveIndex);
                Assert.Equal("acid_1", acid2.NextStateId);
            });
    }

    [Fact]
    public void NativeThreeTurnCycleAccumulatesStrength()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(0);

        state = EndTurn(engine, state);
        Assert.Equal(96, state.Player.Hp);
        AssertEnemy(state, "acid_goop", strength: 0);

        state = EndTurn(engine, state);
        Assert.Equal(96, state.Player.Hp);
        AssertEnemy(state, "inhale", strength: 7);

        state = EndTurn(engine, state);
        Assert.Equal(85, state.Player.Hp);
        AssertEnemy(state, "acid_goop", strength: 7);

        state = EndTurn(engine, state);
        Assert.Equal(74, state.Player.Hp);
        AssertEnemy(state, "acid_goop", strength: 7);
    }

    [Fact]
    public void A9AcidGoopUsesScaledBaseDamage()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(9);

        state = EndTurn(engine, state);
        Assert.Equal(94, state.Player.Hp);

        state = EndTurn(engine, state);
        state = EndTurn(engine, state);
        Assert.Equal(81, state.Player.Hp);
    }

    [Fact]
    public void ReferenceWeakEncounterIsSingleton()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.fuzzy_wurm_crawler_weak");
        var spec = Assert.Single(
            encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle("fuzzy-formation")));

        Assert.Equal(
            "proto.enemy.fuzzy_wurm_crawler",
            spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
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
                .SingleOrDefault(power =>
                    power.PowerId == "proto.power.strength")
                ?.Stacks ?? 0);
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

        var enemy = PrototypeContent.Enemy(
            "proto.enemy.fuzzy_wurm_crawler");

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
                    enemy.Id,
                    enemy.HpRangeAt(1, ascension).Max,
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
            "fuzzy-wurm-test",
            "fuzzy-wurm-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("fuzzy-wurm-test"),
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
