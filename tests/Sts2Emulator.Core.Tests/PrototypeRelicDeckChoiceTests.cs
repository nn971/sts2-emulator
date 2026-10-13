using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRelicDeckChoiceTests
{
    [Fact]
    public void EmptyCageRemovesTwoChosenPersistentCardsSequentially()
    {
        var engine = new PrototypeGameEngine();
        var state = RewardStateForRelic(
            "proto.relic.empty_cage",
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.neutralize")
            ]);

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_relic")).State;

        PrototypeStateInvariants.Validate(state);
        var reward = state.World!.Reward!;
        Assert.True(reward.RelicResolved);
        Assert.NotNull(reward.PendingDeckChoice);
        Assert.Equal(
            PrototypePersistentDeckChoiceKind.Remove,
            reward.PendingDeckChoice!.Kind);
        Assert.Equal(2, reward.PendingDeckChoice.RemainingSelections);
        Assert.Equal(
            new long[] { 1, 2, 3 },
            reward.PendingDeckChoice.CandidateCardInstanceIds);

        var frame = new PrototypeAiEnvironment().Observe(state);
        Assert.NotNull(frame.Observation.Reward!.PendingDeckChoice);
        Assert.Equal(
            2,
            frame.Observation.Reward.PendingDeckChoice!
                .RemainingSelections);
        Assert.Equal(
            3,
            frame.LegalActions.Count(action =>
                action.Kind == "choose_relic_deck_card"));

        state = ChooseDeckCard(engine, state, 1);
        PrototypeStateInvariants.Validate(state);
        Assert.DoesNotContain(
            state.Player.Deck,
            card => card.InstanceId == 1);
        Assert.Equal(
            1,
            state.World!.Reward!.PendingDeckChoice!
                .RemainingSelections);

        state = ChooseDeckCard(engine, state, 2);
        PrototypeStateInvariants.Validate(state);
        Assert.Null(
            state.World!.Reward!.PendingDeckChoice);
        Assert.Equal(
            new long[] { 3 },
            state.Player.Deck
                .Select(card => card.InstanceId)
                .ToArray());
        Assert.Contains(
            engine.GetLegalActions(state),
            action => action.Kind == "leave_reward");
    }

    [Fact]
    public void AstrolabeTransformsThreeChosenCardsAndUpgradesResults()
    {
        var engine = new PrototypeGameEngine();
        var originals = new[]
        {
            Card(1, "proto.silent.strike"),
            Card(2, "proto.silent.defend"),
            Card(3, "proto.silent.survivor"),
            Card(4, "proto.silent.neutralize")
        };
        var state = RewardStateForRelic(
            "proto.relic.astrolabe",
            originals);

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_relic")).State;

        Assert.Equal(
            3,
            state.World!.Reward!.PendingDeckChoice!
                .RemainingSelections);

        foreach (var id in new long[] { 1, 2, 3 })
        {
            state = ChooseDeckCard(
                engine,
                state,
                id);
            PrototypeStateInvariants.Validate(state);
        }

        Assert.Null(
            state.World!.Reward!.PendingDeckChoice);

        foreach (var id in new long[] { 1, 2, 3 })
        {
            var transformed = state.Player.Deck
                .Single(card => card.InstanceId == id);
            var original = originals
                .Single(card => card.InstanceId == id);
            Assert.NotEqual(
                original.CardId,
                transformed.CardId);
            Assert.Contains(
                transformed.CardId,
                PrototypeContent.RewardCardPool);
            Assert.Equal(1, transformed.UpgradeLevel);
            Assert.Null(transformed.Enchantment);
        }

        var untouched = state.Player.Deck
            .Single(card => card.InstanceId == 4);
        Assert.Equal(
            "proto.silent.neutralize",
            untouched.CardId);
        Assert.Equal(0, untouched.UpgradeLevel);
    }

    [Fact]
    public void AcquisitionDeckChoiceUsesAtMostEligibleDeckSize()
    {
        var engine = new PrototypeGameEngine();
        var state = RewardStateForRelic(
            "proto.relic.astrolabe",
            [Card(1, "proto.silent.strike")]);

        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state),
                action => action.Kind == "take_reward_relic")).State;

        var pending =
            state.World!.Reward!.PendingDeckChoice!;
        Assert.Equal(1, pending.RemainingSelections);
        Assert.Equal(
            new long[] { 1 },
            pending.CandidateCardInstanceIds);

        state = ChooseDeckCard(
            engine,
            state,
            1);
        Assert.Null(
            state.World!.Reward!.PendingDeckChoice);
    }

    private static RunState ChooseDeckCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind
                    == "choose_relic_deck_card"
                && item.ReadPayload<ChooseDeckCardPayload>()
                    .CardInstanceId
                    == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static CardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            cardId,
            0,
            PrototypeJson.EmptyObject());

    private static RunState RewardStateForRelic(
        string relicId,
        CardInstance[] deck)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            deck,
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "relic-deck-choice",
            "relic-deck-choice",
            0,
            RunPhase.Reward,
            player,
            PrototypeRng.CreateBundle(
                "relic-deck-choice"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                deck
                    .Select(card => card.InstanceId)
                    .DefaultIfEmpty(0)
                    .Max() + 1,
                PrototypeRoomType.Boss,
                new MapState([]),
                null,
                new RewardState(
                    "Boss",
                    [],
                    null,
                    null,
                    CardResolved: true,
                    PotionResolved: true,
                    RelicResolved: false,
                    EndsAct: true,
                    RelicOptions: [relicId]),
                null,
                null,
                null));
    }
}
