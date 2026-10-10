using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveAmalgamatorHolesTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void SourceEligibilityChecksTwoBasicTagsAndPerfectFitCandidate()
    {
        var amalgamator = PrototypeContent.Event(
            PrototypeNativeHiveEvents.AmalgamatorId);
        var holes = PrototypeContent.Event(
            PrototypeNativeHiveEvents.FieldOfManSizedHolesId);
        Assert.Equal((2, 2, 0),
            (amalgamator.MinAct, amalgamator.MaxAct, amalgamator.Weight));
        Assert.Equal((2, 2, 0),
            (holes.MinAct, holes.MaxAct, holes.Weight));
        var start = EventRun(amalgamator.Id);
        Assert.True(PrototypeNativeHiveEvents.IsEligible(
            amalgamator, start.Player));
        Assert.True(PrototypeNativeHiveEvents.IsEligible(
            holes, start.Player));
        var fewDefends = start.Player with
        {
            Deck = start.Player.Deck
                .Where(c => c.CardId != "proto.silent.defend")
                .Concat(start.Player.Deck.Where(c =>
                    c.CardId == "proto.silent.defend").Take(1))
                .ToArray()
        };
        Assert.False(PrototypeNativeHiveEvents.IsEligible(
            amalgamator, fewDefends));
        var noEnchantable = start.Player with
        {
            Deck = [new CardInstance(
                101, PrototypeNativeHiveEvents.NormalityId, 0,
                PrototypeJson.EmptyObject())]
        };
        Assert.False(PrototypeNativeHiveEvents.IsEligible(
            holes, noEnchantable));
        Assert.Equal(new[] { "combine_strikes", "combine_defends" },
            Choices(start));
    }

    [Theory]
    [InlineData("combine_strikes", "proto.silent.strike",
        "proto.colorless.ultimate_strike", "proto.silent.defend")]
    [InlineData("combine_defends", "proto.silent.defend",
        "proto.colorless.ultimate_defend", "proto.silent.strike")]
    public void AmalgamatorRemovesExactlyTwoTaggedBasicsThenAddsOneUltimate(
        string option, string originalId, string ultimateId, string otherId)
    {
        var state = EventRun(PrototypeNativeHiveEvents.AmalgamatorId);
        var stateHash = CanonicalJson.Sha256(state);
        var initial = state.Player.Deck.Length;
        var unchanged = state.Player.Deck
            .Where(c => c.CardId == otherId)
            .Select(c => c.InstanceId).Order().ToArray();
        var nextId = state.World!.NextCardInstanceId;
        var afterChoice = Select(state, option);
        Assert.Equal(RunPhase.Event, afterChoice.Phase);
        Assert.Equal(initial, afterChoice.Player.Deck.Length);
        Assert.Equal(new[] { originalId },
            Engine.GetLegalActions(afterChoice)
                .Select(a => afterChoice.Player.Deck.Single(c =>
                    c.InstanceId == a.ReadPayload<ChooseEventDeckCardPayload>()
                        .CardInstanceId).CardId).Distinct());

        var first = Engine.GetLegalActions(afterChoice)
            .Single(a => a.Kind == "choose_event_deck_card"
                && a.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId == state.Player.Deck
                        .First(c => c.CardId == originalId).InstanceId);
        var afterFirst = Engine.Step(afterChoice, first).State;
        Assert.Equal(RunPhase.Event, afterFirst.Phase);
        Assert.Equal(initial - 1, afterFirst.Player.Deck.Length);
        Assert.Equal(nextId, afterFirst.World!.NextCardInstanceId);

        var second = Engine.GetLegalActions(afterFirst).First(
            a => a.Kind == "choose_event_deck_card"
                && afterFirst.Player.Deck.Single(c =>
                    c.InstanceId == a.ReadPayload<ChooseEventDeckCardPayload>()
                        .CardInstanceId).CardId == originalId
                && a.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId != first
                        .ReadPayload<ChooseEventDeckCardPayload>()
                        .CardInstanceId);
        var result = Engine.Step(afterFirst, second).State;
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(initial - 1, result.Player.Deck.Length);
        Assert.Equal(ultimateId, result.Player.Deck.Last().CardId);
        Assert.Equal(nextId, result.Player.Deck.Last().InstanceId);
        Assert.Equal(nextId + 1, result.World!.NextCardInstanceId);
        Assert.Equal(unchanged, result.Player.Deck
            .Where(c => c.CardId == otherId)
            .Select(c => c.InstanceId).Order().ToArray());
        Assert.Equal(stateHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(result.Fork()));
    }

    [Fact]
    public void AmalgamatorCannotFeedUncommonUltimateBackOrUseWrongTag()
    {
        var state = EventRun(PrototypeNativeHiveEvents.AmalgamatorId);
        var pending = Select(state, "combine_strikes");
        var allowed = Engine.GetLegalActions(pending).Select(a =>
            a.ReadPayload<ChooseEventDeckCardPayload>().CardInstanceId)
            .ToArray();
        Assert.All(allowed, id =>
        {
            var def = PrototypeContent.Card(
                state.Player.Deck.Single(c => c.InstanceId == id).CardId);
            Assert.Equal(PrototypeCardRarity.Basic, def.Rarity);
            Assert.Contains("Strike", def.Tags!);
        });
        var wrong = state.Player.Deck.First(c =>
            c.CardId == "proto.silent.defend").InstanceId;
        Assert.DoesNotContain(wrong, allowed);
        Assert.Throws<InvalidOperationException>(() =>
            Engine.Step(pending, GameAction.Create(
                "choose_event_deck_card",
                new ChooseEventDeckCardPayload(wrong))));
    }

    [Fact]
    public void HolesResistRemovesTwoThenAddsNormalityCurse()
    {
        var state = EventRun(PrototypeNativeHiveEvents.FieldOfManSizedHolesId);
        var hash = CanonicalJson.Sha256(state);
        var count = state.Player.Deck.Length;
        var first = Select(state, "resist");
        Assert.Equal(RunPhase.Event, first.Phase);
        Assert.DoesNotContain(first.Player.Deck, c =>
            c.CardId == PrototypeNativeHiveEvents.NormalityId);
        for (var n = 0; n < 2; n++)
        {
            var pick = Engine.GetLegalActions(first).First(a =>
                a.Kind == "choose_event_deck_card");
            first = Engine.Step(first, pick).State;
            if (n == 0)
                Assert.Equal(RunPhase.Event, first.Phase);
        }
        Assert.Equal(RunPhase.MapChoice, first.Phase);
        Assert.Equal(count - 1, first.Player.Deck.Length);
        Assert.Equal(PrototypeNativeHiveEvents.NormalityId,
            first.Player.Deck.Last().CardId);
        var curse = PrototypeContent.Card(
            PrototypeNativeHiveEvents.NormalityId);
        Assert.True(curse.Unplayable);
        Assert.Equal(0, curse.MaxUpgradeLevel);
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void HolesEnterYourHoleEnchantPersistsAndExcludesCurses()
    {
        var state = EventRun(PrototypeNativeHiveEvents.FieldOfManSizedHolesId);
        var hash = CanonicalJson.Sha256(state);
        var original = state.Player.Deck.ToDictionary(c =>
            c.InstanceId, c => c.CardId);
        var pending = Select(state, "enter");
        Assert.Equal(RunPhase.Event, pending.Phase);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Enchant,
            pending.World!.Event!.PendingDeckChoice!.Kind);
        var choice = Engine.GetLegalActions(pending).First(a =>
            a.Kind == "choose_event_deck_card");
        var id = choice.ReadPayload<ChooseEventDeckCardPayload>().CardInstanceId;
        var result = Engine.Step(pending, choice).State;
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(original.Count, result.Player.Deck.Length);
        var enchanted = result.Player.Deck.Single(c => c.InstanceId == id);
        Assert.Equal(original[id], enchanted.CardId);
        Assert.Equal(PrototypeCardEnchantmentKind.PerfectFit,
            enchanted.Enchantment?.Kind);
        Assert.Equal(1, enchanted.Enchantment?.Amount);
        Assert.Equal(hash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(result.Fork()));
    }

    [Fact]
    public void NormalityCapsCardPlayOnlyWhileHeldInHand()
    {
        var state = CombatRun(withNormality: true, played: 3);
        Assert.DoesNotContain(Engine.GetLegalActions(state), a =>
            a.Kind == "play_card");
        Assert.Throws<InvalidOperationException>(() =>
            Engine.Step(state, GameAction.Create("play_card",
                new PlayCardPayload(1, null))));

        var without = state with
        {
            World = state.World! with
            {
                Combat = state.World.Combat! with
                {
                    Hand = [1]
                }
            }
        };
        Assert.Contains(Engine.GetLegalActions(without), a =>
            a.Kind == "play_card");
        var beforeLimit = state with
        {
            World = state.World! with
            {
                Combat = state.World.Combat! with
                {
                    Counters = new PrototypeCombatCounters(
                        CardsPlayedThisTurn: 2)
                }
            }
        };
        Assert.Contains(Engine.GetLegalActions(beforeLimit), a =>
            a.Kind == "play_card");
    }

    [Fact]
    public void PerfectFitIsDrawnFirstAfterDiscardReshuffle()
    {
        var first = CombatRun(withNormality: false, played: 0,
            withPerfectFitDiscard: true);
        var hash = CanonicalJson.Sha256(first);
        var action = Assert.Single(Engine.GetLegalActions(first),
            a => a.Kind == "play_card" &&
                a.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        var result = Engine.Step(first, action).State;
        var combat = result.World!.Combat!;
        Assert.Contains(3L, combat.Hand);
        Assert.DoesNotContain(3L, combat.DrawPile);
        Assert.Equal(hash, CanonicalJson.Sha256(first));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Engine.Step(first.Fork(), action).State));
    }

    private static RunState EventRun(string id)
    {
        var initial = Engine.Step(PrototypeGameFactory.Create("hive-v4"),
            GameAction.Empty("start_run")).State;
        return initial with
        {
            Phase = RunPhase.Event,
            World = initial.World! with
            {
                Act = 2, Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(id),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
    }

    private static string[] Choices(RunState state) =>
        Engine.GetLegalActions(state).Select(a =>
            a.ReadPayload<EventChoicePayload>().ChoiceId).ToArray();

    private static RunState Select(RunState state, string choice) =>
        Engine.Step(state, Engine.GetLegalActions(state)
            .Single(a => a.Kind == "event_choice"
                && a.ReadPayload<EventChoicePayload>().ChoiceId == choice))
            .State;

    private static RunState CombatRun(bool withNormality,
        int played, bool withPerfectFitDiscard = false)
    {
        var empty = PrototypeJson.EmptyObject();
        var finesse = new CombatCardInstance(
            1, 101, "proto.colorless.finesse", 0, false, empty);
        var normality = new CombatCardInstance(
            2, 102, PrototypeNativeHiveEvents.NormalityId,
            0, false, empty);
        var perfectFit = new CombatCardInstance(
            3, 103, "proto.silent.strike", 0, false, empty,
            Enchantment: new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.PerfectFit));
        var plain = new CombatCardInstance(
            4, 104, "proto.silent.defend", 0, false, empty);
        var cards = withPerfectFitDiscard
            ? new[] { finesse, perfectFit, plain }
            : withNormality
                ? new[] { finesse, normality }
                : new[] { finesse };
        var deck = cards.Select(c => new CardInstance(
            c.PersistentCardInstanceId!.Value, c.CardId,
            c.UpgradeLevel, empty, c.Enchantment)).ToArray();
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: withNormality ? [1, 2] : [1],
            DrawPile: [], DiscardPile:
                withPerfectFitDiscard ? [3, 4] : [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(1, "proto.enemy.crawler",
                    100, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 5, Cards: cards,
            PlayerPowers: [], NextPowerApplicationOrder: 1,
            Counters: new PrototypeCombatCounters(
                CardsPlayedThisTurn: played));
        return new RunState(
            "prototype-unbound", "prototype-0.1",
            "hive-v4-combat", "hive-v4-combat", 0, RunPhase.Combat,
            new PlayerState(70, 70, 100, deck, [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle("hive-v4-combat"), empty,
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 800,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));
    }
}
