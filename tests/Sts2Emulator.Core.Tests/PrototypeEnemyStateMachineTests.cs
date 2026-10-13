using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

[Collection("MutablePrototypeContent")]
public sealed class PrototypeEnemyStateMachineTests
{
    private const string EnemyId =
        "proto.test.enemy_state_machine";

    [Fact]
    public void MoveGraphTracksUseOnceAndCannotRepeatBranches()
    {
        WithDefinition(() =>
        {
            var engine = new PrototypeGameEngine();
            var state = CreateState();
            var observed = new List<string>();

            for (var turn = 0; turn < 9; turn++)
            {
                state = EndTurn(engine, state);
                observed.Add(
                    Assert.Single(
                        state.World!.Combat!.Enemies)
                        .LastMoveId!);
            }

            Assert.Equal("once", observed[0]);
            Assert.DoesNotContain(
                "once",
                observed.Skip(1));

            for (var index = 2;
                 index < observed.Count;
                 index++)
            {
                Assert.NotEqual(
                    observed[index - 1],
                    observed[index]);
            }

            var enemy = Assert.Single(
                state.World!.Combat!.Enemies);
            Assert.Equal(
                1,
                enemy.MoveUseCounts!["once"]);
            Assert.Equal(
                9,
                enemy.MoveUseCounts.Values.Sum());
            Assert.Equal(
                "rand",
                enemy.AiStateId);
        });
    }

    [Fact]
    public void RandomStateIgnoresNonPositiveWeightBranches()
    {
        var definition = new PrototypeEnemyDefinition(
            "proto.test.weight_filter",
            "Weight Filter",
            10,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "never",
                    []),
                new PrototypeEnemyMoveDefinition(
                    "always",
                    [])
            ],
            MovePolicy:
                PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "rand",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "never",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0,
                        NextStateId: "rand"),
                    new PrototypeEnemyAiStateDefinition(
                        "always",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1,
                        NextStateId: "rand"),
                    new PrototypeEnemyAiStateDefinition(
                        "rand",
                        PrototypeEnemyAiStateKind.Random,
                        Branches:
                        [
                            new(
                                "never",
                                Weight: 0),
                            new(
                                "always",
                                Weight: 7)
                        ])
                ]));

        WithDefinition(
            definition,
            () =>
            {
                var engine =
                    new PrototypeGameEngine();
                var state = CreateState(
                    definition.Id);

                state = EndTurn(
                    engine,
                    state);

                Assert.Equal(
                    "always",
                    Assert.Single(
                        state.World!.Combat!.Enemies)
                        .LastMoveId);
            });
    }

    private static void WithDefinition(
        Action action) =>
        WithDefinition(
            BuildDefinition(),
            action);

    private static void WithDefinition(
        PrototypeEnemyDefinition definition,
        Action action)
    {
        var enemies = Assert.IsType<
            Dictionary<
                string,
                PrototypeEnemyDefinition>>(
            PrototypeContent.Enemies);
        enemies.Add(
            definition.Id,
            definition);

        try
        {
            action();
        }
        finally
        {
            enemies.Remove(definition.Id);
        }
    }

    private static PrototypeEnemyDefinition
        BuildDefinition() =>
        new(
            EnemyId,
            "Enemy State Machine Fixture",
            999,
            0,
            [
                new PrototypeEnemyMoveDefinition(
                    "once",
                    []),
                new PrototypeEnemyMoveDefinition(
                    "a",
                    []),
                new PrototypeEnemyMoveDefinition(
                    "b",
                    [])
            ],
            MovePolicy:
                PrototypeEnemyMovePolicy.StateMachine,
            Ai: new PrototypeEnemyAiDefinition(
                "once",
                [
                    new PrototypeEnemyAiStateDefinition(
                        "once",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 0,
                        NextStateId: "rand"),
                    new PrototypeEnemyAiStateDefinition(
                        "a",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 1,
                        NextStateId: "rand"),
                    new PrototypeEnemyAiStateDefinition(
                        "b",
                        PrototypeEnemyAiStateKind.Move,
                        MoveIndex: 2,
                        NextStateId: "rand"),
                    new PrototypeEnemyAiStateDefinition(
                        "rand",
                        PrototypeEnemyAiStateKind.Random,
                        Branches:
                        [
                            new(
                                "once",
                                Weight: 100,
                                RepeatRule:
                                    PrototypeEnemyAiRepeatRule
                                        .UseOnlyOnce),
                            new(
                                "a",
                                RepeatRule:
                                    PrototypeEnemyAiRepeatRule
                                        .CannotRepeat),
                            new(
                                "b",
                                RepeatRule:
                                    PrototypeEnemyAiRepeatRule
                                        .CannotRepeat)
                        ])
                ]));

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state)
    {
        var action = engine.GetLegalActions(
                state)
            .Single(item =>
                item.Kind == "end_turn");
        return engine.Step(
            state,
            action).State;
    }

    private static RunState CreateState(
        string enemyId = EnemyId)
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
                    enemyId,
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId: 1,
            Cards: [],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "enemy-state-machine-test",
            "enemy-state-machine-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "enemy-state-machine-test"),
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
}
