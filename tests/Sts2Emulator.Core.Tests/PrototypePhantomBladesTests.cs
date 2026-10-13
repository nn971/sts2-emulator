using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePhantomBladesTests
{
    [Fact]
    public void PhantomBladesDefinitionMatchesPinnedV01110Semantics()
    {
        var card = PrototypeContent.Card(
            "proto.silent.phantom_blades");

        Assert.Equal(1, card.Cost.AmountAt(0));
        Assert.Equal(1, card.Cost.AmountAt(1));
        Assert.Equal(
            PrototypeCardType.Power,
            card.Type);
        Assert.Equal(
            PrototypeCardRarity.Uncommon,
            card.Rarity);

        var apply = Assert.Single(card.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            apply.Kind);
        Assert.Equal(
            "proto.power.phantom_blades",
            apply.PowerId);
        Assert.Equal(9, apply.AmountAt(0, 0));
        Assert.Equal(12, apply.AmountAt(1, 0));

        var power = PrototypeContent.Power(
            "proto.power.phantom_blades");
        Assert.Equal(
            PrototypeCardKeyword.Retain,
            power.GrantedCardKeyword);
        Assert.Equal(
            "Shiv",
            power.GrantedCardKeywordRequiredCardTag);
        Assert.Equal(
            "Shiv",
            power.FirstAttackDamageBonusRequiredCardTag);
        Assert.Equal(
            1,
            power.FirstAttackDamageBonusPerStack);
    }

    [Fact]
    public void OnlyFirstShivEachTurnGetsPhantomBladesDamage()
    {
        var first = Card(
            1,
            "proto.silent.shiv");
        var second = Card(
            2,
            "proto.silent.shiv");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [first, second],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.phantom_blades",
                    9,
                    1)
            ],
            enemyHp: 100);

        state = PlayCard(
            engine,
            state,
            first.InstanceId,
            enemyId: 1);

        var combat = state.World!.Combat!;
        Assert.Equal(
            87,
            Assert.Single(combat.Enemies).Hp);
        Assert.Equal(
            1,
            combat.CounterState.PlaysWithTag("Shiv"));

        state = PlayCard(
            engine,
            state,
            second.InstanceId,
            enemyId: 1);

        combat = state.World!.Combat!;
        Assert.Equal(
            83,
            Assert.Single(combat.Enemies).Hp);
        Assert.Equal(
            2,
            combat.CounterState.PlaysWithTag("Shiv"));
    }

    [Fact]
    public void PhantomBladesRetainsShivsAndBonusResetsNextTurn()
    {
        var first = Card(
            1,
            "proto.silent.shiv");
        var second = Card(
            2,
            "proto.silent.shiv");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [first, second],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.phantom_blades",
                    9,
                    1)
            ],
            enemyHp: 100);

        state = PlayCard(
            engine,
            state,
            first.InstanceId,
            enemyId: 1);
        state = EndTurn(engine, state);

        var combat = state.World!.Combat!;
        Assert.Equal(2, combat.Turn);
        Assert.Contains(
            second.InstanceId,
            combat.Hand);
        Assert.Equal(
            0,
            combat.CounterState.PlaysWithTag("Shiv"));

        state = PlayCard(
            engine,
            state,
            second.InstanceId,
            enemyId: 1);

        combat = state.World!.Combat!;
        Assert.Equal(
            74,
            Assert.Single(combat.Enemies).Hp);
        Assert.Equal(
            1,
            combat.CounterState.PlaysWithTag("Shiv"));
    }

    [Fact]
    public void UpgradedPhantomBladesAppliesTwelveDamagePower()
    {
        var phantom = Card(
            1,
            "proto.silent.phantom_blades",
            upgradeLevel: 1);
        var shiv = Card(
            2,
            "proto.silent.shiv");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [phantom, shiv],
            powers: [],
            enemyHp: 100);

        state = PlayCard(
            engine,
            state,
            phantom.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(
            12,
            Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.phantom_blades")
                .Stacks);

        state = PlayCard(
            engine,
            state,
            shiv.InstanceId,
            enemyId: 1);

        combat = state.World!.Combat!;
        Assert.Equal(
            84,
            Assert.Single(combat.Enemies).Hp);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int? enemyId = null)
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

    private static CombatCardInstance Card(
        long instanceId,
        string cardId,
        int upgradeLevel = 0) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            upgradeLevel,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        CombatCardInstance[] hand,
        PrototypePowerInstanceState[] powers,
        int enemyHp)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            hand.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone())).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand.Select(
                card => card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    enemyHp,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId:
                hand.Max(card => card.InstanceId) + 1,
            Cards: hand,
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Length == 0
                    ? 1
                    : powers.Max(
                        power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "phantom-blades-test",
            "phantom-blades-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "phantom-blades-test"),
            empty,
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
