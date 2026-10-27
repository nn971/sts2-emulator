#!/usr/bin/env python3
"""Compare source-matched H7 before/after semantic boundary experiments.

Input directories must each contain h7.json and native.json as generated
by tools/benchmark_h7_rollouts.py and summarize_semantic_perf.py.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


IDENTICAL = (
    "schema", "chance_law", "evidence", "model_sha256", "parameters",
    "workers_requested", "worker_processes_observed", "combats",
    "train_cells", "by_cell", "victories", "player_decisions",
    "total_boundaries", "source_digest", "max_boundaries", "study_seed",
    "actor_seed", "reference_seed", "actor_backend", "actor_device",
    "actor_max_batch", "actor_delay_ms", "initialization", "model_config",
    "emulator_bindings_sha256",
)
# Only protocol-level operation counts must match. Hash invocation counts
# are optimization results; reducing them is the purpose of this experiment.
MATCHED_COUNT_STAGES = frozenset((
    "op.semantic_resolve_draw_card",
    "sub.semantic_resolve_draw_card_transition",
    "sub.semantic_draw_result_chance_sites",
    "sub.semantic_draw_result_controller_mode",
))
STAGES = (
    "op.semantic_resolve_draw_card",
    "sub.semantic_resolve_draw_card_transition",
    "sub.semantic_draw_result_chance_sites",
    "sub.semantic_draw_result_controller_mode",
    "sub.semantic_draw_hash_RunState",
    "sub.semantic_draw_hash_SemanticPilotCombatV111",
)


def compare(before: Path, after: Path) -> str:
    a = json.loads((before / "h7.json").read_text(encoding="utf-8"))
    b = json.loads((after / "h7.json").read_text(encoding="utf-8"))
    for key in IDENTICAL:
        if a.get(key) != b.get(key):
            raise ValueError(f"Incomparable H7 runs: `{key}` differs: "
                             f"{a.get(key)!r} vs {b.get(key)!r}")
    if a.get("emulator_revision") == b.get("emulator_revision"):
        raise ValueError("The experiments use the same emulator revision")
    op_a = json.loads((before / "native.json").read_text(encoding="utf-8"))["operations"]
    op_b = json.loads((after / "native.json").read_text(encoding="utf-8"))["operations"]
    lines = [
        f"Matched sources: {a['combats']} combats, "
        f"{a['player_decisions']} decisions; "
        f"draws: {op_a['op.semantic_resolve_draw_card']['count']}",
        f"Emulator: {a['emulator_revision'][:12]} -> "
        f"{b['emulator_revision'][:12]}",
    ]
    draw_count = op_a["op.semantic_resolve_draw_card"]["count"]
    if draw_count != op_b["op.semantic_resolve_draw_card"]["count"]:
        raise ValueError("Semantic draw counts differ; cannot compare timings")

    def row(name: str, x: float, y: float, units: str) -> None:
        change = (y / x - 1) * 100 if x else float("nan")
        lines.append(
            f"{name:<44} {x:>12.3f} -> {y:>12.3f} {units:<8}"
            f"  ({change:+.1f}%)"
        )

    row("Cohort wall", a["elapsed_seconds"], b["elapsed_seconds"], "s")
    row(
        "Player decisions per second", a["player_decisions_per_second"],
        b["player_decisions_per_second"], "/s"
    )
    for name in STAGES:
        if name not in op_a or name not in op_b:
            raise ValueError(f"Missing profiler counter in either run: {name}")
        x, y = op_a[name], op_b[name]
        if name in MATCHED_COUNT_STAGES and x["count"] != y["count"]:
            raise ValueError(f"Counter count differs: {name}")
        # Print hashes per draw instead of imposing identical hash counts.
        # Reduced canonical hashing is the expected optimization effect.
        if name.startswith("sub.semantic_draw_hash_"):
            row(name + " (calls per draw)",
                x["count"] / draw_count,
                y["count"] / draw_count, "/draw")
        row(name + " (alloc per draw)",
            x["allocatedBytes"] / draw_count / 1e6,
            y["allocatedBytes"] / draw_count / 1e6, "MB/draw")
        row(name + " (time per draw)",
            x["totalSeconds"] / draw_count * 1e3,
            y["totalSeconds"] / draw_count * 1e3, "ms/draw")
    lines.append(
        "Caution: nested sub.* counters overlap. Never sum their allocations."
    )
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    args = parser.parse_args()
    print(compare(args.before, args.after))


if __name__ == "__main__":
    main()
