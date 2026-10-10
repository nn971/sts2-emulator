using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksWaterfallGiantTests
{
    [Fact]
    public void SourceBackedPressureCycleAndAscensionValues()
    {
        var enemy = PrototypeContent.Enemy("proto.enemy.waterfall_giant");
        Assert.Equal((240, 240), enemy.HpRangeAt(1, 0));
        Assert.Equal((250, 250), enemy.HpRangeAt(1, 8));
        Assert.Equal(new[]
        {
            "pressurize", "stomp", "ram", "siphon", "pressure_gun",
            "pressure_up", "about_to_blow", "explode"
        }, enemy.Moves.Select(move => move.Id));
        Assert.Equal(15, enemy.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(20, enemy.Moves[0].Effects[0].AmountAt(1, 9));
        Assert.Equal(15, enemy.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(16, enemy.Moves[1].Effects[0].AmountAt(1, 9));
        Assert.Equal(10, enemy.Moves[2].Effects[0].AmountAt(1, 0));
        Assert.Equal(11, enemy.Moves[2].Effects[0].AmountAt(1, 9));
        Assert.Equal(10, enemy.Moves[3].Effects[0].AmountAt(1, 0));
        Assert.Equal(15, enemy.Moves[3].Effects[0].AmountAt(1, 8));
        Assert.Equal(20, enemy.Moves[4].Effects[0].AmountAt(1, 0));
        Assert.Equal(23, enemy.Moves[4].Effects[0].AmountAt(1, 9));
        Assert.Equal(5, enemy.Moves[4].Effects[0].ExtraAmountPerPriorMoveUse);
        Assert.Equal(13, enemy.Moves[5].Effects[0].AmountAt(1, 0));
        Assert.Equal(14, enemy.Moves[5].Effects[0].AmountAt(1, 9));
        var power = PrototypeContent.Power("proto.power.steam_eruption");
        Assert.Equal(999999999, power.LastStandHp);
        Assert.Equal("about_to_blow", power.LastStandAiStateId);
        Assert.True(enemy.Moves[7].Effects[0].UseStoredEnemyDamage);
        Assert.Equal(PrototypeEnemyEffectKind.KillSelf,
            enemy.Moves[7].Effects[1].Kind);
    }

    [Fact]
    public void PressurizeStompRamSiphonGunAndPressureUpAccumulateSteam()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        state = EndTurn(engine, state);
        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("pressurize", enemy.LastMoveId);
        Assert.Equal(15, Power(enemy, "proto.power.steam_eruption"));
        Assert.Equal(70, state.Player.Hp);

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("stomp", enemy.LastMoveId);
        Assert.Equal(18, Power(enemy, "proto.power.steam_eruption"));
        Assert.Equal(55, state.Player.Hp);
        Assert.Equal(1, state.World.Combat.PlayerPowers.Single(power =>
            power.PowerId == "proto.power.weak").Stacks);

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("ram", enemy.LastMoveId);
        Assert.Equal(21, Power(enemy, "proto.power.steam_eruption"));
        Assert.Equal(45, state.Player.Hp);

        var combat = state.World.Combat;
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = [enemy with { Hp = 180 }]
                }
            }
        };
        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("siphon", enemy.LastMoveId);
        Assert.Equal(190, enemy.Hp);
        Assert.Equal(24, Power(enemy, "proto.power.steam_eruption"));
        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("pressure_gun", enemy.LastMoveId);
        Assert.Equal(25, state.Player.Hp);
        Assert.Equal(27, Power(enemy, "proto.power.steam_eruption"));
        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("pressure_up", enemy.LastMoveId);
        Assert.Equal(12, state.Player.Hp);
        Assert.Equal(30, Power(enemy, "proto.power.steam_eruption"));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void PressureGunGainsFiveDamageEachUse()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies =
                    [
                        enemy with
                        {
                            AiStateId = "pressure_gun",
                            // Synthetic state injection bypasses native
                            // intent commitment: clear the old intent.
                            PlannedMoveIndex = null,
                            PlannedNextAiStateId = null,
                            MoveUseCounts = new Dictionary<string, int>
                            {
                                ["pressure_gun"] = 2
                            }
                        }
                    ]
                }
            }
        };
        state = EndTurn(engine, state);
        Assert.Equal(40, state.Player.Hp);
        Assert.Equal("pressure_gun",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
    }

    [Fact]
    public void LethalPlayerAttackTriggersLastStandAndDelayedExplosion()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        state = EndTurn(engine, state); // pressurize, 15 steam
        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Enemies = [enemy with { Hp = 1, Block = 0 }]
                }
            }
        };

        var attackIds = state.World.Combat!.Hand
            .Select(id => state.World.Combat.Cards.Single(card =>
                card.InstanceId == id))
            .Where(card => PrototypeContent.Card(card.CardId).Type
                == PrototypeCardType.Attack)
            .Select(card => card.InstanceId).ToHashSet();
        var play = engine.GetLegalActions(state).FirstOrDefault(action =>
            action.Kind == "play_card"
            && action.Payload.Deserialize<PlayCardPayload>() is { } payload
            && attackIds.Contains(payload.CardInstanceId));
        Assert.NotNull(play);
        state = engine.Step(state, play).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.True(enemy.LastStandTriggered);
        Assert.Equal(999999999, enemy.Hp);
        Assert.Equal("about_to_blow", enemy.AiStateId);
        Assert.Equal(15, Power(enemy, "proto.power.steam_eruption"));

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("about_to_blow", enemy.LastMoveId);
        Assert.Equal(15, enemy.StoredEnemyDamage);
        Assert.Equal(0, Power(enemy, "proto.power.steam_eruption"));

        state = EndTurn(engine, state);
        Assert.Equal(55, state.Player.Hp);
        Assert.NotEqual(RunPhase.Combat, state.Phase);
    }

    private static int Power(EnemyCombatState enemy, string id) =>
        enemy.PowerStates.Where(power => power.PowerId == id)
            .Sum(power => power.Stacks);

    private static RunState EndTurn(PrototypeGameEngine engine, RunState state)
    {
        var action = engine.GetLegalActions(state).Single(item =>
            item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }

    private static RunState FindBoss(PrototypeGameEngine engine)
    {
        for (var i = 0; i < 60; i++)
        {
            var state = PrototypeNativeUnderdocksRunFactory.Create(
                "waterfall-giant-" + i);
            if (state.World!.ActOneEncounterPool!.BossEncounterId
                != "proto.encounter.waterfall_giant_boss")
            {
                continue;
            }

            const int floor = 16;
            var node = new MapNodeState(
                "waterfall-giant-test", 1, floor,
                PrototypeRoomType.Boss, []);
            state = state with
            {
                Phase = RunPhase.MapChoice,
                World = state.World with
                {
                    Floor = floor - 1, ActiveRoom = null,
                    Map = new MapState(
                        [node], CurrentNodeId: null,
                        EntryNodeIds: [node.NodeId],
                        GenerationProfileId:
                            PrototypeNativeUnderdocks.GenerationProfileId),
                    Combat = null, Reward = null, Shop = null, Event = null
                }
            };
            return engine.Step(state,
                Assert.Single(engine.GetLegalActions(state))).State;
        }
        throw new InvalidOperationException("Waterfall Giant never rolled.");
    }
}
