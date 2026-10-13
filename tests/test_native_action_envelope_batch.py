"""Corpus-level coverage aggregation for sidecar capture, without parity claims."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_action_envelope_batch import collect
from test_native_action_envelope_audit import sample


class SidecarCorpusTests(unittest.TestCase):
    def test_aggregates_observed_mechanics_without_claiming_parity(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)
            first = path / "passive-a.actions.jsonl"
            first.write_text("\n".join(json.dumps(x) for x in sample()) + "\n",
                             encoding="utf-8")
            result = collect(path)
            self.assertEqual("CALLBACK_CORPUS_CONSISTENT", result["status"])
            self.assertEqual(1, result["files_checked"])
            self.assertEqual(1, result["mechanics"]["card_play"]["observations"])
            self.assertEqual("UNOBSERVED",
                             result["mechanics"]["potion_history_entry"]["status"])
            self.assertEqual("NOT_TESTED", result["native_game_parity"])
            self.assertEqual("NOT_TESTED", result["exact_rng_parity"])

    def test_reports_sidecar_gaps_instead_of_hiding_them(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)
            broken = sample()
            broken[-1]["data"]["open_actions"] = [1]
            (path / "passive-b.actions.jsonl").write_text(
                "\n".join(json.dumps(x) for x in broken) + "\n",
                encoding="utf-8")
            result = collect(path)
            self.assertEqual("CAPTURE_GAPS_REQUIRE_REVIEW", result["status"])
            self.assertEqual("INCOMPLETE_CAPTURE", result["traces"][0]["status"])

    def test_empty_directory_has_no_coverage(self):
        with tempfile.TemporaryDirectory() as temp:
            result = collect(Path(temp))
            self.assertEqual("NO_CAPTURES", result["status"])
            self.assertEqual("UNOBSERVED", result["mechanics"]["card_play"]["status"])


if __name__ == "__main__":
    unittest.main()
