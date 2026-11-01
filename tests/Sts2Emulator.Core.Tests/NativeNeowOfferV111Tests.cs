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
    public void UnlockGatesCanSharePostOfferRngStates()
    {
        var variants = NativeNeowOfferV111.PredictAll("source-availability-v111");
        // Both single-allowed hypotheses have the same shuffle length:
        // identical RNG cursors do NOT prove identical offered options.
        Assert.Equal(3, variants.Select(v => (
            v.EventRng.Counter, v.EventRng.State0,
            v.EventRng.State1, v.EventRng.State2, v.EventRng.State3
        )).Distinct().Count());
        Assert.Equal(variants[0].CursedRelicId, variants[3].CursedRelicId);
        Assert.All(variants, v => Assert.DoesNotContain(
            "proto.native.neow.massivescroll", v.OfferedRelicIds));
    }

    [Theory]
    [InlineData("native-neow-live-initial-v111")]
    [InlineData("ABCDEF234567")]
    [InlineData("oldlegacy")]
    public void V111StartRunActuallyUsesNativeEventLocalRngOffers(string seed)
    {
        var configuration = RunConfiguration.Create(
            "silent", 10, ActIdentity.Overgrowth);
        var root = V111RunFactory.Create(seed, configuration);
        var engine = new PrototypeGameEngine();
        var initial = engine.Step(root, engine.GetLegalActions(root).Single()).State;
        Assert.NotNull(initial.World?.Event);
        var expected = NativeNeowOfferV111.Predict(
            seed, kaleidoscopeAllowed: true, scrollBoxesAllowed: true);
        Assert.Equal(expected.OfferedRelicIds.Select(x => "take_" + x),
            initial.World!.Event!.OfferedChoiceIds);
        Assert.Equal(15, initial.NativeRunRng!.Streams.Length);
        // Native Neow options consume ONLY their event-local native RNG.
        // In particular, do not generate discarded prototype offers first.
        Assert.Equal(0UL, initial.Rng.Streams.Single(
            x => x.StreamId == "event").CallCount);
        Assert.NotNull(initial.NativeEventRng);
        Assert.Equal(expected.EventRng, NativeRngV111.ToSnapshot(
            Assert.Single(initial.NativeEventRng!.Streams)));
        Assert.Equal("context.event.NEOW.player_0",
            Assert.Single(initial.NativeEventRng.Streams).StreamId);
        var fork = initial.Fork();
        Assert.NotSame(initial.NativeEventRng, fork.NativeEventRng);
        Assert.Equal(CanonicalJson.Sha256(initial.NativeEventRng),
            CanonicalJson.Sha256(fork.NativeEventRng));
        // Event-local rolls must leave persistent native named streams intact.
        Assert.Equal(
            NativeRngV111.CreateBundle(seed).Streams
                .Select(x => NativeRngV111.ToSnapshot(x)),
            initial.NativeRunRng.Streams
                .Select(x => NativeRngV111.ToSnapshot(x)));
    }

    [Fact]
    public void NativeNeowGeneratorHasDifferentSeedOwnershipThanPrototype()
    {
        var seed = "native-local-event-ownership";
        var root = V111RunFactory.Create(seed,
            RunConfiguration.Create("silent", 10, ActIdentity.Overgrowth));
        var engine = new PrototypeGameEngine();
        var started = engine.Step(root, engine.GetLegalActions(root).Single()).State;
        var expected = NativeNeowOfferV111.Predict(
            seed, kaleidoscopeAllowed: true, scrollBoxesAllowed: true);
        Assert.Equal(expected.OfferedRelicIds.Select(x => "take_" + x),
            started.World!.Event!.OfferedChoiceIds);
        // Neow is allocated its own event stream, so its draws are never
        // charged to the persistent run/player counters.
        Assert.Equal(0, started.NativeRunRng!.Streams
            .Select(x => NativeRngV111.ToSnapshot(x).Counter).Sum());
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
