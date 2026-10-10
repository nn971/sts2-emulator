using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBeforeHandDrawTests
{
    [Fact]
    public void InfiniteBladesCreatesShivBeforeHandDrawAndConsumesHandCapacity()
    {
        var empty = PrototypeJson.EmptyObject();

        var handCards = Enumerable.Range(1, 8)
            .Select(index => new CombatCardInstance(
                index,
                1000 + index,
                "proto.silent.snakebite",
                0,
                false,
                empty))
            .ToArray();

        var drawCards = Enumerable.Range(9, 5)
            .Select(index => new CombatCardInstance(
                index,
                1000 + index,
                "proto.silent.defend",
                0,
                false,
                empty))
            .ToArray();

        var allCards = handCards.Concat(drawCards).ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            allCards.Select(card => new CardInstance(
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
            Hand: handCards.Select(card => card.InstanceId).ToArray(),
            DrawPile: drawCards.Select(card => card.InstanceId).ToArray(),
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
            NextCardInstanceId: 14,
            Cards: allCards,
            PlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.infinite_blades",
                    1,
                    1)
            ],
            NextPowerApplicationOrder: 2);

        var state = CreateState(player, combat);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        combat = state.World!.Combat!;
        Assert.Equal(10, combat.Hand.Length);
        Assert.Equal(4, combat.DrawPile.Length);

        var handModels = combat.Hand
            .Select(instanceId => combat.Cards
                .Single(card => card.InstanceId == instanceId))
            .ToArray();
        Assert.Single(
            handModels,
            card => card.CardId == "proto.silent.shiv");
        Assert.Equal(
            8,
            handModels.Count(
                card => card.CardId == "proto.silent.snakebite"));
        Assert.Equal(
            1,
            handModels.Count(
                card => card.CardId == "proto.silent.defend"));
    }

    private static RunState CreateState(
        PlayerState player,
        CombatState combat)
    {
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "before-hand-draw-test",
            "before-hand-draw-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("before-hand-draw-test"),
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
