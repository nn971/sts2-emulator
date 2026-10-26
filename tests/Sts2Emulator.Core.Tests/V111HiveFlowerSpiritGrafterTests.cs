using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveFlowerSpiritGrafterTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void SourceEligibilityAndGatedActTwoDefinitions()
    {
        var flower = PrototypeContent.Event(
            PrototypeNativeHiveEvents.ColossalFlowerId);
        var grafter = PrototypeContent.Event(
            PrototypeNativeHiveEvents.SpiritGrafterId);
        Assert.Equal((2, 2, 0), (flower.MinAct, flower.MaxAct, flower.Weight));
        Assert.Equal((2, 2, 0), (grafter.MinAct, grafter.MaxAct, grafter.Weight));
        Assert.False(PrototypeNativeHiveEvents.IsEligible(flower,
            EventRun(flower.Id, hp: 18).Player));
        Assert.True(PrototypeNativeHiveEvents.IsEligible(flower,
            EventRun(flower.Id, hp: 19).Player));
        Assert.True(PrototypeNativeHiveEvents.IsEligible(grafter,
            EventRun(grafter.Id, hp: 1).Player));
        Assert.Equal(new[] { "let_it_in", "rejection" },
            grafter.Choices.Select(c => c.Id));
        Assert.Contains("ColossalFlower",
            PrototypeNativeHiveEvents.NativeEventNames);
        Assert.Contains("SpiritGrafter",
            PrototypeNativeHiveEvents.NativeEventNames);
    }

    [Theory]
    [InlineData(0, 35)]
    [InlineData(1, 75)]
    [InlineData(2, 135)]
    public void ColossalFlowerExtractsStageSpecificGold(
        int digs, int payout)
    {
        var state = EventRun(PrototypeNativeHiveEvents.ColossalFlowerId,
            hp: 70, gold: 40);
        for (var i = 0; i < digs; i++)
        {
            var hash = CanonicalJson.Sha256(state);
            Assert.Equal(new[] { "extract", "dig" },
                LegalEventChoices(state));
            state = Select(state, "dig");
            Assert.Equal(RunPhase.Event, state.Phase);
            Assert.Equal(i + 1, state.World!.Event!.NativePageIndex);
        }

        Assert.Equal(digs == 2
                ? new[] { "extract", "relic" }
                : new[] { "extract", "dig" },
            LegalEventChoices(state));
        var resultingHp = 70 - (digs >= 1 ? 5 : 0)
            - (digs >= 2 ? 6 : 0);
        var selected = Select(state, "extract");
        selected = PrototypeEventPageTestHelperV111.ContinueIfFinished(selected);
        Assert.Equal(RunPhase.MapChoice, selected.Phase);
        Assert.Equal(resultingHp, selected.Player.Hp);
        Assert.Equal(40 + payout, selected.Player.Gold);
        Assert.DoesNotContain(selected.Player.Relics, r =>
            r.RelicId == PrototypeNativeHiveEvents.PollinousCoreId);
    }

    [Fact]
    public void FlowerThirdDigPaysSevenHpAndGrantsRelicInsteadOfGold()
    {
        var state = EventRun(PrototypeNativeHiveEvents.ColossalFlowerId,
            hp: 19, gold: 100);
        var original = state;
        state = Select(Select(state, "dig"), "dig");
        Assert.Equal(8, state.Player.Hp);
        Assert.Equal(new[] { "extract", "relic" },
            LegalEventChoices(state));

        var invalidHash = CanonicalJson.Sha256(state);
        Assert.Throws<InvalidOperationException>(() => Select(state, "dig"));
        Assert.Equal(invalidHash, CanonicalJson.Sha256(state));
        var payout = Select(state, "extract");
        Assert.Equal(235, payout.Player.Gold);
        Assert.Equal(8, payout.Player.Hp);

        var relic = Select(state, "relic");
        relic = PrototypeEventPageTestHelperV111.ContinueIfFinished(relic);
        Assert.Equal(RunPhase.MapChoice, relic.Phase);
        Assert.Equal(1, relic.Player.Hp);
        Assert.Equal(100, relic.Player.Gold);
        Assert.Contains(relic.Player.Relics, r =>
            r.RelicId == PrototypeNativeHiveEvents.PollinousCoreId);
        Assert.Equal(CanonicalJson.Sha256(original),
            CanonicalJson.Sha256(original.Fork()));
        Assert.Equal(CanonicalJson.Sha256(relic),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Select(state.Fork(), "relic"))));
    }

    [Fact]
    public void GrafterLetItInHealsAndAddsOneMetamorphosis()
    {
        var state = EventRun(PrototypeNativeHiveEvents.SpiritGrafterId,
            hp: 60, gold: 0);
        var id = state.World!.NextCardInstanceId;
        var result = Select(state, "let_it_in");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(70, result.Player.Hp);
        Assert.Equal(0, result.Player.Gold);
        Assert.Equal(state.Player.Deck.Length + 1,
            result.Player.Deck.Length);
        Assert.Equal(id, result.Player.Deck.Last().InstanceId);
        Assert.Equal(PrototypeNativeHiveEvents.MetamorphosisId,
            result.Player.Deck.Last().CardId);
        Assert.Equal(id + 1, result.World!.NextCardInstanceId);
    }

    [Fact]
    public void GrafterRejectionUpgradesBeforeUnblockableHpLoss()
    {
        var state = EventRun(PrototypeNativeHiveEvents.SpiritGrafterId,
            hp: 30);
        var initialHash = CanonicalJson.Sha256(state);
        var selected = Select(state, "rejection");
        Assert.Equal(RunPhase.Event, selected.Phase);
        Assert.Equal(30, selected.Player.Hp);
        var choice = Engine.GetLegalActions(selected).First(a =>
            a.Kind == "choose_event_deck_card");
        var cardId = choice.ReadPayload<ChooseEventDeckCardPayload>()
            .CardInstanceId;
        var original = state.Player.Deck.Single(c =>
            c.InstanceId == cardId).UpgradeLevel;
        var result = Engine.Step(selected, choice).State;
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(20, result.Player.Hp);
        Assert.Equal(original + 1,
            result.Player.Deck.Single(c =>
                c.InstanceId == cardId).UpgradeLevel);
        Assert.Equal(initialHash, CanonicalJson.Sha256(state));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    public void MetamorphosisCreatesCombatFreeAttacksInDrawPile(
        int upgrade, int expected)
    {
        var state = CombatRun(upgrade);
        var play = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card" &&
            a.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        var initialHash = CanonicalJson.Sha256(state);
        var result = Engine.Step(state, play).State;
        var combat = result.World!.Combat!;
        Assert.Contains(1L, combat.ExhaustPile);
        Assert.Equal(expected, combat.DrawPile.Length);
        Assert.Equal(expected, combat.Cards.Count(c => c.IsTemporary));
        Assert.All(combat.DrawPile, id =>
        {
            var card = combat.Cards.Single(c => c.InstanceId == id);
            var def = PrototypeContent.Card(card.CardId);
            Assert.Equal(PrototypeCardType.Attack, def.Type);
            Assert.True(def.MechanicsImplemented);
            Assert.False(def.MultiplayerOnly);
            Assert.Contains(card.CardId, PrototypeContent.NativeSilentCardPool);
            Assert.Null(card.PersistentCardInstanceId);
            Assert.Equal(0, card.TemporaryEnergyCost!.Cost);
            Assert.Equal(PrototypeTemporaryCardCostExpiry.None,
                card.TemporaryEnergyCost.Expiry);
        });
        Assert.Equal(initialHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Engine.Step(state.Fork(), play).State)));
    }

    [Fact]
    public void PollinousCoreCounterIncludesOpeningDrawAndResetsAtFour()
    {
        var state = EventRun(PrototypeNativeHiveEvents.SpiritGrafterId,
            hp: 1000);
        var relic = new RelicInstance(
            PrototypeNativeHiveEvents.PollinousCoreId,
            PrototypeJson.EmptyObject());
        var node = new MapNodeState("combat", 1, 1,
            PrototypeRoomType.Combat, []);
        state = state with
        {
            Phase = RunPhase.MapChoice,
            Player = state.Player with
            {
                Relics = state.Player.Relics.Append(relic).ToArray(),
                MaxHp = 1000,
                Hp = 1000
            },
            World = state.World! with
            {
                Act = 1,
                Floor = 0,
                ActiveRoom = null,
                Combat = null,
                Event = null,
                Map = new MapState([node], EntryNodeIds: ["combat"])
            }
        };
        state = Engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload("combat"))).State;
        Assert.Equal(1, Counter(state));
        Assert.Equal(RunPhase.Combat, state.Phase);
        for (int i = 2; i <= 4; i++)
        {
            var previous = CanonicalJson.Sha256(state);
            state = Engine.Step(state, Engine.GetLegalActions(state)
                .Single(a => a.Kind == "end_turn")).State;
            Assert.Equal(RunPhase.Combat, state.Phase);
            Assert.Equal(i == 4 ? 0 : i, Counter(state));
            Assert.Equal(CanonicalJson.Sha256(state),
                CanonicalJson.Sha256(state.Fork()));
        }
        Assert.True(state.World!.Combat!.Hand.Length >= 7);
    }

    private static int Counter(RunState state) =>
        state.Player.Relics.Single(r =>
            r.RelicId == PrototypeNativeHiveEvents.PollinousCoreId)
            .PersistentState.GetProperty("TurnsSeen").GetInt32();

    private static RunState Select(RunState state, string choice)
    {
        var action = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choice);
        return Engine.Step(state, action).State;
    }

    private static string[] LegalEventChoices(RunState state) =>
        Engine.GetLegalActions(state)
            .Select(a => a.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState EventRun(string id, int hp = 70,
        int gold = 99)
    {
        var start = Engine.Step(
            PrototypeGameFactory.Create("hive-v3"), GameAction.Empty("start_run")).State;
        return start with
        {
            Phase = RunPhase.Event,
            Player = start.Player with { Hp = hp, Gold = gold },
            World = start.World! with
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

    private static RunState CombatRun(int upgrade)
    {
        var empty = PrototypeJson.EmptyObject();
        var card = new CombatCardInstance(1, 100,
            PrototypeNativeHiveEvents.MetamorphosisId, upgrade, false, empty);
        var enemy = new EnemyCombatState(1,
            "proto.enemy.crawler", 100, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal));
        var combat = new CombatState(
            Turn: 1, Energy: 3, PlayerBlock: 0,
            Hand: [1], DrawPile: [], DiscardPile: [],
            ExhaustPile: [], Enemies: [enemy],
            NextCardInstanceId: 2, Cards: [card],
            PlayerPowers: [], NextPowerApplicationOrder: 1);
        return new RunState(
            "prototype-unbound", "prototype-0.1",
            "hive-v3-combat", "hive-v3-combat", 0,
            RunPhase.Combat,
            new PlayerState(70, 70, 99,
            [
                new CardInstance(100, card.CardId, upgrade, empty)
            ], [], new PotionInstance?[2]),
            PrototypeRng.CreateBundle("hive-v3-combat"), empty,
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 200,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));
    }
}
