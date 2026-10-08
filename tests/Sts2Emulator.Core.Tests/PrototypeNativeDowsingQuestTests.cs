using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeDowsingQuestTests
{
    private static RunState AcquireDowsing()
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            "native-dowsing-quest");
        var world = state.World!;
        var options = world.Event! with
        {
            OfferedChoiceIds =
            [
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("ArcaneScroll"),
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("BoomingConch"),
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId("DowsingRod")
            ]
        };
        state = state with { World = world with { Event = options } };
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(option =>
            option.ReadPayload<EventChoicePayload>().ChoiceId
                == "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "DowsingRod"));
        state = engine.Step(state, action).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        return state;
    }

    private static RunState BeforeUnknownNode(
        RunState state, MapNodeState unknown, int roomsEntered)
    {
        var world = state.World!;
        var map = world.Map;
        var path = new List<MapNodeState>();
        var cursor = unknown;
        while (cursor.Floor > 1)
        {
            var id = cursor.NodeId;
            cursor = map.Nodes.First(node =>
                node.Floor == unknown.Floor - path.Count - 1
                && node.NextNodeIds?.Contains(id, StringComparer.Ordinal)
                    == true);
            path.Add(cursor);
        }

        path.Reverse();
        var predecessor = path[^1];
        var completed = path.Select(node =>
            new PrototypeCompletedRoomRecord(
                1,
                node.Floor,
                node.NodeId,
                node.RoomType == PrototypeRoomType.Unknown
                    ? PrototypeRoomType.Event
                    : node.RoomType)).ToArray();
        var deck = state.Player.Deck.Select(card =>
            card.CardId == "proto.native.neow.dowsing"
                ? card with
                {
                    PersistentState = JsonSerializer.SerializeToElement(
                        new { RoomsEntered = roomsEntered })
                }
                : card).ToArray();

        return state with
        {
            Phase = RunPhase.MapChoice,
            Player = state.Player with { Deck = deck },
            World = world with
            {
                Floor = predecessor.Floor,
                ActiveRoom = null,
                Event = null,
                CompletedRoomHistory = completed,
                Map = map with
                {
                    CurrentNodeId = predecessor.NodeId
                }
            }
        };
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(3, 4, false)]
    [InlineData(4, 5, true)]
    public void DowsingProgressesOnVisibleQuestionMarkNodes(
        int initialProgress, int visited, bool shouldTransform)
    {
        var state = AcquireDowsing();
        var unknown = state.World!.Map.Nodes.First(node =>
            node.RoomType == PrototypeRoomType.Unknown
            && node.Floor >= 2);
        state = BeforeUnknownNode(state, unknown, initialProgress);
        PrototypeStateInvariants.Validate(state);

        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(candidate =>
            candidate.Kind == "choose_map_node"
            && candidate.ReadPayload<ChooseMapNodePayload>()
                .NodeId == unknown.NodeId);
        state = engine.Step(state, action).State;
        var card = Assert.Single(state.Player.Deck,
            candidate => candidate.InstanceId == 14);
        if (shouldTransform)
        {
            Assert.Equal("proto.native.neow.abundance", card.CardId);
            Assert.Equal(JsonValueKind.Object, card.PersistentState.ValueKind);
            Assert.Equal("{}", card.PersistentState.GetRawText());
            Assert.Equal(0, card.UpgradeLevel);
        }
        else
        {
            Assert.Equal("proto.native.neow.dowsing", card.CardId);
            Assert.Equal(visited,
                card.PersistentState.GetProperty("RoomsEntered").GetInt32());
        }

        Assert.Equal(15, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void NormalMapEntryLeavesDowsingQuestProgressUntouched()
    {
        var engine = new PrototypeGameEngine();
        var state = AcquireDowsing();
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        var card = Assert.Single(state.Player.Deck,
            card => card.CardId == "proto.native.neow.dowsing");
        Assert.Equal(JsonValueKind.Object, card.PersistentState.ValueKind);
        Assert.False(card.PersistentState.TryGetProperty(
            "RoomsEntered", out _));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void AbundanceCreatesThreeUpgradedPowersAndRequiresAChoice()
    {
        var engine = new PrototypeGameEngine();
        var state = AcquireDowsing();
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Select(card =>
                    card.CardId == "proto.native.neow.dowsing"
                        ? card with { CardId = "proto.native.neow.abundance" }
                        : card).ToArray()
            }
        };
        state = engine.Step(state, engine.GetLegalActions(state)[0]).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        var combat = state.World!.Combat!;
        var abundance = Assert.Single(combat.Cards, card =>
            card.CardId == "proto.native.neow.abundance");
        var remaining = combat.Cards.Select(card => card.InstanceId)
            .Where(id => id != abundance.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [abundance.InstanceId],
            DrawPile = remaining,
            DiscardPile = [],
            ExhaustPile = [],
            PlayPile = [],
            Energy = 3
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        PrototypeStateInvariants.Validate(state);

        var play = engine.GetLegalActions(state).Single(option =>
            option.Kind == "play_card"
            && option.ReadPayload<PlayCardPayload>().CardInstanceId
                == abundance.InstanceId);
        state = engine.Step(state, play).State;
        var pending = Assert.IsType<PendingCombatChoiceState>(
            state.World!.Combat!.PendingChoice);
        Assert.Equal(PrototypeCardZone.ChoicePool, pending.Selection.SourceZone);
        Assert.Equal(1, pending.Selection.MinSelections);
        Assert.Equal(1, pending.Selection.MaxSelections);
        Assert.Equal(3, pending.CandidateCardInstanceIds.Length);
        Assert.All(pending.CandidateCardInstanceIds, id =>
        {
            var card = state.World.Combat.Cards.Single(item =>
                item.InstanceId == id);
            Assert.Equal(PrototypeCardType.Power,
                PrototypeContent.Card(card.CardId).Type);
            Assert.Equal(1, card.UpgradeLevel);
            Assert.Equal(0, card.TemporaryEnergyCost?.Cost);
        });
        Assert.DoesNotContain(engine.GetLegalActions(state), option =>
            option.Kind == "select_cards"
            && option.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.Length == 0);
        PrototypeStateInvariants.Validate(state);

        var chosen = pending.CandidateCardInstanceIds[0];
        var select = engine.GetLegalActions(state).Single(option =>
            option.Kind == "select_cards"
            && option.ReadPayload<SelectCardsPayload>()
                .CardInstanceIds.SequenceEqual(new[] { chosen }));
        state = engine.Step(state, select).State;
        Assert.Null(state.World!.Combat!.PendingChoice);
        Assert.Contains(chosen, state.World.Combat.Hand);
        Assert.Contains(abundance.InstanceId, state.World.Combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }
}
