#!/usr/bin/env python3
"""Independent native-v111 event RNG reconstruction from the original run seed.

Pinned source: RunRngSet(string), EventModel.BeginEvent, Rng, MegaRandom,
StringHelper.GetDeterministicHashCode (Flaaax/STS2-source@706ef1c9).

For a single-player Neow event the original seed is:
    xxhash64(seed_utf8) + xxhash64(b"NEOW") (mod 2**64).
MegaRandom uses four SplitMix64 outputs for xoshiro256**. Each Neow
NextInt/NextBool call advances exactly once. Source's event RNG counter
therefore independently determines the state, *without inferring options*.
This does not model the emulator's separate prototype RNG or native map RNG.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

MASK = (1 << 64) - 1
P1 = 11400714785074694791
P2 = 14029467366897019727
P3 = 1609587929392839161
P4 = 9650029242287828579
P5 = 2870177450012600261


def rol(value: int, bits: int) -> int:
    return ((value << bits) | (value >> (64 - bits))) & MASK


def _xxround(acc: int, word: int) -> int:
    return rol((acc + word * P2) & MASK, 31) * P1 & MASK


def _merge(acc: int, word: int) -> int:
    return (acc ^ _xxround(0, word)) * P1 + P4 & MASK


def xxhash64(data: bytes) -> int:
    """Zero-seed xxHash64, matching System.IO.Hashing.XxHash64."""
    if not isinstance(data, bytes):
        raise TypeError("xxhash64 requires bytes")
    length = len(data)
    pos = 0
    if length >= 32:
        v1 = (P1 + P2) & MASK
        v2 = P2
        v3 = 0
        v4 = (-P1) & MASK
        while pos <= length - 32:
            v1 = _xxround(v1, int.from_bytes(data[pos:pos+8], "little"))
            v2 = _xxround(v2, int.from_bytes(data[pos+8:pos+16], "little"))
            v3 = _xxround(v3, int.from_bytes(data[pos+16:pos+24], "little"))
            v4 = _xxround(v4, int.from_bytes(data[pos+24:pos+32], "little"))
            pos += 32
        result = (rol(v1, 1) + rol(v2, 7) + rol(v3, 12)
                  + rol(v4, 18)) & MASK
        for word in (v1, v2, v3, v4):
            result = _merge(result, word)
    else:
        result = P5
    result = (result + length) & MASK
    while pos + 8 <= length:
        word = int.from_bytes(data[pos:pos+8], "little")
        result ^= _xxround(0, word)
        result = (rol(result, 27) * P1 + P4) & MASK
        pos += 8
    if pos + 4 <= length:
        result ^= int.from_bytes(data[pos:pos+4], "little") * P1 & MASK
        result = (rol(result, 23) * P2 + P3) & MASK
        pos += 4
    while pos < length:
        result ^= data[pos] * P5 & MASK
        result = rol(result, 11) * P1 & MASK
        pos += 1
    result ^= result >> 33
    result = result * P2 & MASK
    result ^= result >> 29
    result = result * P3 & MASK
    result ^= result >> 32
    return result


def _splitmix(seed: int):
    seed = (seed + 11400714819323198485) & MASK
    word = seed
    word = ((word ^ (word >> 30)) * 13787848793156543929) & MASK
    word = ((word ^ (word >> 27)) * 10723151780598845931) & MASK
    return seed, word ^ (word >> 31)


def next_xoshiro(words: tuple[int, int, int, int]):
    """Advance the native xoshiro256** state by a single raw draw."""
    a, b, c, d = words
    t = b << 17 & MASK
    c ^= a
    d ^= b
    b ^= c
    a ^= d
    c ^= t
    d = rol(d, 45)
    return a, b, c, d


def neow_event_rng(seed: str, counter: int) -> dict:
    if not isinstance(seed, str) or not seed:
        raise ValueError("Original source run string seed is required")
    if seed.startswith("old"):
        raise ValueError("Native legacy 'old' seed requires old-hash compatibility")
    if type(counter) is not int or counter < 0 or counter > 10000:
        raise ValueError("Invalid or excessive native Neow event RNG counter")
    # First single-player slot index=0, and Neow.IsShared=false.
    state_seed = (xxhash64(seed.encode("utf-8"))
                  + xxhash64(b"NEOW")) & MASK
    words = []
    for _ in range(4):
        state_seed, word = _splitmix(state_seed)
        words.append(word)
    state = tuple(words)
    for _ in range(counter):
        state = next_xoshiro(state)
    return {"counter": counter, **{
        "s" + str(i): value for i, value in enumerate(state)
    }}


def verify_neow_event_rng(seed: str, observed: dict) -> dict:
    result = {
        "schema": "native-neow-event-rng-verification-v1",
        "source": "pinned-v111-run-seed-event-rng-reconstruction",
        "status": "NOT_TESTED",
        "first_difference": None,
        "native_emulator_rng_parity": "NOT_TESTED",
        "emulator_event_rng_alignment": "NOT_COMPARABLE",
    }
    if not isinstance(observed, dict):
        return {**result, "status": "BLOCKED_SOURCE_EVENT_RNG",
                "first_difference": {"path": "$.event_rng", "reason": "MISSING_SOURCE_CHECKPOINT"}}
    try:
        expected = neow_event_rng(seed, observed.get("counter"))
    except (TypeError, ValueError) as exc:
        return {**result, "status": "BLOCKED_SOURCE_EVENT_RNG",
                "first_difference": {"path": "$.event_rng",
                                     "reason": type(exc).__name__ + ": " + str(exc)}}
    for key in ("counter", "s0", "s1", "s2", "s3"):
        if type(observed.get(key)) is not int or observed[key] != expected[key]:
            return {**result, "status": "SOURCE_EVENT_RNG_DIFFERENCE",
                    "first_difference": {"path": "$.event_rng." + key,
                                         "native": observed.get(key),
                                         "reconstructed": expected[key]}}
    return {**result, "status": "SOURCE_EVENT_RNG_RECONSTRUCTED"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", required=True)
    parser.add_argument("--counter", type=int, required=True)
    parser.add_argument("--checkpoint-json", type=Path)
    args = parser.parse_args()
    try:
        expected = neow_event_rng(args.seed, args.counter)
        output = (verify_neow_event_rng(
            args.seed, json.loads(args.checkpoint_json.read_text()))
            if args.checkpoint_json else
            {"status": "COMPUTED_NATIVE_EVENT_RNG", "event_rng": expected,
             "native_emulator_rng_parity": "NOT_TESTED"})
    except (OSError, ValueError, TypeError) as exc:
        output = {"status": "INVALID_SOURCE_INPUT", "reason": str(exc)}
    print(json.dumps(output, indent=2, sort_keys=True))
    return 0 if output["status"] in (
        "COMPUTED_NATIVE_EVENT_RNG", "SOURCE_EVENT_RNG_RECONSTRUCTED") else 2


if __name__ == "__main__":
    raise SystemExit(main())
