using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// Source pool identities/rarities and the two RelicGrabBag.Populate steps.
/// The shared enumerable overload retains Ancient and Event buckets, unlike
/// the player's rarity-filtered overload. Native three-act room queues now
/// independently verify the resulting 230-draw prefix for the pinned seed.
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

        Assert.Equal(118, result.SharedBag.TotalCount);
        Assert.Equal(122, result.PlayerBag.TotalCount);
        Assert.Equal(112, result.AfterSharedBag.Counter);
        Assert.Equal(230, result.AfterPlayerBag.Counter);
        Assert.Equal(result.AfterSharedBag, again.AfterSharedBag);
        Assert.Equal(result.AfterPlayerBag, again.AfterPlayerBag);
        Assert.Equal(result.SharedBag.Common, again.SharedBag.Common);
        Assert.Equal(result.SharedBag.Uncommon, again.SharedBag.Uncommon);
        Assert.Equal(result.SharedBag.Rare, again.SharedBag.Rare);
        Assert.Equal(result.SharedBag.Shop, again.SharedBag.Shop);
        Assert.Equal(result.SharedBag.Event, again.SharedBag.Event);
        Assert.Equal(result.SharedBag.Ancient, again.SharedBag.Ancient);
        Assert.Equal(result.PlayerBag.Common, again.PlayerBag.Common);
        Assert.Equal(result.PlayerBag.Uncommon, again.PlayerBag.Uncommon);
        Assert.Equal(result.PlayerBag.Rare, again.PlayerBag.Rare);
        Assert.Equal(result.PlayerBag.Shop, again.PlayerBag.Shop);
        Assert.Empty(result.PlayerBag.Event);
        Assert.Empty(result.PlayerBag.Ancient);
        Assert.Equal(["FresnelLens"], result.SharedBag.Event);
        Assert.Equal(["LoomingFruit", "VeryHotCocoa"].Order(StringComparer.Ordinal),
            result.SharedBag.Ancient.Order(StringComparer.Ordinal));

        static string[] All(NativeUpFrontRelicBagsV111.Bag bag) =>
            bag.Common.Concat(bag.Uncommon).Concat(bag.Rare)
                .Concat(bag.Shop).Concat(bag.Event).Concat(bag.Ancient).ToArray();
        var shared = All(result.SharedBag);
        var personal = All(result.PlayerBag);
        Assert.Equal(shared.Length,
            shared.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(personal.Length,
            personal.Distinct(StringComparer.Ordinal).Count());
        Assert.All(shared.Where(id => id is not
            ("FresnelLens" or "LoomingFruit" or "VeryHotCocoa")),
            id => Assert.Contains(id, personal));
        Assert.Contains("HelicalDart", result.PlayerBag.Rare);
        Assert.Contains("SneckoSkull", result.PlayerBag.Common);
        Assert.DoesNotContain("RingOfTheSnake", personal);
        Assert.Contains("FresnelLens", shared);
        Assert.Contains("LoomingFruit", shared);
        Assert.Contains("VeryHotCocoa", shared);
        Assert.DoesNotContain("FresnelLens", personal);
        Assert.DoesNotContain("LoomingFruit", personal);
        Assert.DoesNotContain("VeryHotCocoa", personal);

        // Independent recurrence check at the 230-draw source prefix,
        // including the shared Ancient-rarity Fisher-Yates swap.
        var independent = NativeRngV111.CreateBundle(seed);
        for (var index = 0; index < 230; index++)
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
        Assert.Equal(230, first.AfterPlayerBag.Counter);
        Assert.Equal(230, second.AfterPlayerBag.Counter);
    }
}
