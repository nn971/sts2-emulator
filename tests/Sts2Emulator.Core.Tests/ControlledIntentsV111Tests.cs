using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledIntentsV111Tests
{
    private static CombatState Prepared(string seed) =>
        PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, mapColumn: 3, mapRow: 5,
            commitInitialIntents: false);

    [Fact]
    public void NativeTicketsReproduceAllCommittedPlansWithCounterfactualCursorUntouched()
    {
        for (int n = 0; n < 48; n++)
        {
            var before = Prepared("controlled-intents-" + n);
            var audit = NativeCombatRngEntryV111.CommitIntentsWithAudit(before);
            var controlled = ControlledIntentsV111.Begin(before);
            Assert.NotNull(controlled.Pending);
            Assert.Equal("combat.committed_intent.index_draw",
                controlled.Pending!.SiteId);
            Assert.Equal("bounded_integer", controlled.Pending.OperationKind);
            Assert.Equal(3, controlled.NextEnemyIndex); // first three deterministic
            string nativeCursor = CanonicalJson.Sha256(controlled.ReferenceStreams);
            var untouched = CanonicalJson.Sha256(before);
            foreach (var expected in audit.Choices)
            {
                Assert.NotNull(controlled.Pending);
                Assert.Equal(expected.Request.OwnerId, controlled.Pending!.OwnerId);
                Assert.Equal(expected.Request.Ordinal, controlled.Pending.Ordinal);
                Assert.Equal(expected.Request.BoundedCount, controlled.Pending.BoundedCount);
                var oldFingerprint = controlled.Fingerprint;
                var advanced = ControlledIntentsV111.Resolve(
                    controlled, oldFingerprint, expected.SelectedIndex);
                Assert.Equal(oldFingerprint, controlled.Fingerprint);
                Assert.Equal(nativeCursor,
                    CanonicalJson.Sha256(advanced.ReferenceStreams));
                controlled = advanced;
            }
            Assert.True(controlled.Complete);
            Assert.Equal(before.Enemies.Length, controlled.NextEnemyIndex);
            Assert.Null(controlled.Combat.NativeRng);
            Assert.Equal(untouched, CanonicalJson.Sha256(before));
            Assert.Equal(
                audit.Combat.Enemies.Select(x => (x.PlannedMoveIndex, x.PlannedNextAiStateId)),
                controlled.Combat.Enemies.Select(x => (x.PlannedMoveIndex, x.PlannedNextAiStateId)));
            Assert.Equal(
                audit.Combat.Enemies.Select(x => (x.Hp, x.Block)),
                controlled.Combat.Enemies.Select(x => (x.Hp, x.Block)));
        }
    }

    [Fact]
    public void SaveRestoreAndForkProduceIndependentLegalIntentBranches()
    {
        var before = Prepared("intent-branch-restore");
        var pending = ControlledIntentsV111.Begin(before);
        var save = ControlledIntentsV111.Save(pending);
        var restored = ControlledIntentsV111.Restore(save);
        Assert.Equal(pending.Fingerprint, restored.Fingerprint);
        Assert.Equal(pending.Pending!.SupportCount, restored.Pending!.SupportCount);
        Assert.True(restored.Pending.SupportCount > 1);

        var left = ControlledIntentsV111.Resolve(pending, pending.Fingerprint, 0);
        var right = ControlledIntentsV111.Resolve(restored,
            restored.Fingerprint, restored.Pending.SupportCount - 1);
        Assert.Equal(pending.Fingerprint,
            ControlledIntentsV111.Restore(save).Fingerprint);
        Assert.NotEqual(left.Fingerprint, right.Fingerprint);
        Assert.True(left.Complete);
        Assert.True(right.Complete);
        Assert.Null(left.Combat.NativeRng);
        Assert.Null(right.Combat.NativeRng);
        Assert.Equal(left.Fingerprint, ControlledIntentsV111.Restore(
            ControlledIntentsV111.Save(left)).Fingerprint);
    }

    [Fact]
    public void ControlledHpToIntentsUsesSameMechanicsAndPersistsAllPriorChoices()
    {
        for (int n = 0; n < 16; n++)
        {
            string seed = "composed-nature-entry-" + n;
            var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
                seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
                "ExoskeletonsNormal", 20, mapColumn: 3, mapRow: 5);
            var nativeHp = NativeCombatRngEntryV111.AttachAndRollInitialHpWithAudit(
                pre.Combat, pre.Formation, pre.NativeStreams);
            var hp = ControlledInitialHpV111.Begin(
                pre.Combat, pre.Formation, pre.NativeStreams);
            foreach (var choice in nativeHp.Choices)
                hp = ControlledInitialHpV111.Resolve(
                    hp, hp.Fingerprint, choice.SelectedIndex);
            Assert.True(hp.Complete);
            var initialHash = CanonicalJson.Sha256(hp.ReferenceStreams);
            var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
            Assert.NotNull(intents.PriorHpTranscript);
            Assert.Equal(nativeHp.Choices.Length, intents.PriorHpTranscript!.Length);
            var nativeIntent = NativeCombatRngEntryV111.CommitIntentsWithAudit(nativeHp.Combat);
            foreach (var choice in nativeIntent.Choices)
            {
                Assert.NotNull(intents.Pending);
                Assert.Equal(choice.Request.OwnerId, intents.Pending!.OwnerId);
                Assert.Equal(choice.Request.SupportCount, intents.Pending.SupportCount);
                intents = ControlledIntentsV111.Resolve(
                    intents, intents.Fingerprint, choice.SelectedIndex);
            }
            Assert.True(intents.Complete);
            Assert.Equal(initialHash,
                CanonicalJson.Sha256(intents.ReferenceStreams));
            Assert.Equal(
                nativeIntent.Combat.Enemies.Select(x => (x.Hp, x.PlannedMoveIndex, x.PlannedNextAiStateId)),
                intents.Combat.Enemies.Select(x => (x.Hp, x.PlannedMoveIndex, x.PlannedNextAiStateId)));
            var restored = ControlledIntentsV111.Restore(
                ControlledIntentsV111.Save(intents));
            Assert.Equal(intents.Fingerprint, restored.Fingerprint);

            var forged = intents.Fork();
            forged.PriorHpTranscript![0] = forged.PriorHpTranscript[0] with
            {
                SelectedIndex = forged.PriorHpTranscript[0].Request.SupportCount
            };
            Assert.Throws<ArgumentException>(() =>
                ControlledIntentsV111.Restore(JsonSerializer.Serialize(forged)));
        }
    }

    [Fact]
    public void IllegalStaleAndTamperedTicketsFailBeforeMutatingParent()
    {
        var pending = ControlledIntentsV111.Begin(Prepared("intent-fail-closed"));
        var fingerprint = pending.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            ControlledIntentsV111.Resolve(pending, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledIntentsV111.Resolve(pending, fingerprint, -1));
        Assert.Throws<ArgumentException>(() =>
            ControlledIntentsV111.Resolve(pending, fingerprint,
                pending.Pending!.SupportCount));
        Assert.Equal(fingerprint, pending.Fingerprint);

        var wrong = pending with
        {
            Pending = pending.Pending! with { OwnerId = "creature:fake" }
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledIntentsV111.Restore(JsonSerializer.Serialize(wrong)));
        var wrongNative = pending with
        {
            ReferenceStreams = NativeRngV111.CreateBundle("different-seed")
        };
        // This is a distinct snapshot: cursor authenticity cannot be
        // inferred from a valid counterfactual reference stream alone.
        Assert.NotEqual(fingerprint, wrongNative.Fingerprint);
    }

    [Fact]
    public void FullReplayRejectsChangedCommittedMoveAndForgedTicket()
    {
        var pending = ControlledIntentsV111.Begin(Prepared("intent-source-replay"));
        var first = ControlledIntentsV111.Resolve(pending,
            pending.Fingerprint, 0);
        Assert.True(first.Complete);
        var changed = first.Fork();
        changed.Combat.Enemies[3] = changed.Combat.Enemies[3] with
        {
            PlannedMoveIndex = 9999
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledIntentsV111.Restore(JsonSerializer.Serialize(changed)));

        var forged = first.Fork();
        forged.Transcript[0] = forged.Transcript[0] with
        {
            SelectedIndex = forged.Transcript[0].Request.SupportCount
        };
        Assert.Throws<ArgumentException>(() =>
            ControlledIntentsV111.Restore(JsonSerializer.Serialize(forged)));
    }
}
