using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeOneShotModifierTests
{
    [Fact]
    public void PounceMakesOnlyTheNextSkillFreeAndConsumesOneStack()
    {
        var state = CreateState();
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1, targetEnemyId: 1);

        Assert.Equal(1, state.World!.Combat!.Energy);
        var freeSkill = Assert.Single(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.free_next_skill");
        Assert.Equal(1, freeSkill.Stacks);

        // An Attack does not consume FreeSkillPower.
        state = Play(engine, state, 2, targetEnemyId: 1);
        Assert.Equal(0, state.World!.Combat!.Energy);
        Assert.Contains(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.free_next_skill");

        // The Skill remains legal at zero Energy and consumes the power.
        var defend = Assert.Single(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 3);
        state = engine.Step(state, defend).State;

        Assert.Equal(0, state.World!.Combat!.Energy);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.free_next_skill");

        // A second 1-Energy Skill is no longer legal.
        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 4);
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

    private static RunState CreateState()
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = new[]
        {
            new CombatCardInstance(
                1, 1001, "proto.silent.pounce", 0, false, empty),
            new CombatCardInstance(
                2, 1002, "proto.silent.strike", 0, false, empty),
            new CombatCardInstance(
                3, 1003, "proto.silent.defend", 0, false, empty),
            new CombatCardInstance(
                4, 1004, "proto.silent.defend", 0, false, empty)
        };

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty)).ToArray(),
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2, 3, 4],
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
            NextCardInstanceId: 5,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "one-shot-modifier-test",
            "one-shot-modifier-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("one-shot-modifier-test"),
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
