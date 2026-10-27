"""H8 A/B benchmark contracts: balanced shards and strict paired journals."""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from benchmark_h8_sessions import (  # noqa: E402
    _choose, canonical_hash, split_shards, validate_pair,
)


class H8BenchmarkContractTests(unittest.TestCase):
    def test_balanced_seed_shards_preserve_every_seed(self):
        seeds = [f"source-{i:03d}" for i in range(128)]
        shards = split_shards(seeds, workers=15)
        self.assertEqual(len(shards), 15)
        self.assertCountEqual(
            [seed for shard in shards for seed in shard], seeds)
        self.assertEqual(max(map(len, shards)) - min(map(len, shards)), 1)

    def test_scripted_choice_is_same_for_both_arms(self):
        for ordinal in range(30):
            support = [4, 7, 11, 19]
            self.assertEqual(
                _choose("same-seed", ordinal, "draw_card", support),
                _choose("same-seed", ordinal, "draw_card", support))
        self.assertEqual(canonical_hash([1, 2]), canonical_hash([1, 2]))

    def test_comparison_rejects_journal_or_status_drift(self):
        episode = dict(
            status="capped", decisions=2,
            kinds=dict(player=1, draw_card=1),
            trace_digest="trace", journal_digest="journal")
        good = dict(source_digest="source", trajectories=dict(s=episode))
        validate_pair(good, good)
        bad = dict(source_digest="source",
                   trajectories=dict(s={**episode, "journal_digest": "other"}))
        with self.assertRaisesRegex(RuntimeError, "journal_digest"):
            validate_pair(good, bad)


if __name__ == "__main__":
    unittest.main()
