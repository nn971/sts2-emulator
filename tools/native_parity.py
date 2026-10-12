#!/usr/bin/env python3
"""Strict, coverage-scoped native vs emulator JSONL parity.

Synthetic fixtures exercise the protocol, not any claimed STS2 mechanical parity.
Exit codes: 0=MATCH, 1=MISMATCH, 2=unverified/invalid/incomplete comparison.
"""
from __future__ import annotations

import argparse
import copy
import json
import re
import sys
from pathlib import Path
from typing import Any

VERDICTS = {
    "MATCH", "MISMATCH", "CHANCE_UNALIGNED", "UNSUPPORTED",
    "INVALID_SETUP", "INCOMPLETE_CAPTURE"
}
PATH_PART = re.compile(r"\.([A-Za-z_][A-Za-z_0-9]*)|\[(\d+)\]")
COMPARE_EVENT_FIELDS = ("kind", "actor_id", "target_id", "payload")


def load_trace(path: Path) -> tuple[list[dict[str, Any]], str | None]:
    records: list[dict[str, Any]] = []
    try:
        with path.open(encoding="utf-8") as source:
            for line_number, raw_line in enumerate(source, 1):
                if not raw_line.strip():
                    continue
                try:
                    record = json.loads(raw_line)
                except json.JSONDecodeError as exc:
                    return records, f"{path}:{line_number}: {exc}"
                if not isinstance(record, dict) or not isinstance(record.get("type"), str):
                    return records, f"{path}:{line_number}: missing record type"
                records.append(record)
    except OSError as exc:
        return records, str(exc)
    if not records or records[0].get("type") != "header":
        return records, "Missing header"
    if len([r for r in records if r.get("type") == "header"]) != 1:
        return records, "Multiple headers"
    if not any(r.get("type") == "terminal" for r in records):
        return records, "Missing terminal/incomplete writer shutdown"
    if records[-1].get("type") != "terminal":
        return records, "Records after terminal"
    previous = 0
    for record in records:
        if record["type"] == "event":
            seq = record.get("event_seq")
            if type(seq) is not int or seq != previous + 1:
                return records, "Missing, duplicate, or unordered event_seq"
            previous = seq
    if path.with_suffix(path.suffix + ".incomplete").exists():
        return records, "Writer reported incomplete data"
    if records[-1].get("status") == "INCOMPLETE_CAPTURE":
        return records, "Native trace reports an incomplete capture"
    if any(r.get("type") == "transition" and
           r.get("status") != "COMPLETE" for r in records):
        return records, "Unfinished transition"
    return records, None


def first_diff(left: Any, right: Any, path: str = "$") -> dict[str, Any] | None:
    if type(left) is not type(right):
        return {"path": path, "native": left, "emulator": right}
    if isinstance(left, dict):
        if set(left) != set(right):
            return {"path": path + ".<keys>", "native": sorted(left), "emulator": sorted(right)}
        for key in sorted(left):
            diff = first_diff(left[key], right[key], path + "." + key)
            if diff:
                return diff
    elif isinstance(left, list):
        if len(left) != len(right):
            return {"path": path + ".<length>", "native": len(left), "emulator": len(right)}
        for i, (a, b) in enumerate(zip(left, right)):
            diff = first_diff(a, b, path + f"[{i}]")
            if diff:
                return diff
    elif left != right:
        return {"path": path, "native": left, "emulator": right}
    return None


def at_path(obj: Any, path: str) -> Any:
    if not path.startswith("$."):
        raise ValueError("Expected a concrete JSON path beginning '$.': " + path)
    pos = 1
    current = obj
    for match in PATH_PART.finditer(path, pos):
        if match.start() != pos:
            raise ValueError("Invalid or unsupported path: " + path)
        key = match.group(1)
        if key is not None:
            current = current[key]
        else:
            current = current[int(match.group(2))]
        pos = match.end()
    if pos != len(path):
        raise ValueError("Invalid or unsupported path: " + path)
    return current


def transition_data(records: list[dict[str, Any]]) -> dict[int, dict[str, Any]]:
    result: dict[int, dict[str, Any]] = {}
    for index, record in enumerate(records):
        kind = record["type"]
        if kind == "decision":
            decision_id = record.get("decision_id")
            if type(decision_id) is not int or decision_id in result:
                raise ValueError("Duplicate or invalid decision ID")
            result[decision_id] = {"before": record, "events": [], "index": index}
        elif kind in ("action", "event", "transition"):
            decision_id = record.get("decision_id")
            if decision_id not in result:
                raise ValueError("Action/event/transition without decision")
            entry = result[decision_id]
            if kind in entry and kind != "event":
                raise ValueError("Repeated " + kind + " for decision " + str(decision_id))
            if kind == "event":
                entry["events"].append(record)
            else:
                entry[kind] = record
    return result


def report(status: str, reason: str, *, decision_id: int | None = None,
           difference: dict[str, Any] | None = None,
           coverage: dict[str, Any] | None = None,
           native_prefix: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    assert status in VERDICTS
    return {
        "report_schema": "0.1",
        "status": status,
        "reason": reason,
        "decision_id": decision_id,
        "first_difference": difference,
        "coverage": coverage or {"verified_fields": [], "missing_fields": [],
                                 "verified_events": [], "rng_alignment": "RNG_UNAUDITED"},
        "native_prefix": native_prefix or [],
    }


def compare(native: list[dict[str, Any]], emulator: list[dict[str, Any]],
            *, decision_limit: int | None = None) -> dict[str, Any]:
    aheader, bheader = native[0], emulator[0]
    for field in ("trace_schema", "game_build", "run_seed", "run_id"):
        if aheader.get(field) != bheader.get(field):
            return report("INVALID_SETUP", "Mismatched " + field)
    if aheader.get("trace_schema") != "0.2":
        return report("UNSUPPORTED", "Parity runner requires trace schema 0.2")

    try:
        asteps, bsteps = transition_data(native), transition_data(emulator)
    except ValueError as exc:
        return report("INCOMPLETE_CAPTURE", str(exc))

    if not asteps:
        return report("UNSUPPORTED", "No decisions captured")

    # Keep complete decision maps for finding the post-state boundary.
    all_asteps, all_bsteps = asteps, bsteps
    if decision_limit is not None:
        asteps = {i: v for i, v in asteps.items() if i <= decision_limit}
    if not asteps:
        return report("INVALID_SETUP", "Requested decision limit precedes trace")
    last_coverage = None
    for decision_id, a in asteps.items():
        prefix_end = next((idx + 1 for idx, record in enumerate(native)
                           if record.get("type") == "transition" and
                           record.get("decision_id") == decision_id), a["index"] + 1)
        prefix = native[:prefix_end]
        if decision_id not in bsteps:
            return report("INCOMPLETE_CAPTURE", "Emulator decision missing",
                          decision_id=decision_id, native_prefix=prefix)
        b = bsteps[decision_id]
        ad, bd = a["before"], b["before"]
        acov, bcov = ad.get("coverage", {}), bd.get("coverage", {})
        left_fields = set(acov.get("verified_fields", []))
        right_fields = set(bcov.get("verified_fields", []))
        fields = sorted(left_fields & right_fields)
        missing = sorted(set(acov.get("missing_fields", [])) |
                         set(bcov.get("missing_fields", [])) |
                         (left_fields ^ right_fields))
        aevents = set(acov.get("verified_events", []))
        bevents = set(bcov.get("verified_events", []))
        audited_events = sorted(aevents & bevents)
        rng = "RNG_UNAUDITED"
        if acov.get("rng_status") == bcov.get("rng_status") == "ALIGNED":
            rng = "ALIGNED"
        manifest = {
            "verified_fields": fields, "missing_fields": missing,
            "verified_events": audited_events, "rng_alignment": rng,
            "native_unsupported": acov.get("unsupported_mechanics", []),
            "emulator_unsupported": bcov.get("unsupported_mechanics", []),
        }
        last_coverage = manifest
        if not fields:
            return report("UNSUPPORTED", "No shared verified semantic fields",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        if acov.get("unsupported_mechanics") or bcov.get("unsupported_mechanics"):
            return report("UNSUPPORTED", "Unimplemented mechanics in comparison scope",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        if acov.get("legal_actions_complete") is not True or bcov.get("legal_actions_complete") is not True:
            return report("UNSUPPORTED", "Exhaustive legal-action enumeration is unaudited",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        left_required = set(acov.get("required_preconditions") or [])
        right_required = set(bcov.get("required_preconditions") or [])
        manifest["required_preconditions"] = sorted(left_required | right_required)
        if not left_required or left_required != right_required:
            return report("INVALID_SETUP", "Precondition requirements unaudited or unequal",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        if not left_required.issubset(fields):
            return report("INVALID_SETUP", "A required continuation field is unverified",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        if ad.get("phase") != bd.get("phase"):
            return report("INVALID_SETUP", "Different phases at decision",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        for path in fields:
            try:
                left = at_path(ad["state"], path)
                right = at_path(bd["state"], path)
            except (KeyError, IndexError, TypeError, ValueError) as exc:
                return report("INVALID_SETUP", "Declared pre-state field unavailable: " + str(exc),
                              decision_id=decision_id, coverage=manifest, native_prefix=prefix)
            diff = first_diff(left, right, path)
            if diff:
                return report("INVALID_SETUP", "Pre-state equivalence not established",
                              decision_id=decision_id, difference=diff,
                              coverage=manifest, native_prefix=prefix)

        if "legal_actions" not in ad or "legal_actions" not in bd:
            return report("INCOMPLETE_CAPTURE", "Legal action menu missing",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        # Menus are sets of semantic commands, ignoring arbitrary display ordering.
        def action_keys(actions: list[dict[str, Any]]) -> list[str]:
            return sorted(json.dumps(action, sort_keys=True, separators=(",", ":"))
                          for action in actions)
        diff = first_diff(action_keys(ad["legal_actions"]),
                          action_keys(bd["legal_actions"]), "$.legal_actions")
        if diff:
            return report("MISMATCH", "Legal action mismatch",
                          decision_id=decision_id, difference=diff,
                          coverage=manifest, native_prefix=prefix)

        if "action" not in a or "action" not in b:
            return report("INCOMPLETE_CAPTURE", "Missing selected action",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        if not a["action"].get("accepted") or not b["action"].get("accepted"):
            return report("INVALID_SETUP", "Action was rejected",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        if json.dumps(a["action"].get("action"), sort_keys=True, separators=(",", ":")) not in action_keys(ad["legal_actions"]):
            return report("INVALID_SETUP", "Selected action absent from audited legal-action menu",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        diff = first_diff(a["action"].get("action"), b["action"].get("action"), "$.action")
        if diff:
            return report("INVALID_SETUP", "Different actions selected",
                          decision_id=decision_id, difference=diff,
                          coverage=manifest, native_prefix=prefix)
        if "transition" not in a or "transition" not in b:
            return report("INCOMPLETE_CAPTURE", "Transition record missing",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)

        # A known chance site is the only valid reason to suppress downstream
        # mechanical comparison when exact native streams are unavailable.
        chance = a["transition"].get("chance_unaligned_at")
        if chance and b["transition"].get("chance_unaligned_at") == chance:
            return report("CHANCE_UNALIGNED", "Explicit common chance boundary: " + str(chance),
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)

        if audited_events:
            def selected_events(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
                return [{key: event.get(key) for key in COMPARE_EVENT_FIELDS}
                        for event in events if event.get("kind") in audited_events]
            diff = first_diff(selected_events(a["events"]), selected_events(b["events"]),
                              "$.events")
            if diff:
                return report("MISMATCH", "Ordered audited event mismatch",
                              decision_id=decision_id, difference=diff,
                              coverage=manifest, native_prefix=prefix)

        # A transition's post state is the next decision's captured pre-state.
        # This is both explicit and independently covered by the next iteration.
        ahash = a["transition"].get("after_hash")
        bhash = b["transition"].get("after_hash")
        if ahash is None or bhash is None:
            return report("INCOMPLETE_CAPTURE", "Missing post-decision checkpoint",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)

        anext = next((v["before"] for i, v in all_asteps.items() if i > decision_id), None)
        bnext = next((v["before"] for i, v in all_bsteps.items() if i > decision_id), None)
        if anext is None and native[-1].get("type") == "terminal" and native[-1].get("state") is not None:
            anext = native[-1]
        if bnext is None and emulator[-1].get("type") == "terminal" and emulator[-1].get("state") is not None:
            bnext = emulator[-1]
        if anext is None or bnext is None:
            return report("UNSUPPORTED", "No comparable post-state boundary",
                          decision_id=decision_id, coverage=manifest, native_prefix=prefix)
        for path in fields:
            try:
                left = at_path(anext["state"], path)
                right = at_path(bnext["state"], path)
            except (KeyError, IndexError, TypeError, ValueError):
                return report("UNSUPPORTED", "Post-state field is absent: " + path,
                              decision_id=decision_id, coverage=manifest, native_prefix=prefix)
            diff = first_diff(left, right, path)
            if diff:
                return report("MISMATCH", "Covered post-state difference",
                              decision_id=decision_id, difference=diff,
                              coverage=manifest, native_prefix=prefix)
    return report("MATCH", "All requested covered decisions and audited events match",
                  decision_id=max(asteps), coverage=last_coverage)


def main() -> int:
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("native", type=Path)
    cli.add_argument("emulator", type=Path)
    cli.add_argument("--decision", type=int, help="Compare only through this decision ID")
    cli.add_argument("--out", type=Path, help="Write machine-readable parity report")
    args = cli.parse_args()
    native, left_error = load_trace(args.native)
    emulator, right_error = load_trace(args.emulator)
    if left_error or right_error:
        result = report("INCOMPLETE_CAPTURE", left_error or right_error or "Incomplete")
    else:
        result = compare(native, emulator, decision_limit=args.decision)
    result["provenance"] = {"native": native[0] if native else None,
                            "emulator": emulator[0] if emulator else None}
    data = json.dumps(result, indent=2, sort_keys=True)
    print(data)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(data + "\n", encoding="utf-8")
    return 0 if result["status"] == "MATCH" else (1 if result["status"] == "MISMATCH" else 2)


if __name__ == "__main__":
    sys.exit(main())
