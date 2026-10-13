"""Mechanic registry presence and native-fidelity testability stay distinct."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_fidelity_testability_matrix import matrix


def row(kind, name, implementation="implemented",
        integration="integrated", validation="unverified",
        missing=None, scope="single-player"):
    return {"kind": kind, "native_id": name, "scope": scope,
            "engine_ids": ["proto." + name.lower()],
            "implementation": implementation, "integration": integration,
            "validation": validation,
            "missing_variants": missing or [], "missing_branches": []}


def coverage(rows):
    return {"schema": "sts2-v111-coverage-v1",
            "game_build": "v0.111.0", "inventory_hash": "synthetic",
            "items": rows}


class NativeFidelityTestabilityMatrixTests(unittest.TestCase):
    def test_implemented_cards_have_only_conditional_checkpoint_import(self):
        report = matrix(coverage([
            row("cards", "STRIKE_SILENT", missing=["upgraded"]),
            row("relics", "SOMETHING"),
            row("events", "MYSTERY", integration="missing"),
            row("cards", "CROSS_CHARACTER", scope="multiplayer-only")]))
        self.assertEqual("CONDITIONAL_VISIBLE_CHECKPOINT_IMPORT_ONLY",
                         report["items"][0]["testability"])
        self.assertEqual("NATIVE_DIFFERENTIAL_INTERFACE_NOT_ESTABLISHED",
                         report["items"][1]["testability"])
        self.assertEqual("NOT_INTEGRATED_IN_GAME_PATH",
                         report["items"][2]["testability"])
        self.assertEqual("OUT_OF_SINGLE_PLAYER_SCOPE",
                         report["items"][3]["testability"])
        self.assertEqual(1, report["by_kind"]["cards"]["missing_variants_and_branches"])
        self.assertFalse(report["mechanics_exhaustively_testable_now"])
        self.assertEqual("NOT_TESTED",
                         report["all_native_rng_streams_match_game_mechanics"])

    def test_static_baseline_envelope_and_corpus_are_qualified(self):
        source = {"report": coverage([
            row("encounters", "TEST"),
            row("cards", "MISSING", implementation="partial")])}
        corpus = {"status": "CALLBACK_CORPUS_CONSISTENT",
                  "native_rng_checkpoint_count": 2,
                  "native_rng_source_calls": 5}
        result = matrix(source, corpus, "v111-baseline.json")
        self.assertEqual("v111-baseline.json", result["inventory_source"])
        self.assertEqual(1, result["implemented_integrated_items"])
        self.assertEqual(2, result["native_rng_checkpoint_count"])
        self.assertEqual("IMPLEMENTATION_INCOMPLETE",
                         result["items"][1]["testability"])

    def test_rejects_invalid_coverage_or_empty_manifest(self):
        with self.assertRaises(ValueError):
            matrix({"schema": "wrong", "items": [row("cards", "C")]})
        with self.assertRaises(ValueError):
            matrix(coverage([]))


if __name__ == "__main__":
    unittest.main()
