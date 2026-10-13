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
    public async Task NativeChoiceResumeKeepsTheSameActionIdentity()
    {
        object action = new();
        var entries = await Record(recorder =>
        {
            recorder.Start(action, "PlayCardAction", State(3));
            recorder.ChoicePause(action, State(2));
            // Native BeforeActionExecuted fires again on this same object.
            recorder.Start(action, "PlayCardAction", State(2));
            recorder.Complete(action, State(2));
            recorder.HistoryReset(12, 0);
            recorder.Finish("Combat ended", State(2));
        });
        Assert.Single(entries, x => x.GetProperty("type").GetString() == "action_started");
        Assert.Single(entries, x => x.GetProperty("type").GetString() == "action_resumed");
        Assert.DoesNotContain(entries, x => x.GetProperty("type").GetString() == "anomaly");
        var resumed = Assert.Single(entries,
            x => x.GetProperty("type").GetString() == "action_resumed");
        Assert.Equal(1, resumed.GetProperty("action_id").GetInt64());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task DuplicateBeforeWithoutChoiceStillFailsClosed()
    {
        object action = new();
        var entries = await Record(recorder =>
        {
            recorder.Start(action, "PlayCardAction", State(3));
            recorder.Start(action, "PlayCardAction", State(3));
            recorder.Complete(action, State(2));
            recorder.Finish("Combat ended", State(2));
        });
        Assert.Single(entries, x => x.GetProperty("type").GetString() == "anomaly");
        Assert.Equal("INCOMPLETE_CAPTURE", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task NativeRngBoundaryIsSourceAttributedAndConsumesNoAdditionalRng()
    {
        var before = new Sts2Emulator.Core.NativeRngSnapshot(7, 1, 2, 3, 4);
        var after = new Sts2Emulator.Core.NativeRngSnapshot(8, 5, 6, 7, 8);
        var entries = await Record(recorder =>
        {
            var action = new object();
            recorder.Start(action, "NativeAction", State(3));
            recorder.NativeRngBoundary(new(
                "combat.committed_intent.index_draw", "run.monster_ai",
                "creature:2", 0, 3, 1, before, after));
            recorder.Complete(action, State(3));
            recorder.Finish("Combat ended", State(3));
        });
        var rng = Assert.Single(entries, e =>
            e.GetProperty("type").GetString() == "native_rng_boundary");
        Assert.Equal("Rng.NextInt", rng.GetProperty("native_type").GetString());
        Assert.Equal(1, rng.GetProperty("action_id").GetInt64());
        var data = rng.GetProperty("data");
        Assert.Equal(3, data.GetProperty("bounded_count").GetInt32());
        Assert.Equal(1, data.GetProperty("selected_index").GetInt32());
        Assert.True(data.GetProperty("attributed").GetBoolean());
        Assert.Equal("source_primitive_hook",
            data.GetProperty("origin").GetString());
        Assert.True(data.GetProperty("source_hook_observed").GetBoolean());
        Assert.Equal(7, data.GetProperty("cursor_before")
            .GetProperty("counter").GetInt32());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task InvalidOrFrameInferredNativeRngCallsAreRejected()
    {
        var before = new Sts2Emulator.Core.NativeRngSnapshot(7, 1, 2, 3, 4);
        var same = before;
        var entries = await Record(recorder =>
        {
            Assert.Throws<ArgumentException>(() =>
                recorder.NativeRngBoundary(new(
                    "UNATTRIBUTED", "run.niche", "creature:1",
                    0, 1, 0, before, same)));
            Assert.Throws<ArgumentException>(() =>
                recorder.NativeRngBoundary(new(
                    "combat.initial_unique_hp", "run.niche", "creature:1",
                    0, 1, 0, before,
                    new Sts2Emulator.Core.NativeRngSnapshot(8, 5, 6, 7, 8),
                    Origin: "rendered_frame_poll")));
            recorder.Finish("Combat ended", State(3));
        });
        Assert.DoesNotContain(entries, e =>
            e.GetProperty("type").GetString() == "native_rng_boundary");
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
