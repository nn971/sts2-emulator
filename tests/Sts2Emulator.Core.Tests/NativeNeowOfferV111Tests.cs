using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeNeowOfferV111Tests
{
    [Theory]
    [InlineData("native-neow-rng-seed")]
    [InlineData("ABCDEF234567")]
    [InlineData("oldlegacy")]
    public void SourceLocalRngPredictionIsSeededIndependentlyOfPrototype(string seed)
    {
        var predictions = NativeNeowOfferV111.PredictAll(seed);
        Assert.Equal(4, predictions.Length);
        var repeat = NativeNeowOfferV111.PredictAll(seed);
        for (var k = 0; k < predictions.Length; k++)
        {
            var result = predictions[k];
            Assert.Equal(result.OfferedRelicIds, repeat[k].OfferedRelicIds);
            Assert.Equal(result.EventRng, repeat[k].EventRng);
            Assert.Equal(3, result.OfferedRelicIds.Length);
            Assert.Equal(3, result.OfferedRelicIds.Distinct().Count());
            Assert.All(result.OfferedRelicIds, id =>
                Assert.StartsWith("proto.native.neow.", id, StringComparison.Ordinal));
            Assert.Equal(result.OfferedRelicIds[2], result.CursedRelicId);
            Assert.NotEqual(0UL, result.EventRng.State0 | result.EventRng.State1 |
                                   result.EventRng.State2 | result.EventRng.State3);
            // 1 curse + (2 or 3) conditionals + (poolSize - 1) swaps.
            Assert.Equal(result.PositivePoolSize +
                (result.CursedRelicId == "proto.native.neow.largecapsule" ? 2 : 3),
                result.EventRng.Counter);
            var initial = NativeRngV111.CreateContentStream(
                "event.neow", NativeRngV111.RunSeed(seed), "NEOW");
            var replay = new RngBundle([initial]);
            for (int i = 0; i < result.EventRng.Counter; i++)
                NativeRngV111.NextUInt64(replay, "event.neow");
            Assert.Equal(result.EventRng, NativeRngV111.ToSnapshot(replay.Streams[0]));
        }
    }

    [Fact]
    public void UnlockGatesYieldDistinctPostOfferRngStates()
    {
        var variants = NativeNeowOfferV111.PredictAll("source-availability-v111");
        Assert.Equal(4, variants.Select(v => (
            v.EventRng.Counter, v.EventRng.State0,
            v.EventRng.State1, v.EventRng.State2, v.EventRng.State3
        )).Distinct().Count());
        Assert.Equal(variants[0].CursedRelicId, variants[3].CursedRelicId);
        Assert.All(variants, v => Assert.DoesNotContain(
            "proto.native.neow.massivescroll", v.OfferedRelicIds));
    }

    [Fact]
    public void PredictionNeverMutatesEmulatorRun()
    {
        var engine = new PrototypeGameEngine();
        var original = V111RunFactory.Create("probe-independent-seed",
            RunConfiguration.Create("silent", 10, ActIdentity.Overgrowth));
        var hash = CanonicalJson.Sha256(original);
        _ = NativeNeowOfferV111.PredictAll(original.RunSeed);
        Assert.Equal(hash, CanonicalJson.Sha256(original));
        var action = engine.GetLegalActions(original)[0];
        var state = engine.Step(original, action).State;
        var before = CanonicalJson.Sha256(state);
        _ = NativeNeowOfferV111.PredictAll(original.RunSeed);
        Assert.Equal(before, CanonicalJson.Sha256(state));
    }
}
