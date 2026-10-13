namespace Sts2Emulator.Core;

public static class PrototypeEncounterFormationResolver
{
    public static PrototypeEncounterEnemySpec[] Resolve(
        PrototypeEncounterDefinition encounter,
        RngBundle rng)
    {
        return encounter.FormationPolicy switch
        {
            PrototypeEncounterFormationPolicy.Fixed =>
                encounter.FixedEnemySpecs
                    .Select(spec => spec with { })
                    .ToArray(),
            PrototypeEncounterFormationPolicy.ChooseDistinct =>
                ResolveDistinct(encounter, rng),
            _ => throw new ArgumentOutOfRangeException(
                nameof(encounter.FormationPolicy))
        };
    }

    private static PrototypeEncounterEnemySpec[] ResolveDistinct(
        PrototypeEncounterDefinition encounter,
        RngBundle rng)
    {
        var pool = encounter.EnemyPool
            ?? Array.Empty<string>();
        var count = encounter.EnemyCount;
        if (count <= 0 || count > pool.Length)
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' requests {count} distinct enemies from a pool of {pool.Length}.");
        }

        if (pool.Distinct(StringComparer.Ordinal).Count()
            != pool.Length)
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.Id}' has duplicate entries in its distinct enemy pool.");
        }

        var choices = (string[])pool.Clone();
        for (var index = 0; index < count; index++)
        {
            var swapIndex = index
                + PrototypeRng.NextInt(
                    rng,
                    "combat",
                    choices.Length - index);
            (choices[index], choices[swapIndex]) =
                (choices[swapIndex], choices[index]);
        }

        return choices
            .Take(count)
            .Select((enemyId, index) =>
                new PrototypeEncounterEnemySpec(
                    enemyId,
                    index))
            .ToArray();
    }
}
