using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeCombatRngEntryV111Tests
{
    private const string Exoskeletons =
        PrototypeNativeHiveNormals.ExoskeletonsNormalId;
    private const string Bowlbugs =
        PrototypeNativeHiveNormals.BowlbugsNormalId;

    private static CombatState Snapshot(
        string seed, string id = Exoskeletons,
        string native = "ExoskeletonsNormal", bool commit = true) =>
        PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, id, native, totalFloor: 20,
            mapColumn: 3, mapRow: 5,
            commitInitialIntents: commit);

    private static NativeRngSnapshot RunCursor(
        CombatState combat, string streamId) =>
        NativeRngV111.ToSnapshot(
            combat.NativeRng!.RunStreams.Streams.Single(
                stream => stream.StreamId == streamId));

    [Fact]
    public void EveryNativeEnemyHpRollConsumesExactlyOneNicheDraw()
    {
        for (var i = 0; i < 32; i++)
        {
            var combat = Snapshot($"niche-hp-{i}", commit: false);
            Assert.Equal(4, combat.Enemies.Length);
            Assert.Equal(4, RunCursor(combat, "run.niche").Counter);
            Assert.Equal(0, RunCursor(combat, "run.monster_ai").Counter);
            Assert.All(combat.Enemies, enemy =>
            {
                var range = PrototypeContent.Enemy(enemy.EnemyId)
                    .HpRangeAt(combat.Act, combat.Ascension);
                Assert.InRange(enemy.Hp, range.Min, range.Max);
                Assert.Equal(enemy.Hp, enemy.MaxHp);
            });
            NativeCombatRngEntryV111.Validate(combat);

            var native = combat.NativeRng!;
            Assert.Equal(0,
                NativeRngV111.ToSnapshot(
                    native.Formation.EncounterRng).Counter);
            Assert.All(native.Formation.MonsterContexts,
                monster => Assert.Equal(0,
                    NativeRngV111.ToSnapshot(monster.Rng).Counter));
            Assert.All(native.RunStreams.Streams
                    .Where(stream => stream.StreamId != "run.niche"),
                stream => Assert.Equal(0,
                    NativeRngV111.ToSnapshot(stream).Counter));
        }
    }

    [Fact]
    public void NicheSelectsDistinctOverlappingHpAndUsesNativeRangeOrder()
    {
        for (var iteration = 0; iteration < 48; iteration++)
        {
            var seed = $"native-multiple-myting-{iteration}";
            var combat = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
                "MytesNormal", 21, mapColumn: 3, mapRow: 5,
                commitInitialIntents: false);
            Assert.Equal(2, combat.Enemies.Length);
            Assert.NotEqual(combat.Enemies[0].Hp, combat.Enemies[1].Hp);

            var oracle = NativeRngV111.CreateBundle(seed);
            var occupied = new HashSet<int>();
            foreach (var enemy in combat.Enemies)
            {
                var range = PrototypeContent.Enemy(enemy.EnemyId)
                    .HpRangeAt(combat.Act, combat.Ascension);
                var values = Enumerable.Range(
                        range.Min, range.Max - range.Min + 1)
                    .Where(hp => !occupied.Contains(hp)).ToArray();
                var selected = values[NativeRngV111.NextInt(
                    oracle, "run.niche", values.Length)];
                Assert.Equal(selected, enemy.Hp);
                occupied.Add(selected);
            }
            Assert.Equal(
                NativeRngV111.ToSnapshot(
                    oracle.Streams.Single(s => s.StreamId == "run.niche")),
                RunCursor(combat, "run.niche"));
        }
    }

    [Fact]
    public void FixedHpRangeStillUsesOneNicheDraw()
    {
        var snapshot = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            "fixed-hp-sample", 3,
            PrototypeNativeGloryNormals.FrogKnightEncounterId,
            "FrogKnightNormal", 37,
            commitInitialIntents: false);
        var enemy = Assert.Single(snapshot.Enemies);
        var expected = PrototypeContent.Enemy(enemy.EnemyId)
            .HpRangeAt(3, 0);
        Assert.Equal(expected.Max, enemy.Hp);
        Assert.Equal(1, RunCursor(snapshot, "run.niche").Counter);
    }

    [Fact]
    public void NativeAiConsumesMonsterAiOnlyAndNeverPrototypeStream()
    {
        for (var i = 0; i < 96; i++)
        {
            var seed = $"native-ai-{i}";
            var prepared = Snapshot(seed, commit: false);
            var before = CanonicalJson.Sha256(prepared);
            var originalNative = prepared.NativeRng!;
            var committed = NativeCombatRngEntryV111.CommitIntents(prepared);
            var direct = Snapshot(seed);
            Assert.Equal(CanonicalJson.Sha256(committed),
                CanonicalJson.Sha256(direct));
            Assert.Equal(before, CanonicalJson.Sha256(prepared));
            Assert.Equal(4, RunCursor(committed, "run.niche").Counter);
            Assert.Equal(1, RunCursor(committed, "run.monster_ai").Counter);
            Assert.Equal(0, RunCursor(prepared, "run.monster_ai").Counter);
            Assert.Equal(RunCursor(prepared, "run.niche"),
                RunCursor(committed, "run.niche"));
            Assert.All(originalNative.Formation.MonsterContexts,
                context => Assert.Equal(0,
                    NativeRngV111.ToSnapshot(context.Rng).Counter));

            var fourth = committed.Enemies[3];
            Assert.Contains(fourth.PlannedMoveIndex, new int?[] { 0, 1 });
            Assert.All(committed.Enemies.Take(3),
                (enemy) => Assert.NotNull(enemy.PlannedMoveIndex));
        }
    }

    [Fact]
    public void MonsterLocalAdvanceOnlyMovesOneCreatureCursor()
    {
        var combat = Snapshot("isolated-monster-context");
        var initialHash = CanonicalJson.Sha256(combat);
        var id = combat.NativeRng!.Formation.MonsterContexts[1].CombatId;
        var next = NativeCombatRngEntryV111.AdvanceMonsterLocal(
            combat, id, 7, out var result);
        Assert.InRange(result, 0, 6);
        Assert.Equal(initialHash, CanonicalJson.Sha256(combat));
        Assert.Equal(RunCursor(combat, "run.niche"),
            RunCursor(next, "run.niche"));
        Assert.Equal(RunCursor(combat, "run.monster_ai"),
            RunCursor(next, "run.monster_ai"));
        Assert.Equal(
            NativeRngV111.ToSnapshot(combat.NativeRng.Formation.EncounterRng),
            NativeRngV111.ToSnapshot(next.NativeRng!.Formation.EncounterRng));
        var original = combat.NativeRng.Formation.MonsterContexts;
        var advanced = next.NativeRng.Formation.MonsterContexts;
        for (var i = 0; i < original.Length; i++)
        {
            var initialCounter = NativeRngV111.ToSnapshot(original[i].Rng).Counter;
            Assert.Equal(initialCounter + (i == 1 ? 1 : 0),
                NativeRngV111.ToSnapshot(advanced[i].Rng).Counter);
        }
    }

    [Fact]
    public void NativeCombatSidecarForkAndJsonSnapshotDoNotAliasState()
    {
        var combat = Snapshot("native-combat-fork-and-json");
        var hash = CanonicalJson.Sha256(combat);
        var fork = combat.Fork();
        var restored = JsonSerializer.Deserialize<CombatState>(
            JsonSerializer.Serialize(combat))!;
        Assert.Equal(hash, CanonicalJson.Sha256(fork));
        Assert.Equal(hash, CanonicalJson.Sha256(restored));
        NativeCombatRngEntryV111.Validate(restored);
        var altered = NativeCombatRngEntryV111.AdvanceMonsterLocal(
            fork, fork.NativeRng!.Formation.MonsterContexts[0].CombatId,
            6, out _);
        Assert.NotEqual(hash, CanonicalJson.Sha256(altered));
        Assert.Equal(hash, CanonicalJson.Sha256(combat));
        Assert.Equal(hash, CanonicalJson.Sha256(restored));
        fork.NativeRng!.RunStreams.Streams[0].StateBytes[0] ^= 1;
        Assert.Equal(hash, CanonicalJson.Sha256(combat));
        Assert.Equal(hash, CanonicalJson.Sha256(restored));
    }

    [Fact]
    public void InvalidFormationOrStreamOwnershipIsRejectedWithoutMutation()
    {
        var combat = Snapshot("native-invalid-bindings");
        var unchanged = CanonicalJson.Sha256(combat);
        var without = combat with { NativeRng = null };
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.CommitIntents(without));
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.AttachAndRollInitialHp(
                combat, combat.NativeRng!.Formation,
                NativeRngV111.CreateBundle("already-attached")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NativeCombatRngEntryV111.AdvanceMonsterLocal(
                combat, 987654, 2, out _));

        var malformed = combat.Fork();
        Assert.Throws<ArgumentException>(() =>
            malformed.NativeRng!.RunStreams.ReplaceStreamAt(0,
                malformed.NativeRng.RunStreams.Streams[1].Fork()));

        var wrongFormation = combat.Fork();
        var wrongFormationEnemies = wrongFormation.Enemies;
        wrongFormationEnemies[0] =
            wrongFormationEnemies[0] with { SlotName = "not-original" };
        wrongFormation = wrongFormation with
        { Enemies = wrongFormationEnemies };
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(wrongFormation));
        Assert.Equal(unchanged, CanonicalJson.Sha256(combat));
    }

    [Theory]
    [InlineData(2, PrototypeNativeHiveNormals.BowlbugsNormalId,
        "BowlbugsNormal")]
    [InlineData(3, PrototypeNativeGloryNormals.FrogKnightEncounterId,
        "FrogKnightNormal")]
    public void NativeFixtureReplaysAcrossMultipleInitialConditions(
        int act, string emulatorId, string nativeEntry)
    {
        for (var i = 0; i < 24; i++)
        {
            var seed = $"native-combat-entry-{act}-{i}";
            var left = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                seed, act, emulatorId, nativeEntry,
                totalFloor: 13 + i, ascension: i % 11,
                firstMonsterCombatId: 2, mapColumn: i % 7,
                mapRow: i % 8);
            var right = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                seed, act, emulatorId, nativeEntry,
                totalFloor: 13 + i, ascension: i % 11,
                firstMonsterCombatId: 2, mapColumn: i % 7,
                mapRow: i % 8);
            Assert.Equal(CanonicalJson.Sha256(left),
                CanonicalJson.Sha256(right));
            Assert.Equal(CanonicalJson.Sha256(left),
                CanonicalJson.Sha256(left.Fork()));
            NativeCombatRngEntryV111.Validate(left);
        }
    }
}
