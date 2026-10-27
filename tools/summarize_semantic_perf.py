#!/usr/bin/env python3
"""Aggregate opt-in semantic JSONL operation counters from emulator workers.

Input files: one JSON profile per Session, from STS2_SEMANTIC_PROFILE_DIR.
These are cumulative, overlapping server-process durations, not elapsed wall.
"""
from __future__ import annotations

import argparse
import json
import math
from collections import defaultdict
from pathlib import Path


def summarize(paths: list[Path]) -> dict:
    if not paths:
        raise ValueError("No semantic profiling JSON files")
    totals = defaultdict(lambda: {"count": 0, "totalSeconds": 0.0,
                                  "allocatedBytes": 0, "maxSeconds": 0.0})
    pids = set()
    for path in paths:
        data = json.loads(path.read_text(encoding="utf-8"))
        if (data.get("schemaId") != "sts2-semantic-jsonl-performance-v1"
                or data.get("enabled") is not True
                or not isinstance(data.get("operations"), dict)):
            raise ValueError(f"Invalid semantic profile: {path}")
        pid = data["processId"]
        if pid in pids:
            raise ValueError(f"Duplicate semantic profiler process ID: {pid}")
        pids.add(pid)
        for operation, row in data["operations"].items():
            if not operation.startswith(("op.semantic_", "sub.semantic_")):
                raise ValueError(f"Unexpected profile operation: {operation}")
            count = row["count"]
            seconds = row["totalSeconds"]
            allocated = row["allocatedBytes"]
            maximum = row["maxSeconds"]
            if (type(count) is not int or count < 0
                    or type(allocated) is not int or allocated < 0
                    or any(not math.isfinite(float(x)) or x < 0
                           for x in (seconds, maximum))):
                raise ValueError(f"Invalid profile sample: {path}: {operation}")
            t = totals[operation]
            t["count"] += count
            t["totalSeconds"] += seconds
            t["allocatedBytes"] += allocated
            t["maxSeconds"] = max(t["maxSeconds"], maximum)
    ordered = dict(sorted(totals.items(),
                          key=lambda x: (-x[1]["totalSeconds"], x[0])))
    return {
        "schema": "sts2-semantic-aggregate-performance-v1",
        "workers": len(paths),
        "interpretation": (
            "Operation wall times are summed across worker processes and "
            "overlap; sub.* timings are nested within op.*. "
            "Do not add nested categories or compare totals directly to "
            "whole-cohort elapsed seconds."
        ),
        "operations": ordered,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path,
                        help="Directory set in STS2_SEMANTIC_PROFILE_DIR")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    paths = sorted(args.directory.glob("semantic-*.json"))
    report = summarize(paths)
    rendered = json.dumps(report, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    print(rendered)


if __name__ == "__main__":
    main()
