using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeTaggedAttackModifierTests
{
    [Fact]
    public void AccuracyBoostsShivsButNotOrdinaryAttacks()
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = new[]
        {
            new CombatCardInstance(
                1, 1001, "proto.silent.accuracy", 0, false, empty),
            new CombatCardInstance(
                2, null, "proto.silent.shiv", 0, true, empty),
            new CombatCardInstance(
                3, 1003, "proto.silent.strike", 0, false, empty)
        };

        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(
                    1001, "proto.silent.accuracy", 0, empty),
                new CardInstance(
                    1003, "proto.silent.strike", 0, empty)
            ],
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2, 3],
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
            NextCardInstanceId: 4,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState(player, combat);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, null);

        var accuracy = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.accuracy");
        Assert.Equal(4, accuracy.Stacks);

        state = Play(engine, state, 2, 1);
        Assert.Equal(
            92,
            state.World!.Combat!.Enemies.Single().Hp);

        state = Play(engine, state, 3, 1);
        Assert.Equal(
            86,
            state.World!.Combat!.Enemies.Single().Hp);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int? targetEnemyId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
            {
                if (action.Kind != "play_card")
                {
                    return false;
                }

                var payload = action.ReadPayload<PlayCardPayload>();
                return payload.CardInstanceId == cardInstanceId
                    && payload.TargetEnemyId == targetEnemyId;
            });
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
            "tagged-attack-modifier-test",
            "tagged-attack-modifier-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("tagged-attack-modifier-test"),
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
