using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeUnderdocksEventsTests
{
    [Fact]
    public void NativeRosterIsDistinctAndSupportedSubsetUsesRealEffects()
    {
        Assert.Equal(10, PrototypeNativeUnderdocksEvents.NativeRegionEventIds.Length);
        Assert.Equal(10, PrototypeNativeUnderdocksEvents.NativeRegionEventIds
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, PrototypeNativeUnderdocksEvents.SupportedRegionEventIds.Length);
        Assert.All(PrototypeNativeUnderdocksEvents.SupportedRegionEventIds, id =>
        {
            Assert.Contains(id, PrototypeNativeUnderdocksEvents.NativeRegionEventIds);
            Assert.NotEmpty(PrototypeContent.Event(id).Choices);
            Assert.DoesNotContain(id, PrototypeNativeOvergrowthEvents.RegionEventIds);
        });

        var doors = PrototypeContent.Event(
            "proto.native.underdocks.doors_of_light_and_dark");
        Assert.Equal(2, doors.Choices.Length);
        Assert.Equal(2, doors.Choices[0].Effects.Length);
        Assert.All(doors.Choices[0].Effects, effect =>
            Assert.Equal(PrototypeRunEffectKind.UpgradeRandomCard, effect.Kind));
        Assert.Equal(PrototypePersistentDeckChoiceKind.Remove,
            doors.Choices[1].DeckChoice!.Kind);
        Assert.Equal(1, doors.Choices[1].DeckChoice!.Selections);
        var greed = PrototypeContent.Card("proto.native.underdocks.greed");
        Assert.Equal(PrototypeCardRarity.Curse, greed.Rarity);
        Assert.True(greed.Unplayable);
        Assert.False(greed.RewardEligible);
    }

    [Fact]
    public void TreasuryPersistsBothRollsAndChoicesSettleOneRolledAmount()
    {
        var engine = new PrototypeGameEngine();
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var state = StartUnderdocksEvent(engine,
                "treasury-seed-" + iteration);
            if (state.World!.Event!.EventId
                != "proto.native.underdocks.sunken_treasury")
            {
                continue;
            }
            var evt = state.World.Event;
            Assert.InRange(evt.NativeEventGold, 52, 67);
            Assert.InRange(evt.NativeEventSecondaryGold, 303, 363);
            var originalGold = state.Player.Gold;
            var first = Select(engine, state, "first_chest");
            var second = Select(engine, state, "second_chest");
            Assert.Equal(originalGold + evt.NativeEventGold,
                first.Player.Gold);
            Assert.Equal(originalGold + evt.NativeEventSecondaryGold,
                second.Player.Gold);
            Assert.DoesNotContain(first.Player.Deck, card =>
                card.CardId == "proto.native.underdocks.greed");
            Assert.Single(second.Player.Deck, card =>
                card.CardId == "proto.native.underdocks.greed");
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(state.Fork()));
            return;
        }
        throw new InvalidOperationException("Treasury never drawn.");
    }

    [Fact]
    public void LightUpgradesTwoDistinctCardsAndDarkOffersRemoval()
    {
        var engine = new PrototypeGameEngine();
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var state = StartUnderdocksEvent(engine,
                "doors-seed-" + iteration);
            if (state.World!.Event!.EventId
                != "proto.native.underdocks.doors_of_light_and_dark")
            {
                continue;
            }
            var original = state.Player.Deck.ToDictionary(
                card => card.InstanceId, card => card.UpgradeLevel);
            var light = Select(engine, state, "light");
            var upgraded = light.Player.Deck.Count(card =>
                card.UpgradeLevel > original[card.InstanceId]);
            Assert.Equal(2, upgraded);

            var dark = Select(engine, state, "dark");
            Assert.NotNull(dark.World!.Event?.PendingDeckChoice);
            Assert.Equal(PrototypePersistentDeckChoiceKind.Remove,
                dark.World.Event!.PendingDeckChoice!.Kind);
            return;
        }
        throw new InvalidOperationException("Doors never drawn.");
    }

    [Fact]
    public void StatueGoldRollIsPersistedAndSwordIsAnAlternativeReward()
    {
        var engine = new PrototypeGameEngine();
        for (var i = 0; i < 32; i++)
        {
            var state = StartUnderdocksEvent(engine, "statue-" + i);
            if (state.World!.Event!.EventId
                != "proto.native.underdocks.sunken_statue")
            {
                continue;
            }

            var original = state.Player;
            var rolled = state.World.Event.NativeEventGold;
            Assert.InRange(rolled, 101, 121);
            var sword = Select(engine, state, "sword");
            Assert.Equal(original.Hp, sword.Player.Hp);
            Assert.Equal(original.Gold, sword.Player.Gold);
            Assert.Contains(sword.Player.Relics, relic =>
                relic.RelicId == "proto.native.event.sword_of_stone");

            var dive = Select(engine, state, "dive");
            Assert.Equal(original.Gold + rolled, dive.Player.Gold);
            Assert.Equal(original.Hp - 7, dive.Player.Hp);
            Assert.DoesNotContain(dive.Player.Relics, relic =>
                relic.RelicId == "proto.native.event.sword_of_stone");
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(state.Fork()));
            return;
        }

        throw new InvalidOperationException("Sunken Statue was never selected.");
    }

    [Fact]
    public void EventRoomsNeverSelectAnotherRegionsInventory()
    {
        var engine = new PrototypeGameEngine();
        for (var i = 0; i < 20; i++)
        {
            var state = StartUnderdocksEvent(engine, "underdocks-event-" + i);
            Assert.Contains(state.World!.Event!.EventId,
                PrototypeNativeUnderdocksEvents.SupportedRegionEventIds);
        }
    }

    private static RunState Select(
        PrototypeGameEngine engine, RunState state, string choice)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "event_choice"
            && item.Payload.GetProperty("ChoiceId").GetString() == choice);
        return engine.Step(state, action).State;
    }

    private static RunState StartUnderdocksEvent(
        PrototypeGameEngine engine, string seed)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(seed);
        var node = new MapNodeState(
            "test-underdocks-event", 1, 1,
            PrototypeRoomType.Event, ["next-room"]);
        var next = new MapNodeState(
            "next-room", 1, 2, PrototypeRoomType.Combat, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = 0,
                ActiveRoom = null,
                Map = new MapState(
                    [node, next], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Event = null,
                Combat = null,
                Reward = null
            }
        };
        return engine.Step(state, Assert.Single(
            engine.GetLegalActions(state))).State;
    }
}
