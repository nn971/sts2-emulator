using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticDeckStateV111Tests
{
    private static (SemanticDeckStateV111 Deck, long Card) ResolveAndDraw(
        SemanticDeckStateV111 state, int index)
    {
        state = SemanticDeckEngineV111.Require(state, 0, "draw");
        if (state.Pending is not null)
            state = SemanticDeckEngineV111.Resolve(state, state.Fingerprint, index);
        var (remaining, card) = SemanticDeckEngineV111.DrawKnown(state);
        return (remaining, card);
    }

    [Fact]
    public void TrustedContentComparisonMatchesCanonicalShaAcrossStateChanges()
    {
        static void AssertSameAsSerialized(
            SemanticDeckStateV111 baseline, SemanticDeckStateV111 candidate)
        {
            Assert.Equal(
                baseline.Fingerprint == candidate.Fingerprint,
                baseline.HasSameSerializedContentsAs(candidate));
        }

        var baseDeck = SemanticDeckEngineV111.BeginNature([11L, 22L, 33L]);
        AssertSameAsSerialized(baseDeck, baseDeck);
        AssertSameAsSerialized(baseDeck, baseDeck.Fork());
        AssertSameAsSerialized(baseDeck,
            baseDeck with { ShuffleEpoch = baseDeck.ShuffleEpoch + 1 });
        AssertSameAsSerialized(baseDeck, baseDeck with
        {
            Slots = [SemanticDeckSlotV111.Known(11L), ..baseDeck.Slots[1..]]
        });
        AssertSameAsSerialized(baseDeck, baseDeck with
        {
            Pools = [new SemanticDeckPoolV111(
                "shuffle:0", [11L, 22L])]
        });
        AssertSameAsSerialized(baseDeck,
            baseDeck with { Transcript = null });

        var pending = SemanticDeckEngineV111.Require(baseDeck, 0, "draw");
        AssertSameAsSerialized(baseDeck, pending);
        AssertSameAsSerialized(pending, pending.Fork());
        AssertSameAsSerialized(pending, pending with
        {
            Pending = pending.Pending! with { Ordinal = 17 }
        });
        var selected = SemanticDeckEngineV111.Resolve(
            pending, pending.Fingerprint, 1);
        AssertSameAsSerialized(pending, selected);
        AssertSameAsSerialized(selected, selected.Fork());
        AssertSameAsSerialized(selected, selected with
        {
            Transcript = selected.Transcript!.Select(
                x => x with { SelectedIndex = x.SelectedIndex + 1 }).ToArray()
        });
    }

    [Fact]
    public void DeferredDrawsChoosePhysicalCardInstancesWithoutReplacement()
    {
        var state = SemanticDeckEngineV111.BeginNature([21, 22, 23]);
        Assert.Equal(SemanticDeckStateV111.NatureMode, state.Mode);
        Assert.Empty(SemanticDeckEngineV111.Observe(state).RevealedPositions);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticDeckEngineV111.DrawKnown(state));
        var pending = SemanticDeckEngineV111.Require(state, 0, "draw");
        var boundary = SemanticDeckEngineV111.Inspect(pending)!;
        Assert.Equal(SemanticDeckStateV111.SchemaId, boundary.SchemaId);
        Assert.Equal("combat.draw_pile.draw_card", boundary.SiteId);
        Assert.Equal("choose_card_instance", boundary.OperationKind);
        Assert.Equal("UNVERIFIED", boundary.ProbabilityStatus);
        Assert.Equal(new long[] { 21, 22, 23 }, boundary.CandidateCardInstanceIds);
        Assert.Equal(0, boundary.Ordinal);
        var resolved = SemanticDeckEngineV111.Resolve(
            pending, pending.Fingerprint, 1);
        Assert.Equal(22, resolved.Slots[0].KnownCardInstanceId);
        Assert.Equal(new long[] { 21, 23 },
            resolved.Pools.Single().RemainingCardInstanceIds);
        Assert.Empty(SemanticDeckEngineV111.Observe(resolved).RevealedPositions);
        var (afterDraw, card) = SemanticDeckEngineV111.DrawKnown(resolved);
        Assert.Equal(22, card);
        Assert.Equal(2, afterDraw.Count);
        var second = SemanticDeckEngineV111.Require(afterDraw, 0, "draw");
        Assert.Equal(new long[] { 21, 23 },
            SemanticDeckEngineV111.Inspect(second)!.CandidateCardInstanceIds);
        Assert.Equal(1, second.Pending!.Ordinal);
        Assert.Equal(boundary.EventId, resolved.Transcript![0].EventId);
        Assert.Null(SemanticDeckEngineV111.Inspect(
            SemanticDeckEngineV111.Resolve(second, second.Fingerprint, 0)));
    }

    [Fact]
    public void MidPileInsertionPreservesOneSharedUnresolvedPoolAcrossBothSides()
    {
        var baseline = SemanticDeckEngineV111.BeginNature([11, 12, 13, 14]);
        var state = SemanticDeckEngineV111.InsertKnown(baseline, 2, 99);
        Assert.Equal(5, state.Count);
        Assert.Equal(99, state.Slots[2].KnownCardInstanceId);
        Assert.Equal(state.Slots[0].PoolId, state.Slots[1].PoolId);
        Assert.Equal(state.Slots[1].PoolId, state.Slots[3].PoolId);
        Assert.Equal(state.Slots[3].PoolId, state.Slots[4].PoolId);
        Assert.Equal(4, state.Pools.Single().RemainingCardInstanceIds.Length);
        (state, var a) = ResolveAndDraw(state, 3);
        (state, var b) = ResolveAndDraw(state, 0);
        Assert.Equal(new long[] { 14, 11 }, new[] { a, b });
        var third = SemanticDeckEngineV111.Require(state, 0, "draw");
        Assert.Null(third.Pending);
        (state, var forced) = SemanticDeckEngineV111.DrawKnown(third);
        Assert.Equal(99, forced);
        var pending = SemanticDeckEngineV111.Require(state, 0, "draw");
        Assert.Equal(new long[] { 12, 13 },
            pending.Pending!.CandidateCardInstanceIds);
        Assert.Equal(2, pending.Pending.Ordinal);
    }

    [Fact]
    public void KnownTopAndBottomAndPeeksNeverBecomeSecondNatureChoices()
    {
        var state = SemanticDeckEngineV111.BeginNature([4, 5, 6]);
        state = SemanticDeckEngineV111.PlaceTop(state, 90);
        state = SemanticDeckEngineV111.PlaceBottom(state, 91);
        Assert.Equal(new long[] { 90, 91 },
            SemanticDeckEngineV111.Observe(state).RevealedPositions
                .Select(x => x.CardInstanceId));
        var top = SemanticDeckEngineV111.Require(state, 0, "draw");
        Assert.Null(top.Pending);
        (state, var card) = SemanticDeckEngineV111.DrawKnown(top);
        Assert.Equal(90, card);
        var peek = SemanticDeckEngineV111.Require(state, 1, "peek");
        Assert.Equal("combat.draw_pile.reveal_card", peek.Pending!.SiteId);
        peek = SemanticDeckEngineV111.Resolve(peek, peek.Fingerprint, 1);
        Assert.Equal(5, peek.Slots[1].KnownCardInstanceId);
        Assert.Equal(5,
            SemanticDeckEngineV111.Observe(peek).RevealedPositions[0].CardInstanceId);
        var same = SemanticDeckEngineV111.Require(peek, 1, "scry");
        Assert.Same(peek, same);
        Assert.Single(same.Transcript!);
        var next = SemanticDeckEngineV111.Require(same, 0, "draw");
        next = SemanticDeckEngineV111.Resolve(next, next.Fingerprint, 0);
        (next, var drawn) = SemanticDeckEngineV111.DrawKnown(next);
        Assert.Equal(4, drawn);
        var revealedTop = SemanticDeckEngineV111.Require(next, 0, "draw");
        Assert.Null(revealedTop.Pending);
        (next, drawn) = SemanticDeckEngineV111.DrawKnown(revealedTop);
        Assert.Equal(5, drawn);
        Assert.Equal(new long[] { 6, 91 },
            new [] { ResolveAndDraw(next, 0).Card, 91L });
    }

    [Fact]
    public void ScryDiscardAndMovingKnownCardsPreserveInstanceIdentity()
    {
        var state = SemanticDeckEngineV111.BeginNature([1, 2, 3]);
        state = SemanticDeckEngineV111.Require(state, 0, "scry");
        state = SemanticDeckEngineV111.Resolve(state, state.Fingerprint, 1);
        Assert.Equal(2, state.Slots[0].KnownCardInstanceId);
        state = SemanticDeckEngineV111.RemoveKnown(state, 2);
        Assert.Equal(2, state.Count);
        Assert.Equal(new long[] { 1, 3 },
            state.Pools.Single().RemainingCardInstanceIds);
        state = SemanticDeckEngineV111.InsertKnown(state, 1, 15);
        state = SemanticDeckEngineV111.MoveKnownToTop(state, 15);
        Assert.Equal(15, state.Slots[0].KnownCardInstanceId);
        Assert.Equal(15, SemanticDeckEngineV111.DrawKnown(state).CardInstanceId);
        var prior = state.Fingerprint;
        Assert.Throws<NotSupportedException>(() =>
            SemanticDeckEngineV111.RemoveKnown(state, 1));
        Assert.Equal(prior, state.Fingerprint);
        Assert.Throws<NotSupportedException>(() =>
            SemanticDeckEngineV111.MoveKnownToTop(state, 3));
    }

    [Fact]
    public void NativeModeKeepsCommittedExactOrderAndNeverEmitsNatureTickets()
    {
        var state = SemanticDeckEngineV111.BeginNative([6, 4, 8]);
        Assert.Equal(SemanticDeckStateV111.NativeMode, state.Mode);
        Assert.Empty(SemanticDeckEngineV111.Observe(state).RevealedPositions);
        state = SemanticDeckEngineV111.Require(state, 1, "peek");
        Assert.Null(state.Pending);
        Assert.Empty(state.Transcript!);
        Assert.Equal(4, SemanticDeckEngineV111.Observe(state)
            .RevealedPositions.Single().CardInstanceId);
        Assert.Equal(6, SemanticDeckEngineV111.DrawKnown(state).CardInstanceId);
        state = SemanticDeckEngineV111.CommitNativeShuffle(state, [8, 4, 6]);
        Assert.Equal(new long[] { 8, 4, 6 },
            state.Slots.Select(x => x.KnownCardInstanceId!.Value));
        Assert.Empty(SemanticDeckEngineV111.Observe(state).RevealedPositions);
        Assert.Null(SemanticDeckEngineV111.Inspect(state));
        Assert.Throws<ArgumentException>(() =>
            SemanticDeckEngineV111.CommitNativeShuffle(state, [8, 4, 7]));
        Assert.Throws<InvalidOperationException>(() =>
            SemanticDeckEngineV111.ReshuffleNature(state));
    }

    [Fact]
    public void SourceShuffleResetsPositionsAndPeekConstraintsAtNextEpoch()
    {
        var state = SemanticDeckEngineV111.BeginNature([1, 2, 3]);
        state = SemanticDeckEngineV111.Require(state, 0, "peek");
        state = SemanticDeckEngineV111.Resolve(state, state.Fingerprint, 2);
        state = SemanticDeckEngineV111.PlaceTop(state, 9);
        Assert.Equal(new long[] { 9, 3 },
            SemanticDeckEngineV111.Observe(state).RevealedPositions
                .Select(x => x.CardInstanceId));
        var epoch = SemanticDeckEngineV111.ReshuffleNature(state);
        Assert.Equal(1, epoch.ShuffleEpoch);
        Assert.Equal(4, epoch.Count);
        Assert.Empty(SemanticDeckEngineV111.Observe(epoch).RevealedPositions);
        Assert.Equal(new long[] { 9, 3, 1, 2 },
            epoch.Pools.Single().RemainingCardInstanceIds);
        Assert.Single(epoch.Transcript!);
        var next = SemanticDeckEngineV111.Require(epoch, 0, "draw");
        Assert.Equal(4, next.Pending!.CandidateCardInstanceIds.Length);
        Assert.NotEqual(state.Fingerprint, epoch.Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticDeckEngineV111.ReshuffleNature(next));
    }

    [Fact]
    public void SnapshotForkAndStaleOutcomesRejectTampering()
    {
        var original = SemanticDeckEngineV111.BeginNature([1, 2, 3, 4]);
        var pending = SemanticDeckEngineV111.Require(
            SemanticDeckEngineV111.InsertKnown(original, 2, 20), 0, "draw");
        var stable = pending.Fingerprint;
        var eventId = SemanticDeckEngineV111.Inspect(pending)!.EventId;
        var restored = SemanticDeckEngineV111.Restore(
            SemanticDeckEngineV111.Save(pending));
        Assert.Equal(stable, restored.Fingerprint);
        Assert.Equal(eventId, SemanticDeckEngineV111.Inspect(restored)!.EventId);
        var left = SemanticDeckEngineV111.Resolve(pending, stable, 0);
        var right = SemanticDeckEngineV111.Resolve(pending, stable, 3);
        Assert.NotEqual(left.Fingerprint, right.Fingerprint);
        Assert.Equal(stable, pending.Fingerprint);
        Assert.Throws<InvalidOperationException>(() =>
            SemanticDeckEngineV111.Resolve(pending, "stale", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SemanticDeckEngineV111.Resolve(pending, stable, 4));
        var forged = pending.Fork();
        forged.Pending!.CandidateCardInstanceIds[0] = 999;
        Assert.Throws<InvalidDataException>(() =>
            SemanticDeckEngineV111.Validate(forged));
        var mutated = pending.Fork();
        mutated.Pools[0].RemainingCardInstanceIds[0] = 777;
        Assert.Throws<InvalidDataException>(() =>
            SemanticDeckEngineV111.Validate(mutated));
        Assert.Equal(stable, pending.Fingerprint);
        Assert.Throws<ArgumentException>(() =>
            SemanticDeckEngineV111.PlaceTop(original, 2));
        Assert.Throws<ArgumentException>(() =>
            SemanticDeckEngineV111.BeginNature([1, 1]));
    }

    [Fact]
    public void SmallDeckEnumerationMatchesAllOnceShuffledPermutations()
    {
        var root = SemanticDeckEngineV111.InsertKnown(
            SemanticDeckEngineV111.BeginNature([1, 2, 3]), 1, 9);
        var paths = new Dictionary<string, double>(StringComparer.Ordinal);
        void Visit(SemanticDeckStateV111 state, List<long> order, double probability)
        {
            if (state.Count == 0)
            {
                var key = string.Join(",", order);
                paths[key] = paths.GetValueOrDefault(key) + probability;
                return;
            }
            var required = SemanticDeckEngineV111.Require(state, 0, "draw");
            if (required.Pending is null)
            {
                var (next, card) = SemanticDeckEngineV111.DrawKnown(required);
                Visit(next, order.Append(card).ToList(), probability);
                return;
            }
            var support = required.Pending.CandidateCardInstanceIds.Length;
            for (int i = 0; i < support; i++)
            {
                var child = SemanticDeckEngineV111.Resolve(
                    required, required.Fingerprint, i);
                var (next, card) = SemanticDeckEngineV111.DrawKnown(child);
                Visit(next, order.Append(card).ToList(), probability / support);
            }
        }
        Visit(root, [], 1);
        Assert.Equal(6, paths.Count);
        foreach (var (key, probability) in paths)
        {
            var ids = key.Split(',').Select(long.Parse).ToArray();
            Assert.Equal(9, ids[1]);
            Assert.Equal(new long[] { 1, 2, 3 },
                ids.Where(id => id != 9).Order());
            Assert.Equal(1.0 / 6.0, probability, 12);
        }
        Assert.Equal(1.0, paths.Values.Sum(), 12);
    }


    [Fact]
    public void SourceRecycleMakesNewEpochWithPerfectFitPriorityGroups()
    {
        var state = SemanticDeckEngineV111.BeginNature([1, 2]);
        (state, _) = SemanticDeckEngineV111.DrawKnown(
            SemanticDeckEngineV111.Resolve(
                SemanticDeckEngineV111.Require(state, 0, "draw"),
                SemanticDeckEngineV111.Require(state, 0, "draw").Fingerprint, 0));
        (state, _) = SemanticDeckEngineV111.DrawKnown(
            SemanticDeckEngineV111.Resolve(
                SemanticDeckEngineV111.Require(state, 0, "draw"),
                SemanticDeckEngineV111.Require(state, 0, "draw").Fingerprint, 0));
        Assert.Equal(0, state.Count);
        var recycled = SemanticDeckEngineV111.RecycleNature(
            state, [2L, 1L, 7L, 8L], [1L, 7L]);
        Assert.Equal(1, recycled.ShuffleEpoch);
        Assert.Equal(4, recycled.Count);
        Assert.Equal(2, recycled.Pools.Length);
        Assert.Equal(recycled.Slots[0].PoolId, recycled.Slots[1].PoolId);
        Assert.NotEqual(recycled.Slots[1].PoolId, recycled.Slots[2].PoolId);
        Assert.Equal(new long[] { 1L, 7L },
            SemanticDeckEngineV111.Require(recycled, 0, "draw")
                .Pending!.CandidateCardInstanceIds);
        Assert.Equal(2, recycled.Transcript!.Length);
        Assert.Equal(recycled.Fingerprint,
            SemanticDeckEngineV111.Restore(
                SemanticDeckEngineV111.Save(recycled)).Fingerprint);
    }

    [Fact]
    public void RecycleRejectsWrongMembershipAndPendingOrNonemptyPileAtomically()
    {
        var original = SemanticDeckEngineV111.BeginNature([1, 2]);
        var fingerprint = original.Fingerprint;
        Assert.Throws<InvalidOperationException>(() =>
            SemanticDeckEngineV111.RecycleNature(original, [3L]));
        Assert.Throws<ArgumentException>(() =>
            SemanticDeckEngineV111.RecycleNature(
                SemanticDeckEngineV111.BeginNature([]), [4L, 5L], [6L]));
        Assert.Throws<ArgumentException>(() =>
            SemanticDeckEngineV111.RecycleNature(
                SemanticDeckEngineV111.BeginNature([]), [4L, 4L]));
        var pending = SemanticDeckEngineV111.Require(original, 0, "draw");
        Assert.Throws<InvalidOperationException>(() =>
            SemanticDeckEngineV111.RecycleNature(pending, [3L]));
        Assert.Equal(fingerprint, original.Fingerprint);
    }

    [Fact]
    public void FifteenWorkersUseIsolatedStateAndStableSemanticEventIds()
    {
        static string Run(int i)
        {
            var state = SemanticDeckEngineV111.PlaceTop(
                SemanticDeckEngineV111.InsertKnown(
                    SemanticDeckEngineV111.BeginNature([1, 2, 3, 4, 5]),
                    2, 99), 98);
            var events = new List<string>();
            while (state.Count > 0)
            {
                state = SemanticDeckEngineV111.Require(state, 0, "draw");
                if (state.Pending is not null)
                {
                    events.Add(SemanticDeckEngineV111.Inspect(state)!.EventId);
                    state = SemanticDeckEngineV111.Resolve(
                        state, state.Fingerprint,
                        i % state.Pending.CandidateCardInstanceIds.Length);
                }
                (state, _) = SemanticDeckEngineV111.DrawKnown(state);
            }
            return state.Fingerprint + ":" + string.Join("|", events);
        }
        var serial = Enumerable.Range(0, 15).Select(Run).ToArray();
        var parallel = new string[15];
        Parallel.For(0, 15, i => parallel[i] = Run(i));
        Assert.Equal(serial, parallel);
    }
}
