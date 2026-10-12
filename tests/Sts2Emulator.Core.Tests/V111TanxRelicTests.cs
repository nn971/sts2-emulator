using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111TanxRelicTests
{
    [Fact]
    public void IronClubDrawsEveryFourthCardAndPersistsItsCounter()
    {
        var (engine, state) = StartCombat(cardsPlayed: 0);
        var initialHandCount = state.World!.Combat!.Hand.Length;

        for (var cardNumber = 1; cardNumber <= 3; cardNumber++)
        {
            var cardId = state.World.Combat!.Hand[0];
            var action = engine.GetLegalActions(state).First(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>().CardInstanceId
                    == cardId);
            state = engine.Step(state, action).State;
            Assert.Equal(initialHandCount - cardNumber,
                state.World!.Combat!.Hand.Length);
        }

        var snapshotHash = CanonicalJson.Sha256(state);
        state = RunSnapshot.Load(RunSnapshot.Save(state));
        Assert.Equal(snapshotHash, CanonicalJson.Sha256(state));
        var playedCardId = state.World!.Combat!.Hand[0];
        var fourthCard = engine.GetLegalActions(state).First(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().CardInstanceId
                == playedCardId);
        state = engine.Step(state, fourthCard).State;

        Assert.Equal(initialHandCount - 3, state.World!.Combat!.Hand.Length);
        var relic = Assert.Single(state.Player.Relics, item =>
            item.RelicId == NativeGloryAncientsV111.IronClubId);
        Assert.Equal(4, relic.PersistentState.GetProperty("CardsPlayed")
            .GetInt32());
    }

    [Fact]
    public void IronClubRestoresItsCounterWhenTheNextCombatStarts()
    {
        var (engine, state) = StartCombat(cardsPlayed: 3);
        var initialHandCount = state.World!.Combat!.Hand.Length;
        state = RunSnapshot.Load(RunSnapshot.Save(state));
        var cardId = state.World!.Combat!.Hand[0];
        var action = engine.GetLegalActions(state).First(item =>
            item.Kind == "play_card"
            && item.ReadPayload<PlayCardPayload>().CardInstanceId == cardId);

        state = engine.Step(state, action).State;

        Assert.Equal(initialHandCount, state.World!.Combat!.Hand.Length);
        var relic = Assert.Single(state.Player.Relics, item =>
            item.RelicId == NativeGloryAncientsV111.IronClubId);
        Assert.Equal(4, relic.PersistentState.GetProperty("CardsPlayed")
            .GetInt32());
    }

    private static (PrototypeGameEngine Engine, RunState State) StartCombat(
        int cardsPlayed)
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(
            $"tanx-iron-club-{cardsPlayed}");
        state = engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Deck = Enumerable.Range(1, 10)
                    .Select(id => new CardInstance(id,
                        "proto.silent.shiv", 0, empty)).ToArray(),
                Relics = [..state.Player.Relics,
                    new RelicInstance(NativeGloryAncientsV111.IronClubId,
                        JsonSerializer.SerializeToElement(
                            new { CardsPlayed = cardsPlayed }))]
            },
            World = state.World! with
            {
                NextCardInstanceId = 11
            }
        };

        var entry = state.World!.Map.Nodes.First(node =>
            node.Floor == 1
            && node.RoomType == PrototypeRoomType.Combat
            && state.World.Map.EntryNodeIds!.Contains(node.NodeId));
        state = engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(entry.NodeId))).State;
        return (engine, state);
    }
}
