#!/usr/bin/env python3
"""Verify source run-start/room/map evidence and its first native combat link.

A source RoomEntered callback proves the visited map coordinate. A map vote
or MoveToMapCoordAction queue request is a *request*, not proof that this
particular request caused the arrival. Native Neow, reward, event, shop,
rest-site, potion and deck choices are not covered by this L1 source journal.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_action_envelope_audit import audit as audit_sidecar
from native_action_envelope_audit import load as load_sidecar

_TYPES = {
    "run_started", "room_entered", "run_rng_snapshot",
    "map_action_request",
    "map_action_outcome", "combat_link", "observation_failure", "terminal",
}
_EXPLICIT_GAPS = [
    "NEOW_CHOICE_NOT_CAPTURED",
    "REWARD_AND_CARD_CHOICE_NOT_CAPTURED",
    "EVENT_CHOICE_NOT_CAPTURED",
    "SHOP_AND_REST_CHOICE_NOT_CAPTURED",
    "POTION_AND_RELIC_CHANGE_NOT_CAPTURED",
    "NATIVE_RUN_RNG_PRECOMBAT_CONTINUATION_NOT_CAPTURED",
]


def _nonnegative(value):
    return type(value) is int and value >= 0


def _coord(data, prefix):
    col = data.get(prefix + "column")
    row = data.get(prefix + "row")
    return ((col is None and row is None)
            or (_nonnegative(col) and _nonnegative(row)))


def _status(status, errors, events, **additional):
    return {
        "schema": "native-run-prelude-audit-v1",
        "status": status, "errors": errors,
        "run_identity": additional.get("identity"),
        "first_combat_link": additional.get("combat_link"),
        "source_room_arrivals": events.get("rooms", []),
        "source_run_rng_snapshots": events.get("rng_snapshots", []),
        "source_run_rng_state_observed": bool(events.get("rng_snapshots")),
        "precombat_rng_call_history_complete": False,
        "source_map_requests": events.get("requests", []),
        "source_map_outcomes": events.get("outcomes", []),
        "source_observed_map_arrival_matches": events.get("arrivals", []),
        "map_vote_is_human_proof": False,
        "map_request_proves_room_arrival": False,
        "native_macro_action_transcript_complete": False,
        "explicit_unobserved_macro_and_rng_inputs": _EXPLICIT_GAPS,
        "emulator_combat_bootstrap": "NOT_EXECUTED",
        "emulator_rng_alignment": "NOT_TESTED",
        "native_emulator_parity": "NOT_TESTED",
        "paired_combat_audit": additional.get("paired_status", "NOT_PROVIDED"),
    }


def audit(records: list[dict], error: str | None = None,
          combat_sidecar: list[dict] | None = None,
          combat_audit_status: str | None = None) -> dict:
    errors = []
    events = {"rooms": [], "requests": [], "outcomes": [],
              "arrivals": [], "rng_snapshots": []}
    if error:
        errors.append(error)
    if not records or not isinstance(records, list):
        errors.append("Missing run prelude records")
        return _status("INVALID_PRELUDE", errors, events)
    if records[0].get("type") != "run_started":
        errors.append("First source event must be RunStarted")
    if records[-1].get("type") != "terminal":
        errors.append("Final source event must be terminal")
    first = records[0].get("data") or {}
    run_id = records[0].get("run_id")
    if (first.get("schema") != "native-run-prelude-v1"
        or not isinstance(first.get("game_build"), str)
        or not first["game_build"]
        or not isinstance(run_id, str) or not run_id
        or not isinstance(first.get("seed"), str) or not first["seed"]
        or not _nonnegative(first.get("ascension"))
        or not isinstance(first.get("character_id"), str)
        or not first["character_id"]
        or not isinstance(first.get("first_act_id"), str)
        or not first["first_act_id"]
        or first.get("player_count") != 1
        or first.get("source") != "RunManager.RunStarted(RunState)"
        or first.get("source_seed") != "RunState.Rng.StringSeed"
        or first.get("source_macro_choices_complete") is not False
        or first.get("source_rng_consumed_by_inspector") is not False
        or first.get("emulator_bootstrap") != "NOT_TESTED"):
        errors.append("Invalid source-verified native RunStarted header")
    identity = {key: first.get(key) for key in (
        "game_build", "seed", "ascension", "character_id", "first_act_id")}
    identity["run_id"] = run_id
    request_ids: dict[int, dict] = {}
    outcomes_seen = set()
    source_rng_streams = None
    player_rng_seed = None
    link = None
    terminal = records[-1].get("data") or {}
    for ordinal, record in enumerate(records, 1):
        typ = record.get("type")
        data = record.get("data") or {}
        if (type(record.get("ordinal")) is not int
            or record["ordinal"] != ordinal
            or record.get("run_id") != run_id
            or typ not in _TYPES or not isinstance(data, dict)):
            errors.append(f"Bad ordinal, run ID, record type or data at {ordinal}")
            continue
        if typ == "run_started" and ordinal != 1:
            errors.append("Repeated run start")
        elif typ == "terminal" and ordinal != len(records):
            errors.append("Terminal before end")
        elif typ == "room_entered":
            if (not _coord(data, "map_")
                or not _nonnegative(data.get("act_index"))
                or not _nonnegative(data.get("act_floor"))
                or not _nonnegative(data.get("total_floor"))
                or not isinstance(data.get("act_id"), str)
                or not isinstance(data.get("room_type"), str)
                or not _nonnegative(data.get("player_hp"))
                or not _nonnegative(data.get("player_gold"))
                or data.get("source") != "RunManager.RoomEntered"
                or data.get("emulator_parity") != "NOT_TESTED"
                or data.get("command_provenance") != "UNVERIFIED"
                or data.get("visited_destination") != (
                    "SOURCE_CURRENT_MAP_COORD" if data.get("map_column") is not None
                    else "MAP_COORD_UNASSIGNED")):
                errors.append(f"Invalid native room callback at {ordinal}")
            else:
                events["rooms"].append({
                    "ordinal": ordinal,
                    "act_index": data["act_index"],
                    "act_id": data["act_id"],
                    "act_floor": data["act_floor"],
                    "total_floor": data["total_floor"],
                    "map_column": data.get("map_column"),
                    "map_row": data.get("map_row"),
                    "room_type": data["room_type"],
                    "room_model_id": data.get("room_model_id"),
                    "player_hp": data["player_hp"],
                    "player_gold": data["player_gold"],
                })
        elif typ == "run_rng_snapshot":
            site = data.get("site")
            streams = data.get("streams")
            valid = (
                site in ("run_started", "room_entered", "combat_attach")
                and _nonnegative(data.get("player_rng_seed"))
                and isinstance(streams, list) and len(streams) > 0
                and data.get("source") ==
                    "RunRngSet.ToSerializable+PlayerRngSet.ToSerializable"
                and data.get("source_rng_consumed_by_inspector") is False
                and data.get("native_rng_precombat_history_complete") is False
                and data.get("emulator_rng_alignment") == "NOT_TESTED")
            keys = []
            if valid:
                for value in streams:
                    if not isinstance(value, dict):
                        valid = False
                        break
                    pair = (value.get("scope"), value.get("stream_id"))
                    if (pair[0] not in ("run", "player")
                        or not isinstance(pair[1], str) or not pair[1]
                        or not _nonnegative(value.get("counter"))
                        or any(not _nonnegative(value.get(key))
                               or value[key] > 2**64 - 1
                               for key in ("s0", "s1", "s2", "s3"))):
                        valid = False
                        break
                    keys.append(pair)
                if (len(keys) != len(set(keys))
                    or not any(scope == "run" for scope, _ in keys)
                    or not any(scope == "player" for scope, _ in keys)
                    or keys != sorted(keys)):
                    valid = False
                if source_rng_streams is not None and keys != source_rng_streams:
                    valid = False
                if (player_rng_seed is not None
                    and data["player_rng_seed"] != player_rng_seed):
                    valid = False
                if site == "run_started" and (
                    len(events["rng_snapshots"]) > 0 or ordinal != 2):
                    valid = False
                if site == "room_entered" and (
                    not events["rooms"]
                    or events["rooms"][-1]["ordinal"] != ordinal - 1):
                    valid = False
                if site == "combat_attach" and (
                    link is not None
                    or not any(r.get("type") == "combat_link"
                               for r in records[ordinal:])):
                    valid = False
            if not valid:
                errors.append(f"Invalid native run RNG snapshot at {ordinal}")
            else:
                source_rng_streams = keys
                player_rng_seed = data["player_rng_seed"]
                events["rng_snapshots"].append({
                    "ordinal": ordinal,
                    "site": site,
                    "player_rng_seed": player_rng_seed,
                    "stream_count": len(streams),
                    "streams": streams,
                    "emulator_rng_alignment": "NOT_TESTED"
                })
        elif typ == "map_action_request":
            request_id = data.get("request_id")
            kind = data.get("kind")
            if (not _nonnegative(request_id) or request_id == 0
                or request_id in request_ids
                or kind not in ("map_vote", "map_move")
                or not _coord(data, "source_")
                or not _coord(data, "target_")
                or (kind == "map_move"
                    and data.get("target_column") is None)
                or (data.get("source_act") is not None
                    and not _nonnegative(data["source_act"]))
                or (data.get("map_generation") is not None
                    and not _nonnegative(data["map_generation"]))
                or data.get("source") !=
                    "ActionQueueSynchronizer.RequestEnqueue.prefix"
                or data.get("queue_acceptance") != "UNVERIFIED"
                or data.get("human_origin") != "UNVERIFIED"
                or data.get("emulator_action") != "NOT_MAPPED"):
                errors.append(f"Invalid native map request at {ordinal}")
            else:
                request_ids[request_id] = {"ordinal": ordinal, **data}
                events["requests"].append({"ordinal": ordinal, **data})
        elif typ == "map_action_outcome":
            request_id = data.get("request_id")
            if (request_id not in request_ids
                or request_id in outcomes_seen
                or data.get("source") !=
                    "ActionQueueSynchronizer.RequestEnqueue.postfix"
                or data.get("native_executor_completion") != "NOT_OBSERVED"
                or data.get("visited_room_proves_this_request") is not False):
                errors.append(f"Invalid native map request outcome at {ordinal}")
            else:
                outcomes_seen.add(request_id)
                events["outcomes"].append({"ordinal": ordinal, **data})
        elif typ == "combat_link":
            if (link is not None
                or not isinstance(data.get("combat_run_id"), str)
                or not data["combat_run_id"]
                or not isinstance(data.get("combat_trace_file"), str)
                or not data["combat_trace_file"].endswith(".jsonl")
                or data["combat_trace_file"] != Path(
                    data["combat_trace_file"]).name
                or not _nonnegative(data.get("act_index"))
                or not _coord(data, "map_")
                or data.get("source") != "NativeV111PassiveBridge.OpenCombat"
                or data.get("combat_initial_rng_alignment") != "NOT_TESTED"
                or data.get("full_macro_action_transcript") != "NOT_CAPTURED"):
                errors.append(f"Invalid first combat source link at {ordinal}")
            else:
                link = {"ordinal": ordinal, **data}
        elif typ == "observation_failure":
            errors.append(f"Source observer reported failure at {ordinal}")
    if set(request_ids) != outcomes_seen:
        errors.append("Native map requests missing queue return observations")
    if terminal.get("status") != "SOURCE_PRELUDE_OBSERVED":
        errors.append("Native prelude terminal is incomplete or lacks combat")
    if (terminal.get("combat_link_recorded") is not True
        or terminal.get("source_macro_choices_complete") is not False
        or terminal.get("native_rng_parity") != "NOT_TESTED"
        or terminal.get("emulator_bootstrap") != "NOT_TESTED"):
        errors.append("Prelude terminal improperly claims complete macro/RNG fidelity")
    if (not events["rng_snapshots"]
        or events["rng_snapshots"][0]["site"] != "run_started"
        or not any(s["site"] == "combat_attach"
                   for s in events["rng_snapshots"])):
        errors.append("Missing native run-start or first-combat RNG snapshot")
    if link is None:
        errors.append("Missing first combat link")
    else:
        matching_room_arrival = any(
            room["act_index"] == link["act_index"]
            and room["map_column"] == link["map_column"]
            and room["map_row"] == link["map_row"]
            for room in events["rooms"])
        if not matching_room_arrival:
            errors.append("Linked combat source RoomEntered not observed")
        if terminal.get("linked_combat_room_arrival_observed") is not True:
            errors.append("Terminal did not verify linked combat room arrival")
        if terminal.get("map_requests_missing_postfix") != 0:
            errors.append("Terminal has pending native map queue returns")

    # A coincident request/arrival is useful diagnostic evidence only.
    # It never proves which queued action caused an actual map traversal.
    for room in events["rooms"]:
        if room["map_column"] is None:
            continue
        for req in events["requests"]:
            if (req["kind"] == "map_move"
                and req["ordinal"] < room["ordinal"]
                and req["target_column"] == room["map_column"]
                and req["target_row"] == room["map_row"]
                and req["source_act"] == room["act_index"]):
                events["arrivals"].append({
                    "request_id": req["request_id"],
                    "request_ordinal": req["ordinal"],
                    "room_ordinal": room["ordinal"],
                    "map_column": room["map_column"],
                    "map_row": room["map_row"],
                    "causal_link_verified": False
                })
    paired_status = "NOT_PROVIDED"
    if combat_sidecar is not None:
        paired_status = combat_audit_status or "NOT_AUDITED"
        if paired_status != "CALLBACK_TIMELINE_CONSISTENT":
            errors.append("Native combat sidecar is not independently audited")
        elif link is not None:
            if not combat_sidecar:
                errors.append("Paired combat sidecar empty")
            else:
                c_header = combat_sidecar[0].get("data") or {}
                actual = [(r.get("data") or {}) for r in combat_sidecar
                          if r.get("type") == "native_run_identity"]
                if (c_header.get("run_id") != link["combat_run_id"]
                    or c_header.get("game_build") != first.get("game_build")
                    or len(actual) != 1
                    or actual[0].get("seed") != first.get("seed")
                    or actual[0].get("ascension") != first.get("ascension")):
                    errors.append("Prelude/combat source identity mismatch")
    if errors:
        status = "INVALID_PRELUDE"
    elif combat_sidecar is None:
        status = "RUN_PRELUDE_SOURCE_OBSERVED"
    else:
        status = "RUN_PRELUDE_PAIRED_SOURCE_OBSERVED"
    return _status(status, errors, events, identity=identity,
                   combat_link=link, paired_status=paired_status)


def load(path: Path) -> tuple[list[dict], str | None]:
    rows = []
    try:
        with path.open(encoding="utf-8") as stream:
            for n, line in enumerate(stream, 1):
                if not line.strip():
                    continue
                try:
                    value = json.loads(line)
                except json.JSONDecodeError as exc:
                    return rows, f"Invalid JSONL line {n}: {exc}"
                if not isinstance(value, dict):
                    return rows, f"Nonobject JSONL record at line {n}"
                rows.append(value)
    except OSError as exc:
        return rows, str(exc)
    if path.with_suffix(path.suffix + ".incomplete").exists():
        return rows, "Async run prelude writer recorded incomplete evidence"
    return rows, None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prelude", type=Path, required=True)
    parser.add_argument("--combat-sidecar", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load(args.prelude)
    sidecar = None
    paired_status = None
    if args.combat_sidecar is not None:
        sidecar, side_error = load_sidecar(args.combat_sidecar)
        side_result = audit_sidecar(sidecar, side_error)
        paired_status = side_result.get("status")
    result = audit(records, error, sidecar, paired_status)
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(rendered, end="")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered, encoding="utf-8")
    return (0 if result["status"] in (
        "RUN_PRELUDE_SOURCE_OBSERVED", "RUN_PRELUDE_PAIRED_SOURCE_OBSERVED")
        else 2)


if __name__ == "__main__":
    raise SystemExit(main())
