#!/usr/bin/env python3
"""Strict source-only audit of a run-wide native macro envelope v1 sidecar.

This verifies chronology and observed source fields. It never substitutes native
outcomes into an emulator and never certifies gameplay, RNG, or price parity.
"""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path

SCHEMA = "native-macro-envelope-v1"
KINDS = {
    "header", "map_snapshot", "room_entered", "map_request",
    "map_request_outcome", "shop_snapshot", "reward_offer",
    "reward_selection_requested", "reward_skip_requested",
    "event_choice_request", "event_choice_return",
    "combat_link", "observation_failure", "terminal",
}


def _integer(value: object, low: int = 0) -> bool:
    return type(value) is int and value >= low


def _coord(data: dict, col: str, row: str) -> tuple[int, int] | None:
    x, y = data.get(col), data.get(row)
    if x is None and y is None:
        return None
    if not _integer(x) or not _integer(y):
        raise ValueError("Malformed coordinate pair")
    return x, y


def audit(records: list[dict]) -> dict:
    result = {
        "schema": "native-macro-envelope-audit-v1",
        "status": "INCOMPLETE_CAPTURE",
        "reason": "",
        "record_counts": dict(Counter(r.get("type") for r in records)),
        "run_id": None,
        "maps": 0,
        "rooms": 0,
        "shop_snapshots": 0,
        "reward_offers": 0,
        "event_choices": 0,
        "source_map_request_links": 0,
        "source_reward_object_links": 0,
        "observational_failures": [],
        "source_rng_consumed_by_inspector": False,
        "native_game_parity": "NOT_TESTED",
        "full_macro_legal_menu_certified": False,
        "whole_run_certified": False,
    }
    try:
        if not records or records[0].get("type") != "header":
            raise ValueError("Missing native macro header")
        header = records[0].get("data") or {}
        run_id = records[0].get("run_id")
        if (header.get("schema") != SCHEMA or not isinstance(run_id, str)
            or not run_id or header.get("run_id") != run_id
            or header.get("observation_only") is not True
            or header.get("native_gameplay_mutated") is not False
            or header.get("native_rng_consumed_by_inspector") is not False
            or header.get("native_emulator_parity") != "NOT_TESTED"):
            raise ValueError("Invalid native macro header")
        result["run_id"] = run_id
        maps: dict[int, dict[tuple[int, int], set[tuple[int, int]]]] = {}
        requests: set[int] = set()
        request_results: set[int] = set()
        events: set[int] = set()
        event_returns: set[int] = set()
        rewards: dict[int, int] = {}
        observed_shop = False
        terminal_seen = False
        last_room = None
        for index, record in enumerate(records, 1):
            kind = record.get("type")
            data = record.get("data") or {}
            if (record.get("ordinal") != index or
                record.get("run_id") != run_id or kind not in KINDS or
                not isinstance(data, dict)):
                raise ValueError(f"Invalid record chronology at {index}")
            if kind == "header":
                if index != 1:
                    raise ValueError("Duplicate macro header")
                continue
            if terminal_seen:
                raise ValueError("Record after terminal")
            if kind == "map_snapshot":
                act = data.get("act_index")
                points = data.get("points")
                rng = data.get("map_rng") or {}
                if (not _integer(act) or act in maps
                    or not isinstance(points, list) or not points
                    or not _integer(rng.get("counter"))
                    or data.get("source") !=
                        "RunState.Map; StandardActMap._rng.ToSerializable"
                    or data.get("legal_destination_basis") != "SOURCE_MAP_EDGES"
                    or data.get("map_ui_eligibility") != "NOT_VERIFIED"
                    or data.get("emulator_parity") != "NOT_TESTED"):
                    raise ValueError("Invalid native map source")
                grid = {}
                for point in points:
                    key = _coord(point, "column", "row")
                    children = point.get("children")
                    if key is None or key in grid or not isinstance(children, list):
                        raise ValueError("Malformed or duplicate map node")
                    destinations = [_coord(p, "column", "row") for p in children]
                    if None in destinations or len(set(destinations)) != len(destinations):
                        raise ValueError("Invalid map adjacency")
                    grid[key] = set(destinations)
                if any(child not in grid for edges in grid.values() for child in edges):
                    raise ValueError("Map child is missing")
                maps[act] = grid
                result["maps"] += 1
            elif kind == "room_entered":
                location = _coord(data, "column", "row")
                act = data.get("act_index")
                outgoing = data.get("outgoing_map_edges")
                if (not _integer(act) or not _integer(data.get("total_floor"))
                    or not _integer(data.get("player_hp"))
                    or not _integer(data.get("player_gold"))
                    or not isinstance(outgoing, list)
                    or not isinstance(data.get("room_type"), str)
                    or data.get("source") != "RunManager.RoomEntered"
                    or data.get("actual_map_screen_actions") != "NOT_CAPTURED"
                    or data.get("emulator_parity") != "NOT_TESTED"):
                    raise ValueError("Invalid room entry")
                edges = [_coord(d, "column", "row") for d in outgoing]
                if None in edges or len(set(edges)) != len(edges):
                    raise ValueError("Invalid room adjacency")
                if location is not None and act in maps:
                    if location not in maps[act] or set(edges) != maps[act][location]:
                        raise ValueError("Room edges disagree with native map")
                result["rooms"] += 1
                last_room = data.get("room_type")
                observed_shop = False
            elif kind == "map_request":
                rid = data.get("request_id")
                target = _coord(data, "to_column", "to_row")
                _coord(data, "from_column", "from_row")
                if (not _integer(rid, 1) or rid in requests
                    or data.get("kind") not in ("map_move", "map_vote")
                    or (data.get("kind") == "map_move" and target is None)
                    or data.get("queue_acceptance") != "UNVERIFIED"
                    or data.get("actual_legal_menu") != "NOT_CAPTURED"):
                    raise ValueError("Invalid macro map request")
                requests.add(rid)
            elif kind == "map_request_outcome":
                rid = data.get("request_id")
                if (rid not in requests or rid in request_results
                    or data.get("execution_completed") is not False
                    or data.get("emulator_parity") != "NOT_TESTED"):
                    raise ValueError("Unlinked map queue outcome")
                request_results.add(rid)
                result["source_map_request_links"] += 1
            elif kind == "shop_snapshot":
                offers = data.get("offers")
                if (last_room != "Shop" or not isinstance(offers, list)
                    or not _integer(data.get("player_gold"))
                    or data.get("cause") not in
                        ("ROOM_ENTERED", "SOURCE_PURCHASE_COMPLETED")
                    or (data["cause"] == "SOURCE_PURCHASE_COMPLETED"
                        and not observed_shop)
                    or data.get("price_modifiers_evaluated_by_inspector") is not False
                    or data.get("native_rng_consumed_by_inspector") is not False
                    or data.get("emulator_parity") != "NOT_TESTED"):
                    raise ValueError("Invalid source shop snapshot")
                slots = []
                for offer in offers:
                    if (not isinstance(offer, dict)
                        or not _integer(offer.get("slot"))
                        or not _integer(offer.get("base_cost"))
                        or type(offer.get("is_stocked")) is not bool
                        or offer.get("displayed_price") is not None
                        or offer.get("displayed_price_coverage") !=
                            "NOT_CAPTURED_FROM_NATIVE_UI"
                        or offer.get("purchase_eligible") != "NOT_VERIFIED"):
                        raise ValueError("Fabricated shop price or malformed offer")
                    slots.append(offer["slot"])
                if len(slots) != len(set(slots)):
                    raise ValueError("Duplicate shop slot")
                observed_shop = True
                result["shop_snapshots"] += 1
            elif kind == "reward_offer":
                oid = data.get("offer_id")
                rows = data.get("rewards")
                if (not _integer(oid, 1) or oid in rewards
                    or not _integer(data.get("native_set_id"))
                    or not isinstance(rows, list)
                    or type(data.get("can_skip")) is not bool
                    or data.get("native_choice_contents_complete") is not False
                    or data.get("emulator_parity") != "NOT_TESTED"
                    or any(
                        not isinstance(row, dict)
                        or row.get("index") != i
                        or not isinstance(row.get("native_type"), str)
                        or not isinstance(row.get("populated"), bool)
                        or row.get("content_coverage") not in (
                            "SOURCE_POPULATED_REWARD_FIELDS", "NOT_CAPTURED")
                        or not isinstance(row.get("model_ids"), list)
                        or any(not isinstance(model, str) or not model
                               for model in row["model_ids"])
                        or (row.get("gold_amount") is not None and
                            not _integer(row.get("gold_amount")))
                        or (row.get("content_coverage") == "NOT_CAPTURED"
                            and (row["model_ids"] or row.get("gold_amount")
                                 is not None))
                        for i, row in enumerate(rows))):
                    raise ValueError("Invalid reward offer")
                rewards[oid] = len(rows)
                result["reward_offers"] += 1
            elif kind == "reward_selection_requested":
                oid, selected = data.get("offer_id"), data.get("selected_index")
                linked = data.get("choice_link") == "SAME_NATIVE_REWARD_OBJECT"
                if (data.get("effect_completed") is not False or
                    (linked and (oid not in rewards or
                        not _integer(selected) or selected >= rewards[oid]))
                    or (not linked and (oid is not None or selected is not None
                        or data.get("choice_link") != "UNVERIFIED"))):
                    raise ValueError("Invalid reward object link")
                if linked:
                    result["source_reward_object_links"] += 1
            elif kind == "reward_skip_requested":
                if (data.get("skipped_set_identity") != "UNVERIFIED"
                    or data.get("effect_completed") is not False):
                    raise ValueError("Fabricated skip settlement")
            elif kind == "event_choice_request":
                eid = data.get("request_id")
                options = data.get("options")
                selected = data.get("selected_index")
                if (not _integer(eid, 1) or eid in events
                    or not isinstance(options, list) or not options
                    or not _integer(selected) or selected >= len(options)
                    or data.get("source_menu_coverage") != "SOURCE_CURRENT_OPTIONS"
                    or data.get("effect_completed") is not False
                    or any(o.get("index") != i or
                        type(o.get("is_locked")) is not bool
                        for i, o in enumerate(options))):
                    raise ValueError("Invalid event source options")
                events.add(eid)
                result["event_choices"] += 1
            elif kind == "event_choice_return":
                eid = data.get("request_id")
                if (eid not in events or eid in event_returns
                    or data.get("effect_completed") is not False):
                    raise ValueError("Unlinked event return")
                event_returns.add(eid)
            elif kind == "combat_link":
                if (not isinstance(data.get("combat_run_id"), str)
                    or not data["combat_run_id"]
                    or data.get("emulator_parity") != "NOT_TESTED"):
                    raise ValueError("Invalid combat identity link")
            elif kind == "observation_failure":
                result["observational_failures"].append(data)
            elif kind == "terminal":
                terminal_seen = True
                if (data.get("status") not in
                        ("SOURCE_OBSERVED", "INCOMPLETE_CAPTURE")
                    or data.get("native_game_parity") != "NOT_TESTED"
                    or data.get("whole_run_certified") is not False
                    or data.get("open_map_requests") != len(requests - request_results)
                    or data.get("open_event_requests") != len(events - event_returns)):
                    raise ValueError("Invalid macro terminal coverage")
                if data["status"] == "INCOMPLETE_CAPTURE":
                    result["reason"] = "Native recorder reported incomplete capture"
                elif not result["observational_failures"]:
                    result["status"] = "SOURCE_MACRO_TIMELINE_CONSISTENT"
        if not terminal_seen:
            result["status"] = "SOURCE_CAPTURE_IN_PROGRESS"
            result["reason"] = "Awaiting native run terminal"
        elif result["observational_failures"]:
            result["reason"] = "Source observation failure recorded"
        return result
    except (KeyError, TypeError, ValueError) as error:
        result["reason"] = str(error)
        return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sidecar", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    try:
        rows = [json.loads(line) for line in
                args.sidecar.read_text(encoding="utf-8").splitlines() if line.strip()]
        report = audit(rows)
    except (OSError, json.JSONDecodeError) as error:
        report = {"schema": "native-macro-envelope-audit-v1",
                  "status": "INCOMPLETE_CAPTURE", "reason": str(error)}
    rendered = json.dumps(report, indent=2, sort_keys=True)
    if args.out:
        args.out.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    if report["status"] == "INCOMPLETE_CAPTURE":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
