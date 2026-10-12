using System.Text.Json;
using Sts2Emulator.Core;
using Sts2Emulator.Trace;
using Sts2ReferenceMod.Capture;
using Sts2ReferenceMod.Decision;

namespace Sts2Emulator.Core.Tests;

public sealed class InspectorContractTests
{
    private static GameAction EndTurn() => GameAction.Empty("end_turn");
    private static JsonElement Payload()
    {
        using var doc = JsonDocument.Parse("{\"damage\":6}");
        return doc.RootElement.Clone();
    }

    [Fact]
    public void IndependentJournalReadersDoNotDrainOneAnother()
    {
        var journal = new EventJournal();
        journal.Append(1, "damage", Payload(), "combat.damage");
        journal.Append(1, "hp_changed", Payload(), "combat.hp");
        var viewer = journal.ReadAfter(0);
        var writer = journal.ReadAfter(0);
        Assert.Equal(new long[] { 1, 2 }, viewer.Select(e => e.EventSeq));
        Assert.Equal(new long[] { 1, 2 }, writer.Select(e => e.EventSeq));
        Assert.Single(journal.ReadAfter(1));
    }

    [Fact]
    public void JournalCapacityFailureIsExplicit()
    {
        var journal = new EventJournal(1);
        journal.Append(1, "damage", Payload(), "hook");
        Assert.Throws<InvalidOperationException>(() =>
            journal.Append(1, "damage", Payload(), "hook"));
    }

    [Fact]
    public void StepPermitsExactlyOneActionAndDeduplicatesBoundary()
    {
        var c = new DecisionCoordinator();
        var boundary = c.ObserveDecision("turn1", "observation-a", new[] { EndTurn() });
        Assert.Equal(boundary, c.ObserveDecision("turn1", "observation-a", new[] { EndTurn() }));
        c.Step();
        var submitted = new ActionSubmission("request1", boundary.DecisionId,
            boundary.Revision, boundary.ObservationHash, EndTurn());
        Assert.True(c.Submit(submitted).Accepted);
        Assert.True(c.Submit(submitted).Accepted);
        Assert.True(c.TryTakeAuthorizedAction(out var action));
        Assert.Equal("end_turn", action!.Kind);
        Assert.False(c.TryTakeAuthorizedAction(out _));
        c.MarkResolving();
        c.Pause();
        c.MarkSettled();
        var second = c.ObserveDecision("turn2", "observation-b", new[] { EndTurn() });
        Assert.NotEqual(boundary.DecisionId, second.DecisionId);
        Assert.Equal(ControlMode.Paused, c.Snapshot().Mode);
        Assert.False(c.TryTakeAuthorizedAction(out _));
        Assert.Equal("STALE_OBSERVATION",
            c.Submit(new ActionSubmission("request2", boundary.DecisionId,
                boundary.Revision, boundary.ObservationHash, EndTurn())).Reason);
    }

    [Fact]
    public void RejectedActionCannotBeExecuted()
    {
        var c = new DecisionCoordinator();
        var b = c.ObserveDecision("turn", "hash", new[] { EndTurn() });
        c.Resume();
        Assert.Equal("ILLEGAL_ACTION", c.Submit(new ActionSubmission("bad", b.DecisionId,
            b.Revision, b.ObservationHash, GameAction.Empty("play_card"))).Reason);
        Assert.False(c.TryTakeAuthorizedAction(out _));
    }

    [Fact]
    public void ReusedHostKeysGetFreshCanonicalIdentities()
    {
        var ids = new EntityIdentityRegistry();
        var a = ids.Resolve("creature", "native-key");
        Assert.Equal(a, ids.Resolve("creature", "native-key"));
        ids.Retire("creature", "native-key");
        Assert.NotEqual(a, ids.Resolve("creature", "native-key"));
        ids.NewScenario();
        Assert.Equal(a, ids.Resolve("creature", "native-key"));
    }

    [Fact]
    public async Task PassiveRecordingWritesOrderedSyntheticTrace()
    {
        string path = Path.Combine(Path.GetTempPath(),
            "sts2-inspector-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var header = new NativeTraceHeader("header", "0.2", "SYNTHETIC", "test",
                null, "run", "0", DateTimeOffset.UtcNow, "passive", "unit-test");
            await using (var session = new Sts2ReferenceMod.Trace.NativeRecordingSession(path, header))
            {
                var adapter = new FakeAdapter("first", "{\"player\":{\"hp\":50}}");
                Assert.NotNull(session.OnStableDecision(adapter));
                Assert.NotNull(session.OnStableDecision(adapter));
                session.OnHumanAction(EndTurn());
                session.OnSemanticEffect("damage", Payload(), "test");
                adapter.Key = "second";
                adapter.State = "{\"player\":{\"hp\":49}}";
                Assert.NotNull(session.OnStableDecision(adapter));
                session.OnHumanAction(EndTurn());
                using var last = JsonDocument.Parse("{\"player\":{\"hp\":48}}");
                session.OnTerminal("SUCCESS", "synthetic fixture", last.RootElement);
            }
            var types = File.ReadAllLines(path).Select(line =>
            {
                using var doc = JsonDocument.Parse(line);
                return doc.RootElement.GetProperty("type").GetString();
            }).ToArray();
            Assert.Equal(new[] { "header", "decision", "action", "event", "transition",
                "decision", "action", "transition", "terminal" }, types);
            Assert.False(File.Exists(path + ".incomplete"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".incomplete")) File.Delete(path + ".incomplete");
        }
    }

    private sealed class FakeAdapter(string key, string state) : INativeCaptureAdapter
    {
        public string Key { get; set; } = key;
        public string State { get; set; } = state;
        public string Phase => "Combat";
        public bool IsAtStablePlayerChoice => true;
        public string SemanticBoundaryKey => Key;
        public string ObservationHash => Key;
        public JsonElement CaptureCanonicalState()
        {
            using var doc = JsonDocument.Parse(State);
            return doc.RootElement.Clone();
        }
        public IReadOnlyList<GameAction> GetCanonicalLegalActions() => new[] { EndTurn() };
        public CaptureCoverage GetCoverage() =>
            new(new[] { "$.player.hp" }, Array.Empty<string>(), Array.Empty<string>(),
                "RNG_UNAUDITED", new[] { "damage" });
    }

}
