#!/usr/bin/env python3
"""Independent persistent-emulator replay at source-observed turn starts.

Bootstrap is from the native *prelude* via the emulator's own RunStart/Neow/map
actions. The combat state is never imported from the original game. Native
turn candidates, source command identities and selected nested choices remain
separate evidence; the comparison is NOT native settlement certification.

The emulator currently resolves enemy turns atomically under end_turn. Hence
only PLAYER_TURN_START is compared, while ENEMY_TURN_START stays source-only.
"""
from __future__ import annotations

import argparse
import base64
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))

from native_action_envelope_audit import load as load_sidecar
from native_lockstep_source_journal import build as build_source_journal
from native_lockstep_shadow import _resolve
from native_run_bootstrap import run_bootstrap
from native_run_prelude_audit import load as load_prelude, audit as audit_prelude


def _report(status, reason, *, matched=None, deferred=None, stepped=0,
            first=None, rng=None):
    return {
        "schema": "native-turn-checkpoint-emulator-replay-v1",
        "status": status, "reason": reason,
        "player_checkpoints": matched or [],
        "enemy_checkpoint_candidates": deferred or [],
        "emulator_canonical_actions_stepped": stepped,
        "first_difference": first,
        "rng_checkpoint_diagnostics": rng or [],
        "native_boundary_settlement": "SETTLEMENT_UNVERIFIED",
        "native_input_origin": "UNVERIFIED",
        "native_legal_menu": "NOT_VERIFIED",
        "native_full_state_parity": "NOT_TESTED",
        "uncompared_native_fields": ["combat.max_energy", "pending_effects",
                                     "complete_rng_history", "relic_potion_mutable_state"],
        "native_live_lockstep": False,
    }


def check_prelude_pair(journal, prelude):
    """Require the source prelude to link to this exact native combat run."""
    if (not isinstance(journal, dict) or not isinstance(prelude, dict)
        or prelude.get("status") != "RUN_PRELUDE_SOURCE_OBSERVED"):
        return "SOURCE_PRELUDE_UNAUDITED"
    identity = prelude.get("run_identity") or {}
    link = prelude.get("first_combat_link") or {}
    if (not journal.get("run_id")
        or link.get("combat_run_id") != journal.get("run_id")
        or identity.get("game_build") != journal.get("game_build")
        or identity.get("seed") != journal.get("run_seed")
        or identity.get("ascension") != journal.get("ascension")):
        return "SOURCE_PRELUDE_COMBAT_IDENTITY_MISMATCH"
    return None


def _token(value):
    if not isinstance(value, str):
        return ""
    s = "".join(c for c in value.split(".")[-1].upper() if c.isalnum())
    return {"STRIKE": "STRIKESILENT", "DEFEND": "DEFENDSILENT",
            "STRENGTHPOWER": "STRENGTH", "WEAKPOWER": "WEAK"}.get(s, s)


def _combat(exact):
    state = exact.get("state") if isinstance(exact, dict) else None
    world = state.get("world") if isinstance(state, dict) else None
    return world.get("combat") if isinstance(world, dict) else None


def _cards_for_zone(combat, zone):
    ids = combat.get(zone)
    cards = combat.get("cards")
    if not isinstance(ids, list) or not isinstance(cards, list):
        raise ValueError("Emulator exact combat lacks " + zone + " or cards")
    by_id = {item["instance_id"]: item for item in cards
             if isinstance(item, dict) and type(item.get("instance_id")) is int}
    if len(by_id) != len(cards):
        raise ValueError("Duplicate or invalid emulator card instance")
    if any(type(i) is not int or i not in by_id for i in ids):
        raise ValueError("Emulator zone references unknown card instance")
    return [by_id[i] for i in reversed(ids)] if zone == "draw_pile" else [
        by_id[i] for i in ids]


def derive_crosswalk(source, snapshot):
    """Require matching entire *ordered* initial card zones before ID pairing."""
    combat = _combat(snapshot)
    player = source.get("player") if isinstance(source, dict) else None
    n = source.get("combat") if isinstance(source, dict) else None
    if not isinstance(combat, dict) or not isinstance(n, dict) or not isinstance(player, dict):
        raise ValueError("Initial combat projections missing")
    cards = {}
    for zone in ("hand", "draw_pile", "discard_pile", "exhaust_pile"):
        left = n.get(zone)
        right = _cards_for_zone(combat, zone)
        if not isinstance(left, list) or len(left) != len(right):
            raise ValueError("Initial zone size mismatch: " + zone)
        for native, emu in zip(left, right):
            if (_token(native.get("model_id")) != _token(emu.get("card_id"))
                or type(native.get("instance_id")) is not int):
                raise ValueError("Initial zone identity mismatch: " + zone)
            key = str(native["instance_id"])
            value = emu["instance_id"]
            if key in cards and cards[key] != value:
                raise ValueError("Conflicting card crosswalk")
            cards[key] = value
    if len(set(cards.values())) != len(cards):
        raise ValueError("Noninjective native card crosswalk")
    # Native creature:0 is the player. Enemy indices must retain source order.
    native_enemies = n.get("creatures")
    emu_enemies = combat.get("enemies")
    if (not isinstance(native_enemies, list)
        or not isinstance(emu_enemies, list)
        or len(native_enemies) != len(emu_enemies) + 1
        or _token(native_enemies[0].get("model_id")) != "SILENT"):
        raise ValueError("Initial creature roster differs")
    enemies = {}
    for a, b in zip(native_enemies[1:], emu_enemies):
        if (_token(a.get("model_id")) != _token(b.get("enemy_id"))
            or a.get("hp") != b.get("hp")
            or a.get("block") != b.get("block")):
            raise ValueError("Initial enemy identity/projection mismatch")
        enemies[a["id"]] = b["instance_id"]
    if len(enemies) != len(set(enemies.values())):
        raise ValueError("Noninjective enemy crosswalk")
    return {"schema": "native-emulator-id-crosswalk-v1",
            "cards": cards, "enemies": enemies}


def first_difference(source, snapshot, crosswalk):
    """Compare typed exact emulator state to the source's visible projection."""
    n = source.get("combat") if isinstance(source, dict) else None
    p = source.get("player") if isinstance(source, dict) else None
    e = _combat(snapshot)
    root = snapshot.get("state") if isinstance(snapshot, dict) else None
    if not all(isinstance(x, dict) for x in (n, p, e, root)):
        return {"path": "$", "reason": "MISSING_COMBAT_PROJECTION"}
    fields = (
        ("$.player.hp", p.get("hp"), root["player"].get("hp")),
        ("$.player.max_hp", p.get("max_hp"), root["player"].get("max_hp")),
        ("$.player.gold", p.get("gold"), root["player"].get("gold")),
        ("$.player.block", p.get("block"), e.get("player_block")),
        ("$.combat.energy", n.get("energy"), e.get("energy")),
        ("$.combat.turn", n.get("turn"), e.get("turn")),
    )
    for path, x, y in fields:
        if type(x) is not int or type(y) is not int:
            return {"path": path, "reason": "SCALAR_UNOBSERVED"}
        if x != y:
            return {"path": path, "reason": "SCALAR_DIFFERENCE",
                    "native": x, "emulator": y}
    for zone in ("hand", "draw_pile", "discard_pile", "exhaust_pile"):
        a = n.get(zone)
        if not isinstance(a, list):
            return {"path": "$.combat." + zone, "reason": "ZONE_UNOBSERVED"}
        try:
            b = _cards_for_zone(e, zone)
        except ValueError as exc:
            return {"path": "$.combat." + zone, "reason": str(exc)}
        expected = []
        for item in a:
            key = str(item.get("instance_id"))
            if key not in crosswalk["cards"]:
                return {"path": "$.combat." + zone,
                        "reason": "NATIVE_CARD_ID_UNMAPPED", "native_id": key}
            expected.append(crosswalk["cards"][key])
        actual = [card["instance_id"] for card in b]
        if expected != actual:
            return {"path": "$.combat." + zone, "reason": "ORDERED_ZONE_DIFFERENCE",
                    "native_mapped": expected, "emulator": actual}
        for native, emu in zip(a, b):
            if _token(native.get("model_id")) != _token(emu.get("card_id")):
                return {"path": "$.combat." + zone, "reason": "CARD_MODEL_DIFFERENCE"}
    native_enemies = n.get("creatures")
    enemies = e.get("enemies")
    if (not isinstance(native_enemies, list) or not isinstance(enemies, list)
        or len(native_enemies) != len(enemies) + 1):
        return {"path": "$.combat.creatures", "reason": "ENEMY_ROSTER_DIFFERENCE"}
    for a, b in zip(native_enemies[1:], enemies):
        if crosswalk["enemies"].get(a.get("id")) != b.get("instance_id"):
            return {"path": "$.combat.creatures", "reason": "ENEMY_CROSSWALK_DIFFERENCE"}
        for key in ("hp", "block"):
            if a.get(key) != b.get(key):
                return {"path": "$.combat.creatures." + a["id"] + "." + key,
                        "reason": "ENEMY_SCALAR_DIFFERENCE",
                        "native": a.get(key), "emulator": b.get(key)}
        a_str = sum(power.get("amount", 0) for power in a.get("powers", [])
                    if _token(power.get("model_id")) == "STRENGTH")
        b_str = sum(power.get("stacks", 0) for power in b.get("power_states", [])
                    if _token(power.get("power_id")) == "STRENGTH")
        if a_str != b_str:
            return {"path": "$.combat.creatures." + a["id"] + ".strength",
                    "reason": "ENEMY_STRENGTH_DIFFERENCE",
                    "native": a_str, "emulator": b_str}
    return None


def _native_rng_window(sidecar, ordinal):
    rng = [row for row in sidecar
           if row.get("type") == "native_rng_stream_checkpoint"]
    before = next((x for x in reversed(rng) if x["ordinal"] < ordinal), None)
    after = next((x for x in rng if x["ordinal"] > ordinal), None)
    if before is None or after is None:
        return None, "SOURCE_RNG_NOT_BRACKETED"
    a, b = before["data"]["streams"], after["data"]["streams"]
    if a != b:
        return None, "SOURCE_RNG_BRACKET_CHANGED"
    if len(a) != 15 or len({x["stream_id"] for x in a}) != 15:
        return None, "SOURCE_RNG_STREAM_INVENTORY_INCOMPLETE"
    return {x["stream_id"]: x["cursor"] for x in a}, "SOURCE_RNG_BRACKET_STABLE"


def _check_rng(source_streams, snapshot):
    if source_streams is None:
        return {"status": "SOURCE_RNG_NOT_COMPARABLE"}
    root = snapshot.get("state", {})
    bundle = root.get("native_run_rng") or {}
    streams = bundle.get("streams")
    if not isinstance(streams, list) or len(streams) != 15:
        return {"status": "EMULATOR_RNG_INVENTORY_MISSING"}
    seen = set()
    for entry in streams:
        sid = entry.get("stream_id")
        if sid in seen or sid not in source_streams:
            return {"status": "EMULATOR_RNG_STREAM_ID_MISMATCH", "stream_id": sid}
        seen.add(sid)
        if entry.get("codec") != "sts2-v111-xoshiro256-starstar-v1":
            return {"status": "EMULATOR_RNG_CODEC_DIFFERENCE", "stream_id": sid}
        try:
            words = struct.unpack("<QQQQ", base64.b64decode(
                entry["state_bytes"], validate=True))
        except (KeyError, ValueError, struct.error):
            return {"status": "EMULATOR_RNG_WORDS_INVALID", "stream_id": sid}
        expected = source_streams[sid]
        native = (expected["Counter"], expected["State0"],
                  expected["State1"], expected["State2"], expected["State3"])
        emu = (entry.get("call_count"), *words)
        if native != emu:
            return {"status": "RNG_STREAM_DIFFERENCE", "stream_id": sid,
                    "native": native, "emulator": emu}
    return {"status": "SOURCE_BRACKETED_15_STREAMS_AGREE"}


def _selected_choice(native_choices, executor_id, crosswalk, legal):
    matches = [c for c in native_choices
               if c.get("containing_executor_action_id") == executor_id]
    if not matches:
        return None, None
    if len(matches) != 1 or matches[0].get("choice_type") != "CombatCard":
        return None, "UNSUPPORTED_NESTED_CHOICE"
    native_ids = matches[0].get("selected_combat_card_ids")
    if not isinstance(native_ids, list) or not native_ids:
        return None, "SOURCE_CHOICE_SELECTION_MISSING"
    if any(str(i) not in crosswalk["cards"] for i in native_ids):
        return None, "NESTED_CHOICE_CARD_UNMAPPED"
    wanted = [crosswalk["cards"][str(i)] for i in native_ids]
    candidates = []
    for a in legal:
        if a.get("kind") != "select_cards":
            continue
        try:
            payload = json.loads(a["payloadJson"])
        except (KeyError, TypeError, ValueError):
            return None, "INVALID_EMULATOR_SELECTION_PAYLOAD"
        selected = payload.get("CardInstanceIds", payload.get("card_instance_ids"))
        if selected == wanted:
            candidates.append(a["actionId"])
    if len(candidates) != 1:
        return None, "NESTED_CHOICE_NOT_UNIQUELY_LEGAL"
    return candidates[0], None


def replay(journal, snapshot, session, sidecar=None):
    """Return projected results; never label uncertified source as parity."""
    base = _report("BLOCKED_NATIVE_SOURCE", "Native journal/source cannot be replayed")
    if (not isinstance(journal, dict)
        or journal.get("schema") != "native-source-action-journal-v1"
        or journal.get("status") != "SOURCE_ACTION_CHRONOLOGY_OBSERVED"
        or journal.get("source_audit") != "CALLBACK_TIMELINE_CONSISTENT"
        or journal.get("errors") or journal.get("source_requests_without_executor_start")
        or journal.get("source_unlinked_canonical_executor_actions")):
        return base
    checkpoints = journal.get("source_turn_checkpoint_candidates") or []
    player = sorted([c for c in checkpoints if c.get("kind") == "PLAYER_TURN_START"],
                    key=lambda c: c["ordinal"])
    enemy = sorted([c for c in checkpoints if c.get("kind") == "ENEMY_TURN_START"],
                   key=lambda c: c["ordinal"])
    deferred = [{"turn": c["turn"], "source_ordinal": c["ordinal"],
                 "emulator_comparison": "DEFERRED_NO_PRE_ENEMY_EFFECT_BOUNDARY",
                 "native_settlement": "SETTLEMENT_UNVERIFIED"} for c in enemy]
    if not player or player[0].get("turn") != 1:
        return _report("BLOCKED_INITIAL_PLAYER_CHECKPOINT",
                       "No first-turn native player-start evidence", deferred=deferred)
    try:
        exact = json.loads(snapshot)
        if (exact.get("schema") != "sts2-exact-snapshot-v1"
            or exact["state"].get("run_seed") != journal.get("run_seed")
            or exact["state"].get("ascension") != journal.get("ascension")
            or _combat(exact) is None):
            raise ValueError("Independent emulator combat identity differs")
        crosswalk = derive_crosswalk(player[0]["source_state"], exact)
        state = session.load_snapshot(snapshot)
    except (ValueError, TypeError, KeyError, RuntimeError) as exc:
        return _report("BLOCKED_EMULATOR_BOOTSTRAP", str(exc), deferred=deferred)
    commands = sorted(journal["source_linked_commands"],
                      key=lambda c: c["executor_start_ordinal"])
    matched, rng_checks = [], []
    stepped = 0
    cp_index = 0
    previous_ordinal = 0

    for cmd in commands + [{"executor_start_ordinal": 10**18,
                            "_sentinel": True}]:
        start = cmd["executor_start_ordinal"]
        # Only check at a matching *semantic* player turn in the emulator:
        # an end_turn step has already resolved the entire previous enemy turn.
        while cp_index < len(player) and player[cp_index]["ordinal"] < start:
            cp = player[cp_index]
            if cp["ordinal"] <= previous_ordinal:
                return _report("BLOCKED_SOURCE_CHRONOLOGY",
                               "Player checkpoint inside a prior native executor",
                               matched=matched, deferred=deferred, stepped=stepped)
            try:
                exact_here = json.loads(state.save_snapshot())
                difference = first_difference(cp["source_state"], exact_here, crosswalk)
            except (ValueError, KeyError, TypeError, RuntimeError) as exc:
                difference = {"path": "$", "reason": type(exc).__name__ + ": " + str(exc)}
            if difference is not None:
                return _report("SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
                               "First projected difference at native player turn start",
                               matched=matched, deferred=deferred, stepped=stepped,
                               first={"turn": cp["turn"], "ordinal": cp["ordinal"],
                                      "difference": difference}, rng=rng_checks)
            evidence, basis = (_native_rng_window(sidecar, cp["ordinal"])
                               if sidecar is not None
                               else (None, "SOURCE_RNG_SIDECAR_NOT_PROVIDED"))
            detail = _check_rng(evidence, exact_here)
            detail.update({"turn": cp["turn"], "source_ordinal": cp["ordinal"],
                           "source_window": basis})
            rng_checks.append(detail)
            matched.append({"turn": cp["turn"], "ordinal": cp["ordinal"],
                            "comparison": "PROJECTED_MECHANICS_AGREE",
                            "native_settlement": "SETTLEMENT_UNVERIFIED",
                            "native_queue_quiescence": cp.get(
                                "queue_quiescence_status", "NOT_CAPTURED_LEGACY"),
                            "rng_comparison": detail["status"]})
            cp_index += 1
        if cmd.get("_sentinel"):
            break
        action = cmd.get("canonical_action")
        if (type(start) is not int or start <= previous_ordinal
            or not isinstance(action, dict)
            or not isinstance(cmd.get("executor_complete_ordinal"), int)):
            return _report("BLOCKED_NATIVE_ACTION_ORDER",
                           "Native action order/canonical command missing",
                           matched=matched, deferred=deferred, stepped=stepped)
        previous_ordinal = cmd["executor_complete_ordinal"]
        try:
            legal = state.actions()
            action_id, problem = _resolve(
                action, legal, crosswalk["cards"], crosswalk["enemies"])
            if problem:
                return _report("BLOCKED_EMULATOR_LEGAL_ACTION",
                               problem, matched=matched, deferred=deferred,
                               stepped=stepped,
                               first={"executor_action_id": cmd.get("executor_action_id"),
                                      "ordinal": start}, rng=rng_checks)
            state = state.step(action_id)
            stepped += 1
            nested = [c for c in journal.get("native_choice_results", [])
                      if c.get("containing_executor_action_id")
                      == cmd.get("executor_action_id")]
            if nested:
                chosen, error = _selected_choice(
                    journal["native_choice_results"],
                    cmd["executor_action_id"], crosswalk, state.actions())
                if error:
                    return _report("BLOCKED_NESTED_SOURCE_CHOICE", error,
                                   matched=matched, deferred=deferred, stepped=stepped,
                                   first={"ordinal": start}, rng=rng_checks)
                state = state.step(chosen)
                stepped += 1
        except (RuntimeError, TypeError, KeyError, ValueError) as exc:
            return _report("BLOCKED_EMULATOR_STEP",
                           type(exc).__name__ + ": " + str(exc),
                           matched=matched, deferred=deferred, stepped=stepped,
                           first={"ordinal": start}, rng=rng_checks)
    status = ("SOURCE_PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED"
              if len(matched) == len(player) else "BLOCKED_MISSING_PLAYER_CHECKPOINTS")
    return _report(status, "Independent emulator replay completed at observed player starts; "
                   "native settlement and full continuation remain unverified",
                   matched=matched, deferred=deferred, stepped=stepped, rng=rng_checks)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--journal", type=Path, required=True)
    parser.add_argument("--prelude", type=Path)
    parser.add_argument("--snapshot", type=Path)
    parser.add_argument("--sidecar", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path, default=(
        ROOT / "src" / "Sts2Emulator.Cli" / "bin" / "Release"
        / "net9.0" / "Sts2Emulator.Cli.dll"))
    args = parser.parse_args()
    report = None
    if (args.prelude is None) == (args.snapshot is None):
        parser.error("Provide exactly one of --prelude or --snapshot")
    journal = json.loads(args.journal.read_text(encoding="utf-8"))
    sidecar = None
    if args.sidecar:
        sidecar, error = load_sidecar(args.sidecar)
        if error:
            parser.error("Native sidecar could not be parsed: " + error)
        regenerated = build_source_journal(sidecar)
        if (regenerated.get("status") != "SOURCE_ACTION_CHRONOLOGY_OBSERVED"
            or regenerated.get("run_id") != journal.get("run_id")
            or regenerated.get("source_linked_commands") !=
                journal.get("source_linked_commands")
            or regenerated.get("source_turn_checkpoint_candidates") !=
                journal.get("source_turn_checkpoint_candidates")
            or regenerated.get("native_choice_results") !=
                journal.get("native_choice_results")):
            parser.error("Source sidecar and journal do not describe the same audited run")
    from sts2_emulator import Session
    with Session([args.dotnet, str(args.cli_dll), "prototype-ai-jsonl"]) as session:
        if args.prelude:
            records, error = load_prelude(args.prelude)
            source = audit_prelude(records, error)
            pairing = check_prelude_pair(journal, source)
            if pairing:
                report = _report("BLOCKED_NATIVE_PRELUDE_PAIRING", pairing)
            else:
                bootstrap, snapshot = run_bootstrap(source, session)
                if (snapshot is None
                    or bootstrap["status"] != "CANDIDATE_COMBAT_REACHED_UNCERTIFIED"):
                    report = _report("BLOCKED_EMULATOR_BOOTSTRAP",
                                     bootstrap["status"] + ": " + bootstrap["reason"],
                                     first=bootstrap.get("first_difference_or_blocker"))
                else:
                    report = replay(journal, snapshot, session, sidecar)
                report["bootstrap_status"] = bootstrap["status"]
                report["bootstrap_steps"] = bootstrap.get("emulator_steps", [])
                report["bootstrap_checks_summary"] = {
                    key: (value.get("status") if isinstance(value, dict) else value)
                    for key, value in bootstrap.get("checks", {}).items()
                    if key.endswith("_comparison")
                    or key in ("native_map_rng_alignment",
                               "native_map_topology_parity",
                               "native_first_combat_identity",
                               "native_upfront_full_queues",
                               "native_upfront_startup")}
        else:
            report = replay(journal, args.snapshot.read_text(encoding="utf-8"),
                            session, sidecar)
            report["bootstrap_status"] = "INDEPENDENT_SNAPSHOT_SUPPLIED"
    args.out.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n",
                        encoding="utf-8")
    print(json.dumps({"status": report["status"],
                      "player_turns_matched": len(report["player_checkpoints"]),
                      "enemy_turns_deferred": len(report["enemy_checkpoint_candidates"]),
                      "emulator_steps": report["emulator_canonical_actions_stepped"]}))
    return 0 if report["status"] == (
        "SOURCE_PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED") else 2


if __name__ == "__main__":
    raise SystemExit(main())
