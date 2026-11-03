using System.Text.Json;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeAsyncTaskTrackerTests
{
    private static async Task<JsonElement[]> Capture(
        Action<NativeActionEnvelopeRecorder, NativeAsyncTaskTracker> test)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "sts2-task-lifecycle-" + Guid.NewGuid().ToString("N") + ".jsonl");
        await using (var recorder = new NativeActionEnvelopeRecorder(
            path, "v111;sha256=synthetic", "synthetic-combat",
            nativeAsyncTaskHookInstalled: true))
        {
            var tracker = new NativeAsyncTaskTracker(recorder);
            test(recorder, tracker);
            tracker.EndCombat();
            recorder.Finish("synthetic combat done", null);
        }
        var rows = File.ReadLines(path).Select(
            line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        File.Delete(path);
        return rows;
    }

    [Fact]
    public async Task PendingHookTaskIsObservedUntilSourceFrameReportsCompletion()
    {
        var source = new TaskCompletionSource<bool>();
        var rows = await Capture((_, tracker) =>
        {
            tracker.Register(source.Task, "HOOK_TASK_BODY",
                "HookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion");
            tracker.Poll();
            tracker.Checkpoint(1, "PLAYER_TURN_START");
            source.SetResult(true);
            tracker.Checkpoint(2, "PLAYER_TURN_START");
        });
        var lifecycle = rows.Where(r =>
            r.GetProperty("type").GetString() ==
                "native_async_task_lifecycle").ToArray();
        Assert.Equal(2, lifecycle.Length);
        Assert.Equal("registered",
            lifecycle[0].GetProperty("data").GetProperty("phase").GetString());
        Assert.Equal("PENDING",
            lifecycle[0].GetProperty("data").GetProperty("task_status").GetString());
        Assert.Equal("completed",
            lifecycle[1].GetProperty("data").GetProperty("phase").GetString());
        Assert.Equal("RAN_TO_COMPLETION",
            lifecycle[1].GetProperty("data").GetProperty("task_status").GetString());

        var snapshots = rows.Where(r =>
            r.GetProperty("type").GetString() ==
                "native_async_task_checkpoint").ToArray();
        Assert.Equal(3, snapshots.Length); // two player frames and teardown
        Assert.Equal(1, snapshots[0].GetProperty("data")
            .GetProperty("tracked_pending_task_count").GetInt32());
        Assert.Equal(0, snapshots[1].GetProperty("data")
            .GetProperty("tracked_pending_task_count").GetInt32());
        Assert.False(snapshots[1].GetProperty("data")
            .GetProperty("native_settlement_certified").GetBoolean());
        Assert.False(snapshots[1].GetProperty("data")
            .GetProperty("exhaustive_native_task_inventory").GetBoolean());
        Assert.Equal(0, snapshots[2].GetProperty("data")
            .GetProperty("tracked_pending_task_count").GetInt32());
    }

    [Fact]
    public async Task IdenticalReferenceIsCountedOnceAcrossResumedHooks()
    {
        var pending = new TaskCompletionSource();
        var rows = await Capture((_, tracker) =>
        {
            tracker.Register(pending.Task, "GAME_ACTION_BODY", "GameAction._executionTask");
            tracker.Register(pending.Task, "GAME_ACTION_BODY", "GameAction._executionTask");
            tracker.Register(pending.Task, "HOOK_TASK_BODY", "HookPlayerChoiceContext.Task");
            tracker.Checkpoint(1, "PLAYER_TURN_START");
            pending.TrySetResult();
            tracker.Checkpoint(2, "PLAYER_TURN_START");
            tracker.Register(pending.Task, "GAME_ACTION_BODY", "GameAction._executionTask");
            tracker.Poll();
        });
        Assert.Single(rows, r => r.GetProperty("type").GetString() ==
            "native_async_task_lifecycle" && r.GetProperty("data")
            .GetProperty("phase").GetString() == "registered");
        Assert.Single(rows, r => r.GetProperty("type").GetString() ==
            "native_async_task_lifecycle" && r.GetProperty("data")
            .GetProperty("phase").GetString() == "completed");
    }

    [Fact]
    public async Task UnfinishedTurnLoopRemainsPendingAtCombatTeardown()
    {
        var pending = new TaskCompletionSource();
        var rows = await Capture((_, tracker) =>
        {
            tracker.Register(pending.Task, "TURN_LOOP",
                "CombatManager.RunTurnLoopAfter");
            tracker.Checkpoint(1, "PLAYER_TURN_START");
        });
        var terminal = rows.Single(r => r.GetProperty("type").GetString() ==
            "native_async_task_checkpoint" &&
            r.GetProperty("data").GetProperty("site").GetString() == "COMBAT_TEARDOWN");
        Assert.Equal(1, terminal.GetProperty("data")
            .GetProperty("tracked_pending_task_count").GetInt32());
        Assert.Equal("NAMED_ASYNC_ENTRYPOINTS_ONLY",
            terminal.GetProperty("data").GetProperty("coverage").GetString());
        Assert.Equal("OBSERVED", rows.Last().GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task TaskFinishedBeforeFirstPollStillEmitsRegisterAndCompletion()
    {
        var rows = await Capture((_, tracker) =>
        {
            tracker.Register(Task.CompletedTask, "GAME_ACTION_STEP",
                "GameAction.Execute");
            tracker.Checkpoint(1, "PLAYER_TURN_START");
        });
        var events = rows.Where(r => r.GetProperty("type").GetString() ==
            "native_async_task_lifecycle").ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, r => Assert.Equal("RAN_TO_COMPLETION",
            r.GetProperty("data").GetProperty("task_status").GetString()));
        Assert.Equal("registered",
            events[0].GetProperty("data").GetProperty("phase").GetString());
        Assert.Equal("completed",
            events[1].GetProperty("data").GetProperty("phase").GetString());
    }
}
