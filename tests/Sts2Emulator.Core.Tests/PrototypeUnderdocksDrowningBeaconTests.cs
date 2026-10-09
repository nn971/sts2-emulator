using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksDrowningBeaconTests
{
    private const string EventId =
        "proto.native.underdocks.drowning_beacon";
    private const string PotionId =
        "proto.native.underdocks.glowwater_potion";
    private const string RelicId =
        "proto.native.underdocks.fresnel_lens";

    [Fact]
    public void BottlingOpensAnOptionalNativePotionOffer()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBeacon(engine);
        var initialHash = CanonicalJson.Sha256(initial.Rng);
        var offered = Select(engine, initial, "bottle");
        Assert.Equal(RunPhase.Event, offered.Phase);
        var pending = Assert.IsType<PrototypePendingEventPotionReplacementState>(
            offered.World!.Event!.PendingPotionReplacement);
        Assert.Equal(PotionId, pending.PotionId);
        Assert.Contains("skip_event_potion",
            engine.GetLegalActions(offered).Select(action => action.Kind));
        Assert.All(engine.GetLegalActions(offered).Where(
            action => action.Kind == "take_event_potion"), action =>
                Assert.Contains(
                    action.ReadPayload<ReplaceEventPotionPayload>().Slot,
                    pending.CandidateSlots));

        var skip = engine.Step(offered, engine.GetLegalActions(offered)
            .Single(action => action.Kind == "skip_event_potion")).State;
        Assert.Equal(RunPhase.MapChoice, skip.Phase);
        Assert.Null(skip.World!.Event);
        Assert.DoesNotContain(skip.Player.PotionSlots, potion =>
            potion?.PotionId == PotionId);

        var take = engine.GetLegalActions(offered).Single(action =>
            action.Kind == "take_event_potion");
        var accepted = engine.Step(offered, take).State;
        Assert.Equal(RunPhase.MapChoice, accepted.Phase);
        Assert.Contains(accepted.Player.PotionSlots, potion =>
            potion?.PotionId == PotionId);
        Assert.Equal(initialHash, CanonicalJson.Sha256(accepted.Rng));
        Assert.Equal(CanonicalJson.Sha256(accepted),
            CanonicalJson.Sha256(accepted.Fork()));
    }

    [Fact]
    public void FullPotionBeltOffersReplacementOrSkip()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBeacon(engine);
        var full = initial with
        {
            Player = initial.Player with
            {
                PotionSlots = initial.Player.PotionSlots
                    .Select(_ => new PotionInstance(
                        "proto.potion.block", PrototypeJson.EmptyObject()))
                    .ToArray()
            }
        };
        var pending = Select(engine, full, "bottle");
        Assert.Equal(full.Player.PotionSlots.Length,
            pending.World!.Event!.PendingPotionReplacement!.CandidateSlots.Length);
        Assert.DoesNotContain(engine.GetLegalActions(pending),
            action => action.Kind == "take_event_potion");
        var replace = engine.GetLegalActions(pending).First(action =>
            action.Kind == "replace_event_potion");
        var result = engine.Step(pending, replace).State;
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Single(result.Player.PotionSlots.Where(
            potion => potion?.PotionId == PotionId));
    }

    [Fact]
    public void ClimbLosesThirteenMaxHpThenGrantsFresnelLens()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBeacon(engine);
        var wounded = initial with
        {
            Player = initial.Player with { Hp = initial.Player.MaxHp - 30 }
        };
        var rngHash = CanonicalJson.Sha256(wounded.Rng);
        var result = Select(engine, wounded, "climb");
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(wounded.Player.MaxHp - 13, result.Player.MaxHp);
        Assert.Equal(Math.Min(wounded.Player.Hp, result.Player.MaxHp),
            result.Player.Hp);
        Assert.Single(result.Player.Relics.Where(relic =>
            relic.RelicId == RelicId));
        Assert.Equal(rngHash, CanonicalJson.Sha256(result.Rng));
        Assert.DoesNotContain(result.Player.PotionSlots,
            potion => potion?.PotionId == PotionId);
    }

    [Fact]
    public void FresnelLensEnchantsNewBlockCardAndPreservesExistingCards()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBeacon(engine);
        var climbed = Select(engine, initial, "climb");
        var deckBefore = climbed.Player.Deck;
        var syntheticNextEvent = climbed with
        {
            Phase = RunPhase.Event,
            World = climbed.World! with
            {
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState("proto.event.cache")
            }
        };
        var obtained = Select(engine, syntheticNextEvent, "technique");
        Assert.Equal(deckBefore.Length + 1, obtained.Player.Deck.Length);
        var added = obtained.Player.Deck.Last();
        Assert.Equal("proto.silent.backflip", added.CardId);
        Assert.NotNull(added.Enchantment);
        Assert.Equal(PrototypeCardEnchantmentKind.Nimble,
            added.Enchantment!.Kind);
        Assert.Equal(2, added.Enchantment.Amount);
        Assert.All(deckBefore, original =>
            Assert.Equal(original.Enchantment,
                obtained.Player.Deck.Single(card =>
                    card.InstanceId == original.InstanceId).Enchantment));
        Assert.Equal(CanonicalJson.Sha256(obtained),
            CanonicalJson.Sha256(obtained.Fork()));
    }

    [Fact]
    public void GlowwaterExhaustsEntireHandBeforeDrawingTen()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBeacon(engine);
        var pending = Select(engine, initial, "bottle");
        var take = engine.GetLegalActions(pending).Single(action =>
            action.Kind == "take_event_potion");
        var accepted = engine.Step(pending, take).State;
        Assert.Equal(RunPhase.MapChoice, accepted.Phase);
        var combatNode = new MapNodeState(
            "test-beacon-combat", 1, 1, PrototypeRoomType.Combat,
            ["after"]);
        var nextNode = new MapNodeState(
            "after", 1, 2, PrototypeRoomType.Rest, []);
        var world = accepted.World!;
        accepted = accepted with
        {
            World = world with
            {
                Floor = 0,
                ActiveRoom = null,
                Map = new MapState(
                    [combatNode, nextNode], CurrentNodeId: null,
                    EntryNodeIds: [combatNode.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                CompletedRoomHistory = []
            }
        };
        var combatState = engine.Step(accepted,
            Assert.Single(engine.GetLegalActions(accepted))).State;
        Assert.Equal(RunPhase.Combat, combatState.Phase);
        var combat = combatState.World!.Combat!;
        var handBefore = combat.Hand.ToArray();
        var availableBefore = combat.DrawPile.Length +
            combat.DiscardPile.Length;
        Assert.NotEmpty(handBefore);
        var slot = Array.FindIndex(combatState.Player.PotionSlots,
            potion => potion?.PotionId == PotionId);
        Assert.True(slot >= 0);

        var use = engine.GetLegalActions(combatState).Single(action =>
            action.Kind == "use_potion"
            && action.ReadPayload<UsePotionPayload>().Slot == slot);
        var after = engine.Step(combatState, use).State;
        Assert.Equal(RunPhase.Combat, after.Phase);
        var resulting = after.World!.Combat!;
        Assert.All(handBefore, id =>
            Assert.Contains(id, resulting.ExhaustPile));
        Assert.DoesNotContain(resulting.Hand,
            id => handBefore.Contains(id));
        Assert.Equal(Math.Min(10, availableBefore), resulting.Hand.Length);
        Assert.Null(after.Player.PotionSlots[slot]);
        Assert.Equal(CanonicalJson.Sha256(after),
            CanonicalJson.Sha256(after.Fork()));
    }

    private static RunState Select(
        PrototypeGameEngine engine, RunState state, string choice)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "event_choice"
            && item.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        return engine.Step(state, action).State;
    }

    private static RunState FindBeacon(PrototypeGameEngine engine)
    {
        for (var i = 0; i < 128; i++)
        {
            var state = StartUnderdocksEvent(engine, "beacon-" + i);
            if (state.World!.Event!.EventId == EventId)
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "Drowning Beacon not selected in any test seed.");
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
        return engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
    }
}
