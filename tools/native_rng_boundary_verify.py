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


def audit(records: list[dict]) -> dict:
    errors = []
    count = 0
    for ordinal, entry in enumerate(records, 1):
        if entry.get("type") != "native_rng_boundary":
            continue
        count += 1
        error = verify_call(entry.get("data") or {})
        if error:
            errors.append({"line": ordinal, "type": error,
                           "native_action_id": entry.get("action_id")})
    return {
        "schema": "native-rng-primitive-verification-v1",
        "status": ("SOURCE_RNG_NOT_OBSERVED" if count == 0
                   else "NATIVE_RNG_PRIMITIVE_MISMATCH" if errors
                   else "OBSERVED_NATIVE_RNG_PRIMITIVES_VALID"),
        "source_calls": count,
        "validated_calls": count - len(errors),
        "errors": errors[:20],
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
            else 1 if report["errors"] else 2)


if __name__ == "__main__":
    raise SystemExit(main())
