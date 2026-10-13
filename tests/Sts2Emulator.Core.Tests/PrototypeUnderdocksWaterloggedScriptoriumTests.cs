using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksWaterloggedScriptoriumTests
{
    private const string EventId =
        "proto.native.underdocks.waterlogged_scriptorium";

    [Fact]
    public void SourceEligibilityAndGoldGatedChoicesAreDistinct()
    {
        var definition = PrototypeContent.Event(EventId);
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "scriptorium-eligibility").Player;
        Assert.False(PrototypeNativeUnderdocksEvents.IsEligible(
            definition, player with { Gold = 54 }));
        Assert.True(PrototypeNativeUnderdocksEvents.IsEligible(
            definition, player with { Gold = 55 }));

        var engine = new PrototypeGameEngine();
        var initial = FindScriptorium(engine);
        Assert.Equal(99, initial.Player.Gold);

        var poor = initial with
        {
            Player = initial.Player with { Gold = 55 }
        };
        Assert.Equal(new[] { "bloody_ink", "tentacle_quill" },
            ChoiceIds(engine, poor));

        var rich = initial with
        {
            Player = initial.Player with { Gold = 99 }
        };
        Assert.Equal(new[]
            {
                "bloody_ink", "tentacle_quill", "prickly_sponge"
            }, ChoiceIds(engine, rich));
        Assert.Equal(10,
            PrototypeNativeUnderdocksEvents.SupportedRegionEventIds.Length);
    }

    [Fact]
    public void BloodyInkGrantsSixMaximumHpAndDoesNotSpendGold()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindScriptorium(engine);
        var wounded = initial with
        {
            Player = initial.Player with
            {
                Hp = 25,
                Gold = 55
            }
        };
        var rngBefore = CanonicalJson.Sha256(wounded.Rng);
        var result = Choose(engine, wounded, "bloody_ink");
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(31, result.Player.Hp);
        Assert.Equal(wounded.Player.MaxHp + 6, result.Player.MaxHp);
        Assert.Equal(55, result.Player.Gold);
        Assert.Equal(rngBefore, CanonicalJson.Sha256(result.Rng));
        Assert.Null(result.World!.Event);
    }

    [Fact]
    public void QuillSpends55AndEnchantsExactlyOneEligibleCard()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindScriptorium(engine);
        var old = initial.Player.Deck.ToDictionary(
            card => card.InstanceId, card => card);
        var rngBefore = CanonicalJson.Sha256(initial.Rng);
        var started = Choose(engine, initial, "tentacle_quill");
        Assert.Equal(initial.Player.Gold - 55, started.Player.Gold);
        Assert.Equal(rngBefore, CanonicalJson.Sha256(started.Rng));
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            started.World!.Event!.PendingDeckChoice);
        Assert.Equal(PrototypeCardEnchantmentKind.Steady,
            pending.EnchantmentKind);
        Assert.Equal(1, pending.RemainingSelections);
        Assert.Equal(old.Keys.Order(), pending.CandidateCardInstanceIds.Order());

        var selected = pending.CandidateCardInstanceIds[0];
        var finished = SelectCard(engine, started, selected);
        Assert.Equal(RunPhase.MapChoice, finished.Phase);
        Assert.Equal(rngBefore, CanonicalJson.Sha256(finished.Rng));
        Assert.Equal(1, finished.Player.Deck.Count(card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Steady));
        Assert.Equal(1, finished.Player.Deck.Single(card =>
            card.InstanceId == selected).Enchantment!.Amount);
        Assert.All(finished.Player.Deck.Where(card =>
            card.InstanceId != selected), card =>
            Assert.Equal(old[card.InstanceId].Enchantment, card.Enchantment));
        Assert.Equal(CanonicalJson.Sha256(finished),
            CanonicalJson.Sha256(finished.Fork()));
    }

    [Fact]
    public void SpongeSpends99AndSelectsTwoDistinctUnenchantedCards()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindScriptorium(engine);
        var rngBefore = CanonicalJson.Sha256(initial.Rng);
        var started = Choose(engine, initial, "prickly_sponge");
        Assert.Equal(initial.Player.Gold - 99, started.Player.Gold);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            started.World!.Event!.PendingDeckChoice);
        Assert.Equal(2, pending.RemainingSelections);

        var first = pending.CandidateCardInstanceIds[0];
        var afterFirst = SelectCard(engine, started, first);
        Assert.Equal(RunPhase.Event, afterFirst.Phase);
        var secondChoice = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            afterFirst.World!.Event!.PendingDeckChoice);
        Assert.Equal(1, secondChoice.RemainingSelections);
        Assert.DoesNotContain(first, secondChoice.CandidateCardInstanceIds);
        var second = secondChoice.CandidateCardInstanceIds[0];
        var finished = SelectCard(engine, afterFirst, second);
        Assert.Equal(RunPhase.MapChoice, finished.Phase);
        Assert.Equal(0, finished.Player.Gold);
        Assert.Equal(2, finished.Player.Deck.Count(card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Steady));
        Assert.Equal(rngBefore, CanonicalJson.Sha256(finished.Rng));
    }

    [Fact]
    public void NoAndOneEligibleCardStillAllowPaidOptions()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindScriptorium(engine);
        var before = initial.Player.Deck;
        var allEnchanted = initial with
        {
            Player = initial.Player with
            {
                Deck = before.Select(card => card with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.Spiral)
                }).ToArray()
            }
        };
        Assert.Contains("tentacle_quill", ChoiceIds(engine, allEnchanted));
        Assert.Contains("prickly_sponge", ChoiceIds(engine, allEnchanted));
        var quill = Choose(engine, allEnchanted, "tentacle_quill");
        Assert.Equal(RunPhase.MapChoice, quill.Phase);
        Assert.Equal(initial.Player.Gold - 55, quill.Player.Gold);
        Assert.All(quill.Player.Deck, card =>
            Assert.Equal(PrototypeCardEnchantmentKind.Spiral,
                card.Enchantment!.Kind));

        var one = allEnchanted with
        {
            Player = allEnchanted.Player with
            {
                Deck = allEnchanted.Player.Deck.Select((card, index) =>
                    index == 0 ? card with { Enchantment = null } : card)
                    .ToArray()
            }
        };
        var sponge = Choose(engine, one, "prickly_sponge");
        Assert.Equal(0, sponge.Player.Gold);
        var only = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            sponge.World!.Event!.PendingDeckChoice);
        Assert.Single(only.CandidateCardInstanceIds);
        Assert.Equal(1, only.RemainingSelections);
        var finished = SelectCard(engine, sponge, only.CandidateCardInstanceIds[0]);
        Assert.Equal(RunPhase.MapChoice, finished.Phase);
        Assert.Single(finished.Player.Deck, card =>
            card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Steady);
    }

    [Fact]
    public void SteadyIsCarriedIntoCombatAndRetainsCardOnTurnEnd()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindScriptorium(engine);
        var started = Choose(engine, initial, "tentacle_quill");
        var selected = started.World!.Event!.PendingDeckChoice!
            .CandidateCardInstanceIds[0];
        var finished = SelectCard(engine, started, selected);

        var node = new MapNodeState(
            "steady-combat", 1, 1, PrototypeRoomType.Combat, ["after"]);
        var after = new MapNodeState(
            "after", 1, 2, PrototypeRoomType.Rest, []);
        var world = finished.World!;
        var mapChoice = finished with
        {
            World = world with
            {
                Floor = 0,
                ActiveRoom = null,
                Map = new MapState(
                    [node, after], CurrentNodeId: null,
                    EntryNodeIds: [node.NodeId],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                CompletedRoomHistory = []
            }
        };
        var entered = engine.Step(mapChoice,
            Assert.Single(engine.GetLegalActions(mapChoice))).State;
        Assert.Equal(RunPhase.Combat, entered.Phase);
        var combat = entered.World!.Combat!;
        var steadyCard = combat.Cards.Single(card =>
            card.PersistentCardInstanceId == selected);
        Assert.Equal(PrototypeCardEnchantmentKind.Steady,
            steadyCard.Enchantment!.Kind);
        var restOfCards = combat.Cards
            .Where(card => card.InstanceId != steadyCard.InstanceId)
            .Select(card => card.InstanceId)
            .ToArray();
        entered = entered with
        {
            World = entered.World with
            {
                Combat = combat with
                {
                    Hand = [steadyCard.InstanceId],
                    DrawPile = restOfCards,
                    DiscardPile = [],
                    ExhaustPile = [],
                    PlayPile = []
                }
            }
        };
        var ended = engine.Step(entered, GameAction.Empty("end_turn")).State;
        Assert.Equal(RunPhase.Combat, ended.Phase);
        Assert.Contains(steadyCard.InstanceId, ended.World!.Combat!.Hand);
        Assert.DoesNotContain(steadyCard.InstanceId,
            ended.World.Combat.DiscardPile);
        Assert.Equal(CanonicalJson.Sha256(ended),
            CanonicalJson.Sha256(ended.Fork()));
    }

    private static string[] ChoiceIds(
        PrototypeGameEngine engine, RunState state) =>
        engine.GetLegalActions(state).Where(action =>
                action.Kind == "event_choice")
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState Choose(
        PrototypeGameEngine engine, RunState state, string choice)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "event_choice"
            && item.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        return engine.Step(state, action).State;
    }

    private static RunState SelectCard(
        PrototypeGameEngine engine, RunState state, long id)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "choose_event_deck_card"
            && item.ReadPayload<ChooseEventDeckCardPayload>().CardInstanceId
                == id);
        return engine.Step(state, action).State;
    }

    private static RunState FindScriptorium(PrototypeGameEngine engine)
    {
        for (var i = 0; i < 256; i++)
        {
            var state = StartUnderdocksEvent(
                engine, "scriptorium-seed-" + i);
            if (state.World!.Event!.EventId == EventId)
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "Waterlogged Scriptorium not selected in any test seed.");
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
