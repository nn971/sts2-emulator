using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBladeOfInkTests
{
    [Fact]
    public void BladeOfInkCreatesTwoInkyShivs()
    {
        var state = CreateState(
            Card(1, "proto.silent.blade_of_ink"));
        var engine = new PrototypeGameEngine();

        state = PlayOnlyCard(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(2, combat.Hand.Length);
        Assert.All(
            combat.Hand,
            id =>
            {
                var card = combat.Cards.Single(
                    item => item.InstanceId == id);
                Assert.Equal("proto.silent.shiv", card.CardId);
                Assert.NotNull(card.Enchantment);
                Assert.Equal(
                    PrototypeCardEnchantmentKind.Inky,
                    card.Enchantment!.Kind);
                Assert.Equal(1, card.Enchantment.Amount);
            });
    }

    [Fact]
    public void UpgradedBladeOfInkCreatesThreeInkyShivs()
    {
        var state = CreateState(
            Card(1, "proto.silent.blade_of_ink", upgrade: 1));
        var engine = new PrototypeGameEngine();

        state = PlayOnlyCard(engine, state);

        Assert.Equal(3, state.World!.Combat!.Hand.Length);
    }

    [Fact]
    public void InkyShivAppliesWeakAfterItsDamageToChosenTarget()
    {
        var state = CreateState(
            Card(1, "proto.silent.blade_of_ink"));
        var engine = new PrototypeGameEngine();
        state = PlayOnlyCard(engine, state);

        var shivId = state.World!.Combat!.Hand[0];
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == shivId
                && item.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, action).State;

        var combat = state.World!.Combat!;
        var first = combat.Enemies.Single(e => e.InstanceId == 1);
        var second = combat.Enemies.Single(e => e.InstanceId == 2);
        Assert.Equal(16, first.Hp);
        Assert.Equal(
            1,
            first.Statuses.GetValueOrDefault("proto.status.weak"));
        Assert.Equal(20, second.Hp);
        Assert.Equal(
            0,
            second.Statuses.GetValueOrDefault("proto.status.weak"));
    }

    [Fact]
    public void InkyUsesFanOfKnivesAllEnemyTargetSet()
    {
        var state = CreateState(
            Card(1, "proto.silent.blade_of_ink"),
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.fan_of_knives",
                    1,
                    1)
            ]);
        var engine = new PrototypeGameEngine();
        state = PlayOnlyCard(engine, state);

        var shivId = state.World!.Combat!.Hand[0];
        var shivAction = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == shivId);
        Assert.Null(
            shivAction.ReadPayload<PlayCardPayload>()
                .TargetEnemyId);

        state = engine.Step(state, shivAction).State;

        Assert.All(
            state.World!.Combat!.Enemies,
            enemy =>
            {
                Assert.Equal(16, enemy.Hp);
                Assert.Equal(
                    1,
                    enemy.Statuses.GetValueOrDefault(
                        "proto.status.weak"));
            });
    }

    private static RunState PlayOnlyCard(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "play_card");
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

    private static RunState CreateState(
        CombatCardInstance blade,
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var persistent = new CardInstance(
            blade.PersistentCardInstanceId!.Value,
            blade.CardId,
            blade.UpgradeLevel,
            PrototypeJson.EmptyObject());
        var player = new PlayerState(
            70,
            70,
            0,
            [persistent],
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 1,
            PlayerBlock: 0,
            Hand: [blade.InstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                Enemy(1),
                Enemy(2)
            ],
            NextCardInstanceId: blade.InstanceId + 1,
            Cards: [blade],
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Length == 0
                    ? 1
                    : powers.Max(p => p.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "blade-of-ink-test",
            "blade-of-ink-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("blade-of-ink-test"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                2000,
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
