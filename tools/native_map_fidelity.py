#!/usr/bin/env python3
"""Fail-closed native v111 first-act map inspection and emulator comparison.

Only original source-map observations can establish game-vs-emulator map
alignment. This checks one fully captured initial StandardActMap against a
separately initialized exact emulator snapshot, including the native map RNG
cursor. It neither imports map state into the emulator nor proves later
combat/macro parity.
"""
from __future__ import annotations

import argparse
import base64
import binascii
import json
import re
import struct
from pathlib import Path

SOURCE_MAP_MARKER = "RunState.Map (after RunManager.GenerateMap)"
SOURCE_RNG_MARKER = "StandardActMap._rng.ToSerializable"
MAP_CODEC = "sts2-v111-xoshiro256-starstar-v1"
MAP_STREAM = "context.act_1_map"
MAP_PROFILE = "v111-experimental-native-standard-map-opening-v1"
MAP_GENERATION_PROFILE = "v111-experimental-native-standard-act1-map-v1"

# PrototypeRoomType values in src/Sts2Emulator.Core/PrototypeModel.cs.
POINT_TO_EMULATOR = {
    "Monster": 0, "Elite": 1, "Shop": 3, "RestSite": 4,
    "Boss": 5, "Unknown": 6, "Treasure": 7,
}
_COORD = re.compile(r"^1:(\d+):(\d+)$")
_U64 = (1 << 64) - 1


def _number(value: object, maximum: int) -> bool:
    return type(value) is int and 0 <= value <= maximum


def validate_native_map(data: object) -> list[str]:
    """Structural and nonperturbation gates for a single source projection."""
    if not isinstance(data, dict):
        return ["Missing native map object"]
    errors = []
    if (data.get("act_index") != 0
        or not isinstance(data.get("act_id"), str) or not data["act_id"]
        or data.get("map_type") != "StandardActMap"
        or data.get("source") != SOURCE_MAP_MARKER
        or data.get("map_rng_source") != SOURCE_RNG_MARKER
        or data.get("source_rng_consumed_by_inspector") is not False
        or data.get("source_map_mutation_history_complete") is not False
        or data.get("native_map_emulator_alignment") != "NOT_TESTED"):
        errors.append("Untrusted map provenance or misplaced parity claim")

    rng = data.get("map_rng")
    if (not isinstance(rng, dict)
        or not _number(rng.get("counter"), 2**31 - 1)
        or rng["counter"] == 0
        or any(not _number(rng.get(k), _U64)
               for k in ("s0", "s1", "s2", "s3"))
        or not (rng["s0"] | rng["s1"] | rng["s2"] | rng["s3"])):
        errors.append("Invalid native map RNG cursor")
    nodes = data.get("points")
    if not isinstance(nodes, list) or not 17 <= len(nodes) <= 110:
        return errors + ["Invalid or missing full map points"]
    seen = {}
    for n in nodes:
        if (not isinstance(n, dict)
            or not _number(n.get("row"), 17)
            or not _number(n.get("column"), 6)
            or n.get("point_type") not in (*POINT_TO_EMULATOR, "Ancient")
            or type(n.get("can_be_modified")) is not bool
            or not isinstance(n.get("children"), list)):
            return errors + ["Malformed native map point"]
        key = (n["row"], n["column"])
        if key in seen:
            return errors + [f"Duplicate native map point {key}"]
        seen[key] = n
    if (set((p["row"], p["column"]) for p in nodes)
        != set(sorted(seen))):
        errors.append("Internal map coordinate collision")
    if [(p["row"], p["column"]) for p in nodes] != sorted(seen):
        errors.append("Source map points are not sorted by coordinate")
    if ((0, 3) not in seen or seen[0, 3]["point_type"] != "Ancient"
        or (16, 3) not in seen or seen[16, 3]["point_type"] != "Boss"):
        errors.append("First-act ancient or boss boundary missing")
    if sum(p["point_type"] == "Ancient" for p in nodes) != 1:
        errors.append("Unexpected ancient node count")
    if sum(p["point_type"] == "Boss" for p in nodes) != 1:
        errors.append("Unexpected first-act boss count")
    if any(row > 16 for row, col in seen):
        errors.append("Unexpected second boss or post-act map node")
    for row in range(1, 16):
        if not 1 <= sum(p["row"] == row for p in nodes) <= 7:
            errors.append(f"Invalid native map occupancy at row {row}")
            break
    for p in nodes:
        row, col = p["row"], p["column"]
        targets = []
        for child in p["children"]:
            if (not isinstance(child, dict)
                or not _number(child.get("row"), 17)
                or not _number(child.get("column"), 6)):
                return errors + [f"Malformed child of {(row, col)}"]
            target = child["row"], child["column"]
            if target not in seen or target[0] != row + 1:
                errors.append(f"Broken native map edge {(row, col)} -> {target}")
            targets.append(target)
        if targets != sorted(set(targets)):
            errors.append(f"Repeated or unsorted map children at {(row, col)}")
        if row != 16 and not targets:
            errors.append(f"Missing outgoing edges at {(row, col)}")
        if row == 16 and targets:
            errors.append("Terminal boss has outgoing edges")
        if row == 1 and p["point_type"] != "Monster":
            errors.append(f"First map room is not Monster at {(row, col)}")
        if row == 9 and p["point_type"] != "Treasure":
            errors.append(f"Fixed treasure row differs at {(row, col)}")
        if row == 15 and p["point_type"] != "RestSite":
            errors.append(f"Preboss rest row differs at {(row, col)}")
    if (0, 3) in seen:
        pending = [(0, 3)]
        reached = set(pending)
        while pending:
            node = seen[pending.pop()]
            for child in node["children"]:
                target = child["row"], child["column"]
                if target in seen and target not in reached:
                    reached.add(target)
                    pending.append(target)
        if set(seen) != reached:
            errors.append("Native map contains unreachable points")
    return errors


def _source_graph(data: dict) -> tuple[dict, list]:
    nodes = {}
    for point in data["points"]:
        key = point["row"], point["column"]
        if key == (0, 3):
            continue
        nodes[key] = {
            "room_type": POINT_TO_EMULATOR[point["point_type"]],
            "children": sorted((c["row"], c["column"]) for c in point["children"]),
        }
    ancient = next(p for p in data["points"] if p["row"] == 0)
    entries = sorted((c["row"], c["column"]) for c in ancient["children"])
    return nodes, entries


def _parse_id(value: object) -> tuple[int, int]:
    m = _COORD.fullmatch(value) if isinstance(value, str) else None
    if not m:
        raise ValueError("Invalid emulator map node ID")
    row, col = int(m.group(1)), int(m.group(2))
    if not 1 <= row <= 16 or not 0 <= col <= 6:
        raise ValueError("Out-of-range emulator map coordinate")
    return row, col


def _emulator_graph(snapshot: dict) -> tuple[dict, list]:
    if (snapshot.get("schema") != "sts2-exact-snapshot-v1"
        or not isinstance(snapshot.get("state"), dict)):
        raise ValueError("Expected independently created exact emulator snapshot")
    state = snapshot["state"]
    if (not isinstance(state.get("configuration"), dict)
        or state["configuration"].get("fidelity_id") != MAP_PROFILE
        or not isinstance(state.get("world"), dict)):
        raise ValueError("Native StandardActMap fidelity profile is missing")
    world = state["world"]
    m = world.get("map")
    if (not isinstance(m, dict)
        or m.get("generation_profile_id") != MAP_GENERATION_PROFILE
        or not isinstance(m.get("nodes"), list)
        or not isinstance(m.get("entry_node_ids"), list)):
        raise ValueError("Emulator snapshot has no complete act map")
    result = {}
    for node in m["nodes"]:
        if not isinstance(node, dict) or type(node.get("room_type")) is not int:
            raise ValueError("Malformed emulator map node")
        key = _parse_id(node.get("node_id"))
        children = [_parse_id(item) for item in (node.get("next_node_ids") or [])]
        if key in result or children != sorted(set(children)):
            raise ValueError("Duplicate emulator node or noncanonical children")
        result[key] = {
            "room_type": node["room_type"],
            "children": children,
        }
    entries = sorted(_parse_id(item) for item in m["entry_node_ids"])
    if entries != sorted(set(entries)):
        raise ValueError("Duplicate emulator map entries")
    return result, entries


def _emulator_cursor(snapshot: dict) -> dict:
    s = snapshot["state"].get("native_map_rng")
    streams = s.get("streams") if isinstance(s, dict) else None
    if not isinstance(streams, list) or len(streams) != 1:
        raise ValueError("Missing independent emulator map RNG stream")
    r = streams[0]
    if (r.get("stream_id") != MAP_STREAM or r.get("codec") != MAP_CODEC
        or not _number(r.get("call_count"), 2**31 - 1)
        or not isinstance(r.get("state_bytes"), str)):
        raise ValueError("Incorrect emulator map RNG ownership or codec")
    try:
        raw = base64.b64decode(r["state_bytes"], validate=True)
    except (binascii.Error, ValueError) as exc:
        raise ValueError("Malformed emulator RNG state bytes") from exc
    if len(raw) != 32:
        raise ValueError("Emulator map RNG has invalid word count")
    words = struct.unpack("<QQQQ", raw)
    return {"counter": r["call_count"],
            **{f"s{i}": word for i, word in enumerate(words)}}


def compare_native_map(prelude_audit: dict, emulator_snapshot: dict) -> dict:
    """Compare exact first divergence; never claim whole-run native parity."""
    out = {
        "status": "BLOCKED_UNAUDITED_MAP_SOURCE",
        "native_map_topology_parity": "NOT_TESTED",
        "native_map_rng_alignment": "NOT_TESTED",
        "whole_run_game_parity": "NOT_TESTED",
        "first_difference": None,
    }
    if (not isinstance(prelude_audit, dict)
        or prelude_audit.get("schema") != "native-run-prelude-audit-v1"
        or prelude_audit.get("status") not in (
            "RUN_PRELUDE_SOURCE_OBSERVED", "RUN_PRELUDE_PAIRED_SOURCE_OBSERVED")
        or not isinstance(prelude_audit.get("run_identity"), dict)):
        return out
    source = prelude_audit.get("source_initial_map_snapshot")
    if source is None:
        out["status"] = "BLOCKED_SOURCE_MAP_SNAPSHOT_MISSING"
        return out
    failures = validate_native_map(source)
    if failures:
        out["status"] = "BLOCKED_INVALID_SOURCE_MAP"
        out["first_difference"] = {"path": "$.source_initial_map_snapshot",
                                   "reason": failures[0]}
        return out
    ident = prelude_audit["run_identity"]
    state = emulator_snapshot.get("state") if isinstance(emulator_snapshot, dict) else None
    expected_act = {"OVERGROWTH": 0, "UNDERDOCKS": 1}.get(
        str(ident.get("first_act_id", "")).upper())
    configuration = state.get("configuration") if isinstance(state, dict) else None
    actual_acts = configuration.get("acts") if isinstance(configuration, dict) else None
    if (not isinstance(state, dict)
        or state.get("run_seed") != ident.get("seed")
        or state.get("ascension") != ident.get("ascension")
        or expected_act is None
        or not isinstance(actual_acts, list)
        or not actual_acts or actual_acts[0] != expected_act
        or configuration.get("character_id") != "silent"):
        out["status"] = "BLOCKED_EMULATOR_IDENTITY"
        out["first_difference"] = {
            "path": "$.state.run_identity", "native": {
                "seed": ident.get("seed"), "ascension": ident.get("ascension")},
            "emulator": None if not isinstance(state, dict) else {
                "seed": state.get("run_seed"), "ascension": state.get("ascension")}}
        return out
    try:
        native_nodes, native_entries = _source_graph(source)
        emu_nodes, emu_entries = _emulator_graph(emulator_snapshot)
        emu_rng = _emulator_cursor(emulator_snapshot)
    except (ValueError, KeyError, TypeError) as exc:
        out["status"] = "BLOCKED_INVALID_EMULATOR_MAP"
        out["first_difference"] = {"path": "$.state.world.map",
                                   "reason": str(exc)}
        return out

    # Compare topology and cursor independently, preserving both diagnostics.
    native_rng = source["map_rng"]
    rng_keys = ("counter", "s0", "s1", "s2", "s3")
    rng_difference = next((k for k in rng_keys
                           if native_rng[k] != emu_rng[k]), None)
    out["native_map_rng_alignment"] = (
        "MATCHED" if rng_difference is None else "DIVERGED")
    first = None
    if native_entries != emu_entries:
        first = {"path": "$.map.entry_node_ids",
                 "native": native_entries, "emulator": emu_entries}
    if first is None:
        for key in sorted(set(native_nodes) | set(emu_nodes)):
            native, emu = native_nodes.get(key), emu_nodes.get(key)
            if native is None or emu is None:
                first = {"path": f"$.map.nodes[{key[0]}:{key[1]}]",
                         "native": native, "emulator": emu}
                break
            for field in ("room_type", "children"):
                if native[field] != emu[field]:
                    first = {"path": f"$.map.nodes[{key[0]}:{key[1]}].{field}",
                             "native": native[field], "emulator": emu[field]}
                    break
            if first is not None:
                break
    out["native_map_topology_parity"] = "MATCHED" if first is None else "DIVERGED"
    if first is None and rng_difference is not None:
        first = {"path": f"$.native_map_rng.{rng_difference}",
                 "native": native_rng[rng_difference],
                 "emulator": emu_rng[rng_difference]}
    out["first_difference"] = first
    out["status"] = (
        "SOURCE_MAP_AND_RNG_MATCHED" if first is None
        else "SOURCE_MAP_OR_RNG_DIVERGENCE")
    return out


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prelude-audit", type=Path, required=True)
    parser.add_argument("--emulator-snapshot", type=Path, required=True)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    prelude = json.loads(args.prelude_audit.read_text())
    emulator = json.loads(args.emulator_snapshot.read_text())
    result = compare_native_map(prelude, emulator)
    output = json.dumps(result, indent=2, sort_keys=True)
    if args.out:
        args.out.write_text(output + "\n")
    print(output)
    return 0 if result["status"] == "SOURCE_MAP_AND_RNG_MATCHED" else 1


if __name__ == "__main__":
    raise SystemExit(main())
