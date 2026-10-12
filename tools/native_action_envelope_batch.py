#!/usr/bin/env python3
"""Summarize a directory of passive native combat action-envelope sidecars.

A corpus review helps identify *unobserved* mechanic families before further
in-game capture. Its status never implies emulator/native parity.
"""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

from native_action_envelope_audit import audit, load

MECHANIC_KEYS = (
    "card_play", "end_turn", "non_player_or_unknown_action",
    "player_hp_change", "energy_change", "block_change", "creature_hp_change",
    "power_change", "card_zone_change", "exhaust_pile_change",
    "creature_set_change", "nested_choice_pause", "potion_history_entry",
)


def collect(directory: Path) -> dict:
    files = sorted(directory.glob("*.actions.jsonl"))
    traces = []
    aggregate = Counter()
    action_types = Counter()
    statuses = Counter()
    for path in files:
        records, error = load(path)
        result = audit(records, error)
        statuses[result["status"]] += 1
        for name, info in result["observed_mechanic_indicators"].items():
            aggregate[name] += info["count"]
        for item in result["actions"]:
            action_types[item.get("native_type") or "UNKNOWN"] += 1
        traces.append({
            "file": str(path),
            "status": result["status"],
            "reason": result["reason"],
            "action_count": len(result["actions"]),
            "nested_choice_pauses": result["nested_choice_pauses"],
            "action_resumptions": result["action_resumptions"],
            "legacy_resume_candidates": result["legacy_resume_candidates"],
            "terminal_history_clears": result["terminal_history_clears"],
            "raw_terminal_status": result["raw_terminal_status"],
            "frame_states": result["frame_states"],
            "unattributed_history_entries": result["unattributed_history_entries"],
            "incomplete_observations": result["incomplete_observations"],
        })
    return {
        "schema": "native-combat-corpus-audit-v1",
        "status": (
            "NO_CAPTURES" if not files
            else "CALLBACK_CORPUS_CONSISTENT" if all(
                entry["status"] == "CALLBACK_TIMELINE_CONSISTENT"
                for entry in traces)
            else "LEGACY_OBSERVER_ARTIFACTS_REVIEWED" if all(
                entry["status"] in ("CALLBACK_TIMELINE_CONSISTENT",
                                    "LEGACY_CALLBACK_SEQUENCE_RECOVERABLE")
                for entry in traces)
            else "CAPTURE_GAPS_REQUIRE_REVIEW"),
        "native_game_parity": "NOT_TESTED",
        "source_hook_order_parity": "NOT_TESTED",
        "exact_rng_parity": "NOT_TESTED",
        "files_checked": len(files),
        "statuses": dict(statuses),
        "native_action_types": dict(action_types.most_common()),
        "mechanics": {
            key: {
                "observations": aggregate[key],
                "status": (
                    "OBSERVED_UNVERIFIED_MECHANICS"
                    if aggregate[key] else "UNOBSERVED")
            }
            for key in MECHANIC_KEYS
        },
        "traces": traces,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    result = collect(args.directory)
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if result["status"] == "CALLBACK_CORPUS_CONSISTENT" else 2


if __name__ == "__main__":
    raise SystemExit(main())
