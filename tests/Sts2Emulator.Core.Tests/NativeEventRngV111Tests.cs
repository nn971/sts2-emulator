using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeEventRngV111Tests
{
    [Theory]
    [InlineData("NEOW", 0, false)]
    [InlineData("TRIAL", 2, false)]
    [InlineData("TRIAL", 2, true)]
    public void EventOwnerUsesContextFactoryAndRestoresContinuation(
        string entry, int slot, bool shared)
    {
        const string seed = "native-event-owner-v111";
        var expected = NativeRngV111Contexts.ForEvent(seed, entry, slot, shared);
        var local = NativeEventRngV111.Begin(seed, entry, slot, shared);
        var streamId = Assert.Single(local.Streams).StreamId;
        Assert.Equal(expected.StreamId, streamId);
        Assert.Equal(NativeRngV111.ToSnapshot(expected),
            NativeRngV111.ToSnapshot(local.Streams[0]));

        var persistent = NativeRngV111.CreateBundle(seed);
        var persistentBefore = persistent.Streams.Select(
            NativeRngV111.ToSnapshot).ToArray();
        for (int i = 0; i < 17; i++)
            _ = NativeRngV111.NextUInt64(local, streamId);
        var snapshot = NativeRngV111.ToSnapshot(local.Streams[0]);
        var restored = NativeEventRngV111.Restore(seed, entry, snapshot, slot, shared);
        Assert.Equal(streamId, Assert.Single(restored.Streams).StreamId);
        Assert.Equal(snapshot, NativeRngV111.ToSnapshot(restored.Streams[0]));
        Assert.Equal(NativeRngV111.NextUInt64(local, streamId),
            NativeRngV111.NextUInt64(restored, streamId));
        Assert.Equal(persistentBefore, persistent.Streams.Select(
            NativeRngV111.ToSnapshot).ToArray());
    }

    [Fact]
    public void SharedEventIgnoresPlayerSlotAndDifferentEventsHaveDistinctState()
    {
        const string seed = "event-owner-scope";
        var shared0 = NativeEventRngV111.Begin(seed, "TRIAL", shared: true);
        var shared3 = NativeEventRngV111.Begin(seed, "TRIAL", 3, shared: true);
        var other = NativeEventRngV111.Begin(seed, "OTHER_EVENT");
        Assert.Equal(NativeRngV111.ToSnapshot(shared0.Streams[0]),
            NativeRngV111.ToSnapshot(shared3.Streams[0]));
        Assert.NotEqual(NativeRngV111.ToSnapshot(shared0.Streams[0]),
            NativeRngV111.ToSnapshot(other.Streams[0]));
        Assert.Equal(shared0.Streams[0].StreamId, shared3.Streams[0].StreamId);
        // Shared event RNG is keyed to its event identity, not a player.
        var nonshared3 = NativeEventRngV111.Begin(seed, "TRIAL", 3);
        Assert.NotEqual(NativeRngV111.ToSnapshot(shared0.Streams[0]),
            NativeRngV111.ToSnapshot(nonshared3.Streams[0]));
        Assert.NotEqual(shared0.Streams[0].StreamId,
            nonshared3.Streams[0].StreamId);
    }

    [Fact]
    public void EventLocalRngSurvivesNestedChoicesAndReleasesWhenEventExits()
    {
        var root = V111RunFactory.Create("event-owner-lifecycle",
            RunConfiguration.Create("silent", 10, ActIdentity.Overgrowth) with
            {
                FidelityId = V111Build.NativeNeowOpeningFidelityId
            });
        var engine = new PrototypeGameEngine();
        var active = engine.Step(root, engine.GetLegalActions(root).Single()).State;
        Assert.NotNull(active.NativeEventRng);
        Assert.Same(active, NativeEventRngV111.ReconcileAfterStep(root, active));

        // A nested choice still belongs to the same native event instance.
        var nested = active with { DecisionIndex = active.DecisionIndex + 1 };
        Assert.Same(nested, NativeEventRngV111.ReconcileAfterStep(active, nested));
        Assert.NotSame(active.NativeEventRng, active.Fork().NativeEventRng);

        // Event replacement also closes the previous owner's local stream.
        var replaced = active with
        {
            World = active.World! with
            {
                Event = new EventState("proto.native.event.other",
                    OfferedChoiceIds: [])
            }
        };
        Assert.Null(NativeEventRngV111.ReconcileAfterStep(
            active, replaced).NativeEventRng);

        var map = active with
        {
            Phase = RunPhase.MapChoice,
            World = active.World! with { Event = null }
        };
        Assert.Null(NativeEventRngV111.ReconcileAfterStep(active, map).NativeEventRng);
        Assert.NotNull(active.NativeEventRng);
    }

    [Fact]
    public void RestoreRejectsInvalidSourceSnapshot()
    {
        Assert.Throws<InvalidDataException>(() =>
            NativeEventRngV111.Restore("seed", "TRIAL",
                new NativeRngSnapshot(-1, 1, 2, 3, 4)));
        Assert.Throws<InvalidDataException>(() =>
            NativeEventRngV111.Restore("seed", "TRIAL",
                new NativeRngSnapshot(0, 0, 0, 0, 0)));
        Assert.Throws<ArgumentException>(() =>
            NativeEventRngV111.Begin("seed", ""));
    }
}
