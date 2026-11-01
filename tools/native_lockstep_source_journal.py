#!/usr/bin/env python3
"""Build a source-only native action chronology for later persistent emulator replay.

The journal is an observation of the game, not a legal-menu enumerator or
verified emulator command stream. Player input origin remains unverified.
All requests, native queue outcomes, actual executor starts/completions,
and synchronized player-choice results retain their source ordinals.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_action_envelope_audit import audit as audit_sidecar
from native_action_envelope_audit import diff_scalars
from native_action_envelope_audit import load as load_sidecar


def _source_snapshot_agreement(left, right) -> str:
    """Compare captured native states without declaring a decision boundary."""
    if not isinstance(left, dict) or not isinstance(right, dict):
        return "UNOBSERVED"
    return ("SOURCE_OBSERVED_EQUAL" if left == right
            else "SOURCE_OBSERVED_DIFFERENT")


def _observed_delta(left, right) -> dict | None:
    """Source-only measured differences, with missing snapshots explicit."""
    if not isinstance(left, dict) or not isinstance(right, dict):
        return None
    return diff_scalars(left, right)


def _qualified_history_reset(records: list[dict], audit: dict) -> list[dict] | None:
    """Allow *inspection* of callbacks across one uncertain history epoch.

    The strict sidecar audit remains INCOMPLETE_CAPTURE. Only its single
    out-of-terminal reset diagnostic is eligible: reject active actions,
    malformed counts, missing terminal or any unrelated audit failure.
    This never makes the journal eligible for emulator shadow replay.
    """
    if (audit.get("status") != "INCOMPLETE_CAPTURE"
        or audit.get("raw_terminal_status") != "OBSERVED"
        or audit.get("incomplete_observations") != [
            "Native history reset outside terminal cleanup"]):
        return None
    active: set[int] = set()
    resets = []
    for row in records:
        kind, aid = row.get("type"), row.get("action_id")
        if kind == "action_started":
            if type(aid) is not int or aid in active:
                return None
            active.add(aid)
        elif kind == "action_completed":
            if aid not in active:
                return None
            active.remove(aid)
        elif kind == "history_reset":
            d = row.get("data") or {}
            old, new = d.get("previous_count"), d.get("new_count")
            if (active or type(old) is not int or type(new) is not int
                or old <= new or new < 0):
                return None
            resets.append({"ordinal": row["ordinal"],
                           "previous_count": old, "new_count": new})
    if active or len(resets) != 1:
        return None
    return resets


def build(records: list[dict], error: str | None = None) -> dict:
    result: dict = {
        "schema": "native-source-action-journal-v1",
        "status": "INVALID_SOURCE_CAPTURE",
        "source_audit": None,
        "source_history_resets": [],
        "source_history_continuity": "NOT_EVALUATED",
        "game_build": None,
        "run_id": None,
        "run_seed": None,
        "ascension": None,
        "source_linked_commands": [],
        # Linked native executor callbacks without a canonical player command.
        "source_linked_noncanonical_executor_actions": [],
        "source_ui_request_windows": [],
        "source_turn_checkpoint_candidates": [],
        # Includes automatic/uncanonical native executor actions which have
        # no player-decision equivalent. These are source callback snapshots,
        # not necessarily settled state-transition boundaries.
        "source_executor_observed_transitions": [],
        "source_unlinked_canonical_executor_actions": [],
        "source_requests_without_executor_start": [],
        "native_choice_results": [],
        "input_origin": "UNVERIFIED",
        "native_legal_menu": "NOT_VERIFIED",
        "emulator_replay": "NOT_EXECUTED",
        "emulator_parity": "NOT_TESTED",
        "errors": [],
    }
    source = audit_sidecar(records, error)
    result["source_audit"] = source["status"]
    qualified_resets = _qualified_history_reset(records, source)
    if (source["status"] != "CALLBACK_TIMELINE_CONSISTENT"
        and qualified_resets is None):
        result["errors"].append(source.get("reason") or "Source sidecar audit failed")
        return result
    if qualified_resets is not None:
        result["source_history_resets"] = qualified_resets
        result["source_history_continuity"] = (
            "SOURCE_HISTORY_RESET_UNRESOLVED_CALLBACKS_ONLY")
    else:
        result["source_history_continuity"] = "SOURCE_AUDIT_CONSISTENT"

    header = records[0].get("data") or {}
    result["game_build"] = header.get("game_build")
    result["run_id"] = header.get("run_id")
    identity = source.get("native_run_identity")
    if identity is not None:
        result["run_seed"] = identity["seed"]
        result["ascension"] = identity["ascension"]

    # Keep native player/enemy-turn source evidence distinct from certified
    # emulator comparisons. These checkpoints are provisional until native
    # queue and phase-handoff settlement have been independently verified.
    for row in records:
        if row["type"] != "native_turn_checkpoint":
            continue
        details = row.get("data") or {}
        result["source_turn_checkpoint_candidates"].append({
            "ordinal": row["ordinal"],
            "kind": details["kind"],
            "turn": details["turn"],
            "source_state": row.get("state"),
            "native_side": details["native_side"],
            "player_phase": details["player_phase"],
            "observation_site": details["observation_site"],
            "settlement_status": "SETTLEMENT_UNVERIFIED",
            "native_full_queue_settlement": "NOT_VERIFIED",
            "emulator_applied": False,
            "emulator_parity": "NOT_TESTED",
        })

    requests = {}
    source_outcomes = {}
    starts = {}
    completions = {}
    for record in records:
        kind, details = record["type"], record.get("data") or {}
        if kind == "native_input_request":
            requests[details["input_request_id"]] = record
        elif kind == "native_input_outcome":
            source_outcomes[details["input_request_id"]] = record
        elif kind == "action_started":
            starts[record["action_id"]] = record
        elif kind == "action_completed":
            completions[record["action_id"]] = record

    # Compare source executor callbacks in chronological order. The
    # completion closest to a new start need not belong to the previously
    # started action (nested executors can overlap). Both relationships are
    # retained separately. Frames/UI may mutate state between callbacks.
    ordered_starts = sorted(starts.items(),
                            key=lambda pair: pair[1]["ordinal"])
    sorted_completions = sorted(
        completions.values(), key=lambda row: row["ordinal"])
    for action_id, started in ordered_starts:
        completed = completions.get(action_id)
        if completed is None:
            result["errors"].append(
                f"Native executor action {action_id} has no observed completion")
            continue
        start_at = started["ordinal"]
        preceding = next(
            (row for row in reversed(sorted_completions)
             if row["ordinal"] < start_at), None)
        active_before_start = [
            prior_id for prior_id, prior_start in ordered_starts
            if prior_start["ordinal"] < start_at
            and (prior_id not in completions
                 or completions[prior_id]["ordinal"] > start_at)
        ]
        prior_state = preceding.get("state") if preceding else None
        start_state = started.get("state")
        # The interval between callbacks can contain asynchronous effects.
        # Listing observed intervening records makes those gaps inspectable,
        # without asserting that an absent record proves no effects occurred.
        between = [
            row for row in records
            if preceding is not None
            and preceding["ordinal"] < row["ordinal"] < start_at
        ]
        result["source_executor_observed_transitions"].append({
            "executor_action_id": action_id,
            "native_type": started.get("native_type"),
            "canonical_action_candidate": started.get("canonical_action"),
            "start_ordinal": start_at,
            "complete_ordinal": completed["ordinal"],
            "pre_execution_state": start_state,
            "post_execution_state": completed.get("state"),
            "source_executor_observed_delta": _observed_delta(
                start_state, completed.get("state")),
            "preceding_completed_executor_action_id": (
                preceding.get("action_id") if preceding else None),
            "preceding_completion_ordinal": (
                preceding["ordinal"] if preceding else None),
            "preceding_completion_to_start_state_agreement":
                _source_snapshot_agreement(prior_state, start_state),
            "preceding_completion_to_start_observed_delta":
                _observed_delta(prior_state, start_state),
            "active_prior_executor_action_ids": active_before_start,
            "observed_intervening_record_types": sorted({
                row["type"] for row in between
            }),
            "source_comparison": "NATIVE_CALLBACK_PROJECTIONS_ONLY",
            "source_boundary": "NATIVE_EXECUTOR_CALLBACKS_NOT_SETTLED",
            "human_input_origin": "UNVERIFIED",
            "emulator_applied": False,
        })

    linked_request_ids = set()
    # Some native callbacks (e.g. ReadyToBeginEnemyTurnAction) have a
    # RequestEnqueue identity but no canonical play_card/end_turn command.
    # Account for these observed executor starts before enumerating *real*
    # unmatched requests. Do not mislabel them as human commands.
    for action_id, started in ordered_starts:
        action = started.get("canonical_action") or {}
        if action.get("kind") in ("play_card", "end_turn"):
            continue
        request_id = (started.get("data") or {}).get("input_request_id")
        if request_id is None:
            continue
        request = requests.get(request_id)
        completed = completions.get(action_id)
        if (request is None or completed is None
            or request_id in linked_request_ids
            or request["ordinal"] >= started["ordinal"]
            or request.get("native_type") != started.get("native_type")
            or request.get("canonical_action") != started.get("canonical_action")):
            result["errors"].append(
                f"Invalid noncanonical source request/executor link for action {action_id}")
            continue
        linked_request_ids.add(request_id)
        outcome = source_outcomes.get(request_id)
        result["source_linked_noncanonical_executor_actions"].append({
            "input_request_id": request_id,
            "request_ordinal": request["ordinal"],
            "executor_action_id": action_id,
            "executor_start_ordinal": started["ordinal"],
            "executor_complete_ordinal": completed["ordinal"],
            "native_type": started.get("native_type"),
            "canonical_action": started.get("canonical_action"),
            "native_queue_return": (
                (outcome.get("data") or {}).get("queue_result")
                if outcome is not None else "NOT_OBSERVED"),
            "source_boundary": "NATIVE_CALLBACKS_NOT_FULL_LEGAL_MENU",
            "human_input_origin": "UNVERIFIED",
            "emulator_applied": False,
        })

    for action_id, started in sorted(
            starts.items(), key=lambda pair: pair[1]["ordinal"]):
        action = started.get("canonical_action") or {}
        if action.get("kind") not in ("play_card", "end_turn"):
            continue
        data = started.get("data") or {}
        request_id = data.get("input_request_id")
        if request_id is None:
            result["source_unlinked_canonical_executor_actions"].append({
                "executor_action_id": action_id,
                "executor_start_ordinal": started["ordinal"],
                "canonical_action": action,
                "reason": "NATIVE_SOURCE_REQUEST_LINK_MISSING",
            })
            continue
        request = requests.get(request_id)
        completed = completions.get(action_id)
        if request is None or completed is None:
            result["errors"].append(
                f"Executor action {action_id} has no matching native source request/completion")
            continue
        if request_id in linked_request_ids:
            result["errors"].append(
                f"Source request {request_id} linked to several executor starts")
            continue
        linked_request_ids.add(request_id)
        outcome = source_outcomes.get(request_id)
        if request["ordinal"] >= started["ordinal"]:
            result["errors"].append(
                f"Source request {request_id} was not observed before execution")
            continue
        # Pre-execution callbacks may happen synchronously inside
        # RequestEnqueue. Equality of observed states is useful for later
        # replay alignment but cannot certify a fresh native legal menu.
        prior = next((row for row in reversed(sorted_completions)
                      if row["ordinal"] < request["ordinal"]), None)
        result["source_linked_commands"].append({
            "input_request_id": request_id,
            "request_ordinal": request["ordinal"],
            "executor_action_id": action_id,
            "executor_start_ordinal": started["ordinal"],
            "executor_complete_ordinal": completed["ordinal"],
            "native_queue_return_ordinal": (
                outcome["ordinal"] if outcome is not None else None),
            "native_queue_return": (
                (outcome.get("data") or {}).get("queue_result")
                if outcome is not None else "NOT_OBSERVED"),
            "canonical_action": action,
            "pre_request_state": request.get("state"),
            "pre_execution_state": started.get("state"),
            "post_execution_state": completed.get("state"),
            "request_to_execution_state_agreement":
                _source_snapshot_agreement(
                    request.get("state"), started.get("state")),
            "last_native_completion_before_request": (
                prior.get("action_id") if prior else None),
            "last_native_completion_to_request_state_agreement":
                _source_snapshot_agreement(
                    prior.get("state") if prior else None,
                    request.get("state")),
            "source_boundary": "NATIVE_CALLBACKS_NOT_FULL_LEGAL_MENU",
            "native_executor_object_identity": "SOURCE_OBSERVED",
            "originating_human_input": "UNVERIFIED",
            "emulator_applied": False,
        })

    # A deferred/canceled native request can remain without an executor start,
    # or the same object may have been re-requested later. Retain it verbatim.
    for request_id, request in sorted(requests.items()):
        if request_id in linked_request_ids:
            continue
        outcome = source_outcomes.get(request_id)
        result["source_requests_without_executor_start"].append({
            "input_request_id": request_id,
            "request_ordinal": request["ordinal"],
            "canonical_action": request.get("canonical_action"),
            "native_queue_return": (
                (outcome.get("data") or {}).get("queue_result")
                if outcome is not None else "NOT_OBSERVED"),
            "execution_observed": False,
        })

    # Source-only UI evidence for each native play-card request. The native
    # hand guard can admit an input while the earlier executor is active.
    # Match by card ID within consecutive play-card request intervals; never
    # infer human provenance, an entire legal menu, or a settled boundary.
    ui_begins = [row for row in records
                 if row["type"] == "native_ui_play_begin"]
    frames = [row for row in records if row["type"] == "frame_state"]
    strict_gaps = {row.get("action_id"): row for row in records
                   if row["type"] == "strict_input_gap"}
    linked_by_request = {
        row["input_request_id"]: row
        for row in result["source_linked_commands"]
    }
    last_card_request_ordinal = 0
    for request in sorted(requests.values(),
                          key=lambda row: row["ordinal"]):
        canonical = request.get("canonical_action") or {}
        if canonical.get("kind") != "play_card":
            continue
        at = request["ordinal"]
        card_id = (canonical.get("payload") or {}).get("card_instance_id")
        candidates = [
            row for row in ui_begins
            if last_card_request_ordinal < row["ordinal"] < at
        ]
        last_card_request_ordinal = at
        matching = [row for row in candidates
                    if (row.get("data") or {}).get("combat_card_id")
                    == card_id]
        selected = matching[0] if len(matching) == 1 else None
        selected_at = selected["ordinal"] if selected else None
        # Read-only original native guard *return* callbacks are evidence
        # for this UI attempt when immediately before its play-begin hook.
        guard_pair = (
            records[selected_at - 3:selected_at - 1]
            if selected_at is not None else [])
        gates_passed = (len(guard_pair) == 2
            and [r["type"] for r in guard_pair] ==
                ["native_ui_hand_gate", "native_ui_hand_gate"]
            and [(r.get("data") or {}).get("gate") for r in guard_pair]
                == ["AreCardActionsAllowed", "CanPlayCards"]
            and all((r.get("data") or {}).get("returned_allowed") is True
                    and (r.get("data") or {}).get("source_evaluation_only")
                        is True for r in guard_pair))
        active_at_ui = [
            aid for aid, start in ordered_starts
            if selected_at is not None
            and start["ordinal"] < selected_at
            and (aid not in completions
                 or completions[aid]["ordinal"] > selected_at)
        ]
        prior_frame = next(
            (row for row in reversed(frames)
             if selected_at is not None
             and row["ordinal"] < selected_at), None)
        linked = linked_by_request.get(
            (request.get("data") or {}).get("input_request_id"))
        gap = strict_gaps.get(linked["executor_action_id"]) if linked else None
        result["source_ui_request_windows"].append({
            "input_request_id": (request.get("data") or {}).get("input_request_id"),
            "request_ordinal": at,
            "card_instance_id": card_id,
            "candidate_ui_begin_ordinals": [row["ordinal"] for row in matching],
            "unmatched_ui_begin_ordinals": [
                row["ordinal"] for row in candidates if row not in matching],
            "matching_status": (
                "SINGLE_TEMPORAL_CARD_ID_MATCH" if selected is not None else
                "AMBIGUOUS_UI_BEGINS" if matching else "UI_BEGIN_NOT_OBSERVED"),
            "source_ui_begin_ordinal": selected_at,
            "ui_begin_state_observation": (
                (selected.get("data") or {}).get(
                    "ui_state_observation", "UNOBSERVED_LEGACY")
                if selected else "NOT_OBSERVED"),
            "ui_begin_to_request_state_agreement":
                _source_snapshot_agreement(
                    selected.get("state") if selected else None,
                    request.get("state")),
            "original_native_hand_guards_returned_true": (
                gates_passed if selected is not None else None),
            "source_ui_guard_ordinals": (
                [row["ordinal"] for row in guard_pair]
                if gates_passed else []),
            "active_executor_action_ids_at_ui_begin": active_at_ui,
            "completed_executor_action_ids_between_ui_and_request": [
                aid for aid, completed in completions.items()
                if selected_at is not None
                and selected_at < completed["ordinal"] < at
            ],
            "last_frame_before_ui_ordinal": (
                prior_frame["ordinal"] if prior_frame else None),
            "last_frame_to_request_state_agreement":
                _source_snapshot_agreement(
                    prior_frame.get("state") if prior_frame else None,
                    request.get("state")),
            "linked_executor_action_id": (
                linked["executor_action_id"] if linked else None),
            "strict_input_gap_ordinal": gap["ordinal"] if gap else None,
            "ui_correlation": "SOURCE_TEMPORAL_CARD_ID_MATCH_ONLY",
            "strict_decision": "NOT_VERIFIED",
            "full_legal_menu": "NOT_VERIFIED",
            "human_input_origin": "UNVERIFIED",
            "emulator_applied": False,
        })

    menus = {
        (d.get("selection_owner_id"), d.get("selection_choice_id")): d
        for r in records if r["type"] == "native_hand_menu"
        for d in [r.get("data") or {}]
        if d.get("selection_id_binding") == "SOURCE_ASYNC_FLOW_RESERVED_ID"
    }
    for record in records:
        if record["type"] != "native_player_choice":
            continue
        details = record.get("data") or {}
        key = (details.get("owner_id"), details.get("choice_id"))
        menu = menus.get(key)
        result["native_choice_results"].append({
            "choice_ordinal": record["ordinal"],
            "containing_executor_action_id": record.get("action_id"),
            "owner_id": details.get("owner_id"),
            "choice_id": details.get("choice_id"),
            "choice_type": details.get("choice_type"),
            "selected_combat_card_ids": details.get("selected_combat_card_ids"),
            "selected_indexes": details.get("selected_indexes"),
            "selected_canonical_card_models":
                details.get("selected_canonical_card_models"),
            "native_initial_hand_menu_id": (
                menu.get("menu_id") if menu is not None else None),
            "native_hand_menu_binding": (
                "SOURCE_ASYNC_FLOW_RESERVED_ID"
                if menu is not None else "NOT_OBSERVED"),
            "full_legal_menu": "NOT_VERIFIED",
            "emulator_applied": False,
        })

    if result["errors"]:
        return result
    if identity is None:
        result["status"] = "SOURCE_IDENTITY_MISSING"
    elif result["source_unlinked_canonical_executor_actions"]:
        result["status"] = "SOURCE_INPUT_COVERAGE_PARTIAL"
    elif qualified_resets is not None:
        result["status"] = "SOURCE_ACTION_CHRONOLOGY_HISTORY_RESET_UNRESOLVED"
    else:
        result["status"] = "SOURCE_ACTION_CHRONOLOGY_OBSERVED"
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sidecar", type=Path, required=True)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load_sidecar(args.sidecar)
    result = build(records, error)
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(rendered, end="")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered, encoding="utf-8")
    return 0 if result["status"] in (
        "SOURCE_ACTION_CHRONOLOGY_OBSERVED",
        "SOURCE_ACTION_CHRONOLOGY_HISTORY_RESET_UNRESOLVED") else 2


if __name__ == "__main__":
    raise SystemExit(main())
