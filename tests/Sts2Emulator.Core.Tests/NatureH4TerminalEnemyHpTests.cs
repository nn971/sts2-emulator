using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NatureH4TerminalEnemyHpTests
{
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
        Assert.Equal(RunPhase.Terminal, defeated.Phase);
    }
}
