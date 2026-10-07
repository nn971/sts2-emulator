using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeShrinkerBeetleTests
{
    [Fact]
    public void DefinitionMatchesPinnedActOneData()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.shrinker_beetle");

        Assert.Equal("Shrinker Beetle", enemy.Name);
        Assert.Equal((38, 40), enemy.HpRangeAt(1, 0));
        Assert.Equal((40, 42), enemy.HpRangeAt(1, 8));
        Assert.Equal(1, enemy.MoveLoopStartIndex);

        Assert.Collection(
            enemy.Moves,
            shrinker =>
            {
                Assert.Equal("shrinker", shrinker.Id);
                var debuff = Assert.Single(shrinker.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyPlayerPower,
                    debuff.Kind);
                Assert.Equal("proto.power.shrink", debuff.PowerId);
            },
            chomp =>
            {
                Assert.Equal("chomp", chomp.Id);
                var damage = Assert.Single(chomp.Effects);
                Assert.Equal(7, damage.AmountAt(1, 8));
                Assert.Equal(8, damage.AmountAt(1, 9));
            },
            stomp =>
            {
                Assert.Equal("stomp", stomp.Id);
                var damage = Assert.Single(stomp.Effects);
                Assert.Equal(13, damage.AmountAt(1, 8));
                Assert.Equal(14, damage.AmountAt(1, 9));
            });

        var shrink = PrototypeContent.Power(
            "proto.power.shrink");
        Assert.True(shrink.IsDebuff);
        Assert.True(shrink.SourceBoundToEnemy);
        Assert.True(shrink.DoesNotStack);
        Assert.Equal(7, shrink.PlayerAttackDamageNumerator);
        Assert.Equal(10, shrink.PlayerAttackDamageDenominator);
    }

    [Fact]
    public void ShrinkerOpensThenAlternatesChompAndStomp()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(100, state.Player.Hp);
        Assert.Equal(
            "shrinker",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);
        Assert.Equal(
            "proto.power.shrink",
            Assert.Single(
                state.World.Combat.PlayerPowers).PowerId);

        state = EndTurn(engine, state);
        Assert.Equal(93, state.Player.Hp);
        Assert.Equal(
            "chomp",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);

        state = EndTurn(engine, state);
        Assert.Equal(80, state.Player.Hp);
        Assert.Equal(
            "stomp",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);

        state = EndTurn(engine, state);
        Assert.Equal(73, state.Player.Hp);
        Assert.Equal(
            "chomp",
            Assert.Single(
                state.World!.Combat!.Enemies).LastMoveId);
    }

    [Fact]
    public void ShrinkReducesAttackDamageAndClearsWhenSourceDies()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            beetleHp: 4,
            withStrike: true);

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        Assert.Single(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.shrink");

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        state = engine.Step(state, strike).State;

        combat = state.World!.Combat!;
        Assert.Equal(
            0,
            Assert.Single(combat.Enemies).Hp);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.shrink");
    }

    [Fact]
    public void ReferenceFormationsCoverWeakAndCrawlerPair()
    {
        var rng = PrototypeRng.CreateBundle(
            "shrinker-formations");

        var weak = PrototypeContent.Encounter(
            "proto.encounter.shrinker_beetle_weak")
            .ResolveEnemySpecs(rng);
        var weakEnemy = Assert.Single(weak);
        Assert.Equal(
            "proto.enemy.shrinker_beetle",
            weakEnemy.EnemyId);

        var pair = PrototypeContent.Encounter(
            "proto.encounter.overgrowth_crawlers")
            .ResolveEnemySpecs(rng);
        Assert.Collection(
            pair,
            crawler =>
            {
                Assert.Equal(
                    "proto.enemy.fuzzy_wurm_crawler",
                    crawler.EnemyId);
                Assert.Equal(0, crawler.FormationPosition);
            },
            beetle =>
            {
                Assert.Equal(
                    "proto.enemy.shrinker_beetle",
                    beetle.EnemyId);
                Assert.Equal(1, beetle.FormationPosition);
            });
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
        int beetleHp = 40,
        bool withStrike = false)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        CombatCardInstance[] cards = withStrike
            ? [
                new CombatCardInstance(
                    1,
                    null,
                    "proto.silent.strike",
                    0,
                    true,
                    PrototypeJson.EmptyObject())
              ]
            : [];

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: withStrike ? [1] : [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.shrinker_beetle",
                    beetleHp,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: withStrike ? 2 : 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "shrinker-beetle-test",
            "shrinker-beetle-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "shrinker-beetle-test"),
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
                null));
    }
}
