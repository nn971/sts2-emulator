#!/usr/bin/env python3
"""Extract qualified *source-observed* player/enemy turn-boundary candidates.

This accepts previously captured v111 action sidecars without requiring
the new native_turn_checkpoint hooks. It establishes only callback/frame
chronology, never native queue settlement or emulator parity.

Player candidate: last changed frame of a turn before its first native
player-command request, with no active executor and identical projections.
Enemy candidate: state immediately before the native
ReadyToBeginEnemyTurnAction executor, following an EndPlayerTurnAction.
Neither observation certifies a complete native phase handoff.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_action_envelope_audit import audit as audit_sidecar
from native_action_envelope_audit import load as load_sidecar


def _turn(row: dict) -> int | None:
    state = row.get("state")
    if not isinstance(state, dict):
        return None
    combat = state.get("combat")
    turn = combat.get("turn") if isinstance(combat, dict) else None
    return turn if type(turn) is int and turn > 0 else None


def _active_at(records: list[dict], ordinal: int) -> list[int]:
    active = set()
    for row in records:
        if row["ordinal"] >= ordinal:
            break
        if row["type"] == "action_started":
            active.add(row["action_id"])
        elif row["type"] == "action_completed":
            active.discard(row["action_id"])
    return sorted(active)


def analyze(records: list[dict], error: str | None = None) -> dict:
    source = audit_sidecar(records, error)
    result = {
        "schema": "native-turn-boundary-candidate-diagnostics-v1",
        "source_audit": source["status"],
        "status": "SOURCE_AUDIT_BLOCKED",
        "player_turn_starts": [],
        "enemy_turn_starts": [],
        "parity_checkpoints": 0,
        "native_settlement": "NOT_VERIFIED",
        "native_legal_menu": "NOT_VERIFIED",
        "emulator_replay": "NOT_EXECUTED",
        "emulator_parity": "NOT_TESTED",
        "reason": source.get("reason"),
    }
    if source["status"] != "CALLBACK_TIMELINE_CONSISTENT":
        return result
    result["status"] = "SOURCE_BOUNDARY_CANDIDATES_OBSERVED"
    requests = [
        row for row in records
        if row["type"] == "native_input_request"
        and isinstance(row.get("canonical_action"), dict)
        and row["canonical_action"].get("kind") in ("play_card", "end_turn")
    ]
    first_request_per_turn = {}
    for row in requests:
        turn = _turn(row)
        if turn is not None and turn not in first_request_per_turn:
            first_request_per_turn[turn] = row

    frames = [row for row in records if row["type"] == "frame_state"]
    for turn, request in sorted(first_request_per_turn.items()):
        before = [frame for frame in frames
                  if frame["ordinal"] < request["ordinal"]
                  and _turn(frame) == turn]
        frame = before[-1] if before else None
        idle = frame is not None and not _active_at(
            records, frame["ordinal"])
        equal = frame is not None and frame.get("state") == request.get("state")
        result["player_turn_starts"].append({
            "turn": turn,
            "source_frame_ordinal": frame["ordinal"] if frame else None,
            "first_player_request_ordinal": request["ordinal"],
            "frame_projection_equals_request": equal,
            "source_executor_idle_at_frame": idle if frame else None,
            "candidate_status": (
                "SOURCE_FRAME_REQUEST_PROJECTIONS_AGREE"
                if idle and equal else "UNVERIFIED_OR_AMBIGUOUS"),
            "native_settlement": "NOT_VERIFIED",
            "emulator_parity": "NOT_TESTED",
        })

    complete_by_ordinal = {
        row["ordinal"]: row for row in records
        if row["type"] == "action_completed"
    }
    starts = [row for row in records if row["type"] == "action_started"]
    for row in starts:
        native_type = row.get("native_type") or ""
        if not native_type.endswith("ReadyToBeginEnemyTurnAction"):
            continue
        turn = _turn(row)
        previous = next(
            (record for record in reversed(records[:row["ordinal"] - 1])
             if record["type"] == "action_completed"), None)
        # Native automatic action *starts* are sequential in the observed
        # capture, but the source metadata has no verified full-queue state.
        preceded_end_turn = (previous is not None and
            (previous.get("native_type") or "").endswith(
                "EndPlayerTurnAction"))
        no_other_active = not _active_at(records, row["ordinal"])
        result["enemy_turn_starts"].append({
            "turn": turn,
            "source_pre_executor_ordinal": row["ordinal"],
            "previous_completed_executor_ordinal": (
                previous["ordinal"] if previous else None),
            "previous_executor_was_end_turn": preceded_end_turn,
            "previous_completion_projection_equals_candidate": (
                previous.get("state") == row.get("state")
                if previous is not None else None),
            "no_other_executor_active": no_other_active,
            "candidate_status": (
                "SOURCE_PRE_ENEMY_SETUP_OBSERVED"
                if preceded_end_turn and no_other_active and turn is not None
                else "UNVERIFIED_OR_AMBIGUOUS"),
            "atomic_before_first_enemy_effect": "NOT_VERIFIED",
            "native_settlement": "NOT_VERIFIED",
            "emulator_parity": "NOT_TESTED",
        })
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sidecar", type=Path, required=True)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load_sidecar(args.sidecar)
    result = analyze(records, error)
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if result["status"] == "SOURCE_BOUNDARY_CANDIDATES_OBSERVED" else 2


if __name__ == "__main__":
    raise SystemExit(main())
