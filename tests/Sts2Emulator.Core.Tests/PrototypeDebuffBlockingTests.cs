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
}
