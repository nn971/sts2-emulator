using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeEventEnchantmentsTests
{
    private static RunState EnterEvent(string eventId, string seed)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed);
        state = state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with
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
        var neow = engine.GetLegalActions(state).Single(a =>
            a.ReadPayload<EventChoicePayload>().ChoiceId ==
                "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(
                    "GoldenPearl"));
        state = engine.Step(state, neow).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);

        var map = state.World!.Map;
        var node = map.Nodes.First(n =>
            n.RoomType == PrototypeRoomType.Unknown);
        var parent = map.Nodes.First(n =>
            n.NextNodeIds?.Contains(node.NodeId,
                StringComparer.Ordinal) == true);
        var history = Enumerable.Range(1, parent.Floor).Select(floor =>
        {
            var visited = floor == parent.Floor
                ? parent : map.Nodes.First(n => n.Floor == floor);
            return new PrototypeCompletedRoomRecord(
                1, floor, visited.NodeId, visited.RoomType);
        }).ToArray();

        state = state with
        {
            Phase = RunPhase.Event,
            World = state.World with
            {
                Floor = node.Floor,
                Map = map with { CurrentNodeId = node.NodeId },
                ActiveRoom = PrototypeRoomType.Event,
                CompletedRoomHistory = history,
                Event = new EventState(eventId),
                EventHistory = state.World.EventIds.Append(eventId).ToArray()
            }
        };
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState ChooseAndApply(
        RunState state, string choice, long cardInstanceId)
    {
        var engine = new PrototypeGameEngine();
        var choose = engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        state = engine.Step(state, choose).State;
        Assert.Equal(RunPhase.Event, state.Phase);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            state.World!.Event!.PendingDeckChoice);
        Assert.Contains(cardInstanceId, pending.CandidateCardInstanceIds);
        var fork = state.Fork();
        Assert.NotSame(pending.CandidateCardInstanceIds,
            fork.World!.Event!.PendingDeckChoice!.CandidateCardInstanceIds);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));

        var select = engine.GetLegalActions(state).Single(a =>
            a.Kind == "choose_event_deck_card"
            && a.ReadPayload<ChooseEventDeckCardPayload>().CardInstanceId
                == cardInstanceId);
        state = engine.Step(state, select).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState StartFirstCombat(RunState state)
    {
        var engine = new PrototypeGameEngine();
        var world = state.World!;
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = world with
            {
                Floor = 0,
                ActiveRoom = null,
                CompletedRoomHistory = [],
                Map = world.Map with { CurrentNodeId = null }
            }
        };
        PrototypeStateInvariants.Validate(state);
        var first = engine.GetLegalActions(state).First();
        state = engine.Step(state, first).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    private static RunState PutOnlyCardInHand(
        RunState state, string cardId)
    {
        var combat = state.World!.Combat!;
        var chosen = combat.Cards.First(c => c.CardId == cardId
            && (cardId != "proto.silent.strike"
                || c.PersistentCardInstanceId == 1L));
        var otherIds = combat.Cards.Select(c => c.InstanceId)
            .Where(id => id != chosen.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [chosen.InstanceId],
            DrawPile = otherIds,
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
        return state;
    }

    [Theory]
    [InlineData("bird", "proto.native.event.peck")]
    [InlineData("torus", "proto.native.event.toric_toughness")]
    public void WoodCarvingsTransformsBasicCardToSpecificEventCard(
        string choice, string expectedCardId)
    {
        var state = EnterEvent(
            "proto.native.event.wood_carvings",
            "wood-carving-" + choice);
        var engine = new PrototypeGameEngine();
        var action = engine.GetLegalActions(state).Single(a =>
            a.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        state = engine.Step(state, action).State;
        var pending = state.World!.Event!.PendingDeckChoice!;
        Assert.True(pending.BasicCardsOnly);
        Assert.Equal(expectedCardId, pending.TransformToCardId);
        // The native Silent starter has 12 Basic cards:
        // 5 Strike, 5 Defend, Neutralize, and Survivor.
        Assert.Equal(12, pending.CandidateCardInstanceIds.Length);
        Assert.Contains(11L, pending.CandidateCardInstanceIds);
        Assert.Contains(12L, pending.CandidateCardInstanceIds);
        Assert.DoesNotContain(13L, pending.CandidateCardInstanceIds);

        var select = engine.GetLegalActions(state).Single(a =>
            a.ReadPayload<ChooseEventDeckCardPayload>().CardInstanceId == 1L);
        state = engine.Step(state, select).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(expectedCardId, state.Player.Deck[0].CardId);
        Assert.Equal(1L, state.Player.Deck[0].InstanceId);
        Assert.Equal(0, state.Player.Deck[0].UpgradeLevel);
        Assert.Null(state.Player.Deck[0].Enchantment);
        Assert.Equal(13, state.Player.Deck.Length);
        Assert.DoesNotContain(expectedCardId, PrototypeContent.RewardCardPool);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SownGrantsOneEnergyOnFirstPlayButNotSecond()
    {
        var state = ChooseAndApply(EnterEvent(
            "proto.native.event.sapphire_seed", "sown-persistent"),
            "plant", 1);
        Assert.Equal(PrototypeCardEnchantmentKind.Sown,
            state.Player.Deck[0].Enchantment!.Kind);
        state = PutOnlyCardInHand(StartFirstCombat(state),
            "proto.silent.strike");
        var engine = new PrototypeGameEngine();
        var combat = state.World!.Combat!;
        var strike = combat.Cards.Single(c =>
            c.PersistentCardInstanceId == 1);
        var target = combat.Enemies[0].InstanceId;
        var play = GameAction.Create("play_card",
            new PlayCardPayload(strike.InstanceId, target));
        Assert.Contains(engine.GetLegalActions(state), action =>
            action.Kind == play.Kind &&
            action.ReadPayload<PlayCardPayload>() == play.ReadPayload<PlayCardPayload>());
        state = engine.Step(state, play).State;
        Assert.Equal(3, state.World!.Combat!.Energy);
        Assert.True(state.World.Combat.Cards.Single(c =>
            c.InstanceId == strike.InstanceId)
            .EnchantmentTriggeredThisCombat);

        combat = state.World.Combat with
        {
            Hand = [strike.InstanceId],
            DrawPile = state.World.Combat.DrawPile,
            DiscardPile = [],
            Energy = 3
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };
        PrototypeStateInvariants.Validate(state);
        state = engine.Step(state, play).State;
        Assert.Equal(2, state.World!.Combat!.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SlitherRerollsCostWhenDrawnAndSurvivesCombatInitialization()
    {
        var state = ChooseAndApply(EnterEvent(
            "proto.native.event.wood_carvings", "slither-persistent"),
            "snake", 1);
        Assert.Equal(PrototypeCardEnchantmentKind.Slither,
            state.Player.Deck[0].Enchantment!.Kind);
        state = StartFirstCombat(state);
        var combat = state.World!.Combat!;
        var enchanted = combat.Cards.Single(c =>
            c.PersistentCardInstanceId == 1);
        var rest = combat.Cards.Single(c =>
            c.CardId == "proto.common.restlessness");
        var remaining = combat.Cards
            .Select(c => c.InstanceId)
            .Where(id => id != enchanted.InstanceId
                && id != rest.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [rest.InstanceId],
            DrawPile = remaining.Append(enchanted.InstanceId).ToArray(),
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
        var fork = state.Fork();

        var engine = new PrototypeGameEngine();
        var play = GameAction.Create("play_card",
            new PlayCardPayload(rest.InstanceId, null));
        state = engine.Step(state, play).State;
        fork = engine.Step(fork, play).State;
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(fork));
        var resultingCombat = state.World!.Combat!;
        Assert.Contains(enchanted.InstanceId, resultingCombat.Hand);
        var drawn = resultingCombat.Cards.Single(c =>
            c.InstanceId == enchanted.InstanceId);
        var newCost = Assert.IsType<PrototypeTemporaryCardCost>(
            drawn.TemporaryEnergyCost);
        Assert.InRange(newCost.Cost, 0, 3);
        Assert.Equal(PrototypeCardEnchantmentKind.Slither,
            drawn.Enchantment!.Kind);
        PrototypeStateInvariants.Validate(state);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public void PeckHitsThreeTimesOrFourTimesWhenUpgraded(
        bool upgraded, int expectedDamage)
    {
        var state = ChooseAndApply(EnterEvent(
            "proto.native.event.wood_carvings", "peck-hit-" + upgraded),
            "bird", 1L);
        if (upgraded)
        {
            state = state with
            {
                Player = state.Player with
                {
                    Deck = state.Player.Deck.Select(card =>
                        card.InstanceId == 1
                            ? card with { UpgradeLevel = 1 } : card)
                        .ToArray()
                }
            };
        }
        state = PutOnlyCardInHand(StartFirstCombat(state),
            "proto.native.event.peck");
        var combat = state.World!.Combat!;
        var peck = combat.Cards.Single(card =>
            card.CardId == "proto.native.event.peck");
        var target = combat.Enemies[0];
        Assert.True(target.Hp > expectedDamage);
        var engine = new PrototypeGameEngine();
        var action = GameAction.Create("play_card",
            new PlayCardPayload(peck.InstanceId, target.InstanceId));
        state = engine.Step(state, action).State;
        Assert.Equal(target.Hp - expectedDamage,
            state.World!.Combat!.Enemies[0].Hp);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ToricToughnessGrantsBlockAndTwoFutureRenewals()
    {
        var state = ChooseAndApply(EnterEvent(
            "proto.native.event.wood_carvings", "toric-combat"),
            "torus", 1);
        state = PutOnlyCardInHand(StartFirstCombat(state),
            "proto.native.event.toric_toughness");
        var engine = new PrototypeGameEngine();
        var combat = state.World!.Combat!;
        var card = combat.Cards.Single(c =>
            c.CardId == "proto.native.event.toric_toughness");
        state = engine.Step(state, GameAction.Create(
            "play_card", new PlayCardPayload(card.InstanceId, null))).State;
        Assert.Equal(1, state.World!.Combat!.Energy);
        Assert.Equal(5, state.World.Combat.PlayerBlock);
        var shield = Assert.Single(state.World.Combat.ToricShields!);
        Assert.Equal(5, shield.BlockAmount);
        Assert.Equal(2, shield.ClearsRemaining);
        PrototypeStateInvariants.Validate(state);
        state = engine.Step(state, GameAction.Empty("end_turn")).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(2, state.World!.Combat!.Turn);
        var firstRenewal = Assert.Single(state.World.Combat.ToricShields!);
        Assert.Equal(5, firstRenewal.BlockAmount);
        Assert.Equal(1, firstRenewal.ClearsRemaining);
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state, GameAction.Empty("end_turn")).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(3, state.World!.Combat!.Turn);
        Assert.Empty(state.World.Combat.ToricShields!);
        PrototypeStateInvariants.Validate(state);
    }
}
