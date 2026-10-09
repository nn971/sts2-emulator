using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeUnderdocksMatriarchTests
{
    [Fact]
    public void PinnedMatriarchStatsAndDeclarativeWakeRule()
    {
        Assert.Equal(new[]
        {
            "proto.encounter.soul_fysh_boss",
            "proto.encounter.lagavulin_matriarch_boss",
            "proto.encounter.waterfall_giant_boss"
        }, PrototypeNativeUnderdocks.SupportedBossEncounterIds);
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.lagavulin_matriarch_boss");
        Assert.Equal(PrototypeRoomType.Boss, encounter.RoomType);
        Assert.Equal("proto.enemy.lagavulin_matriarch",
            Assert.Single(encounter.FixedEnemySpecs).EnemyId);
        var boss = PrototypeContent.Enemy("proto.enemy.lagavulin_matriarch");
        Assert.Equal((222, 222), boss.HpRangeAt(1, 0));
        Assert.Equal((233, 233), boss.HpRangeAt(1, 8));
        Assert.Equal(new[]
        {
            "sleep", "slash", "disembowel", "slash2", "soul_siphon"
        }, boss.Moves.Select(move => move.Id));
        Assert.Equal(19, boss.Moves[1].Effects[0].AmountAt(1, 0));
        Assert.Equal(21, boss.Moves[1].Effects[0].AmountAt(1, 9));
        Assert.Equal(9, boss.Moves[2].Effects[0].AmountAt(1, 0));
        Assert.Equal(10, boss.Moves[2].Effects[0].AmountAt(1, 9));
        Assert.Equal(2, boss.Moves[2].Effects[0].Repetitions);
        Assert.Equal(12, boss.Moves[3].Effects[1].AmountAt(1, 0));
        Assert.Equal(14, boss.Moves[3].Effects[1].AmountAt(1, 8));
        Assert.Equal(new[] { 12, 3 },
            boss.StartingPowers!.Select(power => power.Stacks));
        var asleep = PrototypeContent.Power("proto.power.asleep");
        Assert.Equal(1, asleep.EnemyStacksDecayAtSideTurnEnd);
        Assert.True(asleep.WakeOwnerOnUnblockedAttackDamage);
        Assert.True(asleep.StunOwnerOnWake);
        Assert.Equal("slash", asleep.OwnerAiStateOnWake);
        Assert.Equal("slash", asleep.OwnerAiStateOnPowerExpiry);
        Assert.Equal(new[]
        {
            "proto.power.asleep", "proto.power.plating"
        }, asleep.RemoveOwnerPowersOnWake);
    }

    [Fact]
    public void UndamagedMatriarchSleepsThreeTurnsThenAttacks()
    {
        var engine = new PrototypeGameEngine();
        var state = FindBoss(engine);
        var matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(12, matriarch.Block);
        Assert.Equal(3, Stacks(matriarch, "proto.power.asleep"));
        Assert.Equal(12, Stacks(matriarch, "proto.power.plating"));

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("sleep", matriarch.LastMoveId);
        Assert.Equal(2, Stacks(matriarch, "proto.power.asleep"));
        Assert.Equal(12, matriarch.Block);

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("sleep", matriarch.LastMoveId);
        Assert.Equal(1, Stacks(matriarch, "proto.power.asleep"));
        Assert.Equal(11, matriarch.Block);

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("sleep", matriarch.LastMoveId);
        Assert.Equal("slash", matriarch.AiStateId);
        Assert.DoesNotContain(matriarch.PowerStates,
            power => power.PowerId is "proto.power.asleep"
                or "proto.power.plating");
        Assert.Equal(0, matriarch.Block);

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("slash", matriarch.LastMoveId);
        Assert.Equal(51, state.Player.Hp);

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("disembowel", matriarch.LastMoveId);
        Assert.Equal(33, state.Player.Hp);

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("slash2", matriarch.LastMoveId);
        Assert.Equal(21, state.Player.Hp);
        Assert.Equal(12, matriarch.Block);

        state = EndTurn(engine, state);
        matriarch = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal("soul_siphon", matriarch.LastMoveId);
        Assert.Equal(21, state.Player.Hp);
        Assert.Equal(-2, state.World.Combat!.PlayerPowers.Where(
            power => power.PowerId == "proto.power.strength")
            .Sum(power => power.Stacks));
        Assert.Equal(-2, state.World.Combat!.PlayerPowers.Where(
            power => power.PowerId == "proto.power.dexterity")
            .Sum(power => power.Stacks));
        Assert.Equal(2, Stacks(matriarch, "proto.power.strength"));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void UnblockedPlayerAttackWakesMatriarchButBlocksDoNot()
    {
        var engine = new PrototypeGameEngine();
        var initial = FindBoss(engine);
        var combat = initial.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);

        // Starting Plating absorbs a six-damage Strike.
        var blocked = PlayStrike(engine, initial);
        var sleeping = Assert.Single(blocked.World!.Combat!.Enemies);
        Assert.Equal(3, Stacks(sleeping, "proto.power.asleep"));
        Assert.Equal("sleep", sleeping.AiStateId
            ?? PrototypeContent.Enemy(sleeping.EnemyId).Ai!.InitialStateId);

        var state = initial with
        {
            World = initial.World with
            {
                Combat = combat with
                {
                    Enemies = [enemy with { Block = 0 }]
                }
            }
        };
        state = PlayStrike(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(216, enemy.Hp);
        Assert.Equal("slash", enemy.AiStateId);
        Assert.Equal(1, enemy.EnemyActionSkipsRemaining);
        Assert.DoesNotContain(enemy.PowerStates, power =>
            power.PowerId is "proto.power.asleep"
                or "proto.power.plating");

        state = EndTurn(engine, state);
        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(70, state.Player.Hp);
        Assert.Equal(0, enemy.EnemyActionSkipsRemaining);
        state = EndTurn(engine, state);
        Assert.Equal(51, state.Player.Hp);
        Assert.Equal("slash",
            Assert.Single(state.World!.Combat!.Enemies).LastMoveId);
    }

    private static RunState PlayStrike(PrototypeGameEngine engine, RunState state)
    {
        var combat = state.World!.Combat!;
        var strikeIds = combat.Hand.Where(id =>
            combat.Cards.Any(card => card.InstanceId == id
                && card.CardId == "proto.silent.strike")).ToHashSet();
        // Native starter has five Strikes; choose a hand containing one.
        var action = engine.GetLegalActions(state)
            .FirstOrDefault(candidate =>
                candidate.Kind == "play_card"
                && candidate.Payload.Deserialize<PlayCardPayload>() is
                    { } payload
                && strikeIds.Contains(payload.CardInstanceId));
        if (action is null)
        {
            throw new InvalidOperationException(
                "No opening Strike in the current seeded hand.");
        }
        return engine.Step(state, action).State;
    }

    private static int Stacks(EnemyCombatState enemy, string id) =>
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
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var state = PrototypeNativeUnderdocksRunFactory.Create(
                "lagavulin-matriarch-" + attempt);
            if (state.World!.ActOneEncounterPool!.BossEncounterId
                != "proto.encounter.lagavulin_matriarch_boss")
            {
                continue;
            }
            const int floor = 16;
            var node = new MapNodeState(
                "matriarch-test-boss", 1, floor,
                PrototypeRoomType.Boss, []);
            state = state with
            {
                Phase = RunPhase.MapChoice,
                World = state.World with
                {
                    Floor = floor - 1,
                    ActiveRoom = null,
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
        throw new InvalidOperationException("Matriarch never rolled.");
    }
}
