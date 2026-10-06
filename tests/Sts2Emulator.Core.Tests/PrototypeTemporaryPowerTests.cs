using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTemporaryPowerTests
{
    [Fact]
    public void AnticipateAddsTemporaryDexterityWithoutRemovingPermanentDexterity()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(1001, "proto.silent.anticipate", 0, empty),
                new CardInstance(1002, "proto.silent.defend", 0, empty)
            ],
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(
                    1, 1001, "proto.silent.anticipate", 0, false, empty),
                new CombatCardInstance(
                    2, 1002, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.dexterity",
                    1,
                    1)
            ],
            NextPowerApplicationOrder: 2);

        var state = CreateState(player, combat);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        Assert.Contains(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId == "proto.power.temporary_dexterity"
                && power.Stacks == 2);

        state = Play(engine, state, 2);
        Assert.Equal(8, state.World!.Combat!.PlayerBlock);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.temporary_dexterity");
        Assert.Contains(
            state.World.Combat.PlayerPowers,
            power =>
                power.PowerId == "proto.power.dexterity"
                && power.Stacks == 1);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long instanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == instanceId);
        return engine.Step(state, action).State;
    }

    private static RunState CreateState(
        PlayerState player,
        CombatState combat)
    {
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "temporary-power-test",
            "temporary-power-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("temporary-power-test"),
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
