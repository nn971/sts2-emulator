using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBulletTimeTests
{
    [Fact]
    public void BulletTimeMakesCurrentFixedCostHandFreeAndBlocksAdditionalDraws()
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = new[]
        {
            Card(1, 1001, "proto.silent.bullet_time"),
            Card(2, 1002, "proto.silent.backflip"),
            Card(3, 1003, "proto.silent.defend"),
            Card(4, 1004, "proto.silent.skewer"),
            Card(5, 1005, "proto.silent.strike"),
            Card(6, 1006, "proto.silent.strike"),
            Card(7, 1007, "proto.silent.defend"),
            Card(8, 1008, "proto.silent.defend"),
            Card(9, 1009, "proto.silent.defend")
        };

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty)).ToArray(),
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2, 3, 4],
            DrawPile: [5, 6, 7, 8, 9],
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
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 10,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState(player, combat);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        combat = state.World!.Combat!;

        Assert.Equal(0, combat.Energy);
        Assert.Contains(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.no_draw");

        Assert.Equal(
            0,
            combat.Cards.Single(card => card.InstanceId == 2)
                .TemporaryEnergyCost?.Cost);
        Assert.Equal(
            0,
            combat.Cards.Single(card => card.InstanceId == 3)
                .TemporaryEnergyCost?.Cost);
        Assert.Null(
            combat.Cards.Single(card => card.InstanceId == 4)
                .TemporaryEnergyCost);

        var drawPileBeforeBackflip = combat.DrawPile.ToArray();
        state = Play(engine, state, 2);
        combat = state.World!.Combat!;

        Assert.Equal(drawPileBeforeBackflip, combat.DrawPile);
        Assert.Null(
            combat.Cards.Single(card => card.InstanceId == 2)
                .TemporaryEnergyCost);
        Assert.Equal(5, combat.PlayerBlock);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;
        combat = state.World!.Combat!;

        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.no_draw");
        Assert.All(
            combat.Cards,
            card => Assert.Null(card.TemporaryEnergyCost));
        Assert.NotEmpty(combat.Hand);
    }

    [Fact]
    public void UpgradedBulletTimeCostsTwo()
    {
        var card = PrototypeContent.Card("proto.silent.bullet_time");

        Assert.Equal(3, card.Cost.Amount);
        Assert.Equal(-1, card.Cost.UpgradeDelta);
        Assert.Equal(2, card.Cost.AmountAt(upgradeLevel: 1));
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId
                    == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        long persistentId,
        string cardId) =>
        new(
            instanceId,
            persistentId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        PlayerState player,
        CombatState combat)
    {
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "bullet-time-test",
            "bullet-time-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("bullet-time-test"),
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
