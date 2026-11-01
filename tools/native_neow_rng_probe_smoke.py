#!/usr/bin/env python3
"""Pure native Neow RNG diagnostic over a real emulator JSONL process.

This proves API, deterministic source reconstruction and nonmutation;
the installed native game is NOT involved.
"""
from __future__ import annotations
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))
from sts2_emulator import Session
from native_neow_rng_probe_audit import compare
from native_neow_rng_verify import verify_neow_event_rng


def main():
    command = ["dotnet", str(ROOT / "src/Sts2Emulator.Cli" /
                            "bin/Release/net9.0/Sts2Emulator.Cli.dll"),
               "prototype-ai-jsonl"]
    with Session(command) as session:
        state = session.reset("native-neow-probe-smoke-v1", ascension=10)
        before = state.exact_hash()
        result = session.call("native_neow_rng_probe",
                              seed="native-neow-probe-smoke-v1")
        repeat = session.call("native_neow_rng_probe",
                              seed="native-neow-probe-smoke-v1")
        assert result["schemaId"] == "sts2-v111-native-neow-rng-probe-v1"
        assert result["predictions"] == repeat["predictions"]
        assert len(result["predictions"]) == 4
        assert len({
            tuple(p["eventRng"].values()) for p in result["predictions"]
        }) == 3, "Shared call-count hypotheses must share the exact RNG state"
        assert state.exact_hash() == before
        for prediction in result["predictions"]:
            words = prediction["eventRng"]
            observed = {"counter": words["counter"],
                        **{"s" + str(i): words["state" + str(i)]
                           for i in range(4)}}
            independent = verify_neow_event_rng(
                "native-neow-probe-smoke-v1", observed)
            assert independent["status"] == "SOURCE_EVENT_RNG_RECONSTRUCTED", (
                "C# and independently implemented Python xoshiro disagree: "
                + repr(independent))
        chosen = result["predictions"][0]
        state_rng = chosen["eventRng"]
        source_rng = {"counter": state_rng["counter"],
                      **{"s" + str(i): state_rng["state" + str(i)]
                         for i in range(4)}}
        match = compare(chosen["offeredRelicIds"], source_rng, result)
        assert match["source_rng_recomputation"] == "MATCHED", match
        assert match["emulator_event_rng_alignment"] == "NOT_TESTED"
        # Recorded source preselection Neow state from pinned v111
        # Silent A10 run 6JK8MD0VQQTA (run-prelude 2026-11-01).
        # This verifies source-local Neow reconstruction on a real seed
        # independently of emulator run/Neow action state transitions.
        captured_seed = "6JK8MD0VQQTA"
        captured_ids = [
            "proto.native.neow.phialholster",
            "proto.native.neow.lostcoffer",
            "proto.native.neow.silvercrucible",
        ]
        captured_cursor = {
            "counter": 19,
            "s0": 7953574168331048626,
            "s1": 3395371163224860218,
            "s2": 16899011179517407643,
            "s3": 6951990959699619831,
        }
        replayed = verify_neow_event_rng(captured_seed, captured_cursor)
        assert replayed["status"] == "SOURCE_EVENT_RNG_RECONSTRUCTED", replayed
        pinned = session.call("native_neow_rng_probe", seed=captured_seed)
        captured_comparison = compare(captured_ids, captured_cursor, pinned)
        assert captured_comparison["status"] == (
            "SOURCE_NEOW_EVENT_RNG_AND_OFFERS_RECOMPUTED"), (
                "Pinned native preselection Neow offer/cursor hypothesis "
                "differs: " + json.dumps(captured_comparison, sort_keys=True))
        print(json.dumps({
            "status": "NATIVE_NEOW_RNG_SOURCE_PROBE_SMOKE_PASS",
            "real_emulator_jsonl_process": True,
            "source_data": "C_SHARP_PREDICTION_VERIFIED_BY_INDEPENDENT_PYTHON_CODEC",
            "native_game_parity": "NOT_TESTED",
            "pinned_source_neow_offer_and_rng": "SOURCE_MATCHED_OFFLINE"
        }, sort_keys=True))


if __name__ == "__main__":
    main()
