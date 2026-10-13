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
        var rngBeforeEvent = CanonicalJson.Sha256(state.Rng);
        var nativeRngBeforeEvent = state.NativeRunRng is null
            ? null : CanonicalJson.Sha256(state.NativeRunRng);
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
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(1, state.World!.Event!.NativePageIndex);
        Assert.Equal(initialDeck.Length - 1, state.Player.Deck.Length);
        Assert.Equal(expectedCard, state.Player.Deck.Last().CardId);
        Assert.Equal(nextId, state.Player.Deck.Last().InstanceId);
        Assert.Equal(nextId + 1, state.World!.NextCardInstanceId);
        Assert.Equal(PrototypeCardRarity.Uncommon,
            PrototypeContent.Card(expectedCard).Rarity);
        Assert.Equal(original, CanonicalJson.Sha256(Fixture()));
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(state))));
        Assert.Equal(rngBeforeEvent, CanonicalJson.Sha256(state.Rng));
        Assert.Equal(nativeRngBeforeEvent,
            state.NativeRunRng is null
                ? null : CanonicalJson.Sha256(state.NativeRunRng));
        var rngAtFinish = CanonicalJson.Sha256(state.Rng);
        Assert.Equal("continue_event",
            Assert.Single(Engine.GetLegalActions(state)).Kind);
        Assert.Throws<InvalidOperationException>(() => Engine.Step(
            state, GameAction.Empty("invalid_continue")));
        state = Engine.Step(state, GameAction.Empty("continue_event")).State;
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Null(state.World!.Event);
        Assert.Equal(rngAtFinish, CanonicalJson.Sha256(state.Rng));
        Assert.Equal(nativeRngBeforeEvent,
            state.NativeRunRng is null
                ? null : CanonicalJson.Sha256(state.NativeRunRng));
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
        var map = PrototypeNativeLaterActMap.GenerateNative(
            "hive-amalgamator", 2);
        var eventNode = map.Nodes.First(node => node.Floor == 2);
        return start with
        {
            Phase = RunPhase.Event,
            Player = start.Player with { Deck = deck },
            World = start.World! with
            {
                Act = 2, Floor = eventNode.Floor,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(PrototypeNativeHiveEvents.AmalgamatorId),
                EventHistory = [PrototypeNativeHiveEvents.AmalgamatorId],
                Map = map with { CurrentNodeId = eventNode.NodeId }
            }
        };
    }
}
