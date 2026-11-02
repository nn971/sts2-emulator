"""Focused, game-free checks for source-only post-card continuity evidence."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_action_window_audit import audit_action_windows


def row(kind, ordinal, action_id=None, state=None, action=None):
    return {
        "type": kind, "ordinal": ordinal, "action_id": action_id,
        "state": copy.deepcopy(state), "canonical_action": action,
    }


def source(turn=1, hp=40):
    return {
        "player": {"hp": 56, "max_hp": 70, "block": 0},
        "combat": {
            "turn": turn, "energy": 2,
            "creatures": [{"id": "creature:0", "hp": 56},
                          {"id": "creature:1", "hp": hp}],
            "hand": [{"instance_id": 1}], "draw_pile": [],
            "discard_pile": [], "exhaust_pile": [],
        }
    }


CARD = {"kind": "play_card",
        "payload": {"card_instance_id": 1, "target_id": "creature:1"}}
END = {"kind": "end_turn", "payload": {}}


class NativeActionWindowAuditTests(unittest.TestCase):
    def test_completed_card_to_next_action_is_a_bounded_source_window(self):
        a, b = source(hp=40), source(hp=34)
        records = [
            row("action_started", 1, 1, a, CARD),
            row("nested_choice_pause", 2, 1, a),
            row("action_completed", 3, 1, b),
            row("frame_state", 4, None, b),
            row("action_started", 5, 2, b, END),
            row("action_completed", 6, 2, b),
        ]
        report = audit_action_windows(records)
        self.assertEqual("NATIVE_POST_CARD_WINDOWS_OBSERVED_UNCERTIFIED",
                         report["status"])
        self.assertEqual(1, report["source_completed_card_actions"])
        self.assertEqual(1, report["stable_post_card_windows"])
        self.assertEqual(1, report["nested_choice_stable_windows"])
        self.assertEqual(0, report["changed_post_card_windows"])
        self.assertIsNone(report["source_windows"][0]["first_source_difference_path"])
        self.assertFalse(report["native_settlement_certified"])
        self.assertEqual("NOT_TESTED", report["emulator_action_projection_parity"])

    def test_intervening_source_change_is_visible_but_not_a_forged_error(self):
        a, b, c = source(hp=40), source(hp=34), source(hp=30)
        records = [
            row("action_started", 1, 1, a, CARD),
            row("action_completed", 2, 1, b),
            row("action_started", 3, 2, c, CARD),
            row("action_completed", 4, 2, c),
        ]
        report = audit_action_windows(records)
        self.assertEqual(1, report["changed_post_card_windows"])
        self.assertEqual("$.combat.creatures[1].hp",
                         report["source_windows"][0]["first_source_difference_path"])
        self.assertEqual(1, report["unpaired_post_card_windows"])
        self.assertFalse(report["native_settlement_certified"])

    def test_phase_change_or_cross_turn_is_deferred(self):
        a, b = source(), source(turn=2)
        records = [
            row("action_started", 1, 1, a, CARD),
            row("action_completed", 2, 1, a),
            row("native_phase_handoff", 3),
            row("action_started", 4, 2, a, END),
            row("action_completed", 5, 2, a),
        ]
        self.assertEqual(1, audit_action_windows(records)[
            "unpaired_post_card_windows"])
        records.pop(2)
        records[2]["state"] = b
        self.assertEqual(1, audit_action_windows(records)[
            "unpaired_post_card_windows"])

    def test_missing_or_overlapping_action_completion_fails_observation_only(self):
        a = source()
        records = [
            row("action_started", 1, 1, a, CARD),
            row("action_started", 2, 2, a, END),
            row("action_completed", 3, 1, a),
        ]
        result = audit_action_windows(records)
        self.assertEqual(1, result["unverifiable_post_card_windows"])
        self.assertEqual(0, result["stable_post_card_windows"])
        self.assertFalse(result["native_settlement_certified"])
        records[2]["ordinal"] = 1
        result = audit_action_windows(records)
        self.assertEqual(1, result["unverifiable_post_card_windows"])


if __name__ == "__main__":
    unittest.main()
