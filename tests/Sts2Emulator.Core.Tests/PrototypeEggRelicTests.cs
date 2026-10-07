using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEggRelicTests
{
    [Theory]
    [InlineData(
        "proto.relic.molten_egg",
        "proto.silent.strike",
        PrototypeCardType.Attack)]
    [InlineData(
        "proto.relic.toxic_egg",
        "proto.silent.defend",
        PrototypeCardType.Skill)]
    [InlineData(
        "proto.relic.frozen_egg",
        "proto.silent.footwork",
        PrototypeCardType.Power)]
    public void MatchingEggUpgradesCardAsItEntersDeck(
        string relicId,
        string cardId,
        PrototypeCardType expectedType)
    {
        Assert.Equal(
            expectedType,
            PrototypeContent.Card(cardId).Type);

        var state = RewardStateForCard(
            cardId,
            [relicId]);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_card")).State;

        var added = Assert.Single(state.Player.Deck);
        Assert.Equal(cardId, added.CardId);
        Assert.Equal(1, added.UpgradeLevel);
    }

    [Fact]
    public void EggDoesNotUpgradeDifferentCardType()
    {
        var state = RewardStateForCard(
            "proto.silent.defend",
            ["proto.relic.molten_egg"]);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_card")).State;

        Assert.Equal(
            0,
            Assert.Single(state.Player.Deck).UpgradeLevel);
    }

    [Fact]
    public void EggAndLuckyFyshComposeOnSameCardAddition()
    {
        var state = RewardStateForCard(
            "proto.silent.strike",
            [
                "proto.relic.molten_egg",
                "proto.relic.lucky_fysh"
            ]);
        var engine = new PrototypeGameEngine();

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_card")).State;

        var added = Assert.Single(state.Player.Deck);
        Assert.Equal(1, added.UpgradeLevel);
        Assert.Equal(15, state.Player.Gold);
    }

    private static RunState RewardStateForCard(
        string cardId,
        string[] relicIds)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [],
            relicIds.Select(
                id => new RelicInstance(id, empty))
                .ToArray(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "egg-relic-test",
            "egg-relic-test",
            0,
            RunPhase.Reward,
            player,
            PrototypeRng.CreateBundle("egg-relic-test"),
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
                new RewardState(
                    "Combat",
                    [cardId],
                    null,
                    null,
                    CardResolved: false,
                    PotionResolved: true,
                    RelicResolved: true,
                    EndsAct: false),
                null,
                null,
                null));
    }
}
