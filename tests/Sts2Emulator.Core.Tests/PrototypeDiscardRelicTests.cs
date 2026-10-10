using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeDiscardRelicTests
{
    [Fact]
    public void DiscardReactiveRelicsFireFromChoiceDiscard()
    {
        var state = CombatStateWith(
            relicIds:
            [
                "proto.relic.tough_bandages",
                "proto.relic.tingsha"
            ],
            cards:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.strike")
            ],
            hand: [1, 2],
            enemies:
            [
                Enemy(1, 20),
                Enemy(2, 20)
            ]);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        state = SelectOnly(engine, state, 2);

        var combat = state.World!.Combat!;
        Assert.Equal(11, combat.PlayerBlock);
        Assert.Equal(
            37,
            combat.Enemies.Sum(enemy => enemy.Hp));
        Assert.All(
            combat.RelicStates,
            relic => Assert.Equal(1, relic.TriggerCounts[0]));
        Assert.Equal(
            1,
            combat.CounterState.CardsDiscardedThisTurn);
    }

    [Fact]
    public void HoveringKiteTriggersOnlyOnFirstDiscardEachTurn()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.hovering_kite"],
            cards:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.survivor"),
                Card(4, "proto.silent.strike")
            ],
            hand: [1, 2, 3, 4],
            enemies: [Enemy(1, 200)]);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        state = SelectOnly(engine, state, 2);
        Assert.Equal(3, state.World!.Combat!.Energy);

        state = Play(engine, state, 3);
        state = SelectOnly(engine, state, 4);

        var combat = state.World!.Combat!;
        Assert.Equal(2, combat.Energy);
        Assert.Equal(
            2,
            Assert.Single(combat.RelicStates)
                .TriggerCounts[0]);
        Assert.Equal(
            2,
            combat.CounterState.CardsDiscardedThisTurn);
    }

    [Fact]
    public void NormalEndTurnCleanupDoesNotCountAsDiscard()
    {
        var state = CombatStateWith(
            relicIds: ["proto.relic.tingsha"],
            cards: [Card(1, "proto.silent.defend")],
            hand: [1],
            enemies: [Enemy(1, 200)],
            playerBlock: 999);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Equal(
            0,
            Assert.Single(combat.RelicStates)
                .TriggerCounts[0]);
        Assert.Equal(
            0,
            combat.CounterState.CardsDiscardedThisTurn);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState SelectOnly(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "select_cards"
                && item.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual(
                        [cardInstanceId]));
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long id,
        string cardId) =>
        new(
            id,
            1000 + id,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int id,
        int hp) =>
        new(
            id,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CombatStateWith(
        string[] relicIds,
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState[] enemies,
        int energy = 3,
        int playerBlock = 0)
    {
        var empty = PrototypeJson.EmptyObject();
        var relics = relicIds
            .Select(id => new RelicInstance(id, empty))
            .ToArray();
        var combatRelics = relicIds
            .Select((id, index) =>
            {
                var definition =
                    PrototypeContent.Relic(id);
                return new CombatRelicState(
                    index,
                    id,
                    index + 1L,
                    new int[
                        (definition.Triggers
                            ?? Array.Empty<
                                PrototypeRelicTriggerSpec>())
                        .Length]);
            })
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    empty))
                .ToArray(),
            relics,
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: playerBlock,
            Hand: hand,
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId:
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder:
                combatRelics.Length + 1,
            Relics: combatRelics);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "discard-relic-test",
            "discard-relic-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "discard-relic-test"),
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
