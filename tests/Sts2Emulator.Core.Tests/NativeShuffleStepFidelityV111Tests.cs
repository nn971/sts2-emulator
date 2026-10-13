using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeShuffleStepFidelityV111Tests
{
    private static readonly long[] NativeOrdered = [0, 1, 2, 3, 4, 5];

    private static NativeShuffleStepExpectationV111 Expectation(
        long[]? ordered = null, long[]? post = null)
    {
        var stream = NativeRngV111.CreateBundle("step-shuffle-native-evidence");
        var shuffled = NativeFisherYatesV111.Execute(
            ordered ?? NativeOrdered, stream, new NativeRandomIndexProviderV111());
        return new NativeShuffleStepExpectationV111(
            "recycle",
            shuffled.OrderedInput,
            shuffled.PreHookOutput,
            post ?? shuffled.PreHookOutput,
            shuffled.Choices.Select(c => c.SelectedIndex).ToArray());
    }

    [Fact]
    public void StepScopedScriptUsesActualFisherYatesProviderAndChecksPostHook()
    {
        var input = NativeOrdered.Reverse().ToArray();
        var source = Expectation();
        var rng = NativeRngV111.CreateBundle("step-isolated-nature");
        var before = CanonicalJson.Sha256(rng);
        using (var scope = NativeShuffleStepReplayV111.Begin([source], rng))
        {
            using var boundaries = RandomBoundaryJournalV111.Begin();
            var result = NativeShuffleStepReplayV111.ShuffleRecycle(input, rng);
            Assert.Equal(source.NativePreHookOutput, result);
            Assert.Equal(source.NativePostHookOutput,
                NativeShuffleStepReplayV111.CheckPostHook(result));
            scope.AssertComplete();
            var observation = Assert.Single(scope.Observations);
            Assert.Equal("SOURCE_SHUFFLE_STAGE_CONSISTENT", observation.Status);
            Assert.Equal("SOURCE_SHUFFLE_STAGES_CONSISTENT", scope.Status);
            Assert.Equal(5, boundaries.Entries.Count);
            Assert.Equal(5, boundaries.Entries.Select(c => c.Request.Ordinal).Distinct().Count());
        }
        Assert.Equal(before, CanonicalJson.Sha256(rng));
    }

    [Fact]
    public void SourceShufflePostHookMismatchesWithoutOverwritingEmulatorResult()
    {
        var source = Expectation();
        var alteredPost = source.NativePostHookOutput.Reverse().ToArray();
        if (alteredPost.SequenceEqual(source.NativePostHookOutput))
            (alteredPost[0], alteredPost[1]) = (alteredPost[1], alteredPost[0]);
        var expected = source with { NativePostHookOutput = alteredPost };
        using var scope = NativeShuffleStepReplayV111.Begin(
            [expected], NativeRngV111.CreateBundle("step-scripted"));
        var actual = NativeShuffleStepReplayV111.ShuffleRecycle(
            (long[])NativeOrdered.Clone(), NativeRngV111.CreateBundle("legacy"));
        Assert.Equal(actual, NativeShuffleStepReplayV111.CheckPostHook(actual));
        var observation = Assert.Single(scope.Observations);
        Assert.Equal("SOURCE_SHUFFLE_POST_HOOK_MISMATCH", observation.Status);
        Assert.Equal("post_hook_order", observation.FirstMismatchStage);
        scope.AssertComplete();
    }

    [Fact]
    public void WrongSourceCardsAndIncompleteScriptsFailClosed()
    {
        var native = Expectation();
        using var scope = NativeShuffleStepReplayV111.Begin(
            [native], NativeRngV111.CreateBundle("mapping-mismatch"));
        Assert.Throws<InvalidOperationException>(() =>
            NativeShuffleStepReplayV111.ShuffleRecycle(
                [0, 1, 2, 3, 4, 77],
                NativeRngV111.CreateBundle("legacy")));
        Assert.Equal("REPLAY_INCOMPLETE", scope.Status);
        Assert.Throws<InvalidOperationException>(scope.AssertComplete);
    }

    [Fact]
    public void InvalidSourcePermutationIsRejectedBeforeAnyStep()
    {
        var native = Expectation();
        var altered = (long[])native.NativePreHookOutput.Clone();
        altered[0] = -22;
        Assert.Throws<ArgumentException>(() =>
            NativeShuffleStepReplayV111.Begin(
                [native with { NativePreHookOutput = altered }],
                NativeRngV111.CreateBundle("invalid")));
    }

    [Fact]
    public void ScopeNeverLeaksAcrossNestedOrCompletedEngineSteps()
    {
        var native = Expectation();
        var rng = NativeRngV111.CreateBundle("nested-probe");
        using (var outer = NativeShuffleStepReplayV111.Begin([native], rng))
        {
            using (var inner = NativeShuffleStepReplayV111.Begin([], rng))
            {
                Assert.Equal("SOURCE_SHUFFLE_STAGES_CONSISTENT", inner.Status);
            }
            Assert.Equal("REPLAY_INCOMPLETE", outer.Status);
        }
        var independent = NativeShuffleStepReplayV111.ShuffleRecycle(
            [0, 1, 2, 3], NativeRngV111.CreateBundle("legacy"));
        Assert.Equal(4, independent.Length);
    }
}
