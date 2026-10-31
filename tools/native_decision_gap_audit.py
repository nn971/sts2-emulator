#!/usr/bin/env python3
"""Locate first uncaptured canonical player-action *candidate* in paired v111 traces.

The executor is a source observation, not a proof of human/UI origin. This
tool aligns its action-start envelope with actual strict decision/action pairs.
No missing legal menu, input timing, or native result is reconstructed.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_action_envelope_audit import load as load_sidecar
from native_parity import load_trace


def _canonical(action):
    if not isinstance(action, dict):
        return None
    if action.get("kind") not in ("play_card", "end_turn"):
        return None
    return (action["kind"], json.dumps(
        action.get("payload"), sort_keys=True, separators=(",", ":")))


def audit(strict: list[dict], sidecar: list[dict]) -> dict:
    report = {
        "schema": "native-decision-gap-audit-v1",
        "status": "INVALID_PAIRED_CAPTURE",
        "strict_decisions": 0,
        "strict_actions": 0,
        "executor_player_action_candidates": 0,
        "matched_actions": 0,
        "unmatched_actions": 0,
        "first_gap": None,
        "errors": [],
        "action_origin": "EXECUTOR_ACTION_TYPE_ONLY_HUMAN_ORIGIN_UNVERIFIED",
        "legal_menu_reconstruction": "NOT_PERFORMED",
        "emulator_parity": "NOT_TESTED",
    }
    if (not strict or strict[0].get("type") != "header"
        or not sidecar or sidecar[0].get("type") != "header"):
        report["errors"].append("MISSING_PAIR_HEADER")
        return report
    hdr, sidehdr = strict[0], sidecar[0].get("data") or {}
    if (hdr.get("run_id") != sidehdr.get("run_id")
        or hdr.get("game_build") != sidehdr.get("game_build")):
        report["errors"].append("PAIRED_PROVENANCE_MISMATCH")
        return report
    decisions = {r["decision_id"]: r for r in strict
                 if r.get("type") == "decision"}
    actions = {r["decision_id"]: r for r in strict
               if r.get("type") == "action"}
    report["strict_decisions"] = len(decisions)
    report["strict_actions"] = len(actions)
    if len(decisions) != sum(r.get("type") == "decision" for r in strict):
        report["errors"].append("DUPLICATE_STRICT_DECISION")
    if len(actions) != sum(r.get("type") == "action" for r in strict):
        report["errors"].append("DUPLICATE_STRICT_ACTION")
    if report["errors"]:
        return report
    stable_id = None
    consumed = set()
    last_matched_start = 0
    nested_pause = []
    frames = []
    candidates = []
    # RequestEnqueue may execute an action synchronously before its postfix;
    # index all source return observations before walking executor starts.
    requested = {}
    returned = {}
    for row in sidecar:
        details = row.get("data") or {}
        if row.get("type") == "native_input_request":
            requested[details.get("input_request_id")] = row
        elif row.get("type") == "native_input_outcome":
            returned[details.get("input_request_id")] = row
    choices = []
    hand_menus = []
    menu_revisions = []
    menu_closures = []
    for i, record in enumerate(sidecar, 1):
        if record.get("ordinal") != i:
            report["errors"].append("NONCONSECUTIVE_SIDECAR_ORDINAL")
            break
        kind = record.get("type")
        if kind == "stable_decision":
            stable_id = (record.get("data") or {}).get("decision_id")
            if stable_id not in decisions:
                report["errors"].append("SIDECAR_DECISION_ABSENT_FROM_STRICT")
                break
            if record.get("state") != decisions[stable_id].get("state"):
                report["errors"].append("SIDECAR_DECISION_STATE_MISMATCH")
                break
        elif kind == "nested_choice_pause":
            nested_pause.append((i, record.get("action_id")))
        elif kind == "frame_state":
            frames.append(i)
        elif kind == "native_player_choice":
            choices.append((i, record.get("data") or {}))
        elif kind == "native_hand_menu":
            hand_menus.append((i, record.get("data") or {}))
        elif kind == "native_hand_menu_revision":
            menu_revisions.append((i, record.get("data") or {}))
        elif kind == "native_hand_menu_closed":
            menu_closures.append((i, record.get("data") or {}))
        elif kind == "action_started":
            chosen = _canonical(record.get("canonical_action"))
            if chosen is None:
                continue
            aid = record.get("action_id")
            candidates.append((i, aid))
            report["executor_player_action_candidates"] += 1
            expected = actions.get(stable_id)
            if (stable_id not in consumed and expected is not None
                and expected.get("accepted") is True
                and _canonical(expected.get("action")) == chosen):
                consumed.add(stable_id)
                report["matched_actions"] += 1
                last_matched_start = i
                continue
            report["unmatched_actions"] += 1
            if report["first_gap"] is None:
                # Include pauses *during* the preceding matched action, whose
                # suspension/resume can consume a second human input.
                pauses = [n for n, _ in nested_pause if last_matched_start < n < i]
                recent_frames = [n for n in frames if last_matched_start < n < i]
                input_id = (record.get("data") or {}).get("input_request_id")
                source_request = requested.get(input_id) if input_id is not None else None
                source_return = returned.get(input_id) if input_id is not None else None
                selected = [(n, d) for n, d in choices
                            if last_matched_start < n < i]
                displayed = [(n, d) for n, d in hand_menus
                             if last_matched_start < n < i]
                report["first_gap"] = {
                    "source_native_hand_menus_since_prior_action": [
                        {
                            "ordinal": n, "menu_id": d.get("menu_id"),
                            "source_model_id": d.get("source_model_id"),
                            "coverage": d.get("coverage"),
                            "option_ids": [
                                o.get("combat_card_id")
                                for o in d.get("options", [])
                                if isinstance(o, dict)],
                            "selection_id_binding":
                                d.get("selection_id_binding"),
                            "selection_owner_id":
                                d.get("selection_owner_id"),
                            "selection_choice_id":
                                d.get("selection_choice_id"),
                        } for n, d in displayed
                    ],
                    "source_native_menu_revisions_since_prior_action": [
                        {
                            "ordinal": n,
                            "menu_id": d.get("menu_id"),
                            "revision": d.get("revision"),
                            "peek_mode": d.get("peek_mode"),
                            "coverage": d.get("coverage"),
                            "option_ids": [
                                o.get("combat_card_id")
                                for o in d.get("options", [])
                                if isinstance(o, dict)]
                        } for n, d in menu_revisions
                        if last_matched_start < n < i
                    ],
                    "source_native_menu_closures_since_prior_action": [
                        {
                            "ordinal": n, "menu_id": d.get("menu_id"),
                            "final_revision": d.get("final_revision"),
                            "native_reason": d.get("native_reason")
                        } for n, d in menu_closures
                        if last_matched_start < n < i
                    ],
                    "source_input_request_id": input_id,
                    "source_input_request_ordinal": (
                        source_request.get("ordinal") if source_request else None),
                    "source_queue_return_classification": (
                        (source_return.get("data") or {}).get("queue_result")
                        if source_return else None),
                    "source_selected_choices_since_prior_action": [
                        {
                            "ordinal": n,
                            "choice_id": d.get("choice_id"),
                            "choice_type": d.get("choice_type"),
                            "selected_combat_card_ids":
                                d.get("selected_combat_card_ids"),
                        } for n, d in selected
                    ],
                    "sidecar_ordinal": i,
                    "executor_action_id": aid,
                    "native_type": record.get("native_type"),
                    "candidate_action": record.get("canonical_action"),
                    "last_strict_decision_id": stable_id,
                    "strict_action_already_consumed": stable_id in consumed,
                    "nested_pause_since_prior_matched_action": bool(pauses),
                    "nested_pause_ordinals": pauses[:10],
                    "frame_samples_since_prior_matched_action": len(recent_frames),
                    "last_frame_ordinal": recent_frames[-1] if recent_frames else None,
                    "pre_action_state_observed": isinstance(record.get("state"), dict),
                    "gap_type": ("UNSUPPORTED_NESTED_CHOICE_CONTINUATION"
                                 if pauses else
                                 "EXECUTOR_ACTION_WITHOUT_NEW_STABLE_BOUNDARY"),
                    "diagnostic_only": True,
                }
    if report["errors"]:
        return report
    if report["first_gap"] is not None:
        gap = report["first_gap"]
        candidate = gap.get("candidate_action") or {}
        payload = candidate.get("payload") or {}
        cid, target = payload.get("card_instance_id"), payload.get("target_id")
        if candidate.get("kind") == "play_card":
            matching_manual = [
                {
                    "ordinal": r.get("ordinal"),
                    "returned_success": (r.get("data") or {}).get("returned_success"),
                    "source": (r.get("data") or {}).get("source")
                } for r in sidecar
                if r.get("type") == "native_manual_play_result"
                and (r.get("data") or {}).get("combat_card_id") == cid
                and (r.get("data") or {}).get("target_id") == target
            ]
        else:
            matching_manual = []
        gap["source_matching_manual_play_return_candidates"] = matching_manual
        gap["manual_play_correlation"] = "TEMPORAL_CANDIDATES_NOT_UNIQUE_PROOF"
        gap["source_matching_ui_play_begin_candidates"] = [
            {
                "ordinal": r.get("ordinal"),
                "started_via_shortcut":
                    (r.get("data") or {}).get("started_via_shortcut"),
                "native_ui_gate":
                    (r.get("data") or {}).get("native_ui_gate")
            } for r in sidecar
            if r.get("type") == "native_ui_play_begin"
            and (r.get("data") or {}).get("combat_card_id") == cid
        ] if candidate.get("kind") == "play_card" else []
        gap["ui_play_correlation"] = "CARD_ID_CANDIDATES_NOT_UNIQUE_PROOF"
    unpaired_strict = sorted(set(actions) - consumed)
    if unpaired_strict:
        report["errors"].append("STRICT_ACTION_WITHOUT_MATCHED_EXECUTOR_START")
        report["unpaired_strict_decisions"] = unpaired_strict
        return report
    terminal = next((r for r in reversed(strict)
                     if r.get("type") == "terminal"), {})
    report["strict_terminal_status"] = terminal.get("status")
    report["strict_terminal_reason"] = terminal.get("reason")
    report["status"] = ("FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND"
                        if report["unmatched_actions"] else
                        "NO_EXECUTOR_ACTION_GAP_OBSERVED")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strict", required=True, type=Path)
    parser.add_argument("--sidecar", required=True, type=Path)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    strict, strict_error = load_trace(args.strict)
    sidecar, sidecar_error = load_sidecar(args.sidecar)
    if strict_error or sidecar_error:
        report = {"schema": "native-decision-gap-audit-v1",
                  "status": "INVALID_PAIRED_CAPTURE",
                  "errors": [x for x in (strict_error, sidecar_error) if x]}
    else:
        report = audit(strict, sidecar)
    output = json.dumps(report, indent=2, sort_keys=True) + "\n"
    print(output, end="")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return 1 if report["status"] == "INVALID_PAIRED_CAPTURE" else (
        2 if report["status"] == "FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND" else 0)


if __name__ == "__main__":
    raise SystemExit(main())
