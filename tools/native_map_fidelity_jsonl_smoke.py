#!/usr/bin/env python3
"""Exercise the complete map adapter against the *real* emulator JSONL server.

All "native" records below are constructed from the same emulator snapshot.
This verifies serialization, graph normalization, and first-difference
reporting, NEVER game-vs-emulator parity.
"""
from __future__ import annotations

import argparse
import base64
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))
from sts2_emulator import Session
from native_map_fidelity import (
    MAP_PROFILE, SOURCE_MAP_MARKER, SOURCE_RNG_MARKER,
    compare_native_map, validate_native_map,
)

NATIVE_ROOM_FROM_EMULATOR = {
    0: "Monster", 1: "Elite", 3: "Shop", 4: "RestSite",
    5: "Boss", 6: "Unknown", 7: "Treasure",
}


def synthetic_audit_from_snapshot(snapshot: dict, act_name: str) -> dict:
    """Convert emulator map to native-shaped synthetic *test* records."""
    state = snapshot["state"]
    nodes = state["world"]["map"]["nodes"]
    streams = state["native_map_rng"]["streams"]
    if len(streams) != 1:
        raise AssertionError("Expected one native act-map RNG stream")
    words = struct.unpack(
        "<QQQQ", base64.b64decode(streams[0]["state_bytes"], validate=True))
    cursor = {"counter": streams[0]["call_count"],
              **{f"s{i}": word for i, word in enumerate(words)}}

    def point_id(value: str) -> tuple[int, int]:
        _, row, col = value.split(":")
        return int(row), int(col)

    points = [{
        "column": 3, "row": 0, "point_type": "Ancient",
        "can_be_modified": True,
        "children": [{"row": row, "column": col}
                     for row, col in sorted(
                         point_id(e) for e in state["world"]["map"]["entry_node_ids"])],
    }]
    for node in nodes:
        row, col = point_id(node["node_id"])
        point_type = NATIVE_ROOM_FROM_EMULATOR[node["room_type"]]
        points.append({
            "row": row, "column": col, "point_type": point_type,
            "can_be_modified": row not in (1, 9, 15, 16),
            "children": [{"row": r, "column": c} for r, c in sorted(
                point_id(child) for child in node["next_node_ids"])],
        })
    points.sort(key=lambda node: (node["row"], node["column"]))
    source = {
        "act_index": 0, "act_id": act_name.upper(),
        "map_type": "StandardActMap", "points": points,
        "map_rng": cursor,
        "source": SOURCE_MAP_MARKER, "map_rng_source": SOURCE_RNG_MARKER,
        "source_rng_consumed_by_inspector": False,
        "source_map_mutation_history_complete": False,
        "native_map_emulator_alignment": "NOT_TESTED",
    }
    structural = validate_native_map(source)
    if structural:
        raise AssertionError("Emulator map cannot form synthetic source graph: "
                             + repr(structural))
    return {
        "schema": "native-run-prelude-audit-v1",
        "status": "RUN_PRELUDE_SOURCE_OBSERVED",
        "run_identity": {
            "seed": state["run_seed"], "ascension": state["ascension"],
            "first_act_id": act_name.upper(),
        },
        "source_initial_map_snapshot": source,
    }


def test_jsonl_map(command: list[str]) -> dict:
    cases = []
    with Session(command) as session:
        for act in ("Overgrowth", "Underdocks"):
            for ascension in (0, 10):
                seed = f"map-jsonl-self-consistency-{act.lower()}-a{ascension}"
                root = session.reset(
                    seed, mode="v111", character="silent",
                    first_act=act, ascension=ascension,
                    fidelity_id=MAP_PROFILE)
                legal = root.actions()
                if len(legal) != 1 or legal[0]["kind"] != "start_run":
                    raise AssertionError("No unique emulator start_run")
                state = root.step(legal[0]["actionId"])
                snapshot = json.loads(state.save_snapshot())
                source = synthetic_audit_from_snapshot(snapshot, act)
                equal = compare_native_map(source, snapshot)
                if equal["status"] != "SOURCE_MAP_AND_RNG_MATCHED":
                    raise AssertionError(
                        "Synthetic map adapter disagrees with emulator: "
                        + json.dumps(equal, sort_keys=True))
                # Independently exercise an RNG discrepancy without changing
                # graph structure, then a room-type discrepancy without
                # changing the RNG. Both should locate the exact first field.
                source["source_initial_map_snapshot"]["map_rng"]["counter"] += 1
                rng_diff = compare_native_map(source, snapshot)
                if (rng_diff["native_map_topology_parity"] != "MATCHED"
                    or rng_diff["native_map_rng_alignment"] != "DIVERGED"
                    or rng_diff["first_difference"]["path"]
                        != "$.native_map_rng.counter"):
                    raise AssertionError("Missed synthetic map RNG divergence")
                source["source_initial_map_snapshot"]["map_rng"]["counter"] -= 1
                first = next(n for n in source["source_initial_map_snapshot"]["points"]
                             if n["row"] == 1)
                first["point_type"] = "Elite"
                bad = compare_native_map(source, snapshot)
                if bad["status"] != "BLOCKED_INVALID_SOURCE_MAP":
                    raise AssertionError(
                        "Modified fixed first room escaped source structural audit")
                cases.append({
                    "act": act, "ascension": ascension,
                    "synthetic_map_nodes": len(snapshot["state"]["world"]["map"]["nodes"]),
                    "map_rng_call_count": snapshot["state"]["native_map_rng"]
                                          ["streams"][0]["call_count"],
                    "real_emulator_jsonl": True,
                    "source_provenance": "EMULATOR_DERIVED_SYNTHETIC_ONLY",
                })
    return {
        "status": "EMULATOR_DERIVED_SYNTHETIC_MAP_ADAPTER_PASS",
        "cases": cases,
        "native_game_map_parity": "NOT_TESTED",
        "native_game_rng_alignment": "NOT_TESTED",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path, default=(
        ROOT / "src" / "Sts2Emulator.Cli" / "bin" /
        "Release" / "net9.0" / "Sts2Emulator.Cli.dll"))
    args = parser.parse_args()
    report = test_jsonl_map([
        args.dotnet, str(args.cli_dll), "prototype-ai-jsonl"])
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
