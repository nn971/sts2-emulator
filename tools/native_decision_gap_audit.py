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
    last_matched_end = 0
    last_start_id = None
    last_end_by_id = {}
    nested_pause = []
    frames = []
    candidates = []
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
        elif kind == "action_completed":
            if type(record.get("action_id")) is int:
                last_end_by_id[record["action_id"]] = i
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
                last_start_id = aid
                continue
            report["unmatched_actions"] += 1
            if report["first_gap"] is None:
                from_ordinal = last_end_by_id.get(last_start_id, last_matched_end)
                pauses = [n for n, _ in nested_pause if n > from_ordinal]
                recent_frames = [n for n in frames if from_ordinal < n < i]
                report["first_gap"] = {
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
