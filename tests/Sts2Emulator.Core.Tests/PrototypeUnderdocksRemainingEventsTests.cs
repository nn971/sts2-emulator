using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksRemainingEventsTests
{
    [Fact]
    public void EventEligibilityRespectsNativeGoldAndFloorGates()
    {
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-final-event-gates").Player;
        var conveyor = PrototypeContent.Event(PrototypeNativeEndlessConveyor.EventId);
        Assert.False(PrototypeNativeUnderdocksEvents.IsEligible(
            conveyor, player with { Gold = 119 }));
        Assert.True(PrototypeNativeUnderdocksEvents.IsEligible(
            conveyor, player with { Gold = 120 }));

        var world = PrototypeNativeUnderdocksRunFactory.Create(
            "underdocks-event-floor").World!;
        var punchOff = PrototypeContent.Event(PrototypeNativePunchOff.EventId);
        Assert.False(PrototypeNativeUnderdocksEvents.IsFloorEligible(
            punchOff, world with { Floor = 5 }));
        Assert.True(PrototypeNativeUnderdocksEvents.IsFloorEligible(
            punchOff, world with { Floor = 6 }));
        Assert.Equal(10,
            PrototypeNativeUnderdocksEvents.SupportedRegionEventIds.Length);
        Assert.Equal(10,
            PrototypeNativeUnderdocksEvents.NativeRegionEventIds.Length);
    }

    [Fact]
    public void ConveyorRollsNoImmediateRepeatAndForcesEveryFifthDish()
    {
        var player = PrototypeNativeUnderdocksRunFactory.Create(
            "conveyor-rolls").Player with { Gold = 500 };
        var rng = PrototypeRng.CreateBundle("conveyor-rolls");
        var evt = new EventState(PrototypeNativeEndlessConveyor.EventId);
        var previous = "";
        var all = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i <= 25; i++)
        {
            evt = PrototypeNativeEndlessConveyor.RollNextDish(
                evt, player, rng);
            Assert.Equal(i, evt.NativeDishCount);
            Assert.NotEqual(previous, evt.NativeDishId);
            Assert.Equal(evt.NativeDishId, evt.NativeLastDishId);
            if (i % 5 == 0)
            {
                Assert.Equal("SEAPUNK_SALAD", evt.NativeDishId);
            }
            else
            {
                Assert.NotEqual("SEAPUNK_SALAD", evt.NativeDishId);
                if (i == 1)
                {
                    Assert.NotEqual("GOLDEN_FYSH", evt.NativeDishId);
                }
            }
            previous = evt.NativeDishId!;
            all.Add(previous);
        }
        Assert.Contains("SEAPUNK_SALAD", all);
        Assert.True(all.Count >= 4);
    }

    [Fact]
    public void ConveyorChefUpgradesAndLeavesWithoutSpendingGold()
    {
        var engine = new PrototypeGameEngine();
        var entered = FindEvent(
            engine, PrototypeNativeEndlessConveyor.EventId,
            "conveyor-observe", gold: 400);
        Assert.Equal(1, entered.World!.Event!.NativeDishCount);
        var rngBefore = CanonicalJson.Sha256(entered.Rng);
        var upgraded = Choose(engine, entered, "observe");
        Assert.Equal(RunPhase.MapChoice, upgraded.Phase);
        Assert.Equal(entered.Player.Gold, upgraded.Player.Gold);
        Assert.Equal(1, upgraded.Player.Deck.Count(card =>
            card.UpgradeLevel == 1));
        Assert.NotEqual(rngBefore, CanonicalJson.Sha256(upgraded.Rng));
    }

    [Fact]
    public void ConveyorFourGrabsLeadToFifthGuaranteedFeedingFrenzyDish()
    {
        var engine = new PrototypeGameEngine();
        var current = FindEvent(
            engine, PrototypeNativeEndlessConveyor.EventId,
            "conveyor-five", gold: 500);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(i + 1, current.World!.Event!.NativeDishCount);
            current = Choose(engine, current, "grab");
            while (current.Phase == RunPhase.Event
                && (current.World!.Event!.PendingDeckChoice is not null
                    || current.World.Event.PendingPotionReplacement is not null))
            {
                var legal = engine.GetLegalActions(current);
                var selected = legal.FirstOrDefault(a =>
                    a.Kind == "skip_event_potion")
                    ?? legal.First();
                current = engine.Step(current, selected).State;
            }
            Assert.Equal(RunPhase.Event, current.Phase);
            Assert.Equal(i + 2, current.World!.Event!.NativeDishCount);
            Assert.Equal(CanonicalJson.Sha256(current),
                CanonicalJson.Sha256(current.Fork()));
        }

        Assert.Equal("SEAPUNK_SALAD",
            current.World!.Event!.NativeDishId);
        Assert.Equal("proto.native.underdocks.feeding_frenzy",
            PrototypeNativeEndlessConveyor.FeedingFrenzyId);
        var oldCount = current.Player.Deck.Length;
        var beforeGold = current.Player.Gold;
        var eaten = Choose(engine, current, "grab");
        Assert.Equal(RunPhase.Event, eaten.Phase);
        Assert.Equal(oldCount + 1, eaten.Player.Deck.Length);
        Assert.Equal(PrototypeNativeEndlessConveyor.FeedingFrenzyId,
            eaten.Player.Deck.Last().CardId);
        Assert.Equal(beforeGold - 40, eaten.Player.Gold);
        Assert.Equal(6, eaten.World!.Event!.NativeDishCount);
        Assert.True(PrototypeContent.Card(
            PrototypeNativeEndlessConveyor.FeedingFrenzyId)
            .MechanicsImplemented);
        Assert.DoesNotContain(
            PrototypeNativeEndlessConveyor.FeedingFrenzyId,
            PrototypeContent.RewardCardPool);

        var left = Choose(engine, eaten, "leave");
        Assert.Equal(RunPhase.MapChoice, left.Phase);
        Assert.Null(left.World!.Event);
    }

    [Fact]
    public void ConveyorCannotGrabWithLessThanFortyGold()
    {
        var engine = new PrototypeGameEngine();
        var current = FindEvent(
            engine, PrototypeNativeEndlessConveyor.EventId,
            "conveyor-broke", gold: 140);
        current = current with
        {
            Player = current.Player with { Gold = 39 }
        };
        Assert.Equal(new[] { "observe" }, ChoiceIds(engine, current));
        Assert.Equal(RunPhase.MapChoice,
            Choose(engine, current, "observe").Phase);
    }

    [Fact]
    public void PunchOffNabOffersOnlyRelicAndAddsInjury()
    {
        var engine = new PrototypeGameEngine();
        var current = FindEvent(
            engine, PrototypeNativePunchOff.EventId,
            "punch-nab", floor: 6);
        var before = current.Player.Deck.Length;
        Assert.Equal(new[] { "nab", "take_them" },
            ChoiceIds(engine, current));
        var chosen = Choose(engine, current, "nab");
        Assert.Equal(RunPhase.Reward, chosen.Phase);
        Assert.Equal(before + 1, chosen.Player.Deck.Length);
        Assert.Equal("proto.native.neow.injury",
            chosen.Player.Deck.Last().CardId);
        var reward = Assert.IsType<RewardState>(chosen.World!.Reward);
        Assert.StartsWith("Event:", reward.SourceRoom);
        Assert.True(reward.CardResolved);
        Assert.True(reward.PotionResolved);
        Assert.False(reward.RelicResolved);
        Assert.NotNull(reward.RelicOption);
    }

    [Fact]
    public void PunchOffFightUsesTwoWoundedConstructsAndNativeOpeningMoves()
    {
        var engine = new PrototypeGameEngine();
        var entered = FindEvent(
            engine, PrototypeNativePunchOff.EventId,
            "punch-fight", floor: 6);
        var challenged = Choose(engine, entered, "take_them");
        Assert.Equal(RunPhase.Event, challenged.Phase);
        Assert.Equal(new[] { "fight" }, ChoiceIds(engine, challenged));
        var started = Choose(engine, challenged, "fight");
        Assert.Equal(RunPhase.Combat, started.Phase);
        Assert.Equal(2, started.World!.Event!.NativePageIndex);
        var combat = started.World.Combat!;
        Assert.Equal(2, combat.Enemies.Length);
        Assert.All(combat.Enemies, enemy =>
        {
            Assert.Equal("proto.enemy.punch_construct", enemy.EnemyId);
            Assert.InRange(enemy.Hp, 46, 53);
            Assert.Contains(enemy.Powers!, power =>
                power.PowerId == "proto.power.artifact");
        });
        Assert.Equal(1, combat.Enemies[0].MoveIndex);
        Assert.Equal(0, combat.Enemies[1].MoveIndex);
        Assert.Equal(PrototypeNativePunchOff.EncounterId,
            started.World.EncounterIds.Last());
    }

    [Fact]
    public void PunchOffVictoryOffersRelicAndPotionInsteadOfGenericCards()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEvent(
            engine, PrototypeNativePunchOff.EventId,
            "punch-victory", floor: 6);
        state = Choose(engine, Choose(engine, state, "take_them"), "fight");
        var combat = state.World!.Combat!;
        var strikes = combat.Cards.Where(card =>
            card.CardId == "proto.silent.strike")
            .Take(2).ToArray();
        Assert.Equal(2, strikes.Length);
        var otherCards = combat.Cards.Where(card =>
            strikes.All(strike => strike.InstanceId != card.InstanceId))
            .Select(card => card.InstanceId).ToArray();
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = combat.Enemies.Select(enemy =>
                        enemy with { Hp = 1, Block = 0 }).ToArray(),
                    Hand = strikes.Select(card => card.InstanceId).ToArray(),
                    DrawPile = otherCards,
                    DiscardPile = [],
                    PlayPile = [],
                    ExhaustPile = [],
                    Energy = 3
                }
            }
        };
        foreach (var target in combat.Enemies)
        {
            var strike = engine.GetLegalActions(state).First(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().TargetEnemyId
                    == target.InstanceId
                && strikes.Any(card => card.InstanceId ==
                    action.ReadPayload<PlayCardPayload>().CardInstanceId));
            state = engine.Step(state, strike).State;
        }
        Assert.Equal(RunPhase.Reward, state.Phase);
        var reward = state.World!.Reward!;
        Assert.Empty(reward.CardOptions);
        Assert.True(reward.CardResolved);
        Assert.NotNull(reward.RelicOption);
        Assert.NotNull(reward.PotionOption);
        Assert.False(reward.RelicResolved);
        Assert.False(reward.PotionResolved);
    }


    [Fact]
    public void ConveyorWaitsForJellyLiverTransformBeforeRollingNextDish()
    {
        var engine = new PrototypeGameEngine();
        var entered = FindEvent(engine,
            PrototypeNativeEndlessConveyor.EventId,
            "conveyor-jelly-continuation", gold: 400);
        var forced = entered with
        {
            World = entered.World! with
            {
                Event = entered.World.Event! with
                {
                    NativeDishId = "JELLY_LIVER",
                    NativeLastDishId = "JELLY_LIVER"
                }
            }
        };
        var visible = new PrototypeAiEnvironment().Observe(forced)
            .Observation.Event!;
        Assert.Equal("JELLY_LIVER", visible.CurrentDishId);
        Assert.Equal(1, visible.CurrentDishNumber);

        var rngBefore = CanonicalJson.Sha256(forced.Rng);
        var pending = Choose(engine, forced, "grab");
        var eventBeforeChoice = pending.World!.Event!;
        Assert.True(eventBeforeChoice.NativeDishRollPending);
        Assert.Equal("JELLY_LIVER", eventBeforeChoice.NativeDishId);
        Assert.Equal(1, eventBeforeChoice.NativeDishCount);
        Assert.NotNull(eventBeforeChoice.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Transform,
            eventBeforeChoice.PendingDeckChoice.Kind);
        // The dish itself needs no event RNG until the transform target
        // is chosen; rolling the next dish here would advance it early.
        Assert.Equal(rngBefore, CanonicalJson.Sha256(pending.Rng));
        Assert.Equal(CanonicalJson.Sha256(pending),
            CanonicalJson.Sha256(pending.Fork()));

        var select = engine.GetLegalActions(pending).First(action =>
            action.Kind == "choose_event_deck_card");
        var resolved = engine.Step(pending, select).State;
        Assert.Equal(RunPhase.Event, resolved.Phase);
        Assert.False(resolved.World!.Event!.NativeDishRollPending);
        Assert.Null(resolved.World.Event.PendingDeckChoice);
        Assert.Equal(2, resolved.World.Event.NativeDishCount);
        Assert.NotNull(resolved.World.Event.NativeDishId);
        var newVisible = new PrototypeAiEnvironment()
            .Observe(resolved).Observation.Event!;
        Assert.Equal(resolved.World.Event.NativeDishId,
            newVisible.CurrentDishId);
        Assert.Equal(2, newVisible.CurrentDishNumber);
    }

    [Fact]
    public void ConveyorWaitsForCondimentPotionChoiceBeforeRolling()
    {
        var engine = new PrototypeGameEngine();
        var entered = FindEvent(engine,
            PrototypeNativeEndlessConveyor.EventId,
            "conveyor-condiment-continuation", gold: 400);
        var forced = entered with
        {
            World = entered.World! with
            {
                Event = entered.World.Event! with
                {
                    NativeDishId = "SUSPICIOUS_CONDIMENT",
                    NativeLastDishId = "SUSPICIOUS_CONDIMENT"
                }
            }
        };
        Assert.Contains(null, forced.Player.PotionSlots);
        var pending = Choose(engine, forced, "grab");
        Assert.Equal(RunPhase.Event, pending.Phase);
        Assert.True(pending.World!.Event!.NativeDishRollPending);
        Assert.NotNull(pending.World.Event.PendingPotionReplacement);
        Assert.Equal(1, pending.World.Event.NativeDishCount);
        Assert.Equal("SUSPICIOUS_CONDIMENT",
            pending.World.Event.NativeDishId);

        var skip = engine.GetLegalActions(pending).Single(action =>
            action.Kind == "skip_event_potion");
        var resolved = engine.Step(pending, skip).State;
        Assert.Equal(RunPhase.Event, resolved.Phase);
        Assert.False(resolved.World!.Event!.NativeDishRollPending);
        Assert.Null(resolved.World.Event.PendingPotionReplacement);
        Assert.Equal(2, resolved.World.Event.NativeDishCount);
    }

    [Fact]
    public void PunchOffEntryConsumesItsNativeDynamicGoldRoll()
    {
        var engine = new PrototypeGameEngine();
        var entered = FindEvent(engine, PrototypeNativePunchOff.EventId,
            "punch-entry-gold-roll", floor: 6);
        // PunchOff.CalculateVars samples NextInt(91,99) on event entry,
        // independently of its Injury/relic and combat branches.
        Assert.InRange(entered.World!.Event!.NativeEventGold, 91, 98);
        Assert.Equal(CanonicalJson.Sha256(entered),
            CanonicalJson.Sha256(entered.Fork()));
        Assert.Contains("nab", ChoiceIds(engine, entered));
        Assert.Contains("take_them", ChoiceIds(engine, entered));
    }

    private static string[] ChoiceIds(
        PrototypeGameEngine engine, RunState state) =>
        engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState Choose(
        PrototypeGameEngine engine, RunState state, string id)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "event_choice"
            && item.ReadPayload<EventChoicePayload>().ChoiceId == id);
        return engine.Step(state, action).State;
    }

    private static RunState FindEvent(
        PrototypeGameEngine engine, string eventId, string seed,
        int floor = 1, int gold = 400)
    {
        for (var i = 0; i < 256; i++)
        {
            var current = StartEvent(engine, seed + i, floor, gold);
            if (current.World!.Event!.EventId == eventId)
            {
                return current;
            }
        }
        throw new InvalidOperationException(
            $"Native event '{eventId}' never selected.");
    }

    private static RunState StartEvent(
        PrototypeGameEngine engine, string seed, int floor, int gold)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(seed);
        var node = new MapNodeState(
            "underdocks-final-event", 1, floor,
            PrototypeRoomType.Event, ["next-room"]);
        var next = new MapNodeState(
            "next-room", 1, floor + 1,
            PrototypeRoomType.Combat, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            Player = state.Player with { Gold = gold },
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
        return engine.Step(
            state, Assert.Single(engine.GetLegalActions(state))).State;
    }
}
