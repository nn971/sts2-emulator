"""M1 acceptance source pairing and no-emulator fail-closed contracts."""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_live_m1_acceptance import (
    AcceptanceError, acceptance_result, discover_pair, inspect_pair)


def sample(directory: Path):
    sidecar = directory / "passive-synthetic.actions.jsonl"
    prelude = directory / "run-prelude-synthetic.jsonl"
    source = [
        {"type": "header", "ordinal": 1, "data": {
            "run_id": "native-session-a",
            "game_build": "synthetic-v111"}},
        {"type": "native_run_identity", "ordinal": 2, "data": {
            "seed": "synthetic-seed", "ascension": 10}},
        {"type": "native_turn_checkpoint", "ordinal": 3, "data": {
            "kind": "PLAYER_TURN_START", "turn": 1}},
        {"type": "terminal", "ordinal": 4, "data": {"status": "OBSERVED"}},
    ]
    startup = [
        {"type": "run_started", "ordinal": 1, "data": {
            "game_build": "synthetic-v111",
            "seed": "synthetic-seed", "ascension": 10}},
        {"type": "combat_link", "ordinal": 2, "data": {
            "combat_run_id": "native-session-a"}},
        {"type": "terminal", "ordinal": 3, "data": {
            "status": "SOURCE_PRELUDE_OBSERVED"}},
    ]
    def write(path, rows):
        path.write_text("".join(json.dumps(row) + "\n" for row in rows),
                        encoding="utf-8")
    write(sidecar, source)
    write(prelude, startup)
    return sidecar, prelude, source, startup, write


class M1AcceptanceTests(unittest.TestCase):
    def test_pair_by_native_run_identity_and_reject_mismatch(self):
        with tempfile.TemporaryDirectory() as tmp:
            sidecar, prelude, source, startup, write = sample(Path(tmp))
            self.assertEqual("RUN_ID_BUILD_SEED_ASCENSION_MATCHED",
                             inspect_pair(sidecar, prelude)["native_pairing"])
            self.assertEqual((sidecar, prelude), discover_pair(Path(tmp)))
            startup[1]["data"]["combat_run_id"] = "another-run"
            write(prelude, startup)
            with self.assertRaisesRegex(AcceptanceError, "identities disagree"):
                inspect_pair(sidecar, prelude)

    def test_missing_ordinal_and_incomplete_terminal_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            sidecar, prelude, source, startup, write = sample(Path(tmp))
            source[2]["ordinal"] = 5
            write(sidecar, source)
            with self.assertRaisesRegex(AcceptanceError, "noncontiguous"):
                inspect_pair(sidecar, prelude)
            source[2]["ordinal"] = 3
            source[-1]["data"]["status"] = "INCOMPLETE_CAPTURE"
            write(sidecar, source)
            with self.assertRaisesRegex(AcceptanceError, "did not finish"):
                inspect_pair(sidecar, prelude)

    def test_missing_emulator_executable_reports_blocker_without_replay(self):
        with tempfile.TemporaryDirectory() as tmp:
            sidecar, prelude, _, _, _ = sample(Path(tmp))
            report = acceptance_result(
                sidecar, prelude, dotnet="dotnet",
                cli_dll=Path(tmp) / "no-emulator.dll",
                coordinator_report=Path(tmp) / "unused.json",
                min_player_turns=6)
            self.assertEqual("BLOCKED_EMULATOR_EXECUTABLE_UNAVAILABLE",
                             report["status"])
            self.assertFalse(report["native_settlement_certified"])
            self.assertEqual(1, report["player_turn_checkpoint_candidates"])
            self.assertFalse((Path(tmp) / "unused.json").exists())


if __name__ == "__main__":
    unittest.main()
