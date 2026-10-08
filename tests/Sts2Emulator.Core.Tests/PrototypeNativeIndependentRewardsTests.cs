using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeIndependentRewardsTests
{
    private const string OfferedRelic = "proto.relic.smiling_mask";

    private static RunState WinFirstCombat(
        string seed, bool forcePotion = true,
        bool prayerWheel = false)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var pearl = PrototypeNativeOvergrowthEvents.NeowRelicId(
            "GoldenPearl");
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    OfferedChoiceIds =
                    [
                        "take_" + pearl,
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "DowsingRod")
                    ]
                }
            }
        };
        state = engine.Step(state, engine.GetLegalActions(state).Single(
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "take_" + pearl)).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        if (forcePotion || prayerWheel)
        {
            var relics = new List<RelicInstance>(
                state.Player.Relics);
            if (forcePotion)
            {
                relics.Add(new RelicInstance(
                    "proto.relic.white_beast_statue",
                    PrototypeJson.EmptyObject()));
            }
            if (prayerWheel)
            {
                relics.Add(new RelicInstance(
                    "proto.relic.prayer_wheel",
                    PrototypeJson.EmptyObject()));
            }
            state = state with
            {
                Player = state.Player with { Relics = relics.ToArray() }
            };
        }
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
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        var action = engine.GetLegalActions(state).Single(option =>
            option.Kind == "play_card"
            && option.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId);
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.Reward, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState WithRelicOffer(
        RunState state, string id = OfferedRelic)
    {
        var world = state.World!;
        var reward = world.Reward!;
        state = state with
        {
            World = world with
            {
                Reward = reward with
                {
                    RelicOption = id,
                    RelicResolved = false
                }
            }
        };
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static GameAction Action(
        PrototypeGameEngine engine, RunState state, string kind) =>
        engine.GetLegalActions(state).First(action =>
            action.Kind == kind);

    [Fact]
    public void AllNativeRewardTypesAreAvailableAtOnce()
    {
        var state = WithRelicOffer(WinFirstCombat(
            "independent-rewards-all"));
        var engine = new PrototypeGameEngine();
        var kinds = engine.GetLegalActions(state)
            .Select(action => action.Kind).ToHashSet(
                StringComparer.Ordinal);
        Assert.Contains("take_reward_card", kinds);
        Assert.Contains("skip_reward_card", kinds);
        Assert.Contains("take_reward_potion", kinds);
        Assert.Contains("skip_reward_potion", kinds);
        Assert.Contains("take_reward_relic", kinds);
        Assert.Contains("take_reward_gold", kinds);
        Assert.Contains("leave_reward", kinds);
        var frame = new PrototypeAiEnvironment().Observe(state);
        Assert.True(frame.Observation.Reward!.IndependentSelection);
        Assert.Equal(kinds,
            frame.LegalActions.Select(action => action.Kind)
                .ToHashSet(StringComparer.Ordinal));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakingPotionOrRelicFirstPreservesOtherRewards(
        bool potionFirst)
    {
        var state = WithRelicOffer(WinFirstCombat(
            "independent-order-" + potionFirst));
        var engine = new PrototypeGameEngine();
        var initial = state.Fork();
        var first = potionFirst
            ? "take_reward_potion"
            : "take_reward_relic";
        var second = potionFirst
            ? "take_reward_relic"
            : "take_reward_potion";

        state = engine.Step(state, Action(engine, state, first)).State;
        Assert.False(state.World!.Reward!.CardResolved);
        Assert.False(state.World.Reward.GoldResolved);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == second);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_card");
        state = engine.Step(state, Action(engine, state, second)).State;
        Assert.True(state.World!.Reward!.PotionResolved);
        Assert.True(state.World.Reward.RelicResolved);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == OfferedRelic);
        Assert.Contains(state.Player.PotionSlots, potion =>
            potion?.PotionId == initial.World!.Reward!.PotionOption);

        state = engine.Step(state,
            Action(engine, state, "take_reward_card")).State;
        Assert.True(state.World!.Reward!.CardResolved);
        Assert.Equal(initial.Player.Deck.Length + 1,
            state.Player.Deck.Length);
        var gold = Assert.IsType<int>(
            state.World.Reward.GoldOption);
        state = engine.Step(state,
            Action(engine, state, "take_reward_gold")).State;
        Assert.Equal(initial.Player.Gold + gold, state.Player.Gold);
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind is "take_reward_gold"
                or "take_reward_card"
                or "take_reward_potion"
                or "take_reward_relic");
        state = engine.Step(state,
            Action(engine, state, "leave_reward")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ExtraCardGroupDoesNotBlockIndependentPotionSelection()
    {
        var state = WinFirstCombat(
            "independent-extra-card", prayerWheel: true);
        var engine = new PrototypeGameEngine();
        Assert.Single(state.World!.Reward!.ExtraCardOptions!);
        state = engine.Step(state,
            Action(engine, state, "skip_reward_card")).State;
        Assert.True(state.World!.Reward!.CardResolved);
        Assert.Equal(0, state.World.Reward.ExtraCardRewardsResolved);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_potion");
        var originalExtra = state.World.Reward.CurrentCardOptions;
        state = engine.Step(state,
            Action(engine, state, "take_reward_potion")).State;
        Assert.True(state.World!.Reward!.PotionResolved);
        Assert.Equal(originalExtra, state.World.Reward.CurrentCardOptions);
        state = engine.Step(state,
            Action(engine, state, "skip_reward_card")).State;
        Assert.Equal(1, state.World!.Reward!.ExtraCardRewardsResolved);
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_card");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void FullPotionSlotsCanReplacePotionBeforeCardSelection()
    {
        var state = WinFirstCombat(
            "independent-potion-replacement");
        var slots = state.Player.PotionSlots.Select(_ =>
            (PotionInstance?)new PotionInstance(
                "proto.potion.block", PrototypeJson.EmptyObject()))
            .ToArray();
        state = state with
        {
            Player = state.Player with { PotionSlots = slots }
        };
        PrototypeStateInvariants.Validate(state);
        var engine = new PrototypeGameEngine();
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_potion");
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "replace_reward_potion");
        var expected = state.World!.Reward!.PotionOption;
        state = engine.Step(state,
            Action(engine, state, "replace_reward_potion")).State;
        Assert.Equal(expected, state.Player.PotionSlots[0]?.PotionId);
        Assert.False(state.World!.Reward!.CardResolved);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_card");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void MandatoryRelicDeckChoiceSuspendsAllOtherChoices()
    {
        var state = WithRelicOffer(WinFirstCombat(
            "independent-mandatory-deck"), "proto.relic.empty_cage");
        var engine = new PrototypeGameEngine();
        state = engine.Step(state,
            Action(engine, state, "take_reward_relic")).State;
        Assert.NotNull(state.World!.Reward!.PendingDeckChoice);
        Assert.All(engine.GetLegalActions(state), action =>
            Assert.Equal("choose_relic_deck_card", action.Kind));
        Assert.Throws<InvalidOperationException>(() => engine.Step(
            state, GameAction.Empty("leave_reward")));
        Assert.Throws<InvalidOperationException>(() => engine.Step(
            state, GameAction.Empty("take_reward_gold")));

        for (var i = 0; i < 2; i++)
        {
            state = engine.Step(state,
                Action(engine, state, "choose_relic_deck_card")).State;
        }

        Assert.Null(state.World!.Reward!.PendingDeckChoice);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_card");
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_gold");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ExtraRelicCanBePickedWhileCardAndPotionRemainUnresolved()
    {
        var state = WithRelicOffer(WinFirstCombat(
            "independent-extra-relic"));
        var reward = state.World!.Reward!;
        reward = reward with
        {
            ExtraRelicRewardIds = ["proto.relic.prayer_wheel"]
        };
        state = state with
        {
            World = state.World with { Reward = reward }
        };
        PrototypeStateInvariants.Validate(state);
        var engine = new PrototypeGameEngine();
        state = engine.Step(state,
            Action(engine, state, "take_reward_relic")).State;
        Assert.False(state.World!.Reward!.CardResolved);
        Assert.False(state.World.Reward.PotionResolved);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_relic");
        state = engine.Step(state,
            Action(engine, state, "take_reward_relic")).State;
        Assert.Equal(1, state.World!.Reward!.ExtraRelicsResolved);
        Assert.Contains(state.Player.Relics,
            relic => relic.RelicId == "proto.relic.prayer_wheel");
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_card");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LegacyRewardStillUsesOrderedSelection()
    {
        var native = WithRelicOffer(WinFirstCombat(
            "independent-legacy-guard"));
        var world = native.World!;
        var state = native with
        {
            World = world with
            {
                Reward = world.Reward! with
                {
                    IndependentSelection = false,
                    GoldOption = null,
                    GoldResolved = true
                }
            }
        };
        var engine = new PrototypeGameEngine();
        Assert.All(engine.GetLegalActions(state),
            action => Assert.Contains(action.Kind,
                new[] { "take_reward_card", "skip_reward_card" }));
        state = engine.Step(state,
            Action(engine, state, "skip_reward_card")).State;
        Assert.All(engine.GetLegalActions(state),
            action => Assert.Contains(action.Kind,
                new[] { "take_reward_potion", "replace_reward_potion",
                    "skip_reward_potion" }));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void IndependenceRequiresCombatSource()
    {
        var state = WinFirstCombat("independent-source-validation");
        var invalid = state with
        {
            World = state.World! with
            {
                Reward = state.World.Reward! with
                {
                    SourceRoom = "Event:Noncombat"
                }
            }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(invalid));
    }
}
