using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSilentCardBatchOneTests
{
    [Fact]
    public void CalculatedGambleDiscardsHandThenDrawsSameCountAndExhausts()
    {
        var state = CreateState(
            "calculated-gamble",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.calculated_gamble"),
                Card(2, 102, "proto.silent.strike"),
                Card(3, 103, "proto.silent.defend"),
                Card(4, 104, "proto.silent.slice"),
                Card(5, 105, "proto.silent.backflip")
            ],
            hand: [1, 2, 3],
            drawPile: [4, 5]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Equal(3, combat.Energy);
        Assert.Equal(new long[] { 5, 4 }, combat.Hand);
        Assert.Equal(new long[] { 2, 3 }, combat.DiscardPile);
        Assert.Equal(new long[] { 1 }, combat.ExhaustPile);
        Assert.Equal(2, combat.CounterState.CardsDiscardedThisTurn);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UpgradedCalculatedGambleHasIntrinsicRetain()
    {
        var definition =
            PrototypeContent.Card(
                "proto.silent.calculated_gamble");
        Assert.True(definition.RetainOnUpgrade);

        var state = CreateState(
            "calculated-gamble-retain",
            energy: 3,
            cards:
            [
                Card(
                    1,
                    101,
                    "proto.silent.calculated_gamble",
                    upgradeLevel: 1)
            ],
            hand: [1]);

        var engine = new PrototypeGameEngine();
        state = EndTurn(engine, state);

        Assert.Contains(
            1L,
            state.World!.Combat!.Hand);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StormOfSteelDiscardsHandAndCreatesMatchingUpgradedShivs()
    {
        var state = CreateState(
            "storm-of-steel",
            energy: 3,
            cards:
            [
                Card(
                    1,
                    101,
                    "proto.silent.storm_of_steel",
                    upgradeLevel: 1),
                Card(2, 102, "proto.silent.strike"),
                Card(3, 103, "proto.silent.defend"),
                Card(4, 104, "proto.silent.slice")
            ],
            hand: [1, 2, 3, 4],
            nextCardInstanceId: 5);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        Assert.Equal(2, combat.Energy);
        Assert.Equal(
            new long[] { 2, 3, 4, 1 },
            combat.DiscardPile);
        Assert.Equal(3, combat.Hand.Length);
        Assert.All(
            combat.Hand,
            id =>
            {
                var shiv = combat.Cards.Single(
                    card => card.InstanceId == id);
                Assert.Equal(
                    "proto.silent.shiv",
                    shiv.CardId);
                Assert.Equal(1, shiv.UpgradeLevel);
            });
        Assert.Equal(3, combat.CounterState.CardsDiscardedThisTurn);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ExposeClearsBlockAndArtifactBeforeApplyingVulnerable()
    {
        var state = CreateState(
            "expose",
            energy: 3,
            cards:
            [
                Card(
                    1,
                    101,
                    "proto.silent.expose",
                    upgradeLevel: 1)
            ],
            hand: [1],
            enemies:
            [
                Enemy(
                    hp: 40,
                    block: 17,
                    powers:
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.artifact",
                            2,
                            1)
                    ])
            ],
            nextPowerApplicationOrder: 2);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1, targetEnemyId: 1);

        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        Assert.Equal(0, enemy.Block);
        Assert.DoesNotContain(
            enemy.PowerStates,
            power =>
                power.PowerId
                    == "proto.power.artifact");
        Assert.Equal(
            3,
            enemy.Statuses[
                "proto.status.vulnerable"]);
        Assert.Equal(
            new long[] { 1 },
            combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void OutbreakAppliesPoisonToAllThenTriggersItUnblockably()
    {
        var state = CreateState(
            "outbreak",
            energy: 3,
            cards:
            [
                Card(
                    1,
                    101,
                    "proto.silent.outbreak")
            ],
            hand: [1],
            enemies:
            [
                Enemy(
                    instanceId: 1,
                    hp: 40,
                    block: 20,
                    statuses:
                        new Dictionary<string, int>(
                            StringComparer.Ordinal)
                        {
                            ["proto.status.poison"] = 2
                        }),
                Enemy(
                    instanceId: 2,
                    hp: 40,
                    block: 20)
            ]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        var first = combat.Enemies.Single(
            enemy => enemy.InstanceId == 1);
        var second = combat.Enemies.Single(
            enemy => enemy.InstanceId == 2);

        Assert.Equal(29, first.Hp);
        Assert.Equal(31, second.Hp);
        Assert.Equal(20, first.Block);
        Assert.Equal(20, second.Block);
        Assert.Equal(
            10,
            first.Statuses[
                "proto.status.poison"]);
        Assert.Equal(
            8,
            second.Statuses[
                "proto.status.poison"]);
        Assert.Equal(0, combat.Energy);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StrangleSkipsItsOwnApplicationThenStacksAcrossLaterCards()
    {
        var state = CreateState(
            "strangle",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.strangle"),
                Card(2, 102, "proto.silent.strangle"),
                Card(3, 103, "proto.silent.defend")
            ],
            hand: [1, 2, 3],
            enemies:
            [
                Enemy(
                    hp: 100,
                    block: 20)
            ]);

        var engine = new PrototypeGameEngine();

        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);
        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        Assert.Equal(100, enemy.Hp);
        Assert.Equal(12, enemy.Block);
        Assert.Single(
            enemy.PowerStates,
            power =>
                power.PowerId
                    == "proto.power.strangle");

        state = Play(
            engine,
            state,
            2,
            targetEnemyId: 1);
        combat = state.World!.Combat!;
        enemy = Assert.Single(combat.Enemies);
        Assert.Equal(98, enemy.Hp);
        Assert.Equal(4, enemy.Block);
        Assert.Equal(
            2,
            enemy.PowerStates.Count(
                power =>
                    power.PowerId
                        == "proto.power.strangle"));

        state = Play(
            engine,
            state,
            3);
        combat = state.World!.Combat!;
        enemy = Assert.Single(combat.Enemies);
        Assert.Equal(94, enemy.Hp);
        Assert.Equal(
            4,
            enemy.Block);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StrangleIsBlockedByArtifactAndExpiresAtEnemyTurnEnd()
    {
        var state = CreateState(
            "strangle-artifact",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.strangle"),
                Card(2, 102, "proto.silent.defend")
            ],
            hand: [1, 2],
            enemies:
            [
                Enemy(
                    hp: 100,
                    powers:
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.artifact",
                            1,
                            1)
                    ])
            ],
            nextPowerApplicationOrder: 2);

        var engine = new PrototypeGameEngine();
        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.DoesNotContain(
            enemy.PowerStates,
            power =>
                power.PowerId
                    == "proto.power.artifact");
        Assert.DoesNotContain(
            enemy.PowerStates,
            power =>
                power.PowerId
                    == "proto.power.strangle");

        // Apply an unblocked Strangle in a fresh state and verify its
        // enemy-side lifetime.
        state = CreateState(
            "strangle-expiry",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.strangle")
            ],
            hand: [1],
            enemies:
            [
                Enemy(hp: 100)
            ]);
        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);

        Assert.Contains(
            Assert.Single(
                state.World!.Combat!.Enemies)
                .PowerStates,
            power =>
                power.PowerId
                    == "proto.power.strangle");

        state = EndTurn(engine, state);

        Assert.DoesNotContain(
            Assert.Single(
                state.World!.Combat!.Enemies)
                .PowerStates,
            power =>
                power.PowerId
                    == "proto.power.strangle");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BatchOneCardsAreNowSinglePlayerRewardEligible()
    {
        var ids = new[]
        {
            "proto.silent.calculated_gamble",
            "proto.silent.expose",
            "proto.silent.outbreak",
            "proto.silent.storm_of_steel",
            "proto.silent.strangle"
        };

        Assert.All(
            ids,
            id =>
            {
                Assert.True(
                    PrototypeContent.Card(id)
                        .MechanicsImplemented);
                Assert.Contains(
                    id,
                    PrototypeContent.RewardCardPool);
            });
    }

    private static CombatCardInstance Card(
        long instanceId,
        long persistentId,
        string cardId,
        int upgradeLevel = 0) =>
        new(
            instanceId,
            persistentId,
            cardId,
            upgradeLevel,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int instanceId = 1,
        int hp = 100,
        int block = 0,
        Dictionary<string, int>? statuses = null,
        PrototypePowerInstanceState[]? powers = null) =>
        new(
            instanceId,
            "proto.enemy.crawler",
            hp,
            block,
            0,
            statuses
                ?? new Dictionary<string, int>(
                    StringComparer.Ordinal),
            Powers: powers);

    private static RunState CreateState(
        string seed,
        int energy,
        CombatCardInstance[] cards,
        long[] hand,
        long[]? drawPile = null,
        long[]? discardPile = null,
        long[]? exhaustPile = null,
        EnemyCombatState[]? enemies = null,
        long nextCardInstanceId = 100,
        long nextPowerApplicationOrder = 1)
    {
        var persistentCards = cards
            .Where(card =>
                card.PersistentCardInstanceId
                    is not null)
            .Select(card =>
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    PrototypeJson.EmptyObject()))
            .ToArray();
        var player = new PlayerState(
            70,
            70,
            0,
            persistentCards,
            Array.Empty<RelicInstance>(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand,
            DrawPile:
                drawPile ?? Array.Empty<long>(),
            DiscardPile:
                discardPile ?? Array.Empty<long>(),
            ExhaustPile:
                exhaustPile ?? Array.Empty<long>(),
            Enemies:
                enemies
                ?? [Enemy()],
            NextCardInstanceId:
                nextCardInstanceId,
            Cards: cards,
            PlayerPowers:
                Array.Empty<
                    PrototypePowerInstanceState>(),
            NextPowerApplicationOrder:
                nextPowerApplicationOrder);

        return new RunState(
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
                persistentCards
                    .Select(card =>
                        card.InstanceId)
                    .DefaultIfEmpty(0)
                    .Max() + 1,
                PrototypeRoomType.Combat,
                new MapState(
                    Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));
    }

    private static RunState Play(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId,
        int? targetEnemyId = null)
    {
        var action = engine.GetLegalActions(state)
            .Single(candidate =>
            {
                if (candidate.Kind != "play_card")
                {
                    return false;
                }

                var payload =
                    candidate.ReadPayload<
                        PlayCardPayload>();
                return payload.CardInstanceId
                        == cardInstanceId
                    && payload.TargetEnemyId
                        == targetEnemyId;
            });
        return engine.Step(state, action).State;
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(state)
            .Single(candidate =>
                candidate.Kind == "end_turn");
        return engine.Step(state, action).State;
    }
}
