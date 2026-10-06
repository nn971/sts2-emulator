using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePowerTests
{
    [Fact]
    public void FootworkAddsDexterityThatModifiesLaterBlockEffects()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(40, "proto.silent.footwork", 0, empty),
                new CardInstance(41, "proto.silent.defend", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
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
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 40, "proto.silent.footwork", 0, false, empty),
                new CombatCardInstance(2, 41, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateState("dex-test", player, combat, nextPersistentCardId: 42);
        var engine = new PrototypeGameEngine();

        var footwork = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, footwork).State;

        Assert.Equal(
            2,
            Assert.Single(state.World!.Combat!.PlayerPowers).Stacks);

        var defend = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, defend).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(7, state.World!.Combat!.PlayerBlock);
        Assert.Equal(1, state.World.Combat.Energy);
    }

    [Fact]
    public void NoxiousFumesTriggersAtRulesetPlayerTurnStartStep()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(50, "proto.silent.noxious_fumes", 0, empty)],
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
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                new EnemyCombatState(
                    2,
                    "proto.enemy.raider",
                    32,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 50, "proto.silent.noxious_fumes", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = CreateState("fumes-test", player, combat, nextPersistentCardId: 51);
        var engine = new PrototypeGameEngine();

        var fumes = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");
        state = engine.Step(state, fumes).State;

        var endTurn = engine.GetLegalActions(state)
            .Single(action => action.Kind == "end_turn");
        state = engine.Step(state, endTurn).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Equal(2, combat.Turn);
        Assert.All(
            combat.Enemies,
            enemy => Assert.Equal(1, enemy.Statuses["proto.status.poison"]));
        Assert.Equal(1, Assert.Single(combat.PlayerPowers).Stacks);
    }

    private static RunState CreateState(
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
