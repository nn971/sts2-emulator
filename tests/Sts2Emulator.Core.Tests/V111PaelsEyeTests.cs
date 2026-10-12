using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelsEyeTests
{
    private static readonly PrototypeGameEngine Engine = new();

    private static RunState Combat()
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("paels-eye", NativePaelRewardsV111.EyeId);
        state = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        state = state with
        {
            Player = state.Player with { Hp = 1000, MaxHp = 1000 },
            World = state.World! with
            {
                Map = new MapState([], GenerationProfileId: PrototypeNativeLaterActMap.HiveProfileId),
                Floor = 1, ActiveRoom = PrototypeRoomType.Combat, Event = null, Reward = null
            }
        };
        var method = typeof(PrototypeGameEngine).GetMethod("StartCombat", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (RunState)method.Invoke(null, [state, PrototypeRoomType.Combat,
            PrototypeContent.Encounter("proto.native.hive.encounter.tunneler_weak")])!;
    }

    [Fact]
    public void NoManualPlayExhaustsHandAndTakesOneExtraTurnWithoutEnemyActions()
    {
        var state = Combat();
        var combat = state.World!.Combat!;
        var openingHand = (long[])combat.Hand.Clone();
        var enemyIntents = combat.Enemies
            .Select(enemy => (enemy.InstanceId, enemy.PlannedMoveIndex))
            .ToArray();
        var action = Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn");
        var result = Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State;
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(Engine.Step(state, action).State));

        var nextCombat = result.World!.Combat!;
        Assert.Equal(2, nextCombat.Turn);
        Assert.True(nextCombat.IsPlayerTurn);
        Assert.Empty(nextCombat.Hand.Intersect(openingHand));
        Assert.All(openingHand, id => Assert.Contains(id, nextCombat.ExhaustPile));
        Assert.Equal(enemyIntents, nextCombat.Enemies
            .Select(enemy => (enemy.InstanceId, enemy.PlannedMoveIndex)));
        Assert.True(nextCombat.RelicStates.Single(relic => relic.RelicId == NativePaelRewardsV111.EyeId)
            .PaelsEyeUsedThisCombat);
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void ManualCardPlayUsesUpTheTurnAndPreventsPaelsEyeExtraTurn()
    {
        var state = Combat();
        var play = Engine.GetLegalActions(state).First(action => action.Kind == "play_card");
        state = Engine.Step(state, play).State;
        Assert.Equal(1, state.World!.Combat!.CounterState.ManualCardsPlayedThisTurn);

        var endTurn = Engine.GetLegalActions(state).Single(action => action.Kind == "end_turn");
        var result = Engine.Step(state, endTurn).State;
        var combat = result.World!.Combat!;
        Assert.Equal(2, combat.Turn);
        Assert.False(combat.RelicStates.Single(relic => relic.RelicId == NativePaelRewardsV111.EyeId)
            .PaelsEyeUsedThisCombat);
        PrototypeStateInvariants.Validate(result);
    }
}
