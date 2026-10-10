using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActEventV3Tests
{
    [Theory]
    [InlineData(PrototypeNativeLaterActEventExpansionV3.SpiritGrafterId, 2)]
    [InlineData(PrototypeNativeLaterActEventExpansionV3.ZenWeaverId, 2)]
    [InlineData(PrototypeNativeLaterActEventExpansionV3.HungryForMushroomsId, 3)]
    public void EventsAreRegisteredForTheirPinnedAct(string id, int act)
    {
        var evt = PrototypeContent.Event(id);
        Assert.Equal(act, evt.MinAct);
        Assert.Equal(act, evt.MaxAct);
        Assert.Equal(0, evt.Weight);
        Assert.Contains(id, PrototypeNativeLaterActEvents.SupportedIds(act));
        Assert.True(PrototypeNativeLaterActEvents.IsSupported(act, id));
    }

    [Fact]
    public void HungryForMushroomsBigGrantsTwentyMaxHpAndFirstHandPenalty()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(3,
            PrototypeNativeLaterActEventExpansionV3.HungryForMushroomsId,
            "big-mushroom-event");
        var before = state.Player;
        var done = engine.Step(state, Event("big_mushroom")).State;
        Assert.Equal(RunPhase.MapChoice, done.Phase);
        Assert.Equal(before.MaxHp + 20, done.Player.MaxHp);
        Assert.Equal(before.Hp + 20, done.Player.Hp);
        Assert.Contains(done.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeLaterActEventExpansionV3.BigMushroomId);
        Assert.Equal(2, PrototypeNativeLaterActEventExpansionV3
            .BigMushroomFirstTurnDrawPenalty(done.Player));
    }

    [Fact]
    public void FragrantMushroomLosesFifteenThenRandomlyUpgradesTwoEligibleCards()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(3,
            PrototypeNativeLaterActEventExpansionV3.HungryForMushroomsId,
            "fragrant-mushroom-event");
        var originalDeck = state.Player.Deck;
        var hp = state.Player.Hp;
        var copy = state.Fork();
        var finished = engine.Step(state, Event("fragrant_mushroom")).State;
        var replay = engine.Step(copy, Event("fragrant_mushroom")).State;
        Assert.Equal(CanonicalJson.Sha256(finished),
            CanonicalJson.Sha256(replay));
        Assert.Equal(hp - 15, finished.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, finished.Phase);
        Assert.Contains(finished.Player.Relics, relic =>
            relic.RelicId == PrototypeNativeLaterActEventExpansionV3.FragrantMushroomId);
        Assert.Equal(2, finished.Player.Deck.Count(card =>
            originalDeck.Single(before => before.InstanceId == card.InstanceId)
                .UpgradeLevel < card.UpgradeLevel));
    }

    [Fact]
    public void SpiritGrafterAcceptHealsAndAddsPlayableMetamorphosis()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2,
            PrototypeNativeLaterActEventExpansionV3.SpiritGrafterId,
            "spirit-let");
        state = state with
        {
            Player = state.Player with { Hp = 30 }
        };
        var before = state.Player.Deck.Length;
        var done = engine.Step(state, Event("let_it_in")).State;
        Assert.Equal(RunPhase.MapChoice, done.Phase);
        Assert.Equal(Math.Min(state.Player.MaxHp, 55), done.Player.Hp);
        Assert.Equal(before + 1, done.Player.Deck.Length);
        Assert.Equal(PrototypeNativeLaterActEventExpansionV3.MetamorphosisId,
            done.Player.Deck[^1].CardId);
        var card = PrototypeContent.Card(
            PrototypeNativeLaterActEventExpansionV3.MetamorphosisId);
        Assert.True(card.ExhaustOnUse);
        Assert.True(card.MechanicsImplemented);
        Assert.Equal(3, card.Effects[0].AmountAt(0, 0));
        Assert.Equal(5, card.Effects[0].AmountAt(1, 0));
    }

    [Fact]
    public void SpiritGrafterRejectionSelectsOneDeckCardBeforeTenHpLoss()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2,
            PrototypeNativeLaterActEventExpansionV3.SpiritGrafterId,
            "spirit-reject");
        var hp = state.Player.Hp;
        var choosing = engine.Step(state, Event("rejection")).State;
        Assert.Equal(RunPhase.Event, choosing.Phase);
        Assert.NotNull(choosing.World!.Event!.PendingDeckChoice);
        Assert.Equal(hp, choosing.Player.Hp);
        var option = engine.GetLegalActions(choosing).First();
        Assert.Equal("choose_event_deck_card", option.Kind);
        var selectedId = option.ReadPayload<ChooseEventDeckCardPayload>()
            .CardInstanceId;
        var before = choosing.Player.Deck.Single(c =>
            c.InstanceId == selectedId);
        var done = engine.Step(choosing, option).State;
        Assert.Equal(RunPhase.MapChoice, done.Phase);
        Assert.Equal(hp - 10, done.Player.Hp);
        Assert.Equal(before.UpgradeLevel + 1,
            done.Player.Deck.Single(c => c.InstanceId == selectedId)
                .UpgradeLevel);
    }

    [Fact]
    public void ZenWeaverIsEligibleAt125AndPricesFilterVisibleChoices()
    {
        var cheap = new PlayerState(70, 80, 124, [], [],
            new PotionInstance?[2]);
        Assert.False(PrototypeNativeLaterActEventExpansionV3.IsEligible(
            PrototypeNativeLaterActEventExpansionV3.ZenWeaverId, cheap));
        Assert.True(PrototypeNativeLaterActEventExpansionV3.IsEligible(
            PrototypeNativeLaterActEventExpansionV3.ZenWeaverId,
            cheap with { Gold = 125 }));
        var engine = new PrototypeGameEngine();
        var state = Enter(2,
            PrototypeNativeLaterActEventExpansionV3.ZenWeaverId,
            "zen-weaver-price");
        state = state with
        {
            Player = state.Player with { Gold = 125 }
        };
        Assert.Equal(new[] { "breathing_techniques", "emotional_awareness" },
            ChoiceIds(engine, state));
        Assert.Throws<InvalidOperationException>(() =>
            engine.Step(state, Event("arachnid_acupuncture")));
        state = state with
        {
            Player = state.Player with { Gold = 250 }
        };
        Assert.Equal(new[] { "breathing_techniques",
            "emotional_awareness", "arachnid_acupuncture" },
            ChoiceIds(engine, state));
    }

    [Fact]
    public void ZenBreathingSpendsFiftyForTwoEnlightenments()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2,
            PrototypeNativeLaterActEventExpansionV3.ZenWeaverId,
            "zen-two-cards");
        state = state with
        {
            Player = state.Player with { Gold = 250 }
        };
        var done = engine.Step(state, Event("breathing_techniques")).State;
        Assert.Equal(RunPhase.MapChoice, done.Phase);
        Assert.Equal(200, done.Player.Gold);
        Assert.Equal(state.Player.Deck.Length + 2, done.Player.Deck.Length);
        Assert.All(done.Player.Deck.TakeLast(2), card =>
            Assert.Equal(PrototypeNativeLaterActEventExpansionV3
                .EnlightenmentId, card.CardId));
        Assert.True(PrototypeContent.Card(
            PrototypeNativeLaterActEventExpansionV3.EnlightenmentId).ExhaustOnUse);
    }

    [Theory]
    [InlineData("emotional_awareness", 125, 1)]
    [InlineData("arachnid_acupuncture", 250, 2)]
    public void ZenRemovalPromptsForExactlyThePaidNumberOfCards(
        string optionId, int goldCost, int cardsToRemove)
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2,
            PrototypeNativeLaterActEventExpansionV3.ZenWeaverId,
            "zen-removal-" + optionId);
        state = state with
        {
            Player = state.Player with { Gold = 300 }
        };
        var before = state.Player.Deck.Length;
        var choosing = engine.Step(state, Event(optionId)).State;
        Assert.Equal(300 - goldCost, choosing.Player.Gold);
        Assert.NotNull(choosing.World!.Event!.PendingDeckChoice);
        for (var n = 0; n < cardsToRemove; n++)
        {
            Assert.Equal(RunPhase.Event, choosing.Phase);
            var action = engine.GetLegalActions(choosing).First();
            Assert.Equal("choose_event_deck_card", action.Kind);
            choosing = engine.Step(choosing, action).State;
        }
        Assert.Equal(RunPhase.MapChoice, choosing.Phase);
        Assert.Equal(before - cardsToRemove, choosing.Player.Deck.Length);
        Assert.Null(choosing.World!.Event);
    }

    [Theory]
    [InlineData(PrototypeNativeLaterActEventExpansionV3.MetamorphosisId, 3)]
    [InlineData(PrototypeNativeLaterActEventExpansionV3.EnlightenmentId, 0)]
    public void EventCardsAreTaggedAndCombatMechanicsRegistered(
        string id, int generatedCount)
    {
        var card = PrototypeContent.Card(id);
        Assert.True(card.ExhaustOnUse);
        Assert.Equal(PrototypeCardRarity.Event, card.Rarity);
        Assert.False(card.RewardEligible);
        Assert.Equal(PrototypeCardType.Skill, card.Type);
        if (generatedCount > 0)
            Assert.Equal(generatedCount, Assert.Single(card.Effects).Amount);
        else
            Assert.Equal(PrototypeCombatEffectKind.SetHandCardsEnergyCostMaxOne,
                Assert.Single(card.Effects).Kind);
    }

    private static string[] ChoiceIds(PrototypeGameEngine engine,
        RunState state) => engine.GetLegalActions(state).Select(action =>
            action.ReadPayload<EventChoicePayload>().ChoiceId).ToArray();

    private static GameAction Event(string id) =>
        GameAction.Create("event_choice", new EventChoicePayload(id));

    private static RunState Enter(int act, string eventId, string seed)
    {
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create(seed);
        state = engine.Step(state, GameAction.Empty("start_run")).State;
        for (var next = 2; next <= act; next++)
        {
            state = engine.Step(state with
            {
                Phase = RunPhase.ActTransition
            }, GameAction.Empty("continue_act")).State;
        }
        var map = state.World!.Map;
        var entry = map.Nodes.Single(node =>
            node.NodeId == map.EntryNodeIds![0]);
        var target = entry.NextNodeIds![0];
        map = map with
        {
            CurrentNodeId = entry.NodeId,
            Nodes = map.Nodes.Select(node => node.NodeId == target
                ? node with { RoomType = PrototypeRoomType.Event }
                : node).ToArray()
        };
        state = state with
        {
            World = state.World with { Map = map, Floor = 1 }
        };
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(target))).State;
        return state with
        {
            World = state.World! with
            {
                Event = new EventState(eventId)
            }
        };
    }
}
