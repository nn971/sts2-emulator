#!/usr/bin/env python3
"""Standalone portable smoke tests for exact native RNG stream auditing."""
import json
import tempfile
from pathlib import Path

from native_rng_stream_audit import (
    PLAYER_STREAMS, RUN_STREAMS, MASK, advance, check_trace, initial_state,
    stream_name, xxhash64,
)


def serialized(seed, names, offsets=None):
    offsets = offsets or {}
    values = {}
    for name in names:
        count = offsets.get(name, 0)
        state = initial_state(
            (seed + xxhash64(stream_name(name))) & MASK)
        for _ in range(count):
            state = advance(state)
        values[name] = {"counter": count, **{
            "state" + str(i): value for i, value in enumerate(state)
        }}
    return {"rngs": values}


def snapshot(seq, offsets_run=None, offsets_player=None):
    run_seed = xxhash64("rng-smoke")
    run = serialized(run_seed, RUN_STREAMS, offsets_run)
    run["seed"] = "rng-smoke"
    player = serialized(run_seed, PLAYER_STREAMS, offsets_player)
    player["seed"] = run_seed
    return {
        "type": "boundary", "sequence": seq,
        "recorder_combat_index": 1,
        "boundary": "combat_manager.TurnStarted",
        "state": {
            "run_rng": run,
            "players": [{"player_rng": player}],
        },
    }


def main():
    assert xxhash64("") == 0xEF46DB3751D8E999
    assert xxhash64("abc") == 0x44BC2CF5AD770999
    # Actual A10 capture's string seed/hash pair. No private game
    # content, game state or trace data is stored in this test.
    assert xxhash64("WTAQ7JGMPLCH") == 12110187181724548864

    header = {"type": "session", "schema": "sts2-reference-probe-v4",
              "expected_game_version": "v0.111.0",
              "expected_game_commit": "41cef1ea"}
    records = [
        header,
        snapshot(1, {"Shuffle": 12, "UpFront": 421}, {"Rewards": 3}),
        snapshot(2, {"Shuffle": 14, "UpFront": 421}, {"Rewards": 4}),
    ]
    with tempfile.TemporaryDirectory() as temp:
        path = Path(temp) / "trace.jsonl"

        def write():
            path.write_text(
                "\n".join(json.dumps(value) for value in records) + "\n",
                encoding="utf-8",
            )
        write()
        good = check_trace(path)
        assert good["passed"], good["errors"]
        assert good["counts"] == {
            "boundaries_with_rng": 2, "stream_snapshots": 30,
            "verified_transitions": 15, "xoshiro_steps": 441
        }, good["counts"]
        assert len(good["streams_checked"]) == 15
        assert good["last_counters"]["run.Shuffle"] == 14

        # A changed xoshiro state without a corresponding counter advance
        # is evidence of a real mismatch, even when every sample is well-formed.
        records[2]["state"]["run_rng"]["rngs"]["Shuffle"]["state0"] ^= 1
        write()
        bad = check_trace(path)
        assert not bad["passed"]
        assert any("run.Shuffle: xoshiro state mismatch" in e
                   for e in bad["errors"]), bad["errors"]

        records[2]["state"]["run_rng"]["rngs"]["Shuffle"]["state0"] ^= 1
        records[2]["state"]["run_rng"]["rngs"].pop("Niche")
        write()
        bad = check_trace(path)
        assert not bad["passed"]
        assert any("inventory mismatch" in e for e in bad["errors"])

    print("PASS: exact named native RNG inventory, xxHash64, "
          "SplitMix64/xoshiro256**, call counters, corruptions")


if __name__ == "__main__":
    main()
