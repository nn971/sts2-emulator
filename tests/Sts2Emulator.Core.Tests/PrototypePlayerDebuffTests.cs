using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePlayerDebuffTests
{
    [Fact]
    public void WeakReducesAttackCardDamageByTwentyFivePercent()
    {
        var state = CreateCardState(
            "proto.silent.strike",
            new PrototypePowerInstanceState(
                "proto.power.weak",
                2,
                1));
        var engine = new PrototypeGameEngine();

        state = PlayOnlyCard(engine, state);

        Assert.Equal(
            96,
            Assert.Single(
                state.World!.Combat!.Enemies).Hp);
    }

    [Fact]
    public void FrailReducesCardBlockAfterAdditiveBlockBonuses()
    {
        var state = CreateCardState(
            "proto.silent.defend",
            new PrototypePowerInstanceState(
                "proto.power.frail",
                2,
                1));
        var engine = new PrototypeGameEngine();

        state = PlayOnlyCard(engine, state);

        Assert.Equal(
            3,
            state.World!.Combat!.PlayerBlock);
    }

    [Fact]
    public void VulnerableAppliesThroughOwnerTurnThenDecrementsAtTurnEnd()
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.twig_slime_s",
                    11,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.vulnerable",
                    2,
                    1)
            ],
            NextPowerApplicationOrder: 2);
        var state = CreateRunState(
            player,
            combat,
            "vulnerable-duration-test");
        var engine = new PrototypeGameEngine();

        state = EndTurn(engine, state);

        Assert.Equal(94, state.Player.Hp);
        Assert.Equal(
            1,
            Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power => power.PowerId
                    == "proto.power.vulnerable")
                .Stacks);

        state = EndTurn(engine, state);

        Assert.Equal(90, state.Player.Hp);
        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId
                == "proto.power.vulnerable");
    }

    private static RunState CreateCardState(
        string cardId,
        PrototypePowerInstanceState power)
    {
        var player = new PlayerState(
            100,
            100,
            0,
            [],
            [],
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);
        var card = new CombatCardInstance(
            1,
            null,
            cardId,
            0,
            true,
            PrototypeJson.EmptyObject());
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [card.InstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.twig_slime_s",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards: [card],
            PlayerPowers: [power],
            NextPowerApplicationOrder: 2);

        return CreateRunState(
            player,
            combat,
            $"player-debuff-{cardId}");
    }

    private static RunState CreateRunState(
        PlayerState player,
        CombatState combat,
        string seed) =>
        new(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(seed),
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

    private static RunState PlayOnlyCard(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "play_card");
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(item => item.Kind == "end_turn");
        return engine.Step(state, action).State;
    }
}
