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
}
