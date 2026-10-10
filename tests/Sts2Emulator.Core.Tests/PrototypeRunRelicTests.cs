using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRunRelicTests
{
    [Fact]
    public void OldCoinTriggersOnlyWhenAcquired()
    {
        var state = RewardStateForRelic(
            "proto.relic.old_coin",
            hp: 40,
            maxHp: 70,
            gold: 25);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_relic")).State;

        Assert.Equal(325, state.Player.Gold);
        Assert.Contains(
            state.Player.Relics,
            relic => relic.RelicId == "proto.relic.old_coin");
    }

    [Fact]
    public void MangoAndLeesWaffleApplyAcquisitionHealthEffects()
    {
        var engine = new PrototypeGameEngine();

        var mango = RewardStateForRelic(
            "proto.relic.mango",
            hp: 40,
            maxHp: 70);
        mango = engine.Step(
            mango,
            Assert.Single(
                engine.GetLegalActions(mango),
                action => action.Kind == "take_reward_relic")).State;
        Assert.Equal(54, mango.Player.Hp);
        Assert.Equal(84, mango.Player.MaxHp);

        var waffle = RewardStateForRelic(
            "proto.relic.lees_waffle",
            hp: 10,
            maxHp: 70);
        waffle = engine.Step(
            waffle,
            Assert.Single(
                engine.GetLegalActions(waffle),
                action => action.Kind == "take_reward_relic")).State;
        Assert.Equal(77, waffle.Player.MaxHp);
        Assert.Equal(77, waffle.Player.Hp);
    }

    [Fact]
    public void MealTicketHealsWhenEnteringShop()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            20,
            70,
            200,
            [],
            [
                new RelicInstance(
                    "proto.relic.meal_ticket",
                    empty)
            ],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "meal-ticket-test",
            "meal-ticket-test",
            0,
            RunPhase.MapChoice,
            player,
            PrototypeRng.CreateBundle("meal-ticket-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                null,
                new MapState(
                    [
                        new MapNodeState(
                            "shop",
                            1,
                            2,
                            PrototypeRoomType.Shop,
                            [])
                    ],
                    EntryNodeIds: ["shop"]),
                null,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload("shop"))).State;

        Assert.Equal(RunPhase.Shop, state.Phase);
        Assert.Equal(35, state.Player.Hp);
    }

    [Fact]
    public void LuckyFyshRewardsEveryCardAddedToDeck()
    {
        var state = RewardStateForCard(
            "proto.silent.defend",
            relicId: "proto.relic.lucky_fysh");
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action =>
                    action.Kind == "take_reward_card")).State;

        Assert.Equal(15, state.Player.Gold);
        Assert.Contains(
            state.Player.Deck,
            card => card.CardId == "proto.silent.defend");
    }

    [Fact]
    public void DarkstonePeriaptRaisesMaxHpOnlyForCurseAdded()
    {
        var engine = new PrototypeGameEngine();

        var curse = RewardStateForCard(
            "proto.curse.ascenders_bane",
            relicId: "proto.relic.darkstone_periapt");
        curse = engine.Step(
            curse,
            engine.GetLegalActions(curse)
                .Single(action =>
                    action.Kind == "take_reward_card")).State;
        Assert.Equal(76, curse.Player.MaxHp);
        Assert.Equal(76, curse.Player.Hp);

        var skill = RewardStateForCard(
            "proto.silent.defend",
            relicId: "proto.relic.darkstone_periapt");
        skill = engine.Step(
            skill,
            engine.GetLegalActions(skill)
                .Single(action =>
                    action.Kind == "take_reward_card")).State;
        Assert.Equal(70, skill.Player.MaxHp);
    }

    private static RunState RewardStateForRelic(
        string relicId,
        int hp,
        int maxHp,
        int gold = 0)
    {
        var state = BaseRewardState(hp, maxHp, gold);
        return state with
        {
            World = state.World! with
            {
                Reward = new RewardState(
                    "Combat",
                    [],
                    null,
                    relicId,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: false,
                    EndsAct: false)
            }
        };
    }

    private static RunState RewardStateForCard(
        string cardId,
        string relicId)
    {
        var state = BaseRewardState(70, 70, 0);
        var empty = PrototypeJson.EmptyObject();
        return state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance(
                        relicId,
                        empty)
                ]
            },
            World = state.World! with
            {
                Reward = new RewardState(
                    "Combat",
                    [cardId],
                    null,
                    null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false)
            }
        };
    }

    private static RunState BaseRewardState(
        int hp,
        int maxHp,
        int gold)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            hp,
            maxHp,
            gold,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "run-relic-test",
            "run-relic-test",
            0,
            RunPhase.Reward,
            player,
            PrototypeRng.CreateBundle("run-relic-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                1,
                PrototypeRoomType.Combat,
                new MapState([]),
                null,
                null,
                null,
                null,
                null));
    }
}
