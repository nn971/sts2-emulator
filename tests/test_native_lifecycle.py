"""Strict native JSONL grammar tests; synthetic only."""
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_parity import load_trace, validate_lifecycle


def complete_fixture():
    state = {"player": {"hp": 50}, "combat": {"energy": 3}}
    coverage = {"verified_fields": ["$.player.hp"], "missing_fields": [],
                "unsupported_mechanics": [], "rng_status": "RNG_UNAUDITED",
                "required_preconditions": ["$.player.hp"], "legal_actions_complete": True,
                "verified_events": ["damage"]}
    action = {"kind": "end_turn", "payload": {}}
    return [
        {"type": "header", "trace_schema": "0.2", "game_build": "SYNTHETIC",
         "run_id": "test", "run_seed": "0", "bridge_version": "test"},
        {"type": "decision", "decision_id": 1, "event_seq": 0,
         "phase": "Combat", "state": state, "state_hash": "before-1",
         "legal_actions": [action], "observation_revision": 1,
         "observation_hash": "test", "coverage": coverage},
        {"type": "action", "decision_id": 1, "accepted": True, "action": action},
        {"type": "event", "event_seq": 1, "decision_id": 1, "kind": "damage", "payload": {"amount": 6}},
        {"type": "transition", "decision_id": 1, "status": "COMPLETE",
         "first_event_seq": 1, "last_event_seq": 1,
         "before_hash": "before-1", "after_hash": "after-1"},
        {"type": "terminal", "status": "SUCCESS", "reason": "synthetic",
         "state": {"player": {"hp": 44}, "combat": {"energy": 3}}},
    ]


class NativeLifecycleTests(unittest.TestCase):
    def test_valid_terminal_transition(self):
        self.assertIsNone(validate_lifecycle(complete_fixture()))

    def test_terminal_cannot_hide_unfinished_decision(self):
        lines = complete_fixture()
        lines.pop(4)
        self.assertIn("unresolved", validate_lifecycle(lines))

    def test_event_after_transition_fails(self):
        lines = complete_fixture()
        late = lines.pop(3)
        lines.insert(4, late)
        self.assertIn("last_event_seq", validate_lifecycle(lines))

    def test_duplicate_action_fails(self):
        lines = complete_fixture()
        lines.insert(3, copy.deepcopy(lines[2]))
        self.assertIn("Duplicate action", validate_lifecycle(lines))

    def test_boundary_cursor_is_verified(self):
        lines = complete_fixture()
        lines[1]["event_seq"] = 4
        self.assertIn("cursor", validate_lifecycle(lines))

    def test_transition_event_range_is_verified(self):
        lines = complete_fixture()
        lines[4]["last_event_seq"] = 0
        self.assertIn("last_event_seq", validate_lifecycle(lines))

    def test_transition_post_hash_links_to_next_decision(self):
        lines = complete_fixture()
        lines.pop()  # replace terminal with a second complete decision
        lines.extend([
            {"type": "decision", "decision_id": 2, "event_seq": 1,
             "state": {"player": {"hp": 44}}, "state_hash": "not-after-1"},
            {"type": "action", "decision_id": 2, "accepted": True, "action": {"kind": "end_turn", "payload": {}}},
            {"type": "transition", "decision_id": 2, "status": "COMPLETE",
             "first_event_seq": 2, "last_event_seq": 1,
             "before_hash": "not-after-1", "after_hash": "after-2"},
            {"type": "terminal", "status": "SUCCESS", "reason": "synthetic"},
        ])
        self.assertIn("hash", validate_lifecycle(lines))

    def test_loader_rejects_terminal_marker_after_unfinished_decision(self):
        lines = complete_fixture()
        lines.pop(4)
        with tempfile.TemporaryDirectory() as dirname:
            path = Path(dirname) / "truncated.jsonl"
            path.write_text("\n".join(json.dumps(x) for x in lines) + "\n")
            _, error = load_trace(path)
            self.assertIn("unresolved", error)


if __name__ == "__main__":
    unittest.main()
