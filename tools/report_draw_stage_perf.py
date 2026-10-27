#!/usr/bin/env python3
"""Report nested semantic card-draw profiler stages normalized per draw.

All source metrics are *nested* wall-clock and allocation samples recorded on
the same .NET thread. They overlap and must never be summed as independent work.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("profile", type=Path, help="Aggregate native.json")
    args = parser.parse_args()
    data = json.loads(args.profile.read_text(encoding="utf-8"))
    ops = data["operations"]
    overall = ops.get("op.semantic_resolve_draw_card")
    transition = ops.get("sub.semantic_resolve_draw_card_transition")
    if not overall or not transition:
        raise SystemExit("Missing full draw profiler counters")
    count = overall["count"]
    if count < 1 or count != transition["count"]:
        raise SystemExit("Missing or inconsistent draw counts")
    stages = sorted(
        ((name, metrics) for name, metrics in ops.items()
         if name.startswith(("sub.semantic_draw_", "sub.semantic_result_chance_sites"))),
        key=lambda pair: -pair[1]["allocatedBytes"],
    )
    if not stages:
        raise SystemExit(
            "No nested stage data; use the diagnostic emulator branch with "
            "STS2_SEMANTIC_PROFILE_DIR enabled"
        )
    print(f"Draw calls: {count}; workers: {data['workers']}")
    print(
        f"Whole op: {overall['totalSeconds'] / count * 1e3:.1f} ms/call, "
        f"{overall['allocatedBytes'] / count / 1e6:.2f} MB/call"
    )
    print(
        f"Core transition: {transition['totalSeconds'] / count * 1e3:.1f} ms/call, "
        f"{transition['allocatedBytes'] / count / 1e6:.2f} MB/call"
    )
    print("Each stage is nested; overlapping measurements MUST NOT be added.")
    print(f"{'Stage':57} {'Calls':>8} {'ms/call':>11} {'MB/call':>11} {'MB/draw':>11}")
    for name, row in stages:
        n = row["count"]
        if n == 0:
            continue
        print(
            f"{name.removeprefix('sub.semantic_'):57} {n:8d} "
            f"{row['totalSeconds'] / n * 1e3:11.3f} "
            f"{row['allocatedBytes'] / n / 1e6:11.3f} "
            f"{row['allocatedBytes'] / count / 1e6:11.3f}"
        )


if __name__ == "__main__":
    main()
