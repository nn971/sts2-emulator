using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePotionInventoryTests
{
    [Fact]
    public void FullPotionBeltCanReplaceEitherSlotFromReward()
    {
        var engine = new PrototypeGameEngine();
        var state = RewardState(
            relicIds: [],
            potionSlots:
            [
                Potion("proto.potion.block"),
                Potion("proto.potion.fire")
            ],
            rewardPotionId: "proto.potion.strength");

        var legal = engine.GetLegalActions(state);
        Assert.DoesNotContain(
            legal,
            action => action.Kind == "take_reward_potion");

        var replace = legal
            .Where(action =>
                action.Kind == "replace_reward_potion")
            .ToArray();
        Assert.Equal(2, replace.Length);
        Assert.Equal(
            new[] { 0, 1 },
            replace
                .Select(action =>
                    action.ReadPayload<
                        ReplaceRewardPotionPayload>()
                        .Slot)
                .Order()
                .ToArray());
        Assert.Contains(
            legal,
            action => action.Kind == "skip_reward_potion");

        var frame =
            new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            2,
            frame.LegalActions.Count(action =>
                action.Kind == "replace_reward_potion"));

        state = engine.Step(
            state,
            replace.Single(action =>
                action.ReadPayload<
                    ReplaceRewardPotionPayload>()
                    .Slot == 1)).State;

        Assert.Equal(
            "proto.potion.block",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal(
            "proto.potion.strength",
            state.Player.PotionSlots[1]!.PotionId);
        Assert.True(
            state.World!.Reward!.PotionResolved);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FullPotionBeltCanReplaceEitherSlotWhenBuyingPotion()
    {
        var engine = new PrototypeGameEngine();
        var state = ShopState(
            gold: 100,
            relicIds: [],
            potionSlots:
            [
                Potion("proto.potion.block"),
                Potion("proto.potion.fire")
            ],
            shopPotionId: "proto.potion.strength",
            price: 40);

        var legal = engine.GetLegalActions(state);
        Assert.DoesNotContain(
            legal,
            action => action.Kind == "buy_potion");

        var replace = legal
            .Where(action =>
                action.Kind == "replace_shop_potion")
            .ToArray();
        Assert.Equal(2, replace.Length);
        Assert.All(
            replace,
            action => Assert.Equal(
                100,
                action.ReadPayload<
                    ReplaceShopPotionPayload>()
                    .OfferId));

        state = engine.Step(
            state,
            replace.Single(action =>
                action.ReadPayload<
                    ReplaceShopPotionPayload>()
                    .Slot == 0)).State;

        Assert.Equal(60, state.Player.Gold);
        Assert.Equal(
            "proto.potion.strength",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal(
            "proto.potion.fire",
            state.Player.PotionSlots[1]!.PotionId);
        Assert.True(
            state.World!.Shop!.PotionOffer!.Sold);
        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "replace_shop_potion");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EmptyPotionSlotUsesSimpleAcquisitionAction()
    {
        var engine = new PrototypeGameEngine();
        var reward = RewardState(
            relicIds: [],
            potionSlots:
            [
                Potion("proto.potion.block"),
                null
            ],
            rewardPotionId: "proto.potion.strength");

        var rewardActions =
            engine.GetLegalActions(reward);
        Assert.Contains(
            rewardActions,
            action =>
                action.Kind == "take_reward_potion");
        Assert.DoesNotContain(
            rewardActions,
            action =>
                action.Kind == "replace_reward_potion");

        var shop = ShopState(
            gold: 100,
            relicIds: [],
            potionSlots:
            [
                Potion("proto.potion.block"),
                null
            ],
            shopPotionId: "proto.potion.strength",
            price: 40);
        var shopActions =
            engine.GetLegalActions(shop);
        Assert.Contains(
            shopActions,
            action => action.Kind == "buy_potion");
        Assert.DoesNotContain(
            shopActions,
            action =>
                action.Kind == "replace_shop_potion");
    }

    [Fact]
    public void SozuBlocksPotionReplacementWhenBeltIsFull()
    {
        var engine = new PrototypeGameEngine();
        var relics = new[] { "proto.relic.sozu" };
        var slots = new PotionInstance?[]
        {
            Potion("proto.potion.block"),
            Potion("proto.potion.fire")
        };

        var reward = RewardState(
            relics,
            slots,
            "proto.potion.strength");
        var rewardActions =
            engine.GetLegalActions(reward);
        Assert.DoesNotContain(
            rewardActions,
            action =>
                action.Kind == "take_reward_potion");
        Assert.DoesNotContain(
            rewardActions,
            action =>
                action.Kind == "replace_reward_potion");
        Assert.Contains(
            rewardActions,
            action =>
                action.Kind == "skip_reward_potion");

        var shop = ShopState(
            gold: 100,
            relicIds: relics,
            potionSlots: slots,
            shopPotionId: "proto.potion.strength",
            price: 40);
        var shopActions =
            engine.GetLegalActions(shop);
        Assert.DoesNotContain(
            shopActions,
            action => action.Kind == "buy_potion");
        Assert.DoesNotContain(
            shopActions,
            action =>
                action.Kind == "replace_shop_potion");
    }

    private static PotionInstance Potion(
        string potionId) =>
        new(
            potionId,
            PrototypeJson.EmptyObject());

    private static PlayerState Player(
        int gold,
        string[] relicIds,
        PotionInstance?[] potionSlots) =>
        new(
            Hp: 70,
            MaxHp: 70,
            Gold: gold,
            Deck: [],
            Relics: relicIds
                .Select(id =>
                    new RelicInstance(
                        id,
                        PrototypeJson.EmptyObject()))
                .ToArray(),
            PotionSlots:
                (PotionInstance?[])potionSlots.Clone());

    private static RunState RewardState(
        string[] relicIds,
        PotionInstance?[] potionSlots,
        string rewardPotionId)
    {
        var player =
            Player(0, relicIds, potionSlots);
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "potion-reward",
            "potion-reward",
            0,
            RunPhase.Reward,
            player,
            PrototypeRng.CreateBundle(
                "potion-reward"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                PrototypeRoomType.Combat,
                new MapState([]),
                null,
                new RewardState(
                    "Combat",
                    [],
                    rewardPotionId,
                    null,
                    CardResolved: true,
                    PotionResolved: false,
                    RelicResolved: true,
                    EndsAct: false),
                null,
                null,
                null));
    }

    private static RunState ShopState(
        int gold,
        string[] relicIds,
        PotionInstance?[] potionSlots,
        string shopPotionId,
        int price)
    {
        var player =
            Player(gold, relicIds, potionSlots);
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "potion-shop",
            "potion-shop",
            0,
            RunPhase.Shop,
            player,
            PrototypeRng.CreateBundle(
                "potion-shop"),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                PrototypeRoomType.Shop,
                new MapState([]),
                null,
                null,
                new ShopState(
                    [],
                    new ShopOffer(
                        100,
                        shopPotionId,
                        price,
                        Sold: false,
                        BasePrice: price),
                    null,
                    RemovalUsed: true),
                null,
                null));
    }
}
