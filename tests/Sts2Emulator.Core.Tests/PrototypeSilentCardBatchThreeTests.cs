using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSilentCardBatchThreeTests
{
    [Fact]
    public void ShadowmeldDoublesBlockGainedThisTurn()
    {
        var state = CreateState(
            "shadowmeld",
            energy: 2,
            cards:
            [
                Card(1, 101, "proto.silent.shadowmeld"),
                Card(2, 102, "proto.silent.defend")
            ],
            hand: [1, 2]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);
        state = Play(engine, state, 2);

        var combat = state.World!.Combat!;
        Assert.Equal(10, combat.PlayerBlock);
        Assert.Contains(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.shadowmeld");

        state = EndTurn(engine, state);
        Assert.DoesNotContain(
            state.World!.Combat!.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.shadowmeld");
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void StackedShadowmeldMultipliesBlockPerStack()
    {
        var state = CreateState(
            "shadowmeld-stack",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.shadowmeld"),
                Card(2, 102, "proto.silent.shadowmeld"),
                Card(3, 103, "proto.silent.defend")
            ],
            hand: [1, 2, 3]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);
        state = Play(engine, state, 2);
        state = Play(engine, state, 3);

        Assert.Equal(
            20,
            state.World!.Combat!.PlayerBlock);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void WraithFormCapsTwoEnemyTurnsAndLosesDexterityEachPlayerTurn()
    {
        var state = CreateState(
            "wraith-form",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.wraith_form")
            ],
            hand: [1],
            enemy:
                Enemy(hp: 24));

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        var combat = state.World!.Combat!;
        Assert.Contains(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.intangible"
                && power.Stacks == 2);
        Assert.Contains(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.wraith_form");

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(69, state.Player.Hp);
        Assert.Contains(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.intangible"
                && power.Stacks == 1);
        Assert.Equal(
            -1,
            Assert.Single(
                combat.PlayerPowers,
                power =>
                    power.PowerId
                        == "proto.power.dexterity")
                .Stacks);

        state = EndTurn(engine, state);
        combat = state.World!.Combat!;
        Assert.Equal(68, state.Player.Hp);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power =>
                power.PowerId
                    == "proto.power.intangible");
        Assert.Equal(
            -2,
            Assert.Single(
                combat.PlayerPowers,
                power =>
                    power.PowerId
                        == "proto.power.dexterity")
                .Stacks);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void UpgradedWraithFormStartsWithThreeIntangible()
    {
        var state = CreateState(
            "wraith-form-plus",
            energy: 3,
            cards:
            [
                Card(
                    1,
                    101,
                    "proto.silent.wraith_form",
                    upgradeLevel: 1)
            ],
            hand: [1]);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        Assert.Equal(
            3,
            Assert.Single(
                state.World!.Combat!.PlayerPowers,
                power =>
                    power.PowerId
                        == "proto.power.intangible")
                .Stacks);
    }

    [Fact]
    public void ArtifactBlocksWraithFormDebuffButKeepsIntangible()
    {
        var state = CreateState(
            "wraith-form-artifact",
            energy: 3,
            cards:
            [
                Card(1, 101, "proto.silent.wraith_form")
            ],
            hand: [1],
            playerPowers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.artifact",
                    1,
                    1)
            ],
            nextPowerApplicationOrder: 2);

        var engine = new PrototypeGameEngine();
        state = Play(engine, state, 1);

        var powers =
            state.World!.Combat!.PlayerPowers;
        Assert.DoesNotContain(
            powers,
            power =>
                power.PowerId
                    == "proto.power.artifact");
        Assert.DoesNotContain(
            powers,
            power =>
                power.PowerId
                    == "proto.power.wraith_form");
        Assert.Contains(
            powers,
            power =>
                power.PowerId
                    == "proto.power.intangible"
                && power.Stacks == 2);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void BatchThreeCardsHaveExpectedPoolEligibility()
    {
        Assert.True(
            PrototypeContent.Card(
                "proto.silent.shadowmeld")
                .MechanicsImplemented);
        Assert.True(
            PrototypeContent.Card(
                "proto.silent.wraith_form")
                .MechanicsImplemented);

        Assert.Contains(
            "proto.silent.shadowmeld",
            PrototypeContent.RewardCardPool);
        Assert.DoesNotContain(
            "proto.silent.wraith_form",
            PrototypeContent.RewardCardPool);
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
        int hp = 100) =>
        new(
            1,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal));

    private static RunState CreateState(
        string seed,
        int energy,
        CombatCardInstance[] cards,
        long[] hand,
        EnemyCombatState? enemy = null,
        PrototypePowerInstanceState[]? playerPowers = null,
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
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies: [enemy ?? Enemy()],
            NextCardInstanceId: 100,
            Cards: cards,
            PlayerPowers:
                playerPowers
                ?? Array.Empty<
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
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(candidate =>
                candidate.Kind == "play_card"
                && candidate.ReadPayload<
                        PlayCardPayload>()
                    .CardInstanceId
                    == cardInstanceId);
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
