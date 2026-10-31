#!/usr/bin/env python3
"""Opt-in persistent emulator shadow stepping from source-observed native actions.

Uses the existing version-negotiated STS2 JSONL Session and a true emulator
exact snapshot supplied separately. Native visible states are partial and
action-completion callbacks are not certified settled decision boundaries.
Consequently *no result from this tool is a native/emulator fidelity verdict*.

The native-to-emulator card/creature ID crosswalk must be explicit. Absent
identity or bootstrap evidence fails closed without stepping the emulator.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def _result(status: str, reason: str, *, first=None, transitions=None,
            source_actions=0, mapped_actions=0) -> dict:
    return {
        "schema": "native-lockstep-shadow-v1",
        "status": status,
        "reason": reason,
        "first_observation": first,
        "source_action_count": source_actions,
        "emulator_actions_stepped": mapped_actions,
        "transitions": transitions or [],
        "native_game_parity": "NOT_TESTED",
        "emulator_rng_alignment": "NOT_TESTED",
        "native_boundary_settlement": "UNVERIFIED",
        "input_origin": "UNVERIFIED",
        "verified_native_state_import": False,
    }


def _dict_int_map(obj, field):
    if not isinstance(obj, dict):
        raise ValueError(field + " must be an object")
    result = {}
    for k, v in obj.items():
        if not isinstance(k, str) or not k or type(v) is not int or v < 0:
            raise ValueError("Invalid " + field + " mapping entry")
        result[k] = v
    if len(set(result.values())) != len(result):
        raise ValueError("Duplicate destination IDs in " + field)
    return result


def check_inputs(journal: dict, snapshot: str | None,
                 crosswalk: dict | None) -> tuple[str, str] | None:
    if not isinstance(journal, dict):
        return "BLOCKED_NATIVE_SOURCE", "Journal must be a JSON object"
    if (journal.get("schema") != "native-source-action-journal-v1"
        or journal.get("status") != "SOURCE_ACTION_CHRONOLOGY_OBSERVED"
        or journal.get("source_audit") != "CALLBACK_TIMELINE_CONSISTENT"
        or journal.get("source_unlinked_canonical_executor_actions")
        or journal.get("errors")
        or not isinstance(journal.get("source_linked_commands"), list)):
        return "BLOCKED_NATIVE_SOURCE", "Missing or incomplete native source action chronology"
    if (not isinstance(journal.get("run_seed"), str)
        or not journal["run_seed"]
        or type(journal.get("ascension")) is not int):
        return "BLOCKED_NATIVE_IDENTITY", "Original native run seed/ascension is not verified"
    if snapshot is None:
        return "BLOCKED_EMULATOR_BOOTSTRAP", (
            "Requires an independently aligned emulator exact snapshot; "
            "a seed-only reset starts at RunStart, not the native combat")
    try:
        exact = json.loads(snapshot)
    except (ValueError, TypeError):
        return "BLOCKED_EMULATOR_SNAPSHOT", "Invalid emulator exact snapshot JSON"
    if (not isinstance(exact, dict)
        or exact.get("schema") != "sts2-exact-snapshot-v1"
        or not isinstance(exact.get("state"), dict)):
        return "BLOCKED_EMULATOR_SNAPSHOT", "Expected sts2-exact-snapshot-v1 envelope"
    state = exact["state"]
    # RunSnapshot uses canonical JSON; C# phase enums can serialize as
    # numeric values. Require the actual combat continuation instead.
    world = state.get("world")
    combat = world.get("combat") if isinstance(world, dict) else None
    if (state.get("run_seed") != journal["run_seed"]
        or state.get("ascension") != journal["ascension"]
        or not isinstance(combat, dict)):
        return "BLOCKED_EMULATOR_IDENTITY", (
            "Emulator exact snapshot must match native seed/ascension and "
            "contain a real combat continuation")
    if not isinstance(crosswalk, dict) or crosswalk.get("schema") != (
            "native-emulator-id-crosswalk-v1"):
        return "BLOCKED_ID_CROSSWALK", "Explicit native-emulator identity crosswalk required"
    try:
        _dict_int_map(crosswalk.get("cards"), "cards")
        _dict_int_map(crosswalk.get("enemies"), "enemies")
    except ValueError as exc:
        return "BLOCKED_ID_CROSSWALK", str(exc)
    return None


def _native_emulator_observation_difference(native: dict, emulator: dict,
                                            cards: dict) -> dict | None:
    # These are *visible structural comparisons* only, not certified stable
    # boundaries; report the first discrepant value without claiming parity.
    if not isinstance(native, dict) or not isinstance(emulator, dict):
        return {"path": "$", "reason": "NATIVE_OR_EMULATOR_STATE_MISSING"}
    player, combat = native.get("player"), native.get("combat")
    ecombat = emulator.get("combat")
    if not isinstance(player, dict) or not isinstance(combat, dict):
        return {"path": "$", "reason": "SOURCE_COMBAT_PROJECTION_MISSING"}
    if not isinstance(ecombat, dict):
        return {"path": "$.combat", "reason": "EMULATOR_NOT_IN_COMBAT"}
    for native_path, na, ea in (
        ("$.player.hp", player.get("hp"), emulator.get("hp")),
        ("$.player.max_hp", player.get("max_hp"), emulator.get("max_hp")),
        ("$.player.block", player.get("block"), ecombat.get("player_block")),
        ("$.combat.energy", combat.get("energy"), ecombat.get("energy")),
        ("$.combat.turn", combat.get("turn"), ecombat.get("turn"))):
        if type(na) is not int or type(ea) is not int:
            return {"path": native_path, "reason": "UNOBSERVED_SCALAR",
                    "native": na, "emulator": ea}
        if na != ea:
            return {"path": native_path, "reason": "VISIBLE_SCALAR_DIFFERENCE",
                    "native": na, "emulator": ea}
    nh = combat.get("hand")
    eh = ecombat.get("hand")
    if not isinstance(nh, list) or not isinstance(eh, list):
        return {"path": "$.combat.hand", "reason": "HAND_PROJECTION_MISSING"}
    for item in nh:
        if not isinstance(item, dict) or type(item.get("instance_id")) is not int:
            return {"path": "$.combat.hand", "reason": "INVALID_NATIVE_CARD"}
        key = str(item["instance_id"])
        if key not in cards:
            return {"path": "$.combat.hand", "reason": "CARD_CROSSWALK_INCOMPLETE",
                    "native_instance_id": item["instance_id"]}
        matched = [card for card in eh if card.get("instance_id") == cards[key]]
        if len(matched) != 1:
            return {"path": "$.combat.hand", "reason": "EMULATOR_CARD_UNMAPPED",
                    "native_instance_id": item["instance_id"]}
        actual_model = item.get("model_id")
        emu_model = matched[0].get("card_id")
        if (not isinstance(actual_model, str)
            or not isinstance(emu_model, str)
            or actual_model.casefold() != emu_model.casefold()):
            return {"path": "$.combat.hand", "reason": "CARD_MODEL_DIFFERENCE",
                    "native": actual_model, "emulator": emu_model}
    if len(nh) != len(eh):
        return {"path": "$.combat.hand", "reason": "HAND_COUNT_DIFFERENCE",
                "native": len(nh), "emulator": len(eh)}
    return None


def _resolve(action: dict, legal: list[dict], cards: dict,
             enemies: dict) -> tuple[str | None, str | None]:
    kind = action.get("kind")
    payload = action.get("payload")
    if kind == "end_turn" and isinstance(payload, dict) and not payload:
        expected = {}
    elif kind == "play_card" and isinstance(payload, dict):
        card_id = payload.get("card_instance_id")
        target_id = payload.get("target_id")
        if type(card_id) is not int or str(card_id) not in cards:
            return None, "NATIVE_CARD_ID_UNMAPPED"
        if target_id is not None and (
                not isinstance(target_id, str) or target_id not in enemies):
            return None, "NATIVE_TARGET_ID_UNMAPPED"
        expected = {"card_instance_id": cards[str(card_id)],
                    "target_enemy_id": (
                        enemies[target_id] if target_id is not None else None)}
    else:
        return None, "SOURCE_ACTION_KIND_UNSUPPORTED"
    matching = []
    for candidate in legal:
        if candidate.get("kind") != kind:
            continue
        try:
            emu_payload = json.loads(candidate["payloadJson"])
        except (ValueError, KeyError, TypeError):
            return None, "INVALID_EMULATOR_ACTION_WIRE"
        if emu_payload == expected:
            matching.append(candidate["actionId"])
    if len(matching) != 1:
        return None, ("ACTION_NOT_IN_EMULATOR_LEGAL_MENU"
                      if not matching else "AMBIGUOUS_EMULATOR_ACTION")
    return matching[0], None


def run_shadow(journal: dict, snapshot: str | None, crosswalk: dict | None,
               session: Any | None) -> dict:
    total = (len(journal.get("source_linked_commands", []))
             if isinstance(journal, dict)
             and isinstance(journal.get("source_linked_commands"), list) else 0)
    check = check_inputs(journal, snapshot, crosswalk)
    if check is not None:
        return _result(*check, source_actions=total)
    if session is None:
        return _result("BLOCKED_EMULATOR_SESSION",
                       "No persistent emulator JSONL session supplied",
                       source_actions=total)

    card_ids = _dict_int_map(crosswalk["cards"], "cards")
    enemy_ids = _dict_int_map(crosswalk["enemies"], "enemies")
    transitions = []
    stepped = 0
    try:
        state = session.load_snapshot(snapshot)
        if not hasattr(state, "actions") or not hasattr(state, "observe"):
            raise TypeError("JSONL state handle lacks actions/observe")
    except (Exception) as exc:
        return _result("BLOCKED_EMULATOR_SNAPSHOT_LOAD",
                       "Emulator exact snapshot rejected: " + type(exc).__name__,
                       source_actions=total)

    for index, command in enumerate(journal["source_linked_commands"]):
        action = command.get("canonical_action")
        eid = command.get("executor_action_id")
        if not isinstance(action, dict):
            return _result("BLOCKED_NATIVE_COMMAND",
                           "Missing canonical source action",
                           first={"command_index": index, "executor_action_id": eid},
                           source_actions=total, mapped_actions=stepped,
                           transitions=transitions)
        # A suspended native action may contain additional player decisions;
        # the source journal records them, but the emulator's pending-choice
        # transition is a distinct legal command with its own semantics.
        nested = [c for c in journal.get("native_choice_results", [])
                  if c.get("containing_executor_action_id") == eid]
        if nested:
            return _result(
                "BLOCKED_NESTED_CHOICE_CONTINUATION",
                "Nested native choices need a separate canonical action boundary",
                first={"command_index": index, "executor_action_id": eid,
                       "choice_ids": [c.get("choice_id") for c in nested]},
                source_actions=total, mapped_actions=stepped,
                transitions=transitions)
        try:
            frame = state.observe()
        except Exception as exc:
            return _result("BLOCKED_EMULATOR_OBSERVATION",
                           "Emulator observation unavailable: " + type(exc).__name__,
                           first={"command_index": index},
                           source_actions=total, mapped_actions=stepped,
                           transitions=transitions)
        before = _native_emulator_observation_difference(
            command.get("pre_request_state"), frame, card_ids)
        if before is not None:
            return _result("BLOCKED_VISIBLE_PRESTATE_ALIGNMENT",
                           "Native pre-request and emulator visible projections differ; "
                           "this is not a certified state divergence",
                           first={"command_index": index,
                                  "executor_action_id": eid,
                                  "difference": before},
                           source_actions=total, mapped_actions=stepped,
                           transitions=transitions)
        try:
            legal = state.actions()
        except Exception as exc:
            return _result("BLOCKED_EMULATOR_LEGAL_ACTIONS",
                           "Emulator legal actions unavailable: " + type(exc).__name__,
                           first={"command_index": index},
                           source_actions=total, mapped_actions=stepped,
                           transitions=transitions)
        action_id, reason = _resolve(action, legal, card_ids, enemy_ids)
        if reason:
            return _result("BLOCKED_ACTION_ALIGNMENT", reason,
                           first={"command_index": index,
                                  "executor_action_id": eid},
                           source_actions=total, mapped_actions=stepped,
                           transitions=transitions)
        try:
            child = state.step(action_id)
            after_frame = child.observe()
        except Exception as exc:
            return _result("BLOCKED_EMULATOR_STEP",
                           "Emulator transition failed: " + type(exc).__name__,
                           first={"command_index": index,
                                  "executor_action_id": eid},
                           source_actions=total, mapped_actions=stepped,
                           transitions=transitions)
        stepped += 1
        after = _native_emulator_observation_difference(
            command.get("post_execution_state"), after_frame, card_ids)
        transitions.append({
            "command_index": index,
            "executor_action_id": eid,
            "source_input_request_id": command.get("input_request_id"),
            "emulator_action_id": action_id,
            "emulator_transition_executed": True,
            "native_after_executor_ordinal": command.get("executor_complete_ordinal"),
            "visible_post_projection_agrees": after is None,
            "difference": after,
            "native_settlement": "UNVERIFIED",
            "rng_alignment": "NOT_TESTED",
        })
        if after is not None:
            return _result(
                "SHADOW_VISIBLE_DIFFERENCE_UNCERTIFIED",
                "Source action completion is not an audited settled decision boundary",
                first={"command_index": index, "executor_action_id": eid,
                       "difference": after},
                source_actions=total, mapped_actions=stepped,
                transitions=transitions)
        state = child  # Preserve one evolving state handle in one JSONL session.
    return _result(
        "SHADOW_STEPPED_VISIBLE_ONLY",
        "Source actions stepped in one emulator session with matching visible projections; "
        "native state continuation and RNG equivalence remain unverified",
        source_actions=total, mapped_actions=stepped,
        transitions=transitions)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--journal", type=Path)
    source.add_argument("--sidecar", type=Path)
    parser.add_argument("--emulator-snapshot", type=Path)
    parser.add_argument("--identity-crosswalk", type=Path)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    try:
        if args.sidecar:
            from native_lockstep_source_journal import build as build_journal
            from native_action_envelope_audit import load as load_sidecar
            records, error = load_sidecar(args.sidecar)
            journal = build_journal(records, error)
        else:
            journal = json.loads(args.journal.read_text(encoding="utf-8"))
        snapshot = (args.emulator_snapshot.read_text(encoding="utf-8")
                    if args.emulator_snapshot else None)
        crosswalk = (json.loads(args.identity_crosswalk.read_text(encoding="utf-8"))
                     if args.identity_crosswalk else None)
        if isinstance(snapshot, str):
            # A save_snapshot result may be stored either as the exact raw
            # JSON string or as a JSON-encoded string; handle both explicitly.
            parsed = json.loads(snapshot)
            if isinstance(parsed, str):
                snapshot = parsed
    except (OSError, ValueError, TypeError) as exc:
        report = _result("BLOCKED_INPUT_FILES", str(exc))
    else:
        check = check_inputs(journal, snapshot, crosswalk)
        if check is not None:
            report = run_shadow(journal, snapshot, crosswalk, None)
        elif args.cli_dll is None:
            report = _result(
                "BLOCKED_EMULATOR_SESSION",
                "Provide --cli-dll for the pinned emulator JSONL server",
                source_actions=len(journal["source_linked_commands"]))
        else:
            import sys
            sys.path.insert(0, str(Path(__file__).resolve().parents[1] /
                                   "bindings" / "python"))
            from sts2_emulator import Session
            try:
                with Session([args.dotnet, str(args.cli_dll), "prototype-ai-jsonl"]) as session:
                    report = run_shadow(journal, snapshot, crosswalk, session)
            except Exception as exc:
                report = _result("BLOCKED_EMULATOR_SESSION",
                                 "Persistent JSONL session failed: " +
                                 type(exc).__name__)
    output = json.dumps(report, indent=2, sort_keys=True) + "\n"
    print(output, end="")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return 0 if report["status"] == "SHADOW_STEPPED_VISIBLE_ONLY" else 2


if __name__ == "__main__":
    raise SystemExit(main())
