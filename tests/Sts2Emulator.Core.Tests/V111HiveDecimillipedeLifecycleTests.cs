using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveDecimillipedeLifecycleTests
{
    private static readonly string[] Segments =
    [
        PrototypeNativeHiveElites.FrontId,
        PrototypeNativeHiveElites.MiddleId,
        PrototypeNativeHiveElites.BackId
    ];

    [Fact]
    public void ForcedEncounterHasDistinctEvenHpAndOneRotatingOpener()
    {
        var encounter = PrototypeContent.Encounter(
            PrototypeNativeHiveElites.DecimillipedeEncounterId);
        Assert.True(encounter.UniqueEvenEnemyHpWithinEncounter);
        Assert.Equal(new[] { "writhe", "bulk", "constrict" },
            encounter.CyclicOpeningAiStateIds);
        var method = typeof(PrototypeGameEngine).GetMethod("StartCombat",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        for (var seed = 0; seed < 12; seed++)
        {
            var state = BaseState("decimillipede-open-" + seed);
            var started = Assert.IsType<RunState>(method!.Invoke(null,
                [state, PrototypeRoomType.Elite, encounter]));
            var enemies = started.World!.Combat!.Enemies;
            Assert.Equal(3, enemies.Length);
            Assert.All(enemies, enemy =>
            {
                Assert.InRange(enemy.Hp, 40, 46);
                Assert.Equal(0, enemy.Hp % 2);
                Assert.NotNull(enemy.PlannedMoveIndex);
            });
            Assert.Equal(3, enemies.Select(enemy => enemy.Hp).Distinct().Count());
            var openers = enemies.Select(e => e.PlannedMoveIndex!.Value)
                .ToArray();
            Assert.Equal(3, openers.Distinct().Count());
            Assert.Equal((openers[0] + 1) % 3, openers[1]);
            Assert.Equal((openers[1] + 1) % 3, openers[2]);
        }
    }

    [Fact]
    public void DeadSegmentSpendsDeadTurnThenReattachesForTwentyFive()
    {
        var engine = new PrototypeGameEngine();
        var state = BaseState("decimillipede-reattach");
        var combat = state.World!.Combat!;
        var enemies = Segments.Select((id, i) =>
            new EnemyCombatState(
                InstanceId: i + 1,
                EnemyId: id,
                Hp: i == 0 ? 0 : 42,
                Block: 0,
                MoveIndex: 0,
                Statuses: new Dictionary<string, int>(StringComparer.Ordinal),
                Powers:
                [
                    new PrototypePowerInstanceState(
                        PrototypeNativeHiveElites.ReattachId, 25, i + 1)
                ],
                FormationPosition: i,
                SlotName: "segment" + (i + 1),
                AiStateId: "writhe")).ToArray();
        state = state with
        {
            World = state.World with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    combat with { Enemies = enemies }, state.Rng)
            }
        };
        var before = CanonicalJson.Sha256(state);
        state = EndTurn(engine, state);
        var dead = Assert.Single(state.World!.Combat!.Enemies,
            e => e.InstanceId == 1);
        Assert.Equal(0, dead.Hp);
        Assert.True(dead.ReattachDeadMoveResolved);
        Assert.Equal("dead", dead.LastMoveId);
        Assert.Equal(before, CanonicalJson.Sha256(
            BaseStateWithFormation("decimillipede-reattach", enemies)));

        state = EndTurn(engine, state);
        var back = Assert.Single(state.World!.Combat!.Enemies,
            e => e.InstanceId == 1);
        Assert.Equal(25, back.Hp);
        Assert.False(back.ReattachDeadMoveResolved);
        Assert.Equal("reattach", back.LastMoveId);
        Assert.InRange(back.PlannedMoveIndex!.Value, 0, 2);
    }

    private static RunState BaseStateWithFormation(
        string seed, EnemyCombatState[] enemies)
    {
        var state = BaseState(seed);
        return state with
        {
            World = state.World! with
            {
                Combat = PrototypeGameEngine.CommitEnemyIntents(
                    state.World.Combat! with { Enemies = enemies },
                    state.Rng)
            }
        };
    }

    private static RunState BaseState(string seed)
    {
        var combat = new CombatState(
            1, 3, 0, [], [], [], [], [], 1, [], [], 1,
            Act: 2);
        return new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(5000, 5000, 0, [], [],
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 2, 1, 1,
                PrototypeRoomType.Elite, new MapState([]),
                combat, null, null, null, null));
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine, RunState state) =>
        engine.Step(state, engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn")).State;
}
