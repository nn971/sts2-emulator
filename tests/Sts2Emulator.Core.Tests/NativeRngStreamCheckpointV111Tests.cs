using System.Text.Json;
using Sts2Emulator.Core;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRngStreamCheckpointV111Tests
{
    private static NativeRngStreamCheckpointV111 Checkpoint()
    {
        var streams = Enumerable.Range(0, 15).Select(i =>
            new NativeRngStreamCursorV111(
                (i < 12 ? "run." : "player.") + i,
                i < 12 ? "run" : "player:0",
                new NativeRngSnapshot(i, 1, 2, 3, 4))).ToArray();
        return new NativeRngStreamCheckpointV111("combat_attach", streams);
    }

    [Fact]
    public void ReadOnlyInventoryRequiresFifteenDistinctNativeSources()
    {
        var value = Checkpoint();
        value.Validate();
        Assert.Throws<ArgumentException>(() =>
            (value with { Streams = value.Streams[..14] }).Validate());
        var duplicate = value.Streams.ToArray();
        duplicate[1] = duplicate[0];
        Assert.Throws<ArgumentException>(() =>
            (value with { Streams = duplicate }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (value with { Phase = "rendered_frame_poll" }).Validate());
    }

    [Fact]
    public async Task SourceCheckpointIsInActionSidecarWithNoSampling()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "sts2-rng-streams-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeActionEnvelopeRecorder(path, "test", "run",
                nativeRngHookInstalled: true,
                nativeRngStreamCheckpointsEnabled: true);
            recorder.NativeRngStreamCheckpoint(Checkpoint());
            recorder.Finish("Combat ended", null);
            await recorder.DisposeAsync();
            var records = File.ReadAllLines(path).Select(
                row => JsonDocument.Parse(row).RootElement.Clone()).ToArray();
            Assert.True(records[0].GetProperty("data")
                .GetProperty("native_rng_stream_checkpoints_enabled").GetBoolean());
            var entry = Assert.Single(records, row => row.GetProperty("type")
                .GetString() == "native_rng_stream_checkpoint");
            var data = entry.GetProperty("data");
            Assert.Equal(15, data.GetProperty("streams").GetArrayLength());
            Assert.Equal(0, data.GetProperty("rng_draws_performed").GetInt32());
            Assert.False(data.GetProperty("complete_native_rng_inventory").GetBoolean());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".incomplete")) File.Delete(path + ".incomplete");
        }
    }
}
