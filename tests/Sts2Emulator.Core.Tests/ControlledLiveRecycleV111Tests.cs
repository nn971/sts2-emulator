using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ControlledLiveRecycleV111Tests
{
    private static ControlledPlayableCombatV111 Prepared(string seed)
    {
        var pre = PrototypeNativeLaterActFixture.CreateCombatRngPreEntry(
            seed, 2, PrototypeNativeHiveNormals.ExoskeletonsNormalId,
            "ExoskeletonsNormal", 20, mapColumn: 3, mapRow: 5);
        var hp = ControlledInitialHpV111.Begin(
            pre.Combat, pre.Formation, pre.NativeStreams);
        while (!hp.Complete)
            hp = ControlledInitialHpV111.Resolve(hp, hp.Fingerprint, 0);
        var intents = ControlledIntentsV111.BeginAfterControlledHp(hp);
        while (!intents.Complete)
            intents = ControlledIntentsV111.Resolve(
                intents, intents.Fingerprint, 0);
        var playable = ControlledPlayableCombatEngineV111.Begin(intents, seed);
        while (!playable.AtPlayerBoundary)
            playable = ControlledPlayableCombatEngineV111.Resolve(
                playable, playable.Fingerprint, 0);
        return playable;
    }

    private static ControlledPlayableCombatV111 ToRecycle(string seed)
    {
        var state = Prepared(seed);
        for (int turn = 0; turn < 5; turn++)
        {
            if (state.Recycle is not null) return state;
            Assert.True(state.AtPlayerBoundary);
            Assert.Equal(RunPhase.Combat, state.Run.Phase);
            var end = new PrototypeAiEnvironment().Observe(state.Run)
                .LegalActions.Single(action => action.Kind == "end_turn");
            state = ControlledPlayableCombatEngineV111.Step(state, end.ActionId);
            while (state.TurnIntents?.Pending is not null)
                state = ControlledPlayableCombatEngineV111.Resolve(
                    state, state.Fingerprint, 0);
            if (state.Recycle is not null) return state;
        }
        throw new InvalidOperationException(
            "Fixture failed to reach a live draw-pile recycle after end-turn.");
    }

    [Fact]
    public void LiveRecycleSuspendsInsideHandDrawAndResumesWithoutRedoingHooks()
    {
        var pending = ToRecycle("nature-live-recycle");
        Assert.NotNull(pending.Recycle);
        Assert.NotNull(pending.ActivePending);
        Assert.Equal(ControlledRecycleShuffleEngineV111.SiteId,
            pending.ActivePending!.SiteId);
        Assert.Equal("run.shuffle", pending.ActivePending.StreamId);
        Assert.Equal("bounded_integer", pending.ActivePending.OperationKind);
        Assert.False(pending.AtPlayerBoundary);
        Assert.Empty(pending.Run.World!.Combat!.DrawPile);
        Assert.Equal(pending.Run.World.Combat.DiscardPile,
            pending.Recycle!.Baseline);
        var priorHp = pending.Run.Player.Hp;
        var priorDecisionIndex = pending.Run.DecisionIndex;
        var priorTurn = pending.Run.World.Combat.Turn;
        var originalHash = pending.Fingerprint;
        var eventId = NatureBoundaryMetadataV111.Describe(pending).Pending!.EventId;

        Assert.Throws<InvalidOperationException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(pending, "stale", 0));
        Assert.Throws<ArgumentException>(() =>
            ControlledPlayableCombatEngineV111.Resolve(
                pending, originalHash, pending.ActivePending.SupportCount));
        Assert.Equal(originalHash, pending.Fingerprint);

        var restored = ControlledPlayableCombatEngineV111.Restore(
            ControlledPlayableCombatEngineV111.Save(pending));
        Assert.Equal(originalHash, restored.Fingerprint);
        Assert.Equal(eventId,
            NatureBoundaryMetadataV111.Describe(restored).Pending!.EventId);
        var next = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, 0);
        Assert.Equal(originalHash, pending.Fingerprint);
        Assert.Equal(eventId,
            NatureBoundaryMetadataV111.Describe(next).Transcript[^1].EventId);
        while (!next.AtPlayerBoundary)
        {
            Assert.NotNull(next.ActivePending);
            next = ControlledPlayableCombatEngineV111.Resolve(
                next, next.Fingerprint, 0);
        }
        Assert.Equal(priorHp, next.Run.Player.Hp);
        Assert.Equal(priorDecisionIndex, next.Run.DecisionIndex);
        Assert.Equal(priorTurn, next.Run.World!.Combat!.Turn);
        Assert.True(next.Run.World.Combat.Hand.Length > 0);
        Assert.NotEmpty(new PrototypeAiEnvironment().Observe(next.Run).LegalActions);
        Assert.Equal(next.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(next)).Fingerprint);
        Assert.Equal(originalHash, pending.Fingerprint);
    }

    [Fact]
    public void RecycleTicketsAreStrictAndForkedChoicesRemainIndependent()
    {
        var pending = ToRecycle("nature-recycle-forks");
        var view = NatureBoundaryMetadataV111.Describe(pending);
        Assert.Equal("draw_recycle", view.Kind);
        Assert.Equal(ChanceLawV111.SourceVerifiedStatus,
            view.Pending!.ProbabilityStatus);
        Assert.True(view.Pending.SupportCount > 1);
        var left = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, 0);
        var right = ControlledPlayableCombatEngineV111.Resolve(
            pending, pending.Fingerprint, view.Pending.SupportCount - 1);
        Assert.NotEqual(left.Fingerprint, right.Fingerprint);
        Assert.Equal(left.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(left)).Fingerprint);
        Assert.Equal(right.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(right)).Fingerprint);
        var tampered = pending.Fork();
        var recycle = tampered.Recycle!;
        var shuffled = recycle.Shuffled;
        shuffled[0] = -1234;
        tampered = tampered with
        {
            Recycle = recycle with { Shuffled = shuffled }
        };
        Assert.Throws<InvalidDataException>(() =>
            ControlledPlayableCombatEngineV111.Restore(
                JsonSerializer.Serialize(tampered)));
        Assert.Equal(pending.Fingerprint,
            ControlledPlayableCombatEngineV111.Restore(
                ControlledPlayableCombatEngineV111.Save(pending)).Fingerprint);
    }

    [Fact]
    public void FifteenWorkersHaveScheduleIndependentLiveRecycleEvents()
    {
        static string Rollout(int worker)
        {
            var s = ToRecycle("nature-recycle-15-workers-" + worker);
            var ids = new List<string>();
            while (!s.AtPlayerBoundary)
            {
                var view = NatureBoundaryMetadataV111.Describe(s);
                ids.Add(view.Pending!.EventId);
                s = ControlledPlayableCombatEngineV111.Resolve(
                    s, s.Fingerprint, worker % view.Pending.SupportCount);
            }
            return s.Fingerprint + ":" + string.Join("|", ids);
        }

        var serial = Enumerable.Range(0, 15).Select(Rollout).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, worker => parallel[worker] = Rollout(worker));
        Assert.Equal(serial, parallel);
    }

    [Fact]
    public void PureRecycleRejectsForgedOrOutOfRangeTickets()
    {
        var original = ControlledRecycleShuffleEngineV111.Begin([1L, 2L, 3L], 7);
        Assert.Equal(7, original.Pending!.Ordinal);
        Assert.Equal(3, original.Pending.SupportCount);
        Assert.Throws<ArgumentException>(() =>
            ControlledRecycleShuffleEngineV111.Resolve(
                original, 3, NativeRngV111.CreateBundle("unused")));
        var one = ControlledRecycleShuffleEngineV111.Resolve(
            original, 1, NativeRngV111.CreateBundle("unused"));
        Assert.Equal(8, one.Pending!.Ordinal);
        Assert.Equal(2, one.Pending.SupportCount);
        var two = ControlledRecycleShuffleEngineV111.Resolve(
            one, 0, NativeRngV111.CreateBundle("unused"));
        Assert.True(two.Complete);
        Assert.Equal(new long[] { 3, 1, 2 }, two.Shuffled);
        var modified = two.Fork();
        var modifiedTranscript = modified.Transcript;
        modifiedTranscript[0] = modifiedTranscript[0] with
        { SelectedIndex = 0 };
        modified = modified with { Transcript = modifiedTranscript };
        Assert.Throws<InvalidDataException>(() =>
            ControlledRecycleShuffleEngineV111.Validate(modified));
    }
}
