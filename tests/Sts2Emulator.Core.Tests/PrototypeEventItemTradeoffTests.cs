using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEventItemTradeoffTests
{
    [Fact]
    public void CagedVaultOpenGrantsEmptyCageAndUsesRelicDeckChoice()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 70,
            maxHp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.neutralize")
            ]);

        state = ChooseEvent(
            engine,
            state,
            "open");

        Assert.Equal(65, state.Player.MaxHp);
        Assert.Equal(65, state.Player.Hp);
        Assert.Contains(
            state.Player.Relics,
            relic => relic.RelicId
                == "proto.relic.empty_cage");

        var pending =
            state.World!.Event!.PendingDeckChoice!;
        Assert.Equal(
            "proto.relic.empty_cage",
            pending.SourceRelicId);
        Assert.Equal(
            PrototypePersistentDeckChoiceKind.Remove,
            pending.Kind);
        Assert.Equal(2, pending.RemainingSelections);

        var frame =
            new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            "proto.relic.empty_cage",
            frame.Observation.Event!
                .PendingDeckChoice!
                .SourceRelicId);

        state = ChooseDeckCard(engine, state, 1);
        Assert.Equal(RunPhase.Event, state.Phase);
        state = ChooseDeckCard(engine, state, 2);

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(
            new long[] { 3 },
            state.Player.Deck
                .Select(card => card.InstanceId)
                .ToArray());
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EventPotionCanBeDeclinedWithoutReplacingBelt()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 65,
            maxHp: 70,
            gold: 100,
            deck: [Card(1, "proto.silent.strike")],
            potionSlots:
            [
                Potion("proto.potion.block"),
                Potion("proto.potion.fire")
            ]);

        state = ChooseEvent(engine, state, "tonic");
        var actions = engine.GetLegalActions(state);
        Assert.Contains(actions, action => action.Kind == "skip_event_potion");
        Assert.Equal(2, actions.Count(action =>
            action.Kind == "replace_event_potion"));

        state = engine.Step(state,
            actions.Single(action => action.Kind == "skip_event_potion")).State;

        Assert.Equal(70, state.Player.Gold);
        Assert.Equal(65, state.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal("proto.potion.block",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal("proto.potion.fire",
            state.Player.PotionSlots[1]!.PotionId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UsingPotionDuringFullBeltEventOfferFillsFreedSlot()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 35,
            maxHp: 70,
            gold: 100,
            deck: [Card(1, "proto.silent.strike")],
            potionSlots:
            [
                Potion("proto.potion.blood"),
                Potion("proto.potion.block")
            ]);

        state = ChooseEvent(engine, state, "tonic");
        var use = engine.GetLegalActions(state)
            .Single(action => action.Kind == "use_potion");
        Assert.Equal(0, use.ReadPayload<UsePotionPayload>().Slot);
        state = engine.Step(state, use).State;

        Assert.Equal(49, state.Player.Hp);
        Assert.Equal(70, state.Player.Gold);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal("proto.potion.strength",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal("proto.potion.block",
            state.Player.PotionSlots[1]!.PotionId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void DecliningQueuedPotionStillResolvesSubsequentRewards()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.collectors_annex",
            act: 2,
            hp: 65,
            maxHp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            potionSlots:
            [
                Potion("proto.potion.block"),
                Potion("proto.potion.blood")
            ]);

        state = ChooseEvent(engine, state, "bundle");
        Assert.Equal(75, state.Player.Gold);
        Assert.Equal("proto.potion.strength",
            state.World!.Event!.PendingPotionReplacement!.PotionId);
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.Kind == "skip_event_potion")).State;

        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal("proto.potion.fire",
            state.World!.Event!.PendingPotionReplacement!.PotionId);
        Assert.Equal(75, state.Player.Gold);
        state = engine.Step(state,
            engine.GetLegalActions(state).Single(action =>
                action.Kind == "skip_event_potion")).State;

        Assert.Equal("proto.relic.empty_cage",
            state.World!.Event!.PendingDeckChoice!.SourceRelicId);
        Assert.Equal("proto.potion.block",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal("proto.potion.blood",
            state.Player.PotionSlots[1]!.PotionId);

        state = ChooseDeckCard(engine, state, 1);
        state = ChooseDeckCard(engine, state, 2);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Empty(state.Player.Deck);
        Assert.Equal(75, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EventCardAcquisitionAppliesExistingEggAndGoldRelics()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.collectors_annex",
            act: 2,
            hp: 70,
            maxHp: 70,
            gold: 30,
            deck: [],
            relicIds:
            [
                "proto.relic.toxic_egg",
                "proto.relic.lucky_fysh"
            ]);

        state = ChooseEvent(engine, state, "late_card");

        Assert.Equal(45, state.Player.Gold);
        var card = Assert.Single(state.Player.Deck);
        Assert.Equal("proto.silent.backflip", card.CardId);
        Assert.Equal(1, card.UpgradeLevel);
        Assert.Equal(
            "proto.relic.empty_cage",
            state.World!.Event!.PendingDeckChoice!.SourceRelicId);

        state = ChooseDeckCard(engine, state, card.InstanceId);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Empty(state.Player.Deck);
        Assert.Equal(45, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EntropicBrewRefillPreservesPendingReplacementValidity()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 70,
            maxHp: 70,
            gold: 100,
            deck: [Card(1, "proto.silent.strike")],
            potionSlots:
            [
                Potion("proto.potion.entropic_brew"),
                Potion("proto.potion.block")
            ]);

        state = ChooseEvent(engine, state, "tonic");
        var use = engine.GetLegalActions(state).Single(action =>
            action.Kind == "use_potion");
        state = engine.Step(state, use).State;

        // Entropic Brew refills the emptied slot immediately. The
        // offered potion therefore still requires an explicit choice.
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.NotNull(state.Player.PotionSlots[0]);
        Assert.Equal("proto.potion.strength",
            state.World!.Event!.PendingPotionReplacement!.PotionId);
        Assert.Equal(new[] { 0, 1 },
            state.World.Event.PendingPotionReplacement.CandidateSlots);
        PrototypeStateInvariants.Validate(state);

        state = engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "skip_event_potion")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(70, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CagedVaultCofferFiresRelicAcquiredHooks()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 70,
            maxHp: 70,
            gold: 10,
            deck:
            [
                Card(1, "proto.silent.strike")
            ]);

        state = ChooseEvent(
            engine,
            state,
            "coffer");

        Assert.Equal(55, state.Player.Hp);
        Assert.Equal(310, state.Player.Gold);
        Assert.Contains(
            state.Player.Relics,
            relic => relic.RelicId
                == "proto.relic.old_coin");
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }

    [Fact]
    public void FullBeltEventPotionAcquisitionBecomesReplacementDecision()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 70,
            maxHp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike")
            ],
            potionSlots:
            [
                Potion("proto.potion.block"),
                Potion("proto.potion.fire")
            ]);

        state = ChooseEvent(
            engine,
            state,
            "tonic");

        Assert.Equal(70, state.Player.Gold);
        Assert.Equal(RunPhase.Event, state.Phase);

        var pending =
            state.World!.Event!
                .PendingPotionReplacement!;
        Assert.Equal(
            "proto.potion.strength",
            pending.PotionId);
        Assert.Equal(
            new[] { 0, 1 },
            pending.CandidateSlots);

        var frame =
            new PrototypeAiEnvironment().Observe(state);
        Assert.Equal(
            "proto.potion.strength",
            frame.Observation.Event!
                .PendingPotionReplacement!
                .PotionId);
        Assert.Equal(
            2,
            frame.LegalActions.Count(action =>
                action.Kind
                    == "replace_event_potion"));

        state = ReplacePotion(
            engine,
            state,
            1);

        Assert.Equal(70, state.Player.Gold);
        Assert.Equal(
            "proto.potion.block",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal(
            "proto.potion.strength",
            state.Player.PotionSlots[1]!.PotionId);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void EventPotionUsesEmptySlotWithoutContinuation()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 70,
            maxHp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike")
            ],
            potionSlots:
            [
                Potion("proto.potion.block"),
                null
            ]);

        state = ChooseEvent(
            engine,
            state,
            "tonic");

        Assert.Equal(70, state.Player.Gold);
        Assert.Equal(
            "proto.potion.strength",
            state.Player.PotionSlots[1]!.PotionId);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
    }

    [Fact]
    public void SozuSuppressesPotionTradeoffEventChoice()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.caged_vault",
            act: 1,
            hp: 70,
            maxHp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike")
            ],
            relicIds:
            [
                "proto.relic.sozu"
            ]);

        var choices = engine.GetLegalActions(state)
            .Where(action =>
                action.Kind == "event_choice")
            .Select(action =>
                action.ReadPayload<EventChoicePayload>()
                    .ChoiceId)
            .ToArray();

        Assert.DoesNotContain(
            "tonic",
            choices);
        Assert.Contains(
            "open",
            choices);
        Assert.Contains(
            "leave",
            choices);
    }

    [Fact]
    public void ForbiddenArchiveAddsPowerfulCardAndPersistentDownside()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.forbidden_archive",
            act: 2,
            hp: 60,
            maxHp: 70,
            gold: 0,
            deck:
            [
                Card(10, "proto.silent.strike")
            ]);

        state = ChooseEvent(
            engine,
            state,
            "read");

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Contains(
            state.Player.Deck,
            card => card.CardId
                == "proto.silent.wraith_form");
        Assert.Contains(
            state.Player.Deck,
            card => card.CardId
                == "proto.status.infection");

        var added = state.Player.Deck
            .Where(card => card.InstanceId > 10)
            .OrderBy(card => card.InstanceId)
            .ToArray();
        Assert.Equal(2, added.Length);
        Assert.Equal(
            "proto.silent.wraith_form",
            added[0].CardId);
        Assert.Equal(
            "proto.status.infection",
            added[1].CardId);
        Assert.True(
            state.World!.NextCardInstanceId
                > added[^1].InstanceId);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CollectorsAnnexResolvesMultiplePotionAndDeckChoicesInSequence()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.collectors_annex",
            act: 2,
            hp: 70,
            maxHp: 70,
            gold: 100,
            deck:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.strike"),
                Card(4, "proto.silent.defend"),
                Card(5, "proto.silent.backflip"),
                Card(6, "proto.silent.neutralize")
            ],
            potionSlots:
            [
                Potion("proto.potion.block"),
                Potion("proto.potion.block")
            ]);

        state = ChooseEvent(engine, state, "bundle");
        Assert.Equal(75, state.Player.Gold);
        Assert.Equal(2, state.Player.Relics.Length);
        Assert.Equal(
            "proto.potion.strength",
            state.World!.Event!.PendingPotionReplacement!.PotionId);
        Assert.Equal(new[] { "proto.potion.fire" },
            state.World.Event.RemainingPotionIds);
        Assert.Equal(3, state.World.Event.RemainingDeckChoices.Length);
        Assert.Equal(3,
            new PrototypeAiEnvironment().Observe(state)
                .Observation.Event!.QueuedDeckChoices!.Length);

        var fork = state.Fork();
        fork.World!.Event!.QueuedDeckChoices![0] =
            fork.World.Event.QueuedDeckChoices[0] with
            {
                RemainingSelections = 999
            };
        Assert.NotEqual(
            999,
            state.World.Event.RemainingDeckChoices[0]
                .RemainingSelections);

        state = ReplacePotion(engine, state, 0);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(
            "proto.potion.fire",
            state.World!.Event!.PendingPotionReplacement!.PotionId);
        state = ReplacePotion(engine, state, 1);
        Assert.Equal("proto.potion.strength",
            state.Player.PotionSlots[0]!.PotionId);
        Assert.Equal("proto.potion.fire",
            state.Player.PotionSlots[1]!.PotionId);
        Assert.Equal(
            "proto.relic.empty_cage",
            state.World!.Event!.PendingDeckChoice!.SourceRelicId);

        state = ChooseDeckCard(engine, state, 1);
        Assert.Equal(75, state.Player.Gold);
        state = ChooseDeckCard(engine, state, 2);
        Assert.Equal(
            "proto.relic.astrolabe",
            state.World!.Event!.PendingDeckChoice!.SourceRelicId);
        Assert.DoesNotContain(1,
            state.World.Event.PendingDeckChoice.CandidateCardInstanceIds);
        Assert.DoesNotContain(2,
            state.World.Event.PendingDeckChoice.CandidateCardInstanceIds);

        state = ChooseDeckCard(engine, state, 3);
        state = ChooseDeckCard(engine, state, 4);
        state = ChooseDeckCard(engine, state, 5);
        var finalUpgrade = state.World!.Event!.PendingDeckChoice!;
        Assert.Null(finalUpgrade.SourceRelicId);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Upgrade,
            finalUpgrade.Kind);
        Assert.Equal(new long[] { 6 },
            finalUpgrade.CandidateCardInstanceIds);

        state = ChooseDeckCard(engine, state, 6);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(75, state.Player.Gold);
        Assert.Equal(new long[] { 3, 4, 5, 6 },
            state.Player.Deck.Select(card => card.InstanceId).ToArray());
        Assert.All(state.Player.Deck,
            card => Assert.Equal(1, card.UpgradeLevel));
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CompositePotionAcquisitionUsesEmptySlotThenPrompts()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.collectors_annex",
            act: 2,
            hp: 70,
            maxHp: 70,
            gold: 75,
            deck:
            [
                Card(1, "proto.silent.strike"),
                Card(2, "proto.silent.defend")
            ],
            potionSlots:
            [
                Potion("proto.potion.block"),
                null
            ]);
        state = ChooseEvent(engine, state, "bundle");

        Assert.Equal(50, state.Player.Gold);
        Assert.Equal("proto.potion.strength",
            state.Player.PotionSlots[1]!.PotionId);
        Assert.Equal("proto.potion.fire",
            state.World!.Event!.PendingPotionReplacement!.PotionId);
        Assert.Empty(state.World.Event.RemainingPotionIds);
        PrototypeStateInvariants.Validate(state);

        state = ReplacePotion(engine, state, 0);
        Assert.Equal(
            "proto.relic.empty_cage",
            state.World!.Event!.PendingDeckChoice!.SourceRelicId);
        state = ChooseDeckCard(engine, state, 1);
        state = ChooseDeckCard(engine, state, 2);

        // Empty Cage removed every card. Later Astrolabe and the event
        // upgrade must be skipped, not offer stale card-instance IDs.
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Empty(state.Player.Deck);
        Assert.Equal(50, state.Player.Gold);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void RelicDeckRequestIsDeferredUntilLaterCardsAreAdded()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.collectors_annex",
            act: 2,
            hp: 70,
            maxHp: 70,
            gold: 0,
            deck: []);

        state = ChooseEvent(engine, state, "late_card");
        var newlyAdded = Assert.Single(state.Player.Deck);
        Assert.Equal("proto.silent.backflip", newlyAdded.CardId);
        Assert.Equal(
            "proto.relic.empty_cage",
            state.World!.Event!.PendingDeckChoice!.SourceRelicId);
        Assert.Equal(
            new[] { newlyAdded.InstanceId },
            state.World.Event.PendingDeckChoice.CandidateCardInstanceIds);

        state = ChooseDeckCard(engine, state, newlyAdded.InstanceId);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Empty(state.Player.Deck);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void CompositeEventRespectsUpfrontCostsAndDuplicateRelics()
    {
        var engine = new PrototypeGameEngine();
        var state = CreateEventState(
            "proto.event.collectors_annex",
            act: 2,
            hp: 70,
            maxHp: 70,
            gold: 24,
            deck: [Card(1, "proto.silent.strike")]);

        var choices = engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();
        Assert.DoesNotContain("bundle", choices);
        Assert.Contains("leave", choices);

        state = state with
        {
            Player = state.Player with
            {
                Gold = 100,
                Relics =
                [
                    new RelicInstance(
                        "proto.relic.astrolabe",
                        PrototypeJson.EmptyObject())
                ]
            }
        };
        choices = engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();
        Assert.DoesNotContain("bundle", choices);
    }

    private static RunState ChooseEvent(
        PrototypeGameEngine engine,
        RunState state,
        string choiceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "event_choice"
                && item.ReadPayload<EventChoicePayload>()
                    .ChoiceId == choiceId);
        return engine.Step(state, action).State;
    }

    private static RunState ChooseDeckCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind
                    == "choose_event_deck_card"
                && item.ReadPayload<
                    ChooseEventDeckCardPayload>()
                    .CardInstanceId
                    == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState ReplacePotion(
        PrototypeGameEngine engine,
        RunState state,
        int slot)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind
                    == "replace_event_potion"
                && item.ReadPayload<
                    ReplaceEventPotionPayload>()
                    .Slot == slot);
        return engine.Step(state, action).State;
    }

    private static CardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            cardId,
            0,
            PrototypeJson.EmptyObject());

    private static PotionInstance Potion(
        string potionId) =>
        new(
            potionId,
            PrototypeJson.EmptyObject());

    private static RunState CreateEventState(
        string eventId,
        int act,
        int hp,
        int maxHp,
        int gold,
        CardInstance[] deck,
        string[]? relicIds = null,
        PotionInstance?[]? potionSlots = null)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            hp,
            maxHp,
            gold,
            deck,
            (relicIds ?? Array.Empty<string>())
                .Select(id =>
                    new RelicInstance(
                        id,
                        empty))
                .ToArray(),
            potionSlots
                ?? new PotionInstance?[
                    PrototypeContent.Rules.PotionSlots]);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "event-item-tradeoff",
            "event-item-tradeoff",
            0,
            RunPhase.Event,
            player,
            PrototypeRng.CreateBundle(
                "event-item-tradeoff"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                act,
                2,
                deck.Select(card => card.InstanceId)
                    .DefaultIfEmpty(0)
                    .Max() + 1,
                PrototypeRoomType.Event,
                ValidActMap(act),
                null,
                null,
                null,
                new Sts2Emulator.Core.EventState(
                    eventId),
                null,
                EventHistory: [eventId]));
    }

    private static MapState ValidActMap(
        int act) =>
        new(
            [
                new MapNodeState(
                    "entry",
                    act,
                    1,
                    PrototypeRoomType.Combat,
                    ["event"]),
                new MapNodeState(
                    "event",
                    act,
                    2,
                    PrototypeRoomType.Event,
                    ["floor3"]),
                new MapNodeState(
                    "floor3",
                    act,
                    3,
                    PrototypeRoomType.Combat,
                    ["floor4"]),
                new MapNodeState(
                    "floor4",
                    act,
                    4,
                    PrototypeRoomType.Combat,
                    ["rest"]),
                new MapNodeState(
                    "rest",
                    act,
                    5,
                    PrototypeRoomType.Rest,
                    ["boss"]),
                new MapNodeState(
                    "boss",
                    act,
                    6,
                    PrototypeRoomType.Boss,
                    [])
            ],
            CurrentNodeId: "event",
            EntryNodeIds: ["entry"]);
}
