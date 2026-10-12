#!/usr/bin/env python3
"""Audit native action-executor sidecars independently of player-decision traces.

Each executor callback is an observation; the action sidecar remains useful
when a nested choice makes the strict semantic decision trace incomplete.
Observed history order is distinct from proven game-source hook order.
"""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path


def diff_scalars(before: dict, after: dict) -> dict:
    """Small measured delta, with no card-rule assumptions or RNG replay."""
    values: dict = {}
    for label, path in {
        "player_hp": ("player", "hp"),
        "player_block": ("player", "block"),
        "energy": ("combat", "energy"),
        "turn": ("combat", "turn"),
    }.items():
        try:
            x, y = before[path[0]][path[1]], after[path[0]][path[1]]
            if isinstance(x, int) and isinstance(y, int) and x != y:
                values[label] = {"before": x, "after": y}
        except (KeyError, TypeError):
            pass
    try:
        if before["player"].get("powers") != after["player"].get("powers"):
            values["player_powers"] = {
                "before": before["player"].get("powers"),
                "after": after["player"].get("powers"),
            }
    except (KeyError, TypeError):
        pass
    try:
        pre = {x["id"]: x for x in before["combat"]["creatures"]}
        post = {x["id"]: x for x in after["combat"]["creatures"]}
        for identifier in sorted(pre.keys() | post.keys()):
            if identifier not in pre or identifier not in post:
                values.setdefault("creatures", {})[identifier] = "added_or_removed"
                continue
            for field in ("hp", "max_hp", "block", "alive", "powers"):
                if pre[identifier].get(field) != post[identifier].get(field):
                    values.setdefault("creatures", {}).setdefault(
                        identifier, {})[field] = {
                            "before": pre[identifier].get(field),
                            "after": post[identifier].get(field),
                        }
    except (KeyError, TypeError):
        pass
    try:
        for zone in ("hand", "draw_pile", "discard_pile", "exhaust_pile"):
            x = [c["instance_id"] for c in before["combat"][zone]]
            y = [c["instance_id"] for c in after["combat"][zone]]
            if x != y:
                values.setdefault("zones", {})[zone] = {
                    "before": x, "after": y,
                }
    except (KeyError, TypeError):
        pass
    return values


def infer_choice_discard(before: dict, after: dict) -> dict:
    """Observed card movement supporting a nested discard-choice candidate.

    This is a state-delta inference, not a native semantic choice callback.
    """
    try:
        initial_hand = {c["instance_id"] for c in before["combat"]["hand"]}
        final_hand = {c["instance_id"] for c in after["combat"]["hand"]}
        initial_discard = {c["instance_id"] for c in before["combat"]["discard_pile"]}
        final_discard = {c["instance_id"] for c in after["combat"]["discard_pile"]}
        candidates = sorted((initial_hand - final_hand) & (
            final_discard - initial_discard))
    except (KeyError, TypeError, ValueError):
        candidates = []
    return {
        "candidate_discarded_card_instance_id": (
            candidates[0] if len(candidates) == 1 else None),
        "candidate_count": len(candidates),
        "provenance": "INFERRED_FROM_CARD_ZONE_DIFFERENCE",
        "semantic_selection_verified": False,
    }


def observed_indicators(actions: list[dict], records: list[dict]) -> dict:
    """Counts directly observed changes; absence is UNOBSERVED, not a pass."""
    result: dict[str, dict] = {}
    for label in (
        "card_play", "end_turn", "non_player_or_unknown_action",
        "player_hp_change", "energy_change", "block_change",
        "creature_hp_change", "power_change", "card_zone_change",
        "exhaust_pile_change", "creature_set_change",
        "nested_choice_pause", "potion_history_entry",
    ):
        result[label] = {"count": 0, "status": "UNOBSERVED"}

    def add(name: str):
        result[name]["count"] += 1
        result[name]["status"] = "OBSERVED_UNVERIFIED_MECHANICS"

    for action in actions:
        canonical = action.get("canonical_action") or {}
        kind = canonical.get("kind")
        if kind == "play_card":
            add("card_play")
        elif kind == "end_turn":
            add("end_turn")
        else:
            add("non_player_or_unknown_action")
        delta = action.get("observed_delta") or {}
        if "player_hp" in delta:
            add("player_hp_change")
        if "energy" in delta:
            add("energy_change")
        if "player_block" in delta or any(
                isinstance(info, dict) and "block" in info
                for info in delta.get("creatures", {}).values()):
            add("block_change")
        if any(isinstance(info, dict) and "hp" in info
               for info in delta.get("creatures", {}).values()):
            add("creature_hp_change")
        if "player_powers" in delta or any(isinstance(info, dict) and "powers" in info
               for info in delta.get("creatures", {}).values()):
            add("power_change")
        if delta.get("zones"):
            add("card_zone_change")
        if "exhaust_pile" in delta.get("zones", {}):
            add("exhaust_pile_change")
        if "added_or_removed" in delta.get("creatures", {}).values():
            add("creature_set_change")
    for record in records:
        if record.get("type") == "nested_choice_pause":
            add("nested_choice_pause")
        if (record.get("type") == "history_entry" and
                record.get("native_type") == "potion_used"):
            add("potion_history_entry")
    return result


def audit(records: list[dict], error: str | None = None) -> dict:
    report = {
        "schema": "native-action-envelope-audit-v1",
        "status": "INCOMPLETE_CAPTURE",
        "reason": error or "",
        "native_game_parity": "NOT_TESTED",
        "native_source_hook_order": "NOT_TESTED",
        "exact_rng_parity": "NOT_TESTED",
        "record_counts": dict(Counter(r.get("type") for r in records)),
        "actions": [],
        "nested_choices": [],
        "observed_mechanic_indicators": {},
        "nested_choice_pauses": 0,
        "unattributed_history_entries": 0,
        "history_entries_with_missing_fields": 0,
        "frame_states": 0,
        "incomplete_observations": [],
    }
    if error:
        return report
    if not records or records[0].get("type") != "header":
        report["reason"] = "Missing header"
        return report
    if records[0].get("data", {}).get("schema") != "native-action-envelope-v1":
        report["reason"] = "Unexpected sidecar schema"
        return report
    if records[-1].get("type") != "terminal":
        report["reason"] = "Missing terminal record"
        return report
    allowed = {
        "header", "history_baseline", "frame_state", "action_started", "action_completed", "stable_decision",
        "nested_choice_pause", "history_entry", "history_reset", "anomaly",
        "observation_failure", "terminal",
    }
    running: dict[int, dict] = {}
    completed: dict[int, dict] = {}
    choices: list[dict] = []
    history_index = -1
    last_frame_index = -1
    last_frame_hash = None
    strict_failures: list[str] = []
    for ordinal, record in enumerate(records, 1):
        typ = record.get("type")
        if record.get("ordinal") != ordinal:
            report["reason"] = f"Nonconsecutive sidecar ordinal at line {ordinal}"
            return report
        if typ not in allowed:
            report["reason"] = f"Unknown sidecar record kind {typ} at line {ordinal}"
            return report
        if typ == "header" and ordinal != 1:
            report["reason"] = "Duplicate header"
            return report
        if typ == "history_baseline":
            count = (record.get("data") or {}).get("existing_entry_count")
            if type(count) is not int or count < 0 or history_index >= 0:
                report["reason"] = "Invalid native history baseline"
                return report
            history_index = count - 1
        if typ == "terminal" and ordinal != len(records):
            report["reason"] = "Premature terminal"
            return report
        aid = record.get("action_id")
        if typ == "action_started":
            if type(aid) is not int or aid <= 0 or aid in running or aid in completed:
                report["reason"] = "Duplicate or invalid action ID"
                return report
            parent = record.get("parent_action_id")
            if parent is not None and parent not in running:
                report["reason"] = f"Action {aid} references inactive parent {parent}"
                return report
            running[aid] = record
        elif typ == "action_completed":
            if aid not in running:
                report["reason"] = f"Completion has no action start: {aid}"
                return report
            start = running.pop(aid)
            completed[aid] = {
                "action_id": aid,
                "native_type": start.get("native_type"),
                "canonical_action": start.get("canonical_action"),
                "parent_action_id": start.get("parent_action_id"),
                "start_ordinal": start["ordinal"],
                "end_ordinal": ordinal,
                "observed_delta": diff_scalars(
                    start.get("state") or {}, record.get("state") or {}),
                "pre_state_present": isinstance(start.get("state"), dict),
                "post_state_present": isinstance(record.get("state"), dict),
            }
        elif typ == "frame_state":
            info = record.get("data") or {}
            idx, state_hash = info.get("frame_index"), info.get("state_hash")
            if (type(idx) is not int or idx <= last_frame_index or not isinstance(
                    record.get("state"), dict) or not isinstance(state_hash, str) or not state_hash):
                report["reason"] = f"Invalid changed-frame snapshot at ordinal {ordinal}"
                return report
            if last_frame_hash == state_hash:
                report["reason"] = f"Duplicate unchanged frame snapshot at ordinal {ordinal}"
                return report
            last_frame_index, last_frame_hash = idx, state_hash
            report["frame_states"] += 1
        elif typ == "nested_choice_pause":
            report["nested_choice_pauses"] += 1
            choices.append({
                "action_id": aid,
                "pause_ordinal": ordinal,
                "pre_choice_state": record.get("state"),
            })
            if aid not in running:
                strict_failures.append(f"Nested choice with no active action: {aid}")
        elif typ == "history_reset":
            history_index = -1
            strict_failures.append("Native history reset")
        elif typ == "history_entry":
            data = record.get("data") or {}
            idx = data.get("history_index")
            if type(idx) is not int or idx != history_index + 1:
                report["reason"] = f"Nonconsecutive history index at ordinal {ordinal}"
                return report
            history_index = idx
            if aid is None:
                report["unattributed_history_entries"] += 1
            if data.get("missing_fields"):
                report["history_entries_with_missing_fields"] += 1
        elif typ in ("observation_failure", "anomaly"):
            strict_failures.append(f"{typ} at ordinal {ordinal}")
        elif typ == "stable_decision" and not isinstance(record.get("state"), dict):
            strict_failures.append(f"Missing stable decision state at {ordinal}")

    final = records[-1].get("data") or {}
    reported_open = final.get("open_actions", [])
    if sorted(running) != sorted(reported_open):
        report["reason"] = "Terminal open action IDs disagree with callback lifecycle"
        return report
    report["actions"] = [completed[a] for a in sorted(completed)]
    report["observed_mechanic_indicators"] = observed_indicators(
        report["actions"], records)
    for choice in choices:
        aid = choice["action_id"]
        ended = next((r for r in records if r.get("type") == "action_completed"
                     and r.get("action_id") == aid), None)
        candidate = infer_choice_discard(
            choice.get("pre_choice_state") or {},
            (ended or {}).get("state") or {})
        report["nested_choices"].append({
            "action_id": aid,
            "pause_ordinal": choice["pause_ordinal"],
            **candidate,
        })
    report["incomplete_observations"] = strict_failures
    if running or final.get("status") != "OBSERVED" or strict_failures:
        report["status"] = "INCOMPLETE_CAPTURE"
        report["reason"] = (
            f"{len(running)} unfinished actions, {len(strict_failures)} flagged observations"
            f"; native terminal status={final.get('status')}"
        )
    else:
        report["status"] = "CALLBACK_TIMELINE_CONSISTENT"
        report["reason"] = "Action callbacks and recorded history have consistent identities/order"
    return report


def load(path: Path) -> tuple[list[dict], str | None]:
    records: list[dict] = []
    try:
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if line.strip():
                try:
                    record = json.loads(line)
                except json.JSONDecodeError as exc:
                    return records, f"Malformed JSONL line {line_number}: {exc}"
                if not isinstance(record, dict):
                    return records, f"Expected object on line {line_number}"
                records.append(record)
    except OSError as exc:
        return records, str(exc)
    if path.with_suffix(path.suffix + ".incomplete").exists():
        return records, "Sidecar writer reported dropped or failed records"
    return records, None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sidecar", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load(args.sidecar)
    result = audit(records, error)
    rendered = json.dumps(result, sort_keys=True, indent=2)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if result["status"] == "CALLBACK_TIMELINE_CONSISTENT" else 2


if __name__ == "__main__":
    raise SystemExit(main())
