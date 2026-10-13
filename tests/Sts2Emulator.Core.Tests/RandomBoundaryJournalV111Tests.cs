using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class RandomBoundaryJournalV111Tests
{
    private static RandomIndexRequestV111 Request() =>
        new("combat.initial_unique_hp", "run.niche", "creature:1",
            0, [44, 45, 46]);

    [Fact]
    public void NativeVsScriptedSameLegalOutcomeHasMatchingBoundaryAndNoScriptedCursorAdvance()
    {
        var nativeStreams = NativeRngV111.CreateBundle("random-journal-comparison");
        var scriptedStreams = NativeRngV111.CreateBundle("random-journal-comparison");
        RandomBoundaryEventV111[] nativeEvents;
        RandomIndexDecisionV111 decision;
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            decision = new NativeRandomIndexProviderV111()
                .Resolve(Request(), nativeStreams);
            nativeEvents = journal.Entries.ToArray();
        }
        Assert.Single(nativeEvents);
        Assert.NotNull(nativeEvents[0].NativeCursorBefore);
        Assert.NotNull(nativeEvents[0].NativeCursorAfter);
        Assert.NotEqual(nativeEvents[0].NativeCursorBefore,
            nativeEvents[0].NativeCursorAfter);

        string before = CanonicalJson.Sha256(scriptedStreams);
        RandomBoundaryEventV111[] scriptedEvents;
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            var scripted = new ScriptedRandomIndexProviderV111([decision]);
            var replayed = scripted.Resolve(Request(), scriptedStreams);
            scripted.AssertComplete();
            Assert.Equal(decision.SelectedValue, replayed.SelectedValue);
            scriptedEvents = journal.Entries.ToArray();
        }
        Assert.Equal(before, CanonicalJson.Sha256(scriptedStreams));
        Assert.Null(scriptedEvents[0].NativeCursorBefore);
        var compared = RandomBoundaryComparisonV111.Compare(nativeEvents, scriptedEvents);
        Assert.Equal("RANDOM_BOUNDARIES_CONSISTENT", compared.Status);
        Assert.Equal(1, compared.Compared);
        Assert.Equal("NOT_TESTED", compared.SourceHookParity);
        Assert.Equal("RANDOM_BOUNDARY_MISMATCH",
            RandomBoundaryComparisonV111.Compare(nativeEvents, scriptedEvents,
                requireNativeCursorEquality: true).Status);
    }

    [Fact]
    public void CandidateOrderingAndStreamOwnershipAreChecked()
    {
        var streams = NativeRngV111.CreateBundle("boundary-order");
        RandomBoundaryEventV111[] first;
        RandomBoundaryEventV111[] wrongOrder;
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            new ControlledRandomIndexProviderV111(0).Resolve(Request(), streams);
            first = journal.Entries.ToArray();
        }
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            new ControlledRandomIndexProviderV111(0)
                .Resolve(Request() with { OrderedCandidates = [46, 45, 44] }, streams);
            wrongOrder = journal.Entries.ToArray();
        }
        var difference = RandomBoundaryComparisonV111.Compare(first, wrongOrder);
        Assert.Equal("RANDOM_BOUNDARY_MISMATCH", difference.Status);
        Assert.Equal(0, difference.FirstMismatchIndex);
        Assert.Equal("ordered_candidates", difference.MismatchField);
    }

    [Fact]
    public void InvalidOutcomesAreNotJournaledAndNestedScopesRemainIsolated()
    {
        var streams = NativeRngV111.CreateBundle("boundary-invalid");
        string before = CanonicalJson.Sha256(streams);
        using var outer = RandomBoundaryJournalV111.Begin();
        Assert.Throws<ArgumentException>(() =>
            new ControlledRandomIndexProviderV111(99)
                .Resolve(Request(), streams));
        Assert.Empty(outer.Entries);
        using (var inner = RandomBoundaryJournalV111.Begin())
        {
            new ControlledRandomIndexProviderV111(1).Resolve(Request(), streams);
            Assert.Single(inner.Entries);
            Assert.Empty(outer.Entries);
        }
        new ControlledRandomIndexProviderV111(2).Resolve(Request(), streams);
        Assert.Single(outer.Entries);
        Assert.Equal(before, CanonicalJson.Sha256(streams));
    }

    [Fact]
    public void AdditionalOrMissingRequestsAreNotMistakenForParity()
    {
        var streams = NativeRngV111.CreateBundle("boundary-missing");
        using var journal = RandomBoundaryJournalV111.Begin();
        new ControlledRandomIndexProviderV111(0).Resolve(Request(), streams);
        var one = journal.Entries.ToArray();
        new ControlledRandomIndexProviderV111(0).Resolve(
            Request() with { Ordinal = 1 }, streams);
        var two = journal.Entries.ToArray();
        var diff = RandomBoundaryComparisonV111.Compare(one, two);
        Assert.Equal("RANDOM_BOUNDARY_LENGTH_MISMATCH", diff.Status);
        Assert.Equal(1, diff.FirstMismatchIndex);
    }
}
