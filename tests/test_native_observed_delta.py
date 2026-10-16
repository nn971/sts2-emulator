"""Synthetic native observed deltas must report checkpoints, not claim replay."""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_observed_delta import observed_delta


class ObservedDeltaTests(unittest.TestCase):
    @staticmethod
    def fixture():
        return {
            "status": "RECORDED_NATIVE_EXPECTATION",
            "decision_id": 1,
            "card_family": "strike",
            "selected_action": {"kind": "play_card", "payload": {
                "card_instance_id": 7, "target_id": "creature:1"}},
            "pre_state": {
                "player": {"hp": 70, "block": 0},
                "combat": {
                    "energy": 3, "turn": 1,
                    "creatures": [{"id": "creature:1", "model_id": "TEST",
                                   "hp": 20, "block": 0, "alive": True}],
                    "hand": [{"instance_id": 7, "model_id": "STRIKE_SILENT"}],
                    "draw_pile": [], "discard_pile": [], "exhaust_pile": [],
                },
            },
            "post_state": {
                "player": {"hp": 70, "block": 0},
                "combat": {
                    "energy": 2, "turn": 1,
                    "creatures": [{"id": "creature:1", "model_id": "TEST",
                                   "hp": 14, "block": 0, "alive": True}],
                    "hand": [], "draw_pile": [],
                    "discard_pile": [{"instance_id": 7, "model_id": "STRIKE_SILENT"}],
                    "exhaust_pile": [],
                },
            },
            "native_events": [{"kind": "test_damage", "event_seq": 1}],
        }

    def test_observations_do_not_claim_emulator_parity(self):
        report = observed_delta(self.fixture())
        self.assertEqual("NOT_TESTED", report["emulator_parity"])
        self.assertEqual("NATIVE_CHECKPOINT_OBSERVATION_ONLY", report["comparison"])
        self.assertEqual(-1, report["scalars"]["combat.energy"]["delta"])
        self.assertEqual(-6, report["creatures"]["creature:1"]["changes"]["hp"]["delta"])
        self.assertEqual({"instance_id": 7, "from": "hand", "to": "discard_pile"},
                         report["card_movements"][0])
        self.assertEqual(["test_damage"], report["event_kinds"])

    def test_unobserved_scalars_are_not_invented(self):
        fixture = self.fixture()
        del fixture["post_state"]["combat"]["energy"]
        report = observed_delta(fixture)
        self.assertNotIn("combat.energy", report["scalars"])

    def test_rejects_non_native_expectation(self):
        fixture = self.fixture()
        fixture["status"] = "UNSUPPORTED"
        with self.assertRaises(ValueError):
            observed_delta(fixture)


if __name__ == "__main__":
    unittest.main()
