using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryHungryForMushroomsTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string Hungry = PrototypeNativeGloryEvents.HungryForMushroomsId;
    private const string Big = PrototypeNativeGloryEvents.BigMushroomId;
    private const string Fragrant =
        PrototypeNativeGloryEvents.FragrantMushroomId;

    [Fact]
    public void CatalogueIsGatedAndContainsBothEventRelics()
    {
        var evt = PrototypeContent.Event(Hungry);
        Assert.Equal((3, 3, 0),
            (evt.MinAct, evt.MaxAct, evt.Weight));
        Assert.Equal(new[] { "big_mushroom", "fragrant_mushroom" },
            evt.Choices.Select(choice => choice.Id));
        Assert.Equal(-2,
            PrototypeContent.Relic(Big).FirstTurnDrawBonus);
        Assert.NotNull(PrototypeContent.Relic(Fragrant));
        Assert.DoesNotContain(Big, PrototypeContent.RelicPool);
        Assert.DoesNotContain(Fragrant, PrototypeContent.RelicPool);
    }

    [Fact]
    public void BigMushroomGainsTwentyHpAndMaxHpAndPreservesFork()
    {
        var initial = Fixture("v111-glory-big");
        var initialHash = CanonicalJson.Sha256(initial);
        var result = Choose(initial, "big_mushroom");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(initial.Player.MaxHp + 20, result.Player.MaxHp);
        Assert.Equal(initial.Player.Hp + 20, result.Player.Hp);
        Assert.Single(result.Player.Relics, r => r.RelicId == Big);
        Assert.Null(result.World!.Event);
        Assert.Equal(initialHash, CanonicalJson.Sha256(initial));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choose(initial.Fork(), "big_mushroom"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void FragrantMushroomDamagesThenUpgradesTwoDifferentCards()
    {
        var initial = Fixture("v111-glory-fragrant");
        var candidates = initial.Player.Deck
            .Where(card => card.UpgradeLevel == 0
                && PrototypeContent.Card(card.CardId).MaxUpgradeLevel > 0)
            .ToArray();
        Assert.True(candidates.Length >= 2);
        var startHash = CanonicalJson.Sha256(initial);

        var result = Choose(initial, "fragrant_mushroom");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(initial.Player.Hp - 15, result.Player.Hp);
        Assert.Equal(initial.Player.MaxHp, result.Player.MaxHp);
        Assert.Single(result.Player.Relics, r => r.RelicId == Fragrant);
        var changed = result.Player.Deck.Where(card =>
            initial.Player.Deck.Single(original =>
                original.InstanceId == card.InstanceId).UpgradeLevel
                != card.UpgradeLevel).ToArray();
        Assert.Equal(2, changed.Length);
        Assert.All(changed, card => Assert.Equal(1, card.UpgradeLevel));
        Assert.Equal(startHash, CanonicalJson.Sha256(initial));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choose(initial.Fork(), "fragrant_mushroom"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void FragrantNeverUpgradesWhenPickupDamageIsFatal()
    {
        var initial = Fixture("v111-glory-fragrant-lethal");
        initial = initial with
        {
            Player = initial.Player with { Hp = 12 }
        };
        var result = Choose(initial, "fragrant_mushroom");
        Assert.Equal(RunPhase.Terminal, result.Phase);
        Assert.Equal("defeat", result.World!.TerminalOutcome);
        Assert.Equal(0, result.Player.Hp);
        Assert.Single(result.Player.Relics, r => r.RelicId == Fragrant);
        Assert.Equal(initial.Player.Deck.Select(c => c.UpgradeLevel),
            result.Player.Deck.Select(c => c.UpgradeLevel));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void BigMushroomDrawsTwoFewerCardsOnTheFirstCombatTurn()
    {
        // Enter the source-pinned Lantern Key forced fight: a test
        // encounter that does not depend on unimplemented Glory bags.
        var baseline = BattleFixture(withBig: false);
        var withMushroom = BattleFixture(withBig: true);
        Assert.Equal(baseline.World!.Combat!.Hand.Length - 2,
            withMushroom.World!.Combat!.Hand.Length);
        Assert.Equal(PrototypeContent.Rules.HandSize + 2,
            baseline.World.Combat.Hand.Length);
        Assert.Equal(PrototypeContent.Rules.HandSize,
            withMushroom.World.Combat.Hand.Length);
    }

    [Fact]
    public void ForeignOrUnrecognizedChoiceIsRejectedWithoutMutation()
    {
        var initial = Fixture("v111-glory-invalid-choice");
        var hash = CanonicalJson.Sha256(initial);
        Assert.Throws<InvalidOperationException>(() =>
            Choose(initial, "fake_choice"));
        Assert.Equal(hash, CanonicalJson.Sha256(initial));
    }

    private static RunState Fixture(string seed) =>
        PrototypeNativeLaterActFixture.CreateGloryHungryForMushrooms(seed);

    private static RunState Choose(RunState state, string id) =>
        Engine.Step(state, Engine.GetLegalActions(state)
            .SingleOrDefault(a => a.Kind == "event_choice"
                && a.ReadPayload<EventChoicePayload>().ChoiceId == id)
            ?? GameAction.Create("event_choice",
                new EventChoicePayload(id))).State;

    private static RunState BattleFixture(bool withBig)
    {
        var start = Engine.Step(
            PrototypeGameFactory.Create("v111-mushroom-draw"),
            GameAction.Empty("start_run")).State;
        var relics = withBig
            ? start.Player.Relics.Append(new RelicInstance(
                Big, PrototypeJson.EmptyObject())).ToArray()
            : start.Player.Relics;
        var state = start with
        {
            Phase = RunPhase.Event,
            Player = start.Player with { Relics = relics },
            World = start.World! with
            {
                Act = 2, Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.LanternKeyEventId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
        state = Engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload("keep_key"))).State;
        state = Engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload("fight"))).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        return state;
    }
}
