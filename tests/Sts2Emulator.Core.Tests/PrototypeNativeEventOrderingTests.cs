using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeEventOrderingTests
{
    private static RunState EnterEvent(
        string eventId, string seed, int nativeEventGold = 0)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        var opening = state.World!.Event!;
        state = state with
        {
            World = state.World with
            {
                Event = opening with
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
        var takeGold = engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "GoldenPearl"));
        state = engine.Step(state, takeGold).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);

        var map = state.World!.Map;
        var unknown = map.Nodes.First(node =>
            node.RoomType == PrototypeRoomType.Unknown);
        var parent = map.Nodes.First(node =>
            node.NextNodeIds?.Contains(unknown.NodeId,
                StringComparer.Ordinal) == true);
        var completed = Enumerable.Range(1, parent.Floor)
            .Select(floor =>
            {
                var node = floor == parent.Floor
                    ? parent
                    : map.Nodes.First(n => n.Floor == floor);
                return new PrototypeCompletedRoomRecord(
                    1, floor, node.NodeId, node.RoomType);
            }).ToArray();

        state = state with
        {
            Phase = RunPhase.Event,
            World = state.World with
            {
                Floor = unknown.Floor,
                Map = map with { CurrentNodeId = unknown.NodeId },
                ActiveRoom = PrototypeRoomType.Event,
                CompletedRoomHistory = completed,
                Event = new EventState(
                    eventId, NativeEventGold: nativeEventGold),
                EventHistory = state.World.EventIds.Append(eventId).ToArray()
            }
        };
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState Take(RunState state, string choice)
    {
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        state = engine.Step(state, action).State;
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Theory]
    [InlineData(26)]
    [InlineData(44)]
    public void WhisperingHollowGoldOfferChargesStoredNativeRoll(int cost)
    {
        var state = EnterEvent(
            "proto.native.event.whispering_hollow",
            "whisper-gold-" + cost,
            nativeEventGold: cost);
        var startingGold = state.Player.Gold;
        var beforeDeck = state.Player.Deck.ToArray();
        Assert.Null(state.Player.PotionSlots[0]);
        Assert.Null(state.Player.PotionSlots[1]);

        state = Take(state, "gold");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(startingGold - cost, state.Player.Gold);
        Assert.Equal(beforeDeck, state.Player.Deck);
        Assert.Equal(2, state.Player.PotionSlots.Count(p => p is not null));
        Assert.All(state.Player.PotionSlots,
            p => Assert.Contains(p!.PotionId, PrototypeContent.PotionPool));
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void WellspringBatheRemovesCardBeforeAddingGuilty()
    {
        var state = EnterEvent(
            "proto.native.event.wellspring", "wellspring-removal-order");
        var before = state.Player.Deck;
        state = Take(state, "bathe");
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(14, state.World!.NextCardInstanceId);
        Assert.Equal("proto.native.event.guilty",
            state.World.Event!.DeferredCardId);
        Assert.DoesNotContain(state.Player.Deck, card =>
            card.CardId == "proto.native.event.guilty");
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            state.World.Event.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Remove,
            pending.Kind);
        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var engine = new PrototypeGameEngine();
        var remove = engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_event_deck_card"
            && action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == before[0].InstanceId);
        state = engine.Step(state, remove).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(before[0].InstanceId + 13,
            state.Player.Deck[^1].InstanceId);
        Assert.Equal("proto.native.event.guilty",
            state.Player.Deck[^1].CardId);
        Assert.DoesNotContain(state.Player.Deck,
            card => card.InstanceId == before[0].InstanceId);
        Assert.Equal(15, state.World.NextCardInstanceId);
        Assert.Contains(state.World.CompletedRooms,
            room => room.RoomType == PrototypeRoomType.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void WhisperingHollowHugDealsDamageAfterTransformation()
    {
        var state = EnterEvent(
            "proto.native.event.whispering_hollow",
            "whispering-hollow-transform-first");
        var before = state.Player.Deck[0];
        state = Take(state, "hug");
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(70, state.Player.Hp);
        Assert.Equal(9, state.World!.Event!.DeferredHpLoss);
        Assert.Equal(before.CardId, state.Player.Deck[0].CardId);

        var engine = new PrototypeGameEngine();
        var transform = engine.GetLegalActions(state).Single(a =>
            a.Kind == "choose_event_deck_card"
            && a.ReadPayload<ChooseEventDeckCardPayload>().CardInstanceId
                == before.InstanceId);
        state = engine.Step(state, transform).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(61, state.Player.Hp);
        Assert.Equal(70, state.Player.MaxHp);
        Assert.Equal(before.InstanceId, state.Player.Deck[0].InstanceId);
        Assert.NotEqual(before.CardId, state.Player.Deck[0].CardId);
        Assert.Equal(13, state.Player.Deck.Length);
        PrototypeStateInvariants.Validate(state);
    }
}
