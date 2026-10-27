"""H7 semantic profiling comparison rejects nonidentical source/model provenance."""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from tools.compare_h7_boundary_perf import STAGES, compare


class H7BoundaryComparisonTests(unittest.TestCase):
    @staticmethod
    def fixtures(root: Path) -> tuple[Path, Path]:
        a, b = root / "before", root / "after"
        for directory, rev, seconds in (
            (a, "baseline-commit", 160.0), (b, "optimized-commit", 140.0)
        ):
            directory.mkdir()
            (directory / "h7.json").write_text(json.dumps({
                "emulator_revision": rev,
                "combats": 16, "player_decisions": 350,
                "elapsed_seconds": seconds,
                "player_decisions_per_second": 350 / seconds,
                "source_digest": "same-source",
                "model_sha256": "same-model",
            }), encoding="utf-8")
            (directory / "native.json").write_text(json.dumps({
                "operations": {
                    key: {"count": 100, "allocatedBytes": 500000,
                          "totalSeconds": 10.0} for key in STAGES
                },
            }), encoding="utf-8")
        return a, b

    def test_matched_profile_prints_change(self):
        with tempfile.TemporaryDirectory() as folder:
            before, after = self.fixtures(Path(folder))
            printed = compare(before, after)
            self.assertIn("Cohort wall", printed)
            self.assertIn("160.000 ->      140.000", printed)
            self.assertIn("sub.semantic_draw_result_chance_sites", printed)

    def test_source_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            before, after = self.fixtures(Path(folder))
            path = after / "h7.json"
            report = json.loads(path.read_text())
            report["source_digest"] = "different-sources"
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "source_digest"):
                compare(before, after)

    def test_hash_count_reduction_is_expected_and_reported(self):
        with tempfile.TemporaryDirectory() as folder:
            before, after = self.fixtures(Path(folder))
            path = after / "native.json"
            report = json.loads(path.read_text())
            name = "sub.semantic_draw_hash_SemanticPilotCombatV111"
            report["operations"][name]["count"] = 82
            path.write_text(json.dumps(report))
            printed = compare(before, after)
            self.assertIn(name + " (calls per draw)", printed)
            self.assertIn("1.000 ->        0.820", printed)

    def test_protocol_stage_mismatch_is_still_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            before, after = self.fixtures(Path(folder))
            path = after / "native.json"
            report = json.loads(path.read_text())
            name = "sub.semantic_resolve_draw_card_transition"
            report["operations"][name]["count"] = 99
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "Counter count differs"):
                compare(before, after)

    def test_draw_count_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            before, after = self.fixtures(Path(folder))
            path = after / "native.json"
            report = json.loads(path.read_text())
            report["operations"]["op.semantic_resolve_draw_card"]["count"] = 101
            path.write_text(json.dumps(report))
            with self.assertRaisesRegex(ValueError, "draw counts differ"):
                compare(before, after)


if __name__ == "__main__":
    unittest.main()
