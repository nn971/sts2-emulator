using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Regression checks for a source-audited map translation. These tests use
/// emulator-generated data; original-game map/cursor parity is still unverified.
/// </summary>
public sealed class NativeStandardActMapV111Tests
{
    [Theory]
    [InlineData("source-standard-map-a0", 0)]
    [InlineData("source-standard-map-a10", 10)]
    [InlineData("6JK8MD0VQQTA", 10)]
    public void MapRngOwnsBothQuotasAndEntireDeterministicGraph(string seed, int ascension)
    {
        var initial = NativeRngV111Contexts.ForActMap(seed, 1);
        var persistent = NativeRngV111.CreateBundle(seed);
        var persistentBefore = persistent.Streams
            .Select(NativeRngV111.ToSnapshot).ToArray();
        var result = NativeStandardActMapV111.Generate(seed, ascension);
        var again = NativeStandardActMapV111.Generate(seed, ascension);

        Assert.Equal(NativeStandardActMapV111.ProfileId, result.Map.GenerationProfileId);
        Assert.Equal(CanonicalJson.Sha256(result.Map), CanonicalJson.Sha256(again.Map));
        Assert.Equal(result.FinalRng, again.FinalRng);
        Assert.Equal(result.AfterQuotas, again.AfterQuotas);
        Assert.Equal(result.AfterPaths, again.AfterPaths);
        Assert.Equal(result.AfterPruning, again.AfterPruning);
        Assert.True(result.AfterQuotas.Counter > 0);
        Assert.True(result.AfterPaths.Counter > result.AfterQuotas.Counter);
        Assert.True(result.AfterTypes.Counter > result.AfterPaths.Counter);
        Assert.True(result.AfterPruning.Counter >= result.AfterTypes.Counter);
        Assert.Equal(result.FinalRng, result.AfterPruning);
        Assert.InRange(result.RestQuota, 6, 7);
        Assert.InRange(result.UnknownQuota, 10, 14);
        Assert.Equal(ascension >= 1 ? 8 : 5, result.EliteQuota);
        Assert.NotEqual(NativeRngV111.ToSnapshot(initial), result.FinalRng);
        Assert.Equal(persistentBefore, persistent.Streams
            .Select(NativeRngV111.ToSnapshot).ToArray());

        var nodes = result.Map.Nodes;
        var identities = nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(nodes.Length, identities.Count);
        Assert.NotEmpty(result.Map.EntryNodeIds!);
        Assert.All(result.Map.EntryNodeIds!, id =>
            Assert.Equal(1, nodes.Single(n => n.NodeId == id).Floor));
        Assert.Equal(16, nodes.Max(n => n.Floor));
        Assert.Single(nodes, node => node.Floor == 16
            && node.RoomType == PrototypeRoomType.Boss);
        Assert.All(nodes.Where(n => n.Floor == 1),
            node => Assert.Equal(PrototypeRoomType.Combat, node.RoomType));
        Assert.All(nodes.Where(n => n.Floor == 9),
            node => Assert.Equal(PrototypeRoomType.Treasure, node.RoomType));
        Assert.All(nodes.Where(n => n.Floor == 15),
            node => Assert.Equal(PrototypeRoomType.Rest, node.RoomType));
        Assert.All(nodes, n =>
        {
            Assert.InRange(int.Parse(n.NodeId.Split(':')[2]), 0, 6);
            Assert.All(n.NextNodeIds ?? [], child =>
            {
                Assert.Contains(child, identities);
                Assert.Equal(n.Floor + 1,
                    nodes.Single(p => p.NodeId == child).Floor);
            });
        });
        var reachable = new HashSet<string>(result.Map.EntryNodeIds!,
            StringComparer.Ordinal);
        var pending = new Queue<string>(reachable);
        while (pending.Count > 0)
        {
            var nextId = pending.Dequeue();
            var node = nodes.Single(n => n.NodeId == nextId);
            foreach (var child in node.NextNodeIds ?? [])
                if (reachable.Add(child))
                    pending.Enqueue(child);
        }
        Assert.Contains("1:16:3", reachable);
    }

    [Theory]
    [InlineData(ActIdentity.Overgrowth)]
    [InlineData(ActIdentity.Underdocks)]
    public void FidelityOpeningUsesIndependentMapAndNeowWithSerializableCursors(
        ActIdentity region)
    {
        const string seed = "source-native-map-opening-a10";
        var configuration = RunConfiguration.Create("silent", 10, region) with
        {
            FidelityId = V111Build.NativeStandardMapOpeningFidelityId
        };
        var root = V111RunFactory.Create(seed, configuration);
        var engine = new PrototypeGameEngine();
        var started = engine.Step(root, engine.GetLegalActions(root).Single()).State;
        var map = NativeStandardActMapV111.Generate(seed, 10);

        Assert.Equal(RunPhase.Event, started.Phase);
        Assert.Equal(CanonicalJson.Sha256(map.Map),
            CanonicalJson.Sha256(started.World!.Map));
        Assert.NotNull(started.NativeMapRng);
        Assert.Equal(map.FinalRng,
            NativeRngV111.ToSnapshot(Assert.Single(started.NativeMapRng!.Streams)));
        Assert.Equal(NativeRngV111Contexts.ForActMap(seed, 1).StreamId,
            Assert.Single(started.NativeMapRng.Streams).StreamId);
        Assert.Equal(0UL, started.Rng.Streams.Single(s => s.StreamId == "map").CallCount);
        Assert.Equal(0UL, started.Rng.Streams.Single(s => s.StreamId == "event").CallCount);
        Assert.Equal(NativeNeowOfferV111.Predict(seed, true, true).EventRng,
            NativeRngV111.ToSnapshot(Assert.Single(started.NativeEventRng!.Streams)));
        var sourceStartupStream = NativeRngV111.CreateBundle(seed);
        var expectedBags = NativeUpFrontRelicBagsV111.Populate(
            sourceStartupStream);
        Assert.NotNull(started.NativeStartupRelicBags);
        Assert.Equal(expectedBags.AfterPlayerBag,
            started.NativeStartupRelicBags!.AfterPlayerBag);
        if (region == ActIdentity.Overgrowth)
        {
            var expectedRooms = NativeUpFrontRoomsV111.Generate(sourceStartupStream, 10);
            Assert.NotNull(started.NativeStartupRooms);
            Assert.Equal(expectedRooms.FinalRng, started.NativeStartupRooms!.FinalRng);
            Assert.Equal(412UL, started.NativeRunRng!.Streams.Single(
                stream => stream.StreamId == "run.up_front").CallCount);
        }
        else
        {
            // Underdocks has a distinct source room catalogue and retains
            // the relic bag prefix until its own translation is implemented.
            Assert.Null(started.NativeStartupRooms);
            Assert.Equal(229UL, started.NativeRunRng!.Streams.Single(
                stream => stream.StreamId == "run.up_front").CallCount);
        }
        Assert.Equal(sourceStartupStream.Streams
            .Select(NativeRngV111.ToSnapshot),
            started.NativeRunRng.Streams.Select(NativeRngV111.ToSnapshot));

        var snapshot = RunSnapshot.Load(RunSnapshot.Save(started));
        Assert.Equal(CanonicalJson.Sha256(started), CanonicalJson.Sha256(snapshot));
        Assert.NotSame(started.NativeMapRng, snapshot.NativeMapRng);
        Assert.NotSame(started.NativeMapRng, started.Fork().NativeMapRng);
        Assert.NotSame(started.NativeStartupRelicBags,
            snapshot.NativeStartupRelicBags);
        Assert.NotSame(started.NativeStartupRelicBags!.SharedBag.Common,
            snapshot.NativeStartupRelicBags!.SharedBag.Common);
        Assert.NotSame(started.NativeStartupRelicBags,
            started.Fork().NativeStartupRelicBags);
        if (region == ActIdentity.Overgrowth)
        {
            Assert.NotSame(started.NativeStartupRooms, snapshot.NativeStartupRooms);
            Assert.NotSame(started.NativeStartupRooms!.Acts[0].Events,
                snapshot.NativeStartupRooms!.Acts[0].Events);
            Assert.NotSame(started.NativeStartupRooms,
                started.Fork().NativeStartupRooms);
        }
        else Assert.Null(snapshot.NativeStartupRooms);
        RunConfiguration.ValidateState(snapshot);
    }

    [Fact]
    public void OrdinaryAndNeowOnlyProfilesRetainPrototypeMapBehavior()
    {
        const string seed = "native-map-opt-in-isolation";
        var engine = new PrototypeGameEngine();
        foreach (var fidelity in new[]
        {
            "v111-mechanics-prototype-rng-v1",
            V111Build.NativeNeowOpeningFidelityId
        })
        {
            var root = V111RunFactory.Create(seed,
                RunConfiguration.Create("silent", 10) with
                {
                    FidelityId = fidelity
                });
            var started = engine.Step(root, engine.GetLegalActions(root).Single()).State;
            Assert.Null(started.NativeMapRng);
            Assert.Equal(PrototypeNativeOvergrowthMap.GenerationProfileId,
                started.World!.Map.GenerationProfileId);
        }
    }
}
