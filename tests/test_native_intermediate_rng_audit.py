"""Game-free checks for 15-stream RNG windows across native action callbacks."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_intermediate_rng_audit import (
    compare_source_rng_window, audit_native_rng_windows,
)


def capture(ordinal, phase, action_id=None, *, shift=0):
    streams = []
    for i in range(15):
        streams.append({
            "stream_id": f"run.stream_{i}",
            "owner_id": "run",
            "cursor": {
                "Counter": i + shift,
                "State0": 1 + i, "State1": 2 + i,
                "State2": 3 + i, "State3": 4 + i,
            },
        })
    return {
        "type": "native_rng_stream_checkpoint", "ordinal": ordinal,
        "action_id": action_id,
        "data": {
            "phase": phase, "source": "native_rng_object_to_serializable",
            "scope": "15_persistent_streams_in_observed_combat",
            "rng_draws_performed": 0, "complete_native_rng_inventory": False,
            "exact_rng_parity": "NOT_TESTED", "streams": streams,
        },
    }


def action(kind, ordinal, aid, state=None, canonical=None):
    return {
        "type": kind, "ordinal": ordinal, "action_id": aid,
        "canonical_action": canonical, "state": copy.deepcopy(state),
    }


def stable_records():
    state = {"player": {"hp": 50}, "combat": {"turn": 1, "energy": 2}}
    return [
        action("action_started", 1, 1, state,
               {"kind": "play_card", "payload": {"card_instance_id": 1}}),
        action("action_completed", 3, 1, state),
        capture(4, "after_action"),
        action("action_started", 7, 2, state,
               {"kind": "end_turn", "payload": {}}),
        capture(8, "before_action", 2),
        action("action_completed", 9, 2, state),
    ]


class NativeIntermediateRngAuditTests(unittest.TestCase):
    def test_matched_native_15_stream_windows_are_bounded(self):
        rows = stable_records()
        status, cursor = compare_source_rng_window(
            rows[2], rows[4], completion_ordinal=3,
            next_start_ordinal=7, next_action_id=2)
        self.assertEqual("SOURCE_15_TRACKED_RNG_STREAMS_STABLE", status)
        self.assertEqual(15, len(cursor))
        report = audit_native_rng_windows(rows)
        self.assertEqual(1, report["source_projection_stable_windows"])
        self.assertEqual(1, report["source_15_streams_stable_windows"])
        self.assertFalse(report["source_rng_streams_exhaustive"])
        self.assertEqual("NOT_TESTED", report["emulator_rng_parity"])
        self.assertFalse(report["native_settlement_certified"])
        self.assertNotIn("streams", report["windows"][0])

    def test_changed_cursor_is_neither_stable_nor_emulator_parity(self):
        rows = stable_records()
        rows[4]["data"]["streams"][1]["cursor"]["Counter"] += 1
        report = audit_native_rng_windows(rows)
        self.assertEqual(0, report["source_15_streams_stable_windows"])
        self.assertEqual(1, report["source_rng_changed_windows"])
        self.assertEqual("SOURCE_RNG_CHANGED_BETWEEN_CALLBACKS",
                         report["windows"][0]["rng_status"])

    def test_wrong_phase_owner_ordinal_or_incomplete_inventory_is_unverifiable(self):
        rows = stable_records()
        for mutate in (
            lambda r: r["data"].update(phase="before_action"),
            lambda r: r.update(action_id=99),
            lambda r: r.update(ordinal=10),
            lambda r: r["data"]["streams"].pop(),
            lambda r: r["data"]["streams"][1].update(stream_id="run.stream_0"),
            lambda r: r["data"]["streams"][1]["cursor"].update(Counter=-1),
            lambda r: r["data"]["streams"][1]["cursor"].update(State0="1"),
        ):
            bad = copy.deepcopy(rows)
            mutate(bad[4])
            status, cursor = compare_source_rng_window(
                bad[2], bad[4], completion_ordinal=3,
                next_start_ordinal=7, next_action_id=2)
            self.assertEqual("SOURCE_RNG_WINDOW_UNVERIFIED", status)
            self.assertIsNone(cursor)

    def test_missing_post_or_pre_snapshots_are_unverifiable(self):
        rows = stable_records()
        for index in (2, 4):
            truncated = [row for j, row in enumerate(rows) if j != index]
            summary = audit_native_rng_windows(truncated)
            self.assertEqual(1, summary["source_rng_unverifiable_windows"])
            self.assertEqual(0, summary["source_15_streams_stable_windows"])

    def test_changed_public_projection_does_not_claim_stability(self):
        rows = stable_records()
        rows[3]["state"]["player"]["hp"] = 49
        report = audit_native_rng_windows(rows)
        self.assertEqual(0, report["source_projection_stable_windows"])
        self.assertEqual("NOT_CAPTURED", report["status"])


if __name__ == "__main__":
    unittest.main()
