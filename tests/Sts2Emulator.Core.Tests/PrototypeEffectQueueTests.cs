using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEffectQueueTests
{
    [Fact]
    public void DaggerSprayHitsEveryEnemyTwice()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(20, "proto.silent.dagger_spray", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(
                    2,
                    "proto.enemy.raider",
                    32,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(
                    1,
                    20,
                    "proto.silent.dagger_spray",
                    0,
                    false,
                    empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "aoe-test",
            "aoe-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("aoe-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                21,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");

        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        var enemies = state.World!.Combat!.Enemies;
        Assert.Equal(16, enemies.Single(enemy => enemy.InstanceId == 1).Hp);
        Assert.Equal(24, enemies.Single(enemy => enemy.InstanceId == 2).Hp);
        Assert.Equal(new long[] { 1 }, state.World.Combat.DiscardPile);
    }

    [Fact]
    public void SkewerSpendsAllEnergyAndRepeatsPerEnergySpent()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(30, "proto.silent.skewer", 0, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.elite",
                    40,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 30, "proto.silent.skewer", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "x-cost-test",
            "x-cost-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("x-cost-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                31,
                PrototypeRoomType.Elite,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");

        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Equal(0, combat.Energy);
        Assert.Equal(19, combat.Enemies.Single().Hp);
        Assert.Equal(new long[] { 1 }, combat.DiscardPile);
    }

}
