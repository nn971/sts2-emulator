using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// v0.111.0 WhiteBeastStatue.ShouldForcePotionReward integration:
/// the relic guarantees a potion and bypasses the normal RNG/pity
/// transition, including when the current pity value is negative.
/// </summary>
public sealed class PrototypeNativeWhiteBeastStatueTests
{
    private const string RelicId = "proto.relic.white_beast_statue";

    private static RunState WinFirstCombat(
        string seed, bool withStatue, int initialOdds)
    {
        var engine = new PrototypeGameEngine();
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
            && action.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + neowId);
        state = engine.Step(state, neow).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);

        var player = state.Player;
        var world = state.World!;
        if (withStatue)
        {
            player = player with
            {
                Relics = player.Relics.Append(new RelicInstance(
                    RelicId, PrototypeJson.EmptyObject())).ToArray()
            };
            // The owned relic must not remain in the native relic bag.
            var bag = world.RelicBag!;
            world = world with
            {
                RelicBag = bag with
                {
                    Rare = bag.Rare
                        .Where(id => !StringComparer.Ordinal.Equals(
                            id, RelicId)).ToArray()
                }
            };
        }
        state = state with
        {
            Player = player,
            World = world with
            {
                PotionRewardOddsThousandths = initialOdds
            }
        };
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state,
            engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        var combat = state.World!.Combat!;
        var neutralize = combat.Cards.Single(card =>
            card.CardId == "proto.silent.neutralize");
        combat = combat with
        {
            Hand = [neutralize.InstanceId],
            DrawPile = combat.Cards.Select(card =>
                card.InstanceId)
                .Where(id => id != neutralize.InstanceId).ToArray(),
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
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
    public void CatalogPlacesWhiteBeastStatueInRareRelicBag()
    {
        Assert.True(PrototypeContent.Relic(RelicId)
            .ForceCombatPotionReward);
        Assert.True(PrototypeNativeRelicGrabBag.TryGetRarity(
            RelicId, out var rarity));
        Assert.Equal(PrototypeRelicRarity.Rare, rarity);
        var initial = PrototypeNativeOvergrowthRunFactory.Create(
            "white-beast-catalog");
        var bag = Assert.IsType<PrototypeRelicBagState>(
            initial.World!.RelicBag);
        Assert.Contains(RelicId, bag.Rare);
    }

    [Fact]
    public void RelicGuaranteesPotionAndDoesNotMovePity()
    {
        var state = WinFirstCombat(
            "white-beast-forced-potion", true, -200);
        Assert.Equal(-200,
            state.World!.PotionRewardOddsThousandths);
        var reward = state.World.Reward!;
        Assert.NotNull(reward.PotionOption);
        Assert.False(reward.PotionResolved);
        var engine = new PrototypeGameEngine();
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.Kind == "skip_reward_card")).State;
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "take_reward_potion"
                || action.Kind == "replace_reward_potion");
        var observation = new PrototypeAiEnvironment()
            .Observe(state).Observation.Reward!;
        Assert.Equal(reward.PotionOption, observation.PotionOption);
        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(fork));
    }

    [Fact]
    public void WithoutRelicNegativePityCannotOfferPotionAndGrows()
    {
        var state = WinFirstCombat(
            "white-beast-no-relic", false, -200);
        Assert.Null(state.World!.Reward!.PotionOption);
        Assert.True(state.World.Reward.PotionResolved);
        Assert.Equal(-100,
            state.World.PotionRewardOddsThousandths);
    }

    [Fact]
    public void NormalPityStillUpdatesWithoutForce()
    {
        var state = WinFirstCombat(
            "white-beast-normal-pity", false, 400);
        Assert.Contains(state.World!.PotionRewardOddsThousandths,
            new[] { 300, 500 });
    }
}
