using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksClamFogTests
{
    [Fact]
    public void ClamAndFogAreSourceBackedAtAscensionBoundaries()
    {
        var clam = PrototypeContent.Enemy("proto.enemy.sewer_clam");
        Assert.Equal((56, 56), clam.HpRangeAt(1, 0));
        Assert.Equal((58, 58), clam.HpRangeAt(1, 8));
        Assert.Equal(8, Assert.Single(clam.StartingPowers!).StacksAt(0));
        Assert.Equal(9, Assert.Single(clam.StartingPowers!).StacksAt(8));
        Assert.Equal(10, clam.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(11, clam.Moves[0].Effects[0].AmountAt(1, 9));

        var plating = PrototypeContent.Power("proto.power.plating");
        Assert.Equal(1, plating.EnemyStartingBlockPerStack);
        Assert.Equal(1, plating.EnemyBlockAtSideTurnEndPerStack);
        Assert.Equal(1, plating.EnemyStacksDecayAtSideTurnStartAfterFirst);

        var fog = PrototypeContent.Enemy("proto.enemy.living_fog");
        Assert.Equal((80, 80), fog.HpRangeAt(1, 0));
        Assert.Equal((82, 82), fog.HpRangeAt(1, 8));
        Assert.Equal(new[] { "advanced_gas", "bloat", "super_gas_blast" },
            fog.Moves.Select(move => move.Id));
        var bomb = PrototypeContent.Enemy("proto.enemy.gas_bomb");
        Assert.Equal((7, 7), bomb.HpRangeAt(1, 0));
        Assert.Equal((8, 8), bomb.HpRangeAt(1, 8));
        Assert.True(bomb.IsMinion);
        Assert.Equal(PrototypeEnemyEffectKind.KillSelf,
            bomb.Moves[0].Effects[^1].Kind);
        Assert.Equal(8, bomb.Moves[0].Effects[0].AmountAt(1, 0));
        Assert.Equal(9, bomb.Moves[0].Effects[0].AmountAt(1, 9));

        var smoggy = PrototypeContent.Power("proto.power.smoggy");
        Assert.True(smoggy.IsDebuff);
        Assert.Equal(PrototypeCardAfflictionKind.Smog,
            smoggy.SkillPlayAffliction);
        Assert.True(smoggy.BlockPlayOfMatchingAffliction);
        Assert.True(smoggy.ClearMatchingAfflictionAtPlayerTurnEnd);
    }

    [Fact]
    public void SewerClamPlatingBlockDecayAndStrengthFollowSideTurnTiming()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine, "proto.encounter.sewer_clam_normal");
        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(8, enemy.Block);
        Assert.Equal(8, Stacks(enemy, "proto.power.plating"));

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(60, state.Player.Hp);
        Assert.Equal("jet", enemy.LastMoveId);
        Assert.Equal(8, enemy.Block);
        Assert.Equal(8, Stacks(enemy, "proto.power.plating"));

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("pressurize", enemy.LastMoveId);
        Assert.Equal(60, state.Player.Hp);
        Assert.Equal(7, enemy.Block);
        Assert.Equal(7, Stacks(enemy, "proto.power.plating"));
        Assert.Equal(4, Stacks(enemy, "proto.power.strength"));

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("jet", enemy.LastMoveId);
        Assert.Equal(46, state.Player.Hp);
        Assert.Equal(6, enemy.Block);
        Assert.Equal(6, Stacks(enemy, "proto.power.plating"));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void LivingFogAppliesSmoggyThenSummonsBombInLastVacantSlot()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine, "proto.encounter.living_fog_normal");
        Assert.Equal(70, state.Player.Hp);

        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        Assert.Equal(62, state.Player.Hp);
        Assert.Equal("advanced_gas",
            Assert.Single(combat.Enemies).LastMoveId);
        Assert.Single(combat.PlayerPowers,
            power => power.PowerId == "proto.power.smoggy");

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(57, state.Player.Hp);
        Assert.Equal("bloat", combat.Enemies[0].LastMoveId);
        var bomb = Assert.Single(combat.Enemies.Where(enemy =>
            enemy.EnemyId == "proto.enemy.gas_bomb"));
        Assert.Equal("bomb5", bomb.SlotName);
        Assert.Equal(7, bomb.Hp);
        Assert.Null(bomb.LastMoveId);
        Assert.Equal(1, Stacks(bomb, "proto.power.minion"));

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(41, state.Player.Hp);
        Assert.Equal("super_gas_blast", combat.Enemies[0].LastMoveId);
        bomb = Assert.Single(combat.Enemies.Where(enemy =>
            enemy.EnemyId == "proto.enemy.gas_bomb"));
        Assert.Equal("explode", bomb.LastMoveId);
        Assert.Equal(0, bomb.Hp);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void SmoggyAfflictsSkillsAfterSkillPlayBlocksThemThenClears()
    {
        var engine = new PrototypeGameEngine();
        var state = FindEncounter(engine, "proto.encounter.living_fog_normal");
        state = EndTurn(engine, state);
        var combat = state.World!.Combat!;
        var skills = combat.Hand
            .Select(id => combat.Cards.Single(card => card.InstanceId == id))
            .Where(card => PrototypeContent.Card(card.CardId).Type
                == PrototypeCardType.Skill)
            .ToArray();
        Assert.NotEmpty(skills);

        var firstSkillId = skills[0].InstanceId;
        var action = engine.GetLegalActions(state)
            .Where(candidate => candidate.Kind == "play_card")
            .Single(candidate =>
            {
                var payload = candidate.Payload.Deserialize<PlayCardPayload>();
                return payload?.CardInstanceId == firstSkillId;
            });
        state = engine.Step(state, action).State;
        combat = state.World!.Combat!;
        Assert.All(combat.Cards.Where(card =>
            PrototypeContent.Card(card.CardId).Type
                == PrototypeCardType.Skill), card =>
            Assert.Equal(PrototypeCardAfflictionKind.Smog,
                card.Affliction?.Kind));
        var afflictedHand = combat.Hand.Where(id =>
            combat.Cards.Single(card => card.InstanceId == id)
                .Affliction?.Kind == PrototypeCardAfflictionKind.Smog)
            .ToArray();
        Assert.NotEmpty(afflictedHand);
        var legal = engine.GetLegalActions(state)
            .Where(item => item.Kind == "play_card")
            .Select(item => item.Payload.Deserialize<PlayCardPayload>()!
                .CardInstanceId).ToHashSet();
        Assert.All(afflictedHand, id => Assert.DoesNotContain(id, legal));

        state = EndTurn(engine, state);
        Assert.All(state.World!.Combat!.Cards,
            card => Assert.NotEqual(PrototypeCardAfflictionKind.Smog,
                card.Affliction?.Kind));
    }

    private static int Stacks(EnemyCombatState enemy, string powerId) =>
        enemy.PowerStates.Where(power => power.PowerId == powerId)
            .Sum(power => power.Stacks);

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state)
    {
        var end = engine.GetLegalActions(state).Single(action =>
            action.Kind == "end_turn");
        return engine.Step(state, end).State;
    }

    private static RunState FindEncounter(
        PrototypeGameEngine engine, string encounterId)
    {
        var state = PrototypeNativeUnderdocksRunFactory.Create(
            "clam-fog-normal-tests");
        for (var floor = 1; floor <= 11; floor++)
        {
            var node = new MapNodeState(
                "clam-fog-test:" + floor,
                1, floor, PrototypeRoomType.Combat, []);
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
            if (state.World!.EncounterIds[^1] == encounterId)
            {
                return state;
            }
        }
        throw new InvalidOperationException("Expected encounter was not drawn.");
    }
}
