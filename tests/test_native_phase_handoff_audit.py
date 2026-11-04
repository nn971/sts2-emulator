"""Offline adversarial tests of pinned CombatManager phase chronology."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_phase_handoff_audit import audit_phase_handoffs
from native_action_envelope_audit import audit


def event(name, turn, side, phase, sync, *, one=False, two=False,
          enemy=False):
    callback = {
        "ABOUT_TO_SWITCH_TO_ENEMY_TURN": "CombatManager.AboutToSwitchToEnemyTurn",
        "SIDE_SWITCHED": "CombatManager.TurnEnded",
        "TURN_STARTED": "CombatManager.TurnStarted",
    }[name]
    return {"type": "native_phase_handoff", "data": {
        "source_event": name, "source_callback": callback,
        "turn": turn, "native_side": side, "player_phase": phase,
        "synchronizer_state": sync,
        "ending_player_turn_phase_one": one,
        "ending_player_turn_phase_two": two,
        "enemy_turn_started": enemy,
        "source_version": "v0.111.0",
        "observation_basis": "NATIVE_COMBAT_MANAGER_CALLBACK",
        "settlement_status": "SETTLEMENT_UNVERIFIED",
        "native_phase_handoff_order_verified": False,
        "rng_draws_performed_by_inspector": 0}}


def full_cycle():
    return [
        event("TURN_STARTED", 1, "Player", "Play", "PlayPhase"),
        event("ABOUT_TO_SWITCH_TO_ENEMY_TURN", 1, "Player", "End",
              "NotPlayPhase", two=True),
        event("SIDE_SWITCHED", 1, "Enemy", "End", "NotPlayPhase", two=True),
        event("TURN_STARTED", 1, "Enemy", "None", "NotPlayPhase", enemy=True),
        event("SIDE_SWITCHED", 2, "Player", "None", "NotPlayPhase", enemy=True),
        event("TURN_STARTED", 2, "Player", "Play", "PlayPhase"),
    ]


def sidecar(events):
    records = [
        {"type": "header", "data": {"schema": "native-action-envelope-v1"}},
        *events,
        {"type": "terminal", "data": {
            "status": "OBSERVED", "open_actions": []}}]
    for ordinal, row in enumerate(records, 1):
        row["ordinal"] = ordinal
    return records


class PhaseHandoffAuditTests(unittest.TestCase):
    def test_normal_round_is_source_ordered_but_not_settlement_certified(self):
        records = sidecar(full_cycle())
        result = audit_phase_handoffs(records)
        self.assertEqual("SOURCE_PHASE_HANDOFF_ORDER_OBSERVED", result["status"])
        self.assertEqual(6, result["source_events"])
        self.assertEqual(1, result["completed_handoffs"])
        self.assertIsNone(result["pending"])
        self.assertEqual([
            "PLAYER_SETUP_AND_AUTO_PREPLAY_AWAITED_RETURNED",
            "PLAYER_PHASE_ONE_AWAITED_HOOKS_RETURNED",
            "PLAYER_PHASE_TWO_AWAITED_HOOKS_RETURNED",
            "ENEMY_START_HOOKS_AWAITED_RETURNED",
            "ENEMY_ACTIONS_AND_END_HOOKS_AWAITED_RETURNED",
            "PLAYER_SETUP_AND_AUTO_PREPLAY_AWAITED_RETURNED",
        ], [row["milestone"] for row in result["awaited_source_milestones"]])
        self.assertTrue(all(row["settlement_status"] == "SETTLEMENT_UNVERIFIED"
                            for row in result["awaited_source_milestones"]))
        report = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual(result, report["native_phase_handoff"])
        self.assertFalse(any(
            r["data"]["native_phase_handoff_order_verified"] for r in records
            if r["type"] == "native_phase_handoff"))

    def test_native_extra_turn_does_not_require_enemy_start(self):
        records = sidecar([
            event("ABOUT_TO_SWITCH_TO_ENEMY_TURN", 1, "Player", "End",
                  "NotPlayPhase", two=True),
            event("SIDE_SWITCHED", 2, "Player", "End",
                  "NotPlayPhase", two=True),
            event("TURN_STARTED", 2, "Player", "Play", "PlayPhase"),
        ])
        result = audit_phase_handoffs(records)
        self.assertEqual("SOURCE_PHASE_HANDOFF_ORDER_OBSERVED", result["status"])
        self.assertEqual(1, result["extra_player_turn_handoffs"])
        self.assertEqual(
            "PLAYER_PHASE_TWO_RETURNED_EXTRA_TURN_HOOK_PENDING",
            result["awaited_source_milestones"][1]["milestone"])
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", audit(records)["status"])

    def test_partial_capture_and_old_sidecar_remain_explicitly_unverified(self):
        old = sidecar([])
        self.assertEqual("NOT_CAPTURED_LEGACY",
                         audit(old)["native_phase_handoff"]["status"])
        rows = sidecar(full_cycle()[:4])
        result = audit(rows)["native_phase_handoff"]
        self.assertEqual("SOURCE_PHASE_CALLBACKS_PARTIAL", result["status"])
        self.assertEqual("AWAIT_PLAYER_SWITCH", result["pending"])
        self.assertEqual(0, result["completed_handoffs"])
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", audit(rows)["status"])

    def test_turn_started_is_correlated_with_later_queue_checkpoint(self):
        checkpoint = {
            "type": "native_turn_checkpoint",
            "state": {"combat": {"turn": 1}, "player": {"hp": 60}},
            "data": {
                "kind": "PLAYER_TURN_START", "turn": 1,
                "observation_site": "NativeV111PassiveBridge.OnFrame",
                "observation_basis":
                    "IDLE_PLAYABLE_PLAYER_FRAME_BEFORE_OBSERVED_INPUT",
                "native_side": "Player", "player_phase": "Play",
                "settlement_status": "SETTLEMENT_UNVERIFIED",
                "complete_native_queue_verified": False,
                "native_phase_handoff_order_verified": False,
                "original_native_legal_menu": "NOT_VERIFIED",
                "emulator_parity": "NOT_TESTED",
                "rng_draws_performed_by_inspector": 0,
            },
        }
        callback = event("TURN_STARTED", 1, "Player", "Play", "PlayPhase")
        paired = audit(sidecar([callback, checkpoint]))
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", paired["status"])
        self.assertEqual(1, paired["player_checkpoint_phase_callback_pairs"])
        self.assertEqual(0, paired["player_checkpoints_without_observed_start_callback"])

        # A first-turn callback may have preceded observer attachment.
        missed = audit(sidecar([checkpoint]))
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", missed["status"])
        self.assertEqual(0, missed["player_checkpoint_phase_callback_pairs"])
        self.assertEqual(1, missed["player_checkpoints_without_observed_start_callback"])

        reversed_events = audit(sidecar([checkpoint, callback]))
        self.assertEqual("INCOMPLETE_CAPTURE", reversed_events["status"])
        self.assertTrue(reversed_events["incomplete_observations"])

    def test_player_start_hook_choice_pause_is_not_a_false_chronology_error(self):
        # HookPlayerChoiceContext's wait returns on a nested-choice pause.
        # TurnStarted can be emitted before RunAutoPrePlayPhase reaches Play.
        source = full_cycle()
        source[-1]["data"]["player_phase"] = "AutoPrePlay"
        pending = audit_phase_handoffs(sidecar(source))
        self.assertEqual("SOURCE_PHASE_CALLBACKS_PARTIAL", pending["status"])
        self.assertEqual("AWAIT_PLAYER_START_HOOK_COMPLETION", pending["pending"])
        self.assertEqual(1, pending["player_start_hook_pauses_observed"])
        self.assertEqual(0, pending["completed_handoffs"])
        self.assertNotEqual(
            "PLAYER_SETUP_AND_AUTO_PREPLAY_AWAITED_RETURNED",
            pending["awaited_source_milestones"][-1]["milestone"])

        checkpoint = {
            "type": "native_turn_checkpoint",
            "state": {"combat": {"turn": 2}, "player": {"hp": 60}},
            "data": {
                "kind": "PLAYER_TURN_START", "turn": 2,
                "observation_site": "NativeV111PassiveBridge.OnFrame",
                "observation_basis":
                    "IDLE_PLAYABLE_PLAYER_FRAME_BEFORE_OBSERVED_INPUT",
                "native_side": "Player", "player_phase": "Play",
                "settlement_status": "SETTLEMENT_UNVERIFIED",
                "complete_native_queue_verified": False,
                "native_phase_handoff_order_verified": False,
                "original_native_legal_menu": "NOT_VERIFIED",
                "emulator_parity": "NOT_TESTED",
                "rng_draws_performed_by_inspector": 0,
            },
        }
        resumed = audit(sidecar(source + [checkpoint]))
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", resumed["status"])
        result = resumed["native_phase_handoff"]
        self.assertEqual("SOURCE_PHASE_HANDOFF_ORDER_OBSERVED", result["status"])
        self.assertEqual(1, result["player_start_hook_resumes_observed"])
        self.assertEqual(1, result["completed_handoffs"])
        self.assertEqual("PLAYER_AUTO_PREPLAY_REACHED_PLAY_AT_FRAME",
                         result["awaited_source_milestones"][-1]["milestone"])
        self.assertEqual("SETTLEMENT_UNVERIFIED",
                         result["awaited_source_milestones"][-1]["settlement_status"])
        first = audit_phase_handoffs(sidecar([
            event("TURN_STARTED", 1, "Player", "Start", "PlayPhase"),
            {**copy.deepcopy(checkpoint),
             "data": {**checkpoint["data"], "turn": 1},
             "state": {"combat": {"turn": 1}, "player": {"hp": 60}}},
        ]))
        self.assertEqual(0, first["completed_handoffs"])
        self.assertEqual(1, first["player_start_hook_resumes_observed"])

        no_frame = audit_phase_handoffs(sidecar(source + [
            event("ABOUT_TO_SWITCH_TO_ENEMY_TURN", 2, "Player", "End",
                  "NotPlayPhase", two=True),
        ]))
        self.assertEqual([], no_frame["errors"])
        self.assertEqual(1, no_frame["player_start_hook_resolutions_without_frame"])

    def test_source_tracked_effect_depth_is_independently_audited(self):
        checkpoint = {
            "type": "native_turn_checkpoint",
            "state": {"combat": {"turn": 1}, "player": {"hp": 60}},
            "data": {
                "kind": "PLAYER_TURN_START", "turn": 1,
                "observation_site": "NativeV111PassiveBridge.OnFrame",
                "observation_basis":
                    "IDLE_PLAYABLE_PLAYER_FRAME_BEFORE_OBSERVED_INPUT",
                "native_side": "Player", "player_phase": "Play",
                "settlement_status": "SETTLEMENT_UNVERIFIED",
                "complete_native_queue_verified": False,
                "native_phase_handoff_order_verified": False,
                "original_native_legal_menu": "NOT_VERIFIED",
                "emulator_parity": "NOT_TESTED",
                "rng_draws_performed_by_inspector": 0,
                "automatic_effect_evidence": {
                    "no_active_card_or_potion_effect_body": True,
                    "no_player_turn_end_transition": True,
                    "enemy_turn_not_started": True,
                },
                "automatic_effect_evidence_status":
                    "SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME",
            }
        }
        old = copy.deepcopy(checkpoint)
        del old["data"]["automatic_effect_evidence"]
        del old["data"]["automatic_effect_evidence_status"]
        old_report = audit(sidecar([old]))
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", old_report["status"])
        self.assertEqual("NOT_CAPTURED_LEGACY",
                         old_report["turn_boundary_candidates"][0][
                             "automatic_effect_evidence_status"])

        report = audit(sidecar([checkpoint]))
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual(1, report["source_tracked_effect_idle_player_checkpoints"])
        self.assertEqual("SETTLEMENT_UNVERIFIED",
                         report["turn_boundary_candidates"][0]["settlement_status"])

        for mutation in ("bool_as_int", "flag_missing", "false_with_true_status",
                         "forged_status", "misplaced_enemy_evidence"):
            broken = copy.deepcopy(checkpoint)
            data = broken["data"]
            flags = data["automatic_effect_evidence"]
            if mutation == "bool_as_int":
                flags["enemy_turn_not_started"] = 1
            elif mutation == "flag_missing":
                del flags["no_active_card_or_potion_effect_body"]
            elif mutation == "false_with_true_status":
                flags["no_active_card_or_potion_effect_body"] = False
            elif mutation == "forged_status":
                data["automatic_effect_evidence_status"] = "SETTLEMENT_CERTIFIED"
            else:
                data["kind"] = "ENEMY_TURN_START"
                data["observation_site"] = "ActionExecutor.BeforeActionExecuted"
                data["observation_basis"] = "PRE_READY_TO_BEGIN_ENEMY_TURN_ACTION"
            with self.subTest(mutation=mutation):
                result = audit(sidecar([broken]))
                self.assertEqual("INCOMPLETE_CAPTURE", result["status"])
                self.assertTrue(result["incomplete_observations"])

        incomplete = copy.deepcopy(checkpoint)
        incomplete["data"]["automatic_effect_evidence"][
            "no_active_card_or_potion_effect_body"] = False
        incomplete["data"]["automatic_effect_evidence_status"] = (
            "SOURCE_TRACKED_EFFECT_ACTIVITY_UNVERIFIED")
        result = audit(sidecar([incomplete]))
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(0, result["source_tracked_effect_idle_player_checkpoints"])

    def test_invalid_order_provenance_and_forged_settlement_fail_closed(self):
        baseline = full_cycle()
        for mutation in ("wrong_order", "wrong_phase", "duplicate_handoff",
                         "bad_callback", "forged_settlement", "bad_flag",
                         "wrong_turn", "wrong_extra_turn", "missing_event"):
            rows = copy.deepcopy(baseline)
            if mutation == "wrong_order":
                rows[2], rows[3] = rows[3], rows[2]
            elif mutation == "wrong_phase":
                # Start/AutoPrePlay can occur while a native hook choice
                # suspends; End is incompatible with player TurnStarted.
                rows[5]["data"]["player_phase"] = "End"
            elif mutation == "duplicate_handoff":
                rows.insert(2, copy.deepcopy(rows[1]))
            elif mutation == "bad_callback":
                rows[2]["data"]["source_callback"] = "ActionExecutor.AfterActionExecuted"
            elif mutation == "forged_settlement":
                rows[4]["data"]["settlement_status"] = "SETTLEMENT_CERTIFIED"
            elif mutation == "bad_flag":
                rows[1]["data"]["ending_player_turn_phase_two"] = 1
            elif mutation == "wrong_turn":
                rows[4]["data"]["turn"] = 1
            elif mutation == "wrong_extra_turn":
                rows[2]["data"]["native_side"] = "Player"
            else:
                del rows[2]
            records = sidecar(rows)
            with self.subTest(mutation=mutation):
                self.assertEqual(
                    "INVALID_PHASE_HANDOFF_CHRONOLOGY",
                    audit_phase_handoffs(records)["status"])
                report = audit(records)
                self.assertEqual("INCOMPLETE_CAPTURE", report["status"])
                self.assertTrue(report["incomplete_observations"])


if __name__ == "__main__":
    unittest.main()
