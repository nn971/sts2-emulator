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

    def assess(self, *, log=True, error=None, strict_trace=None):
        with (patch("native_l0_evidence.audit_envelope",
                    return_value=self.envelope),
              patch("native_l0_evidence.audit_primitives",
                    return_value=self.primitives),
              patch("native_l0_evidence.audit_checkpoints",
                    return_value=self.checkpoints)):
            return assess(self.preflight, self.records, error,
                          self.log if log else None, strict_trace)

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
