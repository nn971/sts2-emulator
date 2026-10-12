"""Non-game regression tests for the all-action native capture sidecar."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_action_envelope_audit import audit, diff_scalars


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
