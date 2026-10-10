using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeGenerationTests
{
    [Fact]
    public void RewardPoolContainsOnlyNonBasicCardsAndIncludesMultipleRarities()
    {
        Assert.NotEmpty(PrototypeContent.RewardCardPool);
        Assert.All(
            PrototypeContent.RewardCardPool,
            cardId => Assert.NotEqual(
                PrototypeCardRarity.Basic,
                PrototypeContent.Card(cardId).Rarity));

        var rarities = PrototypeContent.RewardCardPool
            .Select(cardId => PrototypeContent.Card(cardId).Rarity)
            .Distinct()
            .ToArray();

        Assert.Contains(PrototypeCardRarity.Common, rarities);
        Assert.Contains(PrototypeCardRarity.Uncommon, rarities);
        Assert.Contains(PrototypeCardRarity.Rare, rarities);
    }

    [Fact]
    public void ActOneFloorOneCombatUsesOvergrowthWeakPool()
    {
        var engine = new PrototypeGameEngine();
        RunState? combatState = null;

        for (var seedIndex = 0; seedIndex < 100 && combatState is null; seedIndex++)
        {
            var state = PrototypeGameFactory.Create($"early-encounter-{seedIndex}");
            state = engine.Step(
                state,
                Assert.Single(engine.GetLegalActions(state))).State;

            var combatChoice = engine.GetLegalActions(state)
                .FirstOrDefault(action =>
                {
                    if (action.Kind != "choose_map_node")
                    {
                        return false;
                    }

                    var payload = action.ReadPayload<ChooseMapNodePayload>();
                    var node = state.World!.Map.AvailableNodes()
                        .Single(item => item.NodeId == payload.NodeId);
                    return node.RoomType == PrototypeRoomType.Combat;
                });

            if (combatChoice is null)
            {
                continue;
            }

            combatState = engine.Step(state, combatChoice).State;
        }

        Assert.NotNull(combatState);
        PrototypeStateInvariants.Validate(combatState!);
        Assert.Contains(
            Assert.Single(combatState!.World!.EncounterIds),
            PrototypeContent.OvergrowthWeakEncounterPool);
    }
    [Theory]
    [InlineData(1, "proto.enemy.boss")]
    [InlineData(2, "proto.enemy.boss_two")]
    [InlineData(3, "proto.enemy.boss_three")]
    public void BossEncounterSelectionIsActSpecific(
        int act,
        string expectedEnemyId)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(1, "proto.silent.strike", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var nodes = Enumerable.Range(1, PrototypeContent.Rules.FloorsPerAct)
            .Select(floor =>
            {
                var isBoss = floor == PrototypeContent.Rules.FloorsPerAct;
                var nodeId = $"test:{act}:{floor}";
                var next = isBoss
                    ? Array.Empty<string>()
                    : new[] { $"test:{act}:{floor + 1}" };
                var room = floor switch
                {
                    5 => PrototypeRoomType.Rest,
                    var value when value == PrototypeContent.Rules.FloorsPerAct
                        => PrototypeRoomType.Boss,
                    _ => PrototypeRoomType.Combat
                };
                return new MapNodeState(
                    nodeId,
                    act,
                    floor,
                    room,
                    next);
            })
            .ToArray();

        var map = new MapState(
            nodes,
            CurrentNodeId: $"test:{act}:{PrototypeContent.Rules.FloorsPerAct - 1}",
            EntryNodeIds: [nodes[0].NodeId]);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            $"boss-act-{act}",
            $"boss-act-{act}",
            0,
            RunPhase.MapChoice,
            player,
            PrototypeRng.CreateBundle($"boss-act-{act}"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                act,
                PrototypeContent.Rules.FloorsPerAct - 1,
                2,
                null,
                map,
                null,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        var chooseBoss = Assert.Single(engine.GetLegalActions(state));
        state = engine.Step(state, chooseBoss).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.Equal(
            expectedEnemyId,
            Assert.Single(state.World!.Combat!.Enemies).EnemyId);
    }


}
