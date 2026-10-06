using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRandomTargetTests
{
    [Fact]
    public void RicochetUsesDedicatedRandomTargetStreamOncePerHit()
    {
        var state = CreateCombatState(upgradeLevel: 0);
        var engine = new PrototypeGameEngine();

        var action = Assert.Single(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        Assert.Null(action.ReadPayload<PlayCardPayload>().TargetEnemyId);

        var beforeCalls = StreamCalls(state, "combat_targets");
        state = engine.Step(state, action).State;
        var afterCalls = StreamCalls(state, "combat_targets");

        Assert.Equal(4, afterCalls - beforeCalls);

        var enemies = state.World!.Combat!.Enemies;
        var totalDamage = enemies.Sum(enemy => 100 - enemy.Hp);
        Assert.Equal(12, totalDamage);
    }

    [Fact]
    public void UpgradedRicochetAddsOneIndependentRandomHit()
    {
        var state = CreateCombatState(upgradeLevel: 1);
        var engine = new PrototypeGameEngine();

        var action = Assert.Single(
            engine.GetLegalActions(state),
            action => action.Kind == "play_card");

        var beforeCalls = StreamCalls(state, "combat_targets");
        state = engine.Step(state, action).State;
        var afterCalls = StreamCalls(state, "combat_targets");

        Assert.Equal(5, afterCalls - beforeCalls);

        var enemies = state.World!.Combat!.Enemies;
        var totalDamage = enemies.Sum(enemy => 100 - enemy.Hp);
        Assert.Equal(15, totalDamage);
    }

    private static ulong StreamCalls(RunState state, string streamId) =>
        state.Rng.Streams
            .Single(stream => stream.StreamId == streamId)
            .CallCount
        ?? 0;

    private static RunState CreateCombatState(int upgradeLevel)
    {
        var empty = PrototypeJson.EmptyObject();
        var card = new CombatCardInstance(
            1,
            1001,
            "proto.silent.ricochet",
            upgradeLevel,
            false,
            empty);

        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(
                    1001,
                    card.CardId,
                    upgradeLevel,
                    empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            1,
            3,
            0,
            [1],
            [],
            [],
            [],
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(
                    2,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            2,
            [card],
            [],
            1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "random-target-test",
            "random-target-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("random-target-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                5000,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));
    }
}
