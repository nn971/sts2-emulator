"""Native RNG source-call comparison never mistakes absent hooks for parity."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_rng_boundary_compare import compare_native_emulator
from native_rng_stream_audit import MASK, advance, initial_state, rotate

WORDS = initial_state(971)
RANDOM64 = (rotate((WORDS[1] * 5) & MASK, 7) * 9) & MASK
NATIVE_INDEX = int(((RANDOM64 >> 11) * (1 / (1 << 53))) * 2)


def cursor(counter, words):
    return {"counter": counter, **{
        "state" + str(i): value for i, value in enumerate(words)}}



def row(action=4, stream="run.niche", index=None,
        source="source_primitive_hook"):
    index = NATIVE_INDEX if index is None else index
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
            "cursor_before": cursor(0, WORDS),
            "cursor_after": cursor(1, advance(WORDS))
        }
    }


def engine(index=None, events=True):
    index = NATIVE_INDEX if index is None else index
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
        r = compare_native_emulator([row()], engine(index=1-NATIVE_INDEX), 4)
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
        r = compare_native_emulator([row(), row(stream="player.rewards")],
                                    engine(), 4)
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

    def test_source_cursor_tampering_precedes_any_pairing_claim(self):
        tampered = row()
        tampered["data"]["cursor_after"]["state2"] ^= 1
        result = compare_native_emulator([tampered], engine(), 4)
        self.assertEqual("NATIVE_RNG_PRIMITIVE_MISMATCH", result["status"])
        self.assertEqual("XOSHIRO_STATE_TRANSITION_MISMATCH",
                         result["first_mismatch"]["type"])

    def test_duplicate_native_hook_observation_never_counts_as_consistent(self):
        duplicate = [row(), row()]
        result = compare_native_emulator(duplicate, engine(), 4)
        self.assertEqual("NATIVE_RNG_STREAM_ORDER_CONFLICT", result["status"])
        self.assertEqual(1, result["native_stream_conflict_count"])

    def test_forward_stream_gap_restricts_supported_comparison(self):
        first = row()
        unobserved = advance(advance(WORDS))
        number = (rotate((unobserved[1] * 5) & MASK, 7) * 9) & MASK
        second = row(action=5, index=int(
            ((number >> 11) * (1 / (1 << 53))) * 2))
        second["data"]["cursor_before"] = cursor(2, unobserved)
        second["data"]["cursor_after"] = cursor(3, advance(unobserved))
        result = compare_native_emulator([first, second], engine(), 4)
        self.assertEqual("OBSERVED_RNG_PRIMITIVE_WITH_UNOBSERVED_STREAM_ADVANCES",
                         result["status"])
        self.assertEqual(1, result["native_stream_gap_count"])
        self.assertEqual(1, result["compared"])

    def test_ranged_float_is_verified_then_rejected_as_unsupported_emulator_provider(self):
        original = row()
        data = original["data"]
        data["operation_kind"] = "range_float"
        data["selected_double"] = None
        data["selected_bool"] = None
        data["selected_float"] = 2.5
        data["float_min"] = 2.5
        data["float_max"] = 2.5
        data.pop("selected_index")
        data.pop("bounded_count")
        original["type"] = "native_rng_scalar_boundary"
        r = compare_native_emulator([original], engine(), 4)
        self.assertEqual("UNSUPPORTED_NATIVE_RNG_OPERATION", r["status"])
        data["selected_float"] = 2.0
        r = compare_native_emulator([original], engine(), 4)
        self.assertEqual("NATIVE_RNG_PRIMITIVE_MISMATCH", r["status"])
        self.assertEqual("SCALAR_FLOAT_RESULT_MISMATCH",
                         r["first_mismatch"]["type"])

    def test_scalar_source_call_must_not_be_ignored_during_action_pairing(self):
        single = row()
        scalar = row()
        scalar["type"] = "native_rng_scalar_boundary"
        scalar["data"]["operation_kind"] = "boolean"
        scalar["data"]["selected_double"] = None
        scalar["data"]["selected_bool"] = False
        scalar["data"].pop("bounded_count")
        scalar["data"].pop("selected_index")
        # Reuse an independent source stream to avoid cross-call overlap.
        scalar["data"]["stream_id"] = "player.rewards"
        scalar["data"]["selected_bool"] = (
            int(((RANDOM64 >> 11) * (1 / (1 << 53))) * 2) == 0)
        result = compare_native_emulator([single, scalar], engine(), 4)
        self.assertEqual("UNSUPPORTED_NATIVE_RNG_OPERATION",
                         result["status"])
        self.assertEqual(2, result["native_call_count"])


if __name__ == "__main__":
    unittest.main()
