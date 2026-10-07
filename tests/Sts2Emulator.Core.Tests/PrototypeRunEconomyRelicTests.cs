using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRunEconomyRelicTests
{
    [Fact]
    public void QuestionCardAndPrayerWheelExpandNormalCombatRewards()
    {
        var engine = new PrototypeGameEngine();
        var state = CombatStateWithRelics(
            [
                "proto.relic.question_card",
                "proto.relic.prayer_wheel"
            ]);

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .TargetEnemyId == 1);
        state = engine.Step(state, strike).State;

        Assert.Equal(RunPhase.Reward, state.Phase);
        var reward = state.World!.Reward!;
        Assert.Equal(4, reward.CardOptions.Length);
        var extra = Assert.Single(
            reward.ExtraCardOptions!);
        Assert.Equal(4, extra.Length);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action =>
                    action.Kind == "skip_reward_card")).State;

        var frame =
            new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            extra,
            frame.Observation.Reward!.CardOptions);
        Assert.Equal(
            1,
            frame.Observation.Reward
                .ExtraCardRewardGroupsRemaining);
        Assert.Equal(
            4,
            frame.LegalActions.Count(action =>
                action.Kind == "take_reward_card"));
    }

    [Fact]
    public void MembershipCardAndSmilingMaskModifyShopPrices()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            999,
            [
                new CardInstance(
                    1,
                    "proto.silent.strike",
                    0,
                    empty)
            ],
            [
                new RelicInstance(
                    "proto.relic.membership_card",
                    empty),
                new RelicInstance(
                    "proto.relic.smiling_mask",
                    empty)
            ],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var state = MapChoiceState(
            player,
            "economy-start-shop");
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("shop"))).State;

        var shop = state.World!.Shop!;
        Assert.All(
            shop.CardOffers,
            offer => Assert.Equal(
                offer.UndiscountedPrice / 2,
                offer.Price));
        Assert.Equal(
            shop.PotionOffer!.UndiscountedPrice / 2,
            shop.PotionOffer.Price);
        Assert.Equal(
            shop.RelicOffer!.UndiscountedPrice / 2,
            shop.RelicOffer.Price);
        Assert.Equal(75, shop.UndiscountedRemovalPrice);
        Assert.Equal(25, shop.RemovalPrice);
    }

    [Fact]
    public void BuyingMembershipCardRepricesRemainingShopImmediately()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            500,
            [
                new CardInstance(
                    1,
                    "proto.silent.strike",
                    0,
                    empty)
            ],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "membership-reprice",
            "membership-reprice",
            0,
            RunPhase.Shop,
            player,
            PrototypeRng.CreateBundle(
                "membership-reprice"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                2,
                PrototypeRoomType.Shop,
                new MapState([]),
                null,
                null,
                new ShopState(
                    [
                        new ShopOffer(
                            1,
                            "proto.silent.defend",
                            100,
                            false,
                            BasePrice: 100)
                    ],
                    new ShopOffer(
                        100,
                        "proto.potion.strength",
                        60,
                        false,
                        BasePrice: 60),
                    new ShopOffer(
                        200,
                        "proto.relic.membership_card",
                        120,
                        false,
                        BasePrice: 120),
                    RemovalPrice: 80,
                    BaseRemovalPrice: 80),
                null,
                null));

        var engine = new PrototypeGameEngine();
        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action =>
                    action.Kind == "buy_relic")).State;

        Assert.Equal(380, state.Player.Gold);
        Assert.Contains(
            state.Player.Relics,
            relic => relic.RelicId
                == "proto.relic.membership_card");

        var shop = state.World!.Shop!;
        Assert.Equal(50, Assert.Single(shop.CardOffers).Price);
        Assert.Equal(30, shop.PotionOffer!.Price);
        Assert.Equal(40, shop.RemovalPrice);
        Assert.True(shop.RelicOffer!.Sold);
    }

    [Fact]
    public void SozuBlocksNewPotionsButAllowsUsingExistingPotion()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            500,
            [],
            [
                new RelicInstance(
                    "proto.relic.sozu",
                    empty)
            ],
            [
                new PotionInstance(
                    "proto.potion.entropic_brew",
                    empty),
                null
            ]);
        var state = MapChoiceState(
            player,
            "sozu-entropic");
        var engine = new PrototypeGameEngine();

        var useBrew = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "use_potion");
        state = engine.Step(
            state,
            useBrew).State;

        Assert.All(
            state.Player.PotionSlots,
            potion => Assert.Null(potion));

        var rewardState = state with
        {
            Phase = RunPhase.Reward,
            World = state.World! with
            {
                ActiveRoom =
                    PrototypeRoomType.Combat,
                Reward = new RewardState(
                    "Combat",
                    [],
                    "proto.potion.strength",
                    null,
                    CardResolved: true,
                    PotionResolved: false,
                    RelicResolved: true,
                    EndsAct: false)
            }
        };
        var rewardActions =
            engine.GetLegalActions(rewardState);
        Assert.DoesNotContain(
            rewardActions,
            action =>
                action.Kind
                    == "take_reward_potion");
        Assert.Contains(
            rewardActions,
            action =>
                action.Kind
                    == "skip_reward_potion");

        var shopState = state with
        {
            Phase = RunPhase.Shop,
            World = state.World! with
            {
                ActiveRoom =
                    PrototypeRoomType.Shop,
                Shop = new ShopState(
                    [],
                    new ShopOffer(
                        100,
                        "proto.potion.strength",
                        1,
                        false,
                        BasePrice: 1),
                    null,
                    RemovalUsed: true)
            }
        };
        Assert.DoesNotContain(
            engine.GetLegalActions(shopState),
            action =>
                action.Kind == "buy_potion");
        Assert.Equal(
            1,
            PrototypeContent.Relic(
                "proto.relic.sozu")
                .EnergyPerTurnBonus);
    }

    private static RunState CombatStateWithRelics(
        string[] relicIds)
    {
        var empty = PrototypeJson.EmptyObject();
        var card = new CombatCardInstance(
            1,
            1001,
            "proto.silent.strike",
            0,
            false,
            empty);
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(
                    1001,
                    card.CardId,
                    0,
                    empty)
            ],
            relicIds
                .Select(id =>
                    new RelicInstance(id, empty))
                .ToArray(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    1,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards: [card],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "run-economy-reward",
            "run-economy-reward",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "run-economy-reward"),
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

    private static RunState MapChoiceState(
        PlayerState player,
        string seed) =>
        new(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.MapChoice,
            player,
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                0,
                Math.Max(
                    1,
                    player.Deck
                        .Select(card =>
                            card.InstanceId)
                        .DefaultIfEmpty(0)
                        .Max() + 1),
                null,
                new MapState(
                    [
                        new MapNodeState(
                            "shop",
                            1,
                            1,
                            PrototypeRoomType.Shop,
                            [])
                    ],
                    EntryNodeIds: ["shop"]),
                null,
                null,
                null,
                null,
                null));
}
