using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeCombatGoldRewardTests
{
    [Theory]
    [InlineData(PrototypeRoomType.Combat, 0, 10, 20)]
    [InlineData(PrototypeRoomType.Elite, 0, 35, 45)]
    [InlineData(PrototypeRoomType.Boss, 0, 100, 100)]
    [InlineData(PrototypeRoomType.Combat, 2, 10, 20)]
    [InlineData(PrototypeRoomType.Combat, 3, 7, 15)]
    [InlineData(PrototypeRoomType.Elite, 3, 26, 33)]
    [InlineData(PrototypeRoomType.Boss, 3, 75, 75)]
    [InlineData(PrototypeRoomType.Elite, 9, 26, 33)]
    public void GoldBoundsFollowPinnedEncounterDefaultsAndPoverty(
        PrototypeRoomType room, int ascension, int min, int max)
    {
        Assert.Equal((min, max),
            PrototypeNativeCombatGoldReward.Bounds(room, ascension));
    }

    [Fact]
    public void NormalGoldUsesNativeProportionAndBankersRounding()
    {
        Assert.Equal((5, 10), PrototypeNativeCombatGoldReward.Bounds(
            PrototypeRoomType.Combat, 0, 0.5f));
        Assert.Equal((4, 8), PrototypeNativeCombatGoldReward.Bounds(
            PrototypeRoomType.Combat, 3, 0.5f));
        Assert.Null(PrototypeNativeCombatGoldReward.Bounds(
            PrototypeRoomType.Combat, 0, 0f));
        Assert.Equal((26, 33), PrototypeNativeCombatGoldReward.Bounds(
            PrototypeRoomType.Elite, 3, 0f));
    }

    [Fact]
    public void RewardStreamRollIsInclusiveAndForkDeterministic()
    {
        var rng = PrototypeRng.CreateBundle("native-gold-roll");
        var expected = rng.Fork();
        foreach (var (room, ascension, min, max) in new[]
        {
            (PrototypeRoomType.Combat, 0, 10, 20),
            (PrototypeRoomType.Elite, 3, 26, 33),
            (PrototypeRoomType.Boss, 0, 100, 100)
        })
        {
            for (var i = 0; i < 64; i++)
            {
                var value = PrototypeNativeCombatGoldReward.Roll(
                    room, ascension, rng);
                var roll = min + PrototypeRng.NextInt(
                    expected, "reward", max - min + 1);
                Assert.Equal((int?)roll, value);
                Assert.InRange(value!.Value, min, max);
            }
        }
        Assert.Equal(expected.Streams.Single(s => s.StreamId == "reward").CallCount,
            rng.Streams.Single(s => s.StreamId == "reward").CallCount);
        var calls = rng.Streams.Single(s => s.StreamId == "reward").CallCount;
        Assert.Null(PrototypeNativeCombatGoldReward.Roll(
            PrototypeRoomType.Combat, 0, rng, 0f));
        Assert.Equal(calls,
            rng.Streams.Single(s => s.StreamId == "reward").CallCount);
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        var rng = PrototypeRng.CreateBundle("native-gold-invalid");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeCombatGoldReward.Bounds(
                PrototypeRoomType.Combat, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeCombatGoldReward.Bounds(
                PrototypeRoomType.Combat, 0, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeCombatGoldReward.Roll(
                PrototypeRoomType.Shop, 0, rng));
    }

    private static RunState WinFirstCombat(string seed, int ascension,
        out int goldBefore)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            seed, ascension);
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
        var start = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + neowId);
        state = engine.Step(state, start).State;
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
            Enemies = combat.Enemies.Select((enemy, index) =>
                enemy with { Hp = index == 0 ? 1 : 0, Block = 0 }).ToArray()
        };
        state = state with { World = state.World with { Combat = combat } };
        goldBefore = state.Player.Gold;
        var attack = engine.GetLegalActions(state).Single(action =>
            action.Kind == "play_card"
            && action.ReadPayload<PlayCardPayload>().CardInstanceId
                == neutralize.InstanceId);
        return engine.Step(state, attack).State;
    }

    [Theory]
    [InlineData(0, 10, 20)]
    [InlineData(3, 7, 15)]
    public void NativeOvergrowthVictoryUsesRolledGoldInsteadOfActFormula(
        int ascension, int minimum, int maximum)
    {
        var state = WinFirstCombat(
            $"native-overgrowth-gold-{ascension}",
            ascension, out var goldBefore);
        Assert.Equal(RunPhase.Reward, state.Phase);
        var reward = state.World!.Reward!;
        var amount = Assert.IsType<int>(reward.NativeCombatGoldAutoGranted);
        Assert.InRange(amount, minimum, maximum);
        Assert.Equal(goldBefore + amount, state.Player.Gold);
        Assert.NotEqual(25, amount);
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }
}
