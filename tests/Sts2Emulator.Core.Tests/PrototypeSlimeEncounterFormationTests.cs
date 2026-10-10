using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeSlimeEncounterFormationTests
{
    private static readonly string[] SmallSlimes =
    [
        "proto.enemy.leaf_slime_s",
        "proto.enemy.twig_slime_s"
    ];

    private static readonly string[] MediumSlimes =
    [
        "proto.enemy.leaf_slime_m",
        "proto.enemy.twig_slime_m"
    ];

    [Fact]
    public void WeakSlimesUseDifferentSmallsAroundOneRandomMedium()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.slimes_weak");

        Assert.Equal(0, encounter.Weight);

        var seenMediums = new HashSet<string>(
            StringComparer.Ordinal);
        var seenSmallOrders = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seed = 0; seed < 64; seed++)
        {
            var specs = encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    $"slimes-weak-{seed}"));

            Assert.Equal(
                [0, 1, 2],
                specs.Select(spec =>
                    spec.FormationPosition));

            Assert.Contains(
                specs[0].EnemyId,
                SmallSlimes);
            Assert.Contains(
                specs[2].EnemyId,
                SmallSlimes);
            Assert.NotEqual(
                specs[0].EnemyId,
                specs[2].EnemyId);
            Assert.Contains(
                specs[1].EnemyId,
                MediumSlimes);

            seenMediums.Add(specs[1].EnemyId);
            seenSmallOrders.Add(
                $"{specs[0].EnemyId}|{specs[2].EnemyId}");
        }

        Assert.Equal(
            MediumSlimes.OrderBy(item => item),
            seenMediums.OrderBy(item => item));
        Assert.Equal(2, seenSmallOrders.Count);
    }

    [Fact]
    public void NormalSlimesFixBothMediumsThenRandomizeSmallOrder()
    {
        var encounter = PrototypeContent.Encounter(
            "proto.encounter.slimes_normal");

        Assert.Equal(0, encounter.Weight);

        var seenSmallOrders = new HashSet<string>(
            StringComparer.Ordinal);

        for (var seed = 0; seed < 64; seed++)
        {
            var specs = encounter.ResolveEnemySpecs(
                PrototypeRng.CreateBundle(
                    $"slimes-normal-{seed}"));

            Assert.Equal(4, specs.Length);
            Assert.Equal(
                [0, 1, 2, 3],
                specs.Select(spec =>
                    spec.FormationPosition));
            Assert.Equal(
                "proto.enemy.twig_slime_m",
                specs[0].EnemyId);
            Assert.Equal(
                "proto.enemy.leaf_slime_m",
                specs[1].EnemyId);
            Assert.Contains(
                specs[2].EnemyId,
                SmallSlimes);
            Assert.Contains(
                specs[3].EnemyId,
                SmallSlimes);
            Assert.NotEqual(
                specs[2].EnemyId,
                specs[3].EnemyId);

            seenSmallOrders.Add(
                $"{specs[2].EnemyId}|{specs[3].EnemyId}");
        }

        Assert.Equal(2, seenSmallOrders.Count);
    }

    [Fact]
    public void SelectionGroupRejectsImpossibleDistinctRequest()
    {
        var encounter = new PrototypeEncounterDefinition(
            "proto.test.impossible_group",
            PrototypeRoomType.Combat,
            ["proto.enemy.twig_slime_s"],
            SelectionGroups:
            [
                new PrototypeEncounterSelectionGroup(
                    ["proto.enemy.twig_slime_s"],
                    [0, 1],
                    ChooseDistinct: true)
            ]);

        var error = Assert.Throws<
            InvalidOperationException>(() =>
                encounter.ResolveEnemySpecs(
                    PrototypeRng.CreateBundle(
                        "impossible-group")));

        Assert.Contains(
            "cannot choose 2 distinct enemies",
            error.Message);
    }
}
