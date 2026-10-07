using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSilentCardBatchTwoTests
{
    [Fact]
    public void AccelerantMakesPoisonTriggerAdditionalTimes()
    {
        var state = CreateState(
            "accelerant",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.accelerant")
            ],
            hand: [1],
            enemies:
            [
                Enemy(
                    hp: 40,
                    statuses:
                        new Dictionary<string, int>(
                            StringComparer.Ordinal)
                        {
                            ["proto.status.poison"] = 3
                        })
            ]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        Assert.Contains(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.accelerant"
                && power.Stacks == 1);

        state = EndTurn(engine, state);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(35, enemy.Hp);
        Assert.Equal(
            1,
            enemy.Statuses[
                "proto.status.poison"]);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void AccelerantAlsoAffectsImmediateOutbreakPoisonTrigger()
    {
        var state = CreateState(
            "accelerant-outbreak",
            energy: 4,
            cards:
            [
                Card(1, 101, "proto.silent.accelerant"),
                Card(2, 102, "proto.silent.outbreak")
            ],
            hand: [1, 2],
            enemies:
            [
                Enemy(hp: 50)
            ]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);
        state = Play(engine, state, 2);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(33, enemy.Hp);
        Assert.Equal(
            7,
            enemy.Statuses[
                "proto.status.poison"]);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void MalaiseUsesAllEnergyForSignedStrengthAndWeak()
    {
        var state = CreateState(
            "malaise",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.malaise")
            ],
            hand: [1],
            enemies:
            [
                Enemy(
                    hp: 50,
                    powers:
                    [
                        new PrototypePowerInstanceState(
                            "proto.power.strength",
                            2,
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

        var combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        Assert.Equal(0, combat.Energy);
        Assert.Equal(
            -1,
            Assert.Single(
                enemy.PowerStates,
                power =>
                    power.PowerId
                        == "proto.power.strength")
                .Stacks);
        Assert.Equal(
            3,
            enemy.Statuses[
                "proto.status.weak"]);
        Assert.Equal(
            new long[] { 1 },
            combat.ExhaustPile);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UpgradedMalaiseAddsOneToBothDebuffs()
    {
        var state = CreateState(
            "malaise-plus",
            energy: 2,
            cards:
            [
                Card(
                    1,
                    101,
                    "proto.silent.malaise",
                    upgradeLevel: 1)
            ],
            hand: [1],
            enemies:
            [
                Enemy(hp: 50)
            ]);

        var engine = new PrototypeGameEngine();
        state = Play(
            engine,
            state,
            1,
            targetEnemyId: 1);

        var enemy = Assert.Single(
            state.World!.Combat!.Enemies);
        Assert.Equal(
            -3,
            Assert.Single(
                enemy.PowerStates,
                power =>
                    power.PowerId
                        == "proto.power.strength")
                .Stacks);
        Assert.Equal(
            3,
            enemy.Statuses[
                "proto.status.weak"]);
    }

    [Fact]
    public void ArtifactBlocksMalaiseStrengthLossBeforeWeak()
    {
        var state = CreateState(
            "malaise-artifact",
            energy: 2,
            cards:
            [
                Card(1, 101, "proto.silent.malaise")
            ],
            hand: [1],
            enemies:
            [
                Enemy(
                    hp: 50,
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
                    == "proto.power.strength");
        Assert.Equal(
            2,
            enemy.Statuses[
                "proto.status.weak"]);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ShadowStepDiscardsHandThenDoublesNextTurnAttacks()
    {
        var state = CreateState(
            "shadow-step",
            energy: 1,
            cards:
            [
                Card(1, 101, "proto.silent.shadow_step"),
                Card(2, 102, "proto.silent.defend"),
                Card(3, 103, "proto.silent.strike")
            ],
            hand: [1, 2],
            drawPile: [3],
            enemies:
            [
                Enemy(hp: 50)
            ]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        Assert.Equal(
            new long[] { 2, 1 },
            combat.DiscardPile);
        Assert.Contains(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.shadow_step");

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;

        Assert.DoesNotContain(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.shadow_step");
        Assert.Contains(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.double_damage"
                && power.Stacks == 1);
        Assert.Contains(3L, combat.Hand);

        state = Play(
            engine,
            state,
            3,
            targetEnemyId: 1);
        Assert.Equal(
            38,
            Assert.Single(
                state.World!.Combat!.Enemies).Hp);

        state = EndTurn(engine, state);
        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.double_damage");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BatchTwoCardsAreNowSinglePlayerRewardEligible()
    {
        var ids = new[]
        {
            "proto.silent.accelerant",
            "proto.silent.malaise",
            "proto.silent.shadow_step"
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
        EnemyCombatState[]? enemies = null,
        long nextCardInstanceId = 100,
        long nextPowerApplicationOrder = 1)
    {
        var persistentCards = cards
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
                Array.Empty<long>(),
            ExhaustPile:
                Array.Empty<long>(),
            Enemies:
                enemies ?? [Enemy()],
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
