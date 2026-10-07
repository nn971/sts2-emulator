using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeVantomTests
{
    [Fact]
    public void DefinitionMatchesPinnedV01110Data()
    {
        var enemy = PrototypeContent.Enemy(
            "proto.enemy.vantom");

        Assert.Equal("Vantom", enemy.Name);
        Assert.Equal((173, 173), enemy.HpRangeAt(1, 0));
        Assert.Equal((183, 183), enemy.HpRangeAt(1, 8));

        var slippery = Assert.Single(enemy.StartingPowers!);
        Assert.Equal("proto.power.slippery", slippery.PowerId);
        Assert.Equal(8, slippery.StacksAt(0));
        Assert.Equal(9, slippery.StacksAt(8));

        Assert.Collection(
            enemy.Moves,
            blot =>
            {
                Assert.Equal("ink_blot", blot.Id);
                var damage = Assert.Single(blot.Effects);
                Assert.Equal(7, damage.AmountAt(1, 8));
                Assert.Equal(8, damage.AmountAt(1, 9));
            },
            lance =>
            {
                Assert.Equal("inky_lance", lance.Id);
                var damage = Assert.Single(lance.Effects);
                Assert.Equal(2, damage.Repetitions);
                Assert.Equal(6, damage.AmountAt(1, 8));
                Assert.Equal(7, damage.AmountAt(1, 9));
            },
            dismember =>
            {
                Assert.Equal("dismember", dismember.Id);
                Assert.Collection(
                    dismember.Effects,
                    damage =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.DamagePlayer,
                            damage.Kind);
                        Assert.Equal(26, damage.AmountAt(1, 8));
                        Assert.Equal(30, damage.AmountAt(1, 9));
                    },
                    wounds =>
                    {
                        Assert.Equal(
                            PrototypeEnemyEffectKind.AddCardsToDiscard,
                            wounds.Kind);
                        Assert.Equal(3, wounds.Amount);
                        Assert.Equal(
                            "proto.status.wound",
                            wounds.CardId);
                    });
            },
            prepare =>
            {
                Assert.Equal("prepare", prepare.Id);
                var strength = Assert.Single(prepare.Effects);
                Assert.Equal(
                    PrototypeEnemyEffectKind.ApplyEnemyPower,
                    strength.Kind);
                Assert.Equal("proto.power.strength", strength.PowerId);
                Assert.Equal(2, strength.Amount);
            });

        var wound = PrototypeContent.Card(
            "proto.status.wound");
        Assert.True(wound.Unplayable);
        Assert.Equal(PrototypeCardType.Status, wound.Type);
        Assert.False(wound.RewardEligible);
    }

    [Fact]
    public void FixedCycleAddsWoundsAndPrepareStrength()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateState();

        state = EndTurn(engine, state);
        Assert.Equal(93, state.Player.Hp);
        AssertMove(state, "ink_blot");

        state = EndTurn(engine, state);
        Assert.Equal(81, state.Player.Hp);
        AssertMove(state, "inky_lance");

        state = EndTurn(engine, state);
        Assert.Equal(55, state.Player.Hp);
        AssertMove(state, "dismember");
        Assert.Equal(
            3,
            state.World!.Combat!.Cards.Count(
                card => card.CardId == "proto.status.wound"));

        state = EndTurn(engine, state);
        Assert.Equal(55, state.Player.Hp);
        AssertMove(state, "prepare");
        Assert.Equal(
            2,
            StrengthOf(
                Assert.Single(
                    state.World!.Combat!.Enemies)));

        state = EndTurn(engine, state);
        Assert.Equal(46, state.Player.Hp);
        AssertMove(state, "ink_blot");
    }

    [Fact]
    public void StartingSlipperyCapsAndConsumesOneHpLoss()
    {
        var slice = new CombatCardInstance(
            1,
            null,
            "proto.silent.slice",
            0,
            true,
            PrototypeJson.EmptyObject());
        var state = CreateState(cards: [slice]);
        var engine = new PrototypeGameEngine();

        state = PlayCard(
            engine,
            state,
            slice.InstanceId,
            enemyId: 1);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(172, enemy.Hp);
        Assert.Equal(
            7,
            Assert.Single(
                enemy.PowerStates,
                power => power.PowerId
                    == "proto.power.slippery")
                .Stacks);
    }

    [Fact]
    public void BossReferenceFormationIsSingleton()
    {
        var spec = Assert.Single(
            PrototypeContent.Encounter(
                    "proto.encounter.vantom_boss")
                .ResolveEnemySpecs(
                    PrototypeRng.CreateBundle(
                        "vantom-formation")));

        Assert.Equal("proto.enemy.vantom", spec.EnemyId);
        Assert.Equal(0, spec.FormationPosition);
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
                return payload.CardInstanceId
                        == cardInstanceId
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

    private static void AssertMove(
        RunState state,
        string moveId) =>
        Assert.Equal(
            moveId,
            Assert.Single(
                state.World!.Combat!.Enemies)
                .LastMoveId);

    private static int StrengthOf(
        EnemyCombatState enemy) =>
        enemy.PowerStates
            .SingleOrDefault(power =>
                power.PowerId == "proto.power.strength")
            ?.Stacks ?? 0;

    private static RunState CreateState(
        CombatCardInstance[]? cards = null)
    {
        cards ??= [];
        var definition = PrototypeContent.Enemy(
            "proto.enemy.vantom");
        var powers =
            definition.StartingPowers!
                .Select((power, index) =>
                    new PrototypePowerInstanceState(
                        power.PowerId,
                        power.StacksAt(0),
                        index + 1L))
                .ToArray();

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
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    definition.Id,
                    173,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal),
                    Powers: powers)
            ],
            NextCardInstanceId:
                cards.Length == 0
                    ? 1
                    : cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                powers.Length + 1L,
            Act: 1,
            Ascension: 0);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "vantom-test",
            "vantom-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "vantom-test"),
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
