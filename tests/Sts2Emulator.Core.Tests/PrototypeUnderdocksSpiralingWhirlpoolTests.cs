using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksSpiralingWhirlpoolTests
{
    private const string EventId =
        "proto.native.underdocks.spiraling_whirlpool";

    [Fact]
    public void EnchantChoiceOnlyOffersBasicStrikeAndDefendCards()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindWhirlpool(engine);
        var before = initial.Player.Deck.ToDictionary(
            card => card.InstanceId, card => card);
        var rngHash = CanonicalJson.Sha256(initial.Rng);

        var awaitingSelection = Choose(engine, initial, "observe");
        Assert.Equal(RunPhase.Event, awaitingSelection.Phase);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            awaitingSelection.World!.Event!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Enchant, pending.Kind);
        Assert.Equal(PrototypeCardEnchantmentKind.Spiral, pending.EnchantmentKind);
        Assert.Equal(1, pending.RemainingSelections);
        Assert.NotEmpty(pending.CandidateCardInstanceIds);

        var expected = before.Values
            .Where(card => card.CardId is
                "proto.silent.strike" or "proto.silent.defend")
            .Select(card => card.InstanceId)
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(expected, pending.CandidateCardInstanceIds.OrderBy(x => x));
        Assert.All(pending.CandidateCardInstanceIds, id =>
            Assert.Null(before[id].Enchantment));
        Assert.Equal(rngHash, CanonicalJson.Sha256(awaitingSelection.Rng));

        var selectedId = pending.CandidateCardInstanceIds[0];
        var legal = engine.GetLegalActions(awaitingSelection);
        Assert.Equal(expected.Length, legal.Count);
        Assert.All(legal, action =>
            Assert.Equal("choose_event_deck_card", action.Kind));
        var action = legal.Single(item =>
            item.Payload.GetProperty("CardInstanceId").GetInt64() == selectedId);
        var resolved = engine.Step(awaitingSelection, action).State;

        Assert.Equal(RunPhase.MapChoice, resolved.Phase);
        Assert.Null(resolved.World!.Event);
        var enchanted = resolved.Player.Deck.Single(card =>
            card.InstanceId == selectedId);
        Assert.NotNull(enchanted.Enchantment);
        Assert.Equal(PrototypeCardEnchantmentKind.Spiral,
            enchanted.Enchantment!.Kind);
        Assert.Equal(1, enchanted.Enchantment.Amount);
        Assert.All(resolved.Player.Deck.Where(card =>
            card.InstanceId != selectedId), card =>
            Assert.Equal(before[card.InstanceId].Enchantment, card.Enchantment));
        Assert.Equal(rngHash, CanonicalJson.Sha256(resolved.Rng));
        Assert.Equal(CanonicalJson.Sha256(resolved),
            CanonicalJson.Sha256(resolved.Fork()));
    }

    [Fact]
    public void DrinkHealsTruncatedThirtyThreePercentOfMaxHp()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindWhirlpool(engine);
        var wounded = initial with
        {
            Player = initial.Player with
            {
                Hp = 10,
                MaxHp = 55
            }
        };
        var rngHash = CanonicalJson.Sha256(wounded.Rng);
        var result = Choose(engine, wounded, "drink");
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(28, result.Player.Hp);
        Assert.Equal(55, result.Player.MaxHp);
        Assert.Null(result.World!.Event);
        Assert.Equal(rngHash, CanonicalJson.Sha256(result.Rng));

        var nearFull = initial with
        {
            Player = initial.Player with
            {
                Hp = initial.Player.MaxHp - 3
            }
        };
        var capped = Choose(engine, nearFull, "drink");
        Assert.Equal(initial.Player.MaxHp, capped.Player.Hp);
    }

    [Fact]
    public void SpiralRequiresAnUnenchantedBasicStrikeOrDefend()
    {
        var starting = PrototypeNativeUnderdocksRunFactory.Create(
            "spiral-eligibility");
        var evt = PrototypeContent.Event(EventId);
        Assert.True(PrototypeNativeUnderdocksEvents.IsEligible(
            evt, starting.Player));

        foreach (var card in starting.Player.Deck)
        {
            var eligible = card.CardId is
                "proto.silent.strike" or "proto.silent.defend";
            Assert.Equal(eligible,
                PrototypeGameEngine.CanSelectEventDeckCard(
                    card, PrototypePersistentDeckChoiceKind.Enchant,
                    null, PrototypeCardEnchantmentKind.Spiral, true));
        }

        var removed = starting with
        {
            Player = starting.Player with
            {
                Deck = starting.Player.Deck.Where(card =>
                    card.CardId is not
                        ("proto.silent.strike" or "proto.silent.defend"))
                    .ToArray()
            }
        };
        Assert.False(PrototypeNativeUnderdocksEvents.IsEligible(
            evt, removed.Player));

        var allEnchanted = starting with
        {
            Player = starting.Player with
            {
                Deck = starting.Player.Deck.Select(card =>
                    card.CardId is "proto.silent.strike" or "proto.silent.defend"
                        ? card with
                        {
                            Enchantment = new PrototypeCardEnchantment(
                                PrototypeCardEnchantmentKind.Spiral, 1)
                        }
                        : card).ToArray()
            }
        };
        Assert.False(PrototypeNativeUnderdocksEvents.IsEligible(
            evt, allEnchanted.Player));
    }

    [Fact]
    public void RegionEventPoolFiltersWhirlpoolWithoutEligibleCards()
    {
        var engine = new PrototypeGameEngine();
        for (var i = 0; i < 32; i++)
        {
            var state = StartUnderdocksEvent(
                engine, "unavailable-spiral-" + i,
                removeStrikeAndDefend: true);
            Assert.NotEqual(EventId, state.World!.Event!.EventId);
            Assert.Contains(state.World.Event.EventId,
                PrototypeNativeUnderdocksEvents.SupportedRegionEventIds);
        }
    }

    private static RunState Choose(
        PrototypeGameEngine engine, RunState state, string choice)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "event_choice"
            && item.Payload.GetProperty("ChoiceId").GetString() == choice);
        return engine.Step(state, action).State;
    }

    private static RunState FindWhirlpool(PrototypeGameEngine engine)
    {
        for (var i = 0; i < 128; i++)
        {
            var state = StartUnderdocksEvent(engine, "spiral-seed-" + i);
            if (state.World!.Event!.EventId == EventId)
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "Spiraling Whirlpool not found in eligible events.");
    }

    private static RunState StartUnderdocksEvent(
        PrototypeGameEngine engine, string seed,
        bool removeStrikeAndDefend = false)
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
            Player = removeStrikeAndDefend
                ? state.Player with
                {
                    Deck = state.Player.Deck.Where(card =>
                        card.CardId is not
                            ("proto.silent.strike" or "proto.silent.defend"))
                        .ToArray()
                }
                : state.Player,
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
