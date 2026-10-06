using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeToolsOfTradeTests
{
    [Fact]
    public void PowerCardLeavesCombatAfterApplyingItsPower()
    {
        var state = CreateState(
            hand:
            [
                Card(1, "proto.silent.tools_of_the_trade")
            ],
            drawPile: []);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var combat = state.World!.Combat!;

        Assert.Empty(combat.Hand);
        Assert.Empty(combat.DiscardPile);
        Assert.Empty(combat.ExhaustPile);
        Assert.DoesNotContain(1, combat.DrawPile);

        var power = Assert.Single(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.tools_of_the_trade");
        Assert.Equal(1, power.Stacks);
    }

    [Fact]
    public void ToolsDrawsExtraCardThenSuspendsForDiscardAtTurnStart()
    {
        var state = CreateState(
            hand:
            [
                Card(1, "proto.silent.tools_of_the_trade")
            ],
            drawPile:
            [
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.defend"),
                Card(4, "proto.silent.strike"),
                Card(5, "proto.silent.defend"),
                Card(6, "proto.silent.strike"),
                Card(7, "proto.silent.defend")
            ]);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);
        state = engine.Step(state, GameAction.Empty("end_turn")).State;

        var combat = state.World!.Combat!;
        var pending = Assert.IsType<PendingCombatChoiceState>(
            combat.PendingChoice);

        Assert.Equal(2, combat.Turn);
        Assert.Equal(6, combat.Hand.Length);
        Assert.Equal(1, pending.Selection.MinSelections);
        Assert.Equal(1, pending.Selection.MaxSelections);
        Assert.Equal(
            PrototypeCardZone.Hand,
            pending.Selection.SourceZone);
        Assert.Equal(
            PrototypeCardSelectionResolutionKind.MoveToDiscard,
            pending.Selection.Resolution);

        var choices = engine.GetLegalActions(state);
        Assert.Equal(6, choices.Count);
        Assert.All(
            choices,
            action =>
            {
                Assert.Equal("select_cards", action.Kind);
                Assert.Single(
                    action.ReadPayload<SelectCardsPayload>()
                        .CardInstanceIds);
            });

        var chosenId = choices[0]
            .ReadPayload<SelectCardsPayload>()
            .CardInstanceIds[0];
        state = engine.Step(state, choices[0]).State;

        combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Equal(5, combat.Hand.Length);
        Assert.Contains(chosenId, combat.DiscardPile);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
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
        CombatCardInstance[] drawPile)
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = hand.Concat(drawPile).ToArray();

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
            1,
            3,
            0,
            hand.Select(card => card.InstanceId).ToArray(),
            drawPile.Select(card => card.InstanceId).ToArray(),
            [],
            [],
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            cards.Max(card => card.InstanceId) + 1,
            cards,
            [],
            1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "tools-test",
            "tools-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("tools-test"),
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
