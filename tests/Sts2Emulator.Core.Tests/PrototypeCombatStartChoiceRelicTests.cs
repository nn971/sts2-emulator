using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCombatStartChoiceRelicTests
{
    [Fact]
    public void ToolboxSuspendsCombatStartForThreeCardChoice()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(
            "toolbox-combat-start");
        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state))).State;

        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance(
                        "proto.relic.toolbox",
                        empty)
                ]
            },
            World = state.World! with
            {
                Map = new MapState(
                    [
                        new MapNodeState(
                            "combat",
                            1,
                            1,
                            PrototypeRoomType.Combat,
                            [])
                    ],
                    EntryNodeIds: ["combat"])
            }
        };

        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload(
                    "combat"))).State;

        var combat = state.World!.Combat!;
        var pending =
            Assert.IsType<PendingCombatChoiceState>(
                combat.PendingChoice);
        Assert.Equal(
            PrototypeCardZone.ChoicePool,
            pending.Selection.SourceZone);
        Assert.Equal(3, combat.ChoiceCardIds.Length);
        Assert.Equal(
            3,
            combat.ChoiceCardIds
                .Distinct()
                .Count());

        Assert.All(
            combat.ChoiceCardIds,
            id =>
            {
                var card = combat.Cards.Single(
                    item => item.InstanceId == id);
                Assert.True(card.IsTemporary);
                Assert.Null(card.TemporaryEnergyCost);
                var definition =
                    PrototypeContent.Card(
                        card.CardId);
                Assert.True(
                    definition
                        .CanBeGeneratedInCombat);
                Assert.True(
                    definition
                        .MechanicsImplemented);
                Assert.False(
                    definition
                        .MultiplayerOnly);
            });

        var legal = engine.GetLegalActions(state);
        Assert.Equal(4, legal.Count);
        Assert.Single(
            legal,
            action =>
                action
                    .ReadPayload<
                        SelectCardsPayload>()
                    .CardInstanceIds.Length == 0);
        Assert.Equal(
            3,
            legal.Count(action =>
                action
                    .ReadPayload<
                        SelectCardsPayload>()
                    .CardInstanceIds.Length == 1));

        PrototypeStateInvariants.Validate(
            state);
    }

    [Fact]
    public void ToolboxChoiceResumesCombatStartAndKeepsOnlyChosenCard()
    {
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(
            "toolbox-combat-start-resolution");
        state = engine.Step(
            state,
            Assert.Single(
                engine.GetLegalActions(state))).State;

        var empty = PrototypeJson.EmptyObject();
        state = state with
        {
            Player = state.Player with
            {
                Relics =
                [
                    new RelicInstance(
                        "proto.relic.toolbox",
                        empty)
                ]
            },
            World = state.World! with
            {
                Map = new MapState(
                    [
                        new MapNodeState(
                            "combat",
                            1,
                            1,
                            PrototypeRoomType.Combat,
                            [])
                    ],
                    EntryNodeIds: ["combat"])
            }
        };

        state = engine.Step(
            state,
            GameAction.Create(
                "choose_map_node",
                new ChooseMapNodePayload(
                    "combat"))).State;

        var before =
            state.World!.Combat!;
        var candidates =
            (long[])before
                .ChoiceCardIds.Clone();
        var choose = engine
            .GetLegalActions(state)
            .First(action =>
                action
                    .ReadPayload<
                        SelectCardsPayload>()
                    .CardInstanceIds.Length == 1);
        var chosenId = choose
            .ReadPayload<SelectCardsPayload>()
            .CardInstanceIds[0];

        state = engine.Step(
            state,
            choose).State;

        var combat =
            state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Empty(
            combat.ChoiceCardIds);
        Assert.Contains(
            chosenId,
            combat.Hand);
        Assert.All(
            candidates.Where(
                id => id != chosenId),
            id => Assert.DoesNotContain(
                combat.Cards,
                card =>
                    card.InstanceId == id));
        Assert.Null(
            combat.Cards.Single(
                    card =>
                        card.InstanceId
                        == chosenId)
                .TemporaryEnergyCost);

        PrototypeStateInvariants.Validate(
            state);
    }
}
