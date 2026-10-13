using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// The pinned RewardsSet builds rewards before population, but rolls
/// potion pity during construction. Existing rewards are then populated
/// before Prayer Wheel's TryModifyRewards appends another CardReward.
/// </summary>
public sealed class PrototypeNativeRewardPopulationOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegularCombatRollsPotionOddsThenGoldPotionCardsAndPrayerWheel(
        bool prayerWheel)
    {
        var engine = new PrototypeGameEngine();
        var seed = "native-reward-order-" + prayerWheel;
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var neowId = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "GoldenPearl");
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    OfferedChoiceIds =
                    [
                        "take_" + neowId,
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "DowsingRod")
                    ]
                }
            }
        };
        var neow = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + neowId);
        state = engine.Step(state, neow).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);

        if (prayerWheel)
        {
            state = state with
            {
                Player = state.Player with
                {
                    Relics = state.Player.Relics.Append(
                        new RelicInstance("proto.relic.prayer_wheel",
                            PrototypeJson.EmptyObject())).ToArray()
                }
            };
        }

        var combat = state.World!.Combat!;
        var neutralize = combat.Cards.Single(card =>
            card.CardId == "proto.silent.neutralize");
        combat = combat with
        {
            Hand = [neutralize.InstanceId],
            DrawPile = combat.Cards.Select(card => card.InstanceId)
                .Where(id => id != neutralize.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        var expectedRng = state.Rng.Fork();
        var originalOdds = state.World.PotionRewardOddsThousandths;
        var originalRarity = state.World.CardRarityOffsetBasisPoints;

        // Replay the native order using a fork of exactly the same
        // reward RNG stream at the combat-to-reward transition.
        var (potionOffered, nextPotionOdds) =
            PrototypeNativePotionRewardOdds.Roll(
                originalOdds, elite: false, expectedRng);
        var gold = PrototypeNativeCombatGoldReward.Roll(
            PrototypeRoomType.Combat, 0, expectedRng);
        var potion = potionOffered
            ? PrototypeNativePotionShop.PickWeighted(
                expectedRng, "reward")
            : null;
        var first = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
            3, 0, PrototypeRoomType.Combat,
            originalRarity, expectedRng);
        var finalRarity = first.NextOffsetBasisPoints;
        string[]? extraCards = null;
        if (prayerWheel)
        {
            var extra = PrototypeNativeCardRarityOdds.GenerateEncounterCards(
                3, 0, PrototypeRoomType.Combat,
                finalRarity, expectedRng);
            extraCards = extra.Cards;
            finalRarity = extra.NextOffsetBasisPoints;
        }

        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId);
        var result = engine.Step(state, play).State;
        Assert.Equal(RunPhase.Reward, result.Phase);
        var reward = result.World!.Reward!;
        Assert.Equal(gold, reward.GoldOption);
        Assert.Equal(potion, reward.PotionOption);
        Assert.Equal(first.Cards, reward.CardOptions);
        Assert.Equal(prayerWheel ? 1 : 0,
            reward.ExtraCardOptions?.Length ?? 0);
        if (prayerWheel)
        {
            Assert.Equal(extraCards, reward.ExtraCardOptions![0]);
        }
        Assert.Equal(finalRarity,
            result.World.CardRarityOffsetBasisPoints);
        Assert.Equal(nextPotionOdds,
            result.World.PotionRewardOddsThousandths);
        Assert.Equal(expectedRng.Streams.Single(stream =>
                stream.StreamId == "reward").CallCount,
            result.Rng.Streams.Single(stream =>
                stream.StreamId == "reward").CallCount);
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void PrayerWheelDoesNotIncreaseEliteRewardGroupCount()
    {
        Assert.Equal(1, PrototypeContent.Relic(
            "proto.relic.prayer_wheel")
                .ExtraNormalCombatCardRewardGroups);
    }
}
