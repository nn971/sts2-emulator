using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Source pool identities/rarities and the two RelicGrabBag.Populate steps.
/// No assertion of final UpFront run-start parity: native GenerateRooms
/// follows this prefix and is still unimplemented.
/// </summary>
public sealed class NativeUpFrontRelicBagsV111Tests
{
    [Fact]
    public void PinnedFullUnlockRelicBagsConsumeExactlySourceShufflePrefix()
    {
        const string seed = "6JK8MD0VQQTA";
        var before = NativeRngV111.CreateBundle(seed);
        var run = before.Fork();
        var result = NativeUpFrontRelicBagsV111.Populate(run);
        var again = NativeUpFrontRelicBagsV111.Populate(before.Fork());

        Assert.Equal(115, result.SharedBag.TotalCount);
        Assert.Equal(122, result.PlayerBag.TotalCount);
        Assert.Equal(111, result.AfterSharedBag.Counter);
        Assert.Equal(229, result.AfterPlayerBag.Counter);
        Assert.Equal(result.AfterSharedBag, again.AfterSharedBag);
        Assert.Equal(result.AfterPlayerBag, again.AfterPlayerBag);
        Assert.Equal(result.SharedBag.Common, again.SharedBag.Common);
        Assert.Equal(result.SharedBag.Uncommon, again.SharedBag.Uncommon);
        Assert.Equal(result.SharedBag.Rare, again.SharedBag.Rare);
        Assert.Equal(result.SharedBag.Shop, again.SharedBag.Shop);
        Assert.Equal(result.PlayerBag.Common, again.PlayerBag.Common);
        Assert.Equal(result.PlayerBag.Uncommon, again.PlayerBag.Uncommon);
        Assert.Equal(result.PlayerBag.Rare, again.PlayerBag.Rare);
        Assert.Equal(result.PlayerBag.Shop, again.PlayerBag.Shop);

        static string[] All(NativeUpFrontRelicBagsV111.Bag bag) =>
            bag.Common.Concat(bag.Uncommon).Concat(bag.Rare)
                .Concat(bag.Shop).ToArray();
        var shared = All(result.SharedBag);
        var personal = All(result.PlayerBag);
        Assert.Equal(shared.Length,
            shared.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(personal.Length,
            personal.Distinct(StringComparer.Ordinal).Count());
        Assert.All(shared, id => Assert.Contains(id, personal));
        Assert.Contains("HelicalDart", result.PlayerBag.Rare);
        Assert.Contains("SneckoSkull", result.PlayerBag.Common);
        Assert.DoesNotContain("RingOfTheSnake", personal);
        Assert.DoesNotContain("FresnelLens", shared);
        Assert.DoesNotContain("LoomingFruit", shared);
        Assert.DoesNotContain("VeryHotCocoa", shared);

        // Independent recurrence check at the 229-draw prefix, without
        // asserting that source pre-generated Acts have been simulated.
        var independent = NativeRngV111.CreateBundle(seed);
        for (var index = 0; index < 229; index++)
            _ = NativeRngV111.NextUInt64(independent, "run.up_front");
        Assert.Equal(result.AfterPlayerBag,
            NativeRngV111.ToSnapshot(independent.Streams.Single(
                stream => stream.StreamId == "run.up_front")));
        foreach (var stream in before.Streams.Where(
            stream => stream.StreamId != "run.up_front"))
            Assert.Equal(NativeRngV111.ToSnapshot(stream),
                NativeRngV111.ToSnapshot(run.Streams.Single(
                    other => other.StreamId == stream.StreamId)));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentBagOrderWithoutChangingPoolMembership()
    {
        var first = NativeUpFrontRelicBagsV111.Populate(
            NativeRngV111.CreateBundle("upfront-source-bag-one"));
        var second = NativeUpFrontRelicBagsV111.Populate(
            NativeRngV111.CreateBundle("upfront-source-bag-two"));
        Assert.NotEqual(first.SharedBag.Common, second.SharedBag.Common);
        Assert.Equal(first.SharedBag.Common.Order(StringComparer.Ordinal),
            second.SharedBag.Common.Order(StringComparer.Ordinal));
        Assert.Equal(first.PlayerBag.Rare.Order(StringComparer.Ordinal),
            second.PlayerBag.Rare.Order(StringComparer.Ordinal));
        Assert.Equal(229, first.AfterPlayerBag.Counter);
        Assert.Equal(229, second.AfterPlayerBag.Counter);
    }
}
