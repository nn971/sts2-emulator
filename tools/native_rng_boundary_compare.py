#!/usr/bin/env python3
"""Compare *source-hook-observed* native RNG calls to an isolated emulator Step.

A native action ID is required to isolate a causal action. Source RNG events
must originate at the real native primitive hook; a frame cursor delta is not
such a call. A matching transcript proves only this audited primitive-call
sequence, never an entire combat or original conditional probability law.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_rng_boundary_verify import audit as verify_native_rng_primitives


def key(obj: dict, name: str):
    """Read either snake_case or C# PascalCase of a canonical field."""
    if not isinstance(obj, dict):
        return None
    return obj.get(name, obj.get("".join(x.capitalize() for x in name.split("_"))))


def compare_native_emulator(
    sidecar: list[dict], engine: dict, action_id: int,
    exact_native_cursors: bool = False,
) -> dict:
    native = []
    for rec in sidecar:
        if key(rec, "type") != "native_rng_boundary":
            continue
        if key(rec, "action_id") == action_id:
            native.append(key(rec, "data"))
    base = {
        "schema": "native-v111-rng-boundary-comparison-v1",
        "native_action_id": action_id,
        "source_hook_parity": "NOT_TESTED",
        "whole_combat_parity": "NOT_TESTED",
        "conditional_probability_law": "NOT_CALIBRATED",
        "exact_native_rng_cursor_comparison": exact_native_cursors,
        "native_call_count": len(native),
        "emulator_call_count": 0,
        "compared": 0,
        "first_mismatch": None,
        "native_primitive_verification": "NOT_CHECKED",
        "native_stream_gap_count": 0,
        "native_stream_conflict_count": 0,
    }

    if not native:
        return {**base, "status": "NATIVE_RNG_SOURCE_UNOBSERVED"}
    if any(not isinstance(n, dict) or
           key(n, "origin") != "source_primitive_hook" or
           key(n, "source_hook_observed") is not True
           for n in native):
        return {**base, "status": "NON_SOURCE_NATIVE_RNG_OBSERVATION"}
    if any(key(n, "attributed") is not True or
           "UNATTRIBUTED" in (key(n, "site_id"),
                               key(n, "stream_id"),
                               key(n, "owner_id"))
           for n in native):
        return {**base, "status": "NATIVE_RNG_CALLSITE_UNATTRIBUTED"}
    # A standalone comparison must also fail closed on source primitive
    # tampering and inter-call stream overlap. Otherwise a fabricated pair
    # of native/emulator journals could be labelled locally consistent.
    primitive = verify_native_rng_primitives(sidecar)
    base["native_primitive_verification"] = primitive["status"]
    base["native_stream_gap_count"] = primitive["stream_gap_count"]
    base["native_stream_conflict_count"] = primitive["stream_conflict_count"]
    if primitive["primitive_error_count"]:
        return {**base, "status": "NATIVE_RNG_PRIMITIVE_MISMATCH",
                "first_mismatch": primitive["errors"][0]}
    if primitive["stream_conflict_count"]:
        return {**base, "status": "NATIVE_RNG_STREAM_ORDER_CONFLICT",
                "first_mismatch": primitive["stream_conflicts"][0]}
    if key(engine, "executed_actual_engine_step") is not True:
        return {**base, "status": "EMULATOR_STEP_NOT_EXECUTED"}
    emulator = key(engine, "emulator_random_boundaries")
    if not isinstance(emulator, list):
        return {**base, "status": "EMULATOR_PROVIDER_JOURNAL_MISSING"}
    base["emulator_call_count"] = len(emulator)
    if not emulator:
        return {**base, "status": "EMULATOR_RNG_PROVIDER_UNOBSERVED"}

    semantic_eligibility_unverified = False
    for i, (native_row, emulator_row) in enumerate(zip(native, emulator)):
        req = key(emulator_row, "request")
        operation = key(req, "operation_kind")
        native_operation = key(native_row, "operation_kind")
        if native_operation != "bounded_integer":
            return {**base, "status": "UNSUPPORTED_NATIVE_RNG_OPERATION"}
        if operation == "choose_index":
            # At the source primitive NextInt we can audit only the bound
            # and the integer outcome. The semantic candidate ordering
            # belongs to a separate, source-audited mechanic-level hook.
            ordered = key(req, "ordered_candidates")
            if not isinstance(ordered, list) or not ordered:
                return {**base, "status": "EMULATOR_ELIGIBILITY_UNAVAILABLE"}
            expected_bound = len(ordered)
            semantic_eligibility_unverified = True
        elif operation == "bounded_integer":
            expected_bound = key(req, "bounded_count")
        else:
            return {**base, "status": "UNSUPPORTED_OPERATION_MAPPING"}
        fields = [
            ("site_id", key(native_row, "site_id"), key(req, "site_id")),
            ("stream_id", key(native_row, "stream_id"), key(req, "stream_id")),
            ("owner_id", key(native_row, "owner_id"), key(req, "owner_id")),
            ("call_ordinal", key(native_row, "call_ordinal"), key(req, "ordinal")),
            ("bounded_count", key(native_row, "bounded_count"),
             expected_bound),
            ("selected_index", key(native_row, "selected_index"),
             key(emulator_row, "selected_index")),
            ("source_version", key(native_row, "source_version"),
             key(req, "source_version")),
        ]
        for name, actual, expected in fields:
            if actual is None or expected is None or actual != expected:
                return {**base, "status": "RNG_BOUNDARY_MISMATCH",
                        "compared": i,
                        "first_mismatch": {
                            "index": i, "field": name,
                            "native": actual, "emulator": expected}}
        if exact_native_cursors:
            for name in ("cursor_before", "cursor_after"):
                a = key(native_row, name)
                b = key(emulator_row, "native_" + name)
                words = ("counter", "state0", "state1", "state2", "state3")
                if a is None or b is None or any(
                    key(a, x) is None or key(a, x) != key(b, x)
                    for x in words
                ):
                    return {**base, "status": "RNG_BOUNDARY_MISMATCH",
                            "compared": i, "first_mismatch": {
                                "index": i, "field": name}}
        base["compared"] = i + 1
    if len(native) != len(emulator):
        return {**base, "status": "RNG_BOUNDARY_COUNT_MISMATCH",
                "first_mismatch": {
                    "index": base["compared"], "field": "call_count",
                    "native": len(native), "emulator": len(emulator)}}
    if primitive["stream_gap_count"]:
        return {**base,
                "status": "OBSERVED_RNG_PRIMITIVE_WITH_UNOBSERVED_STREAM_ADVANCES",
                "semantic_eligibility_parity": "NOT_TESTED"}
    if semantic_eligibility_unverified:
        return {**base,
                "status": "OBSERVED_RNG_PRIMITIVE_INDEX_ONLY_ELIGIBILITY_UNVERIFIED"}
    return {**base, "status": "OBSERVED_RNG_CALL_SEQUENCE_CONSISTENT"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_sidecar", type=Path)
    parser.add_argument("emulator_single_fixture_report", type=Path)
    parser.add_argument("--native-action-id", type=int, required=True)
    parser.add_argument("--exact-native-cursors", action="store_true")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records = [json.loads(line) for line in
               args.native_sidecar.read_text(encoding="utf-8").splitlines()
               if line.strip()]
    engine = json.loads(args.emulator_single_fixture_report.read_text(encoding="utf-8"))
    result = compare_native_emulator(
        records, engine, args.native_action_id, args.exact_native_cursors)
    output = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(output)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return (0 if result["status"] == "OBSERVED_RNG_CALL_SEQUENCE_CONSISTENT"
            else 1 if result["status"] in
                 ("RNG_BOUNDARY_MISMATCH", "RNG_BOUNDARY_COUNT_MISMATCH",
                  "NATIVE_RNG_PRIMITIVE_MISMATCH",
                  "NATIVE_RNG_STREAM_ORDER_CONFLICT")
            else 2)


if __name__ == "__main__":
    raise SystemExit(main())
