"""Native shuffle source evidence must reproduce exact ordered Fisher-Yates."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_rng_stream_audit import MASK, advance, initial_state, rotate
from native_shuffle_operation_verify import validate, audit


def sample():
    original = [f"card:{i}" for i in range(5)]
    pool = original[:]
    state = initial_state(183)
    rows = []
    for end in range(len(pool) - 1, 0, -1):
        raw = (rotate((state[1] * 5) & MASK, 7) * 9) & MASK
        idx = int(((raw >> 11) * (1 / (1 << 53))) * (end + 1))
        after = advance(state)
        def cursor(count, words):
            return {"Counter": count, **{
                f"State{i}": x for i, x in enumerate(words)}}
        rows.append({
            "bounded_count": end + 1, "selected_index": idx,
            "cursor_before": cursor(7 + len(rows), state),
            "cursor_after": cursor(8 + len(rows), after)
        })
        pool[end], pool[idx] = pool[idx], pool[end]
        state = after
    return {
        "source_version": "v0.111.0", "operation_id": 101,
        "kind": "recycle", "ordered_input": original,
        "choices": rows, "pre_hook_order": pool[:],
        "post_hook_order": pool[:],
        "card_models": ["Strike", "Defend", "Strike", "Defend", "Neutralize"],
        "card_identity_scope": "shuffle_operation_local",
        "hook_order_stage": "modify_shuffle_order_completed"
    }


class SourceShuffleTests(unittest.TestCase):
    def test_verified_source_input_swaps_and_after_hooks(self):
        d = sample()
        self.assertIsNone(validate(d))
        result = audit([{"type": "native_shuffle_operation", "data": d}])
        self.assertEqual("OBSERVED_SHUFFLE_STAGES_VALID", result["status"])
        self.assertEqual(1, result["validated_shuffles"])
        self.assertEqual(4, result["source_shuffle_rng_calls"])
        self.assertEqual("NOT_TESTED", result["emulator_parity"])

    def test_hook_reordering_is_legal_but_removing_cards_fails(self):
        d = sample()
        d["post_hook_order"].reverse()
        self.assertIsNone(validate(d))
        d["post_hook_order"][0] = d["post_hook_order"][1]
        self.assertEqual("POST_HOOK_CARD_IDENTITY_MISMATCH", validate(d))

    def test_wrong_bound_and_wrong_selection_and_wrong_pre_hook_order_fail(self):
        d = sample()
        d["choices"][0]["bounded_count"] = 99
        self.assertEqual("SHUFFLE_CHOICE_BOUND_MISMATCH", validate(d))
        d = sample()
        d["choices"][0]["selected_index"] = (
            d["choices"][0]["selected_index"] + 1) % 5
        self.assertEqual("SHUFFLE_BOUNDED_INT_RESULT_MISMATCH", validate(d))
        d = sample()
        d["pre_hook_order"].reverse()
        self.assertEqual("PRE_HOOK_SHUFFLE_PERMUTATION_MISMATCH", validate(d))

    def test_source_cursor_gap_fails(self):
        d = sample()
        d["choices"][2]["cursor_before"]["Counter"] += 2
        self.assertEqual("SHUFFLE_INVALID_NATIVE_CURSOR", validate(d))

    def test_empty_old_capture_is_not_certified(self):
        self.assertEqual("NO_NATIVE_SHUFFLE_SOURCE_EVENTS",
                         audit([{"type": "header"}])["status"])

    def test_duplicate_operation_identity_fails(self):
        d = sample()
        result = audit([
            {"type": "native_shuffle_operation", "data": d},
            {"type": "native_shuffle_operation", "data": d}])
        self.assertEqual("NATIVE_SHUFFLE_MISMATCH", result["status"])
        self.assertEqual(1, len(result["errors"]))


if __name__ == "__main__":
    unittest.main()
