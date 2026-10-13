#!/usr/bin/env python3
"""Audit read-only native 12+3 persistent RNG cursor checkpoints.

Each game source checkpoint is an immutable copy of 15 RNG objects. The
auditor measures which xoshiro advances are independently explained by
instrumented primitive source calls between successive checkpoints.
This is an observation-coverage diagnostic, NOT a fidelity success verdict.
Context-created RNGs, pre-attachment draws and RNG legality stay separate.
"""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path

from native_rng_boundary_verify import field, verify_call, verify_scalar_call
from native_rng_stream_audit import MASK, advance

RUN_STREAMS = (
    "up_front", "shuffle", "unknown_map_point", "combat_card_generation",
    "combat_potion_generation", "combat_card_selection",
    "combat_energy_costs", "combat_targets", "monster_ai",
    "niche", "combat_orbs", "treasure_room_relics",
)
PLAYER_STREAMS = ("rewards", "shops", "transformations")
EXPECTED = frozenset(
    ["run." + value for value in RUN_STREAMS]
    + ["player." + value for value in PLAYER_STREAMS])
SOURCE_TYPES = frozenset(("native_rng_boundary", "native_rng_scalar_boundary"))
PHASES = frozenset(("combat_attach", "before_action", "after_action", "combat_end"))


def cursor(data: dict) -> tuple[int, ...] | None:
    if not isinstance(data, dict):
        return None
    result = tuple(field(data, k) for k in
                   ("counter", "state0", "state1", "state2", "state3"))
    if (type(result[0]) is not int or result[0] < 0
        or any(type(x) is not int or x < 0 or x > MASK
               for x in result[1:])):
        return None
    return result


def checkpoint(row: dict) -> tuple[str, dict[str, tuple[int, ...]]] | None:
    data = row.get("data")
    if not isinstance(data, dict) or data.get("phase") not in PHASES:
        return None
    if (data.get("source") != "native_rng_object_to_serializable"
        or data.get("rng_draws_performed") != 0
        or data.get("scope") != "15_persistent_streams_in_observed_combat"):
        return None
    streams = data.get("streams")
    if not isinstance(streams, list) or len(streams) != len(EXPECTED):
        return None
    states = {}
    for item in streams:
        if not isinstance(item, dict):
            return None
        name = item.get("stream_id")
        if name not in EXPECTED or name in states:
            return None
        if item.get("owner_id") != (
            "run" if name.startswith("run.") else "player:0"):
            return None
        state = cursor(item.get("cursor"))
        if state is None:
            return None
        states[name] = state
    if set(states) != EXPECTED:
        return None
    return data["phase"], states


def check_transition(before: tuple[int, ...], after: tuple[int, ...],
                     max_steps: int = 4096) -> str:
    steps = after[0] - before[0]
    if steps < 0:
        return "CURSOR_REWIND"
    if steps > max_steps:
        return "LARGE_WINDOW_STATE_NOT_REPLAYED"
    state = before[1:]
    for _ in range(steps):
        state = advance(state)
    return ("STATE_ADVANCE_VERIFIED" if state == after[1:]
            else "CHECKPOINT_XOSHIRO_STATE_MISMATCH")


def audit(records: list[dict]) -> dict:
    checkpoints = []
    issues = []
    stream_sums = defaultdict(lambda: {
        "observed_windows": 0, "native_rng_advances": 0,
        "hook_observed_advances": 0, "unobserved_advances": 0,
        "state_verified_windows": 0, "large_windows_unverified": 0})
    for pos, row in enumerate(records):
        if row.get("type") != "native_rng_stream_checkpoint":
            continue
        parsed = checkpoint(row)
        if parsed is None:
            issues.append({"type": "INVALID_CHECKPOINT", "line": pos + 1})
        else:
            phase, snapshots = parsed
            checkpoints.append((pos, phase, snapshots))
    if issues:
        return _report("INVALID_NATIVE_RNG_CHECKPOINT", checkpoints,
                       stream_sums, issues, 0)
    if not checkpoints:
        return _report("NATIVE_RNG_CHECKPOINTS_UNOBSERVED", checkpoints,
                       stream_sums, issues, 0)
    if checkpoints[0][1] != "combat_attach":
        issues.append({"type": "MISSING_COMBAT_ATTACH_CHECKPOINT",
                       "line": checkpoints[0][0] + 1})
    if checkpoints[-1][1] != "combat_end":
        issues.append({"type": "MISSING_COMBAT_END_CHECKPOINT",
                       "line": checkpoints[-1][0] + 1})
    unmatched_steps = 0
    for (a, phase_a, pre), (b, phase_b, post) in zip(
            checkpoints, checkpoints[1:]):
        by_stream = defaultdict(list)
        for pos in range(a + 1, b):
            row = records[pos]
            if row.get("type") not in SOURCE_TYPES:
                continue
            data = row.get("data") or {}
            if not isinstance(data, dict):
                issues.append({"type": "INVALID_SOURCE_CALL", "line": pos + 1})
                continue
            stream = data.get("stream_id")
            if stream not in EXPECTED:
                continue  # Context RNG identity is not certified.
            failure = (verify_call(data) if row["type"] == "native_rng_boundary"
                       else verify_scalar_call(data))
            if failure:
                issues.append({"type": "INVALID_SOURCE_CALL", "line": pos + 1,
                               "stream_id": stream, "reason": failure})
                continue
            by_stream[stream].append((pos + 1, cursor(data["cursor_before"]),
                                       cursor(data["cursor_after"])))
        for name in sorted(EXPECTED):
            before, after = pre[name], post[name]
            count = after[0] - before[0]
            if count < 0:
                issues.append({"type": "CURSOR_REWIND", "stream_id": name,
                               "previous_line": a + 1, "line": b + 1})
                continue
            events = by_stream[name]
            consumed = len(events)
            stats = stream_sums[name]
            stats["observed_windows"] += 1
            stats["native_rng_advances"] += count
            stats["hook_observed_advances"] += consumed
            missing = max(0, count - consumed)
            stats["unobserved_advances"] += missing
            unmatched_steps += missing
            state_check = check_transition(before, after)
            if state_check == "CHECKPOINT_XOSHIRO_STATE_MISMATCH":
                issues.append({"type": state_check, "stream_id": name,
                               "previous_line": a + 1, "line": b + 1})
            elif state_check == "LARGE_WINDOW_STATE_NOT_REPLAYED":
                stats["large_windows_unverified"] += 1
            else:
                stats["state_verified_windows"] += 1
            if consumed > count:
                issues.append({"type": "OBSERVED_CALLS_EXCEED_CURSOR_ADVANCES",
                               "stream_id": name, "line": b + 1})
            for line, source_before, source_after in events:
                if not (before[0] <= source_before[0]
                        and source_after[0] <= after[0]):
                    issues.append({"type": "SOURCE_CALL_OUTSIDE_CHECKPOINT_WINDOW",
                                   "stream_id": name, "line": line})
        if phase_a == "combat_end":
            issues.append({"type": "CHECKPOINT_AFTER_COMBAT_END",
                           "line": b + 1})
    hard = bool(issues)
    partial = unmatched_steps > 0 or any(
        s["large_windows_unverified"] for s in stream_sums.values())
    status = ("INVALID_NATIVE_RNG_CHECKPOINT" if hard
              else "NATIVE_RNG_OBSERVATION_COVERAGE_PARTIAL" if partial
              else "OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR")
    return _report(status, checkpoints, stream_sums, issues, unmatched_steps)


def _report(status, checkpoints, stream_sums, issues, missing):
    return {
        "schema": "native-rng-checkpoint-coverage-v1",
        "status": status,
        "checkpoint_count": len(checkpoints),
        "checkpoint_phases": [x[1] for x in checkpoints],
        "stream_count": 15 if checkpoints else 0,
        "expected_stream_ids": sorted(EXPECTED),
        "streams": {name: stream_sums[name] for name in sorted(EXPECTED)},
        "unobserved_rng_advances": missing,
        "error_count": len(issues),
        "errors": issues[:30],
        "context_rngs": "UNINSTRUMENTED_OWNER_IDENTITY",
        "pre_combat_rng": "BEFORE_FIRST_OBSERVED_COMBAT_FRAME",
        "semantic_candidate_eligibility": "NOT_TESTED",
        "native_game_parity": "NOT_TESTED",
        "exact_rng_parity": "NOT_TESTED",
        "coverage_interpretation": "OBSERVED_SOURCE_PRIMITIVE_CALLS_ONLY",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sidecar", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records = [json.loads(line) for line in
               args.sidecar.read_text(encoding="utf-8").splitlines()
               if line.strip()]
    result = audit(records)
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered, encoding="utf-8")
    return 1 if result["error_count"] else (
        2 if result["status"] != "OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR"
        else 0)


if __name__ == "__main__":
    raise SystemExit(main())
