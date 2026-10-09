#!/usr/bin/env python3
"""Preflight and audit one native Silent / Underdocks reference capture.

This is an integrity/coverage audit, NOT a claim of state/RNG parity.
It does not need STS2 assemblies or modify any capture file.
"""

from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
import sys

EXPECTED_MODE = "underdocks-silent-act1"
EXPECTED_SCHEMA = "sts2-reference-probe-v4"
ERROR_CODES = {
    "capture_failed",
    "run_attach_failed",
    "run_manager_timeout",
    "combat_manager_timeout",
}


def read_jsonl(path: Path) -> list[dict]:
    if not path.is_file():
        raise ValueError(f"Trace file does not exist: {path}")
    records = []
    with path.open("r", encoding="utf-8-sig") as stream:
        for line_number, raw in enumerate(stream, 1):
            if not raw.strip():
                continue
            try:
                item = json.loads(raw)
            except json.JSONDecodeError as exc:
                raise ValueError(
                    f"Line {line_number}: invalid JSONL ({exc.msg}); "
                    "check whether capture was copied during a write"
                ) from exc
            if not isinstance(item, dict):
                raise ValueError(
                    f"Line {line_number}: expected a JSON object"
                )
            records.append(item)
    if not records:
        raise ValueError("Trace is empty; no recorder session was observed")
    return records


def assess(records: list[dict], *, preflight: bool = False) -> dict:
    errors: list[str] = []
    warnings: list[str] = []
    sessions = [row for row in records if row.get("type") == "session"]
    if not sessions:
        errors.append("No recorder session header")
        session: dict = {}
    else:
        session = sessions[0]
        if len(sessions) > 1:
            warnings.append(
                "Multiple sessions in one file; check provenance/concatenation"
            )
        if session.get("corpus_mode") != EXPECTED_MODE:
            errors.append(
                f"Wrong capture mode: expected {EXPECTED_MODE!r}, "
                f"found {session.get('corpus_mode')!r}"
            )
        if session.get("schema") != EXPECTED_SCHEMA:
            errors.append("Unexpected probe schema; verify bridge version")
        if not session.get("build_fingerprint"):
            errors.append("Missing native build fingerprint")
        if not session.get("build"):
            errors.append("Missing verified game-build provenance")

    diagnostics = [
        row for row in records if row.get("type") == "diagnostic"
    ]
    codes = Counter(row.get("code", "<missing>") for row in diagnostics)
    if codes.get("run_recorder_attached", 0) == 0:
        errors.append(
            "Native RunManager hooks not confirmed. Check the staged "
            "underdocks-silent-act1.mode marker and relaunch before playing."
        )
    if codes.get("recorder_attached", 0) == 0:
        errors.append(
            "CombatManager hooks not confirmed. Relaunch with the "
            "instrumented reference bridge before playing."
        )
    for code in sorted(ERROR_CODES):
        if codes.get(code, 0) > 0:
            errors.append(
                f"Recorder reported {code} ({codes[code]} occurrence(s))"
            )

    # Recorder attach diagnostics alone do not prove all individual
    # subscriptions succeeded. Fail preflight for missing lifecycle hooks
    # that make the entire one-run capture unusable.
    for entry in diagnostics:
        if entry.get("code") not in (
            "event_missing", "event_subscribe_failed"
        ):
            continue
        message = str(entry.get("message", ""))
        if any(f"RunManager.{name}" in message for name in
               ("RunStarted", "RoomEntered", "RoomExited", "ActEntered")):
            errors.append(f"Critical run lifecycle subscription: {message}")
        elif "CombatManager.CombatSetUp" in message or \\
             "CombatManager.CombatEnded" in message:
            errors.append(f"Critical combat subscription: {message}")
        else:
            warnings.append(f"Optional native signal unavailable: {message}")

    boundaries = [
        row for row in records if row.get("type") == "boundary"
    ]
    by_boundary = Counter(
        str(row.get("boundary", "<missing>")) for row in boundaries
    )
    run_snapshots = [
        row.get("run_observation")
        for row in boundaries
        if isinstance(row.get("run_observation"), dict)
        and row["run_observation"].get("available") is True
    ]
    maps = [
        snapshot["map"] for snapshot in run_snapshots
        if isinstance(snapshot.get("map"), dict)
        and isinstance(snapshot["map"].get("points"), list)
    ]
    with_decks = [
        snapshot for snapshot in run_snapshots
        if any(
            isinstance(player.get("deck"), dict)
            and isinstance(player["deck"].get("items"), list)
            for player in snapshot.get("players", [])
            if isinstance(player, dict)
        )
    ]
    with_rng = [
        snapshot for snapshot in run_snapshots
        if snapshot.get("run_rng") is not None
    ]
    event_options = [
        native_event
        for snapshot in run_snapshots
        for native_event in snapshot.get("events", [])
        if isinstance(native_event, dict)
        and native_event.get("options")
    ]
    with_history = [
        snapshot for snapshot in run_snapshots
        if snapshot.get("current_map_point_history") is not None
    ]

    if not preflight:
        for name, count, purpose in [
            ("run_manager.RunStarted",
             by_boundary["run_manager.RunStarted"],
             "run identity and first map"),
            ("run_manager.RoomEntered",
             by_boundary["run_manager.RoomEntered"],
             "room-by-room decisions and inventory"),
            ("combat_history.Changed",
             by_boundary["combat_history.Changed"],
             "individual combat actions"),
        ]:
            if count == 0:
                warnings.append(
                    f"No {name} observations: missing {purpose}"
                )
        if not with_decks:
            warnings.append("No persistent deck snapshots found")
        if not maps:
            warnings.append("No map graph snapshot found")
        if not with_rng:
            warnings.append("No run RNG snapshots found")
        if not event_options:
            warnings.append(
                "No event option lists observed. This can mean no events "
                "were encountered, or event subscription timing needs work."
            )
        if not with_history:
            warnings.append("No current map-point history snapshots found")

    return {
        "ok": not errors,
        "preflight": preflight,
        "session_count": len(sessions),
        "schema": session.get("schema"),
        "mode": session.get("corpus_mode"),
        "game_version": session.get("expected_game_version"),
        "game_commit": session.get("expected_game_commit"),
        "build_fingerprint": session.get("build_fingerprint"),
        "total_records": len(records),
        "boundaries": dict(sorted(by_boundary.items())),
        "diagnostics": dict(sorted(codes.items())),
        "run_snapshots": len(run_snapshots),
        "snapshots_with_persistent_deck": len(with_decks),
        "snapshots_with_run_rng": len(with_rng),
        "snapshots_with_map_history": len(with_history),
        "map_snapshots": len(maps),
        "event_option_pages_observed": len(event_options),
        "errors": errors,
        "warnings": warnings,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path, help="native bridge .jsonl")
    parser.add_argument(
        "--preflight", action="store_true",
        help="verify ready-to-record session and passive subscriptions "
             "before starting a run",
    )
    parser.add_argument(
        "--json", action="store_true",
        help="write machine-readable audit to stdout",
    )
    parser.add_argument(
        "--output", type=Path,
        help="also write the audit report to a separate JSON file",
    )
    args = parser.parse_args()

    try:
        rows = read_jsonl(args.trace)
        report = assess(rows, preflight=args.preflight)
    except (OSError, ValueError) as exc:
        print(f"Capture audit failed: {exc}", file=sys.stderr)
        return 2

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(report, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False))
    else:
        print("Native Underdocks capture preflight"
              if args.preflight else "Native Underdocks capture audit")
        print(f"Mode: {report['mode']}  Schema: {report['schema']}")
        print(f"Build: {report['game_version']} ({report['game_commit']})")
        print(f"Boundaries: {sum(report['boundaries'].values())}")
        print(
            "Run snapshots: {run_snapshots}, maps: {map_snapshots}, "
            "decks: {snapshots_with_persistent_deck}, "
            "RNG: {snapshots_with_run_rng}, "
            "event option pages: {event_option_pages_observed}".format(
                **report
            )
        )
        for problem in report["errors"]:
            print("ERROR: " + problem)
        for problem in report["warnings"]:
            print("WARNING: " + problem)
        print("READY" if report["ok"] else "NOT READY / INCOMPLETE")

    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
