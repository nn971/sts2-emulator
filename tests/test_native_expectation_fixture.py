"""Native expectation extraction uses synthetic fixtures, never parity claims."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_expectation_fixture import extract
from test_native_parity import sample


def fixture():
    data = sample()
    data[0]["capture_mode"] = "passive"
    card = {"instance_id": 7, "model_id": "strike_silent", "upgrades": 0}
    data[1]["state"]["combat"]["hand"] = [card]
    data[5]["state"]["combat"]["hand"] = []
    data[1]["legal_actions"] = [
        {"kind": "play_card", "payload": {"card_instance_id": 7, "target_id": "creature:1"}}
    ]
    data[2]["action"] = data[1]["legal_actions"][0]
    data[4]["status"] = "COMPLETE"
    return data


class NativeExpectationTests(unittest.TestCase):
    def test_strike_native_expectation_is_not_emulator_parity(self):
        result = extract(fixture(), 1)
        self.assertEqual("RECORDED_NATIVE_EXPECTATION", result["status"])
        self.assertEqual("strike", result["card_family"])
        self.assertEqual("NOT_TESTED", result["emulator_parity"])
        self.assertFalse(result["ready_for_emulator_step"])
        self.assertEqual("UNIMPLEMENTED", result["next_check"]["emulator_import"])
        self.assertEqual(1, len(result["native_events"]))

    def test_defend_selection(self):
        data = fixture()
        data[1]["state"]["combat"]["hand"][0]["model_id"] = "defend_silent"
        data[1]["legal_actions"][0]["payload"]["target_id"] = None
        data[2]["action"]["payload"]["target_id"] = None
        self.assertEqual("defend", extract(data, 1)["card_family"])

    def test_invalid_selected_card_does_not_invent_a_snapshot(self):
        data = fixture()
        data[2]["action"]["payload"]["card_instance_id"] = 999
        result = extract(data, 1)
        self.assertEqual("UNSUPPORTED", result["status"])

    def test_requires_post_boundary(self):
        data = fixture()
        data = data[:5] + [data[-1]]
        self.assertEqual("UNSUPPORTED", extract(data, 1)["status"])


if __name__ == "__main__":
    unittest.main()
