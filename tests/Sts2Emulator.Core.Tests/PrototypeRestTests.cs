using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRestTests
{
    [Fact]
    public void TrainingTradesCampfireForPermanentMaxHp()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            Hp: 35,
            MaxHp: 70,
            Gold: 0,
            Deck:
            [
                new CardInstance(1, "proto.silent.strike", 0, empty)
            ],
            Relics: Array.Empty<RelicInstance>(),
            PotionSlots: new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var nodes = new[]
        {
            new MapNodeState("r:1", 1, 1, PrototypeRoomType.Combat, ["r:2"]),
            new MapNodeState("r:2", 1, 2, PrototypeRoomType.Combat, ["r:3"]),
            new MapNodeState("r:3", 1, 3, PrototypeRoomType.Combat, ["r:4"]),
            new MapNodeState("r:4", 1, 4, PrototypeRoomType.Combat, ["r:5"]),
            new MapNodeState("r:5", 1, 5, PrototypeRoomType.Rest, ["r:6"]),
            new MapNodeState("r:6", 1, 6, PrototypeRoomType.Boss, Array.Empty<string>())
        };

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "rest-train-test",
            "rest-train-test",
            0,
            RunPhase.Rest,
            player,
            PrototypeRng.CreateBundle("rest-train-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                5,
                2,
                PrototypeRoomType.Rest,
                new MapState(
                    nodes,
                    CurrentNodeId: "r:5",
                    EntryNodeIds: ["r:1"]),
                null,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        PrototypeStateInvariants.Validate(state);

        var legal = engine.GetLegalActions(state);
        Assert.Contains(legal, action => action.Kind == "rest_heal");
        Assert.Contains(legal, action => action.Kind == "rest_train");
        Assert.Contains(legal, action => action.Kind == "rest_upgrade");

        var train = legal.Single(action => action.Kind == "rest_train");
        state = engine.Step(state, train).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(74, state.Player.MaxHp);
        Assert.Equal(39, state.Player.Hp);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Single(engine.GetLegalActions(state));
    }
}
