"""Pinned-v0.111 passive async task lifecycle source consistency tests."""
from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_async_task_lifecycle_audit import audit_native_task_lifecycle
from native_action_envelope_audit import audit
from test_native_phase_handoff_audit import sidecar, event
from test_native_awaited_turn_witness_audit import player_frame

BASIS = "PASSIVE_TASK_REFERENCE_POLLED_AT_FRAME"
METHOD = "GameAction._executionTask"
FAMILY = "GAME_ACTION_BODY"


def header(enabled=True):
    return {"type": "header", "data": {
        "schema": "native-action-envelope-v1",
        "native_async_task_hook_installed": enabled}}


def task(phase, task_id=1, *, status="PENDING", family=FAMILY):
    return {"type": "native_async_task_lifecycle", "data": {
        "task_id": task_id, "family": family, "source": METHOD,
        "phase": phase, "task_status": status,
        "source_version": "v0.111.0",
        "observation_basis": BASIS,
        "native_task_mutations": 0,
        "native_settlement_certified": False}}


def checkpoint(site, *, turn=None, pending_ids=(), n_registered=1,
               n_completed=0, overflow=0, family=FAMILY):
    return {"type": "native_async_task_checkpoint", "data": {
        "site": site, "turn": turn,
        "tracked_pending_task_count": len(pending_ids),
        "tracked_pending_by_family":
            {family: len(pending_ids)} if pending_ids else {},
        "registered_task_count": n_registered,
        "observed_completed_task_count": n_completed,
        "registration_overflow": overflow,
        "pending_task_ids": list(pending_ids),
        "source_version": "v0.111.0",
        "observation_basis": BASIS,
        "coverage": "NAMED_ASYNC_ENTRYPOINTS_ONLY",
        "native_settlement_certified": False,
        "exhaustive_native_task_inventory": False,
        "native_task_mutations": 0}}


def make_records(*events, enabled=True):
    rows = [header(enabled), *copy.deepcopy(events),
            {"type": "terminal", "data": {
                "status": "OBSERVED", "open_actions": []}}]
    for i,r in enumerate(rows,1):
        r["ordinal"] = i
    return rows


class AsyncTaskLifecycleAuditTests(unittest.TestCase):
    def test_task_remains_pending_through_frame_then_completes(self):
        rows = make_records(
            task("registered"),
            player_frame(1),
            checkpoint("PLAYER_TURN_START", turn=1, pending_ids=(1,)),
            task("completed", status="RAN_TO_COMPLETION"),
            checkpoint("COMBAT_TEARDOWN", n_registered=1, n_completed=1),
        )
        report = audit_native_task_lifecycle(rows)
        self.assertEqual("SOURCE_TRACKED_TASK_LIFECYCLE_OBSERVED_UNCERTIFIED",
                         report["status"])
        self.assertEqual(1, report["task_references_registered"])
        self.assertEqual(1, report["task_references_observed_complete"])
        self.assertEqual(1, report["player_checkpoint_count"])
        self.assertEqual(0, report["pending_at_teardown"])
        self.assertEqual(1, report["player_checkpoint_covered_counts"][0][
            "tracked_pending_task_count"])
        self.assertFalse(report["native_settlement_certified"])
        self.assertFalse(report["source_task_inventory_exhaustive"])

    def test_pending_at_teardown_is_valid_without_claiming_settlement(self):
        rows = make_records(
            task("registered", family=FAMILY),
            player_frame(1),
            checkpoint("PLAYER_TURN_START", turn=1, pending_ids=(1,)),
            checkpoint("COMBAT_TEARDOWN", pending_ids=(1,)),
        )
        report = audit_native_task_lifecycle(rows)
        self.assertEqual(1, report["pending_at_teardown"])
        self.assertEqual("SOURCE_TRACKED_TASK_LIFECYCLE_OBSERVED_UNCERTIFIED",
                         report["status"])

    def test_legacy_sidecars_are_unmodified_when_hook_is_disabled(self):
        rows = make_records(player_frame(1), enabled=False)
        self.assertEqual("NOT_CAPTURED_OPTIONAL",
                         audit_native_task_lifecycle(rows)["status"])
        # A legacy turn checkpoint remains an incomplete-but-valid witness,
        # regardless of absent optional Task hooks.
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", audit(rows)["status"])

    def test_injected_events_with_disabled_hook_are_invalid(self):
        rows = make_records(task("registered"), enabled=False)
        report = audit_native_task_lifecycle(rows)
        self.assertEqual("INVALID_NATIVE_TASK_LIFECYCLE", report["status"])
        self.assertTrue(report["errors"])

    def test_checkpoint_must_immediately_follow_matching_player_frame(self):
        rows = make_records(
            task("registered"),
            player_frame(1),
            {"type": "frame_state", "data": {"frame_index": 1}},
            checkpoint("PLAYER_TURN_START", turn=1, pending_ids=(1,)),
            checkpoint("COMBAT_TEARDOWN", pending_ids=(1,)))
        report = audit_native_task_lifecycle(rows)
        self.assertEqual("INVALID_NATIVE_TASK_LIFECYCLE", report["status"])
        self.assertTrue(any("not paired" in x for x in report["errors"]))

    def test_wrong_pending_count_or_forged_settlement_fails_closed(self):
        rows = make_records(
            task("registered"),
            player_frame(1),
            checkpoint("PLAYER_TURN_START", turn=1, pending_ids=(1,)),
            checkpoint("COMBAT_TEARDOWN", pending_ids=(1,)))
        for mutation in ("missing_id", "wrong_count", "forged_settlement",
                         "wrong_epoch", "wrong_coverage", "bad_status",
                         "wrong_family", "completion_without_registration",
                         "missing_player_checkpoint"):
            broken = copy.deepcopy(rows)
            cp = broken[3]["data"]
            if mutation == "missing_id":
                cp["pending_task_ids"] = []
            elif mutation == "wrong_count":
                cp["registered_task_count"] = 2
            elif mutation == "forged_settlement":
                cp["native_settlement_certified"] = True
            elif mutation == "wrong_epoch":
                cp["source_version"] = "v0.112"
            elif mutation == "wrong_coverage":
                cp["coverage"] = "ALL_NATIVE_TASKS"
            elif mutation == "bad_status":
                broken[1]["data"]["task_status"] = "SETTLED"
            elif mutation == "wrong_family":
                broken[1]["data"]["family"] = "MUTATED_GAME_ACTION"
            elif mutation == "completion_without_registration":
                broken[1]["data"]["phase"] = "completed"
                broken[1]["data"]["task_status"] = "RAN_TO_COMPLETION"
            else:
                del broken[3]
            with self.subTest(mutation=mutation):
                if mutation == "missing_player_checkpoint":
                    for j,row in enumerate(broken,1):
                        row["ordinal"] = j
                self.assertEqual("INVALID_NATIVE_TASK_LIFECYCLE",
                                 audit_native_task_lifecycle(broken)["status"])

    def test_capacity_exhaustion_is_explicitly_partial(self):
        rows = make_records(
            task("registered"),
            player_frame(1),
            checkpoint("PLAYER_TURN_START", turn=1, pending_ids=(1,),
                       overflow=3),
            checkpoint("COMBAT_TEARDOWN", pending_ids=(1,), overflow=3))
        report = audit_native_task_lifecycle(rows)
        self.assertEqual("SOURCE_TRACKED_TASK_LIFECYCLE_PARTIAL",
                         report["status"])
        self.assertEqual(3, report["registration_overflow"])
        self.assertFalse(report["source_task_inventory_exhaustive"])


if __name__ == "__main__":
    unittest.main()
