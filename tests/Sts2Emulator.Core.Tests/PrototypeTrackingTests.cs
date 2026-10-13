using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTrackingTests
{
    [Fact]
    public void TrackingDefinitionMatchesPinnedV01110Semantics()
    {
        var card = PrototypeContent.Card("proto.silent.tracking");

        Assert.Equal(2, card.Cost.AmountAt(0));
        Assert.Equal(1, card.Cost.AmountAt(1));
        Assert.Equal(PrototypeCardType.Power, card.Type);
        Assert.Equal(PrototypeCardRarity.Rare, card.Rarity);

        var effect = Assert.Single(card.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.ApplyPlayerPower,
            effect.Kind);
        Assert.Equal("proto.power.tracking", effect.PowerId);

        var power = PrototypeContent.Power("proto.power.tracking");
        Assert.Equal(
            "proto.status.weak",
            power.AttackDamageBonusRequiredTargetStatus);
        Assert.Equal(1, power.AttackDamageBonusTargetNumeratorPerStack);
        Assert.Equal(2, power.AttackDamageBonusTargetDenominator);
    }

    [Fact]
    public void TrackingAddsHalfAttackDamageAgainstWeakTarget()
    {
        var strike = Card(1, "proto.silent.strike");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [strike],
            enemyStatuses: new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["proto.status.weak"] = 2
            },
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.tracking",
                    1,
                    1)
            ]);

        state = PlayCard(engine, state, strike.InstanceId, enemyId: 1);

        Assert.Equal(91, state.World!.Combat!.Enemies.Single().Hp);
    }

    [Fact]
    public void TrackingDoesNotModifyAttackAgainstNonWeakTarget()
    {
        var strike = Card(1, "proto.silent.strike");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [strike],
            enemyStatuses: new Dictionary<string, int>(
                StringComparer.Ordinal),
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.tracking",
                    1,
                    1)
            ]);

        state = PlayCard(engine, state, strike.InstanceId, enemyId: 1);

        Assert.Equal(94, state.World!.Combat!.Enemies.Single().Hp);
    }

    [Fact]
    public void TrackingStacksAdditively()
    {
        var strike = Card(1, "proto.silent.strike");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [strike],
            enemyStatuses: new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["proto.status.weak"] = 1
            },
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.tracking",
                    2,
                    1)
            ]);

        state = PlayCard(engine, state, strike.InstanceId, enemyId: 1);

        Assert.Equal(88, state.World!.Combat!.Enemies.Single().Hp);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int enemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId
                && item.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == enemyId);
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        CombatCardInstance[] hand,
        Dictionary<string, int> enemyStatuses,
        PrototypePowerInstanceState[] powers)
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
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand.Select(card => card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    enemyStatuses)
            ],
            NextCardInstanceId: hand.Max(card => card.InstanceId) + 1,
            Cards: hand,
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Max(power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "tracking-test",
            "tracking-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("tracking-test"),
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
