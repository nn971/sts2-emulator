using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class LegacyFisherYatesRegisteredSiteTests
{
    // These sites are registered as legacy caller-bound bounded_integer
    // shuffles. The generic shuffle primitive must admit them through its
    // legacy wrapper without advertising them as native RNG boundaries.
    [Theory]
    [InlineData("event.reward.card_bundle.shuffle_swap_index", "reward")]
    [InlineData("relic.sand_castle.acquisition_upgrade.shuffle_swap_index", "event")]
    [InlineData("run.random_pool_distinct.shuffle_swap_index", "reward")]
    [InlineData("shop.potion_offer.shuffle_swap_index", "shop")]
    [InlineData("shop.relic_offer.shuffle_swap_index", "shop")]
    public void RegisteredLegacyShuffleRetainsBoundedTicketsAndStream(
        string siteId, string streamId)
    {
        var rng = PrototypeRng.CreateBundle("h6-legacy-shuffle-" + siteId);
        var rngAgain = PrototypeRng.CreateBundle("h6-legacy-shuffle-" + siteId);
        int[] values = [10, 20, 30, 40];
        int[] replay = (int[])values.Clone();

        using (var journal = RandomBoundaryJournalV111.Begin())
        {
            var decisions = LegacyFisherYatesV111.ShuffleInPlace(
                values, rng, streamId, siteId, "h6:legacy:shuffle");
            Assert.Equal(3, decisions.Length);
            Assert.Equal(3, journal.Entries.Count);
            for (var i = 0; i < decisions.Length; i++)
            {
                var entry = journal.Entries[i];
                Assert.Equal(siteId, entry.Request.SiteId);
                Assert.Equal(streamId, entry.Request.StreamId);
                Assert.Equal("h6:legacy:shuffle", entry.Request.OwnerId);
                Assert.Equal("bounded_integer", entry.Request.OperationKind);
                Assert.Equal(4 - i, entry.Request.BoundedCount);
                Assert.Empty(entry.Request.OrderedCandidates);
                Assert.Equal("legacy-prototype-v0", entry.ProviderMode);
                Assert.InRange(entry.SelectedIndex, 0, 3 - i);
            }
        }

        LegacyFisherYatesV111.ShuffleInPlace(
            replay, rngAgain, streamId, siteId, "h6:legacy:shuffle");
        Assert.Equal(values, replay);
        Assert.Equal(new[] { 10, 20, 30, 40 }, values.Order().ToArray());
        Assert.Equal(CanonicalJson.Sha256(rng), CanonicalJson.Sha256(rngAgain));
    }

    [Fact]
    public void LegacySitesStayOutsideNativeShuffleAuthorization()
    {
        const string siteId = "run.random_pool_distinct.shuffle_swap_index";
        var rng = PrototypeRng.CreateBundle("h6-native-boundary-negative");
        var values = new[] { 1, 2, 3 };
        var before = CanonicalJson.Sha256(rng);

        Assert.DoesNotContain(siteId,
            StochasticSiteRegistryV111.NativeFisherYatesSiteIds);
        Assert.Throws<NotSupportedException>(() =>
            NativeFisherYatesV111.ShuffleInPlace(
                values, rng, new NativeRandomIndexProviderV111(),
                "reward", "h6:negative", siteId));
        Assert.Equal(before, CanonicalJson.Sha256(rng));
        Assert.Equal(new[] { 1, 2, 3 }, values);
    }

    [Fact]
    public void LegacyWrapperRejectsRegisteredNonShuffleSite()
    {
        var rng = PrototypeRng.CreateBundle("h6-nonshuffle-negative");
        var values = new[] { 1, 2, 3 };
        Assert.Throws<NotSupportedException>(() =>
            LegacyFisherYatesV111.ShuffleInPlace(
                values, rng, "event",
                "event.selection.weighted_ticket", "h6:negative"));
    }
}
