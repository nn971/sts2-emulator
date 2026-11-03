"""Source-backed awaited-turn and sampled-idle-frame witness tests."""
from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_action_envelope_audit import audit
from native_phase_handoff_audit import audit_phase_handoffs
from native_awaited_turn_witness_audit import audit_awaited_turn_witnesses
from test_native_phase_handoff_audit import event, full_cycle, sidecar


QUEUE_FLAGS = (
    "queue_is_empty", "queue_completion_succeeded",
    "executor_not_running", "executor_completion_succeeded",
    "no_currently_running_action", "executor_not_paused",
    "synchronizer_play_phase", "no_deferred_player_requests",
)


def player_frame(turn, *, idle=True):
    evidence = {key: True for key in QUEUE_FLAGS}
    if not idle:
        evidence["queue_is_empty"] = False
    return {
        "type": "native_turn_checkpoint",
        "state": {"player": {"hp": 50}, "combat": {"turn": turn}},
        "data": {
            "kind": "PLAYER_TURN_START", "turn": turn,
            "observation_site": "NativeV111PassiveBridge.OnFrame",
            "observation_basis": "IDLE_PLAYABLE_PLAYER_FRAME_BEFORE_OBSERVED_INPUT",
            "native_side": "Player", "player_phase": "Play",
            "queue_evidence": evidence,
            "queue_quiescence_status": (
                "SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME"
                if idle else "SOURCE_QUEUE_QUIESCENCE_UNVERIFIED"),
            "automatic_effect_evidence": {
                "no_active_card_or_potion_effect_body": True,
                "no_player_turn_end_transition": True,
                "enemy_turn_not_started": True,
            },
            "automatic_effect_evidence_status":
                "SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME",
            "settlement_status": "SETTLEMENT_UNVERIFIED",
            "complete_native_queue_verified": False,
            "native_phase_handoff_order_verified": False,
            "original_native_legal_menu": "NOT_VERIFIED",
            "emulator_parity": "NOT_TESTED",
            "rng_draws_performed_by_inspector": 0,
        },
    }


def normal_records():
    callbacks = full_cycle()
    return sidecar([callbacks[0], player_frame(1),
                    *callbacks[1:], player_frame(2)])


def summary(records):
    checkpoints = [
        {
            "ordinal": r["ordinal"],
            "kind": r["data"]["kind"], "turn": r["data"]["turn"],
            "queue_quiescence_status": r["data"].get("queue_quiescence_status"),
            "automatic_effect_evidence_status":
                r["data"].get("automatic_effect_evidence_status"),
        } for r in records if r.get("type") == "native_turn_checkpoint"
    ]
    return audit_awaited_turn_witnesses(
        records, checkpoints, audit_phase_handoffs(records))


class AwaitedTurnWitnessTests(unittest.TestCase):
    def test_initial_and_full_enemy_cycle_have_bounded_witnesses(self):
        rows = normal_records()
        result = summary(rows)
        self.assertEqual("SOURCE_BOUNDED_TURN_WITNESSES_OBSERVED_UNCERTIFIED",
                         result["status"])
        self.assertEqual(2, result["bounded_awaited_and_idle_frame_witnesses"])
        self.assertEqual(1, result["normal_enemy_turn_chains"])
        self.assertEqual("NORMAL_ENEMY_TURN_AWAITED_CHAIN",
                         result["witnesses"][1]["handoff_kind"])
        self.assertFalse(result["native_settlement_certified"])
        self.assertFalse(result["arbitrary_delayed_effects_excluded"])
        strict = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", strict["status"])
        self.assertEqual(result, strict["awaited_turn_witnesses"])

    def test_missing_awaited_phase_hook_is_never_silent_success(self):
        rows = normal_records()
        rows = sidecar([r for r in rows[1:-1]
                        if not (r.get("type") == "native_phase_handoff"
                                and (r.get("data") or {}).get("source_event")
                                    == "SIDE_SWITCHED"
                                and (r.get("data") or {}).get("native_side")
                                    == "Player")])
        result = summary(rows)
        self.assertEqual(1, result["bounded_awaited_and_idle_frame_witnesses"])
        self.assertEqual(1, result["turns_without_full_witness"])
        self.assertEqual("TURN_HANDOFF_AWAIT_CHAIN_UNVERIFIED",
                         result["witnesses"][1]["handoff_kind"])
        self.assertEqual("SOURCE_BOUNDED_TURN_WITNESSES_PARTIAL",
                         result["status"])
        self.assertFalse(result["native_settlement_certified"])

    def test_non_idle_queue_and_missing_effect_flags_defer_only_that_turn(self):
        rows = normal_records()
        rows[-2] = player_frame(2, idle=False)
        rows = sidecar(rows[1:-1])
        result = summary(rows)
        self.assertEqual(1, result["bounded_awaited_and_idle_frame_witnesses"])
        self.assertIn("FRAME_QUEUE_OR_TRACKED_EFFECT_NOT_IDLE",
                      result["witnesses"][1]["unverified_reasons"])
        rows[-2]["data"]["automatic_effect_evidence_status"] = "NOT_CAPTURED"
        result = summary(rows)
        self.assertEqual(1, result["turns_without_full_witness"])

    def test_extra_player_turn_has_distinct_awaited_witness(self):
        rows = sidecar([
            event("TURN_STARTED", 1, "Player", "Play", "PlayPhase"),
            player_frame(1),
            event("ABOUT_TO_SWITCH_TO_ENEMY_TURN", 1, "Player", "End",
                  "NotPlayPhase", two=True),
            event("SIDE_SWITCHED", 2, "Player", "End",
                  "NotPlayPhase", two=True),
            event("TURN_STARTED", 2, "Player", "Play", "PlayPhase"),
            player_frame(2),
        ])
        result = summary(rows)
        self.assertEqual(2, result["bounded_awaited_and_idle_frame_witnesses"])
        self.assertEqual(1, result["extra_player_turn_chains"])
        self.assertEqual(0, result["normal_enemy_turn_chains"])
        self.assertFalse(result["native_settlement_certified"])

    def test_paused_auto_preplay_requires_later_playable_frame(self):
        rows = normal_records()
        for r in rows:
            if (r.get("type") == "native_phase_handoff"
                and r["data"].get("turn") == 2
                and r["data"].get("source_event") == "TURN_STARTED"):
                r["data"]["player_phase"] = "AutoPrePlay"
        self.assertEqual(2, summary(rows)["bounded_awaited_and_idle_frame_witnesses"])
        self.assertEqual("SOURCE_PHASE_HANDOFF_ORDER_OBSERVED",
                         audit_phase_handoffs(rows)["status"])
        paused = sidecar([r for r in rows[1:-2]])
        self.assertEqual(0, summary(paused)["player_turns_observed"])

    def test_legacy_checkpoint_without_source_callbacks_is_unverified(self):
        rows = sidecar([player_frame(1)])
        result = summary(rows)
        self.assertEqual(0, result["bounded_awaited_and_idle_frame_witnesses"])
        self.assertEqual("SOURCE_BOUNDED_TURN_WITNESSES_PARTIAL", result["status"])
        self.assertIn("PLAYER_TURN_STARTED_CALLBACK_NOT_OBSERVED_BEFORE_FRAME",
                      result["witnesses"][0]["unverified_reasons"])


if __name__ == "__main__":
    unittest.main()
