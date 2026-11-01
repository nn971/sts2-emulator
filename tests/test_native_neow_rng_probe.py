"""Offline strict checks for native Neow source RNG hypothesis comparison."""
import copy
import sys
import unittest
from unittest.mock import patch
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_neow_rng_probe_audit import compare, has_pinned_native_build
from native_run_bootstrap import run_bootstrap
from test_native_run_bootstrap import fixture, FakeSession
from test_native_run_rng_verify import source_fixture

_IDS = [
    "proto.native.neow.arcanescroll",
    "proto.native.neow.wingedboots",
    "proto.native.neow.heftytablet",
]


def probe():
    variants = []
    for kaleido in (False, True):
        for boxes in (False, True):
            # The two single-enabled hypotheses share an RNG cursor, but
            # may produce different ordered options due to shuffled lists.
            allowed = int(kaleido) + int(boxes)
            counter = 15 + allowed
            words = [counter * 100 + i for i in range(4)]
            offers = _IDS if (kaleido and not boxes) else [
                "proto.native.neow." + str(allowed),
                "proto.native.neow.wingedboots",
                "proto.native.neow.heftytablet",
            ]
            variants.append({
                "kaleidoscopeAllowed": kaleido,
                "scrollBoxesAllowed": boxes,
                "offeredRelicIds": offers,
                "eventRng": dict(zip(("state0", "state1", "state2", "state3"),
                                     words), counter=counter),
                "positivePoolSize": counter - 3,
                "cursedRelicId": "proto.native.neow.heftytablet",
            })
    return {
        "schemaId": "sts2-v111-native-neow-rng-probe-v1",
        "source": "Flaaax/STS2-source@706ef1c9",
        "eventId": "NEOW", "assumedPlayerSlot": 0,
        "modifiers": "NONE_ASSUMED",
        "nativeRngConsumedByInspector": False,
        "emulatorEventRngAlignment": "NOT_TESTED",
        "dynamicUnlockGates": ["Kaleidoscope", "ScrollBoxes"],
        "predictions": variants,
    }


def native_snapshot():
    rng = probe()["predictions"][2]["eventRng"]
    return {
        "counter": rng["counter"], "s0": rng["state0"],
        "s1": rng["state1"], "s2": rng["state2"], "s3": rng["state3"]
    }


class NativeNeowProbeTests(unittest.TestCase):
    def test_matches_source_offer_and_exact_postoffer_state(self):
        report = compare(_IDS, native_snapshot(), probe())
        self.assertEqual("SOURCE_NEOW_EVENT_RNG_AND_OFFERS_RECOMPUTED",
                         report["status"])
        self.assertEqual("MATCHED", report["source_rng_recomputation"])
        self.assertEqual({"kaleidoscope_allowed": True,
                          "scroll_boxes_allowed": False,
                          "unlock_provenance":
                          "HYPOTHESIS_CONSISTENT_NOT_INDEPENDENTLY_OBSERVED"},
                         report["matching_hypothesis"])
        self.assertEqual("NOT_TESTED", report["emulator_event_rng_alignment"])

    def test_wrong_source_rng_word_reports_first_cursor_component(self):
        source = native_snapshot()
        source["s2"] += 1
        report = compare(_IDS, source, probe())
        self.assertEqual("SOURCE_NEOW_EVENT_RNG_STATE_DIFFERENCE",
                         report["status"])
        self.assertEqual("$.event_rng.s2", report["first_difference"]["path"])

    def test_wrong_offer_at_same_cursor_reports_exact_option(self):
        ids = _IDS.copy()
        ids[0] = "proto.native.neow.goldenpearl"
        report = compare(ids, native_snapshot(), probe())
        self.assertEqual("SOURCE_NEOW_OFFER_DIFFERENCE_AT_SAME_RNG_CURSOR",
                         report["status"])
        self.assertEqual("$.neow.options[0]", report["first_difference"]["path"])

    def test_invalid_or_spoofed_probe_fails_closed(self):
        x = probe()
        x["dynamicUnlockGates"] = ["Unknown"]
        self.assertEqual("INVALID_NATIVE_NEOW_RNG_PROBE",
                         compare(_IDS, native_snapshot(), x)["status"])
        x = probe()
        x["predictions"][3]["kaleidoscopeAllowed"] = False
        x["predictions"][3]["scrollBoxesAllowed"] = True
        self.assertEqual("INVALID_NATIVE_NEOW_RNG_PROBE",
                         compare(_IDS, native_snapshot(), x)["status"])
        x = probe()
        x["predictions"][0]["eventRng"]["state0"] = 2**64
        self.assertEqual("INVALID_NATIVE_NEOW_RNG_PROBE",
                         compare(_IDS, native_snapshot(), x)["status"])

    def test_genuine_native_build_requires_probe_before_emulator_reset(self):
        native = fixture()
        native["run_identity"]["game_build"] = (
            "assembly=1.1.1.1;mvid=" + "a" * 32 + ";sha256=" + "b" * 64)
        self.assertTrue(has_pinned_native_build(native["run_identity"]))
        native["source_run_rng_snapshots"] = source_fixture()
        class Stub(FakeSession):
            def call(self, op, **kwargs):
                self.last_call = (op, kwargs)
                return probe()
        s = Stub()
        # The synthetic fixture event RNG does not match the source
        # reconstruction; no emulator start/Neow action is attempted.
        # This test isolates the Neow probe stage. The preceding named
        # run/player stream gate is covered by its own strict tests.
        with patch("native_run_bootstrap.verify_run_streams",
                   return_value={"status": "SOURCE_RNG_CHECKPOINTS_RECONSTRUCTED",
                                 "first_difference": None}):
            result, snap = run_bootstrap(native, s)
        self.assertEqual("SOURCE_NEOW_NATIVE_RNG_DIVERGENCE", result["status"])
        self.assertEqual(0, s.reset_count)
        self.assertIsNone(snap)
        self.assertEqual("native_neow_rng_probe", s.last_call[0])

    def test_pinned_native_build_requires_real_named_streams_first(self):
        native = fixture()
        native["run_identity"]["game_build"] = (
            "assembly=1.1.1.1;mvid=" + "a" * 32 + ";sha256=" + "b" * 64)
        session = FakeSession()
        result, snapshot = run_bootstrap(native, session)
        self.assertEqual("BLOCKED_NATIVE_NAMED_RNG_RECONSTRUCTION",
                         result["status"])
        self.assertIsNone(snapshot)
        self.assertEqual(0, session.reset_count)

    def test_unrecognized_build_never_skips_native_rng_gate(self):
        native = fixture()
        native["run_identity"]["game_build"] = "some-unknown-build"
        s = FakeSession()
        result, snap = run_bootstrap(native, s)
        self.assertEqual("BLOCKED_UNRECOGNIZED_SOURCE_BUILD", result["status"])
        self.assertEqual(0, s.reset_count)
        self.assertIsNone(snap)


if __name__ == "__main__":
    unittest.main()
