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
    public void NativeHandChoiceFlowBindsOnlyOneMatchingSourceReservation()
    {
        Assert.Equal("UNVERIFIED", NativeHandChoiceFlow.Current().Status);
        using (NativeHandChoiceFlow.BeginFromHand(11))
        {
            Assert.Equal("CHOICE_ID_NOT_YET_OBSERVED",
                NativeHandChoiceFlow.Current().Status);
            NativeHandChoiceFlow.Reserved(11, 4);
            var binding = NativeHandChoiceFlow.Current();
            Assert.Equal(11UL, binding.OwnerId);
            Assert.Equal(4U, binding.ChoiceId);
            Assert.Equal("SOURCE_ASYNC_FLOW_RESERVED_ID", binding.Status);
            using (NativeHandChoiceFlow.BeginFromHand(12))
            {
                NativeHandChoiceFlow.Reserved(12, 8);
                Assert.Equal(8U, NativeHandChoiceFlow.Current().ChoiceId);
                NativeHandChoiceFlow.Reserved(12, 9);
                Assert.Equal("AMBIGUOUS_SOURCE_RESERVATION",
                    NativeHandChoiceFlow.Current().Status);
                Assert.Null(NativeHandChoiceFlow.Current().ChoiceId);
            }
            Assert.Equal(4U, NativeHandChoiceFlow.Current().ChoiceId);
        }
        Assert.Equal("UNVERIFIED", NativeHandChoiceFlow.Current().Status);
        using (NativeHandChoiceFlow.BeginFromHand(11))
        {
            NativeHandChoiceFlow.Reserved(99, 3);
            Assert.Equal("AMBIGUOUS_SOURCE_RESERVATION",
                NativeHandChoiceFlow.Current().Status);
        }
    }

    [Fact]
    public async Task NativeHandChoiceFlowSurvivesCapturedAsyncContinuationAndRestoresCaller()
    {
        var ready = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        static async Task<NativeHandChoiceFlow.Binding> GetAfterAwait(
            Task wait)
        {
            await wait;
            return NativeHandChoiceFlow.Current();
        }
        Task<NativeHandChoiceFlow.Binding> continuation;
        using (NativeHandChoiceFlow.BeginFromHand(7))
        {
            NativeHandChoiceFlow.Reserved(7, 18);
            continuation = GetAfterAwait(ready.Task);
        }
        Assert.Equal("UNVERIFIED", NativeHandChoiceFlow.Current().Status);
        ready.SetResult(true);
        var binding = await continuation;
        Assert.Equal(7UL, binding.OwnerId);
        Assert.Equal(18U, binding.ChoiceId);
        Assert.Equal("SOURCE_ASYNC_FLOW_RESERVED_ID", binding.Status);
        Assert.Equal("UNVERIFIED", NativeHandChoiceFlow.Current().Status);
    }

    [Fact]
    public async Task NativeRequestAndExecutionRetainSameObjectIdentityAndUnknownAcceptance()
    {
        object action = new();
        var canonical = new Sts2Emulator.Core.GameAction("play_card",
            JsonSerializer.SerializeToElement(new
            {
                card_instance_id = 5L,
                target_id = (string?)null
            }));
        var entries = await Record(recorder =>
        {
            recorder.NativeRunIdentity("6JK8MD0VQQTA", 10);
            recorder.NativeInputRequest(action, "PlayCardAction", State(3),
                canonical);
            recorder.Start(action, "PlayCardAction", State(3), canonical);
            recorder.Complete(action, State(2));
            // Exercise the possible synchronous completion: even if the
            // original completes before the postfix, the source request
            // identity must still be available.
            recorder.NativeInputOutcome(action, "Finished", 5U);
            object deferred = new();
            recorder.NativeInputRequest(deferred, "PlayCardAction", State(2),
                canonical);
            recorder.NativeInputOutcome(deferred, "None", null);
            recorder.Finish("Combat ended", State(2));
        });
        var outcomes = entries.Where(x =>
            x.GetProperty("type").GetString() == "native_input_outcome").ToArray();
        Assert.Equal(2, outcomes.Length);
        Assert.Equal("ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN",
            outcomes[0].GetProperty("data").GetProperty("queue_result").GetString());
        Assert.Equal("DEFERRED_OR_UNENQUEUED",
            outcomes[1].GetProperty("data").GetProperty("queue_result").GetString());
        var identity = Assert.Single(entries,
            x => x.GetProperty("type").GetString() == "native_run_identity");
        Assert.Equal("6JK8MD0VQQTA",
            identity.GetProperty("data").GetProperty("seed").GetString());
        Assert.Equal(10,
            identity.GetProperty("data").GetProperty("ascension").GetInt32());
        var requests = entries.Where(x =>
            x.GetProperty("type").GetString() == "native_input_request").ToArray();
        Assert.Equal(2, requests.Length);
        var request = requests[0];
        Assert.Equal("UNVERIFIED", request.GetProperty("data")
            .GetProperty("native_acceptance").GetString());
        Assert.False(request.GetProperty("data")
            .GetProperty("input_injected").GetBoolean());
        var started = Assert.Single(entries,
            x => x.GetProperty("type").GetString() == "action_started");
        Assert.Equal(1, started.GetProperty("data")
            .GetProperty("input_request_id").GetInt64());
        Assert.Equal("SAME_NATIVE_ACTION_OBJECT", started.GetProperty("data")
            .GetProperty("request_execution_link").GetString());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task NativeChoiceResultSurvivesStrictNestedCaptureStop()
    {
        object survivor = new();
        var entries = await Record(recorder =>
        {
            recorder.Start(survivor, "PlayCardAction", State(3));
            recorder.ChoicePause(survivor, State(2));
            recorder.NativePlayerChoice(1, 0, "CombatCard",
                [7L], null, null, null);
            recorder.Start(survivor, "PlayCardAction", State(2));
            recorder.Complete(survivor, State(2));
            recorder.Finish("Combat ended", State(2));
        });
        var observed = Assert.Single(entries,
            x => x.GetProperty("type").GetString() == "native_player_choice");
        var data = observed.GetProperty("data");
        Assert.Equal(1UL, data.GetProperty("owner_id").GetUInt64());
        Assert.Equal(0U, data.GetProperty("choice_id").GetUInt32());
        Assert.Equal("CombatCard", data.GetProperty("choice_type").GetString());
        Assert.Equal(7, data.GetProperty("selected_combat_card_ids")[0].GetInt64());
        Assert.Equal("NOT_CAPTURED",
            data.GetProperty("accepted_choice_menu").GetString());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task NativeHandMenuRetainsOriginalFilteredUiOrderAndBounds()
    {
        var entries = await Record(recorder =>
        {
            recorder.NativeHandMenu("SimpleSelect", "SURVIVOR",
                1, 1, false, 4, 0,
                [
                    new NativeHandMenuOption(7, "DEFEND_SILENT", 0),
                    new NativeHandMenuOption(9, "STRIKE_SILENT", 1),
                    new NativeHandMenuOption(11, "NEUTRALIZE", 3)
                ]);
            recorder.NativePlayerChoice(1, 4, "CombatCard",
                [9L], null, null, null);
            recorder.Finish("Combat ended", State(2));
        });
        var menu = Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "native_hand_menu");
        var data = menu.GetProperty("data");
        Assert.Equal("SURVIVOR",
            data.GetProperty("source_model_id").GetString());
        Assert.Equal(1, data.GetProperty("min_select").GetInt32());
        Assert.Equal(1, data.GetProperty("max_select").GetInt32());
        var options = data.GetProperty("options");
        Assert.Equal(3, options.GetArrayLength());
        Assert.Equal(7L, options[0].GetProperty("combat_card_id").GetInt64());
        Assert.Equal(11L, options[2].GetProperty("combat_card_id").GetInt64());
        Assert.Equal(3, options[2].GetProperty("ui_hand_index").GetInt32());
        Assert.Equal("SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            data.GetProperty("coverage").GetString());
        Assert.Equal("UNVERIFIED",
            data.GetProperty("selection_id_binding").GetString());
        Assert.False(data.GetProperty("predicate_re_evaluated_by_inspector").GetBoolean());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task UnmappedUiCardPreventsCompleteMenuAndInvalidProjectionFailsClosed()
    {
        var entries = await Record(recorder =>
        {
            recorder.NativeHandMenu("UpgradeSelect", "ARMAMENTS",
                1, 1, false, 3, 1,
                [
                    new NativeHandMenuOption(7, "DEFEND_SILENT", 0),
                    new NativeHandMenuOption(8, "STRIKE_SILENT", 2)
                ]);
            recorder.NativeHandMenu("SimpleSelect", "SURVIVOR",
                1, 1, false, 2, 0,
                [
                    new NativeHandMenuOption(7, "DEFEND_SILENT", 0),
                    new NativeHandMenuOption(7, "DEFEND_SILENT", 1)
                ]);
            recorder.Finish("Combat ended", State(2));
        });
        var menu = Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "native_hand_menu");
        Assert.Equal("INCOMPLETE_NATIVE_CARD_ID_MAPPING",
            menu.GetProperty("data").GetProperty("coverage").GetString());
        Assert.Single(entries, x => x.GetProperty("type").GetString()
            == "observation_failure");
        Assert.Equal("INCOMPLETE_CAPTURE", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task ManualCardPlayReturnSeparatesLocalAcceptanceFromQueueExecution()
    {
        var entries = await Record(recorder =>
        {
            recorder.NativeUiPlayBegin(5, "SHIV", true);
            recorder.NativeManualPlayResult(5, "SHIV", "creature:2", true);
            recorder.NativeUiPlayBegin(6, "DEFEND_SILENT", false);
            recorder.NativeManualPlayResult(6, "DEFEND_SILENT", null, false);
            recorder.Finish("Combat ended", State(3));
        });
        var uiStarts = entries.Where(x =>
            x.GetProperty("type").GetString() == "native_ui_play_begin").ToArray();
        Assert.Equal(2, uiStarts.Length);
        Assert.True(uiStarts[0].GetProperty("data")
            .GetProperty("started_via_shortcut").GetBoolean());
        Assert.Equal("NOT_TESTED", uiStarts[0].GetProperty("data")
            .GetProperty("manual_play_accepted").GetString());
        Assert.Equal("SOURCE_CALLSITE_PASSED", uiStarts[0].GetProperty("data")
            .GetProperty("native_ui_gate").GetString());
        var results = entries.Where(x =>
            x.GetProperty("type").GetString() == "native_manual_play_result").ToArray();
        Assert.Equal(2, results.Length);
        Assert.True(results[0].GetProperty("data")
            .GetProperty("returned_success").GetBoolean());
        Assert.False(results[1].GetProperty("data")
            .GetProperty("returned_success").GetBoolean());
        Assert.Equal("UNVERIFIED", results[0].GetProperty("data")
            .GetProperty("queue_acceptance").GetString());
        Assert.Equal("NOT_TESTED", results[0].GetProperty("data")
            .GetProperty("emulator_parity").GetString());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
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
    public async Task PreframeInitialHpRollIncludesExactEligibilityAndUnboundCreatureId()
    {
        var streams = Sts2Emulator.Core.NativeRngV111.CreateBundle(
            "native-action-initial-hp");
        var chosen = new Sts2Emulator.Core.NativeRandomIndexProviderV111()
            .Resolve(new Sts2Emulator.Core.RandomIndexRequestV111(
                "combat.initial_unique_hp", "run.niche",
                "monster_creation:1", 0, [34, 35]), streams);
        var evidence = new NativeInitialHpRollEvidenceV111(
            1, "SludgeSpinner", [34, 35], chosen.SelectedIndex,
            chosen.SelectedValue, false,
            chosen.NativeCursorBefore!, chosen.NativeCursorAfter!);
        var entries = await Record(recorder =>
        {
            recorder.NativeInitialHpRoll(evidence, bufferedBeforeFirstFrame: true);
            recorder.Finish("Combat ended", State(3));
        });
        var observed = Assert.Single(entries, x =>
            x.GetProperty("type").GetString() == "native_initial_hp_roll");
        var payload = observed.GetProperty("data");
        Assert.True(payload.GetProperty("recorded_before_first_frame").GetBoolean());
        Assert.Equal("SludgeSpinner",
            payload.GetProperty("monster_model_id").GetString());
        Assert.Equal(2, payload.GetProperty("ordered_eligible_hp").GetArrayLength());
        Assert.Equal("PRE_ATTACH_CREATURE_ID_UNVERIFIED",
            payload.GetProperty("native_creature_combat_id_status").GetString());
        Assert.Equal(JsonValueKind.Null,
            payload.GetProperty("native_creature_combat_id").ValueKind);
    }

    [Fact]
    public async Task NativeInitialShuffleSourceIsRecordedWithOrderedSwapAndHookStages()
    {
        var streams = Sts2Emulator.Core.NativeRngV111.CreateBundle(
            "native-action-shuffle-recorder");
        var shuffled = Sts2Emulator.Core.NativeFisherYatesV111.Execute(
            [0L, 1L, 2L], streams,
            new Sts2Emulator.Core.NativeRandomIndexProviderV111());
        var evidence = new NativeShuffleOperationEvidenceV111(
            1, "initial", ["card:0", "card:1", "card:2"],
            shuffled.Choices.Select(c => new NativeShuffleChoiceV111(
                c.Request.BoundedCount!.Value, c.SelectedIndex,
                c.NativeCursorBefore!, c.NativeCursorAfter!)).ToArray(),
            shuffled.PreHookOutput.Select(x => "card:" + x).ToArray(),
            shuffled.PreHookOutput.Select(x => "card:" + x).ToArray(),
            ["Strike", "Defend", "Strike"]);
        var entries = await Record(recorder =>
        {
            recorder.NativeShuffleOperation(evidence);
            recorder.Finish("Combat ended", State(3));
        });
        var observed = Assert.Single(entries,
            x => x.GetProperty("type").GetString() == "native_shuffle_operation");
        Assert.True(observed.GetProperty("action_id").ValueKind
            == JsonValueKind.Null);
        var data = observed.GetProperty("data");
        Assert.Equal("initial", data.GetProperty("kind").GetString());
        Assert.Equal(2, data.GetProperty("choices").GetArrayLength());
        Assert.Equal("shuffle_operation_local",
            data.GetProperty("card_identity_scope").GetString());
        Assert.True(data.GetProperty("recorded_before_first_frame").GetBoolean());
        Assert.Equal("OBSERVED", entries[^1].GetProperty("data")
            .GetProperty("status").GetString());
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
            .GetProperty("Counter").GetInt32());
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
