"""Actual pinned xoshiro and sampling assertions for native RNG source calls."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_rng_stream_audit import MASK, advance, initial_state, rotate
from native_rng_boundary_verify import verify_call, verify_scalar_call, audit


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

    def test_adjacent_known_stream_calls_have_exact_cursor_continuity(self):
        first = sample()
        first["stream_id"] = "run.monster_ai"
        second = copy.deepcopy(first)
        second["cursor_before"] = copy.deepcopy(first["cursor_after"])
        words = tuple(second["cursor_before"]["State" + str(i)]
                      for i in range(4))
        number = (rotate((words[1] * 5) & MASK, 7) * 9) & MASK
        second["selected_index"] = int(
            ((number >> 11) * (1 / (1 << 53))) * second["bounded_count"])
        second["cursor_after"] = {
            "Counter": 17,
            **{"State" + str(i): word
               for i, word in enumerate(advance(words))}
        }
        report = audit([
            {"type": "native_rng_boundary", "data": first},
            {"type": "native_rng_boundary", "data": second}])
        self.assertEqual("OBSERVED_NATIVE_RNG_PRIMITIVES_VALID",
                         report["status"])
        self.assertEqual(1, report["stream_pairs_contiguous"])
        self.assertEqual(1, report["stream_pairs_checked"])
        self.assertEqual([], report["stream_gaps"])

    def test_skipped_persistent_stream_advance_is_coverage_gap(self):
        first = sample()
        first["stream_id"] = "player.draw"
        second = copy.deepcopy(first)
        # A different non-integer RNG source call can occupy this step.
        words = advance(advance(initial_state(971)))
        second["cursor_before"] = {
            "Counter": 17,
            **{"State" + str(i): word for i, word in enumerate(words)}
        }
        number = (rotate((words[1] * 5) & MASK, 7) * 9) & MASK
        second["selected_index"] = int(
            ((number >> 11) * (1 / (1 << 53))) * second["bounded_count"])
        second["cursor_after"] = {
            "Counter": 18,
            **{"State" + str(i): word
               for i, word in enumerate(advance(words))}
        }
        report = audit([
            {"type": "native_rng_boundary", "data": first},
            {"type": "native_rng_boundary", "data": second}])
        self.assertEqual("OBSERVED_NATIVE_RNG_PRIMITIVES_WITH_STREAM_GAPS",
                         report["status"])
        self.assertEqual(0, report["stream_conflict_count"])
        self.assertEqual(1, report["stream_gap_count"])
        self.assertEqual(1, report["stream_gaps"][0]["unobserved_rng_advances"])

    def test_overlapping_native_overload_callbacks_fail_closed(self):
        first = sample()
        first["stream_id"] = "run.monster_ai"
        # Both primitive callbacks are locally valid; the repeated cursor
        # would indicate a nested/wrapped observer or an owner alias.
        report = audit([
            {"type": "native_rng_boundary", "data": first},
            {"type": "native_rng_boundary", "data": copy.deepcopy(first)}])
        self.assertEqual("NATIVE_RNG_STREAM_ORDER_CONFLICT",
                         report["status"])
        self.assertEqual(2, report["validated_calls"])
        self.assertEqual("STREAM_CURSOR_REWIND_OR_OVERLAP",
                         report["stream_conflicts"][0]["type"])

    def test_same_counter_different_state_and_anonymous_streams(self):
        first = sample()
        first["stream_id"] = "run.shuffle"
        other = sample()
        other["stream_id"] = "run.shuffle"
        # Another individually valid source call with a distinct RNG state
        # and the same counter cannot be contiguous on the same stream.
        words = initial_state(972)
        other["cursor_before"] = {
            "Counter": 16,
            **{"State" + str(i): word for i, word in enumerate(words)}
        }
        other["cursor_after"] = {
            "Counter": 17,
            **{"State" + str(i): word for i, word in enumerate(advance(words))}
        }
        number = (rotate((words[1] * 5) & MASK, 7) * 9) & MASK
        other["selected_index"] = int(
            ((number >> 11) * (1 / (1 << 53))) * other["bounded_count"])
        report = audit([
            {"type": "native_rng_boundary", "data": first},
            {"type": "native_rng_boundary", "data": other}])
        self.assertEqual("NATIVE_RNG_STREAM_ORDER_CONFLICT",
                         report["status"])
        self.assertEqual("SAME_COUNTER_STATE_CONFLICT",
                         report["stream_conflicts"][0]["type"])
        first["stream_id"] = other["stream_id"] = "UNATTRIBUTED"
        report = audit([
            {"type": "native_rng_boundary", "data": first},
            {"type": "native_rng_boundary", "data": other}])
        self.assertEqual(0, report["stream_pairs_checked"])
        self.assertEqual("OBSERVED_NATIVE_RNG_PRIMITIVES_VALID",
                         report["status"])

    def test_different_known_streams_do_not_cross_pair(self):
        a, b = sample(), sample()
        a["stream_id"], b["stream_id"] = "run.monster_ai", "player.draw"
        report = audit([
            {"type": "native_rng_boundary", "data": a},
            {"type": "native_rng_boundary", "data": b}])
        self.assertEqual(0, report["stream_pairs_checked"])
        self.assertEqual(0, report["stream_conflict_count"])

    def test_real_source_unit_double_and_boolean_match_xoshiro(self):
        data = sample()
        words = initial_state(971)
        random64 = (rotate((words[1] * 5) & MASK, 7) * 9) & MASK
        unit = (random64 >> 11) * (1 / (1 << 53))
        data.update(operation_kind="unit_double", selected_double=unit,
                    selected_bool=None, stream_id="run.monster_ai")
        data.pop("bounded_count")
        data.pop("selected_index")
        self.assertIsNone(verify_scalar_call(data))
        data.update(operation_kind="boolean", selected_double=None,
                    selected_bool=(int(unit * 2) == 0))
        self.assertIsNone(verify_scalar_call(data))

    def test_scalar_result_mutation_and_provenance_fail(self):
        data = sample()
        words = initial_state(971)
        unit = ((rotate((words[1] * 5) & MASK, 7) * 9 & MASK) >> 11) * (
            1 / (1 << 53))
        data.update(operation_kind="unit_double", selected_double=unit + 0.01,
                    selected_bool=None)
        self.assertEqual("SCALAR_DOUBLE_RESULT_MISMATCH",
                         verify_scalar_call(data))
        data["origin"] = "frame_cursor_difference"
        self.assertEqual("UNVERIFIED_SCALAR_SOURCE_OR_OPERATION",
                         verify_scalar_call(data))
        data["origin"] = "source_primitive_hook"
        data.update(operation_kind="boolean", selected_double=None,
                    selected_bool=not (int(unit * 2) == 0))
        self.assertEqual("SCALAR_BOOL_RESULT_MISMATCH",
                         verify_scalar_call(data))

    def test_mixed_integer_and_scalar_source_calls_share_one_stream(self):
        first = sample()
        first["stream_id"] = "run.monster_ai"
        next_words = advance(initial_state(971))
        number = (rotate((next_words[1] * 5) & MASK, 7) * 9) & MASK
        unit = (number >> 11) * (1 / (1 << 53))
        second = {
            "origin": "source_primitive_hook",
            "source_version": "v0.111.0",
            "source_hook_observed": True,
            "operation_kind": "unit_double",
            "stream_id": "run.monster_ai",
            "selected_double": unit,
            "selected_bool": None,
            "cursor_before": {"Counter": 16, **{
                "State" + str(i): x for i, x in enumerate(next_words)}},
            "cursor_after": {"Counter": 17, **{
                "State" + str(i): x for i, x in
                enumerate(advance(next_words))}},
        }
        first_event = {"type": "native_rng_boundary", "data": first}
        second_event = {"type": "native_rng_scalar_boundary", "data": second}
        report = audit([first_event, second_event])
        self.assertEqual("OBSERVED_NATIVE_RNG_PRIMITIVES_VALID",
                         report["status"])
        self.assertEqual(2, report["source_calls"])
        self.assertEqual(1, report["scalar_calls"])
        self.assertEqual(1, report["stream_pairs_contiguous"])
        self.assertEqual(0, report["stream_gap_count"])
        # A corrupted post-state must break the chain and fail independently.
        second["cursor_after"]["State0"] ^= 1
        corrupted = audit([first_event, second_event])
        self.assertEqual("NATIVE_RNG_PRIMITIVE_MISMATCH",
                         corrupted["status"])
        self.assertEqual(1, corrupted["primitive_error_count"])

    def test_scalar_unknown_and_known_streams_are_not_confused(self):
        first = sample()
        first["stream_id"] = "run.niche"
        another = sample()
        another.update(operation_kind="unit_double",
                       selected_double=0.0, selected_bool=None,
                       stream_id="UNATTRIBUTED")
        # Primitive result validity is independent of source owner.
        words = initial_state(971)
        number = (rotate((words[1] * 5) & MASK, 7) * 9) & MASK
        another["selected_double"] = (number >> 11) * (1 / (1 << 53))
        report = audit([
            {"type": "native_rng_boundary", "data": first},
            {"type": "native_rng_scalar_boundary", "data": another}])
        self.assertEqual(0, report["stream_pairs_checked"])
        self.assertEqual("OBSERVED_NATIVE_RNG_PRIMITIVES_VALID",
                         report["status"])


if __name__ == "__main__":
    unittest.main()
