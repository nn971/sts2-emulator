"""Native run prelude: source-verified route/identity, not a macro replay."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_run_prelude_audit import audit

BUILD = "assembly=v111;sha256=" + "a" * 64


def prelude():
    rows = [
        {"type": "run_started", "data": {
            "schema": "native-run-prelude-v1", "game_build": BUILD,
            "run_id": "run-uuid", "seed": "6JK8MD0VQQTA",
            "ascension": 10, "character_id": "SILENT",
            "first_act_id": "OVERGROWTH", "player_count": 1,
            "source": "RunManager.RunStarted(RunState)",
            "source_seed": "RunState.Rng.StringSeed",
            "source_character": "RunState.Players[0].Character.Id",
            "source_ascension": "RunState.AscensionLevel",
            "source_act": "RunState.Act.Id",
            "source_rng_consumed_by_inspector": False,
            "source_macro_choices_complete": False,
            "emulator_bootstrap": "NOT_TESTED"}},
        {"type": "room_entered", "data": {
            "act_index": 0, "act_id": "OVERGROWTH",
            "act_floor": 0, "total_floor": 0,
            "map_column": None, "map_row": None, "room_type": "Map",
            "room_model_id": None, "player_hp": 70, "player_gold": 99,
            "source": "RunManager.RoomEntered",
            "visited_destination": "MAP_COORD_UNASSIGNED",
            "command_provenance": "UNVERIFIED",
            "emulator_parity": "NOT_TESTED"}},
        {"type": "map_action_request", "data": {
            "request_id": 1, "kind": "map_vote", "source_act": 0,
            "source_column": None, "source_row": None,
            "target_column": 3, "target_row": 1,
            "map_generation": 1,
            "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
            "action_identity": "SAME_NATIVE_GAME_ACTION_OBJECT",
            "queue_acceptance": "UNVERIFIED",
            "human_origin": "UNVERIFIED",
            "emulator_action": "NOT_MAPPED"}},
        {"type": "map_action_outcome", "data": {
            "request_id": 1, "native_action_state": "Queued",
            "native_action_id": 12,
            "source": "ActionQueueSynchronizer.RequestEnqueue.postfix",
            "native_executor_completion": "NOT_OBSERVED",
            "visited_room_proves_this_request": False}},
        {"type": "map_action_request", "data": {
            "request_id": 2, "kind": "map_move", "source_act": 0,
            "source_column": None, "source_row": None,
            "target_column": 3, "target_row": 1,
            "map_generation": None,
            "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
            "action_identity": "SAME_NATIVE_GAME_ACTION_OBJECT",
            "queue_acceptance": "UNVERIFIED",
            "human_origin": "UNVERIFIED",
            "emulator_action": "NOT_MAPPED"}},
        {"type": "map_action_outcome", "data": {
            "request_id": 2, "native_action_state": "Finished",
            "native_action_id": 13,
            "source": "ActionQueueSynchronizer.RequestEnqueue.postfix",
            "native_executor_completion": "NOT_OBSERVED",
            "visited_room_proves_this_request": False}},
        {"type": "room_entered", "data": {
            "act_index": 0, "act_id": "OVERGROWTH",
            "act_floor": 2, "total_floor": 2,
            "map_column": 3, "map_row": 1, "room_type": "Monster",
            "room_model_id": "MAWLER", "player_hp": 69, "player_gold": 99,
            "source": "RunManager.RoomEntered",
            "visited_destination": "SOURCE_CURRENT_MAP_COORD",
            "command_provenance": "UNVERIFIED",
            "emulator_parity": "NOT_TESTED"}},
        {"type": "combat_link", "data": {
            "combat_run_id": "combat-uuid",
            "combat_trace_file": "passive-combat-uuid.jsonl",
            "act_index": 0,
            "map_column": 3, "map_row": 1,
            "source": "NativeV111PassiveBridge.OpenCombat",
            "combat_initial_rng_alignment": "NOT_TESTED",
            "full_macro_action_transcript": "NOT_CAPTURED"}},
        {"type": "terminal", "data": {
            "status": "SOURCE_PRELUDE_OBSERVED",
            "reason": "First observed combat capture",
            "combat_link_recorded": True,
            "linked_combat_room_arrival_observed": True,
            "map_requests_missing_postfix": 0,
            "source_macro_choices_complete": False,
            "native_rng_parity": "NOT_TESTED",
            "emulator_bootstrap": "NOT_TESTED"}},
    ]
    def snapshot(site):
        return {"type": "run_rng_snapshot", "data": {
            "site": site, "player_rng_seed": 998877665544332211,
            "streams": [
                {"scope": "player", "stream_id": "Rewards", "counter": 0,
                 "s0": 18446744073709551615, "s1": 13, "s2": 24, "s3": 42},
                {"scope": "run", "stream_id": "UpFront", "counter": 12,
                 "s0": 1, "s1": 2, "s2": 3, "s3": 4}],
            "source":
                "RunRngSet.ToSerializable+PlayerRngSet.ToSerializable",
            "source_rng_consumed_by_inspector": False,
            "native_rng_precombat_history_complete": False,
            "emulator_rng_alignment": "NOT_TESTED"}}
    rows.insert(1, snapshot("run_started"))
    for index in reversed(range(len(rows))):
        if rows[index]["type"] == "room_entered":
            rows.insert(index + 1, snapshot("room_entered"))
        elif rows[index]["type"] == "combat_link":
            rows.insert(index, snapshot("combat_attach"))
    for n, row in enumerate(rows, 1):
        row["ordinal"] = n
        row["run_id"] = "run-uuid"
    return rows


def combat():
    return [
        {"type": "header", "data": {
            "run_id": "combat-uuid", "game_build": BUILD}},
        {"type": "native_run_identity", "data": {
            "seed": "6JK8MD0VQQTA", "ascension": 10,
            "source": "RunState.Rng.StringSeed"}},
    ]


class RunPreludeAuditTests(unittest.TestCase):
    def test_read_only_native_startup_queues_are_audited(self):
        rows = prelude()
        acts = []
        for i, count in enumerate((15, 14, 13)):
            acts.append({
                "act_index": i,
                "act_id": ("OVERGROWTH", "HIVE", "GLORY")[i],
                "events": ["EVENT_ONE", "EVENT_TWO"],
                "normal_encounters": ["ENCOUNTER_" + str(j) for j in range(count)],
                "elite_encounters": ["ELITE_" + str(j) for j in range(15)],
                "boss_encounter": "BOSS",
                "second_boss_encounter": "SECOND_BOSS" if i == 2 else None,
                "ancient": "ANCIENT",
                "events_visited": 0, "normal_encounters_visited": 0,
                "elite_encounters_visited": 0, "boss_encounters_visited": 0,
            })
        data = {
            "acts": acts,
            "source": "ActModel._rooms after RunManager.GenerateRooms",
            "source_rng_consumed_by_inspector": False,
            "native_queue_emulator_parity": "NOT_TESTED"
        }
        rows.insert(2, {"type": "startup_room_queues", "data": data,
                        "run_id": "run-uuid"})
        rows[-1]["data"]["startup_room_queues_recorded"] = True
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        result = audit(rows)
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", result["status"],
                         result["errors"])
        self.assertEqual(data, result["source_startup_room_queues"])

        rows[2]["data"]["acts"][0]["normal_encounters"].pop()
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows[2]["data"]["acts"][0]["normal_encounters"].append("ENCOUNTER_14")
        rows[2]["data"]["source_rng_consumed_by_inspector"] = True
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows[2]["data"]["source_rng_consumed_by_inspector"] = False
        rows[-1]["data"]["startup_room_queues_recorded"] = False
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

        rows = prelude()
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", audit(rows)["status"])
        self.assertIsNone(audit(rows)["source_startup_room_queues"])


    @staticmethod
    def with_full_source_map():
        rows = prelude()
        points = []
        for row in range(17):
            point_type = ("Ancient" if row == 0 else "Boss" if row == 16
                          else "Treasure" if row == 9 else "RestSite"
                          if row == 15 else "Monster")
            points.append({
                "column": 3, "row": row, "point_type": point_type,
                "can_be_modified": row not in (0, 16),
                "children": [] if row == 16 else [
                    {"column": 3, "row": row + 1}],
            })
        data = {
            "act_index": 0, "act_id": "OVERGROWTH",
            "map_type": "StandardActMap", "points": points,
            "map_rng": {"counter": 90, "s0": 1,
                        "s1": 2, "s2": 3, "s3": 4},
            "source": "RunState.Map (after RunManager.GenerateMap)",
            "map_rng_source": "StandardActMap._rng.ToSerializable",
            "source_rng_consumed_by_inspector": False,
            "source_map_mutation_history_complete": False,
            "native_map_emulator_alignment": "NOT_TESTED",
        }
        insertion = next(i for i, record in enumerate(rows)
                         if record["type"] == "map_action_request")
        rows.insert(insertion, {"type": "initial_map_snapshot",
                                "data": data})
        rows[-1]["data"]["initial_map_snapshot_recorded"] = True
        for n, record in enumerate(rows, 1):
            record["ordinal"] = n
            record["run_id"] = "run-uuid"
        return rows

    def test_full_native_map_and_local_rng_are_audited_separately(self):
        result = audit(self.with_full_source_map())
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", result["status"])
        self.assertTrue(result["source_map_rng_observed"])
        self.assertEqual("NOT_TESTED", result["native_map_topology_parity"])
        native = result["source_initial_map_snapshot"]
        self.assertEqual(17, len(native["points"]))
        self.assertEqual(90, native["map_rng"]["counter"])

    def test_malformed_duplicate_or_late_map_snapshots_are_rejected(self):
        for case in ("corrupt", "duplicate", "late", "terminal_lie"):
            rows = self.with_full_source_map()
            idx = next(i for i, record in enumerate(rows)
                       if record["type"] == "initial_map_snapshot")
            if case == "corrupt":
                rows[idx]["data"]["points"][1]["point_type"] = "SECRET"
            elif case == "duplicate":
                rows.insert(idx + 1, copy.deepcopy(rows[idx]))
            elif case == "late":
                item = rows.pop(idx)
                pos = next(i for i, record in enumerate(rows)
                           if record["type"] == "map_action_outcome")
                rows.insert(pos + 1, item)
            else:
                rows[-1]["data"]["initial_map_snapshot_recorded"] = False
            for n, record in enumerate(rows, 1):
                record["ordinal"] = n
            self.assertEqual("INVALID_PRELUDE", audit(rows)["status"], case)

    def test_verified_source_and_room_arrival_is_not_complete_macro(self):
        result = audit(prelude())
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", result["status"])
        self.assertEqual(10, result["run_identity"]["ascension"])
        self.assertEqual("6JK8MD0VQQTA", result["run_identity"]["seed"])
        self.assertEqual(2, len(result["source_room_arrivals"]))
        self.assertEqual(2, len(result["source_map_requests"]))
        self.assertEqual(4, len(result["source_run_rng_snapshots"]))
        self.assertEqual("run_started",
                         result["source_run_rng_snapshots"][0]["site"])
        self.assertEqual(18446744073709551615,
                         result["source_run_rng_snapshots"][0][
                             "streams"][0]["s0"])
        self.assertFalse(result["precombat_rng_call_history_complete"])
        self.assertEqual(1, len(result["source_observed_map_arrival_matches"]))
        self.assertFalse(result["source_observed_map_arrival_matches"][0][
            "causal_link_verified"])
        self.assertFalse(result["map_vote_is_human_proof"])
        self.assertFalse(result["native_macro_action_transcript_complete"])
        self.assertIn("NEOW_CHOICE_NOT_CAPTURED",
                      result["explicit_unobserved_macro_and_rng_inputs"])
        self.assertEqual("NOT_EXECUTED", result["emulator_combat_bootstrap"])
        self.assertEqual("NOT_TESTED", result["native_emulator_parity"])

    def test_paired_combat_requires_its_own_independent_audit(self):
        self.assertEqual(
            "INVALID_PRELUDE",
            audit(prelude(), combat_sidecar=combat(),
                  combat_audit_status="UNVERIFIED")["status"])
        paired = audit(prelude(), combat_sidecar=combat(),
                       combat_audit_status="CALLBACK_TIMELINE_CONSISTENT")
        self.assertEqual("RUN_PRELUDE_PAIRED_SOURCE_OBSERVED",
                         paired["status"])
        self.assertEqual("combat-uuid",
                         paired["first_combat_link"]["combat_run_id"])
        self.assertFalse(paired["native_macro_action_transcript_complete"])

    def test_perturbed_combat_identity_fails_closed(self):
        for kind in ("run_id", "game_build", "seed", "ascension"):
            sidecar = combat()
            if kind in ("run_id", "game_build"):
                sidecar[0]["data"][kind] = "wrong"
            else:
                sidecar[1]["data"][kind] = "wrong" if kind == "seed" else 9
            result = audit(prelude(), combat_sidecar=sidecar,
                           combat_audit_status="CALLBACK_TIMELINE_CONSISTENT")
            self.assertEqual("INVALID_PRELUDE", result["status"])
            self.assertIn("Prelude/combat source identity mismatch",
                          result["errors"])

    def test_bad_ordinals_or_duplicate_requests_fail(self):
        rows = prelude()
        rows[2]["ordinal"] = 88
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows = prelude()
        next(row for row in rows if row["type"] == "map_action_request"
             and row["data"]["request_id"] == 2)["data"]["request_id"] = 1
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_no_verified_start_or_no_link_fail(self):
        rows = prelude()
        rows[0]["data"]["seed"] = ""
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows = prelude()
        rows.pop(-2)
        for n, row in enumerate(rows, 1):
            row["ordinal"] = n
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_missing_map_postfix_fails_but_never_invents_acceptance(self):
        rows = prelude()
        rows.remove(next(row for row in rows
                         if row["type"] == "map_action_outcome"
                         and row["data"]["request_id"] == 2))
        for n, row in enumerate(rows, 1):
            row["ordinal"] = n
        result = audit(rows)
        self.assertEqual("INVALID_PRELUDE", result["status"])
        self.assertIn("Native map requests missing queue return observations",
                      result["errors"])
        self.assertFalse(result["map_request_proves_room_arrival"])

    def test_combat_link_can_precede_native_room_entered_callback(self):
        rows = prelude()
        link = next(row for row in rows if row["type"] == "combat_link")
        rows.remove(link)
        room_index = next(i for i, row in enumerate(rows)
                          if row["type"] == "room_entered"
                          and row["data"]["map_column"] == 3)
        rows.insert(room_index, link)  # Earlier than source RoomEntered
        # The combat_attach RNG snapshot must immediately precede the
        # link; relocate it to keep the same native callback source order.
        rng = next(row for row in rows
                   if row["type"] == "run_rng_snapshot"
                   and row["data"]["site"] == "combat_attach")
        rows.remove(rng)
        rows.insert(room_index, rng)
        for n, row in enumerate(rows, 1):
            row["ordinal"] = n
        result = audit(rows)
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", result["status"])
        self.assertLess(result["first_combat_link"]["ordinal"],
                        next(room["ordinal"] for room in
                             result["source_room_arrivals"]
                             if room["map_column"] == 3))

    def test_missing_linked_combat_room_or_unsettled_postfix_fails(self):
        rows = prelude()
        rows.remove(next(row for row in rows
                         if row["type"] == "room_entered"
                         and row["data"]["map_column"] == 3))
        for n, row in enumerate(rows, 1):
            row["ordinal"] = n
        result = audit(rows)
        self.assertEqual("INVALID_PRELUDE", result["status"])
        self.assertIn("Linked combat source RoomEntered not observed",
                      result["errors"])

        rows = prelude()
        rows[-1]["data"]["map_requests_missing_postfix"] = 1
        result = audit(rows)
        self.assertEqual("INVALID_PRELUDE", result["status"])
        self.assertIn("Terminal has pending native map queue returns",
                      result["errors"])

        rows = prelude()
        rows[-1]["data"]["linked_combat_room_arrival_observed"] = False
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_invalid_source_rng_snapshot_fails_closed(self):
        rows = prelude()
        rng = next(row for row in rows
                   if row["type"] == "run_rng_snapshot"
                   and row["data"]["site"] == "run_started")
        rng["data"]["streams"][0]["s0"] = 2**64
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows = prelude()
        rng = next(row for row in rows
                   if row["type"] == "run_rng_snapshot"
                   and row["data"]["site"] == "combat_attach")
        rng["data"]["native_rng_precombat_history_complete"] = True
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_missing_combat_rng_source_state_is_never_silently_recovered(self):
        rows = prelude()
        rows.remove(next(row for row in rows
                         if row["type"] == "run_rng_snapshot"
                         and row["data"]["site"] == "combat_attach"))
        for n, row in enumerate(rows, 1):
            row["ordinal"] = n
        result = audit(rows)
        self.assertEqual("INVALID_PRELUDE", result["status"])
        self.assertIn("Missing native run-start or first-combat RNG snapshot",
                      result["errors"])

    def test_no_macro_completeness_promotion_even_if_causal_destination_matches(self):
        rows = prelude()
        rows[-1]["data"]["source_macro_choices_complete"] = True
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows = prelude()
        rows[-2]["data"]["full_macro_action_transcript"] = "COMPLETE"
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])


    def test_opaque_precombat_queue_request_remains_non_executable(self):
        rows = prelude()
        insert = next(i for i, r in enumerate(rows)
                      if r["type"] == "map_action_request")
        request = {"type": "opaque_action_request", "data": {
            "request_id": 3,
            "native_action_type": "Example.NativeMacroAction",
            "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
            "action_identity": "SAME_NATIVE_GAME_ACTION_OBJECT",
            "queue_acceptance": "UNVERIFIED",
            "human_origin": "UNVERIFIED",
            "emulator_action": "NOT_MAPPED"}}
        outcome = {"type": "opaque_action_outcome", "data": {
            "request_id": 3, "native_action_state": "Deferred",
            "native_action_id": None,
            "source": "ActionQueueSynchronizer.RequestEnqueue.postfix",
            "native_executor_completion": "NOT_OBSERVED"}}
        rows[insert:insert] = [request, outcome]
        for row in (request, outcome):
            row["run_id"] = "run-uuid"
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = audit(rows)
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", report["status"])
        self.assertEqual(["Example.NativeMacroAction"],
                         report["observed_precombat_queue_action_types"])
        self.assertEqual(1, len(report["source_opaque_queue_requests"]))
        self.assertEqual(1, len(report["source_opaque_queue_outcomes"]))
        self.assertFalse(report["native_macro_action_transcript_complete"])
        self.assertEqual("NOT_EXECUTED", report["emulator_combat_bootstrap"])

        rows[insert + 1]["data"]["request_id"] = 999
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_opaque_queue_missing_outcome_or_spoofed_terminal_fails(self):
        rows = prelude()
        index = next(i for i, r in enumerate(rows)
                     if r["type"] == "combat_link")
        rows.insert(index, {"type": "opaque_action_request", "data": {
            "request_id": 3, "native_action_type": "Example.NativeMacroAction",
            "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
            "action_identity": "SAME_NATIVE_GAME_ACTION_OBJECT",
            "queue_acceptance": "UNVERIFIED",
            "human_origin": "UNVERIFIED",
            "emulator_action": "NOT_MAPPED"}})
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows[-1]["data"]["opaque_requests_missing_postfix"] = 1
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])



    def test_precombat_event_choice_offer_and_return(self):
        rows = prelude()
        pos = next(i for i, row in enumerate(rows)
                   if row["type"] == "map_action_request")
        request = {"type": "event_choice_request", "run_id": "run-uuid",
                   "data": {
            "request_id": 1, "event_model_id": "NEOW",
            "owner_id": 7, "selected_index": 1,
            "selected_text_key": "hefty_tablet",
            "options": [
                {"index": 0, "text_key": "arcane_scroll",
                 "is_locked": False, "is_proceed": False,
                 "relic_model_id": "ARCANE_SCROLL"},
                {"index": 1, "text_key": "hefty_tablet",
                 "is_locked": False, "is_proceed": False,
                 "relic_model_id": "HEFTY_TABLET"}],
            "source": "EventSynchronizer.ChooseLocalOption.prefix",
            "selection_origin": "LOCAL_SYNCHRONIZER_CALL",
            "event_rng": {"counter": 8, "s0": 18446744073709551615,
                          "s1": 2, "s2": 3, "s3": 4},
            "event_rng_source": "EventModel.Rng.ToSerializable",
            "source_event_rng_consumed": False,
            "event_rng_alignment": "NOT_TESTED",
            "option_execution": "NOT_OBSERVED",
            "effects_settled": False}}
        returned = {"type": "event_choice_return", "run_id": "run-uuid",
                    "data": {
            "request_id": 1,
            "source": "EventSynchronizer.ChooseLocalOption.postfix",
            "handler_returned": True, "option_effects_settled": False}}
        rows[pos:pos] = [request, returned]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = audit(rows)
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", report["status"])
        self.assertEqual(1, len(report["source_event_choice_requests"]))
        self.assertEqual(1, len(report["source_event_choice_returns"]))
        self.assertEqual("hefty_tablet",
                         report["source_event_choice_requests"][0]
                         ["selected_text_key"])
        self.assertFalse(report["native_macro_action_transcript_complete"])
        self.assertEqual("NOT_EXECUTED", report["emulator_combat_bootstrap"])

        request["data"]["selected_text_key"] = "not-offered"
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        request["data"]["selected_text_key"] = "hefty_tablet"
        request["data"]["event_rng"]["s0"] = 2**64
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        request["data"]["event_rng"]["s0"] = 18446744073709551615
        returned["data"]["request_id"] = 999
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_unreturned_source_event_choice_fails(self):
        rows = prelude()
        pos = next(i for i, row in enumerate(rows)
                   if row["type"] == "map_action_request")
        rows.insert(pos, {"type": "event_choice_request",
                          "run_id": "run-uuid", "data": {
            "request_id": 1, "event_model_id": "NEOW",
            "owner_id": 7, "selected_index": 0,
            "selected_text_key": "hefty_tablet",
            "options": [
                {"index": 0, "text_key": "hefty_tablet",
                 "is_locked": False, "is_proceed": False,
                 "relic_model_id": "HEFTY_TABLET"}],
            "source": "EventSynchronizer.ChooseLocalOption.prefix",
            "selection_origin": "LOCAL_SYNCHRONIZER_CALL",
            "event_rng": {"counter": 8, "s0": 18446744073709551615,
                          "s1": 2, "s2": 3, "s3": 4},
            "event_rng_source": "EventModel.Rng.ToSerializable",
            "source_event_rng_consumed": False,
            "event_rng_alignment": "NOT_TESTED",
            "option_execution": "NOT_OBSERVED",
            "effects_settled": False}})
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        rows[-1]["data"]["event_choice_requests_missing_postfix"] = 1
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])


    def test_synchronized_player_choice_result_preserves_source_ids(self):
        rows = prelude()
        index = next(i for i, r in enumerate(rows)
                     if r["type"] == "map_action_request")
        choice = {"type": "player_choice_result", "run_id": "run-uuid",
                  "data": {
            "owner_id": 99, "choice_id": 3, "choice_type": "DeckCard",
            "combat_card_ids": None, "deck_indices": [2, 7],
            "canonical_card_ids": None, "indexes": None,
            "selected_player_id": None, "mutable_card_count": None,
            "mutable_owner_id": None,
            "source": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
            "synchronized_result_observed": True,
            "offered_options": "NOT_CAPTURED",
            "effects_settled": False,
            "emulator_replay": "NOT_EXECUTED"}}
        rows.insert(index, choice)
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = audit(rows)
        self.assertEqual("RUN_PRELUDE_SOURCE_OBSERVED", report["status"])
        self.assertEqual([2, 7],
                         report["source_precombat_player_choices"][0]
                         ["deck_indices"])
        self.assertFalse(report["native_macro_action_transcript_complete"])
        choice["data"]["offered_options"] = "COMPLETE"
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])
        choice["data"]["offered_options"] = "NOT_CAPTURED"
        choice["data"]["choice_id"] = -1
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])

    def test_repeated_precombat_choice_id_rejected(self):
        rows = prelude()
        index = next(i for i, r in enumerate(rows)
                     if r["type"] == "map_action_request")
        data = {"owner_id": 9, "choice_id": 0,
                "choice_type": "Index", "indexes": [1],
                "combat_card_ids": None, "deck_indices": None,
                "canonical_card_ids": None, "selected_player_id": None,
                "mutable_card_count": None, "mutable_owner_id": None,
                "source": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
                "synchronized_result_observed": True,
                "offered_options": "NOT_CAPTURED",
                "effects_settled": False, "emulator_replay": "NOT_EXECUTED"}
        rows[index:index] = [
            {"type": "player_choice_result", "run_id": "run-uuid",
             "data": data.copy()},
            {"type": "player_choice_result", "run_id": "run-uuid",
             "data": data.copy()}]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        self.assertEqual("INVALID_PRELUDE", audit(rows)["status"])


if __name__ == "__main__":
    unittest.main()
