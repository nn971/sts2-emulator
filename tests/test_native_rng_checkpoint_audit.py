"""Exact 15-stream native source snapshots are quantified fail-closed."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_rng_checkpoint_audit import EXPECTED, audit
from native_rng_stream_audit import MASK, initial_state, advance, rotate


def cursor(counter, state):
    return {"Counter": counter, **{
        "State" + str(i): x for i, x in enumerate(state)}}


def all_streams(offsets=None, mutated=None):
    offsets = offsets or {}
    mutated = mutated or {}
    result = []
    for i, name in enumerate(sorted(EXPECTED)):
        current = initial_state(971 + i)
        steps = offsets.get(name, 0)
        for _ in range(steps):
            current = advance(current)
        current = mutated.get(name, current)
        result.append({
            "stream_id": name,
            "owner_id": "run" if name.startswith("run.") else "player:0",
            "cursor": cursor(steps, current)})
    return result


def checkpoint(phase, offsets=None, mutated=None):
    return {"type": "native_rng_stream_checkpoint",
            "data": {"phase": phase,
                     "source": "native_rng_object_to_serializable",
                     "scope": "15_persistent_streams_in_observed_combat",
                     "rng_draws_performed": 0,
                     "streams": all_streams(offsets, mutated)}}


def integer_source(name, step):
    i = sorted(EXPECTED).index(name)
    state = initial_state(971 + i)
    for _ in range(step):
        state = advance(state)
    random64 = (rotate((state[1] * 5) & MASK, 7) * 9) & MASK
    return {
        "type": "native_rng_boundary",
        "data": {
            "origin": "source_primitive_hook",
            "source_hook_observed": True, "source_version": "v0.111.0",
            "operation_kind": "bounded_integer", "bounded_count": 5,
            "selected_index": int(((random64 >> 11) * (1 / (1 << 53))) * 5),
            "stream_id": name, "cursor_before": cursor(step, state),
            "cursor_after": cursor(step + 1, advance(state))}}


class NativeRngCheckpointAuditTests(unittest.TestCase):
    def test_all_15_snapshots_include_persistent_owners(self):
        r = audit([checkpoint("combat_attach"),
                   checkpoint("combat_end")])
        self.assertEqual("OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR",
                         r["status"])
        self.assertEqual(15, r["stream_count"])
        self.assertEqual(0, r["unobserved_rng_advances"])
        self.assertEqual(15, len(r["streams"]))

    def test_accounted_source_step_in_one_stream(self):
        name = "run.monster_ai"
        r = audit([checkpoint("combat_attach"), integer_source(name, 0),
                   checkpoint("before_action", {name: 1}),
                   checkpoint("after_action", {name: 1}),
                   checkpoint("combat_end", {name: 1})])
        self.assertEqual("OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR",
                         r["status"])
        self.assertEqual(1, r["streams"][name]["hook_observed_advances"])
        self.assertEqual(1, r["streams"][name]["native_rng_advances"])
        self.assertEqual(0, r["error_count"])

    def test_nextfloat_source_explains_exactly_one_monster_ai_advance(self):
        name = "run.monster_ai"
        state = initial_state(971 + sorted(EXPECTED).index(name))
        number = (rotate((state[1] * 5) & MASK, 7) * 9) & MASK
        unit = (number >> 11) * (1 / (1 << 53))
        import struct
        f32 = lambda x: struct.unpack("<f", struct.pack("<f", x))[0]
        source = {
            "type": "native_rng_scalar_boundary",
            "data": {
                "operation_kind": "range_float",
                "origin": "source_primitive_hook",
                "source_version": "v0.111.0",
                "source_hook_observed": True,
                "stream_id": name,
                "site_id": "UNATTRIBUTED",
                "owner_id": "creature:1",
                "float_min": 0.0,
                "float_max": 10.0,
                "selected_float": f32(unit * 10.0),
                "selected_double": None,
                "selected_bool": None,
                "cursor_before": cursor(0, state),
                "cursor_after": cursor(1, advance(state))
            }
        }
        report = audit([checkpoint("combat_attach"), source,
                        checkpoint("combat_end", {name: 1})])
        self.assertEqual("OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR",
                         report["status"])
        self.assertEqual(1, report["streams"][name]["hook_observed_advances"])
        self.assertEqual(0, report["streams"][name]["unobserved_advances"])
        self.assertEqual(0, report["error_count"])

    def test_missing_instrumented_primitive_is_reported_not_silenced(self):
        name = "run.shuffle"
        r = audit([checkpoint("combat_attach"),
                   checkpoint("combat_end", {name: 2})])
        self.assertEqual("NATIVE_RNG_OBSERVATION_COVERAGE_PARTIAL",
                         r["status"])
        self.assertEqual(2, r["unobserved_rng_advances"])
        self.assertEqual(2, r["streams"][name]["unobserved_advances"])
        self.assertEqual("NOT_TESTED", r["native_game_parity"])

    def test_duplicate_or_missing_source_owner_fails_checkpoint(self):
        start = checkpoint("combat_attach")
        end = checkpoint("combat_end")
        end["data"]["streams"][0]["stream_id"] = end["data"]["streams"][1]["stream_id"]
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT",
                         audit([start, end])["status"])
        end = checkpoint("combat_end")
        end["data"]["streams"].pop()
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT",
                         audit([start, end])["status"])

    def test_state_tamper_detected_even_when_counters_unchanged(self):
        name = "run.niche"
        state = list(initial_state(971 + sorted(EXPECTED).index(name)))
        state[0] ^= 4
        r = audit([checkpoint("combat_attach"),
                   checkpoint("combat_end", mutated={name: tuple(state)})])
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT", r["status"])
        self.assertEqual("CHECKPOINT_XOSHIRO_STATE_MISMATCH",
                         r["errors"][0]["type"])

    def test_invalid_primitive_and_outside_cursor_window(self):
        name = "run.monster_ai"
        corrupt = integer_source(name, 0)
        corrupt["data"]["selected_index"] = 99
        r = audit([checkpoint("combat_attach"), corrupt,
                   checkpoint("combat_end", {name: 1})])
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT", r["status"])
        self.assertEqual("INVALID_SOURCE_CALL", r["errors"][0]["type"])
        r = audit([checkpoint("combat_attach"), integer_source(name, 1),
                   checkpoint("combat_end", {name: 1})])
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT", r["status"])
        self.assertEqual("SOURCE_CALL_OUTSIDE_CHECKPOINT_WINDOW",
                         r["errors"][0]["type"])

    def test_missing_first_or_last_checkpoint_marks_incomplete(self):
        only_end = audit([checkpoint("combat_end")])
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT", only_end["status"])
        no_capture = audit([{"type": "header"}])
        self.assertEqual("NATIVE_RNG_CHECKPOINTS_UNOBSERVED",
                         no_capture["status"])

    def test_phase_only_metadata_never_is_recognized_as_real_snapshot(self):
        tampered = checkpoint("combat_attach")
        tampered["data"]["source"] = "rendered_frame_poll"
        self.assertEqual("INVALID_NATIVE_RNG_CHECKPOINT",
                         audit([tampered])["status"])


if __name__ == "__main__":
    unittest.main()
