using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBurstTests
{
    [Fact]
    public void BurstReplaysWholeSkillLifecycleWhilePayingEnergyOnce()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.defend")
            ],
            powers:
            [
                Power(
                    "proto.power.burst",
                    stacks: 1,
                    order: 1)
            ]);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var combat = state.World!.Combat!;

        Assert.Equal(2, combat.Energy);
        Assert.Equal(10, combat.PlayerBlock);
        Assert.Equal(2, combat.CounterState.SkillsPlayedThisTurn);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
        Assert.Equal(new long[] { 1 }, combat.DiscardPile);
    }

    [Fact]
    public void BurstReplaysSurvivorAsTwoIndependentSelections()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.survivor"),
                Card(2, "proto.silent.strike"),
                Card(3, "proto.silent.defend")
            ],
            powers:
            [
                Power(
                    "proto.power.burst",
                    stacks: 1,
                    order: 1)
            ]);

        var engine = new PrototypeGameEngine();
        state = PlayCard(engine, state, 1);

        var firstChoice = state.World!.Combat!.PendingChoice;
        Assert.NotNull(firstChoice);
        Assert.Contains(2, firstChoice.CandidateCardInstanceIds);
        Assert.Contains(3, firstChoice.CandidateCardInstanceIds);

        state = SelectOnly(engine, state, 2);

        var secondChoice = state.World!.Combat!.PendingChoice;
        Assert.NotNull(secondChoice);
        Assert.DoesNotContain(2, secondChoice.CandidateCardInstanceIds);
        Assert.Contains(3, secondChoice.CandidateCardInstanceIds);

        state = SelectOnly(engine, state, 3);

        var combat = state.World!.Combat!;

        Assert.Null(combat.PendingChoice);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(16, combat.PlayerBlock);
        Assert.Equal(2, combat.CounterState.SkillsPlayedThisTurn);
        Assert.Equal(new long[] { 2, 3, 1 }, combat.DiscardPile);
    }

    [Fact]
    public void UpgradedBurstStacksDuplicateTwoSuccessiveSkills()
    {
        var state = CreateState(
            energy: 3,
            hand:
            [
                Card(1, "proto.silent.burst", upgradeLevel: 1),
                Card(2, "proto.silent.defend"),
                Card(3, "proto.silent.defend")
            ]);

        var engine = new PrototypeGameEngine();

        state = PlayCard(engine, state, 1);
        var burst = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
        Assert.Equal(2, burst.Stacks);

        state = PlayCard(engine, state, 2);
        burst = Assert.Single(
            state.World!.Combat!.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
        Assert.Equal(1, burst.Stacks);

        state = PlayCard(engine, state, 3);
        var combat = state.World!.Combat!;

        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
        Assert.Equal(0, combat.Energy);
        Assert.Equal(20, combat.PlayerBlock);

        // Burst itself plays once, and each Defend plays twice.
        Assert.Equal(5, combat.CounterState.SkillsPlayedThisTurn);
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static RunState SelectOnly(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "select_cards"
                && action.ReadPayload<SelectCardsPayload>()
                    .CardInstanceIds.SequenceEqual([cardInstanceId]));
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId,
        int upgradeLevel = 0) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            upgradeLevel,
            false,
            PrototypeJson.EmptyObject());

    private static PrototypePowerInstanceState Power(
        string powerId,
        int stacks,
        long order) =>
        new(powerId, stacks, order);

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            hand.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty)).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            1,
            energy,
            0,
            hand.Select(card => card.InstanceId).ToArray(),
            [],
            [],
            [],
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
            hand.Max(card => card.InstanceId) + 1,
            hand,
            powers,
            powers.Length == 0
                ? 1
                : powers.Max(power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "burst-test",
            "burst-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("burst-test"),
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
