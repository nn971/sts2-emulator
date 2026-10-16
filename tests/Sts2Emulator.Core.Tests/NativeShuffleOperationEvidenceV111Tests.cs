using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeShuffleOperationEvidenceV111Tests
{
    private static readonly string[] Source = ["card:0", "card:1", "card:2", "card:3"];

    private static NativeShuffleOperationEvidenceV111 Sample()
    {
        var streams = NativeRngV111.CreateBundle("native-shuffle-evidence");
        var outcome = NativeFisherYatesV111.Execute(
            [0L, 1L, 2L, 3L], streams, new NativeRandomIndexProviderV111());
        return new NativeShuffleOperationEvidenceV111(
            1, "recycle", (string[])Source.Clone(),
            outcome.Choices.Select(c => new NativeShuffleChoiceV111(
                c.Request.BoundedCount!.Value, c.SelectedIndex,
                c.NativeCursorBefore!, c.NativeCursorAfter!)).ToArray(),
            outcome.PreHookOutput.Select(id => "card:" + id).ToArray(),
            outcome.PreHookOutput.Select(id => "card:" + id).ToArray(),
            ["Strike", "Defend", "Strike", "Defend"]);
    }

    [Fact]
    public void NativeSourceShuffleValidatedAtInputPreHookAndPostHookStages()
    {
        Sample().Validate();
    }

    [Fact]
    public void WrongSwapIndexAndMissingCallFailClosed()
    {
        var sample = Sample();
        var bad = sample.Choices.ToArray();
        bad[0] = bad[0] with
        {
            SelectedIndex = (bad[0].SelectedIndex + 1) % 4
        };
        Assert.Throws<ArgumentException>(() =>
            (sample with { Choices = bad }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (sample with { Choices = sample.Choices.Skip(1).ToArray() }).Validate());
    }

    [Fact]
    public void HookReordersWithoutChangingCardIdentity()
    {
        var sample = Sample();
        var reversed = sample.PostHookOrder.Reverse().ToArray();
        (sample with { PostHookOrder = reversed }).Validate();
        var duplicated = sample.PostHookOrder.ToArray();
        duplicated[0] = duplicated[1];
        Assert.Throws<ArgumentException>(() =>
            (sample with { PostHookOrder = duplicated }).Validate());
    }

    [Fact]
    public void DuplicatedCardModelsStillRequireDistinctOperationTokens()
    {
        var sample = Sample();
        var tokens = sample.OrderedInput.ToArray();
        tokens[2] = tokens[0];
        Assert.Throws<ArgumentException>(() =>
            (sample with { OrderedInput = tokens }).Validate());
        // Card models are intentionally allowed to repeat; native object
        // identities are local to the specific shuffle operation.
        Assert.Equal(2, sample.CardModels.Distinct().Count());
    }

    [Fact]
    public void CursorDiscontinuityAndBrokenBoundFail()
    {
        var sample = Sample();
        var gap = sample.Choices.ToArray();
        gap[1] = gap[1] with
        {
            CursorBefore = gap[1].CursorBefore with
            {
                Counter = gap[1].CursorBefore.Counter + 2
            }
        };
        Assert.Throws<ArgumentException>(() =>
            (sample with { Choices = gap }).Validate());
        var bound = sample.Choices.ToArray();
        bound[1] = bound[1] with { BoundedCount = 7 };
        Assert.Throws<ArgumentException>(() =>
            (sample with { Choices = bound }).Validate());
    }
}
