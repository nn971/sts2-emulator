using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeNeowCustomRewardsTests
{
    private static string OptionId(string name) =>
        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name);

    private static RunState OfferAndChoose(string name, string seed)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var world = state.World!;
        state = state with
        {
            World = world with
            {
                Event = world.Event! with
                {
                    OfferedChoiceIds =
                    [
                        OptionId(name == "ArcaneScroll"
                            ? "BoomingConch" : "ArcaneScroll"),
                        OptionId(name == "GoldenPearl"
                            ? "PreciseScissors" : "GoldenPearl"),
                        OptionId(name)
                    ]
                }
            }
        };
        PrototypeStateInvariants.Validate(state);
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == OptionId(name));
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.NotNull(state.World!.Event);
        Assert.Equal("Event:" + PrototypeNativeOvergrowthEvents.NeowEventId,
            state.World.Reward!.SourceRoom);
        Assert.Empty(state.World.CompletedRooms);
        Assert.Equal(0, state.World.Floor);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState ResolveReward(RunState state,
        bool takeCard = false, bool takePotion = false)
    {
        var engine = new PrototypeGameEngine();
        for (var i = 0; i < 12 && state.Phase == RunPhase.Reward; i++)
        {
            var legal = engine.GetLegalActions(state);
            var action = legal.FirstOrDefault(a => a.Kind ==
                (takeCard ? "take_reward_card" : "skip_reward_card"))
                ?? legal.FirstOrDefault(a => a.Kind ==
                    (takePotion ? "take_reward_potion" : "skip_reward_potion"))
                ?? legal.FirstOrDefault(a => a.Kind == "take_reward_relic")
                ?? legal.FirstOrDefault(a => a.Kind == "choose_relic_deck_card")
                ?? legal.Single(a => a.Kind == "leave_reward");
            state = engine.Step(state, action).State;
            PrototypeStateInvariants.Validate(state);
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Null(state.World.Reward);
        Assert.Null(state.World.Map.CurrentNodeId);
        Assert.Empty(state.World.CompletedRooms);
        Assert.Equal(0, state.World.Floor);
        return state;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeftyTabletOffersThreeRareChoicesThenAddsInjury(bool takeRare)
    {
        var state = OfferAndChoose(
            "HeftyTablet", "neow-hefty-rare-" + takeRare);
        var options = state.World!.Reward!.CardOptions;
        Assert.Equal(3, options.Length);
        Assert.Equal(3, options.Distinct(StringComparer.Ordinal).Count());
        Assert.All(options, id => Assert.Equal(
            PrototypeCardRarity.Rare, PrototypeContent.Card(id).Rarity));
        Assert.Equal("proto.native.neow.injury",
            state.World.Reward.AddCardAfterReward);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck,
            c => c.CardId == "proto.native.neow.injury");
        Assert.Contains(state.Player.Relics,
            r => r.RelicId ==
                PrototypeNativeOvergrowthEvents.NeowRelicId("HeftyTablet"));

        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        var pickedId = options[0];
        state = ResolveReward(state, takeCard: takeRare);
        Assert.Equal(takeRare ? 15 : 14, state.Player.Deck.Length);
        Assert.Equal("proto.native.neow.injury",
            state.Player.Deck[^1].CardId);
        Assert.Equal(takeRare ? 16 : 15, state.World!.NextCardInstanceId);
        if (takeRare)
        {
            Assert.Equal(pickedId, state.Player.Deck[^2].CardId);
            Assert.Equal(PrototypeCardRarity.Rare,
                PrototypeContent.Card(state.Player.Deck[^2].CardId).Rarity);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void LostCofferOffersCardRewardAndPotionWithoutMapProgression(
        bool takeCard, bool takePotion)
    {
        var state = OfferAndChoose("LostCoffer",
            "neow-lost-coffer-" + takeCard + "-" + takePotion);
        var reward = state.World!.Reward!;
        Assert.Equal(3, reward.CardOptions.Length);
        Assert.False(reward.CardResolved);
        Assert.False(reward.PotionResolved);
        Assert.True(reward.RelicResolved);
        Assert.NotNull(reward.PotionOption);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.PotionSlots, p => p is not null);
        var selectedCard = reward.CardOptions[0];
        var offeredPotion = reward.PotionOption;

        state = ResolveReward(state, takeCard, takePotion);
        Assert.Equal(takeCard ? 14 : 13, state.Player.Deck.Length);
        Assert.Equal(takeCard ? selectedCard : "proto.common.restlessness",
            state.Player.Deck[^1].CardId);
        Assert.Equal(takePotion ? offeredPotion : null,
            state.Player.PotionSlots[0]?.PotionId);
        Assert.Contains(state.Player.Relics,
            r => r.RelicId ==
                PrototypeNativeOvergrowthEvents.NeowRelicId("LostCoffer"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ScrollBoxesOffersTwoDistinctThreeCardBundles(int bundleIndex)
    {
        var state = OfferAndChoose("ScrollBoxes",
            "neow-scroll-boxes-" + bundleIndex);
        var bundles = Assert.IsType<string[][]>(
            state.World!.Reward!.CardBundles);
        Assert.Equal(2, bundles.Length);
        Assert.All(bundles, bundle =>
        {
            Assert.Equal(3, bundle.Length);
            Assert.All(bundle.Take(2), id => Assert.Equal(
                PrototypeCardRarity.Common,
                PrototypeContent.Card(id).Rarity));
            Assert.Equal(PrototypeCardRarity.Uncommon,
                PrototypeContent.Card(bundle[2]).Rarity);
        });
        Assert.Equal(6, bundles.SelectMany(bundle => bundle)
            .Distinct(StringComparer.Ordinal).Count());

        var fork = state.Fork();
        Assert.NotSame(state.World!.Reward!.CardBundles,
            fork.World!.Reward!.CardBundles);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var engine = new PrototypeGameEngine();
        var legal = engine.GetLegalActions(state);
        Assert.Equal(2, legal.Count);
        Assert.All(legal, action =>
            Assert.Equal("take_reward_bundle", action.Kind));
        Assert.DoesNotContain(legal, action =>
            action.Kind == "skip_reward_card");

        var action = legal.Single(item =>
            item.ReadPayload<ChooseBundlePayload>().Index == bundleIndex);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        Assert.Equal(16, state.Player.Deck.Length);
        Assert.Equal(bundles[bundleIndex],
            state.Player.Deck.TakeLast(3).Select(card => card.CardId));
        Assert.Equal(new long[] { 14, 15, 16 },
            state.Player.Deck.TakeLast(3).Select(card => card.InstanceId));
        Assert.Equal(17, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);

        state = ResolveReward(state);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Contains(state.Player.Relics, relic => relic.RelicId ==
            PrototypeNativeOvergrowthEvents.NeowRelicId("ScrollBoxes"));
        Assert.Empty(state.World!.CompletedRooms);
    }

    [Fact]
    public void SmallCapsuleRelicRewardResumesTheSameNeowOpening()
    {
        var state = OfferAndChoose("SmallCapsule", "neow-small-capsule");
        var reward = state.World!.Reward!;
        Assert.True(reward.CardResolved);
        Assert.True(reward.PotionResolved);
        Assert.False(reward.RelicResolved);
        Assert.Empty(reward.CardOptions);
        Assert.NotNull(reward.RelicOption);
        Assert.DoesNotContain(state.Player.Relics,
            relic => relic.RelicId == reward.RelicOption);
        var offeredRelic = reward.RelicOption!;

        state = ResolveReward(state);
        Assert.Contains(state.Player.Relics,
            relic => relic.RelicId == offeredRelic);
        Assert.Contains(state.Player.Relics,
            relic => relic.RelicId ==
                PrototypeNativeOvergrowthEvents.NeowRelicId("SmallCapsule"));
        Assert.Equal(3, state.Player.Relics.Length);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(14, state.World!.NextCardInstanceId);
    }
}
