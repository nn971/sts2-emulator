using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCombatCounterTests
{
    [Fact]
    public void FinisherCountsOnlyAttacksAlreadyFinishedThisTurn()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(400, "proto.silent.strike", 0, empty),
                new CardInstance(401, "proto.silent.neutralize", 0, empty),
                new CardInstance(402, "proto.silent.finisher", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
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
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 4,
            Cards:
            [
                new CombatCardInstance(1, 400, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(2, 401, "proto.silent.neutralize", 0, false, empty),
                new CombatCardInstance(3, 402, "proto.silent.finisher", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("finisher-counter-test", player, combat, 403);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, 1);
        Assert.Equal(1, state.World!.Combat!.CounterState.AttacksPlayedThisTurn);

        state = Play(engine, state, 2, 1);
        Assert.Equal(2, state.World!.Combat!.CounterState.AttacksPlayedThisTurn);

        state = Play(engine, state, 3, 1);

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(79, enemy.Hp);
        Assert.Equal(3, state.World.Combat.CounterState.AttacksPlayedThisTurn);
    }

    [Fact]
    public void FlechettesCountsSkillsRemainingInHand()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(410, "proto.silent.flechettes", 0, empty),
                new CardInstance(411, "proto.silent.defend", 0, empty),
                new CardInstance(412, "proto.silent.survivor", 0, empty),
                new CardInstance(413, "proto.silent.strike", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2, 3, 4],
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
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 5,
            Cards:
            [
                new CombatCardInstance(1, 410, "proto.silent.flechettes", 0, false, empty),
                new CombatCardInstance(2, 411, "proto.silent.defend", 0, false, empty),
                new CombatCardInstance(3, 412, "proto.silent.survivor", 0, false, empty),
                new CombatCardInstance(4, 413, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("flechettes-count-test", player, combat, 414);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, 1);

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(90, enemy.Hp);
    }

    [Fact]
    public void MementoMoriUsesCardsDiscardedThisTurn()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(420, "proto.silent.survivor", 0, empty),
                new CardInstance(421, "proto.silent.strike", 0, empty),
                new CardInstance(422, "proto.silent.memento_mori", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
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
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 4,
            Cards:
            [
                new CombatCardInstance(1, 420, "proto.silent.survivor", 0, false, empty),
                new CombatCardInstance(2, 421, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(3, 422, "proto.silent.memento_mori", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("memento-counter-test", player, combat, 423);
        var engine = new PrototypeGameEngine();

        var survivor = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, survivor).State;

        var discardStrike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([2]));
        state = engine.Step(state, discardStrike).State;

        Assert.Equal(
            1,
            state.World!.Combat!.CounterState.CardsDiscardedThisTurn);

        state = Play(engine, state, 3, 1);

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(87, enemy.Hp);
    }

    [Fact]
    public void MurderUsesCardsDrawnThisCombatAndUpgradedCost()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(430, "proto.silent.backflip", 0, empty),
                new CardInstance(431, "proto.silent.murder", 1, empty),
                new CardInstance(432, "proto.silent.defend", 0, empty),
                new CardInstance(433, "proto.silent.defend", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: [3, 4],
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
            NextCardInstanceId: 5,
            Cards:
            [
                new CombatCardInstance(1, 430, "proto.silent.backflip", 0, false, empty),
                new CombatCardInstance(2, 431, "proto.silent.murder", 1, false, empty),
                new CombatCardInstance(3, 432, "proto.silent.defend", 0, false, empty),
                new CombatCardInstance(4, 433, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("murder-draw-counter-test", player, combat, 434);
        var engine = new PrototypeGameEngine();

        var backflip = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, backflip).State;

        Assert.Equal(
            2,
            state.World!.Combat!.CounterState.CardsDrawnThisCombat);

        state = Play(engine, state, 2, 1);

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(97, enemy.Hp);
        Assert.Equal(0, state.World.Combat.Energy);
    }

    [Fact]
    public void PreciseCutCountsOtherCardsRemainingInHand()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(440, "proto.silent.precise_cut", 0, empty),
                new CardInstance(441, "proto.silent.defend", 0, empty),
                new CardInstance(442, "proto.silent.survivor", 0, empty),
                new CardInstance(443, "proto.silent.strike", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2, 3, 4],
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
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 5,
            Cards:
            [
                new CombatCardInstance(1, 440, "proto.silent.precise_cut", 0, false, empty),
                new CombatCardInstance(2, 441, "proto.silent.defend", 0, false, empty),
                new CombatCardInstance(3, 442, "proto.silent.survivor", 0, false, empty),
                new CombatCardInstance(4, 443, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("precise-cut-count-test", player, combat, 444);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, 1);

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(93, enemy.Hp);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int enemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
            {
                if (item.Kind != "play_card")
                {
                    return false;
                }

                var payload = item.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == cardInstanceId
                    && payload.TargetEnemyId == enemyId;
            });

        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        string seed,
        PlayerState player,
        CombatState combat,
        long nextPersistentCardId)
    {
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(seed),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                nextPersistentCardId,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
