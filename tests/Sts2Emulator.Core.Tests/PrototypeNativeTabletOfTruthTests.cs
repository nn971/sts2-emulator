using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeTabletOfTruthTests
{
    private static RunState EnterTablet(string seed)
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
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "GoldenPearl"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "BoomingConch"),
                        "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                            "CursedPearl")
                    ]
                }
            }
        };
        var neow = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "GoldenPearl"));
        state = engine.Step(state, neow).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);

        var map = state.World!.Map;
        // Native Overgrowth hides event destinations behind '?'
        // nodes, rather than placing visible Event tiles.
        var node = map.Nodes.First(item =>
            item.RoomType == PrototypeRoomType.Unknown);
        var parent = map.Nodes.First(item =>
            item.NextNodeIds?.Contains(node.NodeId,
                StringComparer.Ordinal) == true);
        var completed = Enumerable.Range(1, parent.Floor)
            .Select(floor =>
            {
                var room = floor == parent.Floor
                    ? parent
                    : map.Nodes.First(n => n.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, room.NodeId, room.RoomType);
            }).ToArray();
        // Force the random Unknown destination to resolve to this
        // specific native event, without replacing its map node type.
        // This isolates the event-page state machine from '?' RNG.
        state = state with
        {
            Phase = RunPhase.Event,
            World = state.World with
            {
                Floor = node.Floor,
                ActiveRoom = PrototypeRoomType.Event,
                CompletedRoomHistory = completed,
                Map = map with { CurrentNodeId = node.NodeId },
                Event = new EventState(PrototypeNativeTabletOfTruth.EventId),
                EventHistory = state.World.EventIds.Append(
                    PrototypeNativeTabletOfTruth.EventId).ToArray()
            }
        };
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState Choose(
        PrototypeGameEngine engine,
        RunState state,
        string choiceId)
    {
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choiceId);
        state = engine.Step(state, action).State;
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void FiveDeciphersApplyNativeEscalatingCostsAndFinalUpgradeAll()
    {
        var state = EnterTablet("tablet-five-pages");
        var engine = new PrototypeGameEngine();
        var expectedMaxHp = new[] { 67, 61, 49, 25, 1 };
        var deckIds = state.Player.Deck.Select(card =>
            card.InstanceId).ToArray();
        var originalFloor = state.World!.Floor;

        for (var page = 0; page < 5; page++)
        {
            var actions = engine.GetLegalActions(state);
            Assert.Contains(actions, a =>
                a.ReadPayload<EventChoicePayload>().ChoiceId == "decipher");
            if (page == 0)
            {
                Assert.Contains(actions, a =>
                    a.ReadPayload<EventChoicePayload>().ChoiceId == "smash");
            }
            else
            {
                Assert.Equal(2, actions.Count);
                Assert.Contains(actions, a =>
                    a.ReadPayload<EventChoicePayload>().ChoiceId == "give_up");
                Assert.DoesNotContain(actions, a =>
                    a.ReadPayload<EventChoicePayload>().ChoiceId == "smash");
            }

            state = Choose(engine, state, "decipher");
            Assert.Equal(expectedMaxHp[page], state.Player.MaxHp);
            Assert.Equal(expectedMaxHp[page], state.Player.Hp);
            Assert.Equal(deckIds, state.Player.Deck
                .Select(card => card.InstanceId));
            if (page < 4)
            {
                Assert.Equal(RunPhase.Event, state.Phase);
                Assert.Equal(page + 1,
                    state.World!.Event!.NativePageIndex);
                Assert.Equal(page + 1, state.Player.Deck.Count(
                    card => card.UpgradeLevel == 1));
                Assert.Equal(originalFloor, state.World.Floor);
            }
        }

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Equal(13, state.Player.Deck.Count(card =>
            card.UpgradeLevel == 1));
        Assert.Contains(state.World.CompletedRooms, room =>
            room.RoomType == PrototypeRoomType.Event
            && room.Floor == originalFloor);
    }

    [Theory]
    [InlineData(1, 67)]
    [InlineData(2, 61)]
    [InlineData(3, 49)]
    [InlineData(4, 25)]
    public void GiveUpEndsTabletAfterAnyIntermediatePage(
        int decipherCount, int expectedMaxHp)
    {
        var state = EnterTablet("tablet-give-up-" + decipherCount);
        var engine = new PrototypeGameEngine();
        for (var i = 0; i < decipherCount; i++)
        {
            state = Choose(engine, state, "decipher");
        }

        var before = state.Player.Deck.ToArray();
        state = Choose(engine, state, "give_up");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Equal(expectedMaxHp, state.Player.MaxHp);
        Assert.Equal(before, state.Player.Deck);
        Assert.Contains(state.World.CompletedRooms, room =>
            room.RoomType == PrototypeRoomType.Event);
    }

    [Fact]
    public void SmashHealsTwentyAndEndsWithoutAnUpgrade()
    {
        var state = EnterTablet("tablet-smash");
        state = state with
        {
            Player = state.Player with { Hp = 12 }
        };
        PrototypeStateInvariants.Validate(state);
        var engine = new PrototypeGameEngine();
        var originalDeck = state.Player.Deck;
        state = Choose(engine, state, "smash");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(32, state.Player.Hp);
        Assert.Equal(70, state.Player.MaxHp);
        Assert.Equal(originalDeck, state.Player.Deck);
    }

    [Fact]
    public void CannotSmashAgainOnAFollowupPage()
    {
        var engine = new PrototypeGameEngine();
        var state = EnterTablet("tablet-invalid-page");
        state = Choose(engine, state, "decipher");
        Assert.Throws<InvalidOperationException>(() => engine.Step(
            state, GameAction.Create("event_choice",
                new EventChoicePayload("smash"))));
    }
}
