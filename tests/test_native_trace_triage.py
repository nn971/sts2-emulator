"""Native trace directory triage must never conflate oracle evidence with parity."""
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_trace_triage import triage


def valid_trace():
    play = {"kind": "play_card", "payload": {
        "card_instance_id": 7, "target_id": "creature:2"}}
    end = {"kind": "end_turn", "payload": {}}
    before = {"player": {"hp": 70}, "combat": {
        "hand": [{"instance_id": 7, "model_id": "strike_silent"}]}}
    after = {"player": {"hp": 70}, "combat": {"hand": []}}
    return [
        {"type": "header", "trace_schema": "0.2", "capture_mode": "passive",
         "game_build": "synthetic-not-real"},
        {"type": "decision", "decision_id": 1, "phase": "Combat",
         "state_hash": "before", "event_seq": 0, "observation_revision": 1,
         "state": before, "legal_actions": [play],
         "coverage": {"verified_fields": ["$.player.hp", "$.combat.hand",
                                          "$.player.block", "$.combat.energy", "$.combat.creatures"]}},
        {"type": "action", "decision_id": 1, "action": play, "accepted": True, "origin": "human"},
        {"type": "transition", "decision_id": 1,
         "status": "COMPLETE", "before_hash": "before", "after_hash": "after",
         "first_event_seq": 1, "last_event_seq": 0},
        {"type": "decision", "decision_id": 2, "phase": "Combat",
         "state_hash": "after", "event_seq": 0, "observation_revision": 2,
         "state": after, "legal_actions": [end], "coverage": {}},
        {"type": "action", "decision_id": 2, "action": end, "accepted": True},
        {"type": "transition", "decision_id": 2, "status": "COMPLETE",
         "before_hash": "after", "after_hash": "after",
         "first_event_seq": 1, "last_event_seq": 0},
        {"type": "terminal", "status": "TERMINAL", "state": after,
         "reason": "synthetic"},
    ]


class TriageTests(unittest.TestCase):
    def test_selects_complete_native_strike_but_not_end_turn(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            (folder / "passive-good.jsonl").write_text(
                "\n".join(json.dumps(item) for item in valid_trace()) + "\n")
            report = triage(folder, folder / "fixtures")
            self.assertEqual(1, report["structurally_valid_count"])
            self.assertEqual(1, report["fixture_count"])
            self.assertEqual("strike", report["traces"][0]["fixtures"][0]["card_family"])
            file = folder / "fixtures" / "passive-good-decision-1.json"
            self.assertTrue(file.is_file())
            fixture = json.loads(file.read_text())
            self.assertEqual("RECORDED_NATIVE_EXPECTATION", fixture["status"])
            self.assertEqual("NOT_TESTED", fixture["emulator_parity"])
            self.assertFalse(fixture["ready_for_emulator_step"])

    def test_incomplete_trace_reports_reason_without_fabricating_fixture(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            records = valid_trace()
            del records[5]
            (folder / "passive-bad.jsonl").write_text(
                "\n".join(json.dumps(item) for item in records) + "\n")
            report = triage(folder, folder / "fixtures")
            self.assertEqual(0, report["structurally_valid_count"])
            self.assertEqual(0, report["fixture_count"])
            self.assertIn("no selected action", report["traces"][0]["reason"])
            self.assertFalse(list((folder / "fixtures").glob("*.json")))


if __name__ == "__main__":
    unittest.main()
