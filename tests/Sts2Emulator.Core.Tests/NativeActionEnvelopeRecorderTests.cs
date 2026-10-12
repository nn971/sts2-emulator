using System.Text.Json;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeActionEnvelopeRecorderTests
{
    private static JsonElement State(int energy) =>
        JsonSerializer.SerializeToElement(new
        {
            player = new { hp = 50 },
            combat = new { energy }
        });

    private static async Task<JsonElement[]> Record(
        Action<NativeActionEnvelopeRecorder> action)
    {
        string file = Path.Combine(Path.GetTempPath(),
            "sts2-action-envelope-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeActionEnvelopeRecorder(
                file, "synthetic-sha", "synthetic-run");
            action(recorder);
            await recorder.DisposeAsync();
            return File.ReadAllLines(file)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .ToArray();
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
            if (File.Exists(file + ".incomplete")) File.Delete(file + ".incomplete");
        }
    }

    [Fact]
    public async Task AllExecutorActionsSurviveUnsupportedNestedPlayerChoice()
    {
        object play = new();
        object automatic = new();
        var entries = await Record(recorder =>
        {
            recorder.Start(play, "PlayCardAction", State(3));
            recorder.HistoryEntry(0, "energy_spent",
                JsonSerializer.SerializeToElement(new { amount = 1 }),
                "creature:0", null, []);
            recorder.ChoicePause(play, State(2));
            recorder.Start(automatic, "AutomaticAction", State(2));
            recorder.Complete(automatic, State(2));
            recorder.Complete(play, State(2));
            recorder.Finish("Combat ended", State(2));
        });
        Assert.Equal("header", entries[0].GetProperty("type").GetString());
        Assert.Equal(Enumerable.Range(1, entries.Length).Select(i => (long)i),
            entries.Select(entry => entry.GetProperty("ordinal").GetInt64()));
        var started = entries.Where(x =>
            x.GetProperty("type").GetString() == "action_started").ToArray();
        Assert.Equal(2, started.Length);
        Assert.Equal(1L,
            started[1].GetProperty("parent_action_id").GetInt64());
        Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "history_entry");
        Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "nested_choice_pause");
        Assert.Equal("OBSERVED",
            entries[^1].GetProperty("data").GetProperty("status").GetString());
        Assert.Equal("NOT_TESTED", entries[^1].GetProperty("data")
            .GetProperty("native_game_parity").GetString());
    }

    [Fact]
    public async Task ChangedFrameSnapshotsDeduplicateAndKeepChronology()
    {
        var entries = await Record(recorder =>
        {
            recorder.ObserveFrame(State(3));
            recorder.ObserveFrame(State(3));
            recorder.ObserveFrame(State(2));
            recorder.Finish("Combat ended", State(2));
        });
        var frames = entries.Where(x =>
            x.GetProperty("type").GetString() == "frame_state").ToArray();
        Assert.Equal(2, frames.Length);
        Assert.Equal(1, frames[0].GetProperty("data")
            .GetProperty("frame_index").GetInt64());
        Assert.Equal(3, frames[1].GetProperty("data")
            .GetProperty("frame_index").GetInt64());
        Assert.Equal(2, frames[1].GetProperty("state")
            .GetProperty("combat").GetProperty("energy").GetInt32());
    }

    [Fact]
    public async Task TerminalWithUnfinishedActionIsExplicitlyIncomplete()
    {
        var entries = await Record(recorder =>
        {
            recorder.Start(new object(), "LongRunningAction", State(2));
            recorder.Finish("Combat closed", State(2));
        });
        var terminal = entries[^1].GetProperty("data");
        Assert.Equal("INCOMPLETE_CAPTURE",
            terminal.GetProperty("status").GetString());
        Assert.Equal(1, terminal.GetProperty("open_actions").GetArrayLength());
    }

    [Fact]
    public async Task OrphanCompletionIsRecordedAsIncompleteAnomaly()
    {
        var entries = await Record(recorder =>
        {
            recorder.Complete(new object(), State(2));
            recorder.Finish("Combat ended", State(2));
        });
        Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "anomaly");
        Assert.Equal("INCOMPLETE_CAPTURE", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task MissingSnapshotIsAnExplicitObservationFailure()
    {
        var entries = await Record(recorder =>
        {
            object a = new();
            recorder.Start(a, "PlayCardAction", null);
            recorder.Complete(a, State(2));
            recorder.Finish("Combat ended", State(2));
        });
        Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "observation_failure");
        Assert.Equal("INCOMPLETE_CAPTURE", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }
}
