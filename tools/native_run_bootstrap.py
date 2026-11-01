#!/usr/bin/env python3
"""Fail-closed source-prelude -> independent v111 emulator first-combat bootstrap.

This resets a REAL emulator run; no native combat state is imported.
Native room arrivals determine destinations independently of map queue votes.
Only a single Neow choice followed by a directly reached first combat is
currently executable. Source RNG and native mechanics parity stay unverified.
"""
from __future__ import annotations

import argparse
import base64
import json
import re
import struct
from pathlib import Path

from native_run_prelude_audit import audit, load
from native_neow_rng_verify import verify_neow_event_rng
from native_run_rng_verify import verify_run_streams
from native_map_fidelity import compare_native_map
from native_neow_rng_probe_audit import compare as compare_neow_rng, has_pinned_native_build

SCHEMA = "native-first-combat-bootstrap-v1"
# Names verified against the pinned v111 Neow.cs and emulator Neow catalogue.
_NEOW_RELIC_CLASSES = (
    "ArcaneScroll BoomingConch FishingRod GoldenPearl Kaleidoscope "
    "LeadPaperweight LostCoffer MassiveScroll NeowsTorment NewLeaf "
    "PhialHolster PreciseScissors ScrollBoxes WingedBoots "
    "LavaRock SmallCapsule NutritiousOyster StoneHumidifier NeowsTalisman "
    "Pomander CursedPearl DowsingRod HeftyTablet LargeCapsule LeafyPoultice "
    "NeowsBones NeowsSacrifice PrecariousShears SilkenTress SilverCrucible"
).split()
_RELIC_TO_EMULATOR = {
    re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", cls).upper():
        "take_proto.native.neow." + cls.lower()
    for cls in _NEOW_RELIC_CLASSES
}


def _report(status, reason, *, first=None, steps=None, snapshot=None, checks=None):
    return {
        "schema": SCHEMA, "status": status, "reason": reason,
        "first_difference_or_blocker": first,
        "emulator_steps": steps or [],
        "candidate_emulator_snapshot": ("PRODUCED_UNCERTIFIED" if snapshot else "NOT_PRODUCED"),
        "checks": checks or {},
        "source_data": "NATIVE_SOURCE_PRELUDE_OR_EXPLICIT_SYNTHETIC_FIXTURE",
        "native_mechanics_parity": "NOT_TESTED",
        "native_rng_alignment": "NOT_TESTED",
        "event_local_rng_alignment": "NOT_COMPARABLE",
        "source_neow_rng_seed_recomputation": (
            "MATCHED" if (checks or {}).get("native_neow_rng_comparison", {})
                .get("source_rng_recomputation") == "MATCHED"
            else "NOT_TESTED"),
        "native_effect_settlement": "UNVERIFIED",
        "native_to_emulator_identity_parity": "NOT_TESTED",
        "ready_for_native_lockstep": False,
    }, snapshot


def _block(status, reason, **details):
    return _report(status, reason, first=details or None)


def _typed_payload(action):
    try:
        value = json.loads(action["payloadJson"])
    except (ValueError, KeyError, TypeError):
        raise ValueError("Emulator action payload is not valid JSON")
    if not isinstance(value, dict):
        raise ValueError("Emulator action payload is not an object")
    return value


def _choice_id(action):
    value = _typed_payload(action)
    if set(value) == {"ChoiceId"}:
        return value["ChoiceId"]
    if set(value) == {"choice_id"}:
        return value["choice_id"]
    raise ValueError("Unknown emulator event-choice payload keys")


def _node_id(action):
    value = _typed_payload(action)
    if set(value) == {"NodeId"}:
        return value["NodeId"]
    if set(value) == {"node_id"}:
        return value["node_id"]
    raise ValueError("Unknown emulator map-choice payload keys")


def _neow_native_id(option):
    if option.get("is_locked") is not False or option.get("is_proceed") is not False:
        raise ValueError("Locked or proceed Neow option is not a mapped selection")
    relic = option.get("relic_model_id")
    key = option.get("text_key")
    if not isinstance(relic, str) or not isinstance(key, str):
        raise ValueError("Source Neow option lacks its native relic/text identity")
    # The pinned EventModel.OptionKey uses NEOW.pages.INITIAL.options.RELIC_ID.
    if key != "NEOW.pages.INITIAL.options." + relic:
        raise ValueError("Native Neow option key/relic identity disagree")
    if relic not in _RELIC_TO_EMULATOR:
        raise ValueError("Unsupported native Neow relic identity: " + relic)
    return _RELIC_TO_EMULATOR[relic]


def _native_upfront_diagnosis(source, emulator_snapshot):
    """Compare the complete source-ordered UpFront schedule at RunStarted.

    Verify both the native counter and four RNG words. The generated event,
    encounter and Ancient queues are retained for future continuation, but
    their identities remain unobserved by the passive native capture.
    """
    samples = [s for s in source.get("source_run_rng_snapshots", [])
               if s.get("site") == "run_started"]
    if len(samples) != 1:
        return {"status": "BLOCKED_SOURCE_UPFRONT_CHECKPOINT"}
    source_streams = [s for s in samples[0].get("streams", [])
                      if s.get("scope") == "run"
                      and s.get("stream_id") == "UpFront"]
    emu_streams = [s for s in emulator_snapshot.get("state", {})
                   .get("native_run_rng", {}).get("streams", [])
                   if s.get("stream_id") == "run.up_front"]
    if len(source_streams) != 1 or len(emu_streams) != 1:
        return {"status": "BLOCKED_UPFRONT_STREAM_IDENTITY"}
    native, emulator = source_streams[0], emu_streams[0]
    try:
        encoded = emulator["state_bytes"]
        words = struct.unpack("<QQQQ",
            base64.b64decode(encoded, validate=True))
        emulator_count = emulator["call_count"]
        if type(emulator_count) is not int:
            raise ValueError("Invalid emulator RNG counter")
        expected = (native["s0"], native["s1"], native["s2"], native["s3"])
        native_count = native["counter"]
        if type(native_count) is not int:
            raise ValueError("Invalid native RNG counter")
    except (KeyError, ValueError, TypeError, struct.error):
        return {"status": "BLOCKED_UPFRONT_CURSOR_ENCODING"}
    matched = (native_count == emulator_count and expected == words)
    retained = emulator_snapshot.get("state", {}).get("native_startup_rooms")
    acts = retained.get("acts") if isinstance(retained, dict) else None
    generated_queues_present = (isinstance(acts, list) and len(acts) == 3
        and [act.get("act") for act in acts if isinstance(act, dict)] == [1, 2, 3]
        and all(isinstance(act.get("events"), list)
                and isinstance(act.get("normal_encounters"), list)
                and isinstance(act.get("elite_encounters"), list)
                and isinstance(act.get("boss_encounter"), str)
                and isinstance(act.get("ancient"), str)
                for act in acts if isinstance(act, dict)))
    return {
        "status": ("SOURCE_UPFRONT_MATCHED" if matched
                   else "SOURCE_UPFRONT_DIVERGED"),
        "source_boundary": "RunManager.RunStarted",
        "emulator_boundary": ("emulator.start_run_after_source_generate_rooms"
                              if generated_queues_present else
                              "emulator.start_run_without_retained_source_rooms"),
        "source_counter": native_count,
        "emulator_counter": emulator_count,
        "remaining_source_draws": native_count - emulator_count,
        "source_cursor_words_match": expected == words,
        "source_upfront_all_phases_reconstructed": generated_queues_present,
        "source_upfront_room_identity_parity": "OBSERVED_PREFIX_COMPARED_SEPARATELY",
        "first_difference": (None if matched else {
            "path": "$.native_run_rng.run.up_front.counter"
                if native_count != emulator_count else
                "$.native_run_rng.run.up_front.state_bytes",
            "native": native_count if native_count != emulator_count else expected,
            "emulator": emulator_count if native_count != emulator_count else words,
        }),
    }



def _native_upfront_room_prefix_diagnosis(source, emulator_snapshot):
    """Compare pre-generated queue prefixes with actually entered native rooms.

    RNG-state agreement cannot establish queue-content agreement: identical
    call counts can select different encounters if source pools or ordering
    differ. Observed RoomEntered identities are independent native evidence;
    the source does not expose the entire pre-generated queue yet.
    """
    startup = emulator_snapshot.get("state", {}).get("native_startup_rooms")
    acts = startup.get("acts") if isinstance(startup, dict) else None
    if (not isinstance(acts, list) or len(acts) != 3
        or [a.get("act") for a in acts if isinstance(a, dict)] != [1, 2, 3]):
        return {"status": "BLOCKED_SOURCE_ROOM_QUEUE_SNAPSHOT"}

    # Native RunManager.RoomEntered is authoritative for an encountered
    # identity, regardless of unverified map-input queue provenance.
    observed = [room for room in source.get("source_room_arrivals", [])
                if room.get("room_type") in ("Monster", "Elite", "Boss")]
    if not observed:
        return {"status": "SOURCE_ROOM_QUEUE_PREFIX_UNOBSERVED",
                "full_queue_identity_parity": "NOT_TESTED"}

    def identity_token(value):
        return "".join(ch.upper() for ch in value if ch.isalnum())

    counters = {}
    for room in sorted(observed, key=lambda item: item.get("ordinal", 0)):
        act_idx, room_type, native_id = (
            room.get("act_index"), room.get("room_type"), room.get("room_model_id"))
        if (type(act_idx) is not int or not 0 <= act_idx < 3
            or not isinstance(native_id, str) or not native_id):
            return {"status": "BLOCKED_NATIVE_ROOM_IDENTITY"}
        act = acts[act_idx]
        field = {"Monster": "normal_encounters",
                 "Elite": "elite_encounters",
                 "Boss": "boss_encounter"}[room_type]
        count = counters.get((act_idx, room_type), 0)
        counters[(act_idx, room_type)] = count + 1
        if room_type == "Boss":
            expected = (act.get("boss_encounter") if count == 0
                        else act.get("second_boss_encounter"))
            path = ("$.native_startup_rooms.acts["
                    + str(act_idx) + "]."
                    + ("boss_encounter" if count == 0 else "second_boss_encounter"))
        else:
            entries = act.get(field)
            if not isinstance(entries, list) or count >= len(entries):
                return {"status": "BLOCKED_SOURCE_ROOM_QUEUE_ENTRIES",
                        "act": act_idx + 1, "room_type": room_type}
            expected = entries[count]
            path = ("$.native_startup_rooms.acts["
                    + str(act_idx) + "]." + field + "[" + str(count) + "]")
        if not isinstance(expected, str):
            return {"status": "BLOCKED_SOURCE_ROOM_QUEUE_ENTRIES",
                    "act": act_idx + 1, "room_type": room_type}
        if identity_token(native_id) != identity_token(expected):
            return {
                "status": "SOURCE_ROOM_QUEUE_FIRST_DIFFERENCE",
                "source_observed_prefix_length": sum(counters.values()),
                "full_queue_identity_parity": "NOT_VERIFIED",
                "source_evidence": "RunManager.RoomEntered",
                "first_difference": {
                    "path": path, "native": native_id,
                    "emulator_source_queue": expected,
                    "room_type": room_type, "act": act_idx + 1,
                },
            }
    return {
        "status": "SOURCE_ROOM_QUEUE_OBSERVED_PREFIX_MATCHED",
        "source_observed_prefix_length": len(observed),
        "full_queue_identity_parity": "NOT_VERIFIED",
        "source_evidence": "RunManager.RoomEntered",
        "first_difference": None,
    }


def _native_upfront_full_queue_diagnosis(source, emulator_snapshot):
    """Compare whole generated native queues, if a new source capture has them."""
    native = source.get("source_startup_room_queues")
    if native is None:
        return {"status": "SOURCE_FULL_ROOM_QUEUES_NOT_CAPTURED",
                "full_queue_identity_parity": "NOT_TESTED"}
    observed = native.get("acts") if isinstance(native, dict) else None
    retained = emulator_snapshot.get("state", {}).get("native_startup_rooms")
    predicted = retained.get("acts") if isinstance(retained, dict) else None
    if not isinstance(observed, list) or len(observed) != 3:
        return {"status": "BLOCKED_SOURCE_FULL_ROOM_QUEUES"}
    if not isinstance(predicted, list) or len(predicted) != 3:
        return {"status": "BLOCKED_EMULATOR_FULL_ROOM_QUEUES"}

    def token(value):
        return "".join(ch.upper() for ch in value if ch.isalnum())

    for index, (a, b) in enumerate(zip(observed, predicted)):
        if a.get("act_index") != index or b.get("act") != index + 1:
            return {"status": "BLOCKED_FULL_ROOM_QUEUE_ACT_ORDER"}
        for key in ("events", "normal_encounters", "elite_encounters",
                    "boss_encounter", "second_boss_encounter", "ancient"):
            left, right = a.get(key), b.get(key)
            if key in ("events", "normal_encounters", "elite_encounters"):
                if not isinstance(left, list) or not isinstance(right, list):
                    return {"status": "BLOCKED_FULL_ROOM_QUEUE_ENTRIES"}
                length = max(len(left), len(right))
                pairs = [(left[i] if i < len(left) else None,
                          right[i] if i < len(right) else None)
                         for i in range(length)]
            else:
                pairs = [(left, right)]
            for q_index, (native_id, emu_id) in enumerate(pairs):
                if native_id is not None and not isinstance(native_id, str):
                    return {"status": "BLOCKED_FULL_ROOM_QUEUE_ENTRIES"}
                if emu_id is not None and not isinstance(emu_id, str):
                    return {"status": "BLOCKED_FULL_ROOM_QUEUE_ENTRIES"}
                if (token(native_id) if native_id is not None else None) != (
                        token(emu_id) if emu_id is not None else None):
                    return {
                        "status": "SOURCE_FULL_ROOM_QUEUES_DIVERGED",
                        "full_queue_identity_parity": "DIVERGED",
                        "first_difference": {
                            "path": "$.native_startup_rooms.acts[" + str(index)
                                + "]." + key
                                + (f"[{q_index}]" if key in
                                   ("events", "normal_encounters",
                                    "elite_encounters") else ""),
                            "native": native_id, "emulator": emu_id,
                        },
                    }
    return {"status": "SOURCE_FULL_ROOM_QUEUES_MATCHED",
            "full_queue_identity_parity": "MATCHED",
            "first_difference": None}


def _native_first_combat_identity_diagnosis(native_room, emulator_snapshot,
                                             node_id):
    """Compare actual selected emulator combat to native RoomEntered identity.

    This is independent of pre-generated UpFront source-queue predictions,
    which are separately audited and may diverge while the actual combat
    still agrees with the observed native room.
    """
    native_id = native_room.get("room_model_id")
    if not isinstance(native_id, str) or not native_id:
        return {"status": "BLOCKED_NATIVE_COMBAT_IDENTITY"}
    trace = emulator_snapshot.get("state", {}).get("native_startup_queue_trace")
    entries = trace.get("comparisons") if isinstance(trace, dict) else None
    if not isinstance(entries, list) or len(entries) != 1:
        return {"status": "BLOCKED_EMULATOR_COMBAT_IDENTITY_TRACE"}
    item = entries[0]
    if (not isinstance(item, dict) or item.get("map_node_id") != node_id
        or item.get("act") != 1 or item.get("queue_index") != 0):
        return {"status": "BLOCKED_EMULATOR_COMBAT_IDENTITY_TRACE"}
    actual = item.get("actual_emulator_encounter_id")
    predicted = item.get("source_encounter_id")
    if not isinstance(actual, str) or ".encounter." not in actual:
        return {"status": "BLOCKED_EMULATOR_COMBAT_IDENTITY_TRACE"}

    def token(value):
        return "".join(ch.upper() for ch in value if ch.isalnum())
    actual_source = actual.split(".encounter.", 1)[1]
    matched = token(native_id) == token(actual_source)
    return {
        "status": ("SOURCE_FIRST_COMBAT_IDENTITY_MATCHED" if matched
                   else "SOURCE_FIRST_COMBAT_IDENTITY_DIVERGED"),
        "native_observed_encounter": native_id,
        "emulator_entered_encounter": actual,
        "generated_source_queue_prediction": predicted,
        "generated_source_queue_prediction_status": item.get("status"),
        "actual_encounter_identity_parity": ("MATCHED" if matched
                                             else "DIVERGED"),
        "first_difference": (None if matched else {
            "path": "$.first_combat.encounter_id",
            "native": native_id, "emulator": actual}),
    }


def _direct_first_combat_route(source):
    """Return (source combat RoomEntered, emulator map floor), or None.

    The pinned native v111 run enters Neow at map row zero / native act
    floor one, THEN the first traversed map room at row one / native
    act floor two. The emulator's map node 1:1:column represents that
    first *traversed* room. Synthetic legacy bootstraps have one
    explicitly emulator-derived room and retain their old convention.
    """
    identity = source.get("run_identity") or {}
    rooms = source.get("source_room_arrivals") or []
    link = source.get("first_combat_link") or {}
    choices = source.get("source_event_choice_requests") or []
    returns = source.get("source_event_choice_returns") or []

    def matches_link(room):
        return (room.get("act_index") == link.get("act_index")
                and room.get("map_column") == link.get("map_column")
                and room.get("map_row") == link.get("map_row"))

    if has_pinned_native_build(identity):
        if len(rooms) != 2 or len(choices) != 1 or len(returns) != 1:
            return None
        neow, combat = rooms
        if not (
            neow.get("act_index") == 0
            and neow.get("act_floor") == 1
            and neow.get("total_floor") == 1
            and neow.get("room_type") == "Event"
            and neow.get("room_model_id") == "NEOW"
            and neow.get("map_column") == 3
            and neow.get("map_row") == 0
            and neow.get("ordinal", -1) < choices[0].get("ordinal", -1)
            and choices[0].get("ordinal", -1) <
                returns[0].get("ordinal", -1)
            and returns[0].get("ordinal", -1) <
                combat.get("ordinal", -1)
            and combat.get("act_index") == 0
            and combat.get("act_floor") == 2
            and combat.get("total_floor") == 2
            and combat.get("room_type") == "Monster"
            and type(combat.get("map_column")) is int
            and 0 <= combat["map_column"] <= 6
            and combat.get("map_row") == 1
            and matches_link(combat)):
            return None
        # An observed native map-move request can support the destination
        # identification, but its queue-return alone does not prove causation.
        moves = [r for r in source.get("source_map_requests", [])
                 if r.get("kind") == "map_move"]
        if (not moves or any(
                r.get("target_column") != combat["map_column"]
                or r.get("target_row") != 1
                or r.get("ordinal", -1) >= combat["ordinal"]
                for r in moves)):
            return None
        return combat, 1

    # Existing explicitly emulator-derived synthetic source fixtures.
    if len(rooms) != 1:
        return None
    room = rooms[0]
    if (room.get("act_index") == 0
        and room.get("act_floor") == 1
        and room.get("room_type") == "Monster"
        and type(room.get("map_column")) is int
        and room.get("map_row") == 0
        and matches_link(room)):
        return room, 1
    return None


def _source_preflight(source):
    if not isinstance(source, dict) or source.get("schema") != "native-run-prelude-audit-v1":
        return "BLOCKED_UNAUDITED_SOURCE", "Expected independent prelude auditor output"
    if source.get("status") not in (
            "RUN_PRELUDE_SOURCE_OBSERVED", "RUN_PRELUDE_PAIRED_SOURCE_OBSERVED"):
        return "BLOCKED_UNAUDITED_SOURCE", "Native prelude audit is incomplete"
    ident = source.get("run_identity")
    if not isinstance(ident, dict) or (not isinstance(ident.get("seed"), str)
        or not ident["seed"] or type(ident.get("ascension")) is not int):
        return "BLOCKED_SOURCE_IDENTITY", "Missing native run seed or ascension"
    if ident.get("character_id", "").upper() != "SILENT" or (
            ident.get("first_act_id", "").upper() != "OVERGROWTH"):
        return "BLOCKED_UNSUPPORTED_RUN", "First slice supports native Silent/Overgrowth only"
    linked = source.get("first_combat_link")
    if not isinstance(linked, dict):
        return "BLOCKED_SOURCE_COMBAT", "Missing first-combat source link"
    if source.get("source_opaque_queue_requests"):
        return "BLOCKED_OPAQUE_PRECOMBAT_ACTIONS", (
            "Unmapped precombat queue actions could affect continuation")
    if source.get("source_precombat_player_choices"):
        return "BLOCKED_NESTED_PRECOMBAT_CHOICES", (
            "Nested native choice results lack original offered-menu/continuation identities")
    choices = source.get("source_event_choice_requests") or []
    returned = source.get("source_event_choice_returns") or []
    if len(choices) != 1 or len(returned) != 1 or (
            choices[0].get("event_model_id", "").upper() != "NEOW"):
        return "BLOCKED_SOURCE_EVENT_SEQUENCE", (
            "Requires one observed Neow source option selection and its return")
    if choices[0].get("request_id") != returned[0].get("request_id"):
        return "BLOCKED_SOURCE_EVENT_SEQUENCE", "Neow source request/return mismatch"
    if not (choices[0].get("ordinal", -1) < returned[0].get("ordinal", -1)):
        return "BLOCKED_SOURCE_EVENT_SEQUENCE", "Neow request/return order invalid"
    if _direct_first_combat_route(source) is None:
        return "BLOCKED_COMPLEX_PRECOMBAT_ROUTE", (
            "Direct first combat requires a source-linked first map room; "
            "pinned native v111 must show Neow row zero followed by one "
            "monster at map row one/native act floor two")
    if not any(s.get("site") == "run_started" for s in
               source.get("source_run_rng_snapshots", [])):
        return "BLOCKED_SOURCE_RNG_CHECKPOINT", "Missing source run-start RNG checkpoint"
    return None


def _emulator_opening_profile(identity: dict) -> str:
    """Use native source map+Neow for pinned real captures.

    Emulator-derived synthetic fixtures retain the previous Neow-only
    geometry so existing transport tests cannot masquerade as native map
    parity. Unrecognized builds are rejected by run_bootstrap before reset.
    """
    return ("v111-experimental-native-standard-map-opening-v1"
            if has_pinned_native_build(identity)
            else "v111-experimental-native-neow-opening-v1")


def run_bootstrap(source: dict, session):
    """Return (diagnostic report, optional *uncertified* exact snapshot)."""
    blocked = _source_preflight(source)
    if blocked:
        return _block(*blocked)
    ident = source["run_identity"]
    neow = source["source_event_choice_requests"][0]
    room, map_floor = _direct_first_combat_route(source)
    steps = []
    opening_profile = _emulator_opening_profile(ident)
    checks = {
        "source_seed": ident["seed"], "ascension": ident["ascension"],
        "source_combat_room": {
            k: room.get(k) for k in ("act_index", "act_floor", "total_floor",
                                      "map_column", "map_row", "room_type")},
        "emulator_first_map_floor": map_floor,
        "native_neow_floor_offset": (
            "NATIVE_NEOW_ACT_FLOOR_ONE_MAP_ROW_ZERO"
            if has_pinned_native_build(ident)
            else "EXPLICIT_EMULATOR_DERIVED_SYNTHETIC_SINGLE_ROOM"),
        "source_event_rng_counter": neow.get("event_rng", {}).get("counter"),
        "emulator_rng_provider": (
            "NATIVE_NEOW_AND_ACT1_MAP_LOCAL_ONLY"
            if has_pinned_native_build(ident)
            else "NATIVE_NEOW_LOCAL_ONLY_MAP_PROTOTYPE_SYNTHETIC"),
        "emulator_fidelity_id": opening_profile,
        "native_map_rng_alignment": "NOT_TESTED",
        "native_map_topology_parity": "NOT_TESTED",
        "map_destination_basis": "NATIVE_ROOM_ENTERED_NOT_MAP_VOTE",
    }
    # This verifies the source event-local RNG independently of Neow's
    # choices and without taking a single emulator or native RNG draw.
    if has_pinned_native_build(ident):
        stream_diagnosis = verify_run_streams(
            ident["seed"], source.get("source_run_rng_snapshots", []))
        checks["native_named_stream_verification"] = stream_diagnosis
        if stream_diagnosis["status"] != "SOURCE_RNG_CHECKPOINTS_RECONSTRUCTED":
            return _report(
                "BLOCKED_NATIVE_NAMED_RNG_RECONSTRUCTION",
                "Source run/player RNG stream checkpoint differs from seed/counter",
                first=stream_diagnosis["first_difference"], checks=checks)
    else:
        checks["native_named_stream_verification"] = {
            "status": "NOT_TESTED_SYNTHETIC_OR_UNRECOGNIZED_BUILD"}
    neow_rng = verify_neow_event_rng(ident["seed"], neow.get("event_rng"))
    checks["native_neow_rng_verification"] = neow_rng
    if neow_rng["status"] != "SOURCE_EVENT_RNG_RECONSTRUCTED":
        return _report(
            "BLOCKED_NATIVE_NEOW_RNG_RECONSTRUCTION",
            "Source event RNG checkpoint disagrees with pinned native seed/algorithm",
            first=neow_rng["first_difference"], checks=checks)
    try:
        source_offers = [_neow_native_id(o) for o in neow["options"]]
    except (KeyError, TypeError, ValueError) as exc:
        return _report("BLOCKED_SOURCE_NEOW_IDENTITIES", str(exc), checks=checks)
    if len(source_offers) != len(set(source_offers)):
        return _report("BLOCKED_DUPLICATE_NEOW_OPTIONS", "Repeated source Neow IDs",
                       checks=checks)
    # Only native pinned-assembly captures undergo seed-to-event RNG
    # reconstruction. Explicitly synthetic fixtures skip it; an unknown
    # provenance must never be silently treated as verified native data.
    build = ident.get("game_build")
    if has_pinned_native_build(ident):
        try:
            probe = session.call("native_neow_rng_probe", seed=ident["seed"])
            comparison = compare_neow_rng(
                source_offers, neow.get("event_rng"), probe)
        except (RuntimeError, ValueError, TypeError) as exc:
            return _report("BLOCKED_NATIVE_NEOW_RNG_PROBE",
                           type(exc).__name__ + ": " + str(exc),
                           checks=checks)
        checks["native_neow_rng_comparison"] = comparison
        if comparison["status"] != "SOURCE_NEOW_EVENT_RNG_AND_OFFERS_RECOMPUTED":
            return _report("SOURCE_NEOW_NATIVE_RNG_DIVERGENCE",
                           "Pinned native Neow event RNG/offer could not be reproduced",
                           first=comparison.get("first_difference") or {
                               "reason": comparison["status"]},
                           checks=checks)
    elif build == "EMULATOR-DERIVED-SYNTHETIC-NOT-NATIVE":
        checks["native_neow_rng_comparison"] = {
            "status": "SKIPPED_EXPLICITLY_EMULATOR_DERIVED_SYNTHETIC"}
    else:
        return _report("BLOCKED_UNRECOGNIZED_SOURCE_BUILD",
                       "Source build is neither pinned native-assembly format nor explicit synthetic",
                       checks=checks)
    if (has_pinned_native_build(ident)
        and not source.get("source_initial_map_snapshot")):
        return _report("BLOCKED_SOURCE_INITIAL_MAP_SNAPSHOT",
                       "Missing native StandardActMap graph and map-local RNG cursor",
                       checks=checks)
    try:
        root = session.reset(ident["seed"], mode="v111", character="silent",
                             first_act="Overgrowth", ascension=ident["ascension"],
                             fidelity_id=opening_profile)
        exact = json.loads(root.save_snapshot())
        if (exact.get("schema") != "sts2-exact-snapshot-v1"
            or exact.get("state", {}).get("run_seed") != ident["seed"]
            or exact.get("state", {}).get("ascension") != ident["ascension"]):
            return _report("BLOCKED_RESET_IDENTITY", "Emulator reset seed/ascension differ",
                           checks=checks)
        initial = root.actions()
        if len(initial) != 1 or initial[0].get("kind") != "start_run" or (
                _typed_payload(initial[0]) != {}):
            return _report("BLOCKED_START_RUN_ACTION",
                           "Emulator run-start legal menu is not the unique start_run action",
                           checks=checks)
        state = root.step(initial[0]["actionId"])
        steps.append({"kind": "start_run", "action_id": initial[0]["actionId"]})
        if has_pinned_native_build(ident):
            opening_snapshot = json.loads(state.save_snapshot())
            checks["native_upfront_startup"] = _native_upfront_diagnosis(
                source, opening_snapshot)
            checks["native_upfront_room_prefix"] = (
                _native_upfront_room_prefix_diagnosis(source, opening_snapshot))
            checks["native_upfront_full_queues"] = (
                _native_upfront_full_queue_diagnosis(source, opening_snapshot))
            map_check = compare_native_map(source, opening_snapshot)
            checks["native_map_comparison"] = map_check
            checks["native_map_rng_alignment"] = (
                map_check["native_map_rng_alignment"])
            checks["native_map_topology_parity"] = (
                map_check["native_map_topology_parity"])
            if map_check["status"] != "SOURCE_MAP_AND_RNG_MATCHED":
                return _report(
                    "SOURCE_NATIVE_MAP_DIFFERENCE",
                    "The emulator opening map or local RNG cursor differs",
                    first=map_check.get("first_difference") or {
                        "reason": map_check["status"]},
                    steps=steps, checks=checks)
        frame = state.observe()
        if frame.get("event_id") != "proto.native.event.neow" or (
                not isinstance(frame.get("event"), dict)):
            return _report("BLOCKED_EMULATOR_NEOW", "Emulator did not enter Neow",
                           steps=steps, checks=checks)
        emu_offers = frame["event"].get("offered_choice_ids")
        if not isinstance(emu_offers, list):
            return _report("BLOCKED_EMULATOR_NEOW_MENU",
                           "Emulator Neow offered choices are unavailable",
                           steps=steps, checks=checks)
        checks["native_neow_offers"] = source_offers
        checks["emulator_neow_offers"] = emu_offers
        if source_offers != emu_offers:
            first = next((i for i in range(min(len(source_offers), len(emu_offers)))
                          if source_offers[i] != emu_offers[i]),
                         min(len(source_offers), len(emu_offers)))
            return _report(
                "SOURCE_NEOW_OFFER_DIFFERENCE", "First Neow offered choice differs",
                first={"path": "$.neow.offers[" + str(first) + "]",
                       "native": source_offers[first] if first < len(source_offers) else None,
                       "emulator": emu_offers[first] if first < len(emu_offers) else None},
                steps=steps, checks=checks)
        legal = state.actions()
        event_actions = [a for a in legal if a.get("kind") == "event_choice"]
        if (len(event_actions) != len(emu_offers)
            or sorted(_choice_id(a) for a in event_actions) != sorted(emu_offers)):
            return _report("BLOCKED_EMULATOR_NEOW_ACTIONS",
                           "Emulator legal choices do not match its offered menu",
                           steps=steps, checks=checks)
        selected_id = source_offers[neow["selected_index"]]
        selected = [a for a in event_actions if _choice_id(a) == selected_id]
        if len(selected) != 1:
            return _report("BLOCKED_SOURCE_NEOW_SELECTION",
                           "Source Neow selection is not uniquely legal in emulator",
                           steps=steps, checks=checks)
        state = state.step(selected[0]["actionId"])
        steps.append({"kind": "event_choice", "source_ordinal": neow["ordinal"],
                      "choice_id": selected_id, "action_id": selected[0]["actionId"]})
        frame = state.observe()
        if frame.get("combat") is not None:
            return _report("BLOCKED_PREMATURE_COMBAT",
                           "First combat occurred before source map traversal",
                           steps=steps, checks=checks)
        # Native Neow's finished page has a separate UI Proceed action.
        # In the emulator it is represented by exactly one deterministic
        # continue_event action. This is an *uncertified* transport step:
        # the native Proceed click itself was not observed by the prelude.
        continuation = state.actions()
        if len(continuation) == 1 and continuation[0].get("kind") == "continue_event":
            if _typed_payload(continuation[0]) != {}:
                return _report("BLOCKED_NEOW_CONTINUATION",
                               "Emulator terminal-page acknowledgement has payload",
                               steps=steps, checks=checks)
            state = state.step(continuation[0]["actionId"])
            steps.append({"kind": "continue_event",
                          "action_id": continuation[0]["actionId"],
                          "native_proceed_ui": "NOT_CAPTURED",
                          "basis": "UNIQUE_EMULATOR_TERMINAL_PAGE_ACK"})
            checks["native_neow_proceed_ui"] = "NOT_CAPTURED"
            frame = state.observe()
        # A Neow relic may trigger a nested deck/card/relic choice. We have
        # no certified source-to-emulator crosswalk for its continuation yet.
        if not any(a.get("kind") == "choose_map_node"
                   for a in state.actions()):
            return _report("BLOCKED_NEOW_CONTINUATION",
                           "Source Neow effect requires further observed macro choices",
                           steps=steps, checks=checks)
        candidate_node = f"1:{map_floor}:{room['map_column']}"
        nodes = [n for n in frame.get("map", []) if n.get("node_id") == candidate_node]
        if len(nodes) != 1 or nodes[0].get("act") != 1 or (
                nodes[0].get("floor") != map_floor):
            return _report(
                "SOURCE_MAP_NODE_DIFFERENCE",
                "Native visited coordinate has no matching emulator map node",
                first={"path": "$.map", "native": {
                    "floor": room["act_floor"], "column": room["map_column"]},
                    "emulator_node_id": candidate_node,
                    "found": len(nodes)},
                steps=steps, checks=checks)
        moves = [a for a in state.actions() if a.get("kind") == "choose_map_node"]
        matching = [a for a in moves if _node_id(a) == candidate_node]
        if len(matching) != 1:
            return _report(
                "SOURCE_MAP_LEGAL_MENU_DIFFERENCE",
                "Native visited destination is not uniquely selectable in emulator",
                first={"path": "$.legal_actions", "native_node": candidate_node,
                       "emulator_legal_nodes": [_node_id(a) for a in moves]},
                steps=steps, checks=checks)
        state = state.step(matching[0]["actionId"])
        steps.append({"kind": "choose_map_node", "source_room_ordinal": room["ordinal"],
                      "node_id": candidate_node, "action_id": matching[0]["actionId"]})
        frame = state.observe()
        if frame.get("combat") is None:
            return _report("SOURCE_COMBAT_ENTRY_DIFFERENCE",
                           "Emulator entered a different room instead of first combat",
                           first={"path": "$.combat", "source_room": room["room_type"],
                                  "emulator_phase": frame.get("phase")},
                           steps=steps, checks=checks)
        if frame.get("current_map_node_id") != candidate_node:
            return _report("SOURCE_COMBAT_MAP_ID_DIFFERENCE",
                           "Emulator first combat is not at selected node",
                           steps=steps, checks=checks)
        if has_pinned_native_build(ident):
            combat_snapshot = json.loads(state.save_snapshot())
            checks["native_first_combat_identity"] = (
                _native_first_combat_identity_diagnosis(
                    room, combat_snapshot, candidate_node))
            identity_comparison = checks["native_first_combat_identity"]
            if identity_comparison["status"] != "SOURCE_FIRST_COMBAT_IDENTITY_MATCHED":
                return _report(
                    "SOURCE_COMBAT_IDENTITY_DIFFERENCE",
                    "Native RoomEntered encounter identity and actual emulator encounter differ or cannot be verified",
                    first=identity_comparison.get("first_difference") or {
                        "reason": identity_comparison["status"]},
                    steps=steps, checks=checks)
        checks["emulator_first_combat_hp"] = frame.get("hp")
        checks["emulator_first_combat_gold"] = frame.get("gold")
        checks["native_room_callback_hp"] = room.get("player_hp")
        checks["native_room_callback_gold"] = room.get("player_gold")
        checks["room_callback_vs_combat_entry_settlement"] = "UNVERIFIED"
        difference = next((
            {"path": "$." + key, "native": room[native_key],
             "emulator": frame.get(key)}
            for key, native_key in (("hp", "player_hp"), ("gold", "player_gold"))
            if frame.get(key) != room.get(native_key)), None)
        snapshot = state.save_snapshot()
        if difference is not None:
            return _report("CANDIDATE_VISIBLE_DIFFERENCE_UNCERTIFIED",
                           "Room callback and emulator combat entry differ; boundaries are not certified aligned",
                           first=difference, steps=steps, checks=checks,
                           snapshot=snapshot)
        return _report(
            "CANDIDATE_COMBAT_REACHED_UNCERTIFIED",
            "Independent source-shaped Neow/map route reached emulator combat; native effects/RNG not certified",
            steps=steps, checks=checks, snapshot=snapshot)
    except (ValueError, TypeError, KeyError, IndexError, RuntimeError) as exc:
        return _report("BLOCKED_EMULATOR_EXECUTION",
                       type(exc).__name__ + ": " + str(exc),
                       steps=steps, checks=checks)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prelude", type=Path, required=True)
    parser.add_argument("--cli-dll", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--out", type=Path)
    parser.add_argument("--candidate-snapshot-out", type=Path)
    args = parser.parse_args()
    rows, error = load(args.prelude)
    source = audit(rows, error)
    if source["status"] != "RUN_PRELUDE_SOURCE_OBSERVED":
        result, candidate = _block(
            "BLOCKED_UNAUDITED_SOURCE", "Native prelude auditor rejected input",
            source_audit=source["status"], errors=source["errors"])
    else:
        import sys
        sys.path.insert(0, str(Path(__file__).resolve().parents[1] /
                               "bindings" / "python"))
        from sts2_emulator import Session
        try:
            with Session([args.dotnet, str(args.cli_dll),
                          "prototype-ai-jsonl"]) as session:
                result, candidate = run_bootstrap(source, session)
        except (OSError, RuntimeError, ValueError) as exc:
            result, candidate = _block(
                "BLOCKED_EMULATOR_SESSION",
                type(exc).__name__ + ": " + str(exc))
    if candidate is not None and args.candidate_snapshot_out:
        args.candidate_snapshot_out.parent.mkdir(parents=True, exist_ok=True)
        args.candidate_snapshot_out.write_text(candidate, encoding="utf-8")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n",
                            encoding="utf-8")
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0 if result["status"] in (
        "CANDIDATE_COMBAT_REACHED_UNCERTIFIED",
        "CANDIDATE_VISIBLE_DIFFERENCE_UNCERTIFIED",
        "SOURCE_NEOW_OFFER_DIFFERENCE",
        "SOURCE_MAP_NODE_DIFFERENCE",
        "SOURCE_MAP_LEGAL_MENU_DIFFERENCE",
        "SOURCE_COMBAT_ENTRY_DIFFERENCE") else 2


if __name__ == "__main__":
    raise SystemExit(main())
