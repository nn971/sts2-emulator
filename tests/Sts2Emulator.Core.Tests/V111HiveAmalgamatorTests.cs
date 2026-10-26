using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveAmalgamatorTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void SourceEligibilityRequiresBothBasicTaggedPairs()
    {
        var definition = PrototypeContent.Event(
            PrototypeNativeHiveEvents.AmalgamatorId);
        Assert.Equal((2, 2, 0),
            (definition.MinAct, definition.MaxAct, definition.Weight));
        Assert.Equal(new[] {"combine_strikes", "combine_defends"},
            definition.Choices.Select(c => c.Id));
        var source = Fixture();
        Assert.True(PrototypeNativeHiveEvents.IsEligible(definition,
            source.Player));
        var reduced = source.Player with
        {
            Deck = source.Player.Deck.Skip(4).ToArray()
        };
        Assert.False(PrototypeNativeHiveEvents.IsEligible(definition,
            reduced));
    }

    [Theory]
    [InlineData("combine_strikes", "Strike",
        PrototypeNativeHiveEvents.UltimateStrikeId)]
    [InlineData("combine_defends", "Defend",
        PrototypeNativeHiveEvents.UltimateDefendId)]
    public void RemovesOnlyTwoMatchingBasicCardsBeforeCreatingUltimate(
        string choiceId, string tag, string expectedCard)
    {
        var state = Fixture();
        var original = CanonicalJson.Sha256(state);
        var initialDeck = state.Player.Deck;
        var nextId = state.World!.NextCardInstanceId;
        var action = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice" &&
            a.ReadPayload<EventChoicePayload>().ChoiceId == choiceId);
        state = Engine.Step(state, action).State;
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(initialDeck.Length, state.Player.Deck.Length);
        Assert.Equal(2, state.World!.Event!.PendingDeckChoice!.RemainingSelections);
        for (var i = 0; i < 2; i++)
        {
            var actions = Engine.GetLegalActions(state);
            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
            {
                var id = a.ReadPayload<ChooseEventDeckCardPayload>()
                    .CardInstanceId;
                var selected = state.Player.Deck.Single(c =>
                    c.InstanceId == id);
                var def = PrototypeContent.Card(selected.CardId);
                Assert.Equal(PrototypeCardRarity.Basic, def.Rarity);
                Assert.Contains(tag, def.Tags!);
            });
            state = Engine.Step(state, actions[0]).State;
        }
        state = PrototypeEventPageTestHelperV111.ContinueIfFinished(state);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(initialDeck.Length - 1, state.Player.Deck.Length);
        Assert.Equal(expectedCard, state.Player.Deck.Last().CardId);
        Assert.Equal(nextId, state.Player.Deck.Last().InstanceId);
        Assert.Equal(nextId + 1, state.World!.NextCardInstanceId);
        Assert.Equal(PrototypeCardRarity.Uncommon,
            PrototypeContent.Card(expectedCard).Rarity);
        Assert.Equal(original, CanonicalJson.Sha256(Fixture()));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    private static RunState Fixture()
    {
        var start = Engine.Step(PrototypeGameFactory.Create("hive-amalgamator"),
            GameAction.Empty("start_run")).State;
        var original = start.Player.Deck;
        var strikes = original.Where(c =>
            c.CardId == "proto.silent.strike").Take(2);
        var defends = original.Where(c =>
            c.CardId == "proto.silent.defend").Take(2);
        var deck = strikes.Concat(defends).ToArray();
        Assert.Equal(4, deck.Length);
        return start with
        {
            Phase = RunPhase.Event,
            Player = start.Player with { Deck = deck },
            World = start.World! with
            {
                Act = 2, Floor = 1, ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(PrototypeNativeHiveEvents.AmalgamatorId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
    }
}
