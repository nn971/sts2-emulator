using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePredicateTests
{
    [Fact]
    public void GrandFinaleIsLegalOnlyWithAnEmptyDrawPile()
    {
        var engine = new PrototypeGameEngine();

        var blocked = CreateGrandFinaleState(drawPileEmpty: false);
        Assert.DoesNotContain(
            engine.GetLegalActions(blocked),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        var legal = CreateGrandFinaleState(drawPileEmpty: true);
        var play = Assert.Single(
            engine.GetLegalActions(legal),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        legal = engine.Step(legal, play).State;
        Assert.All(
            legal.World!.Combat!.Enemies,
            enemy => Assert.Equal(40, enemy.Hp));
    }

    [Fact]
    public void BubbleBubbleAppliesPoisonOnlyToAlreadyPoisonedTarget()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(200, "proto.silent.bubble_bubble", 0, empty),
                new CardInstance(201, "proto.silent.bubble_bubble", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    50,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(
                    2,
                    "proto.enemy.crawler",
                    50,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["proto.status.poison"] = 2
                    })
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 200, "proto.silent.bubble_bubble", 0, false, empty),
                new CombatCardInstance(2, 201, "proto.silent.bubble_bubble", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState("bubble-predicate-test", player, combat, 202);
        var engine = new PrototypeGameEngine();

        var first = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId == 1);
        state = engine.Step(state, first).State;

        var firstEnemy = state.World!.Combat!.Enemies
            .Single(enemy => enemy.InstanceId == 1);
        Assert.False(firstEnemy.Statuses.ContainsKey("proto.status.poison"));

        var second = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId == 2);
        state = engine.Step(state, second).State;

        var secondEnemy = state.World!.Combat!.Enemies
            .Single(enemy => enemy.InstanceId == 2);
        Assert.Equal(11, secondEnemy.Statuses["proto.status.poison"]);
    }

    private static RunState CreateGrandFinaleState(bool drawPileEmpty)
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = new List<CombatCardInstance>
        {
            new(1, 300, "proto.silent.grand_finale", 0, false, empty)
        };

        var drawPile = Array.Empty<long>();
        var persistentCards = new List<CardInstance>
        {
            new(300, "proto.silent.grand_finale", 0, empty)
        };

        if (!drawPileEmpty)
        {
            cards.Add(new CombatCardInstance(
                2,
                301,
                "proto.silent.defend",
                0,
                false,
                empty));
            persistentCards.Add(new CardInstance(
                301,
                "proto.silent.defend",
                0,
                empty));
            drawPile = [2];
        }

        var player = new PlayerState(
            70,
            70,
            0,
            persistentCards.ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: drawPile,
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
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(
                    2,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: cards.Count + 1,
            Cards: cards.ToArray(),
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return CreateState(
            drawPileEmpty ? "grand-finale-empty" : "grand-finale-blocked",
            player,
            combat,
            302);
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
