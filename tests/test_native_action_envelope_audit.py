"""Non-game regression tests for the all-action native capture sidecar."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_action_envelope_audit import audit, diff_scalars, infer_choice_discard


def sample():
    return [
        {"type": "header", "ordinal": 1,
         "data": {"schema": "native-action-envelope-v1"}},
        {"type": "history_baseline", "ordinal": 2,
         "data": {"existing_entry_count": 3}},
        {"type": "action_started", "ordinal": 3,
         "action_id": 1, "parent_action_id": None,
         "native_type": "PlayCardAction",
         "canonical_action": {"kind": "play_card", "payload": {"card_instance_id": 0}},
         "state": {"player": {"hp": 50, "block": 0},
                   "combat": {"energy": 3, "creatures": [],
                              "hand": [{"instance_id": 0}],
                              "draw_pile": [], "discard_pile": [], "exhaust_pile": []}}},
        {"type": "history_entry", "ordinal": 4, "action_id": 1,
         "data": {"history_index": 3, "missing_fields": [], "payload": {"amount": 1}}},
        {"type": "nested_choice_pause", "ordinal": 5, "action_id": 1,
         "state": {"player": {"hp": 50}, "combat": {"energy": 2}}},
        {"type": "action_started", "ordinal": 6, "action_id": 2,
         "parent_action_id": 1, "native_type": "AutomaticAction", "state": {}},
        {"type": "action_completed", "ordinal": 7, "action_id": 2,
         "state": {}},
        {"type": "action_completed", "ordinal": 8, "action_id": 1,
         "state": {"player": {"hp": 50, "block": 0},
                   "combat": {"energy": 2, "creatures": [],
                              "hand": [], "draw_pile": [],
                              "discard_pile": [{"instance_id": 0}],
                              "exhaust_pile": []}}},
        {"type": "terminal", "ordinal": 9, "data": {
            "status": "OBSERVED", "open_actions": [], "native_game_parity": "NOT_TESTED"}},
    ]


class EnvelopeAuditTests(unittest.TestCase):
    def test_valid_nested_action_window_and_measured_delta(self):
        report = audit(sample())
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual("NOT_TESTED", report["native_game_parity"])
        self.assertEqual(1, report["nested_choice_pauses"])
        self.assertEqual(2, len(report["actions"]))
        action = report["actions"][0]
        self.assertEqual({"before": 3, "after": 2},
                         action["observed_delta"]["energy"])
        self.assertEqual({"before": [0], "after": []},
                         action["observed_delta"]["zones"]["hand"])

    def test_true_action_resumption_and_terminal_cleanup_are_valid(self):
        records = sample()
        records.insert(5, {"type": "action_resumed", "action_id": 1,
                           "native_type": "PlayCardAction",
                           "state": {"combat": {"energy": 2}}})
        records.insert(-1, {"type": "history_reset", "data": {
            "previous_count": 5, "new_count": 0}})
        records[-1]["data"]["reason"] = "Combat ended"
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["action_resumptions"])
        self.assertEqual(1, result["terminal_history_clears"])

    def test_legacy_choice_resume_is_qualified_not_rewritten(self):
        records = sample()
        records.insert(5, {"type": "anomaly", "data": {
            "reason": "duplicate_action_start", "detail": "PlayCardAction"}})
        records.insert(-1, {"type": "history_reset", "data": {
            "previous_count": 5, "new_count": 0}})
        records[-1]["data"].update({"status": "INCOMPLETE_CAPTURE",
                                     "reason": "Combat ended"})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("LEGACY_CALLBACK_SEQUENCE_RECOVERABLE", result["status"])
        self.assertEqual("INCOMPLETE_CAPTURE", result["raw_terminal_status"])
        self.assertEqual(1, result["legacy_resume_candidates"])
        self.assertEqual(1, result["terminal_history_clears"])

    def test_duplicate_start_without_choice_cannot_be_recovered(self):
        records = sample()
        records.insert(3, {"type": "anomaly", "data": {
            "reason": "duplicate_action_start", "detail": "PlayCardAction"}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_history_reset_during_live_action_is_incomplete(self):
        records = sample()
        records.insert(4, {"type": "history_reset", "data": {
            "previous_count": 4, "new_count": 0}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_frame_hp_delta_records_enemy_damage_between_callbacks(self):
        records = sample()
        state1 = {"player": {"hp": 45}, "combat": {"energy": 2}}
        state2 = {"player": {"hp": 40}, "combat": {"energy": 2}}
        records.insert(-1, {"type": "frame_state", "state": state1,
                            "data": {"frame_index": 1, "state_hash": "a"}})
        records.insert(-1, {"type": "frame_state", "state": state2,
                            "data": {"frame_index": 2, "state_hash": "b"}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["observed_mechanic_indicators"]
                         ["player_hp_change"]["count"])

    def test_missing_action_end_is_fail_closed(self):
        records = sample()
        records.pop(7)
        records[-1]["data"]["open_actions"] = [1]
        for i, x in enumerate(records):
            x["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_unmatched_action_end_is_rejected(self):
        records = sample()
        records[6]["action_id"] = 999
        self.assertIn("no action start", audit(records)["reason"])

    def test_nonmonotone_history_detected(self):
        records = sample()
        records[3]["data"]["history_index"] = 7
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_missing_observation_explicitly_incomplete(self):
        records = sample()
        records.insert(5, {"type": "observation_failure",
                           "data": {"stage": "before_action"}})
        for i, x in enumerate(records):
            x["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_nested_discard_candidate_remains_explicitly_unverified(self):
        before = {"combat": {"hand": [
            {"instance_id": 2}, {"instance_id": 5}],
            "discard_pile": [{"instance_id": 0}]}}
        after = {"combat": {"hand": [{"instance_id": 5}],
            "discard_pile": [{"instance_id": 0}, {"instance_id": 2}]}}
        inference = infer_choice_discard(before, after)
        self.assertEqual(2, inference["candidate_discarded_card_instance_id"])
        self.assertFalse(inference["semantic_selection_verified"])
        self.assertEqual("INFERRED_FROM_CARD_ZONE_DIFFERENCE",
                         inference["provenance"])

    def test_observed_mechanics_never_claim_parity(self):
        result = audit(sample())
        indicators = result["observed_mechanic_indicators"]
        self.assertEqual(1, indicators["card_play"]["count"])
        self.assertEqual(1, indicators["energy_change"]["count"])
        self.assertEqual("UNOBSERVED", indicators["potion_history_entry"]["status"])
        self.assertEqual("NOT_TESTED", result["native_source_hook_order"])

    def test_effect_delta_is_independent_of_native_rng_seed(self):
        pre = {"player": {"hp": 50}, "combat": {"energy": 3, "creatures": [],
               "hand": [], "draw_pile": [], "discard_pile": [],
               "exhaust_pile": []}}
        post = copy.deepcopy(pre)
        post["combat"]["energy"] = 2
        self.assertEqual({"energy": {"before": 3, "after": 2}},
                         diff_scalars(pre, post))


if __name__ == "__main__":
    unittest.main()
