using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUniversalKeywordTests
{
    [Fact]
    public void UnplayableCardIsNotOfferedAsACombatAction()
    {
        var state = CreateCombatState(
            [
                Card(1, "proto.status.dazed"),
                Card(2, "proto.silent.strike")
            ]);

        var engine = new PrototypeGameEngine();
        var playableIds = engine.GetLegalActions(state)
            .Where(action => action.Kind == "play_card")
            .Select(action => action.ReadPayload<PlayCardPayload>().CardInstanceId)
            .Distinct()
            .ToArray();

        Assert.DoesNotContain(1, playableIds);
        Assert.Contains(2, playableIds);
    }

    [Fact]
    public void EtherealExhaustsBeforeNormalFlushWhileRetainStaysInHand()
    {
        var state = CreateCombatState(
            [
                Card(1, "proto.status.dazed"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.snakebite")
            ],
            drawPile:
            [
                Card(4, "proto.silent.defend"),
                Card(5, "proto.silent.defend"),
                Card(6, "proto.silent.defend"),
                Card(7, "proto.silent.defend"),
                Card(8, "proto.silent.defend")
            ]);

        var engine = new PrototypeGameEngine();
        state = engine.Step(state, GameAction.Empty("end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(1, combat.ExhaustPile);
        Assert.DoesNotContain(1, combat.DiscardPile);
        Assert.Contains(2, combat.DiscardPile);
        Assert.Contains(3, combat.Hand);
    }

    [Fact]
    public void EternalCardIsNotOfferedForShopRemoval()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            999,
            [
                new CardInstance(1, "proto.curse.ascenders_bane", 0, empty),
                new CardInstance(2, "proto.silent.strike", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "eternal-shop",
            "eternal-shop",
            0,
            RunPhase.Shop,
            player,
            PrototypeRng.CreateBundle("eternal-shop"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                100,
                PrototypeRoomType.Shop,
                new MapState(Array.Empty<MapNodeState>()),
                null,
                null,
                new ShopState(
                    Array.Empty<ShopOffer>(),
                    null,
                    null,
                    RemovalPrice: 75),
                null,
                null));

        var removals = new PrototypeGameEngine()
            .GetLegalActions(state)
            .Where(action => action.Kind == "remove_card")
            .Select(action => action.ReadPayload<RemoveCardPayload>().CardInstanceId)
            .ToArray();

        Assert.DoesNotContain(1, removals);
        Assert.Contains(2, removals);
    }

    [Fact]
    public void NativeKeywordFixturesAreExcludedFromNormalRewardPool()
    {
        Assert.DoesNotContain("proto.status.dazed", PrototypeContent.RewardCardPool);
        Assert.DoesNotContain("proto.curse.ascenders_bane", PrototypeContent.RewardCardPool);
        Assert.DoesNotContain("proto.silent.shiv", PrototypeContent.RewardCardPool);

        Assert.Equal(
            PrototypeCardRarity.Token,
            PrototypeContent.Card("proto.silent.shiv").Rarity);
        Assert.Equal(
            PrototypeCardRarity.Status,
            PrototypeContent.Card("proto.status.dazed").Rarity);
        Assert.Equal(
            PrototypeCardRarity.Curse,
            PrototypeContent.Card("proto.curse.ascenders_bane").Rarity);
    }

    private static CombatCardInstance Card(long id, string cardId) =>
        new(
            id,
            1000 + id,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateCombatState(
        CombatCardInstance[] hand,
        CombatCardInstance[]? drawPile = null)
    {
        drawPile ??= [];
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
            "keyword-test",
            "keyword-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("keyword-test"),
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
