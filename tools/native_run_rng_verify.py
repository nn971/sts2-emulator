#!/usr/bin/env python3
"""Pure native-v111 run/player RNG checkpoint reconstruction (not emulator parity).

Pinned source: RunRngSet, PlayerRngSet, Player.InitializeSeed,
StringHelper.SnakeCase, Rng and MegaRandom@706ef1c9.
"""
from __future__ import annotations

import re

from native_neow_rng_verify import MASK, _splitmix, next_xoshiro, xxhash64

RUN_NAMES = (
    "UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration",
    "CombatPotionGeneration", "CombatCardSelection", "CombatEnergyCosts",
    "CombatTargets", "MonsterAi", "Niche", "CombatOrbs", "TreasureRoomRelics")
PLAYER_NAMES = ("Rewards", "Shops", "Transformations")
_ROSTER = {("run", x) for x in RUN_NAMES} | {
    ("player", x) for x in PLAYER_NAMES}


def native_stream_state(seed: int, name: str, counter: int) -> dict:
    if (type(seed) is not int or seed < 0 or seed > MASK
        or type(counter) is not int or counter < 0 or counter > 100000
        or not isinstance(name, str) or not name):
        raise ValueError("Invalid source RNG stream input")
    snake = re.sub(r"([A-Za-z0-9])([A-Z])", r"\1_\2", name).lower()
    stream_seed = (seed + xxhash64(snake.encode("utf-8"))) & MASK
    words = []
    for _ in range(4):
        stream_seed, word = _splitmix(stream_seed)
        words.append(word)
    state = tuple(words)
    for _ in range(counter):
        state = next_xoshiro(state)
    return {"counter": counter, **{
        "s" + str(i): x for i, x in enumerate(state)}}


def verify_run_streams(run_seed: str, snapshots: list[dict]) -> dict:
    report = {
        "schema": "native-v111-source-stream-reconstruction-v1",
        "status": "SOURCE_RNG_CHECKPOINTS_RECONSTRUCTED",
        "checked_checkpoints": 0, "checked_stream_states": 0,
        "first_difference": None,
        "native_emulator_rng_parity": "NOT_TESTED",
        "emulator_map_rng_alignment": "NOT_TESTED",
    }
    def fail(path, reason, actual=None, expected=None):
        report["status"] = "SOURCE_RNG_CHECKPOINT_DIFFERENCE"
        report["first_difference"] = {
            "path": path, "reason": reason,
            "native": actual, "reconstructed": expected}
        return report

    if (not isinstance(run_seed, str) or not run_seed
        or run_seed.startswith("old")
        or not isinstance(snapshots, list) or not snapshots):
        return fail("$", "INVALID_OR_UNSUPPORTED_SEED_OR_SNAPSHOTS")
    base = xxhash64(run_seed.encode("utf-8"))
    for i, checkpoint in enumerate(snapshots):
        path = f"$.source_run_rng_snapshots[{i}]"
        if not isinstance(checkpoint, dict):
            return fail(path, "NOT_AN_OBJECT")
        site = checkpoint.get("site")
        if site not in ("run_started", "room_entered", "combat_attach"):
            return fail(path + ".site", "UNSUPPORTED_SOURCE_SITE", site)
        if i == 0 and site != "run_started":
            return fail(path + ".site", "RUN_START_CHECKPOINT_MISSING", site)
        observed_seed = checkpoint.get("player_rng_seed")
        if type(observed_seed) is not int or observed_seed != base:
            return fail(path + ".player_rng_seed", "PLAYER_SEED_MISMATCH",
                        observed_seed, base)
        streams = checkpoint.get("streams")
        if not isinstance(streams, list) or len(streams) != len(_ROSTER):
            return fail(path + ".streams", "STREAM_ROSTER_INCOMPLETE")
        if (not all(isinstance(x, dict) for x in streams)
            or {(x.get("scope"), x.get("stream_id")) for x in streams} != _ROSTER):
            return fail(path + ".streams", "STREAM_ROSTER_MISMATCH")
        for n, stream in enumerate(streams):
            p = path + f".streams[{n}]"
            try:
                expected = native_stream_state(
                    base, stream["stream_id"], stream.get("counter"))
            except (TypeError, ValueError, KeyError) as exc:
                return fail(p, "BAD_SOURCE_RNG_COUNTER", str(exc))
            for key in ("counter", "s0", "s1", "s2", "s3"):
                if type(stream.get(key)) is not int or stream[key] != expected[key]:
                    return fail(p + "." + key, "NATIVE_STREAM_WORD_DIFFERENCE",
                                stream.get(key), expected[key])
            report["checked_stream_states"] += 1
        report["checked_checkpoints"] += 1
    return report
