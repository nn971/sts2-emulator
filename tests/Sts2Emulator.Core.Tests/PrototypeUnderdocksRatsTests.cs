using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksRatsTests
{
    [Fact]
    public void SourceBackedRatsHaveStaggeredOpenersAndSummonConstraints()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.two_tailed_rats_normal");
        Assert.Equal(new[] { "third", "fourth", "fifth" },
            encounter.FixedEnemySpecs.Select(enemy => enemy.SlotName));
        Assert.Equal(new[] { "scratch", "bite", "screech" },
            encounter.CyclicOpeningAiStateIds);
        var rat = PrototypeContent.Enemy("proto.enemy.two_tailed_rat");
        Assert.Equal((17, 21), rat.HpRangeAt(1, 0));
        Assert.Equal((18, 22), rat.HpRangeAt(1, 8));
        Assert.Equal(2, rat.NonSummonMovesBeforeEligible);
        Assert.Equal(3, rat.MaxCoordinatedSummons);
        Assert.Equal(new[]
        {
            "scratch", "disease_bite", "screech", "call_for_backup"
        }, rat.Moves.Select(move => move.Id));
        Assert.Equal(8, rat.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(9, rat.Moves[0].Effects[0].AmountAt(1, 9));
        Assert.Equal(6, rat.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(7, rat.Moves[1].Effects[0].AmountAt(1, 9));
        var random = rat.Ai!.States.Single(state => state.Id == "random");
        Assert.Equal(new[] { 1, 1, 3, 9 },
            random.Branches!.Select(branch => branch.Weight));
        Assert.True(random.Branches![^1].RequiresAvailableSummon);
        Assert.Equal(PrototypeEnemyAiRepeatRule.UseOnlyOnce,
            random.Branches![^1].RepeatRule);
    }

    [Fact]
    public void RatsCannotSummonDuringInitialTwoActions()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine);
        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(3, enemies.Length);
        Assert.Equal(3, enemies.Select(enemy => enemy.AiStateId)
            .Distinct().Count());
        Assert.All(enemies, enemy =>
        {
            Assert.Equal(2, enemy.NonSummonMovesUntilEligible);
            Assert.Equal(0, enemy.SharedSummonsUsed);
        });

        state = EndTurn(engine, state);
        enemies = state.World!.Combat!.Enemies;
        Assert.Equal(3, enemies.Length);
        Assert.Equal(new[] { "disease_bite", "scratch", "screech" },
            enemies.Select(enemy => enemy.LastMoveId!)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(enemies, enemy =>
            Assert.Equal(1, enemy.NonSummonMovesUntilEligible));

        state = EndTurn(engine, state);
        enemies = state.World!.Combat!.Enemies;
        Assert.Equal(3, enemies.Length);
        Assert.DoesNotContain(enemies, enemy =>
            enemy.LastMoveId == "call_for_backup");
        Assert.All(enemies, enemy =>
            Assert.Equal(0, enemy.NonSummonMovesUntilEligible));
    }

    [Fact]
    public void RatBackupSharesBudgetAndUsesLastAvailableSlot()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine);
        var combat = state.World!.Combat!;
        // Drive one qualifying backup directly to verify its effect;
        // random-branch selection is tested independently above.
        var enemies = combat.Enemies.Select((enemy, index) =>
            enemy with
            {
                AiStateId = index == 0 ? "backup" : "screech",
                PlannedMoveIndex = null,
                PlannedNextAiStateId = null,
                NonSummonMovesUntilEligible = 0
            }).ToArray();
        state = state with
        {
            World = state.World with
            {
                Combat = combat with { Enemies = enemies }
            }
        };
        state = EndTurn(engine, state);
        var rats = state.World!.Combat!.Enemies;
        Assert.Equal(4, rats.Length);
        Assert.Equal("call_for_backup", rats[0].LastMoveId);
        Assert.Equal("second", rats[^1].SlotName);
        Assert.True(rats[^1].SkipNextEnemyAction == false);
        Assert.Null(rats[^1].LastMoveId);
        Assert.All(rats, rat => Assert.Equal(1, rat.SharedSummonsUsed));
        Assert.All(rats, rat => Assert.True(rat.SharedSummonUsedThisTurn));

        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    private static RunState EndTurn(PrototypeGameEngine engine, RunState state)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState FindEncounter(PrototypeGameEngine engine)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "rats-native-normal-test");
        for (var floor = 1; floor <= 13; floor++)
        {
            var node = new MapNodeState(
                "rats-test:" + floor, 1, floor,
                PrototypeRoomType.Combat, []);
            state = state with
            {
                Phase = RunPhase.MapChoice,
                World = state.World! with
                {
                    Floor = floor - 1,
                    ActiveRoom = null,
                    Map = new MapState(
                        [node], CurrentNodeId: null,
                        EntryNodeIds: [node.NodeId],
                        GenerationProfileId:
                            PrototypeNativeUnderdocks.GenerationProfileId),
                    Combat = null,
                    Reward = null,
                    Shop = null,
                    Event = null
                }
            };
            state = engine.Step(state,
                Assert.Single(engine.GetLegalActions(state))).State;
            if (state.World!.EncounterIds[^1]
                == "proto.encounter.two_tailed_rats_normal")
            {
                return state;
            }
        }
        throw new InvalidOperationException(
            "Rats not selected within implemented normal pool.");
    }
}
