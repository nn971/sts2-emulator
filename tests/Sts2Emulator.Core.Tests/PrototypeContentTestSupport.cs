using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

internal static class PrototypeContentTestSupport
{
    public static RunState ConfiguredNativeRun(
        string seed,
        ActIdentity firstAct = ActIdentity.Overgrowth)
    {
        var configuration = RunConfiguration.Create(firstAct: firstAct);
        var state = V111RunFactory.Create(seed, configuration);
        return new PrototypeGameEngine().Step(
            state, GameAction.Empty("start_run")).State;
    }

    public static Dictionary<string, PrototypeEnemyDefinition>
        MutableEnemyDefinitions() => GetDefinitions<PrototypeEnemyDefinition>(
            "EnemyDefinitions");

    public static Dictionary<string, PrototypePowerDefinition>
        MutablePowerDefinitions() => GetDefinitions<PrototypePowerDefinition>(
            "PowerDefinitions");

    private static Dictionary<string, T> GetDefinitions<T>(string fieldName)
        where T : class
    {
        var field = typeof(PrototypeContent).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(
                typeof(PrototypeContent).FullName, fieldName);
        return field.GetValue(null) as Dictionary<string, T>
            ?? throw new InvalidOperationException(
                $"Prototype content field {fieldName} is not mutable.");
    }
}
