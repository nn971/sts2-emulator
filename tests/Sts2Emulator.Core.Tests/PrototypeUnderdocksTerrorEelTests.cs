using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksTerrorEelTests
{
    [Fact]
    public void SourceBackedEelHasThresholdAndFourStateSequence()
    {
        var eel = PrototypeContent.Enemy("proto.enemy.terror_eel");
        Assert.Equal((140, 140), eel.HpRangeAt(1, 0));
        Assert.Equal((150, 150), eel.HpRangeAt(1, 8));
        Assert.Equal(70, Assert.Single(eel.StartingPowers!).StacksAt(0));
        Assert.Equal(75, Assert.Single(eel.StartingPowers!).StacksAt(8));
        Assert.Equal(new[] { "crash", "thrash", "stun", "terror" },
            eel.Moves.Select(move => move.Id));
        Assert.Equal(16, eel.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(18, eel.Moves[0].Effects[0].AmountAt(1, 9));
        Assert.Equal(3, eel.Moves[1].Effects[0].Repetitions);
        Assert.Equal(6, eel.Moves[1].Effects[1].Amount);
        Assert.Equal(99, eel.Moves[3].Effects[0].Amount);
        Assert.Equal("stun", PrototypeContent.Power("proto.power.shriek")
            .OwnerAiStateOnHpThresholdTrigger);
        Assert.True(PrototypeContent.Power("proto.power.vigor")
            .ConsumeAfterEnemyAttack);
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.terror_eel_elite");
        Assert.Equal(PrototypeRoomType.Elite, encounter.RoomType);
        Assert.Equal("proto.enemy.terror_eel",
            Assert.Single(encounter.FixedEnemySpecs).EnemyId);
    }

    [Fact]
    public void CrashThrashVigorCrashConsumesBuffAfterAttack()
    {
        var engine = new PrototypeGameEngine();
        var state = StartElite(engine);
        Assert.Equal(70, state.Player.Hp);
        state = EndTurn(engine, state);
        var eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("crash", eel.LastMoveId);
        Assert.Equal(54, state.Player.Hp);
        Assert.Equal(0, PowerStacks(eel, "proto.power.vigor"));

        state = EndTurn(engine, state);
        eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("thrash", eel.LastMoveId);
        Assert.Equal(45, state.Player.Hp);
        Assert.Equal(6, PowerStacks(eel, "proto.power.vigor"));

        state = EndTurn(engine, state);
        eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("crash", eel.LastMoveId);
        Assert.Equal(23, state.Player.Hp);
        Assert.Equal(0, PowerStacks(eel, "proto.power.vigor"));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void UnblockedThresholdDamageRedirectsToStunAndTerrorExactlyOnce()
    {
        var engine = new PrototypeGameEngine();
        var state = StartElite(engine);
        var combat = state.World!.Combat!;
        var eel = Assert.Single(combat.Enemies);
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = [eel with { Hp = 75 }]
                }
            }
        };
        var strike = state.World!.Combat!.Hand.Select(id =>
            state.World.Combat.Cards.Single(card => card.InstanceId == id))
            .First(card => card.CardId == "proto.silent.strike");
        var play = engine.GetLegalActions(state).First(action =>
            action.Kind == "play_card"
            && action.Payload.Deserialize<PlayCardPayload>()?.CardInstanceId
                == strike.InstanceId
            && action.Payload.Deserialize<PlayCardPayload>()?.TargetEnemyId
                == eel.InstanceId);
        state = engine.Step(state, play).State;
        eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("stun", eel.AiStateId);
        Assert.True(eel.Hp <= 70);
        Assert.Equal(0, PowerStacks(eel, "proto.power.shriek"));

        state = EndTurn(engine, state);
        eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("stun", eel.LastMoveId);
        Assert.Equal(70, state.Player.Hp);
        state = EndTurn(engine, state);
        eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("terror", eel.LastMoveId);
        Assert.Equal(99, state.World.Combat.PlayerPowers.Single(power =>
            power.PowerId == "proto.power.vulnerable").Stacks);
        state = EndTurn(engine, state);
        eel = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("crash", eel.LastMoveId);
    }

    private static int PowerStacks(EnemyCombatState enemy, string powerId) =>
        enemy.PowerStates.Where(power => power.PowerId == powerId)
            .Sum(power => power.Stacks);

    private static RunState EndTurn(PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn")).State;

    private static RunState StartElite(PrototypeGameEngine engine)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "terror-eel-elite-tests");
        state = state with
        {
            Phase = RunPhase.MapChoice,
            World = state.World! with
            {
                Floor = 5,
                ActiveRoom = null,
                Map = new MapState(
                    [new MapNodeState("terror-eel-room", 1, 6,
                        PrototypeRoomType.Elite, [])],
                    CurrentNodeId: null,
                    EntryNodeIds: ["terror-eel-room"],
                    GenerationProfileId:
                        PrototypeNativeUnderdocks.GenerationProfileId),
                Combat = null,
                Event = null,
                Reward = null,
                Shop = null,
                ActOneEncounterPool = state.World.ActOneEncounterPool! with
                {
                    RemainingEliteEncounterIds =
                        ["proto.encounter.terror_eel_elite"]
                }
            }
        };
        return engine.Step(state,
            Assert.Single(engine.GetLegalActions(state))).State;
    }
}
