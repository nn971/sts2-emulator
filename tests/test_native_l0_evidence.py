"""L0 aggregates established auditors without claiming live game/emulator parity."""
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_l0_evidence import assess


class NativeL0EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.sha = "a" * 64
        self.preflight = {
            "dependencies_present": True,
            "managed_assemblies": {"sts2.dll": {
                "present": True, "sha256": self.sha}},
        }
        self.records = [
            {"type": "header", "data": {
                "game_build": "v0.111.0;sha256=" + self.sha,
                "native_rng_hook_installed": True,
                "native_rng_float_hook_installed": True,
                "native_rng_stream_checkpoints_enabled": True,
                "native_monster_ai_scope_installed": True,
            }},
            {"type": "native_rng_scalar_boundary", "data": {
                "operation_kind": "range_float",
                "stream_id": "run.monster_ai",
            }},
        ]
        self.log = ("[Sts2ReferenceMod] CAPTURE_STATUS=HOOK_ENABLED; "
                    "build=v0.111.0;sha256=" + self.sha + "\n"
                    "[Sts2ReferenceMod] RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS;"
                    " float_source_hook=INSTALLED\n")
        self.envelope = {"status": "CALLBACK_TIMELINE_CONSISTENT",
                         "reason": "valid callback lifecycle"}
        self.primitives = {"status": "OBSERVED_NATIVE_RNG_PRIMITIVES_VALID",
                           "source_calls": 3, "primitive_error_count": 0,
                           "stream_conflict_count": 0, "stream_gap_count": 0,
                           "float_calls": 1}
        self.checkpoints = {
            "status": "OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR",
            "checkpoint_count": 3, "error_count": 0,
            "unobserved_rng_advances": 0,
            "streams": {"run.monster_ai": {
                "native_rng_advances": 1,
                "hook_observed_advances": 1,
                "unobserved_advances": 0,
                "large_windows_unverified": 0,
            }},
        }

    def assess(self, *, log=True, error=None, strict_trace=None,
               run_prelude=None):
        with (patch("native_l0_evidence.audit_envelope",
                    return_value=self.envelope),
              patch("native_l0_evidence.audit_primitives",
                    return_value=self.primitives),
              patch("native_l0_evidence.audit_checkpoints",
                    return_value=self.checkpoints)):
            return assess(self.preflight, self.records, error,
                          self.log if log else None, strict_trace,
                          run_prelude=run_prelude)

    def test_all_existing_source_gates_still_need_no_perturbation_and_parity(self):
        result = self.assess()
        self.assertEqual("L0_SOURCE_EVIDENCE_CONFIRMED_NONPERTURBATION_PENDING",
                         result["status"])
        self.assertEqual("NOT_TESTED", result["native_nonperturbation"])
        self.assertEqual("NOT_TESTED", result["native_emulator_exact_rng_parity"])
        self.assertEqual("NOT_TESTED", result["native_emulator_mechanics_parity"])
        self.assertEqual([], result["hard_failures"])

    def test_incomplete_strict_decision_trace_downgrades_source_report(self):
        report = self.assess(strict_trace={
            "status": "INCOMPLETE_CAPTURE",
            "reason": "UNSUPPORTED: nested native player choice",
            "decisions": 14,
        })
        self.assertEqual("L0_SOURCE_EVIDENCE_PARTIAL", report["status"])
        self.assertEqual("INCOMPLETE_CAPTURE",
                         report["strict_decision_trace_status"])
        self.assertEqual(14, report["strict_decision_count"])
        self.assertIn("STRICT_DECISION_TRACE_INCOMPLETE",
                      report["remaining_capture_gates"])
        self.assertEqual(0, report["native_rng_primitive_errors"])

    def test_valid_paired_strict_trace_remains_source_only(self):
        report = self.assess(strict_trace={
            "status": "STRUCTURALLY_VALID",
            "reason": "Complete passive event sequence",
            "decisions": 7,
        })
        self.assertEqual("L0_SOURCE_EVIDENCE_CONFIRMED_NONPERTURBATION_PENDING",
                         report["status"])
        self.assertEqual(7, report["strict_decision_count"])
        self.assertEqual("NOT_TESTED", report["native_emulator_mechanics_parity"])

    def test_source_input_evidence_is_reported_but_never_counts_as_menu_parity(self):
        self.envelope.update({
            "native_input_hook_installed": True,
            "native_hand_menu_hook_installed": True,
            "native_hand_choice_flow_hook_installed": True,
            "native_hand_menu_choice_matches": [{
                "owner_id": 1, "choice_id": 4, "menu_id": 1,
                "binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
                "selection_in_latest_captured_options": True}, {
                "owner_id": 1, "choice_id": 5, "menu_id": 2,
                "binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
                "selection_in_latest_captured_options": False}],
            "native_hand_menu_revisions": [
                {"menu_id": 1, "revision": 2, "peek_mode": False},
                {"menu_id": 1, "revision": 3, "peek_mode": True}],
            "native_hand_menu_closures": [
                {"menu_id": 1, "final_revision": 3}],
            "native_hand_menu_open_lifetimes": [2],
            "native_manual_play_hook_installed": True,
            "native_ui_play_begin_hook_installed": True,
            "native_ui_gate_hook_installed": True,
            "native_ui_hand_gates": [
                {"gate": "AreCardActionsAllowed",
                 "returned_allowed": True},
                {"gate": "CanPlayCards",
                 "returned_allowed": False}],
            "native_ui_play_begins": [{"started_via_shortcut": True}],
            "native_hand_menus": [
                {"coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
                 "choice_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID"},
                {"coverage": "INCOMPLETE_NATIVE_CARD_ID_MAPPING"}],
            "native_manual_play_results": [
                {"returned_success": True}, {"returned_success": False}],
            "native_input_requests": [
                {"input_request_id": 1}, {"input_request_id": 2}],
            "native_input_outcomes": [
                {"input_request_id": 1, "queue_result": "CANCELED_AT_RETURN"}],
            "source_request_execution_links": 1,
            "native_player_choices": [
                {"choice_type": "CombatCard", "selected_combat_card_ids": [7]}],
            "native_run_identity": {
                "source": "RunState.Rng.StringSeed",
                "seed": "6JK8MD0VQQTA", "ascension": 10},
        })
        report = self.assess()
        observed = report["native_source_input_observation"]
        self.assertTrue(observed["input_hook_reported_installed"])
        self.assertTrue(observed["hand_menu_hook_reported_installed"])
        self.assertTrue(observed["hand_choice_id_hook_reported_installed"])
        self.assertEqual(1, observed["source_reserved_hand_menu_bindings"])
        self.assertEqual(2, observed["hand_menu_choice_result_correlations"])
        self.assertEqual(2, observed["native_hand_menu_revisions"])
        self.assertEqual(1, observed["native_peek_view_revisions"])
        self.assertEqual(1, observed["native_hand_menu_closures"])
        self.assertEqual(1, observed["native_hand_menus_open_at_capture_end"])
        self.assertEqual(1, observed["native_hand_choice_matches_latest_captured_offers"])
        self.assertEqual(1, observed["native_hand_choice_matches_latest_outside_captured_offers"])
        self.assertTrue(observed["manual_play_hook_reported_installed"])
        self.assertTrue(observed["ui_play_begin_hook_reported_installed"])
        self.assertTrue(observed["ui_gate_hook_reported_installed"])
        self.assertEqual(2, observed["native_ui_gate_evaluations"])
        self.assertEqual(1, observed["native_ui_gate_allowed_evaluations"])
        self.assertEqual(1, observed["native_ui_gate_rejected_evaluations"])
        self.assertEqual(1, observed["native_ui_play_begins"])
        self.assertEqual(2, observed["native_filtered_hand_menus"])
        self.assertEqual(1, observed["hand_menus_with_complete_ui_id_projection"])
        self.assertEqual(2, observed["native_manual_play_results"])
        self.assertEqual(2, observed["source_requests"])
        self.assertEqual(1, observed["source_return_outcomes"])
        self.assertEqual(1, observed["requests_linked_to_executor"])
        self.assertEqual(1, observed["source_choice_results"])
        self.assertEqual("6JK8MD0VQQTA",
                         observed["native_run_identity"]["seed"])
        self.assertEqual("NOT_TESTED", observed["legal_menu_coverage"])
        self.assertEqual("NOT_TESTED", observed["native_input_nonperturbation"])
        self.assertEqual("NOT_TESTED", report["native_emulator_mechanics_parity"])

    def test_source_run_prelude_links_first_combat_without_macro_bootstrap(self):
        prelude = {
            "status": "RUN_PRELUDE_PAIRED_SOURCE_OBSERVED",
            "first_combat_link": {
                "combat_run_id": "combat-uuid",
                "source": "NativeV111PassiveBridge.OpenCombat",
            },
            "run_identity": {"seed": "6JK8MD0VQQTA", "ascension": 10},
            "native_macro_action_transcript_complete": False,
            "emulator_combat_bootstrap": "NOT_EXECUTED",
            "native_emulator_parity": "NOT_TESTED",
        }
        result = self.assess(run_prelude=prelude)
        self.assertEqual("L0_SOURCE_EVIDENCE_PARTIAL", result["status"])
        self.assertEqual("NOT_READY", result["source_macro_bootstrap"])
        self.assertEqual("combat-uuid",
                         result["source_run_prelude"]["first_combat_link"][
                             "combat_run_id"])
        self.assertIn("NATIVE_RUN_MACRO_CHOICES_AND_PRECOMBAT_RNG_INCOMPLETE",
                      result["remaining_capture_gates"])
        self.assertEqual("NOT_TESTED", result["native_emulator_mechanics_parity"])
        self.assertEqual("NOT_TESTED", result["native_emulator_exact_rng_parity"])

    def test_invalid_paired_source_run_prelude_is_rejected(self):
        result = self.assess(run_prelude={
            "status": "INVALID_PRELUDE",
            "errors": ["Prelude/combat source identity mismatch"]})
        self.assertEqual("L0_EVIDENCE_REJECTED", result["status"])
        self.assertIn("NATIVE_RUN_PRELUDE_SOURCE_PAIRING_INVALID",
                      result["hard_failures"])

    def test_missing_real_game_log_is_not_a_pass(self):
        result = self.assess(log=False)
        self.assertEqual("L0_SOURCE_EVIDENCE_PARTIAL", result["status"])
        self.assertIn("GAME_LOG_HOOK_BUILD_EVIDENCE_UNVERIFIED",
                      result["remaining_capture_gates"])

    def test_exact_installed_game_sha_is_required(self):
        self.preflight["managed_assemblies"]["sts2.dll"]["sha256"] = "b" * 64
        result = self.assess()
        self.assertEqual("L0_EVIDENCE_REJECTED", result["status"])
        self.assertIn("SIDE_CAR_GAME_BUILD_DOES_NOT_MATCH_INSTALLED_PREFLIGHT",
                      result["hard_failures"])

    def test_uninstalled_float_hook_stays_partial(self):
        self.records[0]["data"]["native_rng_float_hook_installed"] = False
        result = self.assess()
        self.assertIn("NATIVE_HOOK_FLAGS_INCOMPLETE",
                      result["remaining_capture_gates"])

    def test_unexercised_monster_ai_float_not_passed(self):
        self.records[1]["data"]["stream_id"] = "run.shuffle"
        result = self.assess()
        self.assertIn("NO_MONSTER_AI_NEXTFLOAT_OBSERVED",
                      result["remaining_capture_gates"])

    def test_remaining_monster_ai_steps_stay_partial(self):
        self.checkpoints["streams"]["run.monster_ai"]["unobserved_advances"] = 2
        result = self.assess()
        self.assertIn("MONSTER_AI_STREAM_HAS_UNOBSERVED_ADVANCES",
                      result["remaining_capture_gates"])

    def test_corrupt_primitive_and_stream_overlaps_rejected(self):
        self.primitives["primitive_error_count"] = 1
        self.primitives["stream_conflict_count"] = 1
        result = self.assess()
        self.assertEqual("L0_EVIDENCE_REJECTED", result["status"])
        self.assertIn("NATIVE_RNG_PRIMITIVE_OR_STREAM_CONFLICT",
                      result["hard_failures"])

    def test_invalid_rng_checkpoint_is_rejected(self):
        self.checkpoints["error_count"] = 1
        result = self.assess()
        self.assertIn("NATIVE_RNG_CHECKPOINT_ERROR",
                      result["hard_failures"])

    def test_legacy_sidecar_does_not_count_as_complete(self):
        self.envelope["status"] = "LEGACY_CALLBACK_SEQUENCE_RECOVERABLE"
        result = self.assess()
        self.assertEqual("L0_SOURCE_EVIDENCE_PARTIAL", result["status"])
        self.assertIn("LEGACY_CAPTURE_ONLY_RECOVERABLE",
                      result["remaining_capture_gates"])

    def test_unfinished_capture_is_rejected(self):
        self.envelope["status"] = "INCOMPLETE_CAPTURE"
        result = self.assess(error="incomplete marker found")
        self.assertIn("SIDECAR_READ_OR_WRITER_FAILURE",
                      result["hard_failures"])
        self.assertIn("NATIVE_CALLBACK_TIMELINE_INCOMPLETE",
                      result["hard_failures"])


if __name__ == "__main__":
    unittest.main()
