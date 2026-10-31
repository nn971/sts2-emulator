using System.Text.Json;
using Sts2ReferenceMod.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class NativeRunPreludeRecorderTests
{
    private static async Task<JsonElement[]> Record(
        Action<NativeRunPreludeRecorder> action)
    {
        string file = Path.Combine(Path.GetTempPath(),
            "sts2-run-prelude-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var recorder = new NativeRunPreludeRecorder(
                file, "v111;sha256=synthetic", "source-run",
                "6JK8MD0VQQTA", 10, "SILENT", "OVERGROWTH", 1);
            action(recorder);
            await recorder.DisposeAsync();
            return File.ReadAllLines(file)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .ToArray();
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
            if (File.Exists(file + ".incomplete"))
                File.Delete(file + ".incomplete");
        }
    }

    [Fact]
    public async Task RunStartToMapMoveAndCombatLinkKeepsIndependentProvenance()
    {
        object move = new();
        object vote = new();
        var rows = await Record(recorder =>
        {
            recorder.RoomEntered(0, "OVERGROWTH", 0, 0,
                null, null, "Map", null, 70, 99);
            recorder.MapActionRequested(vote, "map_vote", 0,
                null, null, 3, 1, 1);
            recorder.MapActionOutcome(vote, "Queued", 5);
            recorder.MapActionRequested(move, "map_move", 0,
                null, null, 3, 1, null);
            recorder.MapActionOutcome(move, "Finished", 6);
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                3, 1, "Monster", "MAWLER", 70, 99);
            recorder.CombatLink("combat-uuid", "passive-combat-uuid.jsonl",
                0, 3, 1);
            recorder.Finish("First observed combat capture");
        });
        Assert.Equal(9, rows.Length);
        Assert.Equal(Enumerable.Range(1, rows.Length),
            rows.Select(x => x.GetProperty("ordinal").GetInt32()));
        Assert.All(rows, x => Assert.Equal("source-run",
            x.GetProperty("run_id").GetString()));
        var start = rows[0].GetProperty("data");
        Assert.Equal("native-run-prelude-v1",
            start.GetProperty("schema").GetString());
        Assert.Equal("RunManager.RunStarted(RunState)",
            start.GetProperty("source").GetString());
        Assert.Equal("6JK8MD0VQQTA",
            start.GetProperty("seed").GetString());
        Assert.Equal(10, start.GetProperty("ascension").GetInt32());
        Assert.False(start.GetProperty("source_macro_choices_complete").GetBoolean());
        var moveRequest = Assert.Single(rows, x =>
            x.GetProperty("type").GetString() == "map_action_request"
            && x.GetProperty("data").GetProperty("kind").GetString() == "map_move");
        Assert.Equal(3, moveRequest.GetProperty("data")
            .GetProperty("target_column").GetInt32());
        Assert.Equal("UNVERIFIED", moveRequest.GetProperty("data")
            .GetProperty("human_origin").GetString());
        var arrival = rows[^3].GetProperty("data");
        Assert.Equal("SOURCE_CURRENT_MAP_COORD",
            arrival.GetProperty("visited_destination").GetString());
        Assert.Equal("RunManager.RoomEntered",
            arrival.GetProperty("source").GetString());
        Assert.Equal(3, arrival.GetProperty("map_column").GetInt32());
        var link = rows[^2].GetProperty("data");
        Assert.Equal("combat-uuid",
            link.GetProperty("combat_run_id").GetString());
        Assert.Equal("NOT_CAPTURED",
            link.GetProperty("full_macro_action_transcript").GetString());
        var terminal = rows[^1].GetProperty("data");
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            terminal.GetProperty("status").GetString());
        Assert.False(terminal.GetProperty("source_macro_choices_complete")
            .GetBoolean());
        Assert.Equal("NOT_TESTED",
            terminal.GetProperty("emulator_bootstrap").GetString());
    }

    [Fact]
    public async Task InvalidMapRequestOrDuplicateNativeObjectFailsClosed()
    {
        object move = new();
        var rows = await Record(recorder =>
        {
            recorder.MapActionRequested(move, "map_move", 0,
                null, null, 3, 1, null);
            recorder.MapActionRequested(move, "map_move", 0,
                null, null, 3, 1, null);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            recorder.Finish("First combat");
        });
        Assert.Single(rows, x =>
            x.GetProperty("type").GetString() == "observation_failure");
        Assert.Equal("INCOMPLETE_CAPTURE",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task MissingCombatLinkIsNotReportedAsCompletePrelude()
    {
        var rows = await Record(recorder =>
            recorder.RoomEntered(0, "OVERGROWTH", 0, 0,
                null, null, "Map", null, 70, 99));
        Assert.Equal("NO_COMBAT_LINK",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CanceledVoteAndDeferredMapMoveRetainQueueUncertainty()
    {
        object vote = new();
        object move = new();
        var rows = await Record(recorder =>
        {
            recorder.MapActionRequested(vote, "map_vote", 0,
                3, 0, null, null, null);
            recorder.MapActionOutcome(vote, "None", null);
            recorder.MapActionRequested(move, "map_move", 0,
                3, 0, 2, 1, null);
            recorder.MapActionOutcome(move, "Deferred", null);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 2, 1);
            recorder.Finish("First combat");
        });
        Assert.Equal(2, rows.Count(x =>
            x.GetProperty("type").GetString() == "map_action_outcome"));
        Assert.Equal("NOT_OBSERVED",
            rows[2].GetProperty("data")
                .GetProperty("native_executor_completion").GetString());
        Assert.False(rows[4].GetProperty("data")
            .GetProperty("visited_room_proves_this_request").GetBoolean());
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
    }
}
