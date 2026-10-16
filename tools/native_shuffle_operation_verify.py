#!/usr/bin/env python3
"""Verify pinned v0.111 native shuffle operations against actual RNG choices.

The record is SOURCE evidence, not a replay from a rendered UI frame.
Card tokens are local to one operation, so duplicate card models cannot be
identified across independent game/emulator runs yet. Compare separate
mechanics-level instance identity before asserting whole-combat parity.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_rng_boundary_verify import verify_call


def _cursor(c):
    return tuple(c.get(k, c.get(k[0].upper() + k[1:])) for k in
                 ("counter", "state0", "state1", "state2", "state3"))


def validate(data: dict) -> str | None:
    if (data.get("kind") not in ("initial", "recycle")
        or data.get("source_version") != "v0.111.0"
        or data.get("card_identity_scope") != "shuffle_operation_local"
        or data.get("hook_order_stage") != "modify_shuffle_order_completed"):
        return "UNVERIFIED_SHUFFLE_SOURCE"
    seq = data.get("ordered_input")
    pre = data.get("pre_hook_order")
    post = data.get("post_hook_order")
    models = data.get("card_models")
    choices = data.get("choices")
    if any(not isinstance(x, list) for x in (seq, pre, post, models, choices)):
        return "MISSING_SHUFFLE_STAGE"
    if (len(models) != len(seq) or
        len(seq) != len(set(seq)) or
        not all(isinstance(token, str) and token for token in seq)):
        return "INVALID_SHUFFLE_CARD_TOKENS"
    if len(choices) != max(0, len(seq) - 1):
        return "MISSING_FISHER_YATES_CHOICE"
    replay = list(seq)
    for i, choice in enumerate(choices):
        bound = len(seq) - i
        if (choice.get("bounded_count") != bound
            or type(choice.get("selected_index")) is not int
            or not 0 <= choice["selected_index"] < bound):
            return "SHUFFLE_CHOICE_BOUND_MISMATCH"
        checked = verify_call({
            "origin": "source_primitive_hook",
            "operation_kind": "bounded_integer",
            "source_version": "v0.111.0",
            "source_hook_observed": True,
            **choice})
        if checked:
            return "SHUFFLE_" + checked
        if i and _cursor(choices[i - 1]["cursor_after"]) != _cursor(
                choice["cursor_before"]):
            return "SHUFFLE_CURSOR_DISCONTINUITY"
        end = bound - 1
        j = choice["selected_index"]
        replay[end], replay[j] = replay[j], replay[end]
    if replay != pre:
        return "PRE_HOOK_SHUFFLE_PERMUTATION_MISMATCH"
    if len(post) != len(seq) or sorted(post) != sorted(seq):
        return "POST_HOOK_CARD_IDENTITY_MISMATCH"
    # Changes between pre and post are source hook effects; the verifier
    # accepts a permutation and records that parity separately.
    return None


def audit(records: list[dict]) -> dict:
    events = []
    seen_ids = set()
    errors = []
    matched_rng = 0
    primitives = {
        (str(d.get("cursor_before")), str(d.get("cursor_after")),
         d.get("bounded_count"), d.get("selected_index"))
        for r in records if r.get("type") == "native_rng_boundary"
        for d in [r.get("data") or {}]
    }
    for line, row in enumerate(records, 1):
        if row.get("type") != "native_shuffle_operation":
            continue
        data = row.get("data") or {}
        kind = data.get("kind")
        status = validate(data)
        operation_id = data.get("operation_id")
        if operation_id in seen_ids:
            status = "DUPLICATE_SHUFFLE_OPERATION_ID"
        seen_ids.add(operation_id)
        for choice in data.get("choices") or []:
            key = (str(choice.get("cursor_before")),
                   str(choice.get("cursor_after")),
                   choice.get("bounded_count"), choice.get("selected_index"))
            if key in primitives:
                matched_rng += 1
        events.append({
            "line": line,
            "operation_id": operation_id,
            "kind": kind,
            "card_count": len(data.get("ordered_input") or []),
            "rng_calls": len(data.get("choices") or []),
            "hook_modified_order": (
                data.get("pre_hook_order") != data.get("post_hook_order")),
            "status": status or "SOURCE_SHUFFLE_STAGES_VALID"
        })
        if status:
            errors.append({"line": line, "kind": kind, "reason": status})
    return {
        "schema": "native-shuffle-operation-verification-v1",
        "status": ("NO_NATIVE_SHUFFLE_SOURCE_EVENTS" if not events
                   else "NATIVE_SHUFFLE_MISMATCH" if errors
                   else "OBSERVED_SHUFFLE_STAGES_VALID"),
        "source_shuffles": len(events),
        "validated_shuffles": len(events) - len(errors),
        "source_shuffle_rng_calls": sum(x["rng_calls"] for x in events),
        "primitive_calls_correlated": matched_rng,
        "semantic_card_identity_across_runs": "NOT_TESTED",
        "complete_shuffles_and_hooks": "OBSERVED_SOURCE_OPERATIONS_ONLY",
        "emulator_parity": "NOT_TESTED",
        "operations": events,
        "errors": errors[:20]
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sidecar", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records = [
        json.loads(line) for line in
        args.sidecar.read_text(encoding="utf-8").splitlines() if line.strip()
    ]
    result = audit(records)
    output = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(output)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return (0 if result["status"] == "OBSERVED_SHUFFLE_STAGES_VALID"
            else 1 if result["errors"] else 2)


if __name__ == "__main__":
    raise SystemExit(main())
