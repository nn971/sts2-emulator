#!/usr/bin/env python3
"""L0 native Inspector evidence gate for a *fresh, explicitly selected* sidecar.

Reuses the existing native capture, xoshiro and checkpoint verifiers. This
is source-observation acceptance only: it never advances emulator mechanics,
samples RNG, launches the game or asserts native/emulator lockstep parity.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from native_action_envelope_audit import audit as audit_envelope, load
from native_rng_boundary_verify import audit as audit_primitives
from native_rng_checkpoint_audit import audit as audit_checkpoints
from native_parity import load_trace
from native_trace_audit import audit as audit_strict_trace
from native_decision_gap_audit import audit as audit_decision_gap

_SHA_IN_BUILD = re.compile(r"(?<![a-z0-9])sha256=([0-9a-f]{64})(?![a-z0-9])", re.I)


def assess(preflight: dict, records: list[dict], read_error: str | None,
           game_log: str | None, strict_trace: dict | None = None,
           decision_gap: dict | None = None) -> dict:
    """Return statuses without promoting an observed primitive to game parity."""
    header = (records[0].get("data") or {}) if (
        records and records[0].get("type") == "header") else {}
    expected = (preflight.get("managed_assemblies", {})
                .get("sts2.dll", {}).get("sha256"))
    hashes = _SHA_IN_BUILD.findall(str(header.get("game_build") or ""))
    pin_ok = (preflight.get("dependencies_present") is True
              and isinstance(expected, str) and len(expected) == 64
              and len(hashes) == 1 and hashes[0].lower() == expected.lower())

    envelope = audit_envelope(records, read_error)
    primitives = audit_primitives(records)
    checkpoints = audit_checkpoints(records)
    monster = checkpoints.get("streams", {}).get("run.monster_ai", {})
    monster_float_calls = sum(
        row.get("type") == "native_rng_scalar_boundary"
        and isinstance(row.get("data"), dict)
        and row["data"].get("operation_kind") == "range_float"
        and row["data"].get("stream_id") == "run.monster_ai"
        for row in records
    )
    flags = {
        key: header.get(key) is True
        for key in (
            "native_rng_hook_installed",
            "native_rng_float_hook_installed",
            "native_rng_stream_checkpoints_enabled",
            "native_monster_ai_scope_installed",
        )
    }
    logged = (game_log is not None
              and "CAPTURE_STATUS=HOOK_ENABLED" in game_log
              and "RNG_HOOK_STATUS=INSTALLED_BOUND_INT_OVERLOADS" in game_log
              and "float_source_hook=INSTALLED" in game_log
              and isinstance(expected, str)
              and ("sha256=" + expected.lower()) in game_log.lower())

    failures = []
    pending = []
    if not pin_ok:
        failures.append("SIDE_CAR_GAME_BUILD_DOES_NOT_MATCH_INSTALLED_PREFLIGHT")
    if read_error:
        failures.append("SIDECAR_READ_OR_WRITER_FAILURE")
    if envelope["status"] not in (
        "CALLBACK_TIMELINE_CONSISTENT",
        "LEGACY_CALLBACK_SEQUENCE_RECOVERABLE",
    ):
        failures.append("NATIVE_CALLBACK_TIMELINE_INCOMPLETE")
    elif envelope["status"] != "CALLBACK_TIMELINE_CONSISTENT":
        pending.append("LEGACY_CAPTURE_ONLY_RECOVERABLE")
    if primitives["primitive_error_count"] or primitives["stream_conflict_count"]:
        failures.append("NATIVE_RNG_PRIMITIVE_OR_STREAM_CONFLICT")
    if checkpoints["error_count"]:
        failures.append("NATIVE_RNG_CHECKPOINT_ERROR")
    if not all(flags.values()):
        pending.append("NATIVE_HOOK_FLAGS_INCOMPLETE")
    if not logged:
        pending.append("GAME_LOG_HOOK_BUILD_EVIDENCE_UNVERIFIED")
    if not primitives["source_calls"]:
        pending.append("NO_SOURCE_RNG_CALL_OBSERVED")
    if not monster_float_calls:
        pending.append("NO_MONSTER_AI_NEXTFLOAT_OBSERVED")
    if not monster.get("native_rng_advances", 0):
        pending.append("MONSTER_AI_RNG_NOT_EXERCISED_BETWEEN_CHECKPOINTS")
    if (monster.get("unobserved_advances", 0)
        or monster.get("large_windows_unverified", 0)):
        pending.append("MONSTER_AI_STREAM_HAS_UNOBSERVED_ADVANCES")
    if checkpoints["status"] != "OBSERVED_PERSISTENT_STREAM_ADVANCES_ACCOUNTED_FOR":
        pending.append("PERSISTENT_STREAM_COVERAGE_NOT_COMPLETE")
    if primitives["stream_gap_count"]:
        pending.append("SOURCE_STREAM_CALL_GAPS_REMAIN")
    if decision_gap is not None:
        if decision_gap["status"] == "INVALID_PAIRED_CAPTURE":
            failures.append("NATIVE_DECISION_EXECUTOR_PAIRING_INVALID")
        elif decision_gap["status"] == "FIRST_UNCAPTURED_EXECUTOR_ACTION_FOUND":
            pending.append("EXECUTOR_ACTION_WITHOUT_STRICT_BOUNDARY")
    if strict_trace is not None and strict_trace["status"] != "STRUCTURALLY_VALID":
        # An incomplete strict trace does not invalidate source RNG evidence,
        # but cannot count as a complete player-decision capture.
        pending.append("STRICT_DECISION_TRACE_INCOMPLETE")

    status = ("L0_EVIDENCE_REJECTED" if failures
              else "L0_SOURCE_EVIDENCE_PARTIAL" if pending
              else "L0_SOURCE_EVIDENCE_CONFIRMED_NONPERTURBATION_PENDING")
    return {
        "schema": "sts2-native-l0-evidence-v1",
        "status": status,
        "installed_build_pin": "MATCH" if pin_ok else "INVALID_OR_UNVERIFIED",
        "native_callback_status": envelope["status"],
        "native_callback_reason": envelope["reason"],
        "native_source_input_observation": {
            "input_hook_reported_installed":
                envelope.get("native_input_hook_installed", False),
            "hand_menu_hook_reported_installed":
                envelope.get("native_hand_menu_hook_installed", False),
            "manual_play_hook_reported_installed":
                envelope.get("native_manual_play_hook_installed", False),
            "native_filtered_hand_menus":
                len(envelope.get("native_hand_menus", [])),
            "hand_menus_with_complete_ui_id_projection":
                sum(item.get("coverage") ==
                    "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"
                    for item in envelope.get("native_hand_menus", [])),
            "native_manual_play_results":
                len(envelope.get("native_manual_play_results", [])),
            "source_requests": len(envelope.get("native_input_requests", [])),
            "source_return_outcomes":
                len(envelope.get("native_input_outcomes", [])),
            "requests_linked_to_executor":
                envelope.get("source_request_execution_links", 0),
            "source_choice_results":
                len(envelope.get("native_player_choices", [])),
            "native_run_identity": envelope.get("native_run_identity"),
            "legal_menu_coverage": "NOT_TESTED",
            "native_input_nonperturbation": "NOT_TESTED",
        },
        "strict_decision_trace_status": (
            strict_trace["status"] if strict_trace is not None else "NOT_AUDITED"),
        "strict_decision_trace_reason": (
            strict_trace.get("reason") if strict_trace is not None else None),
        "strict_decision_count": (
            strict_trace.get("decisions") if strict_trace is not None else None),
        "executor_decision_pairing": decision_gap,
        "source_hook_flags": flags,
        "matching_game_log_hook_evidence": "OBSERVED" if logged else "NOT_VERIFIED",
        "native_rng_primitive_status": primitives["status"],
        "native_rng_primitive_source_calls": primitives["source_calls"],
        "native_rng_primitive_errors": primitives["primitive_error_count"],
        "native_rng_stream_conflicts": primitives["stream_conflict_count"],
        "native_rng_stream_gaps": primitives["stream_gap_count"],
        "native_rng_float_calls": primitives["float_calls"],
        "native_monster_ai_float_calls": monster_float_calls,
        "native_rng_checkpoint_status": checkpoints["status"],
        "native_rng_checkpoint_count": checkpoints["checkpoint_count"],
        "native_rng_checkpoint_errors": checkpoints["error_count"],
        "native_rng_checkpoint_unobserved_advances":
            checkpoints["unobserved_rng_advances"],
        "monster_ai_stream": {
            "native_rng_advances": monster.get("native_rng_advances", 0),
            "hook_observed_advances": monster.get("hook_observed_advances", 0),
            "unobserved_advances": monster.get("unobserved_advances", 0),
            "large_windows_unverified": monster.get("large_windows_unverified", 0),
        },
        "hard_failures": failures,
        "remaining_capture_gates": pending,
        "native_nonperturbation": "NOT_TESTED",
        "game_source_semantic_eligibility_parity": "NOT_TESTED",
        "context_rng_coverage": "NOT_TESTED",
        "native_emulator_exact_rng_parity": "NOT_TESTED",
        "native_emulator_mechanics_parity": "NOT_TESTED",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preflight", type=Path, required=True,
                        help="JSON from native_game_preflight.py for the installed DLL")
    parser.add_argument("--sidecar", type=Path, required=True,
                        help="Explicit new passive-*.actions.jsonl, not a directory")
    parser.add_argument("--game-log", type=Path,
                        help="Fresh game session log with CAPTURE/RNG_HOOK statuses")
    parser.add_argument("--strict-trace", type=Path,
                        help="Matching passive-*.jsonl decision trace (auto-detected "
                             "beside passive-*.actions.jsonl when omitted)")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    preflight = json.loads(args.preflight.read_text(encoding="utf-8"))
    records, error = load(args.sidecar)
    game_log = args.game_log.read_text(encoding="utf-8") if args.game_log else None
    companion = args.strict_trace
    if companion is None and args.sidecar.name.endswith(".actions.jsonl"):
        companion = args.sidecar.with_name(
            args.sidecar.name.removesuffix(".actions.jsonl") + ".jsonl")
    strict_trace = None
    decision_gap = None
    if companion is not None:
        if companion.exists():
            strict_records, strict_error = load_trace(companion)
            strict_trace = audit_strict_trace(strict_records, strict_error)
            if strict_error is None and error is None:
                decision_gap = audit_decision_gap(strict_records, records)
        else:
            strict_trace = {"status": "INCOMPLETE_CAPTURE",
                            "reason": "Matching native decision trace missing",
                            "decisions": 0}
    report = assess(preflight, records, error, game_log,
                    strict_trace, decision_gap)
    rendered = json.dumps(report, indent=2, sort_keys=True) + "\n"
    print(rendered, end="")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered, encoding="utf-8")
    return 1 if report["hard_failures"] else (
        0 if report["status"] ==
        "L0_SOURCE_EVIDENCE_CONFIRMED_NONPERTURBATION_PENDING" else 2)


if __name__ == "__main__":
    raise SystemExit(main())
