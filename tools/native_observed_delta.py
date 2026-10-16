#!/usr/bin/env python3
"""Report *observed* changes between two native decision checkpoints.

This does NOT simulate a card, infer damage amounts, fill in missing effects,
or certify emulator parity. Automatic actions can occur between checkpoints.
Only fields actually present in the original native expectation are reported.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


SCALARS = (
    ("player.hp", ("player", "hp")),
    ("player.block", ("player", "block")),
    ("combat.energy", ("combat", "energy")),
    ("combat.turn", ("combat", "turn")),
)
ZONES = ("hand", "draw_pile", "discard_pile", "exhaust_pile")


def lookup(state: dict, path: tuple[str, ...]) -> Any:
    cursor: Any = state
    for part in path:
        if not isinstance(cursor, dict) or part not in cursor:
            return None
        cursor = cursor[part]
    return cursor


def value_change(before: Any, after: Any) -> dict | None:
    if before == after:
        return None
    result = {"before": before, "after": after}
    if type(before) in (int, float) and type(after) in (int, float):
        result["delta"] = after - before
    return result


def zone_index(state: dict) -> tuple[dict, dict]:
    zones: dict[str, list] = {}
    entries: dict[int, list[str]] = {}
    for zone in ZONES:
        cards = lookup(state, ("combat", zone))
        if not isinstance(cards, list):
            continue
        zones[zone] = cards
        for card in cards:
            if not isinstance(card, dict) or type(card.get("instance_id")) is not int:
                continue
            entries.setdefault(card["instance_id"], []).append(zone)
    return zones, entries


def observed_delta(fixture: dict) -> dict:
    if fixture.get("status") != "RECORDED_NATIVE_EXPECTATION":
        raise ValueError("A recorded native expectation is required")
    before = fixture["pre_state"]
    after = fixture["post_state"]
    result: dict[str, Any] = {
        "schema": "native-observed-delta-v1",
        "decision_id": fixture["decision_id"],
        "card_family": fixture["card_family"],
        "selected_action": fixture["selected_action"],
        "comparison": "NATIVE_CHECKPOINT_OBSERVATION_ONLY",
        "emulator_parity": "NOT_TESTED",
        "warning": "These are checkpoint differences, not proven immediate card effects.",
        "scalars": {},
        "creatures": {},
        "card_zone_counts": {},
        "card_movements": [],
        "event_kinds": [e.get("kind") for e in fixture.get("native_events", [])],
    }
    for key, path in SCALARS:
        left, right = lookup(before, path), lookup(after, path)
        if left is not None and right is not None:
            change = value_change(left, right)
            if change is not None:
                result["scalars"][key] = change

    a = lookup(before, ("combat", "creatures"))
    b = lookup(after, ("combat", "creatures"))
    if isinstance(a, list) and isinstance(b, list):
        previous = {c.get("id"): c for c in a if isinstance(c, dict) and c.get("id")}
        following = {c.get("id"): c for c in b if isinstance(c, dict) and c.get("id")}
        for identity in sorted(set(previous) & set(following)):
            changes = {}
            for field in ("hp", "block", "alive", "powers"):
                if field in previous[identity] and field in following[identity]:
                    change = value_change(previous[identity][field], following[identity][field])
                    if change is not None:
                        changes[field] = change
            if changes:
                result["creatures"][identity] = {
                    "model_id": previous[identity].get("model_id"), "changes": changes}

    old_zones, old_positions = zone_index(before)
    new_zones, new_positions = zone_index(after)
    for zone in ZONES:
        if zone in old_zones and zone in new_zones:
            change = value_change(len(old_zones[zone]), len(new_zones[zone]))
            if change is not None:
                result["card_zone_counts"][zone] = change

    for card_id in sorted(set(old_positions) | set(new_positions)):
        old = old_positions.get(card_id, [])
        new = new_positions.get(card_id, [])
        if old == new:
            continue
        # A duplicate instance identity cannot be identified as a real move.
        if len(old) > 1 or len(new) > 1:
            result["card_movements"].append(
                {"instance_id": card_id, "status": "AMBIGUOUS_DUPLICATE_ID"})
            continue
        result["card_movements"].append(
            {"instance_id": card_id,
             "from": old[0] if old else None,
             "to": new[0] if new else None})
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_fixture", type=Path,
                        help="Recorded native-expectation fixture JSON")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    fixture = json.loads(args.native_fixture.read_text(encoding="utf-8"))
    report = observed_delta(fixture)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n",
                        encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
