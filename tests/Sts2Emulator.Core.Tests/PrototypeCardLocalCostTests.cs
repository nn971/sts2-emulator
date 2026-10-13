using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeCardLocalCostTests
{
    [Fact]
    public void UpMySleeveReducesOnlyItsOwnCombatCostAfterEachPlay()
    {
        var empty = PrototypeJson.EmptyObject();
        var source = new CombatCardInstance(
            1,
            1001,
            "proto.silent.up_my_sleeve",
            0,
            false,
            empty);
        var defend = new CombatCardInstance(
            2,
            1002,
            "proto.silent.defend",
            0,
            false,
            empty);

        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(1001, source.CardId, 0, empty),
                new CardInstance(1002, defend.CardId, 0, empty)
            ],
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 2,
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
            Cards: [source, defend],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState(player, combat);
        var engine = new PrototypeGameEngine();

        state = Play(engine, state, 1);
        combat = state.World!.Combat!;

        var first = combat.Cards.Single(card => card.InstanceId == 1);
        Assert.Equal(-1, first.CombatEnergyCostDelta);
        Assert.Equal(0, combat.Energy);
        Assert.Equal(
            3,
            combat.Hand
                .Select(id => combat.Cards.Single(card => card.InstanceId == id))
                .Count(card => card.CardId == "proto.silent.shiv"));

        // Put the same combat instance back into Hand. Its second play should cost 1.
        combat = combat with
        {
            Energy = 1,
            Hand = combat.Hand.Append(1).ToArray(),
            DiscardPile = combat.DiscardPile.Where(id => id != 1).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };

        state = Play(engine, state, 1);
        combat = state.World!.Combat!;

        var second = combat.Cards.Single(card => card.InstanceId == 1);
        Assert.Equal(-2, second.CombatEnergyCostDelta);
        Assert.Equal(0, combat.Energy);

        // Defend still costs its ordinary 1 Energy; the mutation is card-local.
        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);

        // At delta -2, Up My Sleeve itself is now free.
        combat = combat with
        {
            Energy = 0,
            Hand = combat.Hand.Append(1).ToArray(),
            DiscardPile = combat.DiscardPile.Where(id => id != 1).ToArray()
        };
        state = state with
        {
            World = state.World with { Combat = combat }
        };

        Assert.Contains(
            engine.GetLegalActions(state),
            action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == cardInstanceId);
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
            "card-local-cost-test",
            "card-local-cost-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("card-local-cost-test"),
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
