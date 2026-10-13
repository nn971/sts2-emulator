using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeFogmogTests
{
    [Fact]
    public void DefinitionsMatchPinnedOvergrowthData()
    {
        var fogmog = PrototypeContent.Enemy(
            "proto.enemy.fogmog");
        Assert.Equal((74, 74), fogmog.HpRangeAt(1, 0));
        Assert.Equal((78, 78), fogmog.HpRangeAt(1, 8));

        Assert.Collection(
            fogmog.Moves,
            summon =>
            {
                Assert.Equal("illusory_spores", summon.Id);
                var effect = Assert.Single(summon.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.SummonEnemy,
                    effect.Kind);
                Assert.Equal(
                    "proto.enemy.eye_with_teeth",
                    effect.EnemyId);
            },
            thwack =>
            {
                Assert.Equal("thwack", thwack.Id);
                Assert.Collection(
                    thwack.Effects,
                    damage =>
                    {
                        Assert.Equal(8, damage.AmountAt(1, 8));
                        Assert.Equal(9, damage.AmountAt(1, 9));
                    },
                    strength =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.ApplyEnemyPower,
                            strength.Kind);
                        Assert.Equal(
                            "proto.power.strength",
                            strength.PowerId);
                    });
            },
            headbutt =>
            {
                Assert.Equal("headbutt", headbutt.Id);
                var damage = Assert.Single(headbutt.Effects);
                Assert.Equal(14, damage.AmountAt(1, 8));
                Assert.Equal(16, damage.AmountAt(1, 9));
            });

        var eye = PrototypeContent.Enemy(
            "proto.enemy.eye_with_teeth");
        Assert.Equal((6, 6), eye.HpRangeAt(1, 10));
        Assert.True(eye.IsMinion);
        Assert.True(eye.RevivesOnEnemyTurn);
        var distract = Assert.Single(eye.Moves);
        var dazed = Assert.Single(distract.Effects);
        Assert.Equal(
            PrototypeEnemyEffectKind.AddCardsToDiscard,
            dazed.Kind);
        Assert.Equal("proto.status.dazed", dazed.CardId);
        Assert.Equal(3, dazed.Amount);
    }

    [Fact]
    public void OpeningSummonDoesNotDistractUntilFollowingEnemyTurn()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateFogmogState();

        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(2, combat.Enemies.Length);
        var eye = combat.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.eye_with_teeth");
        Assert.Equal(6, eye.Hp);
        Assert.Equal(1, eye.LeaderEnemyInstanceId);
        Assert.DoesNotContain(
            combat.Cards,
            card => card.CardId == "proto.status.dazed");

        state = EndTurn(engine, state);

        combat = state.World!.Combat!;
        Assert.Equal(
            3,
            combat.Cards.Count(card =>
                card.CardId == "proto.status.dazed"));
        var fogmog = combat.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.fogmog");
        Assert.Equal("thwack", fogmog.LastMoveId);
        Assert.Equal(
            1,
            Assert.Single(
                fogmog.PowerStates,
                power => power.PowerId
                    == "proto.power.strength")
                .Stacks);
    }

    [Fact]
    public void KillingEyeSkipsOneDistractThenItRevives()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateFogmogState(
            withStrike: true);

        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        var eye = combat.Enemies.Single(enemy =>
            enemy.EnemyId == "proto.enemy.eye_with_teeth");

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == eye.InstanceId);
        state = engine.Step(state, strike).State;
        Assert.Equal(
            0,
            state.World!.Combat!.Enemies
                .Single(enemy =>
                    enemy.EnemyId == "proto.enemy.eye_with_teeth")
                .Hp);

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(
            6,
            combat.Enemies
                .Single(enemy =>
                    enemy.EnemyId == "proto.enemy.eye_with_teeth")
                .Hp);
        Assert.DoesNotContain(
            combat.Cards,
            card => card.CardId == "proto.status.dazed");

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(
            3,
            combat.Cards.Count(card =>
                card.CardId == "proto.status.dazed"));
    }

    [Fact]
    public void FogmogReferenceEncounterStartsWithoutEye()
    {
        var specs = PrototypeContent.Encounter(
                "proto.encounter.fogmog_normal")
            .ResolveEnemySpecs(
                PrototypeRng.CreateBundle("fogmog-formation"));

        var fogmog = Assert.Single(specs);
        Assert.Equal("proto.enemy.fogmog", fogmog.EnemyId);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState CreateFogmogState(
        bool withStrike = false)
    {
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
            DrawPile: withStrike ? [1] : [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.fogmog",
                    74,
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
            "fogmog-test",
            "fogmog-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("fogmog-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                4,
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
