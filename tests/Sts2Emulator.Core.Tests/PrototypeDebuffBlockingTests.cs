using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeDebuffBlockingTests
{
    [Fact]
    public void PositivePlayerPowerDoesNotConsumeArtifact()
    {
        var footwork = new CombatCardInstance(
            1,
            1001,
            "proto.silent.footwork",
            0,
            false,
            PrototypeJson.EmptyObject());

        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(
                    1001,
                    footwork.CardId,
                    0,
                    PrototypeJson.EmptyObject())
            ],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [footwork.InstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards: [footwork],
            PlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.artifact",
                    1,
                    1)
            ],
            NextPowerApplicationOrder: 2);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "artifact-positive-power-test",
            "artifact-positive-power-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "artifact-positive-power-test"),
            PrototypeJson.EmptyObject(),
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

        var engine = new PrototypeGameEngine();
        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == footwork.InstanceId);
        state = engine.Step(state, play).State;

        combat = state.World!.Combat!;
        Assert.Equal(
            1,
            Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.artifact")
                .Stacks);
        Assert.Equal(
            2,
            Assert.Single(
                combat.PlayerPowers,
                power => power.PowerId
                    == "proto.power.dexterity")
                .Stacks);
    }
    [Fact]
    public void EnemyArtifactBlocksFirstIncomingStatusDebuff()
    {
        var neutralize1 = new CombatCardInstance(
            1,
            null,
            "proto.silent.neutralize",
            0,
            true,
            PrototypeJson.EmptyObject());
        var neutralize2 = new CombatCardInstance(
            2,
            null,
            "proto.silent.neutralize",
            0,
            true,
            PrototypeJson.EmptyObject());
        var player = new PlayerState(
            70,
            70,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var enemy = new EnemyCombatState(
            1,
            "proto.enemy.crawler",
            100,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            Powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.artifact",
                    1,
                    1)
            ]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand:
            [
                neutralize1.InstanceId,
                neutralize2.InstanceId
            ],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: [enemy],
            NextCardInstanceId: 3,
            Cards: [neutralize1, neutralize2],
            PlayerPowers: [],
            NextPowerApplicationOrder: 2);
        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "enemy-artifact-test",
            "enemy-artifact-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "enemy-artifact-test"),
            PrototypeJson.EmptyObject(),
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
        var engine = new PrototypeGameEngine();

        var first = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId
                    == neutralize1.InstanceId);
        state = engine.Step(state, first).State;

        enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Empty(enemy.PowerStates);
        Assert.DoesNotContain(
            "proto.status.weak",
            enemy.Statuses.Keys);

        var second = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId
                    == neutralize2.InstanceId);
        state = engine.Step(state, second).State;

        enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(
            1,
            enemy.Statuses["proto.status.weak"]);
    }

}
