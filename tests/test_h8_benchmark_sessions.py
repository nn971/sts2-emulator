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

    def test_replay_audit_uses_global_seed_index_across_shards(self):
        indexed = list(enumerate(f"source-{i:03d}" for i in range(128)))
        shards = split_shards(indexed, workers=15)
        selected = [
            seed for shard in shards for source_index, seed in shard
            if source_index % 8 == 0
        ]
        self.assertEqual(len(selected), 16)
        self.assertEqual(
            set(selected),
            {f"source-{i:03d}" for i in range(0, 128, 8)})

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
            trace_digest="trace", journal_digest="journal",
            mechanics_sha256="mechanics")
        good = dict(source_digest="source", trajectories=dict(s=episode))
        validate_pair(good, good)
        bad = dict(source_digest="source",
                   trajectories=dict(s={**episode, "journal_digest": "other"}))
        with self.assertRaisesRegex(RuntimeError, "journal_digest"):
            validate_pair(good, bad)
        bad_state = dict(
            source_digest="source",
            trajectories=dict(
                s={**episode, "mechanics_sha256": "mismatched"}))
        with self.assertRaisesRegex(RuntimeError, "mechanics_sha256"):
            validate_pair(good, bad_state)


if __name__ == "__main__":
    unittest.main()
