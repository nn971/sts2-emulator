using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeExhaustRelicTests
{
    [Fact]
    public void CharonsAshesDamagesAllEnemiesAfterExhaust()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.shiv")
            ],
            hand: [1],
            enemies:
            [
                Enemy(1, 20),
                Enemy(2, 20)
            ],
            relicId: "proto.relic.charons_ashes");
        var engine = new PrototypeGameEngine();

        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);

        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(
            13,
            enemies.Single(e => e.InstanceId == 1).Hp);
        Assert.Equal(
            17,
            enemies.Single(e => e.InstanceId == 2).Hp);
    }

    [Fact]
    public void JossPaperDrawsOnFifthExhaust()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.shiv"),
                Card(2, "proto.silent.defend")
            ],
            hand: [1],
            draw: [2],
            enemies: [Enemy(1, 50)],
            relicId: "proto.relic.joss_paper",
            initialTriggerCount: 4);
        var engine = new PrototypeGameEngine();

        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Contains(2L, combat.Hand);
        Assert.Equal(
            5,
            Assert.Single(combat.RelicStates)
                .TriggerCounts[0]);
    }

    [Fact]
    public void BurningSticksCopiesFirstExhaustedSkillWithState()
    {
        var state = CreateState(
            cards:
            [
                Card(
                    1,
                    "proto.silent.blade_dance",
                    upgrade: 1)
            ],
            hand: [1],
            enemies: [Enemy(1, 50)],
            relicId: "proto.relic.burning_sticks");
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        var copied = Assert.Single(
            combat.Cards,
            card =>
                card.CardId
                    == "proto.silent.blade_dance"
                && card.InstanceId != 1);
        Assert.True(copied.IsTemporary);
        Assert.Null(copied.PersistentCardInstanceId);
        Assert.Equal(1, copied.UpgradeLevel);
        Assert.Contains(copied.InstanceId, combat.Hand);
        Assert.Equal(
            1,
            Assert.Single(combat.RelicStates)
                .TriggerCounts[0]);
    }

    [Fact]
    public void ForgottenSoulDealsOneRandomDamageAfterExhaust()
    {
        var state = CreateState(
            cards:
            [
                Card(1, "proto.silent.shiv")
            ],
            hand: [1],
            enemies:
            [
                Enemy(1, 20),
                Enemy(2, 20)
            ],
            relicId: "proto.relic.forgotten_soul");
        var engine = new PrototypeGameEngine();

        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);

        Assert.Equal(
            35,
            state.World!.Combat!.Enemies.Sum(enemy => enemy.Hp));
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int? targetEnemyId = null)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
            {
                if (item.Kind != "play_card")
                {
                    return false;
                }

                var payload =
                    item.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId
                        == cardInstanceId
                    && payload.TargetEnemyId
                        == targetEnemyId;
            });
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long id,
        string cardId,
        int upgrade = 0) =>
        new(
            id,
            1000 + id,
            cardId,
            upgrade,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int id,
        int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CreateState(
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        string relicId,
        long[]? draw = null,
        int initialTriggerCount = 0)
    {
        draw ??= [];
        var definition =
            PrototypeContent.Relic(relicId);
        var triggerCount =
            (definition.Triggers
             ?? Array.Empty<
                 PrototypeRelicTriggerSpec>())
            .Length;
        var triggerCounts =
            new int[triggerCount];
        if (triggerCount > 0)
        {
            triggerCounts[0] =
                initialTriggerCount;
        }

        var persistent = cards
            .Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    PrototypeJson.EmptyObject()))
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            persistent,
            [
                new RelicInstance(
                    relicId,
                    PrototypeJson.EmptyObject())
            ],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile: draw,
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Max(card =>
                    card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 2,
            Relics:
            [
                new CombatRelicState(
                    0,
                    relicId,
                    1,
                    triggerCounts)
            ]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "exhaust-relic-test",
            "exhaust-relic-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "exhaust-relic-test"),
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
