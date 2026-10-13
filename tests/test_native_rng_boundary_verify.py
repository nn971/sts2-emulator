"""Actual pinned xoshiro and sampling assertions for native RNG source calls."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_rng_stream_audit import MASK, advance, initial_state, rotate
from native_rng_boundary_verify import verify_call, audit


def sample(bound=7):
    before = initial_state(971)
    random64 = (rotate((before[1] * 5) & MASK, 7) * 9) & MASK
    index = int(((random64 >> 11) * (1 / (1 << 53))) * bound)
    after = advance(before)
    def obj(counter, words):
        return {"Counter": counter, **{
            "State" + str(i): value for i, value in enumerate(words)}}
    return {
        "origin": "source_primitive_hook",
        "source_version": "v0.111.0",
        "source_hook_observed": True,
        "operation_kind": "bounded_integer",
        "bounded_count": bound,
        "selected_index": index,
        "cursor_before": obj(15, before),
        "cursor_after": obj(16, after),
    }


class NativeRngBoundaryVerifyTests(unittest.TestCase):
    def test_one_exact_xoshiro_and_bounded_index_step(self):
        self.assertIsNone(verify_call(sample()))
        result = audit([{"type": "header"},
                        {"type": "native_rng_boundary", "action_id": 9,
                         "data": sample()}])
        self.assertEqual("OBSERVED_NATIVE_RNG_PRIMITIVES_VALID",
                         result["status"])
        self.assertEqual(1, result["validated_calls"])
        self.assertEqual("NOT_TESTED",
                         result["semantic_eligibility_parity"])

    def test_changed_rng_state_is_rejected(self):
        tampered = sample()
        tampered["cursor_after"]["State2"] ^= 4
        self.assertEqual("XOSHIRO_STATE_TRANSITION_MISMATCH",
                         verify_call(tampered))

    def test_changed_native_selection_is_rejected(self):
        tampered = sample()
        tampered["selected_index"] = (
            tampered["selected_index"] + 1) % 7
        self.assertEqual("BOUNDED_INT_RESULT_MISMATCH",
                         verify_call(tampered))

    def test_skip_or_extra_counter_is_rejected(self):
        tampered = sample()
        tampered["cursor_after"]["Counter"] += 1
        self.assertEqual("INVALID_NATIVE_CURSOR", verify_call(tampered))

    def test_unverified_frame_provenance_never_passes(self):
        tampered = sample()
        tampered["origin"] = "rendered_frame_poll"
        self.assertEqual("UNVERIFIED_SOURCE_OR_OPERATION",
                         verify_call(tampered))

    def test_empty_and_legacy_sidecars_remain_unobserved(self):
        self.assertEqual("SOURCE_RNG_NOT_OBSERVED",
                         audit([{"type": "header"}])["status"])

    def test_singleton_still_advances_rng(self):
        self.assertIsNone(verify_call(sample(bound=1)))


if __name__ == "__main__":
    unittest.main()
