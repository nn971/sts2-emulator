using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Inspector-independent differential test at the primitive request boundary.
/// This is not a substitute for full gameplay mechanical-equivalence tests.
/// </summary>
public sealed class StochasticProviderDifferentialAcceptanceTests
{
    [Theory]
    [InlineData("stochastic-diff-01")]
    [InlineData("stochastic-diff-02")]
    [InlineData("stochastic-diff-03")]
    [InlineData("stochastic-diff-04")]
    public void NativeAndScriptedConsumeIdenticalSemanticOutcomesWithoutSharingPrivateState(
        string seed)
    {
        var native = NativeRngV111.CreateBundle(seed);
        var scriptedState = NativeRngV111.CreateBundle(seed);
        var originalScriptedHash = CanonicalJson.Sha256(scriptedState);

        RandomIndexRequestV111[] requests =
        [
            new("combat.initial_unique_hp", "run.niche", "creature:1",
                0, [44, 45, 46]),
            new("combat.initial_unique_hp", "run.niche", "creature:2",
                0, [33, 34, 35, 36]),
            new("combat.initial_unique_hp", "run.niche", "creature:3",
                0, [12, 13]),
        ];

        RandomIndexDecisionV111[] actual;
        RandomBoundaryEventV111[] nativeEvents;
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            var provider = new NativeRandomIndexProviderV111();
            actual = requests.Select(request => provider.Resolve(request, native))
                .ToArray();
            nativeEvents = journal.Entries.ToArray();
        }

        RandomBoundaryEventV111[] scriptedEvents;
        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            var provider = new ScriptedRandomIndexProviderV111(actual);
            for (var i = 0; i < requests.Length; i++)
            {
                var resolved = provider.Resolve(requests[i], scriptedState);
                Assert.Equal(actual[i].SelectedValue, resolved.SelectedValue);
            }
            provider.AssertComplete();
            scriptedEvents = journal.Entries.ToArray();
        }

        Assert.Equal(originalScriptedHash, CanonicalJson.Sha256(scriptedState));
        Assert.Equal(requests.Length, nativeEvents.Length);
        Assert.Equal(requests.Length, scriptedEvents.Length);
        var compared = RandomBoundaryComparisonV111.Compare(
            nativeEvents, scriptedEvents);
        Assert.Equal("RANDOM_BOUNDARIES_CONSISTENT", compared.Status);
        Assert.Equal(requests.Length, compared.Compared);
        Assert.Equal("NOT_TESTED", compared.SourceHookParity);
    }
}
