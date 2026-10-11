using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Pinned seed-derivation vectors use independent libxxhash XXH64 (seed 0)
/// plus SplitMix64, compared against the v0.111.0 constructor formulas.
/// These are codec/constructor tests, not real-game call-order parity.
/// </summary>
public sealed class NativeRngV111ContextTests
{
    private const string Seed = "WTAQ7JGMPLCH";

    [Fact]
    public void MapStreamUsesOneBasedActNumberAndPinnedSeedVector()
    {
        var map = NativeRngV111Contexts.ForActMap(Seed, 2);
        Assert.Equal("context.act_2_map", map.StreamId);
        Assert.Equal(new NativeRngSnapshot(0,
            16900874706717873270UL, 15157779645879145604UL,
            3139340848759896390UL, 17464462911470593770UL),
            NativeRngV111.ToSnapshot(map));
        Assert.NotEqual(NativeRngV111.ToSnapshot(map),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForActMap(Seed, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForActMap(Seed, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForActMap(Seed, 4));
    }

    [Fact]
    public void EncounterFormationMixesNativeEntryAndTotalFloor()
    {
        var stream = NativeRngV111Contexts.ForEncounterFormation(
            Seed, "ENTOMANCER_ELITE", 23);
        Assert.Equal(new NativeRngSnapshot(0,
            9440809968738821987UL, 8593649066030448553UL,
            7889792493235302594UL, 15146914020339388160UL),
            NativeRngV111.ToSnapshot(stream));
        Assert.NotEqual(NativeRngV111.ToSnapshot(stream),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForEncounterFormation(
                Seed, "ENTOMANCER_ELITE", 24)));
        Assert.NotEqual(NativeRngV111.ToSnapshot(stream),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForEncounterFormation(
                Seed, "ENTOMANCER_ELITE_2", 23)));
        Assert.Throws<ArgumentException>(
            () => NativeRngV111Contexts.ForEncounterFormation(Seed, "", 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForEncounterFormation(Seed, "A", -1));
    }

    [Fact]
    public void MonsterUsesMapCoordinatesActIndexAndCreatureCombatId()
    {
        var monster = NativeRngV111Contexts.ForMonster(Seed, 1, 17, 4, 8);
        Assert.Equal(new NativeRngSnapshot(0,
            3242721538251064823UL, 16152112767037021891UL,
            5008486761814845552UL, 2539601867202796371UL),
            NativeRngV111.ToSnapshot(monster));
        Assert.NotEqual(NativeRngV111.ToSnapshot(monster),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForMonster(
                Seed, 1, 18, 4, 8)));
        Assert.NotEqual(NativeRngV111.ToSnapshot(monster),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForMonster(
                Seed, 2, 17, 4, 8)));
        Assert.Equal(
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForMonster(
                Seed, 1, 17)),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForMonster(
                Seed, 1, 17, 0, 0)));
    }

    [Fact]
    public void EventSeedTracksNativeSharedAndOwnerSlotRules()
    {
        var a = NativeRngV111Contexts.ForEvent(Seed, "TRIAL");
        Assert.Equal(new NativeRngSnapshot(0,
            12296240631317923504UL, 9375813000791475181UL,
            10597902684348387469UL, 14026491770002181422UL),
            NativeRngV111.ToSnapshot(a));
        Assert.Equal(NativeRngV111.ToSnapshot(a),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForEvent(
                Seed, "TRIAL", 0, shared: true)));
        Assert.Equal(NativeRngV111.ToSnapshot(a),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForEvent(
                Seed, "TRIAL", 4, shared: true)));
        Assert.NotEqual(NativeRngV111.ToSnapshot(a),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForEvent(
                Seed, "TRIAL", 1)));
        Assert.NotEqual(NativeRngV111.ToSnapshot(a),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForEvent(
                Seed, "ANOTHER_TRIAL")));
    }

    [Fact]
    public void ContentSeedHonorsCallerMixinAndPlayerSlot()
    {
        var content = NativeRngV111Contexts.ForPlayerContent(
            Seed, "FAKE_RELIC", mixin: 7);
        Assert.Equal(new NativeRngSnapshot(0,
            7274349365222831588UL, 11170619532277742512UL,
            15066494958150875371UL, 3438691424063908588UL),
            NativeRngV111.ToSnapshot(content));
        Assert.NotEqual(NativeRngV111.ToSnapshot(content),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForPlayerContent(
                Seed, "FAKE_RELIC", mixin: 8)));
        // Native arithmetic combines slot + mixin; this equivalence
        // deliberately documents the collision rather than promising
        // statistical independence among different content contexts.
        Assert.Equal(NativeRngV111.ToSnapshot(content),
            NativeRngV111.ToSnapshot(NativeRngV111Contexts.ForPlayerContent(
                Seed, "FAKE_RELIC", mixin: 6, playerSlot: 1)));
    }

    [Fact]
    public void ContextStreamStateCanBeForkedAndRestoredIndependently()
    {
        var stream = NativeRngV111Contexts.ForEvent(Seed, "TRIAL");
        var rng = new RngBundle([stream]);
        var fork = rng.Fork();
        var id = stream.StreamId;
        var initial = NativeRngV111.ToSnapshot(rng.Streams[0]);

        Assert.Equal(NativeRngV111.NextInt(fork, id, 17),
            NativeRngV111.NextInt(rng, id, 17));
        Assert.Equal(initial.Counter + 1,
            NativeRngV111.ToSnapshot(fork.Streams[0]).Counter);
        var restored = new RngBundle([
            NativeRngV111.FromSnapshot(
                id, NativeRngV111.ToSnapshot(fork.Streams[0]))]);
        for (var n = 0; n < 25; n++)
            Assert.Equal(NativeRngV111.NextUInt64(fork, id),
                NativeRngV111.NextUInt64(restored, id));
        Assert.Equal(1, NativeRngV111.ToSnapshot(rng.Streams[0]).Counter);
    }

    [Fact]
    public void ContextGeneratorsLeavePersistentNamedRunStreamsUntouched()
    {
        var persistent = NativeRngV111.CreateBundle(Seed);
        var hashBefore = persistent.Streams.Select(
            NativeRngV111.ToSnapshot).ToArray();
        var eventRng = new RngBundle([
            NativeRngV111Contexts.ForEvent(Seed, "TRIAL")]);
        var encounterRng = new RngBundle([
            NativeRngV111Contexts.ForEncounterFormation(
                Seed, "ENTOMANCER_ELITE", 23)]);
        _ = NativeRngV111.NextUInt64(eventRng, eventRng.Streams[0].StreamId);
        _ = NativeRngV111.NextUInt64(encounterRng, encounterRng.Streams[0].StreamId);
        Assert.Equal(hashBefore,
            persistent.Streams.Select(NativeRngV111.ToSnapshot).ToArray());
        Assert.Equal(15, persistent.Streams.Length);
    }

    [Fact]
    public void InvalidContextIdentifiersAndCoordinatesFailBeforeRandomDraws()
    {
        Assert.Throws<ArgumentException>(
            () => NativeRngV111Contexts.ForEvent(Seed, " "));
        Assert.Throws<ArgumentException>(
            () => NativeRngV111Contexts.ForPlayerContent(Seed, ""));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForEvent(Seed, "TRIAL", -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForPlayerContent(Seed, "A", 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForMonster(Seed, 3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForMonster(Seed, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeRngV111Contexts.ForMonster(Seed, 0, 1, 7));
    }
}
