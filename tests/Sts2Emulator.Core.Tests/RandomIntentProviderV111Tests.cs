using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class RandomIntentProviderV111Tests
{
    private static NativeRngSnapshot Cursor(CombatState combat, string stream) =>
        NativeRngV111.ToSnapshot(combat.NativeRng!.RunStreams.Streams.Single(
            item => item.StreamId == stream));

    [Theory]
    [InlineData(2, PrototypeNativeHiveNormals.ExoskeletonsNormalId, "ExoskeletonsNormal")]
    [InlineData(2, PrototypeNativeHiveNormals.MytesNormalId, "MytesNormal")]
    [InlineData(3, PrototypeNativeGloryNormals.FrogKnightEncounterId, "FrogKnightNormal")]
    public void IntentSeamReplaysExactOriginalNativeChoicesAndCursors(
        int act, string encounterId, string entry)
    {
        for (int i = 0; i < 32; i++)
        {
            string seed = $"desired-intent-{entry}-{i}";
            var before = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
                seed, act, encounterId, entry, 20 + i, mapColumn: 3,
                mapRow: 5, commitInitialIntents: false);
            string stateHash = CanonicalJson.Sha256(before);
            var expected = NativeCombatRngEntryV111.CommitIntents(before);
            var audited = NativeCombatRngEntryV111.CommitIntentsWithAudit(before);

            // This checks *all* engine state, including RNG words, counters,
            // enemy committed move and state-machine continuation.
            Assert.Equal(CanonicalJson.Sha256(expected),
                CanonicalJson.Sha256(audited.Combat));
            Assert.Equal(stateHash, CanonicalJson.Sha256(before));
            Assert.Equal("native-v111", audited.ProviderMode);
            Assert.Equal(
                Cursor(audited.Combat, "run.monster_ai").Counter -
                    Cursor(before, "run.monster_ai").Counter,
                audited.Choices.Length);
            Assert.Equal(Cursor(before, "run.niche"),
                Cursor(audited.Combat, "run.niche"));

            for (int ordinal = 0; ordinal < audited.Choices.Length; ordinal++)
            {
                var choice = audited.Choices[ordinal];
                Assert.Equal("combat.committed_intent.index_draw",
                    choice.Request.SiteId);
                Assert.Equal("run.monster_ai", choice.Request.StreamId);
                Assert.Equal(ordinal, choice.Request.Ordinal);
                Assert.Contains(choice.Request.OwnerId,
                    before.Enemies.Select(enemy => "creature:" + enemy.InstanceId));
                Assert.Equal(Enumerable.Range(0, choice.Request.OrderedCandidates.Length),
                    choice.Request.OrderedCandidates);
                Assert.Equal(ordinal, choice.NativeCursorBefore!.Counter);
                Assert.Equal(ordinal + 1, choice.NativeCursorAfter!.Counter);
                Assert.Equal("UNVERIFIED", choice.ProbabilityStatus);
            }

            var streams = NativeRngV111.CreateBundle("intent-counterfactual");
            string controlHash = CanonicalJson.Sha256(streams);
            var scripted = new ScriptedRandomIndexProviderV111(audited.Choices);
            foreach (var entryChoice in audited.Choices)
            {
                var replay = scripted.Resolve(entryChoice.Request, streams);
                Assert.Equal(entryChoice.SelectedIndex, replay.SelectedIndex);
                Assert.Equal(entryChoice.SelectedValue, replay.SelectedValue);
            }
            scripted.AssertComplete();
            Assert.Equal(controlHash, CanonicalJson.Sha256(streams));
        }
    }

    [Fact]
    public void ReplayedIntentRejectsChangedEligibilityOrOwnerAndKeepsCursor()
    {
        var before = PrototypeNativeLaterActFixture.CreateCombatRngSnapshot(
            "intent-eligibility-drift", 2,
            PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, mapColumn: 3, mapRow: 5,
            commitInitialIntents: false);
        var audit = NativeCombatRngEntryV111.CommitIntentsWithAudit(before);
        var choice = Assert.Single(audit.Choices);
        var stream = NativeRngV111.CreateBundle("not-consumed");
        string original = CanonicalJson.Sha256(stream);
        var replay = new ScriptedRandomIndexProviderV111(audit.Choices);
        var req = choice.Request;
        Assert.Throws<InvalidOperationException>(() =>
            replay.Resolve(req with { OwnerId = "creature:other" }, stream));
        Assert.Throws<InvalidOperationException>(() =>
            replay.Resolve(req with { Ordinal = 1 }, stream));
        Assert.Throws<InvalidOperationException>(() =>
            replay.Resolve(req with { OrderedCandidates = [999] }, stream));
        Assert.Equal(1, replay.Remaining);
        Assert.Equal(original, CanonicalJson.Sha256(stream));
        var accepted = replay.Resolve(req, stream);
        Assert.Equal(choice.SelectedIndex, accepted.SelectedIndex);
        replay.AssertComplete();
    }
}
