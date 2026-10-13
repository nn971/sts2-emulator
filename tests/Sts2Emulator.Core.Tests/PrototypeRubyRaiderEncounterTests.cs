using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeRubyRaiderEncounterTests
{
    private static readonly string[] RaiderIds =
    [
        "proto.enemy.assassin_ruby_raider",
        "proto.enemy.axe_ruby_raider",
        "proto.enemy.brute_ruby_raider",
        "proto.enemy.crossbow_ruby_raider",
        "proto.enemy.tracker_ruby_raider"
    ];

    [Fact]
    public void NormalEncounterChoosesThreeDistinctRaiders()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.ruby_raiders_normal");

        Assert.Equal(
            PrototypeEncounterFormationPolicy.ChooseDistinct,
            encounter.FormationPolicy);
        Assert.Equal(3, encounter.EnemyCount);
        Assert.Equal(
            RaiderIds.Order(StringComparer.Ordinal),
            encounter.EnemyPool!
                .Order(StringComparer.Ordinal));

        for (var seedIndex = 0;
             seedIndex < 128;
             seedIndex++)
        {
            var specs = encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    $"ruby-raiders-{seedIndex}"));

            Assert.Equal(3, specs.Length);
            Assert.Equal(
                3,
                specs.Select(spec => spec.EnemyId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.Equal(
                new[] { 0, 1, 2 },
                specs.Select(spec =>
                    spec.FormationPosition));
            Assert.All(
                specs,
                spec => Assert.Contains(
                    spec.EnemyId,
                    RaiderIds));
        }
    }

    [Fact]
    public void DeterministicSweepCanReachEveryRaiderType()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.ruby_raiders_normal");
        var seen = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seedIndex = 0;
             seedIndex < 256
             && seen.Count < RaiderIds.Length;
             seedIndex++)
        {
            foreach (var spec in encounter.ResolveEnemySpecs(
                         PrototypeRng.CreateBundle(
                             $"ruby-raider-coverage-{seedIndex}")))
            {
                seen.Add(spec.EnemyId);
            }
        }

        Assert.Equal(
            RaiderIds.Order(StringComparer.Ordinal),
            seen.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OvergrowthNormalInventoryContainsTwelveNativeEncounterIds()
    {
        Assert.Equal(
            12,
            PrototypeContent.OvergrowthNormalEncounterPool.Length);
        Assert.Equal(
            12,
            PrototypeContent.OvergrowthNormalEncounterPool
                .Distinct(StringComparer.Ordinal)
                .Count());

        Assert.Contains(
            "proto.encounter.ruby_raiders_normal",
            PrototypeContent.OvergrowthNormalEncounterPool);
        Assert.Contains(
            "proto.encounter.mawler_normal",
            PrototypeContent.OvergrowthNormalEncounterPool);
        Assert.Contains(
            "proto.encounter.cubex_construct_normal",
            PrototypeContent.OvergrowthNormalEncounterPool);
    }
}
