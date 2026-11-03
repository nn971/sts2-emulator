"""Check native idle-frame to player-input exposure, without implying settlement."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_idle_async_exposure_audit import audit_idle_frame_exposures
from native_action_envelope_audit import audit
from test_native_phase_handoff_audit import event, sidecar
from test_native_awaited_turn_witness_audit import player_frame

CARD = {"kind": "play_card", "payload": {
    "card_instance_id": 4, "target_id": None,
}}
BASE = {"player": {"hp": 65}, "combat": {"turn": 1, "energy": 3}}


def checkpoint(ordinal=1, state=None):
    cp = player_frame(1)
    cp["ordinal"] = ordinal
    cp["state"] = copy.deepcopy(BASE if state is None else state)
    return cp


def row(kind, ordinal, **kwargs):
    return {"type": kind, "ordinal": ordinal, **kwargs}


def action(ordinal, state=None):
    return row("action_started", ordinal, action_id=1,
               canonical_action=copy.deepcopy(CARD),
               state=copy.deepcopy(BASE if state is None else state))


class IdleAsyncExposureAuditTests(unittest.TestCase):
    def test_source_idle_to_input_without_events_is_observed_not_settled(self):
        records = [
            checkpoint(),
            row("native_input_request", 2),
            action(3),
            row("action_completed", 4, action_id=1),
        ]
        result = audit_idle_frame_exposures(records)
        self.assertEqual("SOURCE_IDLE_TO_INPUT_ACTIVITY_OBSERVED_UNCERTIFIED",
                         result["status"])
        self.assertEqual(1, result["windows_reaching_next_input"])
        self.assertEqual(0, result["windows_with_preinput_tracked_activity"])
        self.assertEqual("UNCHANGED_FROM_IDLE_FRAME",
                         result["windows"][0]["pre_action_projection_relation"])
        self.assertFalse(result["native_settlement_certified"])
        self.assertFalse(result["source_async_task_inventory_exhaustive"])
        self.assertIn("HOOK_PLAYER_CHOICE_TASK_AFTER_PAUSE",
                      result["unobserved_task_classes"])

    def test_pre_input_rng_and_history_activity_is_exposed(self):
        state = copy.deepcopy(BASE)
        state["combat"]["energy"] = 2
        records = [
            checkpoint(),
            row("native_rng_boundary", 2, action_id=None),
            row("history_entry", 3, action_id=None),
            row("native_input_request", 4),
            action(5, state),
        ]
        result = audit_idle_frame_exposures(records)
        self.assertEqual(1, result["windows_with_preinput_tracked_activity"])
        self.assertEqual(1, result["windows_with_preinput_rng_calls"])
        w = result["windows"][0]
        self.assertEqual(2, w["preinput_first_event_ordinal"])
        self.assertEqual(1, w["preinput_event_counts"]["native_rng_boundary"])
        self.assertEqual(1, w["preinput_event_counts"]["history_entry"])
        self.assertEqual("DIFFERENT_BEFORE_EXECUTOR_ACTION",
                         w["pre_action_projection_relation"])
        self.assertEqual("$.combat.energy", w["first_projection_difference_path"])
        self.assertFalse(result["native_settlement_certified"])

    def test_task_completion_observed_between_idle_frame_and_input(self):
        records = [
            checkpoint(),
            row("native_async_task_lifecycle", 2, data={
                "task_id": 3, "family": "HOOK_TASK_BODY",
                "phase": "completed", "task_status": "RAN_TO_COMPLETION",
            }),
            row("native_input_request", 3),
            action(4),
        ]
        result = audit_idle_frame_exposures(records)
        self.assertEqual(1, result["windows_with_preinput_tracked_activity"])
        self.assertEqual(1, result["windows"][0]["preinput_event_counts"][
            "native_async_task_lifecycle"])
        self.assertEqual("TRACKED_SOURCE_ACTIVITY_OBSERVED_BEFORE_INPUT",
                         result["windows"][0]["preinput_activity_observation"])
        self.assertFalse(result["native_settlement_certified"])

    def test_activity_after_input_is_not_mislabeled_as_delayed_work(self):
        records = [
            checkpoint(),
            row("native_input_request", 2),
            row("native_rng_scalar_boundary", 3),
            row("history_entry", 4),
            action(5),
        ]
        result = audit_idle_frame_exposures(records)
        self.assertEqual(0, result["windows_with_preinput_rng_calls"])
        self.assertEqual(0, result["windows_with_preinput_tracked_activity"])
        self.assertEqual(2, result["windows"][0]["first_input_request_ordinal"])

    def test_no_input_marker_or_aborted_capture_is_not_forged_evidence(self):
        records = [checkpoint(), row("frame_state", 2), action(3)]
        result = audit_idle_frame_exposures(records)
        self.assertEqual(1, result[
            "windows_reaching_next_action_without_input_marker"])
        self.assertEqual(0, result["windows_reaching_next_input"])
        self.assertIsNone(result["windows"][0]["first_input_request_ordinal"])
        partial = audit_idle_frame_exposures(records[:2])
        self.assertEqual(1, partial["windows_unclosed"])
        self.assertEqual("UNOBSERVED",
                         partial["windows"][0]["pre_action_projection_relation"])
        self.assertFalse(partial["native_settlement_certified"])

    def test_automatic_action_before_input_is_recorded_separately(self):
        records = [
            checkpoint(),
            row("action_started", 2, action_id=99,
                canonical_action=None),
            row("action_completed", 3, action_id=99),
            row("native_input_request", 4),
            action(5),
        ]
        result = audit_idle_frame_exposures(records)
        self.assertEqual(1,
            result["windows"][0]["preinput_event_counts"]["action_started"])
        self.assertEqual(1, result["windows_with_preinput_tracked_activity"])
        self.assertEqual("CANONICAL_ACTION_OBSERVED",
                         result["windows"][0]["end_reason"])

    def test_multiple_player_frames_are_kept_distinct(self):
        records = [
            checkpoint(),
            action(2),
            checkpoint(3, {"combat": {"turn": 2}, "player": {"hp": 63}}),
            row("terminal", 4, data={"status": "OBSERVED"}),
        ]
        result = audit_idle_frame_exposures(records)
        self.assertEqual(2, result["player_idle_frames_observed"])
        self.assertEqual(1, result["windows_unclosed"])
        self.assertEqual(2, len(result["windows"]))

    def test_strict_envelope_audit_includes_uncertified_exposure(self):
        records = sidecar([
            event("TURN_STARTED", 1, "Player", "Play", "PlayPhase"),
            player_frame(1),
        ])
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        exposure = result["idle_async_exposures"]
        self.assertEqual(1, exposure["player_idle_frames_observed"])
        self.assertEqual(1, exposure["windows_unclosed"])
        self.assertFalse(exposure["native_settlement_certified"])


if __name__ == "__main__":
    unittest.main()
