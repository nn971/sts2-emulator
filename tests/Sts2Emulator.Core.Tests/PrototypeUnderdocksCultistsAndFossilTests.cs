using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksCultistsAndFossilTests
{
    [Fact]
    public void CultistAndFossilModelsMatchPinnedAscensionBoundaries()
    {
        var calcified = PrototypeContent.Enemy(
            "proto.enemy.calcified_cultist");
        Assert.Equal((38, 41), calcified.HpRangeAt(1, 0));
        Assert.Equal((39, 42), calcified.HpRangeAt(1, 8));
        Assert.Equal(2, calcified.Moves[0].Effects[0].Amount);
        Assert.Equal(9, calcified.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(11, calcified.Moves[1].Effects[0].AmountAt(1, 9));

        var damp = PrototypeContent.Enemy("proto.enemy.damp_cultist");
        Assert.Equal((51, 53), damp.HpRangeAt(1, 0));
        Assert.Equal((52, 54), damp.HpRangeAt(1, 8));
        Assert.Equal(5, damp.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(6, damp.Moves[0].Effects[0].AmountAt(1, 9));
        Assert.Equal(1, damp.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(3, damp.Moves[1].Effects[0].AmountAt(1, 9));

        var fossil = PrototypeContent.Enemy("proto.enemy.fossil_stalker");
        Assert.Equal((51, 53), fossil.HpRangeAt(1, 0));
        Assert.Equal((54, 56), fossil.HpRangeAt(1, 8));
        Assert.Equal(new[] { "tackle", "latch", "lash" },
            fossil.Moves.Select(move => move.Id));
        Assert.Equal(2, fossil.Moves[2].Effects[0].Repetitions);
        Assert.Equal(3, Assert.Single(fossil.StartingPowers!).Stacks);
        Assert.Equal("proto.power.suck",
            Assert.Single(fossil.StartingPowers!).PowerId);
        Assert.Equal("latch", fossil.Ai!.InitialStateId);

        var ritual = PrototypeContent.Power("proto.power.ritual");
        Assert.True(ritual.SkipInitialEnemySideTurnEnd);
        Assert.Equal(1, ritual.EnemyStrengthAtSideTurnEndPerStack);
        var suck = PrototypeContent.Power("proto.power.suck");
        Assert.Equal(1, suck.StrengthPerUnblockedAttackHitPerStack);
        Assert.False(suck.SkipInitialEnemySideTurnEnd);
    }

    [Fact]
    public void RitualWaitsUntilFollowingEnemyTurnAndPersists()
    {
        var engine = new PrototypeGameEngine();
        var state = FindNormalCombat(
            "cultists-ritual", "proto.encounter.cultists_normal");
        var initialHp = state.Player.Hp;
        Assert.Equal(2, state.World!.Combat!.Enemies.Length);

        state = EndTurn(engine, state);
        Assert.Equal(initialHp, state.Player.Hp);
        var cultists = state.World!.Combat!.Enemies;
        Assert.Equal(new[] { "proto.enemy.calcified_cultist",
            "proto.enemy.damp_cultist" },
            cultists.Select(enemy => enemy.EnemyId));
        Assert.Equal(2, PowerStacks(cultists[0], "proto.power.ritual"));
        Assert.Equal(5, PowerStacks(cultists[1], "proto.power.ritual"));
        Assert.Equal(0, PowerStacks(cultists[0], "proto.power.strength"));
        Assert.Equal(0, PowerStacks(cultists[1], "proto.power.strength"));
        Assert.All(cultists, enemy =>
            Assert.False(enemy.PowerStates.Single(power =>
                power.PowerId == "proto.power.ritual")
                .SkipNextEnemySideTurnEnd));

        state = EndTurn(engine, state);
        cultists = state.World!.Combat!.Enemies;
        Assert.Equal(2, PowerStacks(cultists[0], "proto.power.strength"));
        Assert.Equal(5, PowerStacks(cultists[1], "proto.power.strength"));
        Assert.Equal(initialHp - 10, state.Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(100, 0)]
    public void SuckOnlyCountsUnblockedAttackHits(
        int playerBlock, int expectedStrength)
    {
        var engine = new PrototypeGameEngine();
        var state = FindNormalCombat(
            "fossil-suck", "proto.encounter.fossil_stalker_normal");
        var combat = state.World!.Combat!;
        Assert.Equal("latch", Assert.Single(combat.Enemies).AiStateId);
        state = state with
        {
            World = state.World with
            {
                Combat = combat with { PlayerBlock = playerBlock }
            }
        };
        state = EndTurn(engine, state);
        var fossil = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(expectedStrength,
            PowerStacks(fossil, "proto.power.strength"));
        Assert.Equal(3, PowerStacks(fossil, "proto.power.suck"));
        Assert.Equal(playerBlock > 0 ? 70 : 58, state.Player.Hp);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(3, 3)]
    [InlineData(6, 0)]
    public void SuckCountsEachUnblockedHitFromLash(
        int playerBlock, int expectedStrength)
    {
        var engine = new PrototypeGameEngine();
        var state = FindNormalCombat(
            "fossil-lash", "proto.encounter.fossil_stalker_normal");
        var combat = state.World!.Combat!;
        var fossil = Assert.Single(combat.Enemies);
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    PlayerBlock = playerBlock,
                    Enemies = [fossil with { AiStateId = "lash" }]
                }
            }
        };
        state = EndTurn(engine, state);
        Assert.Equal(expectedStrength, PowerStacks(
            Assert.Single(state.World!.Combat!.Enemies),
            "proto.power.strength"));
        Assert.Equal(70 - (6 - Math.Min(playerBlock, 6)),
            state.Player.Hp);
    }

    private static int PowerStacks(EnemyCombatState enemy, string powerId) =>
        enemy.PowerStates.Where(power =>
            power.PowerId == powerId).Sum(power => power.Stacks);

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state)
    {
        var end = engine.GetLegalActions(state).Single(action =>
            action.Kind == "end_turn");
        return engine.Step(state, end).State;
    }

    private static RunState FindNormalCombat(
        string seed, string encounterId)
    {
        var engine = new PrototypeGameEngine();
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var state = PrototypeNativeUnderdocksRunFactory.Create(
                seed + "-" + attempt);
            for (var floor = 1; floor <= 7; floor++)
            {
                state = StartRoom(engine, state, floor);
                if (state.World!.EncounterIds[^1] == encounterId)
                {
                    return state;
                }
            }
        }
        throw new InvalidOperationException(
            "Requested normal encounter did not appear.");
    }

    private static RunState StartRoom(
        PrototypeGameEngine engine, RunState state, int floor)
    {
        var node = new MapNodeState(
            "normal-test-" + floor, 1, floor,
            PrototypeRoomType.Combat, []);
        var world = state.World!;
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = world with
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
        var choose = Assert.Single(engine.GetLegalActions(state));
        return engine.Step(state, choose).State;
    }
}
