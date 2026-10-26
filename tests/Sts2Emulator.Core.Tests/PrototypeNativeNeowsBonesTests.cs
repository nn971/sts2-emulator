using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeNeowsBonesTests
{
    private static RunState ChooseNeowsBones(string seed)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var world = state.World!;
        var id = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "NeowsBones");
        state = state with
        {
            World = world with
            {
                Event = world.Event! with
                {
                    NativeEventRngProviderMode = "scripted-v111",
                    NativeEventRng = NativeRngV111Contexts.ForEvent(
                        state.RunSeed,
                        PrototypeNativeOvergrowthEvents.NeowRngEventEntry),
                    OfferedChoiceIds =
                    [
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "GoldenPearl"),
                        "take_" + id
                    ]
                }
            }
        };
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + id);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void PomanderGrantedAsARewardCanUpgradePersistentCards()
    {
        var state = ChooseNeowsBones("bones-pomander-reward");
        var pomander = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "Pomander");
        var goldenPearl = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "GoldenPearl");
        state = state with
        {
            World = state.World! with
            {
                Reward = state.World.Reward! with
                {
                    ExtraRelicRewardIds = [pomander, goldenPearl]
                }
            }
        };
        PrototypeStateInvariants.Validate(state);

        var engine = new PrototypeGameEngine();
        var first = engine.GetLegalActions(state).Single();
        Assert.Equal("take_reward_relic", first.Kind);
        state = engine.Step(state, first).State;
        var pending = Assert.IsType<PrototypePendingDeckChoiceState>(
            state.World!.Reward!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Upgrade,
            pending.Kind);
        Assert.Contains(1L, pending.CandidateCardInstanceIds);
        PrototypeStateInvariants.Validate(state);

        var upgrade = engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_relic_deck_card"
            && action.ReadPayload<ChooseDeckCardPayload>()
                .CardInstanceId == 1L);
        state = engine.Step(state, upgrade).State;
        Assert.Equal(1, state.Player.Deck.Single(card =>
            card.InstanceId == 1L).UpgradeLevel);
        Assert.Equal(1, state.World!.Reward!.ExtraRelicsResolved);
        Assert.Null(state.World.Reward.PendingDeckChoice);
        PrototypeStateInvariants.Validate(state);

        var second = engine.GetLegalActions(state).Single();
        Assert.Equal("take_reward_relic", second.Kind);
        state = engine.Step(state, second).State;
        Assert.Equal(2, state.World!.Reward!.ExtraRelicsResolved);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == goldenPearl);
        Assert.Equal(249, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);

        var leave = engine.GetLegalActions(state).Single();
        Assert.Equal("leave_reward", leave.Kind);
        state = engine.Step(state, leave).State;
        state = PrototypeEventPageTestHelperV111.ContinueIfFinished(state);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(1, state.Player.Deck.Single(card =>
            card.InstanceId == 1L).UpgradeLevel);
        Assert.Contains(state.Player.Deck[^1].CardId,
            NativeGloryAncientsV111.ModifierGeneratedCurseIds);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData("bones-000")]
    [InlineData("bones-001")]
    [InlineData("bones-002")]
    [InlineData("bones-003")]
    [InlineData("bones-004")]
    [InlineData("bones-005")]
    [InlineData("bones-006")]
    [InlineData("bones-007")]
    public void NeowsBonesGrantsTwoDistinctEligibleNeowRelicsThenCurse(
        string seed)
    {
        var state = ChooseNeowsBones(seed);
        var reward = state.World!.Reward!;
        Assert.Equal(2, reward.ExtraRelicRewardIds?.Length);
        Assert.Equal(0, reward.ExtraRelicsResolved);
        Assert.True(reward.CardResolved);
        Assert.True(reward.PotionResolved);
        Assert.True(reward.RelicResolved);
        Assert.Null(reward.RelicOption);
        Assert.True(reward.AddRandomCurseAfterReward);
        Assert.Null(reward.AddCardAfterReward);
        Assert.DoesNotContain(state.Player.Deck, c =>
            NativeGloryAncientsV111.ModifierGeneratedCurseIds
                .Contains(c.CardId, StringComparer.Ordinal));
        Assert.Empty(state.World.CompletedRooms);

        var ids = reward.ExtraRelicRewardIds!;
        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id =>
        {
            Assert.StartsWith("proto.native.neow.", id,
                StringComparison.Ordinal);
            Assert.DoesNotContain(id,
                new[]
                {
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "NeowsBones"),
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "Kaleidoscope"),
                    PrototypeNativeOvergrowthEvents.NeowRelicId(
                        "MassiveScroll")
                });
        });

        var fork = state.Fork();
        Assert.NotSame(state.World.Reward!.ExtraRelicRewardIds,
            fork.World!.Reward!.ExtraRelicRewardIds);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var engine = new PrototypeGameEngine();
        for (var i = 0; i < 20 && state.Phase == RunPhase.Reward; i++)
        {
            var legal = engine.GetLegalActions(state);
            var action = legal.FirstOrDefault(a =>
                a.Kind == "choose_relic_deck_card")
                ?? legal.FirstOrDefault(a =>
                    a.Kind is "take_reward_relic"
                        or "take_reward_extra_relic")
                ?? legal.FirstOrDefault(a =>
                    a.Kind is "take_reward_bundle"
                        or "take_reward_card"
                        or "take_reward_card_group")
                ?? legal.FirstOrDefault(a =>
                    a.Kind is "take_reward_potion"
                        or "take_reward_extra_potion")
                ?? legal.FirstOrDefault(a =>
                    a.Kind is "skip_reward_card"
                        or "skip_reward_card_group"
                        or "skip_reward_potion"
                        or "skip_reward_extra_potion")
                ?? legal.Single(a => a.Kind == "leave_reward");
            state = engine.Step(state, action).State;
            PrototypeStateInvariants.Validate(state);
        }

        state = PrototypeEventPageTestHelperV111.ContinueIfFinished(state);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Null(state.World.Reward);
        Assert.Equal(0, state.World.Floor);
        Assert.Empty(state.World.CompletedRooms);
        Assert.All(ids, id => Assert.Contains(state.Player.Relics,
            relic => relic.RelicId == id));
        var acquisitionRewardRelics = ids.Sum(id => id switch
        {
            var value when value ==
                PrototypeNativeOvergrowthEvents.NeowRelicId("SmallCapsule") => 1,
            var value when value ==
                PrototypeNativeOvergrowthEvents.NeowRelicId("LargeCapsule") => 2,
            _ => 0
        });
        Assert.Equal(4 + acquisitionRewardRelics, state.Player.Relics.Length);
        var expectedModifierCurseCount = 1 + ids.Count(id => id ==
            PrototypeNativeOvergrowthEvents.NeowRelicId("HeftyTablet"));
        Assert.Equal(expectedModifierCurseCount, state.Player.Deck.Count(c =>
            NativeGloryAncientsV111.ModifierGeneratedCurseIds
                .Contains(c.CardId, StringComparer.Ordinal)));
        Assert.Contains(state.Player.Deck[^1].CardId,
            NativeGloryAncientsV111.ModifierGeneratedCurseIds);
        PrototypeStateInvariants.Validate(state);
    }
}
