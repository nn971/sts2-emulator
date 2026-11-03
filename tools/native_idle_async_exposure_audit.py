"""Audit native activity after sampled player-idle frames and before input.

Pinned v0.111 source facts:
- GameAction.Execute waits for the execution Task OR a player-choice pause.
- HookPlayerChoiceContext's await helpers may also return at a pause.
- ActionExecutor.AfterActionExecuted is raised before its next
  CheckWinCondition await and before CurrentlyRunningAction is cleared.
- CombatManager's turn-loop and later async hooks are not exhaustively
  inventoried by the native passive action sidecar.

Consequently an idle source frame proves only sampled properties. This
auditor records *observed* mechanical activity before the next player input;
absence of captured events cannot certify no delayed work.
"""
from __future__ import annotations

from native_action_window_audit import _first_difference

TRACKED = (
    "native_rng_boundary", "native_rng_scalar_boundary",
    "history_entry", "native_phase_handoff", "action_started",
    "nested_choice_pause", "action_resumed",
    "native_shuffle_operation", "native_initial_hp_roll",
)
INPUT = frozenset(("native_input_request",))
CANONICAL = frozenset(("play_card", "end_turn"))


def audit_idle_frame_exposures(records: list[dict]) -> dict:
    result = {
        "status": "NOT_CAPTURED",
        "player_idle_frames_observed": 0,
        "windows_reaching_next_input": 0,
        "windows_reaching_next_action_without_input_marker": 0,
        "windows_unclosed": 0,
        "windows_with_preinput_tracked_activity": 0,
        "windows_with_preinput_rng_calls": 0,
        "windows_with_changed_visible_projection_before_action": 0,
        "source_async_task_inventory_exhaustive": False,
        "native_settlement_certified": False,
        "unobserved_task_classes": [
            "PAUSED_GAME_ACTION_EXECUTION_TASK",
            "HOOK_PLAYER_CHOICE_TASK_AFTER_PAUSE",
            "FUTURE_TURN_LOOP_OR_EXTERNAL_ASYNC_CONTINUATION",
        ],
        "windows": [],
    }
    if not isinstance(records, list):
        return result
    active = None

    def finalize(*, next_action=None, end_reason):
        nonlocal active
        if active is None:
            return
        if next_action is not None:
            active["next_action_start_ordinal"] = next_action.get("ordinal")
            state = next_action.get("state")
            first = active.pop("_source_state")
            if isinstance(first, dict) and isinstance(state, dict):
                path = _first_difference(first, state)
                active["pre_action_projection_relation"] = (
                    "UNCHANGED_FROM_IDLE_FRAME"
                    if path is None else "DIFFERENT_BEFORE_EXECUTOR_ACTION")
                active["first_projection_difference_path"] = path
                if path is not None:
                    result["windows_with_changed_visible_projection_before_action"] += 1
            else:
                active["pre_action_projection_relation"] = "UNOBSERVED"
                active["first_projection_difference_path"] = None
        else:
            active.pop("_source_state")
            active["pre_action_projection_relation"] = "UNOBSERVED"
            active["first_projection_difference_path"] = None

        active["end_reason"] = end_reason
        active["native_settlement"] = "SETTLEMENT_UNVERIFIED"
        if active["first_input_request_ordinal"] is not None:
            result["windows_reaching_next_input"] += 1
        elif next_action is not None:
            result["windows_reaching_next_action_without_input_marker"] += 1
        else:
            result["windows_unclosed"] += 1
        if any(active["preinput_event_counts"].values()):
            active["preinput_activity_observation"] = (
                "TRACKED_SOURCE_ACTIVITY_OBSERVED_BEFORE_INPUT")
            result["windows_with_preinput_tracked_activity"] += 1
        else:
            active["preinput_activity_observation"] = (
                "NO_TRACKED_SOURCE_ACTIVITY_OBSERVED_BEFORE_INPUT")
        if (active["preinput_event_counts"]["native_rng_boundary"]
            or active["preinput_event_counts"]["native_rng_scalar_boundary"]):
            result["windows_with_preinput_rng_calls"] += 1
        result["windows"].append(active)
        active = None

    for row in records:
        if not isinstance(row, dict):
            continue
        kind = row.get("type")
        if kind == "native_turn_checkpoint":
            data = row.get("data")
            if not isinstance(data, dict) or data.get("kind") != "PLAYER_TURN_START":
                continue
            if active is not None:
                finalize(end_reason="NEXT_PLAYER_CHECKPOINT_BEFORE_CANONICAL_ACTION")
            # The strict envelope audit separately validates that this was
            # a playable frame and inspects its eight queue and three effect
            # flags. Preserve reported source evidence without upgrading it.
            active = {
                "turn": data.get("turn"),
                "idle_frame_ordinal": row.get("ordinal"),
                "first_input_request_ordinal": None,
                "next_action_start_ordinal": None,
                "source_queue_observation": data.get(
                    "queue_quiescence_status", "NOT_CAPTURED_LEGACY"),
                "source_effect_observation": data.get(
                    "automatic_effect_evidence_status", "NOT_CAPTURED_LEGACY"),
                "preinput_event_counts": {key: 0 for key in TRACKED},
                "preinput_first_event_ordinal": None,
                "_source_state": row.get("state"),
            }
            result["player_idle_frames_observed"] += 1
            continue
        if active is None:
            continue
        if kind in INPUT and active["first_input_request_ordinal"] is None:
            active["first_input_request_ordinal"] = row.get("ordinal")
            # After this point, pre-executor mutations can be caused by
            # input submission, so never attribute them to background work.
            continue
        if (kind == "action_started"
            and isinstance(row.get("canonical_action"), dict)
            and row["canonical_action"].get("kind") in CANONICAL):
            finalize(next_action=row, end_reason="CANONICAL_ACTION_OBSERVED")
            continue
        if active["first_input_request_ordinal"] is None and kind in TRACKED:
            active["preinput_event_counts"][kind] += 1
            if active["preinput_first_event_ordinal"] is None:
                active["preinput_first_event_ordinal"] = row.get("ordinal")

    if active is not None:
        finalize(end_reason="CAPTURE_TERMINATED_BEFORE_CANONICAL_ACTION")
    if result["player_idle_frames_observed"]:
        result["status"] = "SOURCE_IDLE_TO_INPUT_ACTIVITY_OBSERVED_UNCERTIFIED"
    return result
