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
from native_action_envelope_audit import load as load_sidecar


def _source_snapshot_agreement(left, right) -> str:
    """Compare captured native states without declaring a decision boundary."""
    if not isinstance(left, dict) or not isinstance(right, dict):
        return "UNOBSERVED"
    return ("SOURCE_OBSERVED_EQUAL" if left == right
            else "SOURCE_OBSERVED_DIFFERENT")


def build(records: list[dict], error: str | None = None) -> dict:
    result: dict = {
        "schema": "native-source-action-journal-v1",
        "status": "INVALID_SOURCE_CAPTURE",
        "source_audit": None,
        "game_build": None,
        "run_id": None,
        "run_seed": None,
        "ascension": None,
        "source_linked_commands": [],
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
    if source["status"] != "CALLBACK_TIMELINE_CONSISTENT":
        result["errors"].append(source.get("reason") or "Source sidecar audit failed")
        return result

    header = records[0].get("data") or {}
    result["game_build"] = header.get("game_build")
    result["run_id"] = header.get("run_id")
    identity = source.get("native_run_identity")
    if identity is not None:
        result["run_seed"] = identity["seed"]
        result["ascension"] = identity["ascension"]

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

    # Every executor start/complete pair is directly observed; include
    # automatic actions as well as candidate human commands. An action
    # completion can be followed by additional native effects, so none of
    # these snapshots is promoted to a settled semantic decision.
    for action_id, started in sorted(
            starts.items(), key=lambda pair: pair[1]["ordinal"]):
        completed = completions.get(action_id)
        if completed is None:
            result["errors"].append(
                f"Native executor action {action_id} has no observed completion")
            continue
        result["source_executor_observed_transitions"].append({
            "executor_action_id": action_id,
            "native_type": started.get("native_type"),
            "canonical_action_candidate": started.get("canonical_action"),
            "start_ordinal": started["ordinal"],
            "complete_ordinal": completed["ordinal"],
            "pre_execution_state": started.get("state"),
            "post_execution_state": completed.get("state"),
            "source_boundary": "NATIVE_EXECUTOR_CALLBACKS_NOT_SETTLED",
            "human_input_origin": "UNVERIFIED",
            "emulator_applied": False,
        })

    sorted_completions = sorted(
        completions.values(), key=lambda row: row["ordinal"])

    linked_request_ids = set()
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
    return 0 if result["status"] == "SOURCE_ACTION_CHRONOLOGY_OBSERVED" else 2


if __name__ == "__main__":
    raise SystemExit(main())
