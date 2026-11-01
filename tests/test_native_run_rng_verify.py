"""Native source run/player stream hash, state and counter regression."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_neow_rng_verify import xxhash64
from native_run_rng_verify import (
    RUN_NAMES, PLAYER_NAMES, native_stream_state, verify_run_streams)


def source_fixture(seed="native-stream-fixture"):
    base = xxhash64(seed.encode())
    streams = [
        {"scope": scope, "stream_id": name,
         **native_stream_state(base, name, 3)}
        for scope, names in (("run", RUN_NAMES), ("player", PLAYER_NAMES))
        for name in names
    ]
    return [{"site": "run_started", "player_rng_seed": base,
             "streams": streams}]


class NativeRunStreamVerifyTests(unittest.TestCase):
    def test_all_native_named_streams_reconstructed(self):
        r = verify_run_streams("native-stream-fixture", source_fixture())
        self.assertEqual("SOURCE_RNG_CHECKPOINTS_RECONSTRUCTED", r["status"])
        self.assertEqual(15, r["checked_stream_states"])
        self.assertEqual("NOT_TESTED", r["emulator_map_rng_alignment"])

    def test_csharp_native_codec_named_stream_golden_vector(self):
        # Independent C# NativeRngV111Tests.cs reference.
        seed = xxhash64(b"v111-codec-golden")
        words = native_stream_state(seed, "CombatTargets", 0)
        self.assertEqual([
            10404991001613190346, 2163311314968115413,
            2138220657641300634, 1763551566488335709],
            [words[f"s{i}"] for i in range(4)])

    def test_first_changed_run_word_is_reported(self):
        s = source_fixture()
        s[0]["streams"][0]["s3"] ^= 1
        r = verify_run_streams("native-stream-fixture", s)
        self.assertEqual("SOURCE_RNG_CHECKPOINT_DIFFERENCE", r["status"])
        self.assertEqual("$.source_run_rng_snapshots[0].streams[0].s3",
                         r["first_difference"]["path"])

    def test_multiple_source_checkpoints_are_reconstructed(self):
        snapshots = source_fixture()
        second = source_fixture()[0]
        second["site"] = "combat_attach"
        snapshots.append(second)
        report = verify_run_streams("native-stream-fixture", snapshots)
        self.assertEqual("SOURCE_RNG_CHECKPOINTS_RECONSTRUCTED", report["status"])
        self.assertEqual(2, report["checked_checkpoints"])
        self.assertEqual(30, report["checked_stream_states"])

    def test_player_seed_mismatch_is_reported(self):
        s = source_fixture()
        s[0]["player_rng_seed"] ^= 1
        r = verify_run_streams("native-stream-fixture", s)
        self.assertEqual("PLAYER_SEED_MISMATCH",
                         r["first_difference"]["reason"])

    def test_missing_stream_or_legacy_seed_fails_closed(self):
        s = source_fixture()
        s[0]["streams"].pop()
        self.assertEqual("SOURCE_RNG_CHECKPOINT_DIFFERENCE",
                         verify_run_streams("native-stream-fixture", s)["status"])
        self.assertEqual("SOURCE_RNG_CHECKPOINT_DIFFERENCE",
                         verify_run_streams("oldlegacy", source_fixture())["status"])


if __name__ == "__main__":
    unittest.main()
