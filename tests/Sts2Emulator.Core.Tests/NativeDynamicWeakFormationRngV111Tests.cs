using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Formation RNG oracles transcribed from pinned v0.111.0 source:
/// SlimesWeak.GenerateMonsters(), CorpseSlugsWeak.GenerateMonsters()
/// and CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves().
/// </summary>
public sealed class NativeDynamicWeakFormationRngV111Tests
{
    private static RunState Start(string seed, ActIdentity act)
    {
        var config = RunConfiguration.Create(firstAct: act) with
        {
            FidelityId = V111Build.NativeWeakCombatEntryFidelityId
        };
        return new PrototypeGameEngine().Step(
            V111RunFactory.Create(seed, config),
            GameAction.Empty("start_run")).State;
    }

    private static NativeEncounterFormationV111 Resolve(
        RunState state, int floor, int column, string encounterId)
    {
        var node = state.World!.Map.Nodes.Single(point =>
            point.Floor == floor && point.NodeId.EndsWith(
                ":" + column, StringComparison.Ordinal));
        var world = state.World with
        {
            Floor = floor,
            ActiveRoom = PrototypeRoomType.Combat,
            Map = state.World.Map with { CurrentNodeId = node.NodeId }
        };
        return NativeActOneWeakCombatEntryV111.Prepare(
            state, world, PrototypeContent.Encounter(encounterId));
    }

    private static NativeRngSnapshot Cursor(
        NativeEncounterFormationV111 formation) =>
        NativeRngV111.ToSnapshot(formation.EncounterRng);

    private static void AssertIndependentCreatureStreams(
        NativeEncounterFormationV111 formation, string seed,
        int floor, int col)
    {
        for (var i = 0; i < formation.MonsterContexts.Length; i++)
        {
            var owned = formation.MonsterContexts[i];
            Assert.Equal(i + 1, owned.CombatId);
            Assert.Equal(formation.Enemies[i], owned.Enemy);
            var expected = NativeRngV111Contexts.ForMonster(
                seed, 0, owned.CombatId, col, floor - 1);
            Assert.Equal(NativeRngV111.ToSnapshot(expected),
                NativeRngV111.ToSnapshot(owned.Rng));
            Assert.Equal(0, NativeRngV111.ToSnapshot(owned.Rng).Counter);
        }
    }

    [Fact]
    public void SlimesWeakUsesExactThreeNativeNextItemCallsIncludingSingleton()
    {
        var small = new[]
        {
            "proto.enemy.leaf_slime_s", "proto.enemy.twig_slime_s"
        };
        var medium = new[]
        {
            "proto.enemy.leaf_slime_m", "proto.enemy.twig_slime_m"
        };
        var firstSelections = new HashSet<int>();
        var mediumSelections = new HashSet<int>();

        for (var i = 0; i < 96; i++)
        {
            var seed = "source-slimes-weak-" + i;
            var state = Start(seed, ActIdentity.Overgrowth);
            var map = state.World!.Map;
            var node = map.Nodes.First(node => node.Floor == 1);
            var column = int.Parse(node.NodeId.Split(':')[2]);
            var original = CanonicalJson.Sha256(state);
            var a = Resolve(state, 1, column, "proto.encounter.slimes_weak");
            var b = Resolve(state, 1, column, "proto.encounter.slimes_weak");
            var expected = NativeRngV111Contexts.ForEncounterFormation(
                seed, "SlimesWeak", totalFloor: 1);
            var rng = new RngBundle([expected]);
            var first = NativeRngV111.NextInt(rng, expected.StreamId, 2);
            var remaining = NativeRngV111.NextInt(
                rng, expected.StreamId, 1);
            var center = NativeRngV111.NextInt(rng, expected.StreamId, 2);
            Assert.Equal(0, remaining);
            firstSelections.Add(first);
            mediumSelections.Add(center);
            Assert.Equal(new[] {small[first], medium[center],
                small[1 - first]}, a.Enemies.Select(e => e.EnemyId));
            Assert.Equal(new[] {0, 1, 2},
                a.Enemies.Select(e => e.FormationPosition));
            Assert.All(a.Enemies, e => Assert.Null(e.SlotName));
            Assert.Null(a.EncounterOpeningOffset);
            Assert.Equal(3, Cursor(a).Counter);
            Assert.Equal(NativeRngV111.ToSnapshot(rng.Streams[0]),
                Cursor(a));
            Assert.Equal(CanonicalJson.Sha256(a),
                CanonicalJson.Sha256(b));
            AssertIndependentCreatureStreams(a, seed, 1, column);
            Assert.Equal(original, CanonicalJson.Sha256(state));
        }
        Assert.Equal(new[] {0, 1}, firstSelections.Order());
        Assert.Equal(new[] {0, 1}, mediumSelections.Order());
    }

    [Fact]
    public void CorpseSlugsWeakSamplesOneEncounterOpeningRotationAndNoOtherDraws()
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < 128; i++)
        {
            var seed = "source-slugs-weak-" + i;
            var state = Start(seed, ActIdentity.Underdocks);
            var node = state.World!.Map.Nodes.First(point =>
                point.Floor == 1);
            var column = int.Parse(node.NodeId.Split(':')[2]);
            var a = Resolve(state, 1, column,
                "proto.encounter.corpse_slugs_weak");
            Assert.Equal(new[]
            {
                "proto.enemy.corpse_slug", "proto.enemy.corpse_slug"
            }, a.Enemies.Select(e => e.EnemyId));
            Assert.Equal(new[] {0, 1},
                a.Enemies.Select(e => e.FormationPosition));
            Assert.Equal(1, Cursor(a).Counter);

            var expected = NativeRngV111Contexts.ForEncounterFormation(
                seed, "CorpseSlugsWeak", 1);
            var oracle = new RngBundle([expected]);
            var opener = NativeRngV111.NextInt(
                oracle, expected.StreamId, 3);
            seen.Add(opener);
            Assert.Equal(opener, a.EncounterOpeningOffset);
            Assert.Equal(NativeRngV111.ToSnapshot(oracle.Streams[0]),
                Cursor(a));
            AssertIndependentCreatureStreams(a, seed, 1, column);

            var fork = a.Fork();
            Assert.Equal(CanonicalJson.Sha256(a),
                CanonicalJson.Sha256(fork));
            var encounter = fork.EncounterRng;
            var changedBytes = encounter.StateBytes;
            changedBytes[0] ^= 1;
            fork = fork with
            {
                EncounterRng = encounter with
                { StateBytes = changedBytes }
            };
            Assert.NotEqual(CanonicalJson.Sha256(a),
                CanonicalJson.Sha256(fork));
            Assert.Equal(1, Cursor(a).Counter);
        }
        Assert.Equal(new[] {0, 1, 2}, seen.Order());
    }

    [Fact]
    public void EncounterSeedUsesNativeTotalFloorWhileMonsterSeedUsesMapCoordinates()
    {
        var seed = "dynamic-weak-floor-separation";
        var state = Start(seed, ActIdentity.Overgrowth);
        var map = state.World!.Map;
        var node1 = map.Nodes.First(node => node.Floor == 1);
        var node2 = map.Nodes.First(node => node.Floor == 2);
        var column1 = int.Parse(node1.NodeId.Split(':')[2]);
        var column2 = int.Parse(node2.NodeId.Split(':')[2]);
        var first = Resolve(state, 1, column1,
            "proto.encounter.slimes_weak");
        var later = Resolve(state, 2, column2,
            "proto.encounter.slimes_weak");
        var oracle1 = NativeRngV111Contexts.ForEncounterFormation(
            seed, "SlimesWeak", 1);
        var oracle2 = NativeRngV111Contexts.ForEncounterFormation(
            seed, "SlimesWeak", 2);
        Assert.Equal(3, Cursor(first).Counter);
        Assert.Equal(3, Cursor(later).Counter);
        Assert.NotEqual(Cursor(first), Cursor(later));
        Assert.NotEqual(
            NativeRngV111.ToSnapshot(first.MonsterContexts[0].Rng),
            NativeRngV111.ToSnapshot(later.MonsterContexts[0].Rng));
        Assert.NotEqual(
            NativeRngV111.ToSnapshot(oracle1),
            NativeRngV111.ToSnapshot(oracle2));
        AssertIndependentCreatureStreams(first, seed, 1, column1);
        AssertIndependentCreatureStreams(later, seed, 2, column2);
    }

    [Fact]
    public void LegacySlimesAndCorpseSlugsFormationResolverIsUnchanged()
    {
        foreach (var id in new[]
        {
            "proto.encounter.slimes_weak",
            "proto.encounter.corpse_slugs_weak"
        })
        {
            var definition = PrototypeContent.Encounter(id);
            for (var i = 0; i < 48; i++)
            {
                var seed = id + i;
                var one = PrototypeRng.CreateBundle(seed);
                var two = PrototypeRng.CreateBundle(seed);
                Assert.Equal(definition.ResolveEnemySpecs(one),
                    definition.ResolveEnemySpecs(two));
                Assert.Equal(CanonicalJson.Sha256(one),
                    CanonicalJson.Sha256(two));
            }
        }
    }
}
