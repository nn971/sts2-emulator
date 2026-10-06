using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRelicTests
{
    [Fact]
    public void InkBottleUsesCombatLocalCounterAndFiresEveryThirdCardPlayed()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(100, "proto.silent.strike", 0, empty),
                new CardInstance(101, "proto.silent.defend", 0, empty)
            ],
            [
                new RelicInstance("proto.relic.ink_bottle", empty)
            ],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1],
            DrawPile: [2],
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
                new CombatCardInstance(1, 100, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(2, 101, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 2,
            Relics:
            [
                new CombatRelicState(
                    PersistentIndex: 0,
                    RelicId: "proto.relic.ink_bottle",
                    ApplicationOrder: 1,
                    TriggerCounts: [2])
            ]);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "relic-counter-test",
            "relic-counter-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("relic-counter-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                102,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        PrototypeStateInvariants.Validate(state);

        var play = engine.GetLegalActions(state)
            .Single(action => action.Kind == "play_card");

        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        combat = state.World!.Combat!;
        Assert.Equal(3, Assert.Single(combat.RelicStates).TriggerCounts[0]);
        Assert.Equal(new long[] { 2 }, combat.Hand);
        Assert.Equal(new long[] { 1 }, combat.DiscardPile);

        // The counter is combat-local; persistent relic state remains untouched.
        Assert.Equal(
            "{}",
            state.Player.Relics[0].PersistentState.GetRawText());
    }

    [Fact]
    public void AllEnemyRelicTriggerIsSafeWhenCardAlreadyKilledLastEnemy()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [new CardInstance(200, "proto.silent.strike", 0, empty)],
            [new RelicInstance("proto.relic.letter_opener", empty)],
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
                    6,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 2,
            Cards:
            [
                new CombatCardInstance(1, 200, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 2,
            Relics:
            [
                new CombatRelicState(
                    PersistentIndex: 0,
                    RelicId: "proto.relic.letter_opener",
                    ApplicationOrder: 1,
                    TriggerCounts: [2])
            ]);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "relic-last-enemy-test",
            "relic-last-enemy-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("relic-last-enemy-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                201,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        var play = Assert.Single(engine.GetLegalActions(state)
            .Where(action => action.Kind == "play_card"));

        state = engine.Step(state, play).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(RunPhase.Reward, state.Phase);
    }

}
