"""Incremental SSE -> persistent independent emulator coordinator contracts."""
from __future__ import annotations

import copy
import io
import json
import sys
import unittest
import urllib.request
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_live_offline_replay import simulate_sidecar, _digest
from native_live_fidelity_coordinator import (
    FidelityEventCursor, LiveFidelityError, IncrementalFidelityCoordinator,
    _NoRedirect, _loopback_base, parse_sse, follow,
)
from test_native_turn_checkpoint_replay import fixture, FakeSession, source as native_source


def source_records():
    j, _, _, _ = fixture()
    return [
        {"type": "header", "ordinal": 1, "data": {
            "schema": "native-action-envelope-v1",
            "run_id": "synthetic-native-combat", "game_build": "pinned-v111"}},
        {"type": "native_run_identity", "ordinal": 2,
         "data": {"seed": "TEST-SEED", "ascension": 10}},
        {"type": "native_turn_checkpoint", "ordinal": 3,
         "state": j["source_turn_checkpoint_candidates"][0]["source_state"],
         "data": {"kind": "PLAYER_TURN_START", "turn": 1,
                  "settlement_status": "SETTLEMENT_UNVERIFIED"}},
        {"type": "action_started", "ordinal": 4, "action_id": 1,
         "canonical_action": j["source_linked_commands"][0]["canonical_action"]},
        {"type": "action_completed", "ordinal": 5, "action_id": 1},
        {"type": "action_started", "ordinal": 6, "action_id": 2,
         "canonical_action": j["source_linked_commands"][1]["canonical_action"]},
        {"type": "action_completed", "ordinal": 7, "action_id": 2},
        {"type": "native_turn_checkpoint", "ordinal": 8,
         "state": j["source_turn_checkpoint_candidates"][2]["source_state"],
         "data": {"kind": "PLAYER_TURN_START", "turn": 2,
                  "settlement_status": "SETTLEMENT_UNVERIFIED"}},
        {"type": "terminal", "ordinal": 9, "data": {"status": "OBSERVED"}},
    ]


def synthetic_coordinator(*, probe_post_card_windows=False):
    _, start, _, _ = fixture()
    prelude = {
        "status": "RUN_PRELUDE_SOURCE_OBSERVED",
        "run_identity": {"game_build": "pinned-v111",
                         "seed": "TEST-SEED", "ascension": 10},
        "first_combat_link": {"combat_run_id": "synthetic-native-combat"},
    }
    def bootstrap(_source, _session):
        return ({"status": "CANDIDATE_COMBAT_REACHED_UNCERTIFIED"},
                start.save_snapshot())
    return IncrementalFidelityCoordinator(
        FakeSession(start), prelude, bootstrap=bootstrap,
        probe_post_card_windows=probe_post_card_windows)


class LiveCoordinatorTests(unittest.TestCase):
    def test_emulator_steps_incrementally_and_matches_second_checkpoint(self):
        live = synthetic_coordinator()
        events = simulate_sidecar(source_records())
        receipts = [live.consume(row) for row in events[:-1]]
        self.assertEqual(2, live.stepped)
        self.assertEqual([1, 2], [c["turn"] for c in live.matched])
        self.assertEqual(1, live.session.loaded)
        self.assertEqual("PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED",
                         live.status)
        self.assertFalse(live.stopped)
        self.assertEqual(2, receipts[-1]["player_checkpoints_matched"])
        self.assertEqual("SETTLEMENT_UNVERIFIED",
                         live.matched[-1]["native_settlement"])
        self.assertFalse(any("native_full_state_parity" in r for r in receipts
                             if r))

    def test_offline_post_card_probe_matches_only_source_stable_next_action(self):
        rows = source_records()
        after = native_source(1, [], [1], [0], hp=34)
        after["combat"]["energy"] = 2
        # This synthetic action-completion observation is identical to the
        # native next action's pre-state; the emulator already stepped card 1.
        rows[4]["state"] = copy.deepcopy(after)
        rows[5]["state"] = copy.deepcopy(after)
        live = synthetic_coordinator(probe_post_card_windows=True)
        for event in simulate_sidecar(rows[:6]):
            live.consume(event)
        probe = live.post_card_probe
        self.assertEqual(1, probe["source_stable_windows"])
        self.assertEqual(1, probe["emulator_comparisons"])
        self.assertEqual(1, probe["emulator_projected_matches"])
        self.assertIsNone(probe["first_difference"])
        self.assertFalse(probe["native_settlement_certified"])
        self.assertEqual("DIAGNOSTIC_ONLY", probe["status"])
        self.assertFalse(live.stopped)

    def test_post_card_probe_reports_candidate_without_repair_or_false_parity(self):
        rows = source_records()
        after = native_source(1, [], [1], [0], hp=33)
        after["combat"]["energy"] = 2
        rows[4]["state"] = copy.deepcopy(after)
        rows[5]["state"] = copy.deepcopy(after)
        live = synthetic_coordinator(probe_post_card_windows=True)
        for event in simulate_sidecar(rows[:6]):
            live.consume(event)
        probe = live.post_card_probe
        self.assertEqual(1, probe["source_stable_windows"])
        self.assertEqual(0, probe["emulator_projected_matches"])
        self.assertEqual("$.combat.creatures.creature:1.hp",
                         probe["first_difference"]["difference"]["path"])
        self.assertEqual(6, probe["first_difference"]["next_action_start_ordinal"])
        self.assertEqual(1, live.stepped)
        # A real source-stable intermediate discrepancy is visible at
        # once, before the next action is stepped or a turn checkpoint.
        self.assertTrue(live.stopped)
        self.assertEqual("SOURCE_POST_CARD_ACTION_DIFFERENCE_UNCERTIFIED",
                         live.status)
        receipt = live._receipt()
        self.assertEqual("$.combat.creatures.creature:1.hp",
                         receipt["first_difference_path"])
        self.assertNotIn("post_card_action_probe", receipt)
        self.assertEqual(1, receipt["emulator_actions_stepped"])
        local = live._receipt(include_post_card_probe=True)
        self.assertIsNotNone(local["post_card_action_probe"]["first_difference"])

        rows[5]["state"]["combat"]["energy"] = 1
        deferred = synthetic_coordinator(probe_post_card_windows=True)
        for event in simulate_sidecar(rows[:6]):
            deferred.consume(event)
        self.assertEqual(1, deferred.post_card_probe["deferred_native_windows"])
        self.assertEqual(0, deferred.post_card_probe["emulator_comparisons"])

    def test_guarded_monitor_enabled_by_default_on_real_coordinator(self):
        _, start, _, _ = fixture()
        prelude = {
            "status": "RUN_PRELUDE_SOURCE_OBSERVED",
            "run_identity": {"game_build": "pinned-v111",
                             "seed": "TEST-SEED", "ascension": 10},
            "first_combat_link": {"combat_run_id": "synthetic-native-combat"},
        }
        def bootstrap(_source, _session):
            return ({"status": "CANDIDATE_COMBAT_REACHED_UNCERTIFIED"},
                    start.save_snapshot())
        live = IncrementalFidelityCoordinator(
            FakeSession(start), prelude, bootstrap=bootstrap)
        self.assertTrue(live.probe_post_card_windows)
        self.assertNotIn("post_card_action_probe", live._receipt())

    def test_unsupported_native_post_card_projection_is_deferred(self):
        rows = source_records()
        rows[4]["state"] = {"player": {}, "combat": {}}
        rows[5]["state"] = copy.deepcopy(rows[4]["state"])
        live = synthetic_coordinator(probe_post_card_windows=True)
        for event in simulate_sidecar(rows[:6]):
            live.consume(event)
        self.assertEqual(1, live.post_card_probe["deferred_native_windows"])
        self.assertFalse(live.stopped)

    def test_default_coordinator_does_not_send_provisional_probe_fields(self):
        live = synthetic_coordinator()
        for event in simulate_sidecar(source_records()[:6]):
            live.consume(event)
        self.assertNotIn("post_card_action_probe", live._receipt())
        self.assertFalse(live.probe_post_card_windows)

    def test_live_queue_evidence_is_checked_before_player_comparison(self):
        rows = source_records()
        cp = rows[2]["data"]
        cp["queue_evidence"] = {
            "queue_is_empty": True,
            "queue_completion_succeeded": True,
            "executor_not_running": True,
            "executor_completion_succeeded": True,
            "no_currently_running_action": True,
            "executor_not_paused": True,
            "synchronizer_play_phase": True,
            "no_deferred_player_requests": True,
        }
        cp["queue_quiescence_status"] = (
            "SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME")
        live = synthetic_coordinator()
        for row in simulate_sidecar(rows[:3]):
            live.consume(row)
        self.assertEqual(1, len(live.matched))
        self.assertEqual("SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME",
                         live.matched[0]["queue_quiescence"])
        self.assertEqual("SETTLEMENT_UNVERIFIED",
                         live.matched[0]["native_settlement"])

        rows[2]["data"]["queue_evidence"]["queue_is_empty"] = False
        blocked = synthetic_coordinator()
        for row in simulate_sidecar(rows[:3]):
            blocked.consume(row)
        self.assertEqual("BLOCKED_SOURCE_CHRONOLOGY", blocked.status)
        self.assertEqual(0, len(blocked.matched))
        self.assertEqual(0, blocked.stepped)
        # Independent emulator startup occurs from the preceding native
        # identity record; the forged checkpoint blocks comparison/actions.
        self.assertEqual(1, blocked.session.loaded)

    def test_tracked_effect_evidence_is_checked_before_live_comparison(self):
        rows = source_records()
        cp = rows[2]["data"]
        cp["automatic_effect_evidence"] = {
            "no_active_card_or_potion_effect_body": True,
            "no_player_turn_end_transition": True,
            "enemy_turn_not_started": True,
        }
        cp["automatic_effect_evidence_status"] = (
            "SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME")
        live = synthetic_coordinator()
        for row in simulate_sidecar(rows[:3]):
            live.consume(row)
        self.assertEqual(1, len(live.matched))
        self.assertEqual("SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME",
                         live.matched[0]["automatic_effect_evidence"])
        self.assertEqual("SETTLEMENT_UNVERIFIED",
                         live.matched[0]["native_settlement"])

        cp["automatic_effect_evidence"]["no_active_card_or_potion_effect_body"] = False
        blocked = synthetic_coordinator()
        for row in simulate_sidecar(rows[:3]):
            blocked.consume(row)
        self.assertEqual("BLOCKED_SOURCE_CHRONOLOGY", blocked.status)
        self.assertEqual(0, len(blocked.matched))
        self.assertEqual(0, blocked.stepped)
        self.assertEqual(1, blocked.session.loaded)

    def test_first_observed_difference_freezes_emulator_without_repair(self):
        records = source_records()
        records[-2]["state"]["combat"]["creatures"][1]["hp"] = 21
        live = synthetic_coordinator()
        for row in simulate_sidecar(records):
            live.consume(row)
            if live.stopped:
                break
        self.assertEqual("SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
                         live.status)
        self.assertEqual(1, len(live.matched))
        self.assertEqual(2, live.stepped)
        self.assertEqual("$.combat.creatures.creature:1.hp",
                         live.first_difference["difference"]["path"])

    def test_live_get_only_replay_does_not_override_failed_native_receipt(self):
        # A native session previously sent a terminal failure; diagnose the
        # same live stream from its original sequence without any feedback POST.
        rows = source_records()
        rows[-2]["state"]["combat"]["creatures"][1]["hp"] = 21
        events = simulate_sidecar(rows)
        wire = "".join(
            "id: " + str(e["sequence"]) + "\n"
            "event: native\n"
            "data: " + json.dumps(e) + "\n\n" for e in events)
        live = synthetic_coordinator()
        with patch("native_live_fidelity_coordinator._LOCAL_OPENER.open",
                   return_value=io.StringIO(wire)) as opener, patch(
                   "native_live_fidelity_coordinator.post_feedback") as feedback:
            follow("http://127.0.0.1:39221", "private-test-token",
                   live, report_feedback=False)
        self.assertEqual("SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
                         live.status)
        self.assertEqual(1, len(live.matched))
        self.assertEqual(2, live.stepped)
        self.assertTrue(opener.called)
        feedback.assert_not_called()

    def test_reconnect_duplicate_is_idempotent(self):
        live = synthetic_coordinator()
        events = simulate_sidecar(source_records())
        for row in events[:4]:
            live.consume(row)
        self.assertIsNone(live.consume(events[3]))
        for row in events[4:9]:
            live.consume(row)
        self.assertEqual(2, live.stepped)
        self.assertEqual(2, len(live.matched))

    def test_missing_event_conflict_and_payload_tamper_fail_closed(self):
        events = simulate_sidecar(source_records())
        c = FidelityEventCursor()
        c.accept(events[0])
        with self.assertRaisesRegex(LiveFidelityError, "Missing source stream"):
            c.accept(events[2])
        bad = copy.deepcopy(events[1])
        bad["payload"]["data"]["run_id"] = "forged"
        with self.assertRaisesRegex(LiveFidelityError, "fingerprint"):
            c.accept(bad)
        c.accept(events[1])
        duplicate = copy.deepcopy(events[1])
        duplicate["kind"] = "forged"
        with self.assertRaisesRegex(LiveFidelityError, "Conflicting duplicate"):
            c.accept(duplicate)

    def test_stale_session_epoch_refused(self):
        events = simulate_sidecar(source_records())
        c = FidelityEventCursor()
        c.accept(events[0])
        bad = copy.deepcopy(events[1])
        bad["session_epoch"] = 2
        with self.assertRaisesRegex(LiveFidelityError, "session changed"):
            c.accept(bad)

    def test_mismatched_run_id_blocks_before_emulator_load(self):
        live = synthetic_coordinator()
        events = simulate_sidecar(source_records())
        # Valid source and envelope, but wrong auditor-provided prelude identity.
        live.prelude["first_combat_link"]["combat_run_id"] = "another-run"
        for row in events[:3]:
            live.consume(row)
        self.assertEqual("BLOCKED_NATIVE_PRELUDE_PAIRING", live.status)
        self.assertTrue(live.stopped)
        self.assertEqual(0, live.session.loaded)

    def test_checkpoint_before_first_player_ready_blocks(self):
        rows = source_records()
        rows[2] = {"type": "action_started", "ordinal": 3, "action_id": 1,
                   "canonical_action": rows[3]["canonical_action"]}
        live = synthetic_coordinator()
        for row in simulate_sidecar(rows[:3]):
            live.consume(row)
        self.assertEqual("BLOCKED_SOURCE_CHRONOLOGY", live.status)
        self.assertEqual(0, live.stepped)

    def test_parse_sse_frame_checks_id_and_rejects_incomplete(self):
        row = simulate_sidecar(source_records())[0]
        wire = "id: 1\nevent: native\ndata: " + json.dumps(row) + "\n\n"
        self.assertEqual([row], list(parse_sse(io.StringIO(wire))))
        with self.assertRaisesRegex(LiveFidelityError, "SSE ID"):
            list(parse_sse(io.StringIO(wire.replace("id: 1", "id: 7"))))
        with self.assertRaisesRegex(LiveFidelityError, "Partial SSE frame"):
            list(parse_sse(io.StringIO(wire.rstrip())))

    def test_invalid_sse_json_and_non_object_are_integrity_failures(self):
        for data in ("{invalid", "[]", "null"):
            frame = "id: 1\\nevent: native\\ndata: " + data + "\\n\\n"
            with self.assertRaises(LiveFidelityError) as caught:
                list(parse_sse(io.StringIO(frame.replace("\\n", "\n"))))
            self.assertEqual("BLOCKED_STREAM_INTEGRITY",
                             caught.exception.status)

    def test_fidelity_redirect_cannot_forward_private_token(self):
        no_redirect = _NoRedirect()
        request = urllib.request.Request(
            "http://127.0.0.1:39221/v1/fidelity/result",
            headers={"Authorization": "Bearer synthetic-private-token"})
        self.assertIsNone(no_redirect.redirect_request(
            request, None, 302, "Found",
            {"Location": "http://malicious.example/"},
            "http://malicious.example/"))

    def test_only_loopback_origins_allowed(self):
        self.assertEqual("http://127.0.0.1:39221",
                         _loopback_base("http://127.0.0.1:39221/"))
        for value in ("http://0.0.0.0:39221", "https://127.0.0.1:39221",
                      "http://example.com:39221", "http://127.0.0.1:39221/x",
                      "http://127.0.0.1:39221"):
            if value == "http://127.0.0.1:39221":
                continue
            with self.assertRaises(ValueError):
                _loopback_base(value)


if __name__ == "__main__":
    unittest.main()
