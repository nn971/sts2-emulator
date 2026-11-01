"""Fail-closed pairing of genuine strict decisions and executor candidates."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_decision_gap_audit import audit


def action(card_id):
    return {"kind": "play_card",
            "payload": {"card_instance_id": card_id, "target_id": None}}


def pair(*, nested=False, frame=False):
    strict = [
        {"type": "header", "run_id": "run-1", "game_build": "v111;sha256=a"},
        {"type": "decision", "decision_id": 1, "state": {"combat": {"energy": 3}}},
        {"type": "action", "decision_id": 1, "accepted": True,
         "action": action(1)},
        {"type": "terminal", "status": "INCOMPLETE_CAPTURE",
         "reason": "Player input without new semantic boundary"},
    ]
    rows = [
        {"type": "header", "data": {
            "run_id": "run-1", "game_build": "v111;sha256=a"}},
        {"type": "stable_decision", "data": {"decision_id": 1},
         "state": {"combat": {"energy": 3}}},
        {"type": "action_started", "action_id": 1,
         "canonical_action": action(1), "native_type": "PlayCardAction",
         "state": {"combat": {"energy": 3}}},
    ]
    if nested:
        rows.extend([
            {"type": "nested_choice_pause", "action_id": 1},
            {"type": "action_resumed", "action_id": 1}])
    rows.append({"type": "action_completed", "action_id": 1})
    if frame:
        rows.append({"type": "frame_state", "state": {"combat": {"energy": 2}},
                     "data": {"frame_index": 42}})
    rows.extend([
        {"type": "action_started", "action_id": 2,
         "canonical_action": action(2), "native_type": "PlayCardAction",
         "state": {"combat": {"energy": 2}}},
        {"type": "action_completed", "action_id": 2},
        {"type": "terminal", "data": {"status": "OBSERVED"}},
    ])
    for i, row in enumerate(rows, 1):
        row["ordinal"] = i
    return strict, rows


class DecisionGapAuditTests(unittest.TestCase):
    def test_survivor_style_nested_continuation(self):
        strict, rows = pair(nested=True, frame=True)
        result = audit(strict, rows)
        self.assertEqual("FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND",
                         result["status"])
        self.assertEqual(1, result["matched_actions"])
        self.assertEqual(1, result["unmatched_actions"])
        self.assertEqual("UNSUPPORTED_NESTED_CHOICE_CONTINUATION",
                         result["first_gap"]["gap_type"])
        self.assertTrue(result["first_gap"]["pre_action_state_observed"])
        self.assertTrue(result["first_gap"]["strict_action_already_consumed"])
        self.assertEqual("NOT_PERFORMED", result["legal_menu_reconstruction"])

    def test_leading_strike_shiv_gap_after_changed_frame(self):
        strict, rows = pair(frame=True)
        result = audit(strict, rows)
        self.assertEqual("EXECUTOR_ACTION_WITHOUT_NEW_STABLE_BOUNDARY",
                         result["first_gap"]["gap_type"])
        self.assertEqual(1, result["first_gap"]["frame_samples_since_prior_matched_action"])
        self.assertEqual(action(2), result["first_gap"]["candidate_action"])
        self.assertEqual("EXECUTOR_ACTION_TYPE_ONLY_HUMAN_ORIGIN_UNVERIFIED",
                         result["action_origin"])

    def test_native_ui_begin_during_previous_action_explains_stale_frame_gate(self):
        # Sanitized chronology from 2026-11-01 first combat:
        # source UI began Defend while Neutralize was still executing;
        # request+executor arrived after completion, before another stable
        # frame decision. These are native observations, not a legal menu.
        strict, rows = pair()
        previous_done = next(i for i, row in enumerate(rows)
                             if row.get("type") == "action_completed"
                             and row.get("action_id") == 1)
        rows.insert(previous_done, {
            "type": "native_ui_play_begin",
            "data": {"combat_card_id": 2,
                     "card_model_id": "DEFEND_SILENT"}})
        next_start = next(i for i, row in enumerate(rows)
                          if row.get("type") == "action_started"
                          and row.get("action_id") == 2)
        rows.insert(next_start, {
            "type": "native_input_request",
            "canonical_action": action(2),
            "state": {"combat": {"energy": 2}},
            "data": {"input_request_id": 12}})
        next_start += 1
        rows[next_start]["data"] = {"input_request_id": 12}
        rows.insert(next_start + 1, {
            "type": "strict_input_gap", "action_id": 2, "data": {
                "last_reported_gate": "WAITING_FOR_DECISION:CARD_PLAY_ANIMATION",
                "decision_generation": 9,
                "last_captured_generation": 8}})
        previous = next(row for row in rows
                        if row.get("type") == "action_completed"
                        and row.get("action_id") == 1)
        previous["state"] = {"combat": {"energy": 2}}
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = audit(strict, rows)
        self.assertEqual("FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND",
                         report["status"])
        self.assertEqual(1, report["matched_actions"])
        timing = report["first_gap"]["source_timing"]
        self.assertEqual(
            "NATIVE_UI_PLAY_BEGIN_OVERLAPPED_PREVIOUS_EXECUTOR_ACTION",
            timing["classification"])
        self.assertTrue(timing["ui_begin_before_previous_action_completed"])
        self.assertTrue(timing["request_after_previous_action_completed"])
        self.assertTrue(timing[
            "source_request_executor_action_identity_correlated"])
        self.assertTrue(timing["request_and_start_state_agree"])
        self.assertTrue(timing["last_completion_and_request_state_agree"])
        self.assertEqual("WAITING_FOR_DECISION:CARD_PLAY_ANIMATION",
                         timing["strict_last_reported_frame_gate"])
        self.assertEqual(9, timing["strict_decision_generation"])
        self.assertEqual(8, timing["strict_last_captured_generation"])
        self.assertFalse(timing["fresh_semantic_legal_menu_verified"])
        self.assertEqual("NOT_PERFORMED", report["legal_menu_reconstruction"])

    def test_ui_begin_after_prior_completion_does_not_claim_overlap(self):
        strict, rows = pair()
        previous_done = next(i for i, row in enumerate(rows)
                             if row.get("type") == "action_completed"
                             and row.get("action_id") == 1)
        rows.insert(previous_done + 1, {
            "type": "native_ui_play_begin",
            "data": {"combat_card_id": 2}})
        next_start = next(i for i, row in enumerate(rows)
                          if row.get("type") == "action_started"
                          and row.get("action_id") == 2)
        rows.insert(next_start, {
            "type": "native_input_request",
            "canonical_action": action(2),
            "data": {"input_request_id": 12}})
        rows[next_start + 1]["data"] = {"input_request_id": 12}
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        gap = audit(strict, rows)["first_gap"]
        self.assertEqual("TEMPORAL_CAUSE_UNVERIFIED",
                         gap["source_timing"]["classification"])
        self.assertFalse(gap["source_timing"][
            "ui_begin_before_previous_action_completed"])

    def test_overlapping_ui_with_unlinked_request_does_not_claim_cause(self):
        strict, rows = pair()
        previous_done = next(i for i, row in enumerate(rows)
                             if row.get("type") == "action_completed"
                             and row.get("action_id") == 1)
        rows.insert(previous_done, {
            "type": "native_ui_play_begin",
            "data": {"combat_card_id": 2}})
        next_start = next(i for i, row in enumerate(rows)
                          if row.get("type") == "action_started"
                          and row.get("action_id") == 2)
        rows.insert(next_start, {
            "type": "native_input_request",
            "canonical_action": action(99),
            "data": {"input_request_id": 12}})
        rows[next_start + 1]["data"] = {"input_request_id": 12}
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        timing = audit(strict, rows)["first_gap"]["source_timing"]
        self.assertEqual("TEMPORAL_CAUSE_UNVERIFIED",
                         timing["classification"])
        self.assertFalse(timing[
            "source_request_executor_action_identity_correlated"])

    def test_source_input_and_selected_choice_correlate_with_missing_boundary(self):
        strict, rows = pair(nested=True, frame=True)
        request = {"type": "native_input_request",
            "native_type": "PlayCardAction",
            "canonical_action": action(2),
            "data": {"input_request_id": 17}}
        selected = {"type": "native_player_choice", "action_id": 1,
                    "data": {"choice_id": 3, "choice_type": "CombatCard",
                             "selected_combat_card_ids": [9]}}
        outcome = {"type": "native_input_outcome",
                   "native_type": "PlayCardAction",
                   "data": {"input_request_id": 17,
                            "queue_result": "ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN"}}
        # The request precedes the executor start; the native method may
        # return only *after* synchronous execution of that action.
        second_start = next(i for i, row in enumerate(rows)
                            if row.get("action_id") == 2
                            and row.get("type") == "action_started")
        rows.insert(second_start, request)
        rows.insert(second_start, selected)
        second_start += 2
        rows[second_start]["data"] = {"input_request_id": 17}
        rows.insert(second_start + 2, outcome)
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        result = audit(strict, rows)
        self.assertEqual("FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND",
                         result["status"])
        gap = result["first_gap"]
        self.assertEqual(17, gap["source_input_request_id"])
        self.assertEqual("ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN",
                         gap["source_queue_return_classification"])
        self.assertEqual(3,
                         gap["source_selected_choices_since_prior_action"][0]["choice_id"])
        self.assertEqual([9], gap[
            "source_selected_choices_since_prior_action"][0]["selected_combat_card_ids"])
        self.assertEqual("NOT_PERFORMED", result["legal_menu_reconstruction"])

    def test_survivor_hand_ui_and_manual_shiv_return_are_diagnostic_only(self):
        strict, rows = pair(nested=True, frame=True)
        displayed = {"type": "native_hand_menu", "action_id": 1,
                     "data": {"menu_id": 3, "source_model_id": "SURVIVOR",
                              "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
                              "selection_id_binding": "UNVERIFIED",
                              "options": [
                                  {"combat_card_id": 8},
                                  {"combat_card_id": 9}]}}
        selection = {"type": "native_player_choice", "action_id": 1,
                     "data": {"choice_id": 4, "choice_type": "CombatCard",
                              "selected_combat_card_ids": [8]}}
        manual = {"type": "native_manual_play_result",
                  "data": {"combat_card_id": 2, "target_id": None,
                           "returned_success": True,
                           "source": "CardModel.TryManualPlay.postfix"}}
        ui_start = {"type": "native_ui_play_begin",
                    "data": {"combat_card_id": 2,
                             "started_via_shortcut": True,
                             "native_ui_gate": "SOURCE_CALLSITE_PASSED"}}
        rows.insert(4, displayed)
        rows.insert(5, selection)
        rows.insert(-1, ui_start)
        rows.insert(-1, manual)
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        result = audit(strict, rows)
        gap = result["first_gap"]
        self.assertEqual("FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND",
                         result["status"])
        self.assertEqual("SURVIVOR",
                         gap["source_native_hand_menus_since_prior_action"][0]["source_model_id"])
        self.assertEqual([8, 9],
                         gap["source_native_hand_menus_since_prior_action"][0]["option_ids"])
        self.assertEqual("UNVERIFIED",
                         gap["source_native_hand_menus_since_prior_action"][0]["selection_id_binding"])
        self.assertTrue(
            gap["source_matching_manual_play_return_candidates"][0]["returned_success"])
        self.assertEqual("TEMPORAL_CANDIDATES_NOT_UNIQUE_PROOF",
                         gap["manual_play_correlation"])
        self.assertTrue(
            gap["source_matching_ui_play_begin_candidates"][0]["started_via_shortcut"])
        self.assertEqual("CARD_ID_CANDIDATES_NOT_UNIQUE_PROOF",
                         gap["ui_play_correlation"])

    def test_first_gap_preserves_observed_menu_revision_and_close_chronology(self):
        strict, rows = pair(nested=True, frame=True)
        original = {"type": "native_hand_menu", "action_id": 1, "data": {
            "menu_id": 3, "source_model_id": "SURVIVOR",
            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "selection_owner_id": 1, "selection_choice_id": 4,
            "options": [{"combat_card_id": 7}]}}
        changed = {"type": "native_hand_menu_revision", "action_id": 1,
                   "data": {"menu_id": 3, "revision": 2,
                            "peek_mode": False,
                            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
                            "options": [{"combat_card_id": 9}]}}
        closed = {"type": "native_hand_menu_closed", "action_id": 1,
                  "data": {"menu_id": 3, "final_revision": 2,
                           "native_reason": "AfterCardsSelected"}}
        gate = {"type": "native_ui_hand_gate", "data": {
            "gate": "AreCardActionsAllowed",
            "returned_allowed": True,
            "existing_card_play_active": True,
            "full_legal_menu": "NOT_TESTED"}}
        rows[3:3] = [original, changed, closed, gate]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = audit(strict, rows)
        self.assertEqual("FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND",
                         report["status"])
        gap = report["first_gap"]
        self.assertEqual(4, gap[
            "source_native_hand_menus_since_prior_action"][0]["selection_choice_id"])
        self.assertEqual([9], gap[
            "source_native_menu_revisions_since_prior_action"][0]["option_ids"])
        self.assertEqual(2, gap[
            "source_native_menu_closures_since_prior_action"][0]["final_revision"])
        self.assertEqual("NOT_PERFORMED", report["legal_menu_reconstruction"])
        self.assertTrue(gap[
            "source_native_ui_gate_evaluations_since_prior_action"][0]["returned_allowed"])
        self.assertTrue(gap[
            "source_native_ui_gate_evaluations_since_prior_action"][0]["existing_card_play_active"])

    def test_all_source_candidates_aligned(self):
        strict, rows = pair()
        rows = [r for r in rows if r.get("action_id") != 2]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        result = audit(strict, rows)
        self.assertEqual("NO_EXECUTOR_ACTION_GAP_OBSERVED", result["status"])
        self.assertEqual(1, result["matched_actions"])
        self.assertEqual(0, result["unmatched_actions"])

    def test_reject_unmatched_strict_action(self):
        strict, rows = pair()
        rows[2]["canonical_action"] = action(99)
        self.assertEqual("INVALID_PAIRED_CAPTURE", audit(strict, rows)["status"])
        self.assertIn("STRICT_ACTION_WITHOUT_MATCHED_EXECUTOR_START",
                      audit(strict, rows)["errors"])

    def test_reject_cross_run_pairing(self):
        strict, rows = pair()
        rows[0]["data"]["run_id"] = "other-run"
        result = audit(strict, rows)
        self.assertIn("PAIRED_PROVENANCE_MISMATCH", result["errors"])

    def test_reject_fabricated_or_mutated_boundary(self):
        strict, rows = pair()
        rows[1]["state"] = {"combat": {"energy": 1}}
        self.assertIn("SIDECAR_DECISION_STATE_MISMATCH",
                      audit(strict, rows)["errors"])

    def test_reject_nonconsecutive_sidecar_order(self):
        strict, rows = pair()
        rows[2]["ordinal"] = 999
        self.assertIn("NONCONSECUTIVE_SIDECAR_ORDINAL",
                      audit(strict, rows)["errors"])


if __name__ == "__main__":
    unittest.main()
