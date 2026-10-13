"""Fail-closed unified native/engine/RNG report classifications."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from unified_native_rng_fidelity_gate import combine


def corpus(status="CALLBACK_CORPUS_CONSISTENT", count=1):
    return {"status": status, "files_checked": count,
            "native_action_types": {"PlayCardAction": 3},
            "mechanics": {}}


def strict(complete=1, total=1):
    return {"status": "NATIVE_EVIDENCE_ONLY; EMULATOR_PARITY_NOT_TESTED",
            "trace_count": total, "structurally_valid_count": complete}


def prefixes(count=2):
    return {"recovered_closed_prefix_count": count}


def engine(status="ALL_SUPPORTED_CHECKPOINTS_CONSISTENT",
           stepped=2, mismatched=0, unsupported=0):
    return {"schema": "native-engine-fidelity-batch-v1",
            "status": status, "total_fixtures": 2,
            "actually_stepped": stepped, "consistent": stepped-mismatched,
            "mismatched": mismatched, "unsupported_or_invalid": unsupported,
            "entries": []}


class UnifiedFidelityGateTests(unittest.TestCase):
    def test_empty_corpus_cannot_be_certified(self):
        r = combine(corpus("NO_CAPTURES", 0), strict(0, 0),
                    prefixes(0), None)
        self.assertEqual("NO_CAPTURE_EVIDENCE", r["status"])
        self.assertEqual("NOT_TESTED", r["native_game_parity"])
        self.assertEqual("NOT_IN_NATIVE_SIDECAR_YET",
                         r["random_boundary_capture"])

    def test_actual_engine_mismatch_is_not_hidden_by_capture_gaps(self):
        r = combine(corpus("LEGACY_OBSERVER_ARTIFACTS_REVIEWED"),
                    strict(0), prefixes(), engine(
                        "CHECKPOINT_MISMATCHES_DETECTED", 2, 1, 0))
        self.assertEqual("REAL_ENGINE_CHECKPOINT_MISMATCH", r["status"])
        self.assertEqual(1, r["engine"]["mismatched"])

    def test_supported_checkpoints_agree_without_full_game_claim(self):
        r = combine(corpus(), strict(), prefixes(),
                    engine())
        self.assertEqual("SUPPORTED_CHECKPOINTS_AGREE_NOT_FULL_GAME_PARITY",
                         r["status"])
        self.assertEqual("NOT_TESTED", r["exact_native_rng_parity"])
        self.assertEqual("NOT_TESTED", r["native_source_hook_order_parity"])
        self.assertEqual("NOT_CALIBRATED", r["conditional_chance_law"])

    def test_legacy_sidecars_and_unsupported_power_remain_partial(self):
        r = combine(corpus("LEGACY_OBSERVER_ARTIFACTS_REVIEWED"),
                    strict(0), prefixes(), engine(
                        "PARTIAL_COMPARISON_UNSUPPORTED", 1, 0, 1))
        self.assertEqual("PARTIAL_NATIVE_OR_ENGINE_COVERAGE", r["status"])
        self.assertEqual(1, r["engine"]["actual_engine_steps"])
        self.assertEqual(1, r["engine"]["unsupported_or_invalid"])

    def test_engine_not_run_or_missing_dll_cannot_pass(self):
        r = combine(corpus(), strict(), prefixes(), None)
        self.assertEqual("ENGINE_COMPARISON_NOT_COMPLETED", r["status"])
        error = combine(corpus(), strict(), prefixes(), None,
                        "Engine DLL not found")
        self.assertEqual("ENGINE_EXECUTION_UNAVAILABLE", error["status"])
        self.assertIn("Engine DLL", error["engine"]["error"])


if __name__ == "__main__":
    unittest.main()
