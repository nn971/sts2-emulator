"""Offline regression for source-v111 native map capture and divergence reporting.

All graph examples here are constructed test data, never original-game
evidence of map or RNG parity.
"""
import base64
import copy
import json
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_map_fidelity import (
    compare_native_map, validate_native_map, MAP_PROFILE, MAP_CODEC, MAP_STREAM,
    SOURCE_MAP_MARKER, SOURCE_RNG_MARKER,
)


def example():
    points = []
    for row in range(17):
        name = ("Ancient" if row == 0 else "Boss" if row == 16
                else "Treasure" if row == 9 else "RestSite" if row == 15
                else "Monster")
        points.append({
            "column": 3, "row": row,
            "point_type": name, "can_be_modified": row not in (0, 16),
            "children": [] if row == 16
                else [{"column": 3, "row": row + 1}],
        })
    rng = {"counter": 128, "s0": 17, "s1": 23, "s2": 31, "s3": 47}
    source = {
        "act_index": 0, "act_id": "OVERGROWTH", "map_type": "StandardActMap",
        "points": points, "map_rng": rng,
        "source": SOURCE_MAP_MARKER, "map_rng_source": SOURCE_RNG_MARKER,
        "source_rng_consumed_by_inspector": False,
        "source_map_mutation_history_complete": False,
        "native_map_emulator_alignment": "NOT_TESTED",
    }
    audit = {
        "schema": "native-run-prelude-audit-v1",
        "status": "RUN_PRELUDE_SOURCE_OBSERVED",
        "run_identity": {"seed": "source-example",
                         "ascension": 10, "first_act_id": "OVERGROWTH"},
        "source_initial_map_snapshot": source,
    }
    nodes = []
    point_type = {"Monster": 0, "Treasure": 7,
                  "RestSite": 4, "Boss": 5}
    for row in range(1, 17):
        name = points[row]["point_type"]
        nodes.append({
            "node_id": f"1:{row}:3", "act": 1, "floor": row,
            "room_type": point_type[name],
            "next_node_ids": [] if row == 16 else [f"1:{row + 1}:3"],
        })
    encoded = base64.b64encode(struct.pack("<QQQQ", 17, 23, 31, 47)).decode()
    snapshot = {
        "schema": "sts2-exact-snapshot-v1",
        "state": {
            "run_seed": "source-example", "ascension": 10,
            "configuration": {
                "fidelity_id": MAP_PROFILE, "character_id": "silent",
                "acts": [0, 2, 3]},
            "world": {"map": {
                "generation_profile_id": "v111-experimental-native-standard-act1-map-v1",
                "nodes": nodes, "entry_node_ids": ["1:1:3"]}},
            "native_map_rng": {"streams": [{
                "stream_id": MAP_STREAM, "codec": MAP_CODEC,
                "call_count": 128, "state_bytes": encoded,
            }]},
        },
    }
    return audit, snapshot


class NativeMapFidelityTests(unittest.TestCase):
    def test_source_graph_and_cursor_pass_structural_validation(self):
        source, emulator = example()
        self.assertEqual([], validate_native_map(
            source["source_initial_map_snapshot"]))
        result = compare_native_map(source, emulator)
        self.assertEqual("SOURCE_MAP_AND_RNG_MATCHED", result["status"])
        self.assertEqual("MATCHED", result["native_map_topology_parity"])
        self.assertEqual("MATCHED", result["native_map_rng_alignment"])
        self.assertEqual("NOT_TESTED", result["whole_run_game_parity"])
        self.assertIsNone(result["first_difference"])

    def test_first_room_type_difference_reports_precise_coordinate(self):
        source, emulator = example()
        emulator["state"]["world"]["map"]["nodes"][1]["room_type"] = 3
        result = compare_native_map(source, emulator)
        self.assertEqual("SOURCE_MAP_OR_RNG_DIVERGENCE", result["status"])
        self.assertEqual("DIVERGED", result["native_map_topology_parity"])
        self.assertEqual("MATCHED", result["native_map_rng_alignment"])
        self.assertEqual("$.map.nodes[2:3].room_type",
                         result["first_difference"]["path"])

    def test_geometry_divergence_is_exposed_before_rng_difference(self):
        source, emulator = example()
        emulator["state"]["world"]["map"]["nodes"][1]["next_node_ids"] = []
        emulator["state"]["native_map_rng"]["streams"][0]["call_count"] += 1
        result = compare_native_map(source, emulator)
        self.assertEqual("$.map.nodes[2:3].children",
                         result["first_difference"]["path"])
        self.assertEqual("DIVERGED", result["native_map_rng_alignment"])

    def test_cursor_difference_is_reported_when_map_matches(self):
        source, emulator = example()
        emulator["state"]["native_map_rng"]["streams"][0]["call_count"] += 1
        result = compare_native_map(source, emulator)
        self.assertEqual("SOURCE_MAP_OR_RNG_DIVERGENCE", result["status"])
        self.assertEqual("MATCHED", result["native_map_topology_parity"])
        self.assertEqual("DIVERGED", result["native_map_rng_alignment"])
        self.assertEqual("$.native_map_rng.counter",
                         result["first_difference"]["path"])

    def test_bad_native_graph_and_source_provenance_block_comparison(self):
        source, emulator = example()
        source["source_initial_map_snapshot"]["points"][0][
            "children"][0]["row"] = 8
        result = compare_native_map(source, emulator)
        self.assertEqual("BLOCKED_INVALID_SOURCE_MAP", result["status"])
        self.assertEqual("NOT_TESTED", result["native_map_rng_alignment"])
        source, emulator = example()
        source["source_initial_map_snapshot"][
            "source_rng_consumed_by_inspector"] = True
        self.assertTrue(validate_native_map(
            source["source_initial_map_snapshot"]))

    def test_missing_native_map_or_uncertified_audit_blocks(self):
        source, emulator = example()
        source["source_initial_map_snapshot"] = None
        self.assertEqual("BLOCKED_SOURCE_MAP_SNAPSHOT_MISSING",
                         compare_native_map(source, emulator)["status"])
        source, emulator = example()
        source["status"] = "INVALID_PRELUDE"
        self.assertEqual("BLOCKED_UNAUDITED_MAP_SOURCE",
                         compare_native_map(source, emulator)["status"])

    def test_wrong_run_seed_or_rng_codec_blocks(self):
        source, emulator = example()
        emulator["state"]["run_seed"] = "other"
        self.assertEqual("BLOCKED_EMULATOR_IDENTITY",
                         compare_native_map(source, emulator)["status"])
        source, emulator = example()
        emulator["state"]["native_map_rng"]["streams"][0]["codec"] = "fake"
        self.assertEqual("BLOCKED_INVALID_EMULATOR_MAP",
                         compare_native_map(source, emulator)["status"])

    def test_native_map_projection_cannot_hide_duplicates(self):
        source, _ = example()
        native = source["source_initial_map_snapshot"]
        native["points"].append(copy.deepcopy(native["points"][2]))
        errors = validate_native_map(native)
        self.assertTrue(any("Duplicate" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
