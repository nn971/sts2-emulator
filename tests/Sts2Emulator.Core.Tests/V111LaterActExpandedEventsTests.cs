using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActExpandedEventsTests
{
    [Theory]
    [InlineData(2, PrototypeNativeLaterActEventExpansion.LostWispEventId)]
    [InlineData(2, PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId)]
    [InlineData(3, PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId)]
    public void EventDefinitionsAreSourceActGated(int act, string id)
    {
        var definition = PrototypeContent.Event(id);
        Assert.Equal((act, act, 0),
            (definition.MinAct, definition.MaxAct, definition.Weight));
        Assert.Contains(id, PrototypeNativeLaterActEvents.SupportedIds(act));
        Assert.Equal(3, PrototypeNativeLaterActEventExpansion.Relics.Length);
        Assert.False(PrototypeNativeLaterActEventExpansion.IsEligible(
            PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId,
            new PlayerState(18, 80, 0, [], [],
                new PotionInstance?[2])));
        Assert.False(PrototypeNativeLaterActEventExpansion.IsEligible(
            PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId,
            new PlayerState(11, 80, 0, [], [],
                new PotionInstance?[2])));
    }

    [Fact]
    public void LostWispClaimAddsDecayAndRelicWhileSearchPaysRolledGold()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2, PrototypeNativeLaterActEventExpansion.LostWispEventId,
            "lost-wisp-claim");
        state = state with
        {
            World = state.World! with
            {
                Event = new EventState(
                    PrototypeNativeLaterActEventExpansion.LostWispEventId,
                    NativeEventGold: 72)
            }
        };
        var originalCount = state.Player.Deck.Length;
        var claim = engine.Step(state, Event("claim")).State;
        Assert.Equal(RunPhase.MapChoice, claim.Phase);
        Assert.Equal(originalCount + 1, claim.Player.Deck.Length);
        Assert.Equal(PrototypeNativeLaterActEventExpansion.DecayId,
            claim.Player.Deck[^1].CardId);
        Assert.Contains(claim.Player.Relics, relic => relic.RelicId ==
            PrototypeNativeLaterActEventExpansion.LostWispRelicId);
        Assert.Null(claim.World!.Event);
        Assert.Single(claim.World.CompletedRooms);
        Assert.Equal(2, PrototypeContent.Card(
            PrototypeNativeLaterActEventExpansion.DecayId).EndTurnDamageIfInHand);
        var search = engine.Step(state, Event("search")).State;
        Assert.Equal(state.Player.Gold + 72, search.Player.Gold);
        Assert.Equal(originalCount, search.Player.Deck.Length);
        Assert.DoesNotContain(search.Player.Relics,
            relic => relic.RelicId ==
                PrototypeNativeLaterActEventExpansion.LostWispRelicId);
    }

    [Fact]
    public void ColossalFlowerAllThreeGoldTiersAndPollinousReward()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2, PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId,
            "colossal-flower");
        state = state with
        {
            Player = state.Player with { Hp = 30 },
            World = state.World! with
            {
                Event = new EventState(
                    PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId)
            }
        };
        var baseGold = state.Player.Gold;
        var gold0 = engine.Step(state, Event("extract")).State;
        Assert.Equal(baseGold + 35, gold0.Player.Gold);
        Assert.Equal(30, gold0.Player.Hp);

        var one = engine.Step(state, Event("deeper")).State;
        Assert.Equal(25, one.Player.Hp);
        Assert.Equal(1, one.World!.Event!.NativePageIndex);
        Assert.Equal(new[] { "extract", "deeper" },
            ChoiceIds(engine, one));
        Assert.Equal(RunPhase.Event, one.Phase);
        var gold1 = engine.Step(one, Event("extract")).State;
        Assert.Equal(baseGold + 75, gold1.Player.Gold);
        Assert.Equal(25, gold1.Player.Hp);

        var two = engine.Step(one, Event("deeper")).State;
        Assert.Equal(19, two.Player.Hp);
        Assert.Equal(2, two.World!.Event!.NativePageIndex);
        Assert.Equal(new[] { "extract", "pollinous_core" },
            ChoiceIds(engine, two));
        Assert.Throws<InvalidOperationException>(() =>
            engine.Step(two, Event("deeper")));
        var gold2 = engine.Step(two, Event("extract")).State;
        Assert.Equal(baseGold + 135, gold2.Player.Gold);
        var prize = engine.Step(two, Event("pollinous_core")).State;
        Assert.Equal(12, prize.Player.Hp);
        Assert.Contains(prize.Player.Relics, relic =>
            relic.RelicId ==
                PrototypeNativeLaterActEventExpansion.PollinousCoreRelicId);
        Assert.Equal(RunPhase.MapChoice, prize.Phase);
        Assert.Null(prize.World!.Event);
    }

    [Fact]
    public void FlowerDamageIsUnblockableAndCanEndRun()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(2, PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId,
            "colossal-flower-lethal");
        state = state with
        {
            Player = state.Player with { Hp = 5 },
            World = state.World! with
            {
                Event = new EventState(
                    PrototypeNativeLaterActEventExpansion.ColossalFlowerEventId)
            }
        };
        var result = engine.Step(state, Event("deeper")).State;
        Assert.Equal(0, result.Player.Hp);
        Assert.Equal(RunPhase.Terminal, result.Phase);
    }

    [Fact]
    public void RoundTeaPartyFullHealAndPageGatedFightBranches()
    {
        var engine = new PrototypeGameEngine();
        var state = Enter(3, PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId,
            "round-tea-party");
        state = state with
        {
            Player = state.Player with { Hp = 48 },
            World = state.World! with
            {
                Event = new EventState(
                    PrototypeNativeLaterActEventExpansion.RoundTeaPartyEventId)
            }
        };
        var tea = engine.Step(state, Event("enjoy_tea")).State;
        Assert.Equal(state.Player.MaxHp, tea.Player.Hp);
        Assert.Contains(tea.Player.Relics, relic => relic.RelicId ==
            PrototypeNativeLaterActEventExpansion.RoyalPoisonRelicId);
        Assert.Equal(RunPhase.MapChoice, tea.Phase);

        var fight = engine.Step(state, Event("pick_fight")).State;
        Assert.Equal(RunPhase.Event, fight.Phase);
        Assert.Equal(48, fight.Player.Hp);
        Assert.Equal(new[] { "continue_fight" },
            ChoiceIds(engine, fight));
        Assert.Throws<InvalidOperationException>(() =>
            engine.Step(fight, Event("enjoy_tea")));
        var finish = engine.Step(fight, Event("continue_fight")).State;
        Assert.Equal(37, finish.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, finish.Phase);
        Assert.Equal(state.Player.Relics.Length + 1,
            finish.Player.Relics.Length);
        Assert.DoesNotContain(finish.Player.Relics, relic =>
            relic.RelicId ==
                PrototypeNativeLaterActEventExpansion.RoyalPoisonRelicId);
    }

    [Fact]
    public void PollinousCoreCounterPersistsAcrossIndependentHandDrawsAndSnapshots()
    {
        var id = PrototypeNativeLaterActEventExpansion.PollinousCoreRelicId;
        var player = new PlayerState(40, 80, 0, [],
            [new RelicInstance(id, PrototypeJson.EmptyObject())],
            new PotionInstance?[2]);
        for (var turn = 1; turn <= 12; turn++)
        {
            var result = PrototypeNativeLaterActEventExpansion
                .AdvancePollinousCoreHandDraw(player);
            player = result.Player;
            Assert.Equal(turn % 4 == 0 ? 2 : 0, result.DrawBonus);
            var saved = player.Relics[0].PersistentState;
            Assert.Equal(turn % 4, saved.GetProperty("turnsSeen").GetInt32());
            player = player.Fork();
        }
        // The relic counter survives independent persistent-state JSON
        // roundtrips, including the fourth-draw reset.
        var roundtrip = JsonSerializer.Deserialize<PlayerState>(
            JsonSerializer.Serialize(player));
        Assert.NotNull(roundtrip);
        Assert.Equal(0, roundtrip!.Relics[0].PersistentState
            .GetProperty("turnsSeen").GetInt32());
    }

    [Fact]
    public void RelicTriggerDefinitionsUseSourceEventTimingAndUnpoweredDamage()
    {
        var wisp = PrototypeContent.Relic(
            PrototypeNativeLaterActEventExpansion.LostWispRelicId);
        var wispTrigger = Assert.Single(wisp.Triggers!);
        Assert.Equal(PrototypeCombatEventKind.CardPlayed,
            wispTrigger.EventKind);
        Assert.Equal(PrototypeCardType.Power,
            wispTrigger.RequiredSourceCardType);
        var wispEffect = Assert.Single(wispTrigger.Effects);
        Assert.Equal(8, wispEffect.Amount);
        Assert.Equal(PrototypeEffectTarget.AllEnemies,
            wispEffect.Target);

        var poison = PrototypeContent.Relic(
            PrototypeNativeLaterActEventExpansion.RoyalPoisonRelicId);
        var poisonTrigger = Assert.Single(poison.Triggers!);
        Assert.Equal(PrototypeCombatEventKind.PlayerTurnStarted,
            poisonTrigger.EventKind);
        Assert.Equal(1, poisonTrigger.TurnEquals);
        var poisonEffect = Assert.Single(poisonTrigger.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamagePlayer,
            poisonEffect.Kind);
        Assert.Equal(4, poisonEffect.Amount);
        Assert.True(poisonEffect.IgnorePlayerBlock);
    }

    private static string[] ChoiceIds(PrototypeGameEngine engine,
        RunState state) => engine.GetLegalActions(state)
            .Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId).ToArray();

    private static GameAction Event(string id) =>
        GameAction.Create("event_choice", new EventChoicePayload(id));

    private static RunState Enter(int act, string eventId, string seed)
    {
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create(seed);
        state = engine.Step(state, GameAction.Empty("start_run")).State;
        for (var n = 2; n <= act; n++)
        {
            state = engine.Step(state with { Phase = RunPhase.ActTransition },
                GameAction.Empty("continue_act")).State;
        }
        var map = state.World!.Map;
        var entry = map.Nodes.Single(node =>
            node.NodeId == map.EntryNodeIds![0]);
        var second = entry.NextNodeIds![0];
        map = map with
        {
            CurrentNodeId = entry.NodeId,
            Nodes = map.Nodes.Select(node =>
                node.NodeId == second
                    ? node with { RoomType = PrototypeRoomType.Event }
                    : node).ToArray()
        };
        state = state with
        {
            World = state.World with { Map = map, Floor = 1 }
        };
        // Enter the genuine source-shaped map route; the selected
        // registered event can be forced in a test-only fixture.
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(second))).State;
        return state with
        {
            World = state.World! with
            {
                Event = new EventState(eventId)
            }
        };
    }
}
