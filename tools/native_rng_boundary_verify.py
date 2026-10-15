#!/usr/bin/env python3
"""Independently verify actual pinned-game RNG primitive-call sidecar records.

Validates every observed v111 NextInt source call against the exact
xoshiro256** one-step transition and the original integer sampling rule.
The verifier runs *after* a game trace is recorded and never invokes game RNG.
Unrecorded source calls, original eligibility lists and causal hook order
remain unverified. Existing legacy action sidecars are accepted as evidence
of zero captured source calls, never full parity.
"""
from __future__ import annotations

import argparse
import json
import math
import struct
from pathlib import Path

from native_rng_stream_audit import MASK, advance, rotate


def field(obj: dict, key: str):
    return obj.get(key, obj.get(key[0].upper() + key[1:]))


def verify_call(data: dict) -> str | None:
    if (data.get("origin") != "source_primitive_hook"
        or data.get("operation_kind") != "bounded_integer"
        or data.get("source_version") != "v0.111.0"
        or data.get("source_hook_observed") is not True):
        return "UNVERIFIED_SOURCE_OR_OPERATION"
    bound = data.get("bounded_count")
    index = data.get("selected_index")
    before = data.get("cursor_before")
    after = data.get("cursor_after")
    if type(bound) is not int or bound <= 0 or type(index) is not int:
        return "INVALID_BOUNDED_INDEX"
    if not isinstance(before, dict) or not isinstance(after, dict):
        return "MISSING_NATIVE_CURSOR"
    names = ("state0", "state1", "state2", "state3")
    pre, post = ([field(snapshot, x) for x in names]
                 for snapshot in (before, after))
    counter0 = field(before, "counter")
    counter1 = field(after, "counter")
    if (any(type(v) is not int or v < 0 or v > MASK for v in pre + post)
        or type(counter0) is not int
        or type(counter1) is not int or counter0 < 0
        or counter1 != counter0 + 1):
        return "INVALID_NATIVE_CURSOR"
    # xoshiro256** emits rotl(s1 * 5, 7) * 9 before mutating state.
    random64 = (rotate((pre[1] * 5) & MASK, 7) * 9) & MASK
    next_double = (random64 >> 11) * (1.0 / (1 << 53))
    expected_index = int(next_double * bound)
    if tuple(post) != advance(tuple(pre)):
        return "XOSHIRO_STATE_TRANSITION_MISMATCH"
    if expected_index != index:
        return "BOUNDED_INT_RESULT_MISMATCH"
    return None


def verify_scalar_call(data: dict) -> str | None:
    """Verify pinned native NextDouble, NextBool or ranged NextFloat and one step."""
    if (data.get("origin") != "source_primitive_hook"
        or data.get("operation_kind") not in ("unit_double", "boolean", "range_float")
        or data.get("source_version") != "v0.111.0"
        or data.get("source_hook_observed") is not True):
        return "UNVERIFIED_SCALAR_SOURCE_OR_OPERATION"
    before, after = data.get("cursor_before"), data.get("cursor_after")
    if not isinstance(before, dict) or not isinstance(after, dict):
        return "MISSING_NATIVE_CURSOR"
    try:
        pre, post = _cursor(before), _cursor(after)
    except (TypeError, AttributeError):
        return "INVALID_NATIVE_CURSOR"
    if (any(type(v) is not int or v < 0 or v > MASK
            for v in pre[1:] + post[1:])
        or type(pre[0]) is not int or pre[0] < 0
        or type(post[0]) is not int or post[0] != pre[0] + 1):
        return "INVALID_NATIVE_CURSOR"
    if post[1:] != advance(pre[1:]):
        return "XOSHIRO_STATE_TRANSITION_MISMATCH"
    random64 = (rotate((pre[2] * 5) & MASK, 7) * 9) & MASK
    next_double = (random64 >> 11) * (1.0 / (1 << 53))
    kind = data["operation_kind"]
    if kind == "unit_double":
        actual = data.get("selected_double")
        if (type(actual) not in (int, float)
            or not math.isfinite(actual) or not 0.0 <= actual < 1.0
            or data.get("selected_bool") is not None):
            return "INVALID_SCALAR_VALUE"
        if actual != next_double:
            return "SCALAR_DOUBLE_RESULT_MISMATCH"
    elif kind == "boolean":
        actual = data.get("selected_bool")
        if type(actual) is not bool or data.get("selected_double") is not None:
            return "INVALID_SCALAR_VALUE"
        if actual != (int(next_double * 2) == 0):
            return "SCALAR_BOOL_RESULT_MISMATCH"
    else:
        minimum, maximum = data.get("float_min"), data.get("float_max")
        actual = data.get("selected_float")
        if (any(type(x) not in (int, float) or not math.isfinite(x)
                for x in (minimum, maximum, actual))
            or data.get("selected_double") is not None
            or data.get("selected_bool") is not None):
            return "INVALID_SCALAR_VALUE"
        try:
            # Pinned Rng.NextFloat(float,float) calculates the DIFFERENCE
            # in float32, converts that difference to double, multiplies
            # MegaRandom.NextDouble, adds min in double, and converts the
            # final result back to float32. No extra PRNG step is taken.
            f32 = lambda x: struct.unpack("<f", struct.pack("<f", x))[0]
            if any(f32(x) != x for x in (minimum, maximum, actual)):
                return "INVALID_SCALAR_VALUE"
            if minimum > maximum:
                return "INVALID_SCALAR_VALUE"
            expected = f32(next_double * f32(maximum - minimum) + minimum)
        except (OverflowError, struct.error):
            return "INVALID_SCALAR_VALUE"
        if actual != expected:
            return "SCALAR_FLOAT_RESULT_MISMATCH"
    return None


def _cursor(snapshot: dict) -> tuple[int, ...]:
    """Canonical counter + four state words from an individually valid call."""
    return (field(snapshot, "counter"),) + tuple(
        field(snapshot, name) for name in ("state0", "state1", "state2", "state3")
    )


def audit(records: list[dict]) -> dict:
    errors = []
    conflicts = []
    gaps = []
    count = 0
    scalar_count = 0
    float_count = 0
    continuous_pairs = 0
    checked_pairs = 0
    # The native owner registry identifies persistent run/player Rng instances
    # by reference for ONE combat. Scope-level owner_id may be a creature and
    # call_ordinal may reset on each RollMove; neither keys stream continuity.
    last_by_stream: dict[str, tuple[int, tuple[int, ...]]] = {}
    streams_seen: set[str] = set()
    for line, entry in enumerate(records, 1):
        typ = entry.get("type")
        if typ not in ("native_rng_boundary", "native_rng_scalar_boundary"):
            continue
        count += 1
        if typ == "native_rng_scalar_boundary":
            scalar_count += 1
        data = entry.get("data") or {}
        if (typ == "native_rng_scalar_boundary" and isinstance(data, dict)
            and data.get("operation_kind") == "range_float"):
            float_count += 1
        error = ((verify_scalar_call(data)
                  if typ == "native_rng_scalar_boundary" else verify_call(data))
                 if isinstance(data, dict) else "INVALID_SOURCE_EVENT")
        stream = data.get("stream_id") if isinstance(data, dict) else None
        persistent = (isinstance(stream, str) and
                      (stream.startswith("run.") or stream.startswith("player.")))
        if error:
            errors.append({"line": line, "type": error,
                           "native_action_id": entry.get("action_id")})
            # An invalid middle observation cannot establish an adjacent pair.
            if persistent:
                last_by_stream.pop(stream, None)
            continue
        if not persistent:
            continue  # Context/UNATTRIBUTED objects have no verified identity.
        streams_seen.add(stream)
        before = _cursor(data["cursor_before"])
        after = _cursor(data["cursor_after"])
        prior = last_by_stream.get(stream)
        if prior is not None:
            checked_pairs += 1
            previous_line, previous_after = prior
            difference = before[0] - previous_after[0]
            if difference == 0 and before == previous_after:
                continuous_pairs += 1
            elif difference > 0:
                # Forward advances can be unhooked double/float primitives,
                # other source calls, or dropped records; they are a *coverage
                # gap*, not proof that an integer source callback was missed.
                gaps.append({
                    "stream_id": stream, "previous_line": previous_line,
                    "line": line, "unobserved_rng_advances": difference,
                    "type": "UNOBSERVED_STREAM_ADVANCE",
                })
            else:
                # Rewind / overlapping source hooks / same-counter different
                # states can also indicate a stream reset or changed binding.
                # They require investigation; never certify ordered evidence.
                conflicts.append({
                    "stream_id": stream, "previous_line": previous_line,
                    "line": line,
                    "type": ("STREAM_CURSOR_REWIND_OR_OVERLAP"
                             if difference < 0 else "SAME_COUNTER_STATE_CONFLICT"),
                    "previous_after_counter": previous_after[0],
                    "next_before_counter": before[0],
                })
        last_by_stream[stream] = (line, after)
    status = (
        "SOURCE_RNG_NOT_OBSERVED" if count == 0
        else "NATIVE_RNG_PRIMITIVE_MISMATCH" if errors
        else "NATIVE_RNG_STREAM_ORDER_CONFLICT" if conflicts
        else "OBSERVED_NATIVE_RNG_PRIMITIVES_WITH_STREAM_GAPS" if gaps
        else "OBSERVED_NATIVE_RNG_PRIMITIVES_VALID"
    )
    return {
        "schema": "native-rng-primitive-verification-v1",
        "status": status,
        "source_calls": count,
        "bounded_calls": count - scalar_count,
        "scalar_calls": scalar_count,
        "float_calls": float_count,
        "validated_calls": count - len(errors),
        "primitive_error_count": len(errors),
        "errors": errors[:20],
        "stream_conflict_count": len(conflicts),
        "stream_conflicts": conflicts[:20],
        "stream_gap_count": len(gaps),
        "stream_gaps": gaps[:20],
        "persistent_streams_observed": sorted(streams_seen),
        "stream_pairs_checked": checked_pairs,
        "stream_pairs_contiguous": continuous_pairs,
        "stream_continuity_scope": "OBSERVED_PERSISTENT_RUN_PLAYER_STREAMS_WITHIN_SIDECAR",
        "semantic_eligibility_parity": "NOT_TESTED",
        "native_hook_order_parity": "NOT_TESTED",
        "unobserved_rng_primitives": "UNKNOWN",
        "emulator_parity": "NOT_TESTED",
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("sidecar", type=Path)
    ap.add_argument("--out", type=Path)
    args = ap.parse_args()
    records = [
        json.loads(line) for line in
        args.sidecar.read_text(encoding="utf-8").splitlines() if line.strip()
    ]
    report = audit(records)
    output = json.dumps(report, indent=2, sort_keys=True) + "\n"
    print(output)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return (0 if report["status"] == "OBSERVED_NATIVE_RNG_PRIMITIVES_VALID"
            else 1 if (report["primitive_error_count"] or
                       report["stream_conflict_count"]) else 2)


if __name__ == "__main__":
    raise SystemExit(main())
