"""Fail-closed native SSE evidence-stream and offline producer contracts."""
from __future__ import annotations

import copy
import tempfile
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_live_offline_replay import (
    LiveEventError, _digest, _LexicalFloat, _dump_json, load_live,
    recover_sidecar, simulate_sidecar,
)


def fixture():
    return [
        {
            "type": "header", "ordinal": 1,
            "data": {"run_id": "synthetic-native-combat",
                     "schema": "native-action-envelope-v1",
                     "game_build": "synthetic"},
        },
        {
            "type": "native_turn_checkpoint", "ordinal": 2,
            "data": {"kind": "PLAYER_TURN_START", "turn": 1},
            "state": {"combat": {"hand": [42], "draw_pile": [9]}},
        },
        {
            "type": "terminal", "ordinal": 3,
            "data": {"status": "OBSERVED"},
        },
    ]


def status(events):
    try:
        recover_sidecar(events)
    except LiveEventError as error:
        return error.status
    raise AssertionError("Expected fail-closed stream rejection")


class NativeLiveOfflineReplayTests(unittest.TestCase):
    def test_round_trip_preserves_source_records_without_snapshot_injection(self):
        native = fixture()
        events = simulate_sidecar(native)
        self.assertEqual(native, recover_sidecar(events))
        self.assertEqual(
            ["session_started", "header", "native_turn_checkpoint", "terminal"],
            [e["kind"] for e in events])
        self.assertEqual(list(range(1, 5)),
                         [e["sequence"] for e in events])
        self.assertTrue(all(e["channel"] == "fidelity" for e in events))
        self.assertTrue(all(len(e["content_fingerprint"]) == 64 for e in events))

    def test_identical_retransmission_is_idempotent(self):
        events = simulate_sidecar(fixture())
        self.assertEqual(fixture(), recover_sidecar([
            events[0], events[1], events[1], events[2], events[3], events[2]]))

    def test_conflicting_duplicate_refused(self):
        events = simulate_sidecar(fixture())
        bad = copy.deepcopy(events[1])
        bad["payload"]["type"] = "changed"
        self.assertEqual("CONFLICTING_DUPLICATE_EVENT",
                         status([events[0], events[1], bad]))

    def test_missing_event_refused_before_replay(self):
        events = simulate_sidecar(fixture())
        self.assertEqual("MISSING_LIVE_EVENT", status([events[0], events[2]]))

    def test_bad_hash_refused_before_replay(self):
        events = simulate_sidecar(fixture())
        events[2]["payload"]["state"]["combat"]["hand"] = [91]
        self.assertEqual("LIVE_FINGERPRINT_MISMATCH", status(events))

    def test_session_change_and_stale_source_ordinal_refused(self):
        events = simulate_sidecar(fixture())
        bad = copy.deepcopy(events)
        bad[-1]["session_epoch"] = 2
        self.assertEqual("LIVE_SESSION_CHANGED", status(bad))
        bad = copy.deepcopy(events)
        bad[-1]["source_ordinal"] = 2
        self.assertEqual("LIVE_SOURCE_ORDINAL_MISMATCH", status(bad))

    def test_public_or_unfinished_source_cannot_enter_fidelity_replay(self):
        events = simulate_sidecar(fixture())
        bad = copy.deepcopy(events)
        bad[-1]["channel"] = "public"
        self.assertEqual("INVALID_LIVE_ENVELOPE", status(bad))
        self.assertEqual("INCOMPLETE_LIVE_CAPTURE", status(events[:-1]))

    def test_native_scientific_float_hash_matches_csharp_and_archive(self):
        raw = ('{"type":"native_rng_scalar_boundary","ordinal":371,'
               '"data":{"selected_float":-5.0546736E-05,"label":"<native>&"}}')
        row = json.loads(raw, parse_float=_LexicalFloat)
        self.assertEqual(
            "1984f86719ddf531392b0e39b369f29ec5fa55b93d11646177c8526ab409b072",
            _digest(row))
        self.assertIn("-5.0546736E-05", _dump_json(row, sort_keys=False))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "native.fidelity.jsonl"
            path.write_text(_dump_json(row, sort_keys=False) + "\\n",
                            encoding="utf-8")
            recovered = load_live(path)
            self.assertEqual(_digest(row), _digest(recovered[0]))

    def test_canonical_hash_ignores_json_object_key_order(self):
        self.assertEqual(_digest({"b": [1, 2], "a": {"z": True, "q": None}}),
                         _digest({"a": {"q": None, "z": True}, "b": [1, 2]}))


if __name__ == "__main__":
    unittest.main()
