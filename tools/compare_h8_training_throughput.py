#!/usr/bin/env python3
"""Compare matched H8 full-Transformer PPO benchmarks on exactly the same sources.

The report distinguishes end-to-end rollout throughput from emulator-only
mechanics changes. Fails closed on mismatched model, stochastic source
schedule, incomplete terminal outcomes or missing 128/128 legacy parity.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def compare(before: dict[str, Any], after: dict[str, Any]) -> dict[str, Any]:
    required_equal = (
        "schema", "model_sha256", "model_parameters", "source_digest",
        "reference_law", "source_count", "workers",
        "max_boundaries", "replay_every",
    )
    for key in required_equal:
        if before.get(key) != after.get(key):
            raise ValueError(
                f"H8 benchmark mismatch: {key}: "
                f"{before.get(key)!r} != {after.get(key)!r}")
    expected = before["source_count"]
    for name, item in (("before", before), ("after", after)):
        if item.get("terminal_counts") != {"terminal": expected}:
            raise ValueError(f"{name} has censored or incomplete combats")
        if item.get("verified_terminal_replays") != (
            expected + item["replay_every"] - 1) // item["replay_every"]:
            raise ValueError(f"{name} replay audit selection changed")
        legacy = item.get("legacy_h7")
        if not isinstance(legacy, dict) or (
            legacy.get("matching_source_trajectories") != expected
            or legacy.get("player_decisions") !=
                item["native_h8_actor_metrics"]["requests"]
        ):
            raise ValueError(f"{name} lacks exact legacy trajectory evidence")
        if item["native_h8_actor_metrics"]["requests"] <= 0:
            raise ValueError(f"{name} has no real Transformer decisions")
    if (before["victories"], before["defeats"]) != (
            after["victories"], after["defeats"]):
        raise ValueError("Matched H8 policy and source outcome counts diverged")

    b_roll = before["native_h8_rollout_seconds"]
    a_roll = after["native_h8_rollout_seconds"]
    b_iter = before["total_h8_iteration_seconds"]
    a_iter = after["total_h8_iteration_seconds"]
    if min(b_roll, a_roll, b_iter, a_iter) <= 0:
        raise ValueError("Invalid nonpositive benchmark runtime")
    def actor(name: str, data: dict[str, Any]) -> dict[str, Any]:
        m = data["native_h8_actor_metrics"]
        return dict(
            batches=m["batches"],
            requests=m["requests"],
            mean_batch_size=m["requests"] / m["batches"],
            forward_seconds=m["forward_seconds"],
            collate_seconds=m["collate_seconds"],
        )
    return dict(
        schema="sts2-h8-full-transformer-throughput-comparison-v1",
        model_sha256=before["model_sha256"],
        source_digest=before["source_digest"],
        source_count=expected,
        matched_legacy_trajectories=expected,
        before_emulator_revision=before["emulator_revision"],
        after_emulator_revision=after["emulator_revision"],
        rollout_seconds_before=b_roll,
        rollout_seconds_after=a_roll,
        rollout_speedup=b_roll / a_roll,
        combats_per_second_before=expected / b_roll,
        combats_per_second_after=expected / a_roll,
        training_iteration_seconds_before=b_iter,
        training_iteration_seconds_after=a_iter,
        iteration_speedup=b_iter / a_iter,
        ppo_seconds_before=before["ppo_seconds"],
        ppo_seconds_after=after["ppo_seconds"],
        actor_before=actor("before", before),
        actor_after=actor("after", after),
        combat_throughput_target=50,
        reached_50_combats_per_second=expected / a_roll >= 50,
        attribution_note=(
            "End-to-end rollout speedup includes JSONL, source mechanics, "
            "provider Chance Law, shared actor scheduling, feature collation "
            "and CUDA forward time. This is not an isolated emulator speedup."
        ),
    )


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--before", type=Path, required=True)
    p.add_argument("--after", type=Path, required=True)
    p.add_argument("--output", type=Path)
    args = p.parse_args()
    result = compare(
        json.loads(args.before.read_text(encoding="utf-8")),
        json.loads(args.after.read_text(encoding="utf-8")),
    )
    output = json.dumps(result, indent=2) + "\n"
    print(output, end="")
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output, encoding="utf-8")


if __name__ == "__main__":
    main()
