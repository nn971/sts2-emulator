#!/usr/bin/env python3
"""Compare native preselection Neow event-local RNG against a fresh pure probe.

The C# NativeNeowOfferV111 probe uses the pinned source RNG algorithm and
enumerates both unlock-dependent filters; it does not run any emulator action.
This module checks SOURCE RNG reconstruction only, not emulator map/RNG parity.
"""
from __future__ import annotations

import re

SCHEMA = "sts2-v111-native-neow-rng-probe-v1"
_SOURCE = "Flaaax/STS2-source@706ef1c9"
_FIELDS = ("counter", "s0", "s1", "s2", "s3")
_U64 = 2**64 - 1


def _native_state(raw: dict) -> dict | None:
    if not isinstance(raw, dict):
        return None
    if (type(raw.get("counter")) is not int or raw["counter"] < 0
        or raw["counter"] > 2**31 - 1):
        return None
    if not all(type(raw.get(k)) is int and 0 <= raw[k] <= _U64
               for k in ("s0", "s1", "s2", "s3")):
        return None
    return {k: raw[k] for k in _FIELDS}


def _prediction_state(raw: dict) -> dict | None:
    if not isinstance(raw, dict):
        return None
    return _native_state({
        "counter": raw.get("counter"),
        "s0": raw.get("state0"),
        "s1": raw.get("state1"),
        "s2": raw.get("state2"),
        "s3": raw.get("state3"),
    })


def compare(source_ids: list[str], source_rng: dict, probe: dict) -> dict:
    """Return a structural/seed-derivation diagnosis, never a game parity claim."""
    out = {
        "status": "INVALID_NATIVE_NEOW_RNG_PROBE",
        "source_rng_recomputation": "NOT_VERIFIED",
        "source_rng_observer_nonperturbation": "SOURCE_DECLARED",
        "emulator_event_rng_alignment": "NOT_TESTED",
        "native_game_parity": "NOT_TESTED",
        "unlock_hypotheses_considered": 0,
        "matching_hypothesis": None,
        "first_difference": None,
    }
    if (not isinstance(probe, dict)
        or probe.get("schemaId") != SCHEMA
        or probe.get("source") != _SOURCE
        or probe.get("eventId") != "NEOW"
        or probe.get("assumedPlayerSlot") != 0
        or probe.get("modifiers") != "NONE_ASSUMED"
        or probe.get("nativeRngConsumedByInspector") is not False
        or probe.get("emulatorEventRngAlignment") != "NOT_TESTED"
        or probe.get("dynamicUnlockGates") != ["Kaleidoscope", "ScrollBoxes"]):
        return out
    predictions = probe.get("predictions")
    if not isinstance(predictions, list) or len(predictions) != 4:
        return out
    variants = set()
    normalized = []
    for p in predictions:
        if not isinstance(p, dict) or (type(p.get("kaleidoscopeAllowed")) is not bool
            or type(p.get("scrollBoxesAllowed")) is not bool
            or not isinstance(p.get("offeredRelicIds"), list)
            or len(p["offeredRelicIds"]) != 3
            or not all(isinstance(c, str) and
                       c.startswith("proto.native.neow.") for c in
                       p["offeredRelicIds"])
            or not isinstance(p.get("cursedRelicId"), str)
            or p["offeredRelicIds"][-1] != p["cursedRelicId"]
            or type(p.get("positivePoolSize")) is not int
            or p["positivePoolSize"] < 2):
            return out
        key = (p["kaleidoscopeAllowed"], p["scrollBoxesAllowed"])
        if key in variants:
            return out
        variants.add(key)
        state = _prediction_state(p.get("eventRng"))
        if state is None:
            return out
        if state["counter"] != p["positivePoolSize"] + (
            2 if p["cursedRelicId"] == "proto.native.neow.largecapsule" else 3):
            return out
        normalized.append((key, p["offeredRelicIds"], state))
    if len(variants) != 4:
        return out

    native_state = _native_state(source_rng)
    if (not isinstance(source_ids, list) or len(source_ids) != 3
        or not all(isinstance(x, str) and x.startswith("proto.native.neow.")
                   for x in source_ids) or native_state is None):
        return out
    out["unlock_hypotheses_considered"] = 4
    matches = [(key, ids, state) for key, ids, state in normalized
               if ids == source_ids and state == native_state]
    if len(matches) == 1:
        key, _, _ = matches[0]
        out["status"] = "SOURCE_NEOW_EVENT_RNG_AND_OFFERS_RECOMPUTED"
        out["source_rng_recomputation"] = "MATCHED"
        out["matching_hypothesis"] = {
            "kaleidoscope_allowed": key[0],
            "scroll_boxes_allowed": key[1],
            "unlock_provenance": "HYPOTHESIS_CONSISTENT_NOT_INDEPENDENTLY_OBSERVED"
        }
        return out
    if len(matches) > 1:
        out["status"] = "AMBIGUOUS_SOURCE_NEOW_RNG_HYPOTHESES"
        out["first_difference"] = {
            "path": "$.unlock_availability",
            "reason": "MULTIPLE_HYPOTHESES_MATCH"
        }
        return out

    rng_matches = [(key, ids) for key, ids, state in normalized
                   if state == native_state]
    if rng_matches:
        out["status"] = "SOURCE_NEOW_OFFER_DIFFERENCE_AT_SAME_RNG_CURSOR"
        key, ids = rng_matches[0]
        index = next((i for i in range(3) if source_ids[i] != ids[i]), None)
        out["first_difference"] = {
            "path": "$.neow.options[" + str(index) + "]",
            "source": source_ids[index] if index is not None else None,
            "recomputed": ids[index] if index is not None else None,
            "kaleidoscope_allowed": key[0],
            "scroll_boxes_allowed": key[1]
        }
    else:
        out["status"] = "SOURCE_NEOW_EVENT_RNG_STATE_DIFFERENCE"
        # Find a hypothesis with the closest call-count, preserving source
        # fields and exposing the earliest differing cursor component.
        key, ids, state = min(normalized, key=lambda x: (
            abs(x[2]["counter"] - native_state["counter"]), x[0]))
        field = next((f for f in _FIELDS if state[f] != native_state[f]), "counter")
        out["first_difference"] = {
            "path": "$.event_rng." + field,
            "source": native_state[field], "recomputed": state[field],
            "candidate_unlock_hypothesis": {
                "kaleidoscope_allowed": key[0],
                "scroll_boxes_allowed": key[1]
            }
        }
    return out


def has_pinned_native_build(identity: dict) -> bool:
    build = identity.get("game_build")
    return (isinstance(build, str) and
            re.fullmatch(r"assembly=[^;]+;mvid=[0-9a-fA-F]{32};sha256=[0-9a-fA-F]{64}",
                         build) is not None)
