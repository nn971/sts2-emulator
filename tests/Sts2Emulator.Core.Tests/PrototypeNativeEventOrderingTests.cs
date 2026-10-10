using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeEventOrderingTests
{
    private static RunState EnterEvent(
        string eventId, string seed, int nativeEventGold = 0,
        int nativeSecondaryGold = 0)
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
                    eventId,
                    NativeEventGold: nativeEventGold,
                    NativeEventSecondaryGold: nativeSecondaryGold),
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
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(startingGold - cost, state.Player.Gold);
        Assert.Equal(beforeDeck, state.Player.Deck);
        Assert.All(state.Player.PotionSlots, potion => Assert.Null(potion));

        var engine = new PrototypeGameEngine();
        var first = engine.GetLegalActions(state);
        Assert.Contains(first, action => action.Kind == "skip_event_potion");
        var takeFirst = first.Single(action => action.Kind == "take_event_potion");
        state = engine.Step(state, takeFirst).State;
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Single(state.Player.PotionSlots, p => p is not null);

        var second = engine.GetLegalActions(state);
        Assert.Contains(second, action => action.Kind == "skip_event_potion");
        var takeSecond = second.Single(action => action.Kind == "take_event_potion");
        state = engine.Step(state, takeSecond).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(2, state.Player.PotionSlots.Count(p => p is not null));
        Assert.All(state.Player.PotionSlots.Where(p => p is not null),
            p => Assert.Contains(p!.PotionId, PrototypeContent.PotionPool));
        Assert.Equal(3, state.Player.PotionSlots.Length);
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

    [Theory]
    [InlineData(101)]
    [InlineData(121)]
    public void SunkenStatueDiveUsesPersistedGoldRoll(int reward)
    {
        var state = EnterEvent("proto.native.event.sunken_statue",
            "statue-gold-" + reward, nativeEventGold: reward);
        var goldBefore = state.Player.Gold;
        var hpBefore = state.Player.Hp;
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));

        state = Take(state, "dive");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(goldBefore + reward, state.Player.Gold);
        Assert.Equal(hpBefore - 7, state.Player.Hp);
        Assert.Null(state.World!.Event);
        Assert.Single(state.World.CompletedRooms,
            room => room.RoomType == PrototypeRoomType.Event);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(149)]
    public void LuminousChoirPriceGovernsLegalActionsAndPayment(int price)
    {
        var state = EnterEvent("proto.native.event.luminous_choir",
            "choir-price-" + price, nativeEventGold: price);
        var engine = new PrototypeGameEngine();
        var relicCount = state.Player.Relics.Length;
        var tribute = GameAction.Create("event_choice",
            new EventChoicePayload("tribute"));

        state = state with
        {
            Player = state.Player with { Gold = price - 1 }
        };
        Assert.DoesNotContain(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "tribute");
        Assert.Throws<InvalidOperationException>(
            () => engine.Step(state, tribute));
        PrototypeStateInvariants.Validate(state);

        state = state with
        {
            Player = state.Player with { Gold = price }
        };
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "tribute");
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
        state = Take(state, "tribute");

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(0, state.Player.Gold);
        Assert.Equal(relicCount + 1, state.Player.Relics.Length);
        Assert.Null(state.World!.Event);
    }

    [Theory]
    [InlineData("proto.native.event.sunken_statue", 100)]
    [InlineData("proto.native.event.sunken_statue", 122)]
    [InlineData("proto.native.event.luminous_choir", 99)]
    [InlineData("proto.native.event.luminous_choir", 150)]
    public void EventRollInvariantRejectsOutOfRangeValues(
        string eventId, int invalidRoll)
    {
        var state = EnterEvent(eventId, "invalid-gold-roll");
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    NativeEventGold = invalidRoll
                }
            }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(state));
    }

    [Fact]
    public void SunkenStatueGrabGivesPersistentSwordOfStone()
    {
        var state = EnterEvent("proto.native.event.sunken_statue",
            "sunken-statue-sword", nativeEventGold: 111);
        var count = state.Player.Relics.Length;
        state = Take(state, "grab");

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(count + 1, state.Player.Relics.Length);
        Assert.Single(state.Player.Relics,
            relic => relic.RelicId == "proto.native.event.sword_of_stone");
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData("solo", 135, 64, 135, 18)]
    [InlineData("solo", 164, 35, 164, 18)]
    [InlineData("join", 164, 35, 35, 0)]
    [InlineData("join", 135, 64, 64, 0)]
    public void JungleMazeUsesIndependentPersistedGoldRolls(
        string choice, int soloGold, int joinGold,
        int expectedGold, int expectedHpLoss)
    {
        var state = EnterEvent(
            "proto.native.event.jungle_maze_adventure",
            "jungle-" + choice + "-" + soloGold + "-" + joinGold,
            nativeEventGold: soloGold,
            nativeSecondaryGold: joinGold);
        var beforeGold = state.Player.Gold;
        var beforeHp = state.Player.Hp;
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));

        state = Take(state, choice);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(beforeGold + expectedGold, state.Player.Gold);
        Assert.Equal(beforeHp - expectedHpLoss, state.Player.Hp);
        Assert.Null(state.World!.Event);
        Assert.Single(state.World.CompletedRooms,
            room => room.RoomType == PrototypeRoomType.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(134, 35)]
    [InlineData(165, 35)]
    [InlineData(150, 34)]
    [InlineData(150, 65)]
    [InlineData(150, 0)]
    [InlineData(0, 50)]
    public void JungleMazeRejectsInvalidOrUnpairedRolls(
        int soloGold, int joinGold)
    {
        var state = EnterEvent(
            "proto.native.event.jungle_maze_adventure",
            "jungle-invalid-roll");
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
                {
                    NativeEventGold = soloGold,
                    NativeEventSecondaryGold = joinGold
                }
            }
        };
        Assert.Throws<InvalidOperationException>(
            () => PrototypeStateInvariants.Validate(state));
    }

    [Theory]
    [InlineData(18)]
    [InlineData(10)]
    [InlineData(1)]
    public void JungleMazeSoloRemainsLegalEvenWhenLethalAndAwardsNoGold(
        int initialHp)
    {
        var state = EnterEvent(
            "proto.native.event.jungle_maze_adventure",
            "jungle-lethal-solo-" + initialHp,
            nativeEventGold: 150,
            nativeSecondaryGold: 50);
        state = state with
        {
            Player = state.Player with { Hp = initialHp }
        };
        var startingGold = state.Player.Gold;
        var engine = new PrototypeGameEngine();
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "solo");
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "join");
        PrototypeStateInvariants.Validate(state);
        state = Take(state, "solo");
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.Equal(0, state.Player.Hp);
        Assert.Equal(startingGold, state.Player.Gold);
        Assert.Equal("defeat", state.World!.TerminalOutcome);
    }

    [Fact]
    public void SunkenStatueDivePaysGoldBeforeLethalDamage()
    {
        var state = EnterEvent(
            "proto.native.event.sunken_statue",
            "sunken-statue-lethal", nativeEventGold: 111);
        state = state with
        {
            Player = state.Player with { Hp = 7 }
        };
        var startingGold = state.Player.Gold;
        var engine = new PrototypeGameEngine();
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "dive");
        state = Take(state, "dive");
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.Equal(0, state.Player.Hp);
        Assert.Equal(startingGold + 111, state.Player.Gold);
        Assert.Equal("defeat", state.World!.TerminalOutcome);
    }

    [Fact]
    public void WhisperingHollowAllowsLethalHugThenStopsAfterTransformation()
    {
        var state = EnterEvent(
            "proto.native.event.whispering_hollow",
            "whisper-hug-lethal", nativeEventGold: 35);
        state = state with { Player = state.Player with { Hp = 9 } };
        var engine = new PrototypeGameEngine();
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId
                    == "hug");
        state = Take(state, "hug");
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.NotNull(state.World!.Event!.PendingDeckChoice);
        var before = state.Player.Deck[0];
        var transform = engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_event_deck_card"
            && action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == before.InstanceId);
        state = engine.Step(state, transform).State;
        Assert.Equal(RunPhase.Terminal, state.Phase);
        Assert.Equal(0, state.Player.Hp);
        Assert.Equal("defeat", state.World!.TerminalOutcome);
    }

    [Fact]
    public void WellspringBottleCanBeSkippedWithAnEmptyPotionBelt()
    {
        var state = EnterEvent(
            "proto.native.event.wellspring", "wellspring-skip-bottle");
        state = Take(state, "bottle");
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.All(state.Player.PotionSlots, potion => Assert.Null(potion));
        var pending = state.World!.Event!.PendingPotionReplacement;
        Assert.NotNull(pending);
        Assert.Single(pending.CandidateSlots);

        var engine = new PrototypeGameEngine();
        var actions = engine.GetLegalActions(state);
        Assert.Single(actions, action => action.Kind == "take_event_potion");
        Assert.Single(actions, action => action.Kind == "skip_event_potion");
        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        state = engine.Step(
            state, GameAction.Empty("skip_event_potion")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.All(state.Player.PotionSlots, potion => Assert.Null(potion));
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void WellspringBottleCanReplaceAFullPotionBelt()
    {
        var state = EnterEvent(
            "proto.native.event.wellspring", "wellspring-replace-bottle");
        var oldPotion = PrototypeContent.PotionPool[0];
        var slots = state.Player.PotionSlots
            .Select(_ => (PotionInstance?)new PotionInstance(
                oldPotion, PrototypeJson.EmptyObject()))
            .ToArray();
        state = state with { Player = state.Player with { PotionSlots = slots } };
        PrototypeStateInvariants.Validate(state);
        state = Take(state, "bottle");
        Assert.Equal(RunPhase.Event, state.Phase);
        var engine = new PrototypeGameEngine();
        var actions = engine.GetLegalActions(state);
        Assert.Equal(slots.Length,
            actions.Count(action => action.Kind == "replace_event_potion"));
        Assert.DoesNotContain(actions, action => action.Kind == "take_event_potion");
        Assert.Contains(actions, action => action.Kind == "skip_event_potion");

        var offeredId = state.World!.Event!.PendingPotionReplacement!.PotionId;
        var chosen = actions.Single(action =>
            action.Kind == "replace_event_potion"
            && action.ReadPayload<ReplaceEventPotionPayload>().Slot == 0);
        state = engine.Step(state, chosen).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(offeredId, state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal(oldPotion, state.Player.PotionSlots[1]!.PotionId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void WhisperingHollowSupportsSkippingOnlyFirstPotionReward()
    {
        var state = EnterEvent(
            "proto.native.event.whispering_hollow",
            "whisper-skip-first", nativeEventGold: 35);
        var oldGold = state.Player.Gold;
        state = Take(state, "gold");
        var engine = new PrototypeGameEngine();
        state = engine.Step(
            state, GameAction.Empty("skip_event_potion")).State;
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(oldGold - 35, state.Player.Gold);
        Assert.All(state.Player.PotionSlots, potion => Assert.Null(potion));
        Assert.NotNull(state.World!.Event!.PendingPotionReplacement);
        var next = engine.GetLegalActions(state)
            .Single(action => action.Kind == "take_event_potion");
        state = engine.Step(state, next).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Single(state.Player.PotionSlots, p => p is not null);
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EventPotionActionCannotReplaceOccupiedSlotWithTake()
    {
        var state = EnterEvent(
            "proto.native.event.wellspring", "wellspring-malformed-action");
        state = Take(state, "bottle");
        var engine = new PrototypeGameEngine();
        Assert.Throws<InvalidOperationException>(() =>
            engine.Step(state, GameAction.Create(
                "replace_event_potion", new ReplaceEventPotionPayload(0))));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void LuminousChoirAddsSporeMindOnlyAfterBothRemovals()
    {
        var state = EnterEvent(
            "proto.native.event.luminous_choir",
            "luminous-reach-order", nativeEventGold: 100);
        var originalDeck = state.Player.Deck.ToArray();
        var nextCardId = state.World!.NextCardInstanceId;
        state = Take(state, "reach");

        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.Equal(nextCardId, state.World!.NextCardInstanceId);
        Assert.DoesNotContain(state.Player.Deck,
            card => card.CardId == "proto.native.event.spore_mind");
        Assert.Equal("proto.native.event.spore_mind",
            state.World.Event!.DeferredCardId);

        var engine = new PrototypeGameEngine();
        var firstActions = engine.GetLegalActions(state);
        Assert.DoesNotContain(firstActions, action =>
            action.Kind == "choose_event_deck_card"
            && action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == nextCardId);

        var first = firstActions.Single(action =>
            action.Kind == "choose_event_deck_card"
            && action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == originalDeck[0].InstanceId);
        state = engine.Step(state, first).State;
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(12, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck,
            card => card.CardId == "proto.native.event.spore_mind");

        var second = engine.GetLegalActions(state).Single(action =>
            action.Kind == "choose_event_deck_card"
            && action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == originalDeck[1].InstanceId);
        state = engine.Step(state, second).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(12, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck,
            card => card.InstanceId == originalDeck[0].InstanceId
                || card.InstanceId == originalDeck[1].InstanceId);
        Assert.Single(state.Player.Deck,
            card => card.CardId == "proto.native.event.spore_mind"
                && card.InstanceId == nextCardId);
        Assert.Equal(nextCardId + 1, state.World!.NextCardInstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData("proto.native.event.aroma_of_chaos", "let_go", null, 0)]
    [InlineData("proto.native.event.aroma_of_chaos", "maintain_control", null, 0)]
    [InlineData("proto.native.event.sapphire_seed", "eat", null, 0)]
    [InlineData("proto.native.event.sapphire_seed", "plant", null, 0)]
    [InlineData("proto.native.event.wellspring", "bathe", "proto.native.event.guilty", 0)]
    [InlineData("proto.native.event.luminous_choir", "reach", "proto.native.event.spore_mind", 0)]
    [InlineData("proto.native.event.whispering_hollow", "hug", null, 9)]
    public void SourceOptionalEventCardPickerCanReturnNoSelection(
        string eventId, string choice, string? expectedCard, int hpLoss)
    {
        var state = EnterEvent(
            eventId, "empty-deck-picker-" + choice,
            nativeEventGold: eventId == "proto.native.event.whispering_hollow"
                ? 35 : eventId == "proto.native.event.luminous_choir"
                ? 100 : 0);
        var initialHp = state.Player.Hp;
        state = state with
        {
            Player = state.Player with { Deck = [] }
        };
        PrototypeStateInvariants.Validate(state);
        var engine = new PrototypeGameEngine();
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "event_choice"
                && action.ReadPayload<EventChoicePayload>().ChoiceId == choice);

        state = Take(state, choice);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(initialHp - hpLoss, state.Player.Hp);
        if (expectedCard is null)
        {
            Assert.Empty(state.Player.Deck);
        }
        else
        {
            var card = Assert.Single(state.Player.Deck);
            Assert.Equal(expectedCard, card.CardId);
        }
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }
}
