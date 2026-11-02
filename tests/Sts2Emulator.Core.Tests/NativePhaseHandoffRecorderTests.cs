using System.Text.Json;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativePhaseHandoffRecorderTests
{
    private static async Task<JsonElement[]> Capture(
        Action<NativeActionEnvelopeRecorder> record)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "sts2-phase-handoff-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeActionEnvelopeRecorder(
                path, "v111;sha256=synthetic", "synthetic-run");
            record(recorder);
            recorder.Finish("Combat ended", null);
            await recorder.DisposeAsync();
            return File.ReadAllLines(path)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .ToArray();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".incomplete")) File.Delete(path + ".incomplete");
        }
    }

    [Fact]
    public async Task NativeCallbackEventsAreRecordedWithoutSettlementPromotion()
    {
        var records = await Capture(observer =>
        {
            observer.NativePhaseHandoff(
                "ABOUT_TO_SWITCH_TO_ENEMY_TURN", 1, "Player", "End",
                "NotPlayPhase", false, true, false);
            observer.NativePhaseHandoff(
                "SIDE_SWITCHED", 1, "Enemy", "End",
                "NotPlayPhase", false, true, false);
            observer.NativePhaseHandoff(
                "TURN_STARTED", 1, "Enemy", "None",
                "NotPlayPhase", false, false, true);
        });
        var events = records.Where(row =>
            row.GetProperty("type").GetString() == "native_phase_handoff").ToArray();
        Assert.Equal(3, events.Length);
        Assert.Equal(new[] {
            "ABOUT_TO_SWITCH_TO_ENEMY_TURN", "SIDE_SWITCHED", "TURN_STARTED"
        }, events.Select(row => row.GetProperty("data")
            .GetProperty("source_event").GetString()).ToArray());
        Assert.Equal("CombatManager.TurnEnded",
            events[1].GetProperty("data").GetProperty("source_callback").GetString());
        Assert.True(events[1].GetProperty("data")
            .GetProperty("ending_player_turn_phase_two").GetBoolean());
        Assert.All(events, row =>
        {
            var data = row.GetProperty("data");
            Assert.Equal("SETTLEMENT_UNVERIFIED",
                data.GetProperty("settlement_status").GetString());
            Assert.False(data.GetProperty("native_phase_handoff_order_verified")
                .GetBoolean());
            Assert.Equal(0, data.GetProperty("rng_draws_performed_by_inspector")
                .GetInt32());
        });
        Assert.Equal("OBSERVED", records[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task UnsupportedNativeEventProducesIncompleteCapture()
    {
        var records = await Capture(observer =>
            observer.NativePhaseHandoff(
                "UNVERIFIED", 1, "Player", "Play", "PlayPhase",
                false, false, false));
        Assert.Contains(records, row =>
            row.GetProperty("type").GetString() == "observation_failure");
        Assert.Equal("INCOMPLETE_CAPTURE",
            records[^1].GetProperty("data").GetProperty("status").GetString());
    }
}
