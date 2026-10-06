using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeIntrinsicReplayTests
{
    [Fact]
    public void ReplayCountAddsWholeCardExecutionsAtSingleEnergyCost()
    {
        var defend = Card(1, "proto.silent.defend") with
        {
            ReplayCount = 2
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(energy: 3, hand: [defend]);

        state = PlayCard(engine, state, defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(15, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(3, combat.CounterState.SkillsPlayedThisTurn);
        Assert.Contains(defend.InstanceId, combat.DiscardPile);
        Assert.Equal(
            1,
            combat.DiscardPile.Count(id => id == defend.InstanceId));
    }

    [Fact]
    public void ReplayResuspendsForEachNestedChoice()
    {
        var survivor = Card(1, "proto.silent.survivor") with
        {
            ReplayCount = 1
        };
        var strike = Card(2, "proto.silent.strike");
        var defend = Card(3, "proto.silent.defend");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [survivor, strike, defend]);

        state = PlayCard(engine, state, survivor.InstanceId);
        Assert.NotNull(state.World!.Combat!.PendingChoice);

        state = SelectOnly(engine, state, strike.InstanceId);
        Assert.NotNull(state.World!.Combat!.PendingChoice);

        state = SelectOnly(engine, state, defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Equal(16, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(2, combat.CounterState.SkillsPlayedThisTurn);
        Assert.Contains(strike.InstanceId, combat.DiscardPile);
        Assert.Contains(defend.InstanceId, combat.DiscardPile);
        Assert.Contains(survivor.InstanceId, combat.DiscardPile);
    }

    [Fact]
    public void ReplayAndBurstAddTheirExtraExecutions()
    {
        var defend = Card(1, "proto.silent.defend") with
        {
            ReplayCount = 2
        };
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [defend],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.burst",
                    1,
                    1)
            ]);

        state = PlayCard(engine, state, defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(20, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(4, combat.CounterState.SkillsPlayedThisTurn);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState SelectOnly(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "select_cards"
                && item.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([cardInstanceId]));
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var cards = hand;
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone())).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand.Select(card => card.InstanceId).ToArray(),
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
            NextCardInstanceId: cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: powers,
            NextPowerApplicationOrder: powers.Length == 0
                ? 1
                : powers.Max(power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "intrinsic-replay-test",
            "intrinsic-replay-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("intrinsic-replay-test"),
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
