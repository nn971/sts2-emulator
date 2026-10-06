using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class ForkTests
{
    [Fact]
    public void RngBytesAreIndependentAcrossForks()
    {
        var bundle = new RngBundle(new[]
        {
            new RngStreamState("combat", "opaque-v0", new byte[] { 1, 2, 3 }, 0)
        });

        var fork = bundle.Fork();
        fork.Streams[0].StateBytes[0] = 99;

        Assert.Equal(1, bundle.Streams[0].StateBytes[0]);
        Assert.Equal(99, fork.Streams[0].StateBytes[0]);
    }

    [Fact]
    public void RunStateForkPreservesCanonicalValueInitially()
    {
        using var doc = JsonDocument.Parse("{\"hidden\":42}");
        var empty = JsonDocument.Parse("{}").RootElement.Clone();
        var state = new RunState(
            "game-build",
            "0.1",
            "run",
            "seed",
            12,
            RunPhase.Shop,
            new PlayerState(
                30,
                80,
                123,
                new[] { new CardInstance(1, "demo-card", 0, empty) },
                Array.Empty<RelicInstance>(),
                Array.Empty<PotionInstance?>()),
            new RngBundle(new[] { new RngStreamState("shop", "opaque-v0", new byte[] { 7, 8 }) }),
            doc.RootElement.Clone());

        var fork = state.Fork();

        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        Assert.NotSame(state.Rng.Streams[0].StateBytes, fork.Rng.Streams[0].StateBytes);
    }
}
