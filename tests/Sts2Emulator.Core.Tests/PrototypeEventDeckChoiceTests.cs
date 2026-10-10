using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEventDeckChoiceTests
{
    [Fact]
    public void ForgottenAltarPurgePaysOnceThenRemovesChosenCard()
    {
        var engine = new PrototypeGameEngine();
        var state = EventState(
            "proto.event.forgotten_altar",
            act: 1,
            hp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ]);

        var choice = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>()
                    .ChoiceId == "purge");

        state = engine.Step(state, choice).State;

        Assert.Equal(50, state.Player.Gold);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(
            "purge",
            state.World!.Event!.ChosenChoiceId);
        Assert.Equal(
            PrototypePersistentDeckChoiceKind.Remove,
            state.World.Event.PendingDeckChoice!.Kind);

        var frame =
            new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            2,
            frame.LegalActions.Count(action =>
                action.Kind
                    == "choose_event_deck_card"));
        Assert.Equal(
            1,
            frame.Observation.Event!
                .PendingDeckChoice!
                .RemainingSelections);

        state = ChooseCard(
            engine,
            state,
            1);

        Assert.Equal(50, state.Player.Gold);
        Assert.DoesNotContain(
            state.Player.Deck,
            card => card.InstanceId == 1);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UnaffordableOrLethalEventChoicesAreNotLegal()
    {
        var engine = new PrototypeGameEngine();
        var state = EventState(
            "proto.event.forgotten_altar",
            act: 1,
            hp: 7,
            gold: 49,
            deck:
            [
                Card(1, "proto.silent.strike")
            ]);

        var choices = engine.GetLegalActions(state)
            .Where(action =>
                action.Kind == "event_choice")
            .Select(action =>
                action.ReadPayload<EventChoicePayload>()
                    .ChoiceId)
            .ToArray();

        Assert.DoesNotContain("purge", choices);
        Assert.DoesNotContain("refine", choices);
        Assert.Contains("leave", choices);
    }

    [Fact]
    public void ForgottenAltarUpgradePreservesPersistentCardIdentity()
    {
        var engine = new PrototypeGameEngine();
        var state = EventState(
            "proto.event.forgotten_altar",
            act: 1,
            hp: 50,
            gold: 0,
            deck:
            [
                Card(1, "proto.silent.backflip"),
                Card(2, "proto.silent.strike")
                    with { UpgradeLevel = 1 }
            ]);

        state = ChooseEvent(
            engine,
            state,
            "refine");

        Assert.Equal(43, state.Player.Hp);
        var pending =
            state.World!.Event!.PendingDeckChoice!;
        Assert.Equal(
            new long[] { 1 },
            pending.CandidateCardInstanceIds);

        state = ChooseCard(
            engine,
            state,
            1);

        var upgraded =
            state.Player.Deck.Single(card =>
                card.InstanceId == 1);
        Assert.Equal(
            "proto.silent.backflip",
            upgraded.CardId);
        Assert.Equal(1, upgraded.UpgradeLevel);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }

    [Fact]
    public void WarpedMirrorTransformsTwoCardsSequentiallyAndChargesHpOnce()
    {
        var engine = new PrototypeGameEngine();
        var originals = new[]
        {
            Card(1, "proto.silent.strike"),
            Card(2, "proto.silent.defend"),
            Card(3, "proto.silent.neutralize")
        };
        var state = EventState(
            "proto.event.warped_mirror",
            act: 2,
            hp: 60,
            gold: 100,
            deck: originals);

        state = ChooseEvent(
            engine,
            state,
            "reshape");

        Assert.Equal(51, state.Player.Hp);
        Assert.Equal(
            2,
            state.World!.Event!.PendingDeckChoice!
                .RemainingSelections);

        state = ChooseCard(
            engine,
            state,
            1);

        Assert.Equal(51, state.Player.Hp);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(
            1,
            state.World!.Event!.PendingDeckChoice!
                .RemainingSelections);

        state = ChooseCard(
            engine,
            state,
            2);

        Assert.Equal(51, state.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, state.Phase);

        foreach (var id in new long[] { 1, 2 })
        {
            var before = originals.Single(card =>
                card.InstanceId == id);
            var after = state.Player.Deck.Single(card =>
                card.InstanceId == id);
            Assert.NotEqual(before.CardId, after.CardId);
            Assert.Contains(
                after.CardId,
                PrototypeContent.RewardCardPool);
            Assert.Equal(0, after.UpgradeLevel);
        }

        Assert.Equal(
            "proto.silent.neutralize",
            state.Player.Deck.Single(card =>
                card.InstanceId == 3).CardId);
    }

    [Fact]
    public void WarpedMirrorPolishTransformsAndUpgradesForGold()
    {
        var engine = new PrototypeGameEngine();
        var state = EventState(
            "proto.event.warped_mirror",
            act: 2,
            hp: 60,
            gold: 40,
            deck:
            [
                Card(1, "proto.silent.strike")
            ]);

        state = ChooseEvent(
            engine,
            state,
            "polish");
        Assert.Equal(5, state.Player.Gold);

        state = ChooseCard(
            engine,
            state,
            1);

        var transformed =
            Assert.Single(state.Player.Deck);
        Assert.NotEqual(
            "proto.silent.strike",
            transformed.CardId);
        Assert.Equal(1, transformed.UpgradeLevel);
        Assert.Equal(1, transformed.InstanceId);
    }

    private static RunState ChooseEvent(
        PrototypeGameEngine engine,
        RunState state,
        string choiceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "event_choice"
                && item.ReadPayload<EventChoicePayload>()
                    .ChoiceId == choiceId);
        return engine.Step(state, action).State;
    }

    private static RunState ChooseCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind
                    == "choose_event_deck_card"
                && item.ReadPayload<
                    ChooseEventDeckCardPayload>()
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

    private static RunState EventState(
        string eventId,
        int act,
        int hp,
        int gold,
        CardInstance[] deck)
    {
        var floor = 2;
        var eventNode = "event";
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            hp,
            70,
            gold,
            deck,
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "event-deck-choice",
            "event-deck-choice",
            0,
            RunPhase.Event,
            player,
            PrototypeRng.CreateBundle(
                "event-deck-choice"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                act,
                floor,
                deck.Select(card => card.InstanceId)
                    .DefaultIfEmpty(0)
                    .Max() + 1,
                PrototypeRoomType.Event,
                ValidActMap(act, eventNode),
                null,
                null,
                null,
                new Sts2Emulator.Core.EventState(
                    eventId),
                null,
                EventHistory: [eventId]));
    }

    private static MapState ValidActMap(
        int act,
        string currentNodeId) =>
        new(
            [
                new MapNodeState(
                    "entry",
                    act,
                    1,
                    PrototypeRoomType.Combat,
                    ["event"]),
                new MapNodeState(
                    "event",
                    act,
                    2,
                    PrototypeRoomType.Event,
                    ["floor3"]),
                new MapNodeState(
                    "floor3",
                    act,
                    3,
                    PrototypeRoomType.Combat,
                    ["floor4"]),
                new MapNodeState(
                    "floor4",
                    act,
                    4,
                    PrototypeRoomType.Combat,
                    ["rest"]),
                new MapNodeState(
                    "rest",
                    act,
                    5,
                    PrototypeRoomType.Rest,
                    ["boss"]),
                new MapNodeState(
                    "boss",
                    act,
                    6,
                    PrototypeRoomType.Boss,
                    [])
            ],
            CurrentNodeId: currentNodeId,
            EntryNodeIds: ["entry"]);
}
