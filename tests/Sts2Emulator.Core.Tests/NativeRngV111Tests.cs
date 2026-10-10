using System.Text;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRngV111Tests
{
    [Theory]
    [InlineData("", 0xef46db3751d8e999UL)]
    [InlineData("a", 0xd24ec4f1a98c6e5bUL)]
    [InlineData("abc", 0x44bc2cf5ad770999UL)]
    public void HashMatchesPublishedXxHash64Vectors(string text, ulong hash) =>
        Assert.Equal(hash, NativeRngV111.HashString(text));

    [Theory]
    [InlineData(31, 0x60dd0d01083b99f0UL)]
    [InlineData(32, 0xe2df261fc2ec30ebUL)]
    [InlineData(33, 0xb3fa465f554208a6UL)]
    public void HashBlockBoundariesAgreeWithIndependentAuditVectors(int length, ulong hash) =>
        Assert.Equal(hash, NativeRngV111.HashString(new string('x', length)));

    [Fact]
    public void Utf8SeedAndNamedStreamInitializationAgreeWithIndependentAuditVectors()
    {
        Assert.Equal(0x432f41b3f713622dUL, NativeRngV111.HashString(
            "rng-α-😀-long-seed-that-exercises-the-full-xxhash-loop"));
        var stream = NativeRngV111.CreateBundle("v111-codec-golden").Streams.Single(item => item.StreamId == "run.combat_targets");
        Assert.Equal(new NativeRngSnapshot(0, 10404991001613190346UL,
            2163311314968115413UL, 2138220657641300634UL, 1763551566488335709UL), NativeRngV111.ToSnapshot(stream));
        var rollover = new RngBundle([NativeRngV111.FromSnapshot("test", new(int.MaxValue, 1, 2, 3, 4))]);
        NativeRngV111.NextUInt64(rollover, "test");
        Assert.Equal(int.MinValue, NativeRngV111.ToSnapshot(rollover.Streams[0]).Counter);
    }

    [Fact]
    public void RecurrenceMatchesPublicXoshiro256StarStarVector()
    {
        var bundle = new RngBundle([NativeRngV111.FromSnapshot("test", new(0, 1, 2, 3, 4))]);
        Assert.Equal(11520UL, NativeRngV111.NextUInt64(bundle, "test"));
        Assert.Equal(new NativeRngSnapshot(1, 7, 0, 262146, 211106232532992),
            NativeRngV111.ToSnapshot(bundle.Streams[0]));
        Assert.Equal(0UL, NativeRngV111.NextUInt64(bundle, "test"));
    }

    [Fact]
    public void SeedingMatchesSplitMix64ZeroVector()
    {
        var snapshot = NativeRngV111.ToSnapshot(NativeRngV111.CreateStream("test", 0));
        Assert.Equal(new NativeRngSnapshot(0,
            0xe220a8397b1dcdafUL, 0x6e789e6aa1b965f4UL,
            0x06c45d188009454fUL, 0xf88bb8a8724c81ecUL), snapshot);
    }

    [Fact]
    public void FifteenNamedStreamsAreDistinctAndForkRestorePreservesFutureDraws()
    {
        var bundle = NativeRngV111.CreateBundle("rng-α-😀-long-seed-that-exercises-the-full-xxhash-loop");
        Assert.Equal(15, bundle.Streams.Length);
        Assert.Equal(15, bundle.Streams.Select(stream => Convert.ToHexString(stream.StateBytes)).Distinct().Count());
        Assert.Contains(bundle.Streams, stream => stream.StreamId == "run.combat_orbs");
        Assert.Contains(bundle.Streams, stream => stream.StreamId == "player.transformations");
        var fork = bundle.Fork();
        var untouched = NativeRngV111.ToSnapshot(bundle.Streams[0]);
        var next = NativeRngV111.NextUInt64(fork, fork.Streams[0].StreamId);
        Assert.Equal(untouched, NativeRngV111.ToSnapshot(bundle.Streams[0]));
        Assert.Equal(next, NativeRngV111.NextUInt64(bundle, bundle.Streams[0].StreamId));
        var restored = new RngBundle(fork.Streams.Select(stream => NativeRngV111.FromSnapshot(
            stream.StreamId, NativeRngV111.ToSnapshot(stream))).ToArray());
        for (var index = 0; index < 100; index++)
            Assert.Equal(NativeRngV111.NextInt(fork, "run.shuffle", -20, 30),
                NativeRngV111.NextInt(restored, "run.shuffle", -20, 30));
    }

    [Fact]
    public void NativeSamplingUsesDoubleScalingRatherThanModuloAndConsumesZeroWidthFloats()
    {
        var bundle = new RngBundle([NativeRngV111.FromSnapshot("test", new(0, 1, 2, 3, 4))]);
        Assert.Equal(0, NativeRngV111.NextInt(bundle, "test", 7)); // 11520 % 7 is 5
        Assert.Equal(5f, NativeRngV111.NextFloat(bundle, "test", 5, 5));
        Assert.Equal(2, NativeRngV111.ToSnapshot(bundle.Streams[0]).Counter);
        var all = NativeRngV111.CreateBundle("full-range");
        var fork = all.Fork();
        Assert.Equal(NativeRngV111.NextUInt64(fork, "run.niche"),
            NativeRngV111.NextUInt64(all, "run.niche", ulong.MaxValue));
    }

    [Fact]
    public void EmptyItemsAndSingletonShuffleConsumeNoRandomnessButGaussianConsumesPairs()
    {
        var bundle = NativeRngV111.CreateBundle("draw-count");
        Assert.Null(NativeRngV111.NextItem<string>(bundle, "run.niche", Array.Empty<string>()));
        NativeRngV111.Shuffle(bundle, "run.niche", new[] { 7 });
        var stream = bundle.Streams.Single(item => item.StreamId == "run.niche");
        Assert.Equal(0, NativeRngV111.ToSnapshot(stream).Counter);
        Assert.Equal(3, NativeRngV111.NextGaussianInt(bundle, "run.niche", 3, 0, 3, 3));
        stream = bundle.Streams.Single(item => item.StreamId == "run.niche");
        Assert.Equal(2, NativeRngV111.ToSnapshot(stream).Counter);
        var items = Enumerable.Range(0, 10).ToArray();
        NativeRngV111.Shuffle(bundle, "run.niche", items);
        Assert.Equal(11, NativeRngV111.ToSnapshot(bundle.Streams.Single(item => item.StreamId == "run.niche")).Counter);
        Assert.Equal(Enumerable.Range(0, 10), items.Order());
    }

    [Fact]
    public void InvalidCodecStateAndDuplicateStreamsFailLoudly()
    {
        Assert.Throws<InvalidDataException>(() => NativeRngV111.FromSnapshot("test", new(0, 0, 0, 0, 0)));
        Assert.Throws<InvalidDataException>(() => NativeRngV111.ToSnapshot(PrototypeRng.CreateBundle("legacy").Streams[0]));
        var stream = NativeRngV111.CreateStream("test", 1);
        Assert.Throws<InvalidDataException>(() => NativeRngV111.NextUInt64(new([stream, stream.Fork()]), "test"));
    }
}
