using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NatureBoundaryMetadataV111Tests
{
    private static ControlledInitialHpStateV111 Begin(string seed)
    {
        var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
            seed, 2, PrototypeNativeHiveNormals.MytesNormalId,
            "MytesNormal", 21, mapColumn: 3, mapRow: 5);
        return ControlledInitialHpV111.Begin(
            pre.Combat, pre.Formation, pre.NativeStreams);
    }

    [Fact]
    public void MetadataIdentifiesSourceChoiceAndProvidesUnverifiedLawOnly()
    {
        var hp = Begin("nature-143-metadata");
        var view = NatureBoundaryMetadataV111.Describe(hp);
        Assert.Equal(NatureBoundaryMetadataV111.SchemaId, view.SchemaId);
        Assert.Equal("initial_hp", view.Kind);
        Assert.False(view.Complete);
        Assert.NotNull(view.Pending);
        Assert.Equal("combat.initial_unique_hp", view.Pending!.SiteId);
        Assert.Equal("run.niche", view.Pending.StreamId);
        Assert.Equal("choose_index", view.Pending.OperationKind);
        Assert.Equal("v0.111.0", view.Pending.SourceVersion);
        Assert.Equal(ChanceLawV111.SourceVerifiedStatus,
            view.Pending.ProbabilityStatus);
        Assert.NotNull(view.Pending.ReferenceLawId);
        Assert.Equal(view.Pending.OrderedCandidates.Length, view.Pending.SupportCount);
        Assert.Empty(view.Transcript);

        var json = JsonSerializer.Serialize(view);
        Assert.DoesNotContain("nativeCursor", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("referenceStreams", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baselineCombat", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runSeed", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventIdsSurviveForkSaveRestoreAndRecordPrefixWithoutReroll()
    {
        var hp = Begin("nature-143-fork");
        var initial = NatureBoundaryMetadataV111.Describe(hp);
        var fork = ControlledInitialHpV111.Restore(
            ControlledInitialHpV111.Save(hp));
        Assert.Equal(initial.Pending!.EventId,
            NatureBoundaryMetadataV111.Describe(fork).Pending!.EventId);
        var next = ControlledInitialHpV111.Resolve(
            hp, hp.Fingerprint, 0);
        var nextView = NatureBoundaryMetadataV111.Describe(next);
        Assert.Single(nextView.Transcript);
        Assert.Equal(initial.Pending.EventId, nextView.Transcript[0].EventId);
        Assert.Equal(0, nextView.Transcript[0].SelectedIndex);
        Assert.Equal(ChanceLawV111.SourceVerifiedStatus,
            nextView.Transcript[0].ProbabilityStatus);
        Assert.NotEqual(initial.Pending.EventId, nextView.Pending!.EventId);
        Assert.Equal(nextView.Pending.EventId,
            NatureBoundaryMetadataV111.Describe(ControlledInitialHpV111.Restore(
                ControlledInitialHpV111.Save(next))).Pending!.EventId);

        // Event metadata is a detached copy; callers cannot mutate the
        // pending request or another independently forked handle.
        nextView.Pending.OrderedCandidates[0] = int.MinValue;
        Assert.NotEqual(int.MinValue,
            NatureBoundaryMetadataV111.Describe(next).Pending!.OrderedCandidates[0]);
    }

    [Fact]
    public void IntentsPreserveHpProvenanceInOneOrderedTrace()
    {
        var hp = Begin("nature-143-compose");
        while (!hp.Complete)
            hp = ControlledInitialHpV111.Resolve(hp, hp.Fingerprint, 0);
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        var entry = NatureBoundaryMetadataV111.Describe(intents);
        Assert.Equal("intents", entry.Kind);
        Assert.Equal(hp.Transcript.Length, entry.Transcript.Length);
        Assert.Equal("combat.initial_unique_hp", entry.Transcript[0].SiteId);
        if (!entry.Complete)
        {
            Assert.Equal("combat.committed_intent.index_draw", entry.Pending!.SiteId);
            var next = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, 0);
            var trace = NatureBoundaryMetadataV111.Describe(next);
            Assert.Equal(entry.Pending.EventId, trace.Transcript[^1].EventId);
            Assert.Equal(hp.Transcript.Length + 1, trace.Transcript.Length);
        }
    }

    [Fact]
    public void FifteenIndependentWorkersProduceScheduleIndependentEventIdentities()
    {
        static string Sequence(int worker)
        {
            var hp = Begin("nature-143-worker-" + worker);
            var ids = new List<string>();
            while (!hp.Complete)
            {
                var view = NatureBoundaryMetadataV111.Describe(hp);
                ids.Add(view.Pending!.EventId);
                hp = ControlledInitialHpV111.Resolve(hp, hp.Fingerprint,
                    worker % view.Pending.SupportCount);
            }
            var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
            while (!intents.Complete)
            {
                var view = NatureBoundaryMetadataV111.Describe(intents);
                ids.Add(view.Pending!.EventId);
                intents = ControlledIntentsV111.Resolve(intents, intents.Fingerprint,
                    worker % view.Pending.SupportCount);
            }
            return string.Join("|", ids);
        }

        var serial = Enumerable.Range(0, 15).Select(Sequence).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Sequence(i));
        Assert.Equal(serial, parallel);
        Assert.All(serial, record => Assert.NotEmpty(record));
    }
}
