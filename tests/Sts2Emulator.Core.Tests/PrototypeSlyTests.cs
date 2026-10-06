using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSlyTests
{
    [Fact]
    public void SurvivorDiscardingTacticianAutoPlaysItForFreeBeforeSurvivorCompletes()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.tactician"),
                Card(3, "proto.silent.strike")
            ],
            drawPile: []);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        Assert.NotNull(state.World!.Combat!.PendingChoice);
        Assert.Equal(2, state.World.Combat.Energy);

        var selectTactician = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));

        state = engine.Step(state, selectTactician).State;
        var combat = state.World!.Combat!;

        Assert.Null(combat.PendingChoice);
        Assert.Equal(3, combat.Energy);
        Assert.Equal(new long[] { 3 }, combat.Hand);
        Assert.Equal(new long[] { 2, 1 }, combat.DiscardPile);
    }

    [Fact]
    public void SurvivorDiscardingReflexAutoPlaysItAfterDiscardAndDrawsCards()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.reflex"),
                Card(3, "proto.silent.strike")
            ],
            drawPile:
            [
                Card(4, "proto.silent.defend"),
                Card(5, "proto.silent.defend")
            ]);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var selectReflex = engine.GetLegalActions(state)
            .Single(action =>
                action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));
        state = engine.Step(state, selectReflex).State;

        var combat = state.World!.Combat!;
        Assert.Equal(new long[] { 3, 4, 5 }, combat.Hand);
        Assert.Equal(new long[] { 2, 1 }, combat.DiscardPile);
        Assert.Empty(combat.DrawPile);
        Assert.Equal(2, combat.Energy);
    }

    [Fact]
    public void HiddenDaggersDiscardingSlyCardsAutoPlaysAllBeforeGeneratingShivs()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.hidden_daggers"),
                Card(2, "proto.silent.tactician"),
                Card(3, "proto.silent.untouchable")
            ],
            drawPile: []);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var selectBoth = Assert.Single(engine.GetLegalActions(state));
        Assert.Equal(new long[] { 2, 3 }, selectBoth.ReadPayload<SelectCardsPayload>().CardInstanceIds);

        state = engine.Step(state, selectBoth).State;
        var combat = state.World!.Combat!;

        Assert.Equal(4, combat.Energy);
        Assert.Equal(6, combat.PlayerBlock);
        Assert.Equal(new long[] { 2, 3, 1 }, combat.DiscardPile);

        var shivs = combat.Hand
            .Select(id => combat.Cards.Single(card => card.InstanceId == id))
            .ToArray();
        Assert.Equal(2, shivs.Length);
        Assert.All(shivs, card => Assert.Equal("proto.silent.shiv", card.CardId));
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == cardInstanceId);
        return engine.Step(state, play).State;
    }

    private static CombatCardInstance Card(long instanceId, string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        int energy,
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
            energy,
            0,
            hand.Select(card => card.InstanceId).ToArray(),
            drawPile.Select(card => card.InstanceId).ToArray(),
            Array.Empty<long>(),
            Array.Empty<long>(),
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            cards.Max(card => card.InstanceId) + 1,
            cards,
            Array.Empty<PrototypePowerInstanceState>(),
            1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "sly-test",
            "sly-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("sly-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));
    }
}
