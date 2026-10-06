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
    public void ActOneFloorOneCombatUsesOnlyItsEligibleEncounter()
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
        Assert.Equal(
            "proto.encounter.crawler",
            Assert.Single(combatState!.World!.EncounterIds));
    }
}
