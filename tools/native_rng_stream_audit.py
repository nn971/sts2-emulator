#!/usr/bin/env python3
"""Audit the pinned STS2 v0.111.0 native RNG streams in a passive probe.

Checks the exact 12 RunRngType + 3 PlayerRngType inventories and verifies
each recorded xoshiro256** state against its run/player seed, xxHash64
stream-name offset, SplitMix64 initialization, and reported call count.
This never inspects hidden RNG as a player observation.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

MASK = (1 << 64) - 1
P1, P2, P3, P4, P5 = (
    11400714785074694791, 14029467366897019727,
    1609587929392839161, 9650029242287828579,
    2870177450012600261,
)

RUN_STREAMS = (
    "UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration",
    "CombatPotionGeneration", "CombatCardSelection", "CombatEnergyCosts",
    "CombatTargets", "MonsterAi", "Niche", "CombatOrbs",
    "TreasureRoomRelics",
)
PLAYER_STREAMS = ("Rewards", "Shops", "Transformations")


def rotate(value: int, shift: int) -> int:
    return ((value << shift) | (value >> (64 - shift))) & MASK


def xxhash64(text: str) -> int:
    """Portable xxHash64(seed=0) for the native UTF-8 StringHelper hash."""
    data = text.encode("utf-8")
    size, at = len(data), 0

    def read(length: int) -> int:
        nonlocal at
        number = int.from_bytes(data[at:at + length], "little")
        at += length
        return number

    def round64(acc: int, value: int) -> int:
        acc = (acc + value * P2) & MASK
        acc = rotate(acc, 31)
        return (acc * P1) & MASK

    def merge(acc: int, value: int) -> int:
        acc ^= round64(0, value)
        return (acc * P1 + P4) & MASK

    if size >= 32:
        v1, v2, v3, v4 = (P1 + P2) & MASK, P2, 0, (-P1) & MASK
        while at <= size - 32:
            v1 = round64(v1, read(8))
            v2 = round64(v2, read(8))
            v3 = round64(v3, read(8))
            v4 = round64(v4, read(8))
        result = (rotate(v1, 1) + rotate(v2, 7)
                  + rotate(v3, 12) + rotate(v4, 18)) & MASK
        for v in (v1, v2, v3, v4):
            result = merge(result, v)
    else:
        result = P5
    result = (result + size) & MASK
    while at + 8 <= size:
        value = round64(0, read(8))
        result = (rotate(result ^ value, 27) * P1 + P4) & MASK
    if at + 4 <= size:
        result = (result ^ ((read(4) * P1) & MASK)) & MASK
        result = (rotate(result, 23) * P2 + P3) & MASK
    while at < size:
        result = (result ^ ((read(1) * P5) & MASK)) & MASK
        result = rotate(result, 11) * P1 & MASK
    result ^= result >> 33
    result = result * P2 & MASK
    result ^= result >> 29
    result = result * P3 & MASK
    return (result ^ (result >> 32)) & MASK


def splitmix_next(seed: int) -> tuple[int, int]:
    seed = (seed + 0x9E3779B97F4A7C15) & MASK
    x = seed
    x = ((x ^ (x >> 30)) * 0xBF58476D1CE4E5B9) & MASK
    x = ((x ^ (x >> 27)) * 0x94D049BB133111EB) & MASK
    return seed, (x ^ (x >> 31)) & MASK


def initial_state(seed: int) -> tuple[int, ...]:
    result = []
    for _ in range(4):
        seed, sample = splitmix_next(seed)
        result.append(sample)
    return tuple(result)


def advance(state: tuple[int, ...]) -> tuple[int, ...]:
    a, b, c, d = state
    shift = (b << 17) & MASK
    c ^= a
    d ^= b
    b ^= c
    a ^= d
    c ^= shift
    d = rotate(d, 45)
    return a, b, c, d


def stream_name(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


def check_trace(path: Path, max_errors: int = 20) -> dict:
    errors: list[str] = []
    session = None
    seen: dict[tuple[str, str, str], tuple[int, tuple[int, ...]]] = {}
    totals = {"boundaries_with_rng": 0, "stream_snapshots": 0,
              "verified_transitions": 0, "xoshiro_steps": 0}
    first_counters = {}
    last_counters = {}
    names_checked: set[str] = set()

    def failure(message: str):
        if len(errors) < max_errors:
            errors.append(message)

    for line_number, line in enumerate(path.open(encoding="utf-8"), 1):
        if not line.strip():
            continue
        record = json.loads(line)
        if record.get("type") == "session":
            session = record
            continue
        if record.get("type") != "boundary":
            continue
        state = record.get("state") or {}
        run = state.get("run_rng") or {}
        players = state.get("players") or []
        if not isinstance(run.get("rngs"), dict):
            continue
        totals["boundaries_with_rng"] += 1
        seed_string = run.get("seed")
        if not isinstance(seed_string, str) or seed_string.startswith("old"):
            failure(f"line {line_number}: unsupported or missing current run seed")
            continue
        run_seed = xxhash64(seed_string)
        groups = [("run", run, RUN_STREAMS, run_seed)]
        for slot, player in enumerate(players):
            player_rng = player.get("player_rng") or {}
            if not isinstance(player_rng.get("rngs"), dict):
                continue
            player_seed = player_rng.get("seed")
            if player_seed != ((run_seed + slot) & MASK):
                failure(f"line {line_number}: player slot {slot} seed mismatch")
            groups.append((f"player[{slot}]", player_rng,
                           PLAYER_STREAMS, player_seed))

        for owner, rngset, expected_names, group_seed in groups:
            rngs = rngset.get("rngs") or {}
            if set(rngs) != set(expected_names):
                failure(f"line {line_number} {owner}: RNG enum inventory mismatch")
                continue
            if not isinstance(group_seed, int):
                continue
            for name, value in rngs.items():
                totals["stream_snapshots"] += 1
                key = (owner, str(group_seed), name)
                label = f"{owner}.{name}"
                names_checked.add(label)
                try:
                    count = int(value["counter"])
                    current = tuple(int(value[f"state{i}"]) for i in range(4))
                    if count < 0 or any(x < 0 or x > MASK for x in current):
                        raise ValueError("RNG value out of range")
                except (ValueError, KeyError, TypeError) as exc:
                    failure(f"line {line_number} {label}: invalid state {exc}")
                    continue
                first_counters.setdefault(label, count)
                last_counters[label] = count
                if key in seen:
                    old_count, predicted = seen[key]
                    if count < old_count:
                        failure(f"line {line_number} {label}: counter decreased")
                        continue
                    totals["verified_transitions"] += 1
                    steps = count - old_count
                else:
                    predicted = initial_state(
                        (group_seed + xxhash64(stream_name(name))) & MASK)
                    steps = count
                for _ in range(steps):
                    predicted = advance(predicted)
                totals["xoshiro_steps"] += steps
                if predicted != current:
                    failure(f"line {line_number} {label}: xoshiro state mismatch "
                            f"at counter {count}")
                seen[key] = (count, current)

    if session is None or session.get("schema") != "sts2-reference-probe-v4":
        failure("Missing native v4 session header")
    if session is not None and (session.get("expected_game_version") != "v0.111.0"
                                or session.get("expected_game_commit") != "41cef1ea"):
        failure("The trace has a different native build; this is a pinned v0.111.0 audit")
    return {
        "schema": "native-rng-stream-audit-v1",
        "passed": not errors,
        "errors": errors,
        "counts": totals,
        "streams_checked": sorted(names_checked),
        "first_counters": first_counters,
        "last_counters": last_counters,
        "limitations": [
            "Named persistent streams only; local map/event/encounter RNGs "
            "are not in the snapshots",
            "Counter/state parity verifies PRNG implementation and consumption "
            "counts, not attribution to particular native calls",
            "Only single run per string seed in a trace is supported; repeated "
            "identical-seed runs should be separated",
            "RNG state is private and must not enter player-visible observations",
        ],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = check_trace(args.trace)
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print("PASS" if result["passed"] else "FAIL", result["counts"])
        print("Streams:", ", ".join(result["streams_checked"]))
        for error in result["errors"]:
            print("  ERROR:", error)
    if not result["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
