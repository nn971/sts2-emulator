using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NatureH4TerminalEnemyHpTests
{
    private static EnemyCombatState Enemy(
        int instanceId, string modelId, int hp, int maxHp,
        int priorPhaseHp = 0, bool minionPower = false) =>
        new(instanceId, modelId, hp, 0, 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Powers: minionPower
                ? [new PrototypePowerInstanceState("proto.power.minion", 1, 1L)]
                : [],
            MaxHp: maxHp, CompletedPhaseMaxHp: priorPhaseHp);

    private static CombatState Opening()
    {
        var source = new SemanticSeededEntryV111(
            "total-hp-encounter-test", 1,
            "proto.encounter.ceremonial_beast_boss",
            "CeremonialBeastBoss", 6, 10, 1, 3, 5,
            ReplayOpeningDrawHooks: true,
            FixtureDeckPresetId: SemanticConditionedDeckPresetV111.BossCycle20);
        return SemanticPilotCombatEngineV111.BeginSeeded(source)
            .Run.World!.Combat!;
    }

    [Fact]
    public void GremlinMercDeathSummonsCountAsRemainingEncounterHp()
    {
        var combat = Opening() with
        {
            Enemies = [
                Enemy(1, "proto.enemy.gremlin_merc", 0, 53),
                Enemy(2, "proto.enemy.sneaky_gremlin", 8, 15),
                Enemy(3, "proto.enemy.fat_gremlin", 13, 18)
            ]
        };
        var (total, remaining) =
            PrototypeGameEngine.NonMinionEncounterHp(combat);
        Assert.Equal(53 + 15 + 18, total);
        Assert.Equal(8 + 13, remaining);
    }

    [Fact]
    public void PhrogDeathStageCountsEveryNonMinionWriggler()
    {
        var combat = Opening() with
        {
            Enemies = [
                Enemy(1, "proto.enemy.phrog_parasite", 0, 68),
                Enemy(2, "proto.enemy.wriggler", 8, 18),
                Enemy(3, "proto.enemy.wriggler", 11, 19),
                Enemy(4, "proto.enemy.wriggler", 0, 20),
                Enemy(5, "proto.enemy.wriggler", 6, 21)
            ]
        };
        var (total, remaining) =
            PrototypeGameEngine.NonMinionEncounterHp(combat);
        Assert.Equal(68 + 18 + 19 + 20 + 21, total);
        Assert.Equal(8 + 11 + 6, remaining);
    }

    [Fact]
    public void MultiEnemyHpExcludesMinionsByDefinitionAndByPower()
    {
        var combat = Opening() with
        {
            Enemies = [
                Enemy(1, "proto.enemy.gremlin_merc", 0, 53),
                Enemy(2, "proto.enemy.wriggler", 7, 20),
                Enemy(3, "proto.enemy.phrog_parasite", 19, 68),
                Enemy(4, "proto.enemy.eye_with_teeth", 50, 50),
                Enemy(5, "proto.enemy.sneaky_gremlin", 40, 40,
                    minionPower: true)
            ]
        };
        var (total, remaining) =
            PrototypeGameEngine.NonMinionEncounterHp(combat);
        Assert.Equal(53 + 20 + 68, total);
        Assert.Equal(7 + 19, remaining);
    }

    [Fact]
    public void DefeatSnapshotsCompleteMultistageFormationBeforeTeardown()
    {
        var source = new SemanticSeededEntryV111(
            "gremlin-merc-hp-exit", 1,
            "proto.encounter.ceremonial_beast_boss",
            "CeremonialBeastBoss", 6, 10, 1, 3, 5,
            ReplayOpeningDrawHooks: true,
            FixtureDeckPresetId: SemanticConditionedDeckPresetV111.BossCycle20);
        var state = SemanticPilotCombatEngineV111.BeginSeeded(source).Run;
        var combat = state.World!.Combat! with
        {
            Enemies = [
                Enemy(1, "proto.enemy.gremlin_merc", 0, 53),
                Enemy(2, "proto.enemy.sneaky_gremlin", 8, 15),
                Enemy(3, "proto.enemy.fat_gremlin", 13, 18),
                Enemy(4, "proto.enemy.eye_with_teeth", 99, 99)
            ]
        };
        state = state with { World = state.World with { Combat = combat } };
        var end = typeof(PrototypeGameEngine).GetMethod(
            "EndRun", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(end);
        var terminal = Assert.IsType<RunState>(
            end.Invoke(null, new object[] { state, "defeat" }));
        Assert.Null(terminal.World!.Combat);
        Assert.Equal(120, terminal.World.DefeatRemainingEnemyHp);
        Assert.Equal(53 + 15 + 18,
            terminal.World.DefeatTotalNonMinionEnemyHp);
        Assert.Equal(8 + 13,
            terminal.World.DefeatRemainingNonMinionEnemyHp);
    }

    [Fact]
    public void RepeatedFormsOnSameEnemyCountEveryCompletedStage()
    {
        var combat = Opening() with
        {
            Enemies = [
                Enemy(1, "proto.native.glory.test_subject", 250, 313,
                    priorPhaseHp: 111 + 212)
            ]
        };
        var (total, remaining) =
            PrototypeGameEngine.NonMinionEncounterHp(combat);
        Assert.Equal(111 + 212 + 313, total);
        Assert.Equal(250, remaining);
    }

    [Fact]
    public void DefeatPreservesRemainingEnemyHealthBeforeCombatTeardown()
    {
        var source = new SemanticSeededEntryV111(
            "h4-terminal-enemy-hp-regression", 1,
            "proto.encounter.ceremonial_beast_boss",
            "CeremonialBeastBoss", 6, 10, 1, 3, 5,
            ReplayOpeningDrawHooks: true,
            FixtureDeckPresetId: SemanticConditionedDeckPresetV111.BossCycle20);
        var opening = SemanticPilotCombatEngineV111.BeginSeeded(source);
        var run = opening.Run;
        var expected = run.World!.Combat!.Enemies.Sum(enemy =>
            Math.Max(0, enemy.Hp));
        Assert.True(expected > 0);

        // EndRun is the point where the engine intentionally clears Combat.
        // Direct invocation isolates the reward snapshot contract from AI.
        var end = typeof(PrototypeGameEngine).GetMethod(
            "EndRun", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(end);
        var defeated = Assert.IsType<RunState>(
            end.Invoke(null, new object[] { run, "defeat" }));
        Assert.Null(defeated.World!.Combat);
        Assert.Equal(expected, defeated.World.DefeatRemainingEnemyHp);
        var nonMinion = PrototypeGameEngine.NonMinionEncounterHp(
            run.World.Combat);
        Assert.Equal(nonMinion.Total,
            defeated.World.DefeatTotalNonMinionEnemyHp);
        Assert.Equal(nonMinion.Remaining,
            defeated.World.DefeatRemainingNonMinionEnemyHp);
        Assert.Equal(RunPhase.Terminal, defeated.Phase);
    }
}
