"""Native RNG source-call comparison never mistakes absent hooks for parity."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_rng_boundary_compare import compare_native_emulator


def row(action=4, stream="run.niche", index=1, source="source_primitive_hook"):
    return {
        "type": "native_rng_boundary",
        "action_id": action,
        "data": {
            "site_id": "combat.initial_unique_hp",
            "stream_id": stream, "owner_id": "creature:1",
            "call_ordinal": 0, "operation_kind": "bounded_integer",
            "bounded_count": 2, "selected_index": index,
            "source_version": "v0.111.0",
            "origin": source, "source_hook_observed": source == "source_primitive_hook",
            "attributed": True,
            "cursor_before": {"counter": 0},
            "cursor_after": {"counter": 1}
        }
    }


def engine(index=1, events=True):
    return {
        "executed_actual_engine_step": True,
        "emulator_random_boundaries": [{
            "request": {
                "site_id": "combat.initial_unique_hp",
                "stream_id": "run.niche",
                "owner_id": "creature:1", "ordinal": 0,
                "operation_kind": "bounded_integer",
                "bounded_count": 2, "source_version": "v0.111.0"},
            "selected_index": index,
            "provider_mode": "native-v111",
            "native_cursor_before": {"counter": 0},
            "native_cursor_after": {"counter": 1}
        }] if events else []
    }


class NativeRngBoundaryCompareTests(unittest.TestCase):
    def test_real_matching_source_call_is_only_local_evidence(self):
        r = compare_native_emulator([row()], engine(), 4)
        self.assertEqual("OBSERVED_RNG_CALL_SEQUENCE_CONSISTENT", r["status"])
        self.assertEqual(1, r["compared"])
        self.assertEqual("NOT_TESTED", r["whole_combat_parity"])

    def test_no_native_source_hook_does_not_pass(self):
        self.assertEqual("NATIVE_RNG_SOURCE_UNOBSERVED",
            compare_native_emulator([], engine(), 4)["status"])

    def test_no_emulator_provider_hook_does_not_pass(self):
        self.assertEqual("EMULATOR_RNG_PROVIDER_UNOBSERVED",
            compare_native_emulator([row()], engine(events=False), 4)["status"])

    def test_wrong_stream_and_outcome_identify_first_mismatch(self):
        r = compare_native_emulator([row(stream="player.rewards")], engine(), 4)
        self.assertEqual("RNG_BOUNDARY_MISMATCH", r["status"])
        self.assertEqual("stream_id", r["first_mismatch"]["field"])
        r = compare_native_emulator([row(index=0)], engine(), 4)
        self.assertEqual("selected_index", r["first_mismatch"]["field"])

    def test_unattributed_or_inferred_frame_call_is_not_source_evidence(self):
        r = compare_native_emulator([row(source="rendered_frame_poll")],
                                    engine(), 4)
        self.assertEqual("NON_SOURCE_NATIVE_RNG_OBSERVATION", r["status"])
        unattributed = row()
        unattributed["data"]["site_id"] = "UNATTRIBUTED"
        self.assertEqual("NATIVE_RNG_CALLSITE_UNATTRIBUTED",
            compare_native_emulator([unattributed], engine(), 4)["status"])

    def test_index_match_does_not_claim_semantic_candidate_parity(self):
        step = engine()
        req = step["emulator_random_boundaries"][0]["request"]
        req["operation_kind"] = "choose_index"
        del req["bounded_count"]
        req["ordered_candidates"] = [44, 45]
        report = compare_native_emulator([row()], step, 4)
        self.assertEqual(
            "OBSERVED_RNG_PRIMITIVE_INDEX_ONLY_ELIGIBILITY_UNVERIFIED",
            report["status"])
        self.assertEqual(1, report["compared"])

    def test_wrong_action_is_not_cross_correlated(self):
        self.assertEqual("NATIVE_RNG_SOURCE_UNOBSERVED",
            compare_native_emulator([row(action=7)], engine(), 4)["status"])

    def test_missing_second_random_call_is_a_count_mismatch(self):
        r = compare_native_emulator([row(), row()], engine(), 4)
        self.assertEqual("RNG_BOUNDARY_COUNT_MISMATCH", r["status"])

    def test_cursor_parity_requires_full_native_words(self):
        r = compare_native_emulator([row()], engine(), 4,
                                    exact_native_cursors=True)
        self.assertEqual("RNG_BOUNDARY_MISMATCH", r["status"])
        self.assertEqual("cursor_before", r["first_mismatch"]["field"])

    def test_no_real_engine_step_cannot_pass(self):
        missing = engine()
        missing["executed_actual_engine_step"] = False
        self.assertEqual("EMULATOR_STEP_NOT_EXECUTED",
            compare_native_emulator([row()], missing, 4)["status"])


if __name__ == "__main__":
    unittest.main()
