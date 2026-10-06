#!/usr/bin/env python3
"""Compare two STS2 Emulator JSONL traces and report the first structural difference."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Iterator


def load_jsonl(path: Path) -> list[Any]:
    records: list[Any] = []
    with path.open("r", encoding="utf-8") as f:
        for lineno, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                records.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise SystemExit(f"{path}:{lineno}: invalid JSON: {exc}") from exc
    return records


def first_diff(a: Any, b: Any, path: str = "$") -> tuple[str, Any, Any] | None:
    if type(a) is not type(b):
        return path, a, b

    if isinstance(a, dict):
        akeys, bkeys = set(a), set(b)
        if akeys != bkeys:
            return f"{path}.<keys>", sorted(akeys), sorted(bkeys)
        for key in sorted(akeys):
            diff = first_diff(a[key], b[key], f"{path}.{key}")
            if diff:
                return diff
        return None

    if isinstance(a, list):
        if len(a) != len(b):
            return f"{path}.<length>", len(a), len(b)
        for i, (left, right) in enumerate(zip(a, b)):
            diff = first_diff(left, right, f"{path}[{i}]")
            if diff:
                return diff
        return None

    if a != b:
        return path, a, b
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("reference", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument(
        "--ignore-header-field",
        action="append",
        default=["captured_at_utc", "emulator_version"],
        help="Header field to ignore (repeatable)",
    )
    args = parser.parse_args()

    left = load_jsonl(args.reference)
    right = load_jsonl(args.candidate)

    if left and right and left[0].get("type") == right[0].get("type") == "header":
        for key in args.ignore_header_field:
            left[0].pop(key, None)
            right[0].pop(key, None)

    if len(left) != len(right):
        print(f"record count differs: reference={len(left)} candidate={len(right)}")

    for i, (a, b) in enumerate(zip(left, right)):
        diff = first_diff(a, b)
        if diff:
            path, av, bv = diff
            decision = a.get("decision_index") if isinstance(a, dict) else None
            print(f"FIRST DIVERGENCE: record={i} decision_index={decision}")
            print(f"path: {path}")
            print(f"reference: {json.dumps(av, ensure_ascii=False, sort_keys=True)}")
            print(f"candidate: {json.dumps(bv, ensure_ascii=False, sort_keys=True)}")
            return 1

    if len(left) == len(right):
        print("traces match structurally")
        return 0

    return 1


if __name__ == "__main__":
    raise SystemExit(main())
