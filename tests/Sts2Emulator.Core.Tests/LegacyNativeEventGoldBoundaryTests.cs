using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class LegacyNativeEventGoldBoundaryTests
{
    [Theory]
    [InlineData("proto.native.event.dense_vegetation", 61, 39)]
    [InlineData("proto.native.event.whispering_hollow", 26, 19)]
    [InlineData("proto.native.event.sunken_statue", 101, 21)]
    [InlineData("proto.native.event.luminous_choir", 100, 50)]
    [InlineData("proto.native.event.jungle_maze_adventure", 135, 30)]
    [InlineData("proto.native.underdocks.sunken_treasury", 52, 16)]
    [InlineData("proto.native.underdocks.sunken_treasury", 303, 61)]
    [InlineData("proto.native.event.punch_off", 91, 8)]
    public void EventGoldUsesBoundedIntegerTicketAndRetainsGoldOffset(
        string eventId, int minimum, int count)
    {
        var rng = PrototypeRng.CreateBundle(
            "h6-legacy-gold-" + eventId + "-" + minimum);
        using var journal = RandomBoundaryJournalV111.Begin();

        var gold = PrototypeGameEngine.ResolveLegacyEventGold(
            rng, eventId, "primary", minimum, count);

        var draw = Assert.Single(journal.Entries);
        Assert.Equal("event.generated_gold.index_draw", draw.Request.SiteId);
        Assert.Equal("event", draw.Request.StreamId);
        Assert.Equal($"event:{eventId}:gold:primary", draw.Request.OwnerId);
        Assert.Equal("bounded_integer", draw.Request.OperationKind);
        Assert.Equal(count, draw.Request.BoundedCount);
        Assert.Empty(draw.Request.OrderedCandidates);
        Assert.Equal("legacy-prototype-v0", draw.ProviderMode);
        Assert.Equal(minimum + draw.SelectedIndex, gold);
        Assert.InRange(gold, minimum, minimum + count - 1);
    }

    [Fact]
    public void SeparateEventGoldStagesConsumeTwoBoundedDraws()
    {
        const string eventId = "proto.native.underdocks.sunken_treasury";
        var rng = PrototypeRng.CreateBundle("h6-legacy-two-gold-draws");
        using var journal = RandomBoundaryJournalV111.Begin();

        var small = PrototypeGameEngine.ResolveLegacyEventGold(
            rng, eventId, "primary", 52, 16);
        var large = PrototypeGameEngine.ResolveLegacyEventGold(
            rng, eventId, "secondary", 303, 61);

        Assert.InRange(small, 52, 67);
        Assert.InRange(large, 303, 363);
        Assert.Equal(2, journal.Entries.Count);
        Assert.All(journal.Entries, draw =>
        {
            Assert.Equal("event.generated_gold.index_draw",
                draw.Request.SiteId);
            Assert.Equal("bounded_integer", draw.Request.OperationKind);
        });
        Assert.Equal("event:proto.native.underdocks.sunken_treasury:gold:primary",
            journal.Entries[0].Request.OwnerId);
        Assert.Equal("event:proto.native.underdocks.sunken_treasury:gold:secondary",
            journal.Entries[1].Request.OwnerId);
    }
}
