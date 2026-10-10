using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

[Collection("MutablePrototypeContent")]
public sealed class PrototypeConditionalEnemyAiTests
{
    private const string EnemyId =
        "proto.test.conditional_enemy_ai";

    [Fact]
    public void ConditionalStateUsesAloneBeforeFrontAndBackPredicates()
    {
        WithDefinition(() =>
        {
            var engine = new PrototypeGameEngine();

            var alone = CreateState(
                Enemy(1, formationPosition: 5));
            alone = EndTurn(engine, alone);
            Assert.Equal(
                "alone",
                Assert.Single(
                    alone.World!.Combat!.Enemies)
                    .LastMoveId);

            var pair = CreateState(
                Enemy(1, formationPosition: 0),
                Enemy(2, formationPosition: 1));
            pair = EndTurn(engine, pair);
            Assert.Equal(
                new[] { "front", "back" },
                pair.World!.Combat!.Enemies
                    .Select(enemy => enemy.LastMoveId)
                    .ToArray());
        });
    }

    [Fact]
    public void FrontPredicateRecomputesOverLivingFormation()
    {
        WithDefinition(() =>
        {
            var engine = new PrototypeGameEngine();
            var state = CreateState(
                Enemy(
                    1,
                    hp: 0,
                    formationPosition: 0),
                Enemy(
                    2,
                    formationPosition: 1));

            state = EndTurn(engine, state);

            Assert.Null(
                state.World!.Combat!.Enemies[0]
                    .LastMoveId);
            Assert.Equal(
                "alone",
                state.World.Combat.Enemies[1]
                    .LastMoveId);
        });
    }

    [Fact]
    public void ConditionalStateCanMatchExactSlotName()
    {
        var enemies = Assert.IsType<
            Dictionary<
                string,
                PrototypeEnemyDefinition>>(
            PrototypeContent.Enemies);
        const string slotEnemyId =
            "proto.test.slot_conditional_enemy_ai";
        enemies.Add(
            slotEnemyId,
            new PrototypeEnemyDefinition(
                slotEnemyId,
                "Slot Conditional Fixture",
                100,
                0,
                [
                    new PrototypeEnemyMoveDefinition(
                        "slot_one",
                        []),
                    new PrototypeEnemyMoveDefinition(
                        "slot_two",
                        [])
                ],
                MovePolicy:
                    PrototypeEnemyMovePolicy.StateMachine,
                Ai: new PrototypeEnemyAiDefinition(
                    "init",
                    [
                        new PrototypeEnemyAiStateDefinition(
                            "slot_one",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 0),
                        new PrototypeEnemyAiStateDefinition(
                            "slot_two",
                            PrototypeEnemyAiStateKind.Move,
                            MoveIndex: 1),
                        new PrototypeEnemyAiStateDefinition(
                            "init",
                            PrototypeEnemyAiStateKind.Conditional,
                            ConditionalBranches:
                            [
                                new(
                                    "slot_one",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "wriggler1"),
                                new(
                                    "slot_two",
                                    PrototypeEnemyAiConditionKind
                                        .SlotNameEquals,
                                    "wriggler2")
                            ])
                    ])));

        try
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
                        slotEnemyId,
                        100,
                        0,
                        0,
                        new Dictionary<string, int>(
                            StringComparer.Ordinal),
                        SlotName: "wriggler2")
                ],
                NextCardInstanceId: 1,
                Cards: [],
                PlayerPowers: [],
                NextPowerApplicationOrder: 1);
            var state = new RunState(
                "prototype-unbound",
                "prototype-0.1",
                "slot-condition-test",
                "slot-condition-test",
                0,
                RunPhase.Combat,
                player,
                PrototypeRng.CreateBundle(
                    "slot-condition-test"),
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
            var engine = new PrototypeGameEngine();

            state = EndTurn(engine, state);

            Assert.Equal(
                "slot_two",
                Assert.Single(
                    state.World!.Combat!.Enemies)
                    .LastMoveId);
        }
        finally
        {
            enemies.Remove(slotEnemyId);
        }
    }

    private static void WithDefinition(Action action)
    {
        var enemies = Assert.IsType<
            Dictionary<
                string,
                PrototypeEnemyDefinition>>(
            PrototypeContent.Enemies);
        enemies.Add(
            EnemyId,
            BuildDefinition());

        try
        {
            action();
        }
        finally
        {
            enemies.Remove(EnemyId);
        }
    }

    private static PrototypeEnemyDefinition
        BuildDefinition() =>
        new(
            EnemyId,
            "Conditional Enemy AI Fixture",
            100,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "alone",
                    []),
                new PrototypeEnemyMoveDefinition(
                    "front",
                    []),
                new PrototypeEnemyMoveDefinition(
                    "back",
                    [])
            ],
            MovePolicy:
                PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "init",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "alone",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0,
                        NextStateId: "init"),
                    new PrototypeEnemyAiStateDefinition(
                        "front",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1,
                        NextStateId: "init"),
                    new PrototypeEnemyAiStateDefinition(
                        "back",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2,
                        NextStateId: "init"),
                    new PrototypeEnemyAiStateDefinition(
                        "init",
                        PrototypeEnemyAiStateKind.Conditional,
                        ConditionalBranches:
                        [
                            new(
                                "alone",
                                PrototypeEnemyAiConditionKind
                                    .IsAlone),
                            new(
                                "back",
                                PrototypeEnemyAiConditionKind
                                    .IsNotFront),
                            new(
                                "front",
                                PrototypeEnemyAiConditionKind
                                    .IsFront)
                        ])
                ]));

    private static EnemyCombatState Enemy(
        int instanceId,
        int hp = 100,
        int formationPosition = 0) =>
        new(
            instanceId,
            EnemyId,
            hp,
            0,
            0,
            new Dictionary<string, int>(
                StringComparer.Ordinal),
            FormationPosition: formationPosition);

    private static RunState CreateState(
        params EnemyCombatState[] enemies)
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
            Enemies: enemies,
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "conditional-enemy-ai-test",
            "conditional-enemy-ai-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "conditional-enemy-ai-test"),
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
