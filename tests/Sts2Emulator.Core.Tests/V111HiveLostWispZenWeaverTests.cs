using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveLostWispZenWeaverTests
{
    [Fact]
    public void BothEventsRemainGatedAndCarryPinnedChoices()
    {
        var lost = PrototypeContent.Event(PrototypeNativeHiveEvents.LostWispId);
        var zen = PrototypeContent.Event(PrototypeNativeHiveEvents.ZenWeaverId);
        Assert.Equal(0, lost.Weight);
        Assert.Equal(0, zen.Weight);
        Assert.Equal((2, 2), (lost.MinAct, lost.MaxAct));
        Assert.Equal((2, 2), (zen.MinAct, zen.MaxAct));
        Assert.Equal(new[] { "claim", "search" },
            lost.Choices.Select(x => x.Id));
        Assert.Equal(new[] { "breathing", "emotional", "arachnid" },
            zen.Choices.Select(x => x.Id));
        Assert.False(PrototypeNativeHiveEvents.IsEligible(zen,
            EventRun(PrototypeNativeHiveEvents.ZenWeaverId, 124).Player));
        Assert.True(PrototypeNativeHiveEvents.IsEligible(zen,
            EventRun(PrototypeNativeHiveEvents.ZenWeaverId, 125).Player));
        Assert.True(PrototypeNativeHiveEvents.IsEligible(lost,
            EventRun(PrototypeNativeHiveEvents.LostWispId, 0).Player));
    }

    [Fact]
    public void LostWispClaimAndGoldSearchAreMutuallyExclusiveAndForkSafe()
    {
        var engine = new PrototypeGameEngine();
        var state = EventRun(PrototypeNativeHiveEvents.LostWispId, 300,
            eventGold: 71);
        var sourceHash = CanonicalJson.Sha256(state);
        var claim = Select(engine, state, "claim");
        claim = PrototypeEventPageTestHelperV111.ContinueIfFinished(claim);
        Assert.Equal(RunPhase.MapChoice, claim.Phase);
        Assert.Equal(300, claim.Player.Gold);
        Assert.Equal(state.Player.Deck.Length + 1, claim.Player.Deck.Length);
        Assert.Equal(PrototypeNativeHiveEvents.DecayId,
            claim.Player.Deck.Last().CardId);
        Assert.Contains(claim.Player.Relics, r =>
            r.RelicId == PrototypeNativeHiveEvents.LostWispRelicId);

        var search = Select(engine, state, "search");
        search = PrototypeEventPageTestHelperV111.ContinueIfFinished(search);
        Assert.Equal(RunPhase.MapChoice, search.Phase);
        Assert.Equal(371, search.Player.Gold);
        Assert.Equal(state.Player.Deck.Length, search.Player.Deck.Length);
        Assert.DoesNotContain(search.Player.Relics, r =>
            r.RelicId == PrototypeNativeHiveEvents.LostWispRelicId);
        Assert.Equal(sourceHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(search),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Select(engine, state.Fork(), "search"))));
    }

    [Fact]
    public void ZenWeaverSeparatesEligibilityAndThreePriceTiers()
    {
        var engine = new PrototypeGameEngine();
        var state = EventRun(PrototypeNativeHiveEvents.ZenWeaverId, 125);
        Assert.Equal(new[] { "breathing", "emotional" },
            LegalEventChoices(engine, state));
        Assert.Equal(new[] { "breathing", "emotional", "arachnid" },
            LegalEventChoices(engine,
                EventRun(PrototypeNativeHiveEvents.ZenWeaverId, 250)));
    }

    [Fact]
    public void BreathingTechniquesPurchasesTwoIndependentEnlightenments()
    {
        var engine = new PrototypeGameEngine();
        var state = EventRun(PrototypeNativeHiveEvents.ZenWeaverId, 125);
        var nextId = state.World!.NextCardInstanceId;
        var result = Select(engine, state, "breathing");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(75, result.Player.Gold);
        Assert.Equal(state.Player.Deck.Length + 2, result.Player.Deck.Length);
        Assert.Equal(new[] { nextId, nextId + 1 },
            result.Player.Deck.TakeLast(2).Select(c => c.InstanceId));
        Assert.All(result.Player.Deck.TakeLast(2), c =>
            Assert.Equal(PrototypeNativeHiveEvents.EnlightenmentId, c.CardId));
        var definition = PrototypeContent.Card(
            PrototypeNativeHiveEvents.EnlightenmentId);
        Assert.True(definition.ExhaustOnUse);
        Assert.False(definition.RewardEligible);
    }

    [Theory]
    [InlineData("emotional", 1, 125)]
    [InlineData("arachnid", 2, 250)]
    public void PaidRemovalChargesAfterItsLastSelectedCard(
        string choice, int count, int price)
    {
        var engine = new PrototypeGameEngine();
        var state = EventRun(PrototypeNativeHiveEvents.ZenWeaverId, 300);
        var originalHash = CanonicalJson.Sha256(state);
        var originalIds = state.Player.Deck.Select(c => c.InstanceId).ToArray();

        var current = Select(engine, state, choice);
        Assert.Equal(RunPhase.Event, current.Phase);
        Assert.Equal(300, current.Player.Gold);
        Assert.NotNull(current.World!.Event!.PendingDeckChoice);
        for (int i = 0; i < count; i++)
        {
            var action = engine.GetLegalActions(current)
                .First(a => a.Kind == "choose_event_deck_card");
            current = engine.Step(current, action).State;
            if (i < count - 1)
            {
                Assert.Equal(RunPhase.Event, current.Phase);
                Assert.Equal(300, current.Player.Gold);
            }
        }

        current = PrototypeEventPageTestHelperV111.ContinueIfFinished(current);
        Assert.Equal(RunPhase.MapChoice, current.Phase);
        Assert.Equal(300 - price, current.Player.Gold);
        Assert.Equal(originalIds.Length - count, current.Player.Deck.Length);
        Assert.Equal(count, originalIds.Except(
            current.Player.Deck.Select(c => c.InstanceId)).Count());
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(current),
            CanonicalJson.Sha256(current.Fork()));
    }

    [Fact]
    public void DecayIsUnupgradableAndDealsTwoBlockableTurnEndDamage()
    {
        var c = PrototypeContent.Card(PrototypeNativeHiveEvents.DecayId);
        Assert.True(c.Unplayable);
        Assert.Equal(PrototypeCardType.Curse, c.Type);
        Assert.Equal(PrototypeCardRarity.Curse, c.Rarity);
        Assert.Equal(0, c.MaxUpgradeLevel);
        Assert.Equal(2, c.EndTurnDamageIfInHand);
        Assert.False(c.EndTurnDamageUnblockable);
    }

    [Fact]
    public void LostWispRelicDealsEightUnpoweredDamageToEveryEnemyOnPower()
    {
        var relic = PrototypeContent.Relic(
            PrototypeNativeHiveEvents.LostWispRelicId);
        var trigger = Assert.Single(relic.Triggers!);
        Assert.Equal(PrototypeCombatEventKind.CardPlayed, trigger.EventKind);
        Assert.Equal(PrototypeCardType.Power,
            trigger.RequiredSourceCardType);
        Assert.Equal(PrototypeEffectTarget.AllEnemies,
            Assert.Single(trigger.Effects).Target);

        var engine = new PrototypeGameEngine();
        var state = CombatWith(
            "proto.silent.accuracy", 0, withLostWisp: true);
        var play = Assert.Single(engine.GetLegalActions(state),
            action => action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        var result = engine.Step(state, play).State;
        Assert.Equal(new[] { 92, 92 },
            result.World!.Combat!.Enemies.Select(e => e.Hp));
    }

    [Theory]
    [InlineData(0, PrototypeTemporaryCardCostExpiry.EndOfTurn
                    | PrototypeTemporaryCardCostExpiry.WhenPlayed)]
    [InlineData(1, PrototypeTemporaryCardCostExpiry.None)]
    public void EnlightenmentReducesOnlyExpensiveHandCardsWithNativeLifetime(
        int upgrade, PrototypeTemporaryCardCostExpiry duration)
    {
        var engine = new PrototypeGameEngine();
        var state = CombatWith(
            PrototypeNativeHiveEvents.EnlightenmentId, upgrade);
        var play = Assert.Single(engine.GetLegalActions(state),
            action => action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == 1);
        var result = engine.Step(state, play).State;
        var combat = result.World!.Combat!;
        Assert.Contains(1L, combat.ExhaustPile);
        var expensive = combat.Cards.Single(c => c.InstanceId == 2);
        Assert.NotNull(expensive.TemporaryEnergyCost);
        Assert.Equal(1, expensive.TemporaryEnergyCost!.Cost);
        Assert.Equal(duration, expensive.TemporaryEnergyCost.Expiry);
        var free = combat.Cards.Single(c => c.InstanceId == 3);
        Assert.Null(free.TemporaryEnergyCost);

        var end = engine.GetLegalActions(result).Single(x =>
            x.Kind == "end_turn");
        var later = engine.Step(result, end).State;
        var preserved = later.World!.Combat!.Cards.Single(c => c.InstanceId == 2);
        if (upgrade == 0)
        {
            Assert.Null(preserved.TemporaryEnergyCost);
        }
        else
        {
            Assert.Equal(1, preserved.TemporaryEnergyCost?.Cost);
        }
    }

    private static string[] LegalEventChoices(PrototypeGameEngine engine,
        RunState state) => engine.GetLegalActions(state)
            .Where(action => action.Kind == "event_choice")
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState Select(PrototypeGameEngine engine,
        RunState state, string choice) =>
        engine.Step(state, engine.GetLegalActions(state).Single(action =>
            action.Kind == "event_choice"
            && action.ReadPayload<EventChoicePayload>().ChoiceId == choice)).State;

    private static RunState EventRun(string id, int gold,
        int eventGold = 60)
    {
        var engine = new PrototypeGameEngine();
        var initial = engine.Step(PrototypeGameFactory.Create("hive-event-v2"),
            GameAction.Empty("start_run")).State;
        var world = initial.World!;
        return initial with
        {
            Phase = RunPhase.Event,
            Player = initial.Player with { Gold = gold },
            World = world with
            {
                Act = 2,
                Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(id, NativeEventGold: eventGold),
                Map = new MapState(
                [
                    new("current", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "current", ["current"])
            }
        };
    }

    private static RunState CombatWith(string cardId, int upgrade,
        bool withLostWisp = false)
    {
        var empty = PrototypeJson.EmptyObject();
        var combatCards = new[]
        {
            new CombatCardInstance(1, 101, cardId, upgrade, false, empty),
            new CombatCardInstance(2, 102, "proto.silent.leg_sweep",
                0, false, empty),
            new CombatCardInstance(3, 103, "proto.silent.neutralize",
                0, false, empty)
        };
        var relics = withLostWisp
            ? new[]
            {
                new RelicInstance(PrototypeNativeHiveEvents.LostWispRelicId,
                    empty)
            }
            : [];
        var combatRelics = withLostWisp
            ? new[]
            {
                new CombatRelicState(0,
                    PrototypeNativeHiveEvents.LostWispRelicId,
                    1L, new int[1])
            }
            : [];
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 999,
            Hand: [1, 2, 3], DrawPile: [], DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new(1, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new(2, "proto.enemy.crawler", 100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 4,
            Cards: combatCards, PlayerPowers: [],
            NextPowerApplicationOrder: withLostWisp ? 2 : 1,
            Relics: combatRelics);
        return new RunState("prototype-unbound", "prototype-0.1",
            "hive-event-v2-combat", "hive-event-v2-combat", 0,
            RunPhase.Combat,
            new PlayerState(70, 70, 300,
                combatCards.Select(c => new CardInstance(
                    c.PersistentCardInstanceId!.Value,
                    c.CardId, c.UpgradeLevel, empty)).ToArray(),
                relics, new PotionInstance?[2]),
            PrototypeRng.CreateBundle("hive-event-v2-combat"), empty,
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 500,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));
    }
}
