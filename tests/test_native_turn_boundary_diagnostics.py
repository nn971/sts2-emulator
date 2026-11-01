"""Offline native turn-boundary diagnostics: source evidence is not parity."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_turn_boundary_diagnostics import analyze


def fixture():
    pre = {"player": {"hp": 50},
           "combat": {"turn": 1, "energy": 3, "hand": []}}
    ended = {"player": {"hp": 50},
             "combat": {"turn": 1, "energy": 0, "hand": []}}
    action = {"kind": "end_turn", "payload": {}}
    rows = [
        {"type": "header", "data": {"schema": "native-action-envelope-v1"}},
        {"type": "frame_state", "state": copy.deepcopy(pre),
         "data": {"frame_index": 1, "state_hash": "before-first-input"}},
        {"type": "native_input_request", "native_type": "EndPlayerTurnAction",
         "canonical_action": action, "state": copy.deepcopy(pre),
         "data": {"input_request_id": 1,
                  "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
                  "native_acceptance": "UNVERIFIED",
                  "human_origin": "UNVERIFIED",
                  "input_injected": False}},
        {"type": "action_started", "action_id": 1,
         "native_type": "EndPlayerTurnAction",
         "canonical_action": action, "state": copy.deepcopy(pre),
         "data": {"input_request_id": 1,
                  "request_execution_link": "SAME_NATIVE_ACTION_OBJECT",
                  "human_origin": "UNVERIFIED"}},
        {"type": "action_completed", "action_id": 1,
         "state": copy.deepcopy(ended)},
        {"type": "action_started", "action_id": 2,
         "native_type": "ReadyToBeginEnemyTurnAction",
         "state": copy.deepcopy(ended)},
        {"type": "action_completed", "action_id": 2,
         "state": copy.deepcopy(ended)},
        {"type": "terminal",
         "data": {"status": "OBSERVED", "open_actions": []}},
    ]
    for i, row in enumerate(rows, 1):
        row["ordinal"] = i
    return rows


class NativeTurnBoundaryDiagnosticsTests(unittest.TestCase):
    def test_player_and_enemy_candidates_source_only(self):
        report = analyze(fixture())
        self.assertEqual("SOURCE_BOUNDARY_CANDIDATES_OBSERVED",
                         report["status"])
        p = self.assert_single(report["player_turn_starts"])
        self.assertEqual(2, p["source_frame_ordinal"])
        self.assertEqual(3, p["first_player_request_ordinal"])
        self.assertTrue(p["frame_projection_equals_request"])
        self.assertTrue(p["source_executor_idle_at_frame"])
        self.assertEqual("SOURCE_FRAME_REQUEST_PROJECTIONS_AGREE",
                         p["candidate_status"])
        e = self.assert_single(report["enemy_turn_starts"])
        self.assertEqual(6, e["source_pre_executor_ordinal"])
        self.assertEqual(5, e["previous_completed_executor_ordinal"])
        self.assertTrue(e["previous_executor_was_end_turn"])
        self.assertTrue(e["no_other_executor_active"])
        self.assertEqual("SOURCE_PRE_ENEMY_SETUP_OBSERVED",
                         e["candidate_status"])
        self.assertEqual("NOT_VERIFIED",
                         e["atomic_before_first_enemy_effect"])
        self.assertEqual("NOT_VERIFIED", report["native_settlement"])
        self.assertEqual(0, report["parity_checkpoints"])
        self.assertEqual("NOT_TESTED", report["emulator_parity"])

    @staticmethod
    def assert_single(seq):
        assert len(seq) == 1, seq
        return seq[0]

    def test_player_frame_mismatch_remains_ambiguous(self):
        rows = fixture()
        rows[2]["state"]["player"]["hp"] = 49
        report = analyze(rows)
        p = self.assert_single(report["player_turn_starts"])
        self.assertEqual("UNVERIFIED_OR_AMBIGUOUS", p["candidate_status"])
        self.assertEqual(0, report["parity_checkpoints"])

    def test_non_end_turn_auto_action_remains_ambiguous(self):
        rows = fixture()
        rows[3]["native_type"] = "PlayCardAction"
        rows[2]["native_type"] = "PlayCardAction"
        rows[3]["canonical_action"] = {"kind": "play_card", "payload": {}}
        rows[2]["canonical_action"] = {"kind": "play_card", "payload": {}}
        result = analyze(rows)
        e = self.assert_single(result["enemy_turn_starts"])
        self.assertFalse(e["previous_executor_was_end_turn"])
        self.assertEqual("UNVERIFIED_OR_AMBIGUOUS", e["candidate_status"])

    def test_incomplete_source_is_blocked(self):
        rows = fixture()
        rows[-1]["data"]["status"] = "INCOMPLETE_CAPTURE"
        result = analyze(rows)
        self.assertEqual("SOURCE_AUDIT_BLOCKED", result["status"])
        self.assertEqual([], result["player_turn_starts"])


if __name__ == "__main__":
    unittest.main()
