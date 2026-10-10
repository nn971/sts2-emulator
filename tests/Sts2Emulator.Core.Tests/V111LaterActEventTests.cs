using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActEventTests
{
    [Theory]
    [InlineData(2, "proto.native.hive.bugslayer")]
    [InlineData(2, PrototypeNativeLaterActEvents.InfestedAutomatonId)]
    [InlineData(3, PrototypeNativeLaterActEvents.ReflectionsId)]
    public void ImplementedEventOnlyHasPinnedActAndEnabledChoiceList(
        int act, string id)
    {
        var definition = PrototypeContent.Event(id);
        Assert.Equal(act, definition.MinAct);
        Assert.Equal(act, definition.MaxAct);
        Assert.Contains(id, PrototypeNativeLaterActEvents.SupportedIds(act));
        Assert.True(PrototypeNativeLaterActEvents.IsSupported(act, id));
        Assert.Equal(2, definition.Choices.Length);
        Assert.Equal(0, definition.Weight);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeEventRoomNeverSamplesLegacyOrUnimplementedEvents(int act)
    {
        var engine = new PrototypeGameEngine();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < 40; index++)
        {
            var state = EnterEvent(act, $"event-pool-{act}-{index}");
            Assert.Equal(RunPhase.Event, state.Phase);
            var selected = state.World!.Event!.EventId;
            Assert.Contains(selected, PrototypeNativeLaterActEvents.SupportedIds(act));
            seen.Add(selected);
            var actionIds = engine.GetLegalActions(state).Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId).ToArray();
            Assert.Equal(2, actionIds.Length);
            Assert.Equal(
                PrototypeContent.Event(selected).Choices.Take(2)
                    .Select(c => c.Id),
                actionIds);
        }
        Assert.Equal(PrototypeNativeLaterActEvents.SupportedIds(act).Length,
            seen.Count);
    }

    [Theory]
    [InlineData("extermination", PrototypeNativeLaterActs.ExterminateId)]
    [InlineData("squash", PrototypeNativeLaterActs.SquashId)]
    public void BugslayerAddsExactlyTheSelectedEventCard(
        string choice, string expectedCardId)
    {
        var state = EnterEvent(2, "bugslayer-" + choice);
        state = state with
        {
            World = state.World! with
            {
                Event = new EventState(PrototypeNativeLaterActs.HiveBugslayerId)
            }
        };
        var original = state.Player.Deck.Length;
        var engine = new PrototypeGameEngine();
        state = engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(choice))).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(original + 1, state.Player.Deck.Length);
        Assert.Equal(expectedCardId, state.Player.Deck[^1].CardId);
        Assert.Null(state.World!.Event);
        Assert.Single(state.World.CompletedRooms);
    }

    [Theory]
    [InlineData("study")]
    [InlineData("touch_core")]
    public void InfestedAutomatonGivesSingleSourceFilteredSilentCard(
        string choice)
    {
        var engine = new PrototypeGameEngine();
        var state = EnterEvent(2, "automaton-" + choice);
        state = state with
        {
            World = state.World! with
            {
                Event = new EventState(
                    PrototypeNativeLaterActEvents.InfestedAutomatonId)
            }
        };
        var initialId = state.World!.NextCardInstanceId;
        state = engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(choice))).State;
        var last = state.Player.Deck[^1];
        Assert.Equal(initialId, last.InstanceId);
        Assert.Equal(initialId + 1, state.World!.NextCardInstanceId);
        Assert.Contains(last.CardId, PrototypeContent.NativeSilentCardPool);
        var definition = PrototypeContent.Card(last.CardId);
        if (choice == "study")
            Assert.Equal(PrototypeCardType.Power, definition.Type);
        else
        {
            Assert.Equal(PrototypeCardCostKind.Fixed, definition.Cost.Kind);
            Assert.Equal(0, definition.Cost.Amount);
        }
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }

    [Fact]
    public void ReflectionsShatterCopiesPersistentCardStatesThenAddsEternalBadLuck()
    {
        var engine = new PrototypeGameEngine();
        var state = EnterEvent(3, "reflections-shatter");
        var first = state.Player.Deck[0];
        state = state with
        {
            World = state.World! with
            {
                Event = new EventState(PrototypeNativeLaterActEvents.ReflectionsId)
            },
            Player = state.Player with
            {
                Deck = state.Player.Deck.Select(card => card.InstanceId ==
                    first.InstanceId ? card with { UpgradeLevel = 1 } : card)
                    .ToArray()
            }
        };
        var original = state.Player.Deck.ToArray();
        var nextId = state.World!.NextCardInstanceId;
        var replay = state.Fork();
        var choice = GameAction.Create("event_choice",
            new EventChoicePayload("shatter"));
        state = engine.Step(state, choice).State;
        replay = engine.Step(replay, choice).State;
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(replay));
        Assert.Equal(original.Length * 2 + 1, state.Player.Deck.Length);
        var copies = state.Player.Deck.Skip(original.Length).Take(original.Length)
            .ToArray();
        Assert.Equal(original.Select(card => card.CardId),
            copies.Select(card => card.CardId));
        Assert.Equal(original.Select(card => card.UpgradeLevel),
            copies.Select(card => card.UpgradeLevel));
        Assert.Equal(Enumerable.Range(0, original.Length)
                .Select(i => nextId + i),
            copies.Select(card => card.InstanceId));
        Assert.Equal(PrototypeNativeLaterActEvents.BadLuckId,
            state.Player.Deck[^1].CardId);
        var badLuck = PrototypeContent.Card(
            PrototypeNativeLaterActEvents.BadLuckId);
        Assert.True(badLuck.Unplayable);
        Assert.True(badLuck.Eternal);
        Assert.True(badLuck.EndTurnDamageUnblockable);
        Assert.Equal(13, badLuck.EndTurnDamageIfInHand);
        Assert.Equal(0, badLuck.MaxUpgradeLevel);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(RunSnapshot.Load(RunSnapshot.Save(state))));
    }

    [Fact]
    public void ReflectionsTouchMirrorDowngradesAtMostTwoThenUpgradesAtMostFour()
    {
        var engine = new PrototypeGameEngine();
        var state = EnterEvent(3, "reflections-touch");
        var original = state.Player.Deck.Select((card, index) =>
            card with { UpgradeLevel = index < 4 ? 1 : 0 }).ToArray();
        state = state with
        {
            Player = state.Player with { Deck = original },
            World = state.World! with
            {
                Event = new EventState(PrototypeNativeLaterActEvents.ReflectionsId)
            }
        };
        var clone = state.Fork();
        var action = GameAction.Create("event_choice",
            new EventChoicePayload("touch_mirror"));
        state = engine.Step(state, action).State;
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(engine.Step(clone, action).State));
        Assert.Equal(original.Length, state.Player.Deck.Length);
        Assert.Equal(original.Select(card => card.InstanceId),
            state.Player.Deck.Select(card => card.InstanceId));
        Assert.Equal(6, state.Player.Deck.Sum(card => card.UpgradeLevel));
        Assert.All(state.Player.Deck, card =>
            Assert.InRange(card.UpgradeLevel, 0,
                PrototypeContent.Card(card.CardId).MaxUpgradeLevel));
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ExhaustedImplementedLaterActEventsThrowWithoutMutatingState(int act)
    {
        var engine = new PrototypeGameEngine();
        var state = EnterEvent(act, "exhausted-" + act);
        state = state with
        {
            World = state.World! with
            {
                Event = null,
                EventHistory = PrototypeNativeLaterActEvents.SupportedIds(act)
            }
        };
        // Replay a supported event choice from the real generated
        // map's floor-one predecessor, not a synthetic two-node map.
        var map = state.World!.Map;
        var entry = map.Nodes.Single(node =>
            node.NodeId == map.EntryNodeIds![0]);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World with
            {
                Floor = 1,
                ActiveRoom = null,
                Map = map with { CurrentNodeId = entry.NodeId }
            }
        };
        var before = CanonicalJson.Sha256(state);
        var error = Assert.Throws<NotSupportedException>(() =>
            engine.Step(state, engine.GetLegalActions(state)[0]));
        Assert.Contains("complete later-act event bag", error.Message);
        Assert.Equal(before, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void BadLuckInHandDealsThirteenUnblockableAtPlayerTurnEnd()
    {
        var withCurse = StartCombatWithBadLuck(true);
        var withoutCurse = StartCombatWithBadLuck(false);
        var engine = new PrototypeGameEngine();
        Assert.Equal(9000, withCurse.Player.Hp);
        var curse = Assert.Single(withCurse.World!.Combat!.Hand);
        Assert.Equal(PrototypeNativeLaterActEvents.BadLuckId,
            withCurse.World.Combat.Cards.Single(card =>
                card.InstanceId == curse).CardId);
        Assert.DoesNotContain(engine.GetLegalActions(withCurse), action =>
            action.Kind == "play_card");

        var action = GameAction.Empty("end_turn");
        withCurse = engine.Step(withCurse, action).State;
        withoutCurse = engine.Step(withoutCurse, action).State;
        Assert.Equal(13,
            withoutCurse.Player.Hp - withCurse.Player.Hp);
    }

    private static RunState StartCombatWithBadLuck(bool includeCurse)
    {
        var seed = "bad-luck-event-hp";
        var deck = includeCurse
            ? new[]
            {
                new CardInstance(1,
                    PrototypeNativeLaterActEvents.BadLuckId,
                    0, PrototypeJson.EmptyObject())
            }
            : Array.Empty<CardInstance>();
        var state = new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, deck, [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 3, 1, 2,
                PrototypeRoomType.Elite, new MapState([]),
                new CombatState(1, 3, 0, [], [], [], [], [],
                    1, [], [], 1, Act: 3),
                null, null, null, null));
        var start = typeof(PrototypeGameEngine).GetMethod(
            "StartCombat",
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static);
        Assert.NotNull(start);
        return Assert.IsType<RunState>(start!.Invoke(null,
        [
            state, PrototypeRoomType.Elite,
            PrototypeContent.Encounter(
                PrototypeNativeGloryElites.SoulNexusEncounterId)
        ]));
    }

    private static RunState EnterEvent(int act, string seed)
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
        // Select a real connected floor-two node and annotate only its
        // event room type, retaining the act's full 14/13-row graph.
        var map = state.World!.Map;
        var entry = map.Nodes.Single(node =>
            node.NodeId == map.EntryNodeIds![0]);
        var targetId = entry.NextNodeIds![0];
        map = map with
        {
            CurrentNodeId = entry.NodeId,
            Nodes = map.Nodes.Select(node =>
                node.NodeId == targetId
                    ? node with { RoomType = PrototypeRoomType.Event }
                    : node).ToArray()
        };
        state = state with
        {
            World = state.World with { Map = map, Floor = 1 }
        };
        var choice = GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(targetId));
        return engine.Step(state, choice).State;
    }
}
