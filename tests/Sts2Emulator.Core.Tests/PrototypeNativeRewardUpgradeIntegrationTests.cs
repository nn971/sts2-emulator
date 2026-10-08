using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeRewardUpgradeIntegrationTests
{
    private static RunState WinFirstOvergrowthCombat(string seed)
    {
        var engine = new PrototypeGameEngine();
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
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("GoldenPearl"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("DowsingRod")
                    ]
                }
            }
        };
        var neow = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice" &&
            action.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("GoldenPearl"));
        state = engine.Step(state, neow).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
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
            Enemies = combat.Enemies.Select((enemy, i) =>
                enemy with { Hp = i == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        var play = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId);
        state = engine.Step(state, play).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void NativeActOneRewardGeneratesOptionFlagsAndExposesThem()
    {
        var state = WinFirstOvergrowthCombat("native-upgrade-observation");
        var reward = state.World!.Reward!;
        var flags = Assert.IsType<bool[]>(reward.CardOptionUpgradeFlags);
        Assert.Equal(reward.CardOptions.Length, flags.Length);
        Assert.All(flags, flag => Assert.False(flag));
        Assert.Equal(flags,
            new PrototypeAiEnvironment().Observe(state).Observation
                .Reward!.CardOptionUpgradeFlags);
    }

    [Fact]
    public void TakingSpecificCardUsesOnlyItsUpgradeFlagAndForkKeepsIndependence()
    {
        var state = WinFirstOvergrowthCombat("native-upgrade-choice");
        var reward = state.World!.Reward!;
        var flags = new bool[reward.CardOptions.Length];
        flags[1] = true;
        reward = reward with { CardOptionUpgradeFlags = flags };
        state = state with { World = state.World with { Reward = reward } };
        PrototypeStateInvariants.Validate(state);
        var fork = state.Fork();
        fork.World!.Reward!.CardOptionUpgradeFlags![1] = false;
        Assert.True(state.World!.Reward!.CardOptionUpgradeFlags![1]);
        var engine = new PrototypeGameEngine();
        var options = engine.GetLegalActions(state)
            .Where(action => action.Kind == "take_reward_card")
            .ToArray();
        var unupgraded = engine.Step(state, options[0]).State;
        var upgraded = engine.Step(state, options[1]).State;
        Assert.Equal(0, unupgraded.Player.Deck[^1].UpgradeLevel);
        Assert.Equal(1, upgraded.Player.Deck[^1].UpgradeLevel);
        Assert.Equal(flags,
            new PrototypeAiEnvironment().Observe(state).Observation
                .Reward!.CardOptionUpgradeFlags);
        PrototypeStateInvariants.Validate(upgraded);
    }

    [Fact]
    public void ExtraGroupsHaveIndependentPerOptionUpgradeFlags()
    {
        var state = WinFirstOvergrowthCombat("native-extra-upgrade");
        var reward = state.World!.Reward!;
        reward = reward with
        {
            ExtraCardOptions = [(string[])reward.CardOptions.Clone()],
            ExtraCardOptionsUpgraded = [false],
            ExtraCardOptionUpgradeFlags = [[true, false, false]]
        };
        state = state with { World = state.World with { Reward = reward } };
        PrototypeStateInvariants.Validate(state);
        var engine = new PrototypeGameEngine();
        var skipped = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.Kind == "skip_reward_card")).State;
        Assert.Equal(new[] { true, false, false },
            skipped.World!.Reward!.CurrentCardOptionUpgradeFlags);
        Assert.Equal(new[] { true, false, false },
            new PrototypeAiEnvironment().Observe(skipped).Observation
                .Reward!.CardOptionUpgradeFlags);
        var picked = engine.Step(skipped,
            engine.GetLegalActions(skipped).First(action =>
                action.Kind == "take_reward_card")).State;
        Assert.Equal(1, picked.Player.Deck[^1].UpgradeLevel);
        PrototypeStateInvariants.Validate(picked);
    }

    [Fact]
    public void RejectMismatchedRewardUpgradeMetadata()
    {
        var state = WinFirstOvergrowthCombat("native-upgrade-invariant");
        var reward = state.World!.Reward!;
        state = state with
        {
            World = state.World with
            {
                Reward = reward with { CardOptionUpgradeFlags = [true] }
            }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(state));
    }
}
