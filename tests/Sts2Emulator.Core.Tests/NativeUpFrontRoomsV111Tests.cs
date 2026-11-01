using Xunit;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Source-model queue identities and the complete pinned UpFront continuation.
/// Expected counters come from the source-ordered independent sampler;
/// native RunStarted currently observes only the final stream, not room queues.
/// </summary>
public sealed class NativeUpFrontRoomsV111Tests
{
    private const string Seed = "6JK8MD0VQQTA";

    [Fact]
    public void FullyUnlockedSilentA10GeneratesAllThreeActsInSourceOrder()
    {
        var rng = NativeRngV111.CreateBundle(Seed);
        var bags = NativeUpFrontRelicBagsV111.Populate(rng);
        var result = NativeUpFrontRoomsV111.Generate(rng, 10);
        Assert.Equal(229, bags.AfterPlayerBag.Counter);
        Assert.Equal(231, result.AfterSharedAncients.Counter);
        Assert.Equal(["Darv"], result.HiveSharedAncients);
        Assert.Empty(result.GlorySharedAncients);
        Assert.Equal(3, result.Acts.Length);
        Assert.Equal([261, 322, 379],
            result.Acts.Select(act => act.AfterEvents.Counter).ToArray());
        Assert.Equal([295, 355, 412],
            result.Acts.Select(act => act.AfterRooms.Counter).ToArray());
        Assert.Equal([31, 28, 25],
            result.Acts.Select(act => act.Events.Length).ToArray());
        Assert.Equal([15, 14, 13],
            result.Acts.Select(act => act.NormalEncounters.Length).ToArray());
        Assert.All(result.Acts, act => Assert.Equal(15, act.EliteEncounters.Length));
        Assert.Equal([2, 2, 2],
            result.Acts.Select(act => act.RejectedCandidateDraws).ToArray());

        Assert.Equal(["SlimesWeak", "FuzzyWurmCrawlerWeak", "ShrinkerBeetleWeak"],
            result.Acts[0].NormalEncounters[..3]);
        Assert.Equal("TheKinBoss", result.Acts[0].BossEncounter);
        Assert.Equal("Neow", result.Acts[0].Ancient);
        Assert.Null(result.Acts[0].SecondBossEncounter);
        Assert.Equal("KaiserCrabBoss", result.Acts[1].BossEncounter);
        Assert.Equal("Orobas", result.Acts[1].Ancient);
        Assert.Equal("TestSubjectBoss", result.Acts[2].BossEncounter);
        Assert.Equal("Nonupeipe", result.Acts[2].Ancient);
        Assert.Equal("QueenBoss", result.Acts[2].SecondBossEncounter);

        // A seeded xoshiro state at call 412, independently recomputed
        // without importing a state from the original game's trace.
        Assert.Equal(new NativeRngSnapshot(
            412, 14133024965363381498UL, 3052595882297166200UL,
            14908445774459425184UL, 16826779579573568376UL),
            result.FinalRng);
        Assert.Equal(result.FinalRng,
            NativeRngV111.ToSnapshot(rng.Streams.Single(
                stream => stream.StreamId == "run.up_front")));
    }

    [Theory]
    [InlineData(1, PrototypeRoomType.Combat, "SlimesWeak",
        "proto.encounter.slimes_weak")]
    [InlineData(1, PrototypeRoomType.Combat, "FuzzyWurmCrawlerWeak",
        "proto.encounter.fuzzy_wurm_crawler_weak")]
    [InlineData(1, PrototypeRoomType.Elite, "ByrdonisElite",
        "proto.encounter.byrdonis_elite")]
    [InlineData(1, PrototypeRoomType.Boss, "TheKinBoss",
        "proto.encounter.the_kin_boss")]
    public void CrosswalkResolvesUnambiguousSourceIdentities(
        int act, PrototypeRoomType room, string sourceId, string emulatorId)
    {
        var mapped = NativeUpFrontEncounterCrosswalkV111.Resolve(
            act, room, sourceId);
        Assert.Equal("RESOLVED", mapped.Status);
        Assert.Equal(emulatorId, mapped.EmulatorId);
    }

    [Fact]
    public void CrosswalkFailsClosedForUnknownOrWrongActAndAuditsEveryQueue()
    {
        var missing = NativeUpFrontEncounterCrosswalkV111.Resolve(
            1, PrototypeRoomType.Combat, "DoesNotExistWeak");
        Assert.Equal("UNSUPPORTED_SOURCE_ENCOUNTER", missing.Status);
        Assert.Null(missing.EmulatorId);
        var wrongRoom = NativeUpFrontEncounterCrosswalkV111.Resolve(
            1, PrototypeRoomType.Elite, "SlimesWeak");
        Assert.Equal("UNSUPPORTED_SOURCE_ENCOUNTER", wrongRoom.Status);
        Assert.Null(wrongRoom.EmulatorId);

        var rng = NativeRngV111.CreateBundle(Seed);
        _ = NativeUpFrontRelicBagsV111.Populate(rng);
        var rooms = NativeUpFrontRoomsV111.Generate(rng, 10);
        var mappings = NativeUpFrontEncounterCrosswalkV111.Audit(rooms);
        Assert.Equal(91, mappings.Length);
        Assert.DoesNotContain(mappings, mapping =>
            mapping.Status == "AMBIGUOUS_SOURCE_ENCOUNTER");
        Assert.All(mappings.Where(mapping => mapping.Act == 1),
            mapping => Assert.Equal("RESOLVED", mapping.Status));
        Assert.Equal(rooms.FinalRng,
            NativeRngV111.ToSnapshot(rng.Streams.Single(
                stream => stream.StreamId == "run.up_front")));
    }

    [Fact]
    public void GeneratedQueuesAndAncientSubsetsForkWithoutSharedArrays()
    {
        var rng = NativeRngV111.CreateBundle(Seed);
        _ = NativeUpFrontRelicBagsV111.Populate(rng);
        var original = NativeUpFrontRoomsV111.Generate(rng, 10);
        var fork = original.Fork();
        fork.HiveSharedAncients[0] = "Changed";
        fork.Acts[0].Events[0] = "Changed";
        fork.Acts[0].NormalEncounters[0] = "Changed";
        fork.Acts[0].EliteEncounters[0] = "Changed";
        Assert.Equal("Darv", original.HiveSharedAncients[0]);
        Assert.NotEqual("Changed", original.Acts[0].Events[0]);
        Assert.Equal("SlimesWeak", original.Acts[0].NormalEncounters[0]);
        Assert.NotEqual("Changed", original.Acts[0].EliteEncounters[0]);
    }

    [Fact]
    public void RoomsRequireTwoRealSourceRelicBagPopulations()
    {
        var rng = NativeRngV111.CreateBundle(Seed);
        Assert.Throws<InvalidDataException>(() =>
            NativeUpFrontRoomsV111.Generate(rng, 10));
    }
}
