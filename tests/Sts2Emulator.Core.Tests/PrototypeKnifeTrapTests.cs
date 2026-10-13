using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeKnifeTrapTests
{
    [Fact]
    public void KnifeTrapAutoPlaysEveryExhaustedShivAtTarget()
    {
        var knife = Card(1, "proto.silent.knife_trap");
        var firstShiv = Card(2, "proto.silent.shiv");
        var secondShiv = Card(3, "proto.silent.shiv");
        var defend = Card(4, "proto.silent.defend");
        var state = CreateState(
            [knife, firstShiv, secondShiv, defend],
            hand: [1],
            exhaust: [2, 3, 4]);
        var engine = new PrototypeGameEngine();

        state = PlayKnifeTrap(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(12, combat.Enemies.Single(e => e.InstanceId == 1).Hp);
        Assert.Contains(2L, combat.ExhaustPile);
        Assert.Contains(3L, combat.ExhaustPile);
        Assert.Contains(4L, combat.ExhaustPile);
        Assert.Contains(1L, combat.DiscardPile);
        Assert.Equal(3, combat.CounterState.CardsPlayedThisTurn);
        Assert.Equal(2, combat.CounterState.AttacksPlayedThisTurn);
        Assert.Equal(1, combat.CounterState.SkillsPlayedThisTurn);
    }

    [Fact]
    public void UpgradedKnifeTrapUpgradesShivsBeforeAutoPlay()
    {
        var knife = Card(1, "proto.silent.knife_trap", upgrade: 1);
        var shiv = Card(2, "proto.silent.shiv");
        var state = CreateState(
            [knife, shiv],
            hand: [1],
            exhaust: [2]);
        var engine = new PrototypeGameEngine();

        state = PlayKnifeTrap(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(14, combat.Enemies.Single(e => e.InstanceId == 1).Hp);
        Assert.Equal(
            1,
            combat.Cards.Single(c => c.InstanceId == 2).UpgradeLevel);
        Assert.Contains(2L, combat.ExhaustPile);
    }

    [Fact]
    public void KnifeTrapPreservesAndExecutesInkyOnExhaustedShiv()
    {
        var knife = Card(1, "proto.silent.knife_trap");
        var shiv = Card(
            2,
            "proto.silent.shiv",
            enchantment: new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Inky));
        var state = CreateState(
            [knife, shiv],
            hand: [1],
            exhaust: [2]);
        var engine = new PrototypeGameEngine();

        state = PlayKnifeTrap(engine, state, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        var target = combat.Enemies.Single(e => e.InstanceId == 1);
        Assert.Equal(16, target.Hp);
        Assert.Equal(
            1,
            target.Statuses.GetValueOrDefault("proto.status.weak"));
        Assert.Equal(
            PrototypeCardEnchantmentKind.Inky,
            combat.Cards.Single(c => c.InstanceId == 2)
                .Enchantment!.Kind);
        Assert.Contains(2L, combat.ExhaustPile);
    }

    [Fact]
    public void KnifeTrapAutoPlayedShivUsesFanOfKnivesTargetPromotion()
    {
        var knife = Card(1, "proto.silent.knife_trap");
        var shiv = Card(2, "proto.silent.shiv");
        var state = CreateState(
            [knife, shiv],
            hand: [1],
            exhaust: [2],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.fan_of_knives",
                    1,
                    1)
            ]);
        var engine = new PrototypeGameEngine();

        state = PlayKnifeTrap(engine, state, targetEnemyId: 1);

        Assert.All(
            state.World!.Combat!.Enemies,
            enemy => Assert.Equal(16, enemy.Hp));
    }

    private static RunState PlayKnifeTrap(
        PrototypeGameEngine engine,
        RunState state,
        int targetEnemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1
                && item.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == targetEnemyId);
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long id,
        string cardId,
        int upgrade = 0,
        PrototypeCardEnchantment? enchantment = null) =>
        new(
            id,
            1000 + id,
            cardId,
            upgrade,
            false,
            PrototypeJson.EmptyObject(),
            Enchantment: enchantment);

    private static RunState CreateState(
        CombatCardInstance[] cards,
        long[] hand,
        long[] exhaust,
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var persistent = cards
            .Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    PrototypeJson.EmptyObject(),
                    card.Enchantment))
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
            Energy: 2,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: exhaust,
            Enemies:
            [
                Enemy(1),
                Enemy(2)
            ],
            NextCardInstanceId: cards.Max(c => c.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Length == 0
                    ? 1
                    : powers.Max(p => p.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "knife-trap-test",
            "knife-trap-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("knife-trap-test"),
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

    private static EnemyCombatState Enemy(int id) =>
        new(
            id,
            "proto.enemy.crawler",
            20,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));
}
