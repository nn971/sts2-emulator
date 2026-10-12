#!/usr/bin/env python3
"""Audit one passive native JSONL trace for completeness and provenance.

This checks trace integrity only. A structurally valid passive trace is
native evidence and does not establish emulator mechanical parity.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from native_parity import load_trace, transition_data

HEX64 = re.compile(r"^[0-9a-fA-F]{64}$")


def audit(records: list[dict], error: str | None = None) -> dict:
    result = {
        "audit_schema": "0.1",
        "status": "INCOMPLETE_CAPTURE" if error else "INVALID_SETUP",
        "reason": error or "",
        "capture_integrity": "UNVERIFIED",
        "native_parity": "NOT_TESTED",
        "game_build_pinned": False,
        "decisions": 0,
        "actions": 0,
        "events": 0,
        "verified_fields": [],
        "missing_fields": [],
        "unsupported_mechanics": [],
        "rng_alignment": "RNG_UNAUDITED",
    }
    if error or not records:
        return result
    header = records[0]
    if header.get("trace_schema") != "0.2" or header.get("capture_mode") != "passive":
        result["reason"] = "Expected native passive trace schema 0.2"
        return result
    game_build = header.get("game_build", "")
    result["game_build_pinned"] = bool(
        re.search(r"sha256=([0-9a-fA-F]{64})$", game_build))
    result["actions"] = len([r for r in records if r.get("type") == "action"])
    result["events"] = len([r for r in records if r.get("type") == "event"])
    try:
        steps = transition_data(records)
    except (ValueError, KeyError) as exc:
        result["status"] = "INCOMPLETE_CAPTURE"
        result["reason"] = str(exc)
        return result

    result["decisions"] = len(steps)
    if not steps:
        result["status"] = "INCOMPLETE_CAPTURE"
        result["reason"] = "No native decision boundary recorded"
        return result

    ordered = sorted(steps)
    if ordered != list(range(1, len(steps) + 1)):
        result["status"] = "INCOMPLETE_CAPTURE"
        result["reason"] = "Decision IDs must increase consecutively from 1"
        return result

    fields: set[str] = set()
    missing: set[str] = set()
    unsupported: set[str] = set()
    previous_revision = 0
    for i, decision_id in enumerate(ordered):
        step = steps[decision_id]
        boundary = step["before"]
        coverage = boundary.get("coverage", {})
        fields.update(coverage.get("verified_fields", []))
        missing.update(coverage.get("missing_fields", []))
        unsupported.update(coverage.get("unsupported_mechanics", []))
        revision = boundary.get("observation_revision", -1)
        if not isinstance(revision, int) or revision <= previous_revision:
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Non-increasing observation revision"
            return result
        previous_revision = revision
        if not isinstance(boundary.get("state"), dict) or not boundary.get("state_hash"):
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Canonical state/hash absent"
            return result
        if "action" not in step or "transition" not in step:
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = f"Missing action/transition for decision {decision_id}"
            return result
        action = step["action"]
        transition = step["transition"]
        if not action.get("accepted"):
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Native action missing from declared legal menu or rejected"
            return result
        menu = boundary.get("legal_actions")
        if not isinstance(menu, list) or action.get("action") not in menu:
            result["status"] = "INVALID_SETUP"
            result["reason"] = "Selected action absent from declared canonical menu"
            return result
        if transition.get("before_hash") != boundary["state_hash"]:
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Transition before hash differs from native boundary hash"
            return result
        if transition.get("status") != "COMPLETE":
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Unfinished native transition"
            return result
        if transition.get("first_event_seq") != boundary.get("event_seq", 0) + 1:
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Transition event range does not start at boundary cursor"
            return result
        expected_last = (steps[ordered[i + 1]]["before"].get("event_seq")
                         if i + 1 < len(ordered) else
                         max((e.get("event_seq", 0) for e in step["events"]), default=boundary.get("event_seq", 0)))
        if transition.get("last_event_seq") != expected_last:
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Transition event range does not terminate at successor cursor"
            return result
        if i + 1 < len(ordered) and transition.get("after_hash") != steps[ordered[i + 1]]["before"].get("state_hash"):
            result["status"] = "INCOMPLETE_CAPTURE"
            result["reason"] = "Transition successor hash mismatch"
            return result

    result["verified_fields"] = sorted(fields)
    result["missing_fields"] = sorted(missing)
    result["unsupported_mechanics"] = sorted(unsupported)
    result["rng_alignment"] = (
        "ALIGNED" if all(step["before"].get("coverage", {}).get("rng_status") == "ALIGNED"
                         for step in steps.values()) else "RNG_UNAUDITED")
    result["status"] = "STRUCTURALLY_VALID"
    result["reason"] = "Complete passive event sequence and decision lifecycle"
    result["capture_integrity"] = "STRUCTURALLY_VALID"
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_trace", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load_trace(args.native_trace)
    report = audit(records, error)
    rendered = json.dumps(report, indent=2, sort_keys=True)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if report["status"] == "STRUCTURALLY_VALID" else 2


if __name__ == "__main__":
    raise SystemExit(main())
