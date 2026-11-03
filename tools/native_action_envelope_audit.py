#!/usr/bin/env python3
"""Audit native action-executor sidecars independently of player-decision traces.

Each executor callback is an observation; the action sidecar remains useful
when a nested choice makes the strict semantic decision trace incomplete.
Observed history order is distinct from proven game-source hook order.
"""
from __future__ import annotations

import argparse
import json
import math
from native_phase_handoff_audit import audit_phase_handoffs
from native_action_window_audit import audit_action_windows
from native_intermediate_rng_audit import audit_native_rng_windows
from collections import Counter
from pathlib import Path


def _is_json_number(value: object) -> bool:
    """Accept float lexeme-preserving JSON parsers while rejecting booleans.

    Fidelity transport retains native scientific-notation spelling inside a
    float subclass so that its original C# payload fingerprint is verifiable.
    Source mechanics audit must check numeric value rather than parser class.
    """
    return isinstance(value, (int, float)) and not isinstance(value, bool)


QUEUE_QUIESCENCE_FLAGS = (
    "queue_is_empty", "queue_completion_succeeded",
    "executor_not_running", "executor_completion_succeeded",
    "no_currently_running_action", "executor_not_paused",
    "synchronizer_play_phase", "no_deferred_player_requests",
)


AUTOMATIC_EFFECT_FLAGS = (
    "no_active_card_or_potion_effect_body",
    "no_player_turn_end_transition",
    "enemy_turn_not_started",
)


def automatic_effect_evidence_status(data: dict, *, player_start: bool) -> str:
    """Validate bounded source effect-depth evidence, not global settlement."""
    if ("automatic_effect_evidence" not in data
        and "automatic_effect_evidence_status" not in data):
        return "NOT_CAPTURED_LEGACY"
    evidence = data.get("automatic_effect_evidence")
    status = data.get("automatic_effect_evidence_status")
    if evidence is None:
        if status != "NOT_CAPTURED":
            raise ValueError("Missing tracked-effect evidence cannot imply idle effects")
        return status
    if (not player_start or not isinstance(evidence, dict)
        or set(evidence) != set(AUTOMATIC_EFFECT_FLAGS)
        or any(type(evidence[k]) is not bool for k in AUTOMATIC_EFFECT_FLAGS)):
        raise ValueError("Malformed or misplaced source tracked-effect evidence")
    expected = (
        "SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME"
        if all(evidence[k] for k in AUTOMATIC_EFFECT_FLAGS)
        else "SOURCE_TRACKED_EFFECT_ACTIVITY_UNVERIFIED"
    )
    if status != expected:
        raise ValueError("Tracked-effect status contradicts native source flags")
    return status


def queue_quiescence_status(data: dict, *, player_start: bool) -> str:
    """Validate optional native source snapshot, preserving old sidecars.

    This proves a queue/executor observation at *one frame*, never that
    future async hooks cannot enqueue further automatic game actions.
    """
    if ("queue_evidence" not in data and
        "queue_quiescence_status" not in data):
        return "NOT_CAPTURED_LEGACY"
    evidence = data.get("queue_evidence")
    status = data.get("queue_quiescence_status")
    if evidence is None:
        if status != "NOT_CAPTURED":
            raise ValueError("Missing queue evidence cannot imply quiescence")
        return status
    if (not player_start or not isinstance(evidence, dict)
        or set(evidence) != set(QUEUE_QUIESCENCE_FLAGS)
        or any(type(evidence[k]) is not bool
               for k in QUEUE_QUIESCENCE_FLAGS)):
        raise ValueError("Malformed or misplaced native queue evidence")
    expected = (
        "SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME"
        if all(evidence[k] for k in QUEUE_QUIESCENCE_FLAGS)
        else "SOURCE_QUEUE_QUIESCENCE_UNVERIFIED"
    )
    if status != expected:
        raise ValueError("Native queue quiescence claim contradicts source flags")
    return status


def diff_scalars(before: dict, after: dict) -> dict:
    """Small measured delta, with no card-rule assumptions or RNG replay."""
    values: dict = {}
    for label, path in {
        "player_hp": ("player", "hp"),
        "player_block": ("player", "block"),
        "energy": ("combat", "energy"),
        "turn": ("combat", "turn"),
    }.items():
        try:
            x, y = before[path[0]][path[1]], after[path[0]][path[1]]
            if isinstance(x, int) and isinstance(y, int) and x != y:
                values[label] = {"before": x, "after": y}
        except (KeyError, TypeError):
            pass
    try:
        if before["player"].get("powers") != after["player"].get("powers"):
            values["player_powers"] = {
                "before": before["player"].get("powers"),
                "after": after["player"].get("powers"),
            }
    except (KeyError, TypeError):
        pass
    try:
        pre = {x["id"]: x for x in before["combat"]["creatures"]}
        post = {x["id"]: x for x in after["combat"]["creatures"]}
        for identifier in sorted(pre.keys() | post.keys()):
            if identifier not in pre or identifier not in post:
                values.setdefault("creatures", {})[identifier] = "added_or_removed"
                continue
            for field in ("hp", "max_hp", "block", "alive", "powers"):
                if pre[identifier].get(field) != post[identifier].get(field):
                    values.setdefault("creatures", {}).setdefault(
                        identifier, {})[field] = {
                            "before": pre[identifier].get(field),
                            "after": post[identifier].get(field),
                        }
    except (KeyError, TypeError):
        pass
    try:
        for zone in ("hand", "draw_pile", "discard_pile", "exhaust_pile"):
            x = [c["instance_id"] for c in before["combat"][zone]]
            y = [c["instance_id"] for c in after["combat"][zone]]
            if x != y:
                values.setdefault("zones", {})[zone] = {
                    "before": x, "after": y,
                }
    except (KeyError, TypeError):
        pass
    return values


def infer_choice_discard(before: dict, after: dict) -> dict:
    """Observed card movement supporting a nested discard-choice candidate.

    This is a state-delta inference, not a native semantic choice callback.
    """
    try:
        initial_hand = {c["instance_id"] for c in before["combat"]["hand"]}
        final_hand = {c["instance_id"] for c in after["combat"]["hand"]}
        initial_discard = {c["instance_id"] for c in before["combat"]["discard_pile"]}
        final_discard = {c["instance_id"] for c in after["combat"]["discard_pile"]}
        candidates = sorted((initial_hand - final_hand) & (
            final_discard - initial_discard))
    except (KeyError, TypeError, ValueError):
        candidates = []
    return {
        "candidate_discarded_card_instance_id": (
            candidates[0] if len(candidates) == 1 else None),
        "candidate_count": len(candidates),
        "provenance": "INFERRED_FROM_CARD_ZONE_DIFFERENCE",
        "semantic_selection_verified": False,
    }


def observed_indicators(actions: list[dict], records: list[dict]) -> dict:
    """Counts directly observed changes; absence is UNOBSERVED, not a pass."""
    result: dict[str, dict] = {}
    for label in (
        "card_play", "end_turn", "non_player_or_unknown_action",
        "player_hp_change", "energy_change", "block_change",
        "creature_hp_change", "power_change", "card_zone_change",
        "exhaust_pile_change", "creature_set_change",
        "nested_choice_pause", "potion_history_entry",
    ):
        result[label] = {"count": 0, "status": "UNOBSERVED",
                         "counts_by_observer": {"action": 0, "frame": 0, "history": 0}}

    def add(name: str, origin: str = "action"):
        # The same mechanical change can appear both inside an action
        # snapshot and again in a frame delta. Do not add those two
        # overlapping observation streams as if they were distinct effects.
        counts = result[name]["counts_by_observer"]
        counts[origin] += 1
        result[name]["count"] = max(counts.values())
        result[name]["status"] = "OBSERVED_UNVERIFIED_MECHANICS"

    for action in actions:
        canonical = action.get("canonical_action") or {}
        kind = canonical.get("kind")
        if kind == "play_card":
            add("card_play")
        elif kind == "end_turn":
            add("end_turn")
        else:
            add("non_player_or_unknown_action")
        delta = action.get("observed_delta") or {}
        if "player_hp" in delta:
            add("player_hp_change")
        if "energy" in delta:
            add("energy_change")
        if "player_block" in delta or any(
                isinstance(info, dict) and "block" in info
                for info in delta.get("creatures", {}).values()):
            add("block_change")
        if any(isinstance(info, dict) and "hp" in info
               for info in delta.get("creatures", {}).values()):
            add("creature_hp_change")
        if "player_powers" in delta or any(isinstance(info, dict) and "powers" in info
               for info in delta.get("creatures", {}).values()):
            add("power_change")
        if delta.get("zones"):
            add("card_zone_change")
        if "exhaust_pile" in delta.get("zones", {}):
            add("exhaust_pile_change")
        if "added_or_removed" in delta.get("creatures", {}).values():
            add("creature_set_change")
    # Between-action effects (notably enemy-turn HP changes) can happen
    # outside executor action envelopes. Frame snapshots cover those changes.
    last_frame = None
    for record in records:
        if record.get("type") == "frame_state" and isinstance(record.get("state"), dict):
            current = record["state"]
            if last_frame is not None:
                delta = diff_scalars(last_frame, current)
                if "player_hp" in delta: add("player_hp_change", "frame")
                if "player_block" in delta: add("block_change", "frame")
                if "energy" in delta: add("energy_change", "frame")
                if "player_powers" in delta: add("power_change", "frame")
                if delta.get("zones"): add("card_zone_change", "frame")
                if "exhaust_pile" in delta.get("zones", {}): add("exhaust_pile_change", "frame")
                if any(isinstance(info, dict) and "hp" in info
                       for info in delta.get("creatures", {}).values()):
                    add("creature_hp_change", "frame")
            last_frame = current
        if record.get("type") == "nested_choice_pause":
            add("nested_choice_pause", "history")
        if (record.get("type") == "history_entry" and
                record.get("native_type") == "potion_used"):
            add("potion_history_entry", "history")
    return result


def _unchanged_combat_end_checkpoint(records: list[dict], reset_ordinal: int) -> bool:
    """Recognize the pinned v111 terminal callback sequence, fail-closed.

    An after_action checkpoint, optionally followed by read-only frame polls,
    precedes the history reset; a combat_end checkpoint follows it before
    the terminal record. This only verifies equality of *observed* streams.
    """
    if reset_ordinal + 1 >= len(records):
        return False
    end_checkpoint, terminal = records[reset_ordinal:reset_ordinal + 2]
    if (end_checkpoint.get("type") != "native_rng_stream_checkpoint"
        or terminal.get("type") != "terminal"):
        return False
    end = end_checkpoint.get("data") or {}
    if (end.get("phase") != "combat_end"
        or end.get("source") != "native_rng_object_to_serializable"
        or end.get("rng_draws_performed") != 0
        or not isinstance(end.get("streams"), list)
        or not end["streams"]):
        return False

    # reset_ordinal is 1-based, so the last record before the reset
    # is records[reset_ordinal - 2]. Accept intervening frame polls only.
    prior_index = reset_ordinal - 2
    while prior_index >= 0 and records[prior_index].get("type") == "frame_state":
        prior_index -= 1
    if prior_index < 1:
        return False
    preceding = records[prior_index]
    before = preceding.get("data") or {}
    if (preceding.get("type") != "native_rng_stream_checkpoint"
        or records[prior_index - 1].get("type") != "action_completed"
        or before.get("phase") != "after_action"
        or before.get("source") != end.get("source")
        or before.get("rng_draws_performed") != 0
        or before.get("scope") != end.get("scope")
        or before.get("complete_native_rng_inventory")
            != end.get("complete_native_rng_inventory")
        or before.get("exact_rng_parity") != end.get("exact_rng_parity")
        or before.get("streams") != end.get("streams")):
        return False
    return True


def audit(records: list[dict], error: str | None = None) -> dict:
    report = {
        "schema": "native-action-envelope-audit-v1",
        "status": "INCOMPLETE_CAPTURE",
        "reason": error or "",
        "native_game_parity": "NOT_TESTED",
        "native_source_hook_order": "NOT_TESTED",
        "exact_rng_parity": "NOT_TESTED",
        "record_counts": dict(Counter(r.get("type") for r in records)),
        "actions": [],
        "nested_choices": [],
        "strict_input_gaps": [],
        "native_input_hook_installed": False,
        "native_hand_menu_hook_installed": False,
        "native_hand_choice_flow_hook_installed": False,
        "native_manual_play_hook_installed": False,
        "native_ui_play_begin_hook_installed": False,
        "native_ui_gate_hook_installed": False,
        "native_ui_hand_gates": [],
        "native_ui_play_begins": [],
        "turn_boundary_candidates": [],
        "source_queue_quiescent_player_checkpoints": 0,
        "source_tracked_effect_idle_player_checkpoints": 0,
        "native_phase_handoff": None,
        "post_card_windows": None,
        "post_card_tracked_rng_windows": None,
        "player_checkpoint_phase_callback_pairs": 0,
        "player_checkpoints_without_observed_start_callback": 0,
        "native_input_requests": [],
        "native_input_outcomes": [],
        "native_player_choices": [],
        "native_hand_menus": [],
        "native_hand_menu_revisions": [],
        "native_hand_menu_closures": [],
        "native_hand_menu_open_lifetimes": [],
        "native_hand_menu_choice_matches": [],
        "native_manual_play_results": [],
        "native_run_identity": None,
        "source_request_execution_links": 0,
        "observed_mechanic_indicators": {},
        "nested_choice_pauses": 0,
        "unattributed_history_entries": 0,
        "history_entries_with_missing_fields": 0,
        "frame_states": 0,
        "action_resumptions": 0,
        "legacy_resume_candidates": 0,
        "terminal_history_clears": 0,
        "raw_terminal_status": None,
        "native_rng_hook_installed": False,
        "native_rng_double_hook_installed": False,
        "native_rng_bool_hook_installed": False,
        "native_rng_float_hook_installed": False,
        "native_rng_stream_checkpoints_enabled": False,
        "native_rng_stream_checkpoint_count": 0,
        "native_monster_ai_scope_installed": False,
        "native_shuffle_scope_installed": False,
        "native_initial_hp_scope_installed": False,
        "native_initial_hp_source_rolls": 0,
        "native_shuffle_source_operations": 0,
        "native_rng_source_calls": 0,
        "native_rng_scalar_source_calls": 0,
        "native_rng_float_source_calls": 0,
        "native_rng_unattributed_calls": 0,
        "native_rng_source_coverage": "SOURCE_RNG_UNINSTRUMENTED",
        "incomplete_observations": [],
    }
    if error:
        return report
    if not records or records[0].get("type") != "header":
        report["reason"] = "Missing header"
        return report
    if records[0].get("data", {}).get("schema") != "native-action-envelope-v1":
        report["reason"] = "Unexpected sidecar schema"
        return report
    report["native_input_hook_installed"] = (
        records[0].get("data", {}).get("native_input_hook_installed") is True)
    report["native_hand_menu_hook_installed"] = (
        records[0].get("data", {}).get("native_hand_menu_hook_installed") is True)
    report["native_hand_choice_flow_hook_installed"] = (
        records[0].get("data", {}).get("native_hand_choice_flow_hook_installed") is True)
    report["native_manual_play_hook_installed"] = (
        records[0].get("data", {}).get("native_manual_play_hook_installed") is True)
    report["native_ui_play_begin_hook_installed"] = (
        records[0].get("data", {}).get("native_ui_play_begin_hook_installed") is True)
    report["native_ui_gate_hook_installed"] = (
        records[0].get("data", {}).get("native_ui_gate_hook_installed") is True)
    report["native_rng_hook_installed"] = (
        records[0].get("data", {}).get("native_rng_hook_installed") is True)
    report["native_rng_double_hook_installed"] = (
        records[0].get("data", {}).get("native_rng_double_hook_installed") is True)
    report["native_rng_bool_hook_installed"] = (
        records[0].get("data", {}).get("native_rng_bool_hook_installed") is True)
    report["native_rng_float_hook_installed"] = (
        records[0].get("data", {}).get("native_rng_float_hook_installed") is True)
    report["native_rng_stream_checkpoints_enabled"] = (
        records[0].get("data", {}).get("native_rng_stream_checkpoints_enabled") is True)
    report["native_monster_ai_scope_installed"] = (
        records[0].get("data", {}).get("native_monster_ai_scope_installed") is True)
    report["native_shuffle_scope_installed"] = (
        records[0].get("data", {}).get("native_shuffle_scope_installed") is True)
    report["native_initial_hp_scope_installed"] = (
        records[0].get("data", {}).get("native_initial_hp_scope_installed") is True)
    if report["native_rng_hook_installed"]:
        report["native_rng_source_coverage"] = (
            "HOOK_INSTALLED_NO_SOURCE_CALLS_OBSERVED")
    if records[-1].get("type") != "terminal":
        report["reason"] = "Missing terminal record"
        return report
    allowed = {
        "header", "history_baseline", "frame_state", "action_started", "action_resumed", "action_completed", "stable_decision",
        "nested_choice_pause", "strict_input_gap", "history_entry", "history_reset", "anomaly",
        "observation_failure", "native_rng_boundary",
        "native_rng_scalar_boundary", "native_rng_stream_checkpoint",
        "native_shuffle_operation", "native_initial_hp_roll",
        "native_input_request", "native_input_outcome",
        "native_player_choice", "native_run_identity",
        "native_hand_menu", "native_hand_menu_revision",
        "native_hand_menu_closed", "native_manual_play_result",
        "native_ui_play_begin", "native_ui_hand_gate",
        "native_turn_checkpoint", "native_phase_handoff", "terminal",
    }
    running: dict[int, dict] = {}
    completed: dict[int, dict] = {}
    choices: list[dict] = []
    awaiting_resume: set[int] = set()
    history_index = -1
    last_frame_index = -1
    last_frame_hash = None
    strict_failures: list[str] = []
    input_requests: dict[int, dict] = {}
    request_outcomes: set[int] = set()
    choices_seen: set[tuple[int, int]] = set()
    hand_menus_seen: set[int] = set()
    menus_by_choice: dict[tuple[int, int], dict] = {}
    menus_by_id: dict[int, dict] = {}
    turn_checkpoint_ids: set[tuple[str, int]] = set()
    player_turn_start_callbacks: dict[int, int] = {}
    for ordinal, record in enumerate(records, 1):
        typ = record.get("type")
        if record.get("ordinal") != ordinal:
            report["reason"] = f"Nonconsecutive sidecar ordinal at line {ordinal}"
            return report
        if typ not in allowed:
            report["reason"] = f"Unknown sidecar record kind {typ} at line {ordinal}"
            return report
        if typ == "header" and ordinal != 1:
            report["reason"] = "Duplicate header"
            return report
        if typ == "history_baseline":
            count = (record.get("data") or {}).get("existing_entry_count")
            if type(count) is not int or count < 0 or history_index >= 0:
                report["reason"] = "Invalid native history baseline"
                return report
            history_index = count - 1
        if typ == "terminal" and ordinal != len(records):
            report["reason"] = "Premature terminal"
            return report
        aid = record.get("action_id")
        if typ == "action_started":
            if type(aid) is not int or aid <= 0 or aid in running or aid in completed:
                report["reason"] = "Duplicate or invalid action ID"
                return report
            parent = record.get("parent_action_id")
            if parent is not None and parent not in running:
                report["reason"] = f"Action {aid} references inactive parent {parent}"
                return report
            running[aid] = record
            link = (record.get("data") or {}).get("input_request_id")
            if link is not None:
                source = input_requests.get(link)
                if (source is None
                    or source.get("canonical_action") != record.get("canonical_action")
                    or source.get("native_type") != record.get("native_type")):
                    strict_failures.append(
                        f"Invalid source request/executor link at ordinal {ordinal}")
                else:
                    report["source_request_execution_links"] += 1
        elif typ == "action_resumed":
            if aid not in running or aid not in awaiting_resume:
                report["reason"] = f"Resume has no matching suspended action: {aid}"
                return report
            if record.get("native_type") != running[aid].get("native_type"):
                report["reason"] = f"Resumed action type changed: {aid}"
                return report
            awaiting_resume.remove(aid)
            report["action_resumptions"] += 1
        elif typ == "action_completed":
            if aid not in running:
                report["reason"] = f"Completion has no action start: {aid}"
                return report
            start = running.pop(aid)
            awaiting_resume.discard(aid)
            completed[aid] = {
                "action_id": aid,
                "native_type": start.get("native_type"),
                "canonical_action": start.get("canonical_action"),
                "parent_action_id": start.get("parent_action_id"),
                "start_ordinal": start["ordinal"],
                "end_ordinal": ordinal,
                "observed_delta": diff_scalars(
                    start.get("state") or {}, record.get("state") or {}),
                "pre_state_present": isinstance(start.get("state"), dict),
                "post_state_present": isinstance(record.get("state"), dict),
            }
        elif typ == "native_phase_handoff":
            data = record.get("data") or {}
            if (data.get("source_event") == "TURN_STARTED"
                and data.get("native_side") == "Player"):
                turn = data.get("turn")
                if type(turn) is not int or turn in player_turn_start_callbacks:
                    strict_failures.append(
                        f"Duplicate or invalid native player start at {ordinal}")
                elif ("PLAYER_TURN_START", turn) in turn_checkpoint_ids:
                    strict_failures.append(
                        f"Native player start followed its frame checkpoint at {ordinal}")
                else:
                    player_turn_start_callbacks[turn] = ordinal
        elif typ == "native_turn_checkpoint":
            data = record.get("data") or {}
            kind, turn = data.get("kind"), data.get("turn")
            try:
                quiescence = queue_quiescence_status(
                    data, player_start=kind == "PLAYER_TURN_START")
            except ValueError:
                quiescence = "INVALID_QUEUE_EVIDENCE"
            try:
                effect_status = automatic_effect_evidence_status(
                    data, player_start=kind == "PLAYER_TURN_START")
            except ValueError:
                effect_status = "INVALID_AUTOMATIC_EFFECT_EVIDENCE"
            player_start = kind == "PLAYER_TURN_START"
            enemy_start = kind == "ENEMY_TURN_START"
            key = (kind, turn)
            snapshot = record.get("state")
            combat = (snapshot or {}).get("combat") if isinstance(
                snapshot, dict) else None
            previous = records[ordinal - 2] if ordinal > 1 else None
            native_type = (previous or {}).get("native_type") or ""
            valid = (
                (player_start or enemy_start)
                and type(turn) is int and turn > 0
                and key not in turn_checkpoint_ids
                and isinstance(combat, dict)
                and combat.get("turn") == turn
                and data.get("observation_site") ==
                    ("NativeV111PassiveBridge.OnFrame" if player_start
                     else "ActionExecutor.BeforeActionExecuted")
                and data.get("observation_basis") ==
                    ("IDLE_PLAYABLE_PLAYER_FRAME_BEFORE_OBSERVED_INPUT"
                     if player_start else
                     "PRE_READY_TO_BEGIN_ENEMY_TURN_ACTION")
                and isinstance(data.get("native_side"), str)
                and isinstance(data.get("player_phase"), str)
                and data.get("settlement_status") == "SETTLEMENT_UNVERIFIED"
                and quiescence != "INVALID_QUEUE_EVIDENCE"
                and effect_status != "INVALID_AUTOMATIC_EFFECT_EVIDENCE"
                and data.get("complete_native_queue_verified") is False
                and data.get("native_phase_handoff_order_verified") is False
                and data.get("original_native_legal_menu") == "NOT_VERIFIED"
                and data.get("emulator_parity") == "NOT_TESTED"
                and data.get("rng_draws_performed_by_inspector") == 0
                and (player_start and not running
                     and data["native_side"] == "Player"
                     and data["player_phase"] == "Play"
                     or enemy_start and previous is not None
                     and previous.get("type") == "action_started"
                     and native_type.endswith("ReadyToBeginEnemyTurnAction")
                     and previous.get("state") == snapshot)
            )
            if not valid:
                strict_failures.append(
                    f"Invalid native turn-boundary candidate at {ordinal}")
            else:
                turn_checkpoint_ids.add(key)
                if player_start:
                    if turn in player_turn_start_callbacks:
                        report["player_checkpoint_phase_callback_pairs"] += 1
                    else:
                        # The first turn's callback can precede OpenCombat.
                        # Missing callback evidence is reported, not invented.
                        report["player_checkpoints_without_observed_start_callback"] += 1
                if effect_status == "SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME":
                    report["source_tracked_effect_idle_player_checkpoints"] += 1
                if quiescence == "SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME":
                    report["source_queue_quiescent_player_checkpoints"] += 1
                report["turn_boundary_candidates"].append({
                    "ordinal": ordinal, "kind": kind, "turn": turn,
                    "observation_site": data["observation_site"],
                    "observation_basis": data["observation_basis"],
                    "native_side": data["native_side"],
                    "player_phase": data["player_phase"],
                    "settlement_status": "SETTLEMENT_UNVERIFIED",
                    "queue_quiescence_status": quiescence,
                    "automatic_effect_evidence_status": effect_status,
                    "emulator_parity": "NOT_TESTED",
                })
        elif typ == "frame_state":
            info = record.get("data") or {}
            idx, state_hash = info.get("frame_index"), info.get("state_hash")
            if (type(idx) is not int or idx <= last_frame_index or not isinstance(
                    record.get("state"), dict) or not isinstance(state_hash, str) or not state_hash):
                report["reason"] = f"Invalid changed-frame snapshot at ordinal {ordinal}"
                return report
            if last_frame_hash == state_hash:
                report["reason"] = f"Duplicate unchanged frame snapshot at ordinal {ordinal}"
                return report
            last_frame_index, last_frame_hash = idx, state_hash
            report["frame_states"] += 1
        elif typ == "nested_choice_pause":
            report["nested_choice_pauses"] += 1
            choices.append({
                "action_id": aid,
                "pause_ordinal": ordinal,
                "pre_choice_state": record.get("state"),
            })
            if aid not in running:
                strict_failures.append(f"Nested choice with no active action: {aid}")
            else:
                awaiting_resume.add(aid)
        elif typ == "strict_input_gap":
            data = record.get("data") or {}
            if (aid not in running
                or data.get("reason") != "executor_action_without_unique_strict_decision"
                or type(data.get("decision_generation")) is not int
                or type(data.get("last_captured_generation")) is not int
                or data.get("human_input_origin_verified") is not False
                or data.get("legal_menu_reconstructed") is not False):
                strict_failures.append(f"Invalid strict input gap at ordinal {ordinal}")
            else:
                report["strict_input_gaps"].append({
                    "ordinal": ordinal,
                    "action_id": aid,
                    "last_reported_gate": data.get("last_reported_gate"),
                    "decision_generation": data["decision_generation"],
                    "last_captured_generation": data["last_captured_generation"],
                })
        elif typ == "native_run_identity":
            data = record.get("data") or {}
            if (report["native_run_identity"] is not None
                or not isinstance(data.get("seed"), str)
                or not data["seed"]
                or type(data.get("ascension")) is not int
                or data["ascension"] < 0
                or data.get("source") != "RunState.Rng.StringSeed"
                or data.get("rng_draws_performed") != 0):
                strict_failures.append(f"Invalid native run identity at {ordinal}")
            else:
                report["native_run_identity"] = {
                    "seed": data["seed"], "ascension": data["ascension"],
                    "source": data["source"]
                }
        elif typ == "native_input_request":
            data = record.get("data") or {}
            request_id = data.get("input_request_id")
            if (type(request_id) is not int or request_id <= 0
                or request_id in input_requests
                or data.get("source") !=
                    "ActionQueueSynchronizer.RequestEnqueue.prefix"
                or data.get("native_acceptance") != "UNVERIFIED"
                or data.get("human_origin") != "UNVERIFIED"
                or data.get("input_injected") is not False
                or not isinstance(record.get("native_type"), str)):
                strict_failures.append(f"Invalid native input request at {ordinal}")
            else:
                input_requests[request_id] = record
                report["native_input_requests"].append({
                    "ordinal": ordinal,
                    "input_request_id": request_id,
                    "native_type": record["native_type"],
                    "canonical_action": record.get("canonical_action"),
                    "acceptance": "UNVERIFIED",
                })
        elif typ == "native_input_outcome":
            data = record.get("data") or {}
            request_id = data.get("input_request_id")
            request = input_requests.get(request_id)
            native_state = data.get("native_action_state")
            action_id = data.get("native_action_id")
            expected = (
                "DEFERRED_OR_UNENQUEUED"
                if native_state == "None" and action_id is None else
                "CANCELED_AT_RETURN"
                if native_state == "Canceled" else
                "ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN"
                if native_state in ("WaitingForExecution", "Executing",
                                    "GatheringPlayerChoice",
                                    "ReadyToResumeExecuting", "Finished")
                and type(action_id) is int and action_id >= 0 else
                "STATE_UNCLASSIFIED"
            )
            if (request is None or request_id in request_outcomes
                or request.get("native_type") != record.get("native_type")
                or data.get("source") !=
                    "ActionQueueSynchronizer.RequestEnqueue.postfix"
                or data.get("human_origin") != "UNVERIFIED"
                or data.get("legal_menu_verified") is not False
                or data.get("queue_result") != expected):
                strict_failures.append(f"Invalid native input outcome at {ordinal}")
            else:
                request_outcomes.add(request_id)
                report["native_input_outcomes"].append({
                    "ordinal": ordinal, "input_request_id": request_id,
                    "native_action_id": action_id,
                    "native_action_state": native_state,
                    "queue_result": expected,
                    "menu_verified": False,
                })
        elif typ == "native_hand_menu":
            data = record.get("data") or {}
            menu_id = data.get("menu_id")
            mode = data.get("mode")
            minimum, maximum = data.get("min_select"), data.get("max_select")
            holder_count = data.get("native_hand_holder_count")
            missing_ids = data.get("missing_card_ids")
            options = data.get("options")
            binding = data.get("selection_id_binding")
            owner = data.get("selection_owner_id")
            choice_id = data.get("selection_choice_id")
            exact = binding == "SOURCE_ASYNC_FLOW_RESERVED_ID"
            valid_binding = (binding in (
                    "UNVERIFIED", "SOURCE_ASYNC_FLOW_RESERVED_ID",
                    "CHOICE_ID_NOT_YET_OBSERVED",
                    "AMBIGUOUS_SOURCE_RESERVATION")
                and (type(owner) is int and owner >= 0 or owner is None)
                and ((type(choice_id) is int and choice_id >= 0
                      and type(owner) is int and owner >= 0)
                     if exact else choice_id is None))
            valid = (valid_binding
                and type(menu_id) is int and menu_id > 0
                and menu_id not in hand_menus_seen
                and mode in ("SimpleSelect", "UpgradeSelect")
                and type(minimum) is int and minimum >= 0
                and type(maximum) is int and maximum >= minimum
                and type(holder_count) is int and holder_count >= 0
                and type(missing_ids) is int and missing_ids >= 0
                and type(data.get("require_manual_confirmation")) is bool
                and isinstance(options, list)
                and len(options) + missing_ids <= holder_count
                and data.get("predicate_re_evaluated_by_inspector") is False
                and data.get("source") ==
                    "NPlayerHand.SelectCards.postfix_after_native_visibility_update"
                and data.get("emulator_parity") == "NOT_TESTED"
                and data.get("coverage") == (
                    "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                    if missing_ids == 0 else
                    "INCOMPLETE_NATIVE_CARD_ID_MAPPING"))
            if valid:
                all_ids, all_positions = [], []
                for option in options:
                    if not isinstance(option, dict):
                        valid = False
                        break
                    card_id = option.get("combat_card_id")
                    model_id = option.get("card_model_id")
                    slot = option.get("ui_hand_index")
                    if (type(card_id) is not int or card_id < 0
                        or not isinstance(model_id, str) or not model_id
                        or type(slot) is not int
                        or slot < 0 or slot >= holder_count):
                        valid = False
                        break
                    all_ids.append(card_id)
                    all_positions.append(slot)
                if valid:
                    valid = (
                        len(all_ids) == len(set(all_ids))
                        and len(all_positions) == len(set(all_positions))
                        and all_positions == sorted(all_positions))
            if not valid:
                strict_failures.append(f"Invalid native hand menu at {ordinal}")
            else:
                hand_menus_seen.add(menu_id)
                if exact:
                    key = (owner, choice_id)
                    if key in menus_by_choice:
                        strict_failures.append(
                            f"Multiple source menus for one choice id at {ordinal}")
                    else:
                        menus_by_choice[key] = {
                            "menu_id": menu_id, "ordinal": ordinal,
                            "options": options, "coverage": data["coverage"],
                            "latest_options": options,
                            "latest_coverage": data["coverage"],
                            "latest_peek_mode": False,
                            "revision": 1,
                            "mode": mode,
                            "owner": owner,
                            "choice_id": choice_id,
                            "binding": binding,
                            "closed": False
                        }
                if exact and (owner, choice_id) in menus_by_choice:
                    menus_by_id[menu_id] = menus_by_choice[(owner, choice_id)]
                else:
                    menus_by_id[menu_id] = {
                        "menu_id": menu_id, "ordinal": ordinal,
                        "options": options, "coverage": data["coverage"],
                        "latest_options": options,
                        "latest_coverage": data["coverage"],
                        "latest_peek_mode": False,
                        "revision": 1,
                        "mode": mode,
                        "owner": owner,
                        "choice_id": choice_id,
                        "binding": binding,
                        "closed": False
                    }
                report["native_hand_menus"].append({
                    "ordinal": ordinal, "menu_id": menu_id,
                    "mode": mode,
                    "source_model_id": data.get("source_model_id"),
                    "min_select": minimum, "max_select": maximum,
                    "options": options, "coverage": data["coverage"],
                    "choice_id_binding": binding,
                    "choice_owner_id": owner, "choice_id": choice_id,
                })
        elif typ == "native_hand_menu_revision":
            data = record.get("data") or {}
            menu_id = data.get("menu_id")
            menu = menus_by_id.get(menu_id)
            revision = data.get("revision")
            options = data.get("options")
            peeking = data.get("peek_mode")
            count = data.get("native_hand_holder_count")
            missing = data.get("missing_card_ids")
            expected_coverage = (
                "NATIVE_PEEK_VIEW_NOT_SELECTABLE" if peeking is True
                else "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                if missing == 0 else "INCOMPLETE_NATIVE_CARD_ID_MAPPING")
            valid = (
                menu is not None and not menu["closed"]
                and type(revision) is int
                and revision == menu["revision"] + 1
                and data.get("mode") == menu["mode"]
                and data.get("selection_owner_id") == menu["owner"]
                and data.get("selection_choice_id") == menu["choice_id"]
                and data.get("selection_id_binding") == menu["binding"]
                and type(peeking) is bool
                and type(count) is int and count >= 0
                and type(missing) is int and missing >= 0
                and isinstance(options, list)
                and data.get("coverage") == expected_coverage
                and data.get("source") ==
                    "NPlayerHand.UpdateSelectModeCardVisibility.postfix"
                and data.get("predicate_re_evaluated_by_inspector") is False
                and data.get("complete_menu_history") == "NOT_VERIFIED"
                and data.get("emulator_parity") == "NOT_TESTED"
                and (not peeking or (missing == 0 and len(options) == 0))
                and (peeking or len(options) + missing <= count))
            if valid:
                ids = []
                slots = []
                for option in options:
                    if not isinstance(option, dict):
                        valid = False
                        break
                    card_id = option.get("combat_card_id")
                    model_id = option.get("card_model_id")
                    slot = option.get("ui_hand_index")
                    if (type(card_id) is not int or card_id < 0
                        or not isinstance(model_id, str) or not model_id
                        or type(slot) is not int or slot < 0 or slot >= count):
                        valid = False
                        break
                    ids.append(card_id)
                    slots.append(slot)
                if len(set(ids)) != len(ids) or len(set(slots)) != len(slots):
                    valid = False
                if slots != sorted(slots):
                    valid = False
            if not valid:
                strict_failures.append(
                    f"Invalid native hand menu revision at {ordinal}")
            else:
                menu.update(
                    latest_options=options,
                    latest_coverage=data["coverage"],
                    latest_peek_mode=peeking,
                    revision=revision)
                report["native_hand_menu_revisions"].append({
                    "ordinal": ordinal, "menu_id": menu_id,
                    "revision": revision, "peek_mode": peeking,
                    "coverage": data["coverage"],
                    "options": options,
                    "choice_id_binding": menu["binding"],
                })
        elif typ == "native_hand_menu_closed":
            data = record.get("data") or {}
            menu_id = data.get("menu_id")
            menu = menus_by_id.get(menu_id)
            reason = data.get("native_reason")
            expected_source = (
                "NPlayerHand.AfterCardsSelected.postfix"
                if reason == "AfterCardsSelected"
                else "NPlayerHand._ExitTree.prefix")
            valid = (
                menu is not None and not menu["closed"]
                and type(data.get("final_revision")) is int
                and data.get("final_revision") == menu["revision"]
                and data.get("selection_owner_id") == menu["owner"]
                and data.get("selection_choice_id") == menu["choice_id"]
                and data.get("selection_id_binding") == menu["binding"]
                and reason in ("AfterCardsSelected", "NativeHandExitTree")
                and data.get("source") == expected_source
                and data.get("native_choice_result_verified") is False
                and data.get("complete_menu_history") == "NOT_VERIFIED")
            if not valid:
                strict_failures.append(
                    f"Invalid native hand menu close at {ordinal}")
            else:
                menu["closed"] = True
                report["native_hand_menu_closures"].append({
                    "ordinal": ordinal, "menu_id": menu_id,
                    "final_revision": menu["revision"],
                    "native_reason": reason
                })
        elif typ == "native_ui_hand_gate":
            data = record.get("data") or {}
            gate = data.get("gate")
            if (gate not in ("CanPlayCards", "AreCardActionsAllowed")
                or type(data.get("returned_allowed")) is not bool
                or data.get("native_hand_mode") not in (
                    "Play", "None", "SimpleSelect", "UpgradeSelect")
                or type(data.get("existing_card_play_active")) is not bool
                or data.get("source") !=
                    "NPlayerHand." + str(gate) + ".postfix"
                or data.get("result_basis") != "ORIGINAL_NATIVE_RETURN_VALUE"
                or data.get("source_evaluation_only") is not True
                or data.get("human_origin") != "UNVERIFIED"
                or data.get("full_legal_menu") != "NOT_TESTED"
                or data.get("emulator_parity") != "NOT_TESTED"):
                strict_failures.append(
                    f"Invalid native hand UI gate at {ordinal}")
            else:
                report["native_ui_hand_gates"].append({
                    "ordinal": ordinal, "gate": gate,
                    "returned_allowed": data["returned_allowed"],
                    "native_hand_mode": data["native_hand_mode"],
                    "existing_card_play_active":
                        data["existing_card_play_active"],
                    "source_evaluation_only": True,
                    "full_legal_menu": "NOT_TESTED"
                })
        elif typ == "native_ui_play_begin":
            data = record.get("data") or {}
            card_id = data.get("combat_card_id")
            ui_observation = data.get("ui_state_observation")
            observed_state = record.get("state")
            state_valid = (
                (ui_observation is None and observed_state is None)
                or (ui_observation == "UNOBSERVED" and observed_state is None)
                or (ui_observation == "SOURCE_UI_BEGIN_CANONICAL_SNAPSHOT"
                    and isinstance(observed_state, dict)))
            if (not state_valid
                or card_id is not None
                    and (type(card_id) is not int or card_id < 0)
                or not isinstance(data.get("card_model_id"), str)
                or not data.get("card_model_id")
                or type(data.get("started_via_shortcut")) is not bool
                or data.get("source") != "NPlayerHand.StartCardPlay.prefix"
                or data.get("native_ui_gate") != "SOURCE_CALLSITE_PASSED"
                or data.get("manual_play_accepted") != "NOT_TESTED"
                or data.get("queue_acceptance") != "NOT_TESTED"
                or data.get("human_origin") != "UNVERIFIED"
                or data.get("full_legal_menu") != "NOT_TESTED"
                or data.get("emulator_parity") != "NOT_TESTED"):
                strict_failures.append(
                    f"Invalid native UI play begin at {ordinal}")
            else:
                report["native_ui_play_begins"].append({
                    "ordinal": ordinal,
                    "source_ui_state_observation": (
                        ui_observation or "UNOBSERVED_LEGACY"),
                    "combat_card_id": card_id,
                    "card_model_id": data["card_model_id"],
                    "started_via_shortcut": data["started_via_shortcut"],
                    "ui_gate": "SOURCE_CALLSITE_PASSED",
                })
        elif typ == "native_manual_play_result":
            data = record.get("data") or {}
            card_id = data.get("combat_card_id")
            if (card_id is not None and
                (type(card_id) is not int or card_id < 0)
                or not isinstance(data.get("card_model_id"), str)
                or not data.get("card_model_id")
                or type(data.get("returned_success")) is not bool
                or data.get("source") != "CardModel.TryManualPlay.postfix"
                or data.get("queue_acceptance") != "UNVERIFIED"
                or data.get("human_origin") != "UNVERIFIED"
                or data.get("emulator_parity") != "NOT_TESTED"):
                strict_failures.append(
                    f"Invalid native manual play result at {ordinal}")
            else:
                report["native_manual_play_results"].append({
                    "ordinal": ordinal,
                    "combat_card_id": card_id,
                    "card_model_id": data["card_model_id"],
                    "target_id": data.get("target_id"),
                    "returned_success": data["returned_success"],
                    "queue_acceptance": "UNVERIFIED",
                })
        elif typ == "native_player_choice":
            data = record.get("data") or {}
            owner, choice_id = data.get("owner_id"), data.get("choice_id")
            kind, projection = data.get("choice_type"), data.get("selection_projection")
            valid = (type(owner) is int and owner >= 0
                and type(choice_id) is int and choice_id >= 0
                and isinstance(kind, str) and bool(kind)
                and data.get("origin") ==
                    "PlayerChoiceSynchronizer.PlayerChoiceReceived"
                and data.get("accepted_choice_menu") == "NOT_CAPTURED"
                and data.get("human_origin") == "UNVERIFIED"
                and (owner, choice_id) not in choices_seen
                and (projection == {
                    "CombatCard": "EXACT_COMBAT_CARD_IDS",
                    "Index": "EXACT_INDEXES",
                    "CanonicalCard": "EXACT_CANONICAL_MODEL_IDS",
                    "Player": "EXACT_PLAYER_ID",
                }.get(kind, "UNPROJECTED_CHOICE_PAYLOAD")))
            if kind == "CombatCard":
                valid = valid and isinstance(
                    data.get("selected_combat_card_ids"), list)
            elif kind == "Index":
                valid = valid and isinstance(data.get("selected_indexes"), list)
            elif kind == "CanonicalCard":
                valid = valid and isinstance(
                    data.get("selected_canonical_card_models"), list)
            if not valid:
                strict_failures.append(f"Invalid native player choice at {ordinal}")
            else:
                choices_seen.add((owner, choice_id))
                report["native_player_choices"].append({
                    "ordinal": ordinal, "action_id": aid,
                    "owner_id": owner, "choice_id": choice_id,
                    "choice_type": kind, "selection_projection": projection,
                    "selected_combat_card_ids":
                        data.get("selected_combat_card_ids"),
                    "selected_indexes": data.get("selected_indexes"),
                })
                source_menu = menus_by_choice.get((owner, choice_id))
                if source_menu is not None and kind == "CombatCard":
                    selected = data.get("selected_combat_card_ids")
                    initial_offers = {
                        option["combat_card_id"]
                        for option in source_menu["options"]}
                    is_initial_subset = (
                        isinstance(selected, list)
                        and all(type(x) is int and x >= 0 for x in selected)
                        and set(selected) <= initial_offers)
                    report["native_hand_menu_choice_matches"].append({
                        "owner_id": owner, "choice_id": choice_id,
                        "menu_id": source_menu["menu_id"],
                        "menu_ordinal": source_menu["ordinal"],
                        "choice_ordinal": ordinal,
                        "selection_in_initial_visible_options": (
                            is_initial_subset
                            if source_menu["coverage"] ==
                            "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                            else None),
                        "latest_captured_revision": source_menu["revision"],
                        "latest_captured_peek_mode":
                            source_menu["latest_peek_mode"],
                        "selection_in_latest_captured_options": (
                            set(selected) <= {
                                x["combat_card_id"]
                                for x in source_menu["latest_options"]}
                            if (isinstance(selected, list)
                                and all(type(x) is int and x >= 0
                                    for x in selected)
                                and source_menu["latest_coverage"] ==
                                    "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                                and not source_menu["latest_peek_mode"])
                            else None),
                        "native_hand_menu_closed_before_choice":
                            source_menu["closed"],
                        "selection_menu_revalidation": (
                            "SOURCE_REVISIONS_OBSERVED"
                            if source_menu["revision"] > 1 else "NOT_OBSERVED"),
                        "complete_menu_history": "NOT_VERIFIED",
                        "binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
                    })
        elif typ == "native_initial_hp_roll":
            data = record.get("data") or {}
            if (data.get("source_version") != "v0.111.0"
                or data.get("source_callsite_observed") is not True
                or not isinstance(data.get("ordered_eligible_hp"), list)
                or type(data.get("selected_index")) is not int
                or type(data.get("selected_hp")) is not int):
                report["reason"] = (
                    f"Invalid native initial HP source roll at {ordinal}")
                return report
            report["native_initial_hp_source_rolls"] += 1
        elif typ == "native_shuffle_operation":
            data = record.get("data") or {}
            if (data.get("source_version") != "v0.111.0"
                or data.get("kind") not in ("initial", "recycle")
                or type(data.get("operation_id")) is not int
                or not isinstance(data.get("choices"), list)
                or not isinstance(data.get("ordered_input"), list)
                or not isinstance(data.get("pre_hook_order"), list)
                or not isinstance(data.get("post_hook_order"), list)):
                report["reason"] = (
                    f"Invalid native shuffle source operation at {ordinal}")
                return report
            report["native_shuffle_source_operations"] += 1
        elif typ == "native_rng_stream_checkpoint":
            # Complete inventory and state arithmetic are verified by the
            # independent native_rng_checkpoint_audit.py; count here only.
            details = record.get("data") or {}
            if (details.get("source") != "native_rng_object_to_serializable"
                or details.get("rng_draws_performed") != 0
                or details.get("phase") not in
                    ("combat_attach", "before_action", "after_action", "combat_end")
                or not isinstance(details.get("streams"), list)):
                report["reason"] = "Invalid native RNG checkpoint at ordinal " + str(ordinal)
                return report
            report["native_rng_stream_checkpoint_count"] += 1
        elif typ == "native_rng_scalar_boundary":
            data = record.get("data") or {}
            before, after = data.get("cursor_before"), data.get("cursor_after")
            def counter(obj):
                return (obj.get("counter", obj.get("Counter"))
                        if isinstance(obj, dict) else None)
            kind = data.get("operation_kind")
            scalar = data.get("selected_double")
            boolean = data.get("selected_bool")
            float_value = data.get("selected_float")
            float_min = data.get("float_min")
            float_max = data.get("float_max")
            value_valid = (
                (kind == "unit_double" and _is_json_number(scalar)
                 and 0 <= scalar < 1 and boolean is None)
                or (kind == "boolean" and type(boolean) is bool
                    and scalar is None)
                or (kind == "range_float" and scalar is None and boolean is None
                    and all(_is_json_number(x) for x in
                            (float_value, float_min, float_max))
                    and all(math.isfinite(x) for x in
                            (float_value, float_min, float_max))
                    and float_min <= float_max))
            if (data.get("origin") != "source_primitive_hook"
                or data.get("source_hook_observed") is not True
                or data.get("source_version") != "v0.111.0"
                or not value_valid
                or type(counter(before)) is not int
                or type(counter(after)) is not int
                or counter(after) != counter(before) + 1):
                report["reason"] = (
                    f"Invalid native RNG scalar source call at ordinal {ordinal}")
                return report
            report["native_rng_source_calls"] += 1
            report["native_rng_scalar_source_calls"] += 1
            if kind == "range_float":
                report["native_rng_float_source_calls"] += 1
            report["native_rng_source_coverage"] = "SOURCE_RNG_CALLS_OBSERVED"
            if (data.get("attributed") is not True
                or any(data.get(name) in (None, "", "UNATTRIBUTED")
                       for name in ("site_id", "stream_id", "owner_id"))):
                report["native_rng_unattributed_calls"] += 1
        elif typ == "native_rng_boundary":
            data = record.get("data") or {}
            before, after = data.get("cursor_before"), data.get("cursor_after")
            def counter(obj):
                return (obj.get("counter", obj.get("Counter"))
                        if isinstance(obj, dict) else None)
            n, selected = data.get("bounded_count"), data.get("selected_index")
            if (data.get("origin") != "source_primitive_hook"
                or data.get("source_hook_observed") is not True
                or data.get("source_version") != "v0.111.0"
                or data.get("operation_kind") != "bounded_integer"
                or type(n) is not int or n <= 0
                or type(selected) is not int or not 0 <= selected < n
                or type(counter(before)) is not int
                or type(counter(after)) is not int
                or counter(after) != counter(before) + 1):
                report["reason"] = f"Invalid native RNG source call at ordinal {ordinal}"
                return report
            report["native_rng_source_calls"] += 1
            report["native_rng_source_coverage"] = "SOURCE_RNG_CALLS_OBSERVED"
            if (data.get("attributed") is not True
                or any(data.get(name) in (None, "", "UNATTRIBUTED")
                       for name in ("site_id", "stream_id", "owner_id"))):
                report["native_rng_unattributed_calls"] += 1
        elif typ == "history_reset":
            history_index = -1
            details = record.get("data") or {}
            next_record = records[ordinal] if ordinal < len(records) else None
            with_end_checkpoint = _unchanged_combat_end_checkpoint(records, ordinal)
            terminal = (records[ordinal + 1] if with_end_checkpoint
                        else next_record)
            final_reason = ((terminal or {}).get("data") or {}).get("reason")
            terminal_cleanup = (not running and terminal is not None
                and terminal.get("type") == "terminal"
                and final_reason == "Combat ended"
                and (next_record is terminal or with_end_checkpoint)
                and details.get("new_count") == 0
                and type(details.get("previous_count")) is int
                and details["previous_count"] > 0)
            if terminal_cleanup:
                report["terminal_history_clears"] += 1
            else:
                strict_failures.append("Native history reset outside terminal cleanup")
        elif typ == "history_entry":
            data = record.get("data") or {}
            idx = data.get("history_index")
            if type(idx) is not int or idx != history_index + 1:
                report["reason"] = f"Nonconsecutive history index at ordinal {ordinal}"
                return report
            history_index = idx
            if aid is None:
                report["unattributed_history_entries"] += 1
            if data.get("missing_fields"):
                report["history_entries_with_missing_fields"] += 1
        elif typ == "anomaly":
            detail = record.get("data") or {}
            preceding = records[ordinal-2] if ordinal > 1 else {}
            suspended_id = preceding.get("action_id")
            # Legacy Inspector v1 wrote a duplicate_action_start anomaly
            # whenever BeforeActionExecuted was emitted on resuming a choice.
            # Preserve the raw anomaly, but identify explainable sequences.
            expected_resume = (
                detail.get("reason") == "duplicate_action_start"
                and preceding.get("type") == "nested_choice_pause"
                and suspended_id in running
                and suspended_id in awaiting_resume
                and detail.get("detail") == running[suspended_id].get("native_type"))
            if expected_resume:
                awaiting_resume.remove(suspended_id)
                report["legacy_resume_candidates"] += 1
            else:
                strict_failures.append(f"anomaly at ordinal {ordinal}")
        elif typ == "observation_failure":
            strict_failures.append(f"{typ} at ordinal {ordinal}")
        elif typ == "stable_decision" and not isinstance(record.get("state"), dict):
            strict_failures.append(f"Missing stable decision state at {ordinal}")

    # Validate turn-loop callbacks independently of frame-derived candidates.
    # Partial captures remain partial; source event ordering alone cannot
    # certify completion of future asynchronous automatic effects.
    phase_handoff = audit_phase_handoffs(records)
    report["native_phase_handoff"] = phase_handoff
    report["post_card_windows"] = audit_action_windows(records)
    report["post_card_tracked_rng_windows"] = audit_native_rng_windows(
        records, source_windows=report["post_card_windows"]["source_windows"])
    strict_failures.extend(phase_handoff["errors"])
    report["native_hand_menu_open_lifetimes"] = sorted(
        menu_id for menu_id, menu in menus_by_id.items() if not menu["closed"])
    final = records[-1].get("data") or {}
    reported_open = final.get("open_actions", [])
    if sorted(running) != sorted(reported_open):
        report["reason"] = "Terminal open action IDs disagree with callback lifecycle"
        return report
    report["actions"] = [completed[a] for a in sorted(completed)]
    report["observed_mechanic_indicators"] = observed_indicators(
        report["actions"], records)
    for choice in choices:
        aid = choice["action_id"]
        ended = next((r for r in records if r.get("type") == "action_completed"
                     and r.get("action_id") == aid), None)
        candidate = infer_choice_discard(
            choice.get("pre_choice_state") or {},
            (ended or {}).get("state") or {})
        report["nested_choices"].append({
            "action_id": aid,
            "pause_ordinal": choice["pause_ordinal"],
            **candidate,
        })
    report["incomplete_observations"] = strict_failures
    report["raw_terminal_status"] = final.get("status")
    if running or strict_failures or final.get("status") not in ("OBSERVED", "INCOMPLETE_CAPTURE"):
        report["status"] = "INCOMPLETE_CAPTURE"
        report["reason"] = (
            f"{len(running)} unfinished actions, {len(strict_failures)} flagged observations"
            f"; native terminal status={final.get('status')}"
        )
    elif final.get("status") == "INCOMPLETE_CAPTURE":
        # Never silently upgrade the historic on-disk terminal. Recovery is a
        # qualified *audit interpretation*, not repaired source data.
        if report["legacy_resume_candidates"] and report["terminal_history_clears"]:
            report["status"] = "LEGACY_CALLBACK_SEQUENCE_RECOVERABLE"
            report["reason"] = ("Action lifecycle consistent after explaining choice "
                "re-entrancies and terminal CombatHistory clear; raw terminal remains incomplete")
        else:
            report["status"] = "INCOMPLETE_CAPTURE"
            report["reason"] = "Raw incomplete terminal lacks sufficient recovery evidence"
    else:
        report["status"] = "CALLBACK_TIMELINE_CONSISTENT"
        report["reason"] = "Action callbacks and recorded history have consistent identities/order"
    return report


def load(path: Path) -> tuple[list[dict], str | None]:
    records: list[dict] = []
    try:
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if line.strip():
                try:
                    record = json.loads(line)
                except json.JSONDecodeError as exc:
                    return records, f"Malformed JSONL line {line_number}: {exc}"
                if not isinstance(record, dict):
                    return records, f"Expected object on line {line_number}"
                records.append(record)
    except OSError as exc:
        return records, str(exc)
    if path.with_suffix(path.suffix + ".incomplete").exists():
        return records, "Sidecar writer reported dropped or failed records"
    return records, None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sidecar", type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load(args.sidecar)
    result = audit(records, error)
    rendered = json.dumps(result, sort_keys=True, indent=2)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if result["status"] == "CALLBACK_TIMELINE_CONSISTENT" else 2


if __name__ == "__main__":
    raise SystemExit(main())
