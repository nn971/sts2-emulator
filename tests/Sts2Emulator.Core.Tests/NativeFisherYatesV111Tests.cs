using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeFisherYatesV111Tests
{
    private static readonly long[] Input =
        [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];

    [Fact]
    public void NativeAndScriptedShuffleMatchEverySwapWithoutChangingScriptRng()
    {
        var nativeRng = NativeRngV111.CreateBundle("shuffle-source-audit-971");
        var scriptedRng = NativeRngV111.CreateBundle("shuffle-source-audit-971");
        var native = NativeFisherYatesV111.Execute(
            Input, nativeRng, new NativeRandomIndexProviderV111());
        Assert.Equal(9, native.Choices.Length);
        Assert.Equal(Enumerable.Range(2, 9).Reverse(),
            native.Choices.Select(d => d.Request.BoundedCount!.Value));
        Assert.Equal(Enumerable.Range(0, 9),
            native.Choices.Select(d => d.Request.Ordinal));
        Assert.Equal("run.shuffle",
            Assert.Single(native.Choices.Select(d => d.Request.StreamId).Distinct()));
        var untouched = CanonicalJson.Sha256(scriptedRng);
        var provider = new ScriptedRandomIndexProviderV111(native.Choices);
        var replay = NativeFisherYatesV111.Execute(Input, scriptedRng, provider);
        provider.AssertComplete();
        Assert.Equal(native.PreHookOutput, replay.PreHookOutput);
        Assert.Equal(Input, native.OrderedInput);
        Assert.Equal(untouched, CanonicalJson.Sha256(scriptedRng));
    }

    [Fact]
    public void RngInputOrderingAndChoiceCountAreStrictlyAudited()
    {
        var streams = NativeRngV111.CreateBundle("shuffle-native-coverage");
        var native = NativeFisherYatesV111.Execute(
            Input, streams, new NativeRandomIndexProviderV111());
        var replayRng = NativeRngV111.CreateBundle("shuffle-native-coverage");
        var replay = new ScriptedRandomIndexProviderV111(native.Choices);
        var permuted = (long[])Input.Clone();
        (permuted[0], permuted[1]) = (permuted[1], permuted[0]);
        var output = NativeFisherYatesV111.Execute(
            permuted, replayRng, replay);
        Assert.NotEqual(native.PreHookOutput, output.PreHookOutput);
        Assert.Throws<InvalidOperationException>(() =>
            NativeFisherYatesV111.Execute(Input[..9],
                NativeRngV111.CreateBundle("shuffle-native-coverage"),
                new ScriptedRandomIndexProviderV111(native.Choices)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EmptyAndSingletonInputMakeZeroRandomCalls(int count)
    {
        var streams = NativeRngV111.CreateBundle("shuffle-no-calls");
        var before = CanonicalJson.Sha256(streams);
        var result = NativeFisherYatesV111.Execute(
            Input.Take(count).ToArray(), streams,
            new NativeRandomIndexProviderV111());
        Assert.Empty(result.Choices);
        Assert.Equal(before, CanonicalJson.Sha256(streams));
    }

    [Fact]
    public void CardInstanceIdentityMustBeUnambiguous()
    {
        Assert.Throws<ArgumentException>(() => NativeFisherYatesV111.Execute(
            [1, 2, 1], NativeRngV111.CreateBundle("shuffle-duplicate"),
            new NativeRandomIndexProviderV111()));
    }
}
