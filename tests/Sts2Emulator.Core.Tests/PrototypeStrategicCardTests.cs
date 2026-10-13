using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeStrategicCardTests
{
    [Fact]
    public void ConcentrateResumesAfterThreeCardDiscardAndGainsEnergy()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(1, "proto.silent.concentrate", 0, empty),
                new CardInstance(2, "proto.silent.strike", 0, empty),
                new CardInstance(3, "proto.silent.defend", 0, empty),
                new CardInstance(4, "proto.silent.slice", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 1,
            PlayerBlock: 0,
            Hand: [1, 2, 3, 4],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 5,
            Cards:
            [
                new CombatCardInstance(1, 1, "proto.silent.concentrate", 0, false, empty),
                new CombatCardInstance(2, 2, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(3, 3, "proto.silent.defend", 0, false, empty),
                new CombatCardInstance(4, 4, "proto.silent.slice", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateCombatState("concentrate-test", player, combat, 5);
        var engine = new PrototypeGameEngine();

        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        Assert.NotNull(state.World!.Combat!.PendingChoice);
        Assert.Equal(1, state.World.Combat.Energy);

        var discard = Assert.Single(engine.GetLegalActions(state));
        Assert.Equal(
            new long[] { 2, 3, 4 },
            discard.ReadPayload<SelectCardsPayload>().CardInstanceIds);

        state = engine.Step(state, discard).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Null(combat.PendingChoice);
        Assert.Equal(3, combat.Energy);
        Assert.Empty(combat.Hand);
        Assert.Equal(new long[] { 2, 3, 4, 1 }, combat.DiscardPile);
    }

    [Fact]
    public void UpgradedCatalystTriplesExistingPoisonAndExhausts()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(10, "proto.silent.catalyst", 1, empty)],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["proto.status.poison"] = 5
                    })
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 10, "proto.silent.catalyst", 1, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateCombatState("catalyst-test", player, combat, 11);
        var engine = new PrototypeGameEngine();

        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        var enemy = Assert.Single(combat.Enemies);
        Assert.Equal(15, enemy.Statuses["proto.status.poison"]);
        Assert.Equal(new long[] { 1 }, combat.ExhaustPile);
        Assert.Equal(2, combat.Energy);
    }

    [Fact]
    public void AdrenalineAddsEnergyDrawsAndExhausts()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(20, "proto.silent.adrenaline", 0, empty),
                new CardInstance(21, "proto.silent.strike", 0, empty),
                new CardInstance(22, "proto.silent.defend", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 1,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: [2, 3],
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    24,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 4,
            Cards:
            [
                new CombatCardInstance(1, 20, "proto.silent.adrenaline", 0, false, empty),
                new CombatCardInstance(2, 21, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(3, 22, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateCombatState("adrenaline-test", player, combat, 23);
        var engine = new PrototypeGameEngine();

        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Equal(2, combat.Energy);
        Assert.Equal(2, combat.Hand.Length);
        Assert.Equal(new long[] { 1 }, combat.ExhaustPile);
    }

    private static RunState CreateCombatState(
        string seed,
        PlayerState player,
        CombatState combat,
        long nextPersistentCardId)
    {
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            seed,
            seed,
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(seed),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                nextPersistentCardId,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));
    }
}
