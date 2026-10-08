using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeOvergrowthEventCatalogTests
{
    [Fact]
    public void CatalogContainsEveryPinnedOvergrowthRegionEventOnce()
    {
        Assert.Equal(13, PrototypeNativeOvergrowthEvents.RegionEventIds.Length);
        Assert.Equal(13, PrototypeNativeOvergrowthEvents.Definitions.Length);
        Assert.Equal(13, PrototypeNativeOvergrowthEvents.RegionEventIds
            .Distinct(StringComparer.Ordinal).Count());

        foreach (var id in PrototypeNativeOvergrowthEvents.RegionEventIds)
        {
            var definition = PrototypeContent.Event(id);
            Assert.True(PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(id));
            Assert.Equal(1, definition.MinAct);
            Assert.Equal(1, definition.MaxAct);
            Assert.NotEmpty(definition.Choices);
            Assert.True(definition.OncePerRun);
        }

        Assert.False(PrototypeNativeOvergrowthEvents.IsNativeRegionEvent(
            "proto.event.fountain"));
    }

    [Fact]
    public void NativeEligibilityUsesSourceBackedPlayerConditions()
    {
        var initial = PrototypeNativeOvergrowthRunFactory.Create("native-events");
        var player = initial.Player;

        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(
            PrototypeContent.Event("proto.native.event.unrest_site"),
            player));
        Assert.True(PrototypeNativeOvergrowthEvents.IsEligible(
            PrototypeContent.Event("proto.native.event.unrest_site"),
            player with { Hp = 45 }));

        Assert.True(PrototypeNativeOvergrowthEvents.IsEligible(
            PrototypeContent.Event("proto.native.event.morphic_grove"),
            player with { Gold = 150 }));
        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(
            PrototypeContent.Event("proto.native.event.morphic_grove"),
            player with { Gold = 99 }));
        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(
            PrototypeContent.Event("proto.native.event.whispering_hollow"),
            player with { Gold = 43 }));
        Assert.True(PrototypeNativeOvergrowthEvents.IsEligible(
            PrototypeContent.Event("proto.native.event.whispering_hollow"),
            player with { Gold = 44 }));
    }

    [Fact]
    public void NativeMapEventEntriesNeverSelectSyntheticEventIdentity()
    {
        var engine = new PrototypeGameEngine();
        var found = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var initial = PrototypeNativeOvergrowthRunFactory.Create(
                $"native-event-selection-{seed}");
            var map = initial.World!.Map;
            var unknown = map.Nodes.First(node =>
                node.RoomType == PrototypeRoomType.Unknown);
            var predecessor = map.Nodes.First(node =>
                node.NextNodeIds?.Contains(
                    unknown.NodeId, StringComparer.Ordinal) == true);
            var ready = initial with
            {
                Phase = RunPhase.MapChoice,
                World = initial.World with
                {
                    Event = null,
                    Floor = predecessor.Floor,
                    Map = map with { CurrentNodeId = predecessor.NodeId }
                }
            };
            var entered = engine.Step(ready, GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload(unknown.NodeId))).State;
            if (entered.Phase != RunPhase.Event)
            {
                continue;
            }

            found++;
            var eventId = entered.World!.Event!.EventId;
            Assert.Contains(eventId,
                PrototypeNativeOvergrowthEvents.RegionEventIds);
            Assert.DoesNotContain(eventId,
                new[] { "proto.event.cache", "proto.event.fountain" });
        }

        Assert.True(found >= 3,
            $"Expected multiple native region events, encountered {found}.");
    }

    [Fact]
    public void NativeEventGeneratedCardsAreNotNormalRewardCards()
    {
        foreach (var id in new[]
        {
            "proto.native.event.byrdonis_egg",
            "proto.native.event.spore_mind",
            "proto.native.event.poor_sleep",
            "proto.native.event.guilty"
        })
        {
            Assert.True(PrototypeContent.Cards.ContainsKey(id));
            Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
        }
    }
    [Fact]
    public void MorphicGroveRequiresTwoActuallyTransformableCards()
    {
        var player = PrototypeNativeOvergrowthRunFactory.Create(
            "morphic-eligibility").Player;
        var evt = PrototypeContent.Event("proto.native.event.morphic_grove");
        var empty = PrototypeJson.EmptyObject();
        var valid = new CardInstance(12345, "proto.silent.strike", 0, empty);
        var eternal = new CardInstance(12346, "proto.native.neow.greed", 0, empty);
        var egg = new CardInstance(12347, "proto.native.event.byrdonis_egg", 0, empty);
        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(
            evt, player with { Gold = 100, Deck = [valid, eternal] }));
        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(
            evt, player with { Gold = 100, Deck = [eternal, egg] }));
        Assert.True(PrototypeNativeOvergrowthEvents.IsEligible(
            evt, player with { Gold = 100, Deck = [valid, egg] }));
        Assert.True(PrototypeNativeOvergrowthEvents.IsEligible(
            evt, player with { Gold = 100, Deck = [valid, valid with { InstanceId = 12348 }] }));
    }

}
