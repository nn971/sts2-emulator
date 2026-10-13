using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111TanxMeatCleaverTests
{
    [Fact]
    public void MeatCleaverCooksTwoRemovableCardsForFiveMaxHp()
    {
        var state = CreateRestState();
        var engine = new PrototypeGameEngine();
        var legal = engine.GetLegalActions(state);
        Assert.Contains(legal, action => action.Kind == "rest_cook");
        var cook = legal.First(action => action.Kind == "rest_cook");
        var selected = cook.ReadPayload<SelectCardsPayload>()
            .CardInstanceIds;

        var cooked = engine.Step(state, cook).State;

        Assert.Equal(2, selected.Length);
        Assert.Equal(2, cooked.Player.Deck.Length);
        Assert.DoesNotContain(cooked.Player.Deck,
            card => selected.Contains(card.InstanceId));
        Assert.Equal(40, cooked.Player.Hp);
        Assert.Equal(75, cooked.Player.MaxHp);
        Assert.Equal(RunPhase.MapChoice, cooked.Phase);
        PrototypeStateInvariants.Validate(cooked);
    }

    [Fact]
    public void MeatCleaverCookRequiresTwoRemovableDeckCards()
    {
        var state = CreateRestState(deckSize: 1);

        Assert.DoesNotContain(new PrototypeGameEngine()
                .GetLegalActions(state),
            action => action.Kind == "rest_cook");
    }

    [Fact]
    public void CookIsUnavailableWithoutMeatCleaver()
    {
        var state = CreateRestState(hasMeatCleaver: false);

        Assert.DoesNotContain(new PrototypeGameEngine()
                .GetLegalActions(state),
            action => action.Kind == "rest_cook");
    }

    private static RunState CreateRestState(
        int deckSize = 4,
        bool hasMeatCleaver = true)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = Enumerable.Range(1, deckSize)
            .Select(id => new CardInstance(id,
                id % 2 == 0 ? "proto.silent.defend" : "proto.silent.strike",
                0, empty)).ToArray();
        var player = new PlayerState(
            Hp: 35,
            MaxHp: 70,
            Gold: 0,
            Deck: deck,
            Relics: hasMeatCleaver
                ? [new RelicInstance(
                    NativeGloryAncientsV111.MeatCleaverId, empty)]
                : [],
            PotionSlots: new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var nodes = new[]
        {
            new MapNodeState("rest:1", 1, 1,
                PrototypeRoomType.Combat, ["rest:2"]),
            new MapNodeState("rest:2", 1, 2,
                PrototypeRoomType.Combat, ["rest:3"]),
            new MapNodeState("rest:3", 1, 3,
                PrototypeRoomType.Combat, ["rest:4"]),
            new MapNodeState("rest:4", 1, 4,
                PrototypeRoomType.Combat, ["rest:5"]),
            new MapNodeState("rest:5", 1, 5,
                PrototypeRoomType.Rest, ["rest:6"]),
            new MapNodeState("rest:6", 1, 6,
                PrototypeRoomType.Boss, Array.Empty<string>())
        };
        var seed = $"tanx-meat-cleaver-rest-{deckSize}";

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Rest,
            player,
            PrototypeRng.CreateBundle(seed),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                5,
                deckSize + 1,
                PrototypeRoomType.Rest,
                new MapState(nodes,
                    CurrentNodeId: "rest:5",
                    EntryNodeIds: ["rest:1"]),
                null,
                null,
                null,
                null,
                null));
    }
}
