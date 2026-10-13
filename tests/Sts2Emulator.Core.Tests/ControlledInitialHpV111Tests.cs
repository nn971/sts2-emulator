using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledInitialHpV111Tests
{
    private static (CombatState Combat, NativeEncounterFormationV111 Formation, RngBundle Streams)
        Setup(string seed)
    {
        var native = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
            "MytesNormal", 21, mapColumn: 3, mapRow: 5,
            commitInitialIntents: false);
        return (native with { NativeRng = null },
            native.NativeRng!.Formation.Fork(), NativeRngV111.CreateBundle(seed));
    }

    [Fact]
    public void NativeTranscriptCanDriveSameHpMechanicsWithNoNativeCursorAdvance()
    {
        for (int n = 0; n < 32; n++)
        {
            string seed = "pending-native-replay-" + n;
            var (bare, formation, streams) = Setup(seed);
            var audit = NativeCombatRngEntryV111.AttachAndRollInitialHpWithAudit(
                bare, formation, streams);
            var current = ControlledInitialHpV111.Begin(bare, formation, streams);
            var originalCursorHash = CanonicalJson.Sha256(current.ReferenceStreams);
            Assert.Equal(0, current.NextEnemyIndex);
            Assert.NotNull(current.Pending);
            foreach (var expected in audit.Choices)
            {
                Assert.Equal(expected.Request.OrderedCandidates, current.Pending!.OrderedCandidates);
                Assert.Equal(expected.Request.OwnerId, current.Pending.OwnerId);
                string before = current.Fingerprint;
                var advanced = ControlledInitialHpV111.Resolve(
                    current, before, expected.SelectedIndex);
                Assert.Equal(before, current.Fingerprint);
                Assert.Equal(originalCursorHash,
                    CanonicalJson.Sha256(advanced.ReferenceStreams));
                Assert.Throws<InvalidOperationException>(() =>
                    ControlledInitialHpV111.Resolve(current, "stale-fingerprint", 0));
                current = advanced;
            }
            Assert.True(current.Complete);
            Assert.Null(current.Pending);
            Assert.Null(current.Combat.NativeRng);
            Assert.Equal(audit.Combat.Enemies.Select(e => (e.Hp, e.MaxHp)),
                current.Combat.Enemies.Select(e => (e.Hp, e.MaxHp)));
            Assert.Throws<InvalidOperationException>(() =>
                ControlledInitialHpV111.Resolve(current, current.Fingerprint, 0));
        }
    }

    [Fact]
    public void ResumeFromSavedBoundaryIsDeterministicAndForkIsolated()
    {
        var (bare, formation, streams) = Setup("pending-save-restore");
        var original = ControlledInitialHpV111.Begin(bare, formation, streams);
        var restored = ControlledInitialHpV111.Restore(
            ControlledInitialHpV111.Save(original));
        Assert.Equal(original.Fingerprint, restored.Fingerprint);
        var first = ControlledInitialHpV111.Resolve(original,
            original.Fingerprint, 0);
        var other = ControlledInitialHpV111.Resolve(restored,
            restored.Fingerprint, restored.Pending!.SupportCount - 1);
        Assert.Equal(original.Fingerprint,
            ControlledInitialHpV111.Restore(ControlledInitialHpV111.Save(original)).Fingerprint);
        Assert.Null(original.Combat.NativeRng);
        Assert.True(first.NextEnemyIndex == 1 && other.NextEnemyIndex == 1);
        Assert.Equal(0, original.NextEnemyIndex);
        var continued = ControlledInitialHpV111.Restore(
            ControlledInitialHpV111.Save(first));
        Assert.Equal(first.Fingerprint, continued.Fingerprint);
        var second = ControlledInitialHpV111.Resolve(
            continued, continued.Fingerprint, 0);
        Assert.True(second.Complete);
        Assert.Equal(2, second.Transcript.Length);
        Assert.Equal("controlled-v111", second.Transcript[0].ProviderMode);
    }

    [Fact]
    public void InvalidChoiceDoesNotMutateOrConsumeState()
    {
        var (bare, formation, streams) = Setup("pending-illegal");
        var current = ControlledInitialHpV111.Begin(bare, formation, streams);
        var before = current.Fingerprint;
        Assert.Throws<ArgumentException>(() =>
            ControlledInitialHpV111.Resolve(current, before, -1));
        Assert.Throws<ArgumentException>(() =>
            ControlledInitialHpV111.Resolve(current, before,
                current.Pending!.SupportCount));
        Assert.Equal(before, current.Fingerprint);

        var tampered = current.Fork();
        tampered.Pending!.OrderedCandidates[0] += 1000;
        Assert.Throws<InvalidOperationException>(() =>
            ControlledInitialHpV111.Resolve(tampered, tampered.Fingerprint, 0));
        Assert.Equal(before, current.Fingerprint);
    }

    [Fact]
    public void RestoringTamperedPendingFailsClosed()
    {
        var (bare, formation, streams) = Setup("pending-tamper");
        var pending = ControlledInitialHpV111.Begin(bare, formation, streams);
        var changed = pending with
        {
            Pending = pending.Pending! with
            {
                OwnerId = "creature:wrong"
            }
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledInitialHpV111.Restore(JsonSerializer.Serialize(changed)));
    }
}
