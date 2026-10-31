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
    public async Task SourceRunAndPlayerRngWordsAreRecordedWithoutClaimingHistory()
    {
        var streams = new NativeRunRngStreamView[]
        {
            new("run", "UpFront", 12, 1, 2, 3, 4),
            new("player", "Rewards", 0, ulong.MaxValue, 7, 8, 9)
        };
        var entries = await Record(recorder =>
        {
            recorder.RunRngSnapshot("run_started", 123UL, streams);
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                3, 1, "Monster", "MAWLER", 70, 99);
            recorder.RunRngSnapshot("room_entered", 123UL, streams);
            recorder.RunRngSnapshot("combat_attach", 123UL, streams);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            recorder.Finish("First observed combat");
        });
        var rng = entries.Where(x =>
            x.GetProperty("type").GetString() == "run_rng_snapshot").ToArray();
        Assert.Equal(3, rng.Length);
        var initial = rng[0].GetProperty("data");
        Assert.Equal("run_started", initial.GetProperty("site").GetString());
        Assert.Equal("RunRngSet.ToSerializable+PlayerRngSet.ToSerializable",
            initial.GetProperty("source").GetString());
        Assert.False(initial.GetProperty("source_rng_consumed_by_inspector")
            .GetBoolean());
        Assert.False(initial.GetProperty("native_rng_precombat_history_complete")
            .GetBoolean());
        Assert.Equal(2, initial.GetProperty("streams").GetArrayLength());
        var first = initial.GetProperty("streams")[0];
        Assert.Equal("player", first.GetProperty("scope").GetString());
        Assert.Equal(ulong.MaxValue, first.GetProperty("s0").GetUInt64());
        Assert.Equal(12, initial.GetProperty("streams")[1]
            .GetProperty("counter").GetInt32());
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            entries[^1].GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task DuplicateOrInvalidSourceStreamDoesNotProduceRngEvidence()
    {
        var rows = await Record(recorder =>
        {
            recorder.RunRngSnapshot("run_started", 123UL,
            [
                new("run", "UpFront", 0, 1, 2, 3, 4),
                new("run", "UpFront", 0, 1, 2, 3, 4),
                new("player", "Rewards", 0, 1, 2, 3, 4)
            ]);
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2, 3, 1,
                "Monster", "MAWLER", 70, 99);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            recorder.Finish("First observed combat");
        });
        Assert.DoesNotContain(rows, x =>
            x.GetProperty("type").GetString() == "run_rng_snapshot");
        Assert.Single(rows, x =>
            x.GetProperty("type").GetString() == "observation_failure");
        Assert.Equal("INCOMPLETE_CAPTURE",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
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
    public async Task AwaitedRoomArrivalAndMapPostfixCanFollowCombatLink()
    {
        object mapMove = new();
        bool beforeArrival = true;
        bool afterArrival = true;
        bool afterQueueReturn = false;
        var rows = await Record(recorder =>
        {
            recorder.MapActionRequested(mapMove, "map_move", 0,
                null, null, 3, 1, null);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            beforeArrival = recorder.HasLinkedCombatRoomAndSettledMapRequests;
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2, 3, 1,
                "Monster", "MAWLER", 70, 99);
            afterArrival = recorder.HasLinkedCombatRoomAndSettledMapRequests;
            recorder.MapActionOutcome(mapMove, "Finished", 5);
            afterQueueReturn = recorder.HasLinkedCombatRoomAndSettledMapRequests;
            recorder.Finish("Late room arrival and queue return observed");
        });
        Assert.False(beforeArrival);
        Assert.False(afterArrival);
        Assert.True(afterQueueReturn);
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
        Assert.True(rows[^1].GetProperty("data")
            .GetProperty("linked_combat_room_arrival_observed").GetBoolean());
        Assert.Equal(0, rows[^1].GetProperty("data")
            .GetProperty("map_requests_missing_postfix").GetInt32());
    }

    [Fact]
    public async Task MissingAwaitedRoomArrivalFailsClosedEvenAfterCombatLink()
    {
        var rows = await Record(recorder =>
        {
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            recorder.Finish("Room callback not yet observed");
        });
        Assert.Equal("INCOMPLETE_CAPTURE",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
        Assert.False(rows[^1].GetProperty("data")
            .GetProperty("linked_combat_room_arrival_observed").GetBoolean());
    }

    [Fact]
    public async Task OpaquePrecombatQueueActionsAreTypedButNeverDecoded()
    {
        object action = new();
        var rows = await Record(recorder =>
        {
            recorder.OpaqueActionRequested(action, "Example.NativeMacroAction");
            recorder.MapActionOutcome(action, "Deferred", null);
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                3, 1, "Monster", "MAWLER", 70, 99);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            recorder.Finish("First combat");
        });
        var request = Assert.Single(rows, r =>
            r.GetProperty("type").GetString() == "opaque_action_request")
            .GetProperty("data");
        Assert.Equal("Example.NativeMacroAction",
            request.GetProperty("native_action_type").GetString());
        Assert.Equal("UNVERIFIED",
            request.GetProperty("human_origin").GetString());
        Assert.False(request.TryGetProperty("choice", out _));
        var outcome = Assert.Single(rows, r =>
            r.GetProperty("type").GetString() == "opaque_action_outcome")
            .GetProperty("data");
        Assert.Equal(request.GetProperty("request_id").GetInt64(),
            outcome.GetProperty("request_id").GetInt64());
        Assert.Equal("Deferred",
            outcome.GetProperty("native_action_state").GetString());
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(0, rows[^1].GetProperty("data")
            .GetProperty("opaque_requests_missing_postfix").GetInt32());
    }

    [Fact]
    public async Task PendingOpaquePostfixPreventsPrematurePreludeClose()
    {
        object action = new();
        bool settledBefore = true;
        bool settledAfter = false;
        var rows = await Record(recorder =>
        {
            recorder.OpaqueActionRequested(action, "Example.NativeMacroAction");
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                3, 1, "Monster", "MAWLER", 70, 99);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            settledBefore = recorder.HasLinkedCombatRoomAndSettledMapRequests;
            recorder.MapActionOutcome(action, "Finished", 15);
            settledAfter = recorder.HasLinkedCombatRoomAndSettledMapRequests;
            recorder.Finish("All queue returns recorded");
        });
        Assert.False(settledBefore);
        Assert.True(settledAfter);
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task SourceEventChoiceCapturesNativeOfferAndHandlerReturn()
    {
        object token = new();
        var rows = await Record(recorder =>
        {
            recorder.EventChoiceRequested(token, "NEOW", 7, 1,
            [
                new("arcane_scroll", false, false, "ARCANE_SCROLL"),
                new("hefty_tablet", false, false, "HEFTY_TABLET"),
                new("other", true, false, null)
            ]);
            recorder.EventChoiceReturned(token);
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                3, 1, "Monster", "MAWLER", 70, 99);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            recorder.Finish("First combat");
        });
        var offer = Assert.Single(rows, r =>
            r.GetProperty("type").GetString() == "event_choice_request")
            .GetProperty("data");
        Assert.Equal("NEOW", offer.GetProperty("event_model_id").GetString());
        Assert.Equal(1, offer.GetProperty("selected_index").GetInt32());
        Assert.Equal("hefty_tablet", offer.GetProperty("selected_text_key").GetString());
        Assert.Equal(3, offer.GetProperty("options").GetArrayLength());
        Assert.Equal("HEFTY_TABLET", offer.GetProperty("options")[1]
            .GetProperty("relic_model_id").GetString());
        Assert.False(offer.GetProperty("effects_settled").GetBoolean());
        var returned = Assert.Single(rows, r =>
            r.GetProperty("type").GetString() == "event_choice_return")
            .GetProperty("data");
        Assert.Equal(offer.GetProperty("request_id").GetInt64(),
            returned.GetProperty("request_id").GetInt64());
        Assert.False(returned.GetProperty("option_effects_settled").GetBoolean());
        Assert.Equal(0, rows[^1].GetProperty("data")
            .GetProperty("event_choice_requests_missing_postfix").GetInt32());
        Assert.Equal("SOURCE_PRELUDE_OBSERVED",
            rows[^1].GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task PendingEventHandlerReturnKeepsPreludeIncomplete()
    {
        object token = new();
        bool canFinish = true;
        var rows = await Record(recorder =>
        {
            recorder.EventChoiceRequested(token, "NEOW", 7, 0,
                [new("hefty_tablet", false, false, "HEFTY_TABLET")]);
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                3, 1, "Monster", "MAWLER", 70, 99);
            recorder.CombatLink("combat-uuid", "passive.jsonl", 0, 3, 1);
            canFinish = recorder.HasLinkedCombatRoomAndSettledMapRequests;
            recorder.Finish("Unreturned event hook");
        });
        Assert.False(canFinish);
        Assert.Equal(1, rows[^1].GetProperty("data")
            .GetProperty("event_choice_requests_missing_postfix").GetInt32());
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
            recorder.RoomEntered(0, "OVERGROWTH", 2, 2,
                2, 1, "Monster", "MAWLER", 70, 99);
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
