using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSilentBacklogThirdBatchTests
{
    [Fact]
    public void EchoingSlashRepeatsOnceForEachEnemyKilledInEachRound()
    {
        var state = CreateState(
            [
                Card(1, 100, "proto.silent.echoing_slash")
            ],
            hand: [1],
            enemies:
            [
                Enemy(1, 5),
                Enemy(2, 15),
                Enemy(3, 35)
            ],
            energy: 1);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);

        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(0, enemies.Single(e => e.InstanceId == 1).Hp);
        Assert.Equal(0, enemies.Single(e => e.InstanceId == 2).Hp);
        Assert.Equal(5, enemies.Single(e => e.InstanceId == 3).Hp);
    }

    [Fact]
    public void UpgradedEchoingSlashUsesThirteenDamagePerRound()
    {
        var state = CreateState(
            [
                Card(1, 100, "proto.silent.echoing_slash", upgrade: 1)
            ],
            hand: [1],
            enemies:
            [
                Enemy(1, 10),
                Enemy(2, 30)
            ],
            energy: 1);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);

        Assert.Equal(
            4,
            state.World!.Combat!.Enemies
                .Single(e => e.InstanceId == 2).Hp);
    }

    [Fact]
    public void FanOfKnivesCreatesShivsAndMakesThemHitAllEnemies()
    {
        var state = CreateState(
            [
                Card(1, 100, "proto.silent.fan_of_knives")
            ],
            hand: [1],
            enemies:
            [
                Enemy(1, 20),
                Enemy(2, 20)
            ],
            energy: 2);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        Assert.Equal(4, combat.Hand.Length);
        Assert.All(
            combat.Hand,
            id => Assert.Equal(
                "proto.silent.shiv",
                combat.Cards.Single(c => c.InstanceId == id).CardId));
        Assert.Contains(
            combat.PlayerPowers,
            p => p.PowerId == "proto.power.fan_of_knives");

        var shivId = combat.Hand[0];
        var shivActions = engine.GetLegalActions(state)
            .Where(a =>
                a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId == shivId)
            .ToArray();
        var shivAction = Assert.Single(shivActions);
        Assert.Null(shivAction.ReadPayload<PlayCardPayload>().TargetEnemyId);

        state = engine.Step(state, shivAction).State;
        Assert.All(
            state.World!.Combat!.Enemies,
            enemy => Assert.Equal(16, enemy.Hp));
    }

    [Fact]
    public void UpgradedFanOfKnivesCreatesFiveShivs()
    {
        var state = CreateState(
            [
                Card(1, 100, "proto.silent.fan_of_knives", upgrade: 1)
            ],
            hand: [1],
            enemies: [Enemy(1, 20)],
            energy: 2);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);

        Assert.Equal(5, state.World!.Combat!.Hand.Length);
    }

    [Fact]
    public void WellLaidPlansPreservesWholeNonEtherealHand()
    {
        var state = CreateState(
            [
                Card(1, 100, "proto.silent.well_laid_plans"),
                Card(2, 101, "proto.silent.strike"),
                Card(3, 102, "proto.silent.defend"),
                Card(4, 103, "proto.status.dazed")
            ],
            hand: [1, 2, 3, 4],
            enemies: [Enemy(1, 100)],
            energy: 2);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(a => a.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(2L, combat.Hand);
        Assert.Contains(3L, combat.Hand);
        Assert.Contains(4L, combat.ExhaustPile);
        Assert.DoesNotContain(4L, combat.Hand);
    }

    [Fact]
    public void UpgradedWellLaidPlansCostsOne()
    {
        var card = PrototypeContent.Card(
            "proto.silent.well_laid_plans");
        Assert.Equal(2, card.Cost.AmountAt(0));
        Assert.Equal(1, card.Cost.AmountAt(1));
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(a =>
                a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>().CardInstanceId
                    == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long combatId,
        long persistentId,
        string cardId,
        int upgrade = 0) =>
        new(
            combatId,
            persistentId,
            cardId,
            upgrade,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(int id, int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(StringComparer.Ordinal));

    private static RunState CreateState(
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        int energy)
    {
        var persistent = cards
            .Select(card =>
                new CardInstance(
                    card.PersistentCardId ?? card.InstanceId,
                    card.CardId,
                    card.UpgradeLevel,
                    PrototypeJson.EmptyObject()))
            .ToArray();

        var player = new PlayerState(
            70,
            70,
            0,
            persistent,
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: cards.Max(c => c.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "silent-third-batch",
            "silent-third-batch",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("silent-third-batch"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                persistent.Max(c => c.InstanceId) + 1,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
