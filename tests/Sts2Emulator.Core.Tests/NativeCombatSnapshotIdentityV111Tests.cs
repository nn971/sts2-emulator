using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Native v111 RNG stream cursors are owned by a particular encounter and
/// creature identity. A syntactically valid xoshiro cursor from a different
/// owner must not be accepted by deterministic replay or fork expansion.
/// </summary>
public sealed class NativeCombatSnapshotIdentityV111Tests
{
    private static CombatState Snapshot(string seed, int act = 2,
        long firstMonsterCombatId = 1) =>
        PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, act,
            act == 2
                ? PrototypeNativeHiveNormals.ExoskeletonsNormalId
                : PrototypeNativeGloryNormals.FrogKnightEncounterId,
            act == 2 ? "ExoskeletonsNormal" : "FrogKnightNormal",
            totalFloor: act == 2 ? 20 : 37,
            firstMonsterCombatId: firstMonsterCombatId,
            mapColumn: 3, mapRow: 5);

    private static CombatState ChangeFormation(CombatState original,
        Func<NativeEncounterFormationV111, NativeEncounterFormationV111> edit)
    {
        var fork = original.Fork();
        return fork with
        {
            NativeRng = fork.NativeRng! with
            {
                Formation = edit(fork.NativeRng.Formation)
            }
        };
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public void NativeOwnerIdentitySurvivesForkJsonAndCursorAdvancement(
        int act, long firstCombatId)
    {
        for (var seedIndex = 0; seedIndex < 12; seedIndex++)
        {
            var combat = Snapshot(
                $"native-owner-valid-{act}-{firstCombatId}-{seedIndex}",
                act, firstCombatId);
            NativeCombatRngEntryV111.Validate(combat);
            var hash = CanonicalJson.Sha256(combat);
            var restored = JsonSerializer.Deserialize<CombatState>(
                JsonSerializer.Serialize(combat))!;
            NativeCombatRngEntryV111.Validate(restored);
            Assert.Equal(hash, CanonicalJson.Sha256(restored));
            Assert.Equal(hash, CanonicalJson.Sha256(combat.Fork()));

            var id = combat.NativeRng!.Formation.MonsterContexts[0].CombatId;
            var advanced = NativeCombatRngEntryV111.AdvanceMonsterLocal(
                restored, id, 7, out _);
            NativeCombatRngEntryV111.Validate(advanced);
            Assert.Equal(hash, CanonicalJson.Sha256(combat));
            Assert.NotEqual(hash, CanonicalJson.Sha256(advanced));
        }
    }

    [Fact]
    public void SwappedOrDuplicatedValidMonsterStreamsAreRejected()
    {
        var original = Snapshot("native-owner-swap");
        var hash = CanonicalJson.Sha256(original);
        var contexts = original.NativeRng!.Formation.MonsterContexts;
        Assert.True(contexts.Length >= 2);

        // Stream state/codec remains valid; only the creature-local
        // ownership binding has been corrupted.
        var swapped = ChangeFormation(original, formation =>
        {
            var monsters = formation.MonsterContexts
                .Select(context => context.Fork()).ToArray();
            (monsters[0], monsters[1]) =
                (monsters[0] with { Rng = monsters[1].Rng.Fork() },
                 monsters[1] with { Rng = monsters[0].Rng.Fork() });
            return formation with { MonsterContexts = monsters };
        });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(swapped));

        var duplicated = ChangeFormation(original, formation =>
        {
            var monsters = formation.MonsterContexts
                .Select(context => context.Fork()).ToArray();
            monsters[1] = monsters[1] with
            {
                Rng = monsters[0].Rng.Fork()
            };
            return formation with { MonsterContexts = monsters };
        });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(duplicated));
        Assert.Equal(hash, CanonicalJson.Sha256(original));
    }

    [Fact]
    public void EncounterFloorAndProvenanceMismatchAreRejected()
    {
        var original = Snapshot("native-owner-encounter-provenance");
        var hash = CanonicalJson.Sha256(original);
        var formation = original.NativeRng!.Formation;
        var wrongEncounterStream = ChangeFormation(original, value =>
            value with
            {
                EncounterRng = value.EncounterRng with
                {
                    StreamId = "context.encounter.OtherEncounter.floor_20"
                }
            });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(wrongEncounterStream));

        var wrongFloor = ChangeFormation(original, value =>
            value with { TotalFloor = value.TotalFloor + 1 });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(wrongFloor));

        var wrongAct = ChangeFormation(original, value =>
            value with { NativeActIndex = 2 });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(wrongAct));

        var wrongColumn = ChangeFormation(original, value =>
            value with { MapColumn = 7 });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(wrongColumn));

        var changedColumn = ChangeFormation(original, value =>
            value with { MapColumn = 2 });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(changedColumn));

        var partialProvenance = ChangeFormation(original, value =>
            value with { NativeActIndex = null });
        Assert.Throws<InvalidOperationException>(() =>
            NativeCombatRngEntryV111.Validate(partialProvenance));

        // The complete legacy optional-provenance shape remains readable.
        var oldShape = ChangeFormation(original, value =>
            value with { NativeActIndex = null, RunSeed = null });
        NativeCombatRngEntryV111.Validate(oldShape);
        Assert.Equal(hash, CanonicalJson.Sha256(original));
        Assert.Equal(formation.RunSeed,
            original.NativeRng.Formation.RunSeed);
    }
}
