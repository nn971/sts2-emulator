using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeOvergrowthStarterTests
{
    [Fact]
    public void NativeProfileStartsWithCapturedThirteenCardSilentDeck()
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create("starter-deck");
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(5, state.Player.Deck.Count(card =>
            card.CardId == "proto.silent.strike"));
        Assert.Equal(5, state.Player.Deck.Count(card =>
            card.CardId == "proto.silent.defend"));
        Assert.Single(state.Player.Deck, card =>
            card.CardId == "proto.silent.neutralize");
        Assert.Single(state.Player.Deck, card =>
            card.CardId == "proto.silent.survivor");
        Assert.Single(state.Player.Deck, card =>
            card.CardId == "proto.common.restlessness");
        Assert.Equal(14, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void NativeFactoryExposesReplayablePublicOpening(int ascension)
    {
        var environment = new PrototypeAiEnvironment();
        var first = PrototypeNativeOvergrowthRunFactory.Create(
            $"training-gate-{ascension}", ascension);
        var replay = PrototypeNativeOvergrowthRunFactory.Create(
            $"training-gate-{ascension}", ascension);
        var frame = environment.Observe(first);
        var repeated = environment.Observe(replay);

        Assert.Equal(
            PrototypeNativeOvergrowthMap.GenerationProfileId,
            frame.Observation.MapGenerationProfileId);
        Assert.Equal(1, frame.Observation.Act);
        Assert.Equal(0, frame.Observation.Floor);
        Assert.Equal(RunPhase.Event, frame.Observation.Phase);
        Assert.Equal(
            PrototypeNativeOvergrowthEvents.NeowEventId,
            frame.Observation.EventId);
        Assert.Contains(frame.Observation.Map, node => node.Floor == 16);
        Assert.Equal(13, frame.Observation.Deck.Length);
        Assert.Single(frame.Observation.Deck,
            card => card.CardId == "proto.common.restlessness");
        Assert.Equal(3, frame.LegalActions.Length);
        Assert.All(frame.LegalActions, action =>
            Assert.Equal("event_choice", action.Kind));

        Assert.Equal(frame.ObservationHash, repeated.ObservationHash);
        Assert.Equal(frame.CanonicalStateHash, repeated.CanonicalStateHash);
        Assert.Equal(
            frame.LegalActions.Select(action => action.ActionId),
            repeated.LegalActions.Select(action => action.ActionId));
        var next = environment.Step(first, frame.LegalActions[0].ActionId).State;
        var nextReplay = environment.Step(
            replay, repeated.LegalActions[0].ActionId).State;
        Assert.Equal(CanonicalJson.Sha256(next),
            CanonicalJson.Sha256(nextReplay));
        PrototypeStateInvariants.Validate(next);
    }

    [Theory]
    [InlineData(false, 0, 2, 2)]
    [InlineData(true, 0, 3, 3)]
    [InlineData(false, 1, 0, 0)]
    public void RestlessnessChecksLastCardAtPlayTime(
        bool upgraded, int otherCardsInHand,
        int expectedDraw, int expectedEnergy)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            $"restlessness-{upgraded}-{otherCardsInHand}");
        // This card-behavior test needs a no-deck-change Neow option.
        // The generated Neow pool also contains multi-page rewards
        // (Scroll Boxes, Lost Coffer, etc.), so blindly taking its
        // first random offer is no longer a valid setup shortcut.
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    OfferedChoiceIds =
                    [
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "GoldenPearl"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "DowsingRod")
                    ]
                }
            }
        };
        var opening = engine.GetLegalActions(state).Single(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "GoldenPearl"));
        state = engine.Step(state, opening).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        var first = engine.GetLegalActions(state)[0];
        state = engine.Step(state, first).State;
        var combat = state.World!.Combat!;
        var rest = Assert.Single(combat.Cards, card =>
            card.CardId == "proto.common.restlessness");

        var ids = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != rest.InstanceId)
            .ToArray();
        var retained = ids.Take(otherCardsInHand).ToArray();
        var drawn = ids.Skip(otherCardsInHand).ToArray();

        combat = combat with
        {
            Hand = new[] { rest.InstanceId }.Concat(retained).ToArray(),
            DrawPile = drawn,
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3,
            Cards = combat.Cards.Select(card =>
                card.InstanceId == rest.InstanceId && upgraded
                    ? card with { UpgradeLevel = 1 }
                    : card).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        PrototypeStateInvariants.Validate(state);

        var action = Assert.Single(
            engine.GetLegalActions(state), act =>
                act.Kind == "play_card"
                && act.ReadPayload<PlayCardPayload>().CardInstanceId
                    == rest.InstanceId);
        state = engine.Step(state, action).State;
        Assert.Equal(3 + expectedEnergy, state.World!.Combat!.Energy);
        Assert.Equal(otherCardsInHand + expectedDraw,
            state.World.Combat.Hand.Length);
        PrototypeStateInvariants.Validate(state);
    }
}
