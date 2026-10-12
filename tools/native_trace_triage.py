#!/usr/bin/env python3
"""Triage native passive traces and select recorded Strike/Defend expectations.

A structurally valid native trace is an observed oracle, *not* emulator parity.
The script never simulates a game action or fabricates a missing post-state.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_expectation_fixture import extract
from native_trace_audit import audit
from native_parity import load_trace, transition_data


def summarize_trace(path: Path) -> tuple[dict, list[tuple[int, dict]]]:
    records, error = load_trace(path)
    audited = audit(records, error)
    result = {
        "file": path.name,
        "bytes": path.stat().st_size,
        "capture_integrity": audited["status"],
        "reason": audited["reason"],
        "counts_as_complete_capture": audited["status"] == "STRUCTURALLY_VALID",
        "audit_schema": audited["audit_schema"],
        "counts": {kind: sum(r.get("type") == kind for r in records)
                   for kind in ("header", "decision", "action", "event", "transition", "terminal")},
        "terminal": [
            {"status": r.get("status"), "reason": r.get("reason")}
            for r in records if r.get("type") == "terminal"
        ],
        "actions": [],
    }
    candidates: list[tuple[int, dict]] = []
    # Preserve useful action summaries even in incomplete captures, but never
    # extract fixtures from an invalid lifecycle.
    decisions = {r.get("decision_id"): r
                 for r in records if r.get("type") == "decision"}
    for record in records:
        if record.get("type") != "action":
            continue
        decision_id = record.get("decision_id")
        action = record.get("action") or {}
        payload = action.get("payload") or {}
        pre = decisions.get(decision_id, {}).get("state") or {}
        card_id = payload.get("card_instance_id")
        card = next((c for c in pre.get("combat", {}).get("hand", [])
                     if c.get("instance_id") == card_id), None)
        result["actions"].append({
            "decision_id": decision_id,
            "kind": action.get("kind"),
            "card_model": (card or {}).get("model_id"),
            "card_instance_id": card_id,
            "target_id": payload.get("target_id"),
            "accepted": record.get("accepted"),
            "origin": record.get("origin"),
        })
    if audited["status"] == "STRUCTURALLY_VALID":
        steps = transition_data(records)
        for decision_id in sorted(steps):
            candidate = extract(records, decision_id)
            if candidate["status"] == "RECORDED_NATIVE_EXPECTATION":
                candidates.append((decision_id, candidate))
    result["fixtures"] = [
        {"decision_id": d, "card_family": fixture["card_family"],
         "status": fixture["status"], "emulator_parity": fixture["emulator_parity"],
         "ready_for_emulator_step": fixture["ready_for_emulator_step"]}
        for d, fixture in candidates
    ]
    return result, candidates


def triage(directory: Path, fixture_dir: Path | None = None) -> dict:
    paths = sorted(directory.glob("passive-*.jsonl"))
    result = {"schema": "native-trace-triage-v1",
              "status": "NATIVE_EVIDENCE_ONLY; EMULATOR_PARITY_NOT_TESTED",
              "source_directory": str(directory),
              "trace_count": len(paths), "structurally_valid_count": 0,
              "fixture_count": 0, "traces": []}
    if fixture_dir is not None:
        fixture_dir.mkdir(parents=True, exist_ok=True)
    for path in paths:
        entry, fixtures = summarize_trace(path)
        if entry["capture_integrity"] == "STRUCTURALLY_VALID":
            result["structurally_valid_count"] += 1
        result["fixture_count"] += len(fixtures)
        result["traces"].append(entry)
        for decision_id, fixture in fixtures:
            if fixture_dir is not None:
                filename = path.stem + f"-decision-{decision_id}.json"
                (fixture_dir / filename).write_text(
                    json.dumps(fixture, indent=2, sort_keys=True) + "\n",
                    encoding="utf-8")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace_dir", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--fixture-dir", type=Path,
                        help="Write authentic native Strike/Defend expectation fixtures")
    args = parser.parse_args()
    if not args.trace_dir.is_dir():
        parser.error("Trace directory does not exist: " + str(args.trace_dir))
    report = triage(args.trace_dir, args.fixture_dir)
    rendered = json.dumps(report, indent=2, sort_keys=True)
    print(rendered)
    if args.out is not None:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
