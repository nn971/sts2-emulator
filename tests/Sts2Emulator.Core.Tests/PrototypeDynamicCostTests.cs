using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeDynamicCostTests
{
    [Fact]
    public void PinpointCostFallsWithCompletedSkillsThisTurn()
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = new[]
        {
            Card(1, 1001, "proto.silent.pinpoint"),
            Card(2, 1002, "proto.silent.defend"),
            Card(3, 1003, "proto.silent.deflect")
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
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 2,
            PlayerBlock: 0,
            Hand: [1, 2, 3],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    40,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 4,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "pinpoint-cost-test",
            "pinpoint-cost-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("pinpoint-cost-test"),
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

        var engine = new PrototypeGameEngine();

        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        state = PlayCard(engine, state, 2);
        Assert.Equal(1, state.World!.Combat!.CounterState.SkillsPlayedThisTurn);
        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        state = PlayCard(engine, state, 3);
        Assert.Equal(2, state.World!.Combat!.CounterState.SkillsPlayedThisTurn);

        var pinpoint = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, pinpoint).State;

        Assert.Equal(0, state.World!.Combat!.Energy);
        Assert.Equal(25, Assert.Single(state.World.Combat.Enemies).Hp);
    }

    [Fact]
    public void PinpointDynamicCostNeverFallsBelowZero()
    {
        var cost = PrototypeContent.Card("proto.silent.pinpoint").Cost;

        Assert.Equal(
            3,
            cost.AmountAt(
                upgradeLevel: 0,
                reductionCount: 0));
        Assert.Equal(
            1,
            cost.AmountAt(
                upgradeLevel: 0,
                reductionCount: 2));
        Assert.Equal(
            0,
            cost.AmountAt(
                upgradeLevel: 0,
                reductionCount: 99));
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>().CardInstanceId
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
}
