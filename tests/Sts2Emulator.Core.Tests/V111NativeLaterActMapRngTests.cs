using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111NativeLaterActMapRngTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 10)]
    [InlineData(3, 0)]
    [InlineData(3, 10)]
    public void NativeMapStreamIsDeterministicAndAuditable(
        int act, int ascension)
    {
        var seed = $"native-context-map-{act}-{ascension}";
        var (map, finalRng) =
            PrototypeNativeLaterActMap.GenerateNativeWithAudit(
                seed, act, ascension);
        var (repeatMap, repeatRng) =
            PrototypeNativeLaterActMap.GenerateNativeWithAudit(
                seed, act, ascension);
        Assert.Equal(CanonicalJson.Sha256(map),
            CanonicalJson.Sha256(repeatMap));
        Assert.Equal(finalRng, repeatRng);
        Assert.True(finalRng.Counter > 0);
        Assert.Equal(PrototypeNativeLaterActMap.ProfileId(act),
            map.GenerationProfileId);
        Assert.Equal(
            CanonicalJson.Sha256(map),
            CanonicalJson.Sha256(
                PrototypeNativeLaterActMap.GenerateNative(seed, act, ascension)));

        // Independent reconstruction: the final state must equal exactly
        // the number of native xoshiro steps consumed by the map algorithm.
        var initial = NativeRngV111Contexts.ForActMap(seed, act);
        var bundle = new RngBundle([initial]);
        for (var i = 0; i < finalRng.Counter; i++)
            _ = NativeRngV111.NextUInt64(bundle, initial.StreamId);
        Assert.Equal(finalRng, NativeRngV111.ToSnapshot(bundle.Streams[0]));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeMapConstructionDoesNotConsumeNamedRunOrPrototypeStreams(
        int act)
    {
        var seed = $"map-context-isolation-{act}";
        var nativeRun = NativeRngV111.CreateBundle(seed);
        var prototypeRun = PrototypeRng.CreateBundle(seed);
        var nativeBefore = nativeRun.Streams.Select(
            NativeRngV111.ToSnapshot).ToArray();
        var prototypeBefore = CanonicalJson.Sha256(prototypeRun);
        var (first, firstEnd) =
            PrototypeNativeLaterActMap.GenerateNativeWithAudit(seed, act);
        Assert.Equal(nativeBefore,
            nativeRun.Streams.Select(NativeRngV111.ToSnapshot).ToArray());
        Assert.Equal(prototypeBefore, CanonicalJson.Sha256(prototypeRun));

        // Only the act-local stream advances; the next act begins from
        // a separately derived state even when initialized from one seed.
        var (_, otherEnd) =
            PrototypeNativeLaterActMap.GenerateNativeWithAudit(
                seed, act == 2 ? 3 : 2);
        Assert.NotEqual(firstEnd, otherEnd);
        Assert.NotEqual(CanonicalJson.Sha256(first),
            CanonicalJson.Sha256(
                PrototypeNativeLaterActMap.GenerateNative(
                    seed, act == 2 ? 3 : 2)));
    }

    [Fact]
    public void LegacyGeneratorRetainsOriginalPrototypeMapStreamConsumption()
    {
        var seed = "late-legacy-map-regression";
        var legacy = PrototypeRng.CreateBundle(seed);
        var before = CanonicalJson.Sha256(legacy);
        var unaffected = legacy.Streams
            .Where(s => s.StreamId != "map")
            .Select(s => (s.StreamId, Hash: CanonicalJson.Sha256(s)))
            .ToArray();
        var map = PrototypeNativeLaterActMap.Generate(legacy, 2);
        Assert.Equal(PrototypeNativeLaterActMap.HiveProfileId,
            map.GenerationProfileId);
        Assert.NotEqual(before, CanonicalJson.Sha256(legacy));
        foreach (var (streamId, hash) in unaffected)
            Assert.Equal(hash, CanonicalJson.Sha256(
                legacy.Streams.Single(s => s.StreamId == streamId)));
        Assert.Equal(
            CanonicalJson.Sha256(map),
            CanonicalJson.Sha256(
                PrototypeNativeLaterActMap.Generate(
                    PrototypeRng.CreateBundle(seed), 2)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void OptInFixtureUsesNativeMapWhilePreservingLegacyCompatibility(int act)
    {
        var seed = $"later-fixture-native-map-{act}";
        var fixture = PrototypeNativeLaterActFixture.Create(seed, act);
        var direct = PrototypeNativeLaterActMap.GenerateNative(seed, act);
        Assert.Equal(CanonicalJson.Sha256(direct),
            CanonicalJson.Sha256(fixture.World!.Map));
        Assert.Equal(CanonicalJson.Sha256(fixture),
            CanonicalJson.Sha256(fixture.Fork()));
        Assert.Equal(CanonicalJson.Sha256(fixture),
            CanonicalJson.Sha256(RunSnapshot.Load(
                RunSnapshot.Save(fixture))));

        // Prior Act 1 prototype generation cannot pollute a later
        // act's independent map generator or its compatibility RNG.
        var pristineMapStream = PrototypeRng.CreateBundle(seed).Streams
            .Single(s => s.StreamId == "map");
        var fixtureMapStream = fixture.Rng.Streams
            .Single(s => s.StreamId == "map");
        Assert.Equal(CanonicalJson.Sha256(pristineMapStream),
            CanonicalJson.Sha256(fixtureMapStream));
        PrototypeStateInvariants.Validate(fixture);
    }

    [Fact]
    public void InvalidNativeMapConfigurationIsRejectedBeforeGeneration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActMap.GenerateNative("seed", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActMap.GenerateNative("seed", 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActMap.GenerateNative("seed", 2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrototypeNativeLaterActMap.GenerateNative("seed", 3, 11));
        Assert.Throws<ArgumentNullException>(() =>
            PrototypeNativeLaterActMap.Generate(null!, 2));
    }
}
