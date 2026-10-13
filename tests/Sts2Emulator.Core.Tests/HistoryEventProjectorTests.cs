using Sts2ReferenceMod.Capture;

namespace Sts2Emulator.Core.Tests;

public sealed class HistoryEventProjectorTests
{
    private sealed record DamageResult(decimal UnblockedDamage);
    private sealed record Actor(string Id);
    private sealed record DamageReceivedEntry(DamageResult Result, Actor Dealer, Actor Receiver);
    private sealed record BlockGainedEntry(decimal Amount, Actor Receiver);
    private sealed record UnknownNativeHistoryEntry(int Delta);
    private sealed record EnergySpentEntry();

    [Fact]
    public void DamageHistoryCapturesOnlyDeclaredScalarAndIdentity()
    {
        var entry = new DamageReceivedEntry(new DamageResult(7m), new Actor("player:1"),
            new Actor("creature:2"));
        var effect = HistoryEventProjector.Project(entry, 4, obj => ((Actor)obj).Id);

        Assert.Equal("damage", effect.Kind);
        Assert.Equal("player:1", effect.ActorId);
        Assert.Equal("creature:2", effect.TargetId);
        Assert.Empty(effect.MissingFields);
        Assert.Equal(7m, effect.Payload.GetProperty("result_unblocked_damage").GetDecimal());
        Assert.Equal(4, effect.Payload.GetProperty("history_index").GetInt64());
    }

    [Fact]
    public void MissingNativeFieldIsExplicitlyReported()
    {
        var effect = HistoryEventProjector.Project(new EnergySpentEntry(), 0, _ => null);
        Assert.Contains("Amount", effect.MissingFields);
        Assert.False(effect.Payload.TryGetProperty("amount", out _));
    }

    [Fact]
    public void UnknownEventIsVisibleButNotInvented()
    {
        var effect = HistoryEventProjector.Project(new UnknownNativeHistoryEntry(5), 3, _ => null);
        Assert.Equal("native_history_entry", effect.Kind);
        Assert.Equal("UnknownNativeHistoryEntry", effect.Payload.GetProperty("entry_type").GetString());
        Assert.False(effect.Payload.TryGetProperty("delta", out _));
    }

    [Fact]
    public void BlockGainedTracksAmountAndTarget()
    {
        var effect = HistoryEventProjector.Project(
            new BlockGainedEntry(5m, new Actor("creature:7")), 6, obj => ((Actor)obj).Id);
        Assert.Equal("block_gained", effect.Kind);
        Assert.Equal(5m, effect.Payload.GetProperty("amount").GetDecimal());
        Assert.Equal("creature:7", effect.TargetId);
    }
}
