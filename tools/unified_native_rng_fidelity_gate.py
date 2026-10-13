#!/usr/bin/env python3
"""One fail-closed report across native Inspector captures and real engine steps.

This is a coverage-aware *diagnostic* gate. It cannot claim whole-game parity:
native source hook order, hidden continuation and the original conditional
chance laws remain independently unverified. Never use native post-state as a
step input. The existing actual-engine comparator owns all stepping.
"""
from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path

from native_action_envelope_batch import collect
from native_trace_triage import triage
from native_prefix_checkpoints import extract_dir


def combine(corpus: dict, strict: dict, prefixes: dict,
            engine: dict | None, engine_error: str | None = None) -> dict:
    captures = corpus.get("files_checked", 0)
    prefixes_count = prefixes.get("recovered_closed_prefix_count", 0)
    stepped = 0 if engine is None else engine.get("actually_stepped", 0)
    mismatched = 0 if engine is None else engine.get("mismatched", 0)
    all_native_valid = (
        captures > 0 and corpus.get("status") == "CALLBACK_CORPUS_CONSISTENT"
        and strict.get("structurally_valid_count", 0) == strict.get("trace_count", 0)
    )
    if engine_error:
        status = "ENGINE_EXECUTION_UNAVAILABLE"
    elif captures == 0 and strict.get("trace_count", 0) == 0:
        status = "NO_CAPTURE_EVIDENCE"
    elif mismatched:
        status = "REAL_ENGINE_CHECKPOINT_MISMATCH"
    elif engine is None or stepped == 0:
        status = "ENGINE_COMPARISON_NOT_COMPLETED"
    elif not all_native_valid or engine.get("unsupported_or_invalid", 0):
        status = "PARTIAL_NATIVE_OR_ENGINE_COVERAGE"
    else:
        status = "SUPPORTED_CHECKPOINTS_AGREE_NOT_FULL_GAME_PARITY"

    return {
        "schema": "unified-native-rng-fidelity-gate-v1",
        "status": status,
        "native_game_parity": "NOT_TESTED",
        "native_source_hook_order_parity": "NOT_TESTED",
        "exact_native_rng_parity": "NOT_TESTED",
        "conditional_chance_law": "NOT_CALIBRATED",
        "random_boundary_capture": "NOT_IN_NATIVE_SIDECAR_YET",
        "verification_boundary": "ISOLATED_SUPPORTED_NATIVE_CHECKPOINTS_ONLY",
        "capture": {
            "sidecar_files": captures,
            "strict_trace_files": strict.get("trace_count", 0),
            "strict_traces_complete": strict.get("structurally_valid_count", 0),
            "closed_decision_prefixes": prefixes_count,
            "observed_action_types": corpus.get("native_action_types", {}),
            "sidecar_status": corpus.get("status"),
            "strict_trace_status": strict.get("status"),
            "mechanics": corpus.get("mechanics", {}),
        },
        "engine": {
            "attempted": engine is not None,
            "error": engine_error,
            "status": None if engine is None else engine.get("status"),
            "fixtures": 0 if engine is None else engine.get("total_fixtures", 0),
            "actual_engine_steps": stepped,
            "consistent": 0 if engine is None else engine.get("consistent", 0),
            "mismatched": mismatched,
            "unsupported_or_invalid": 0 if engine is None else
                engine.get("unsupported_or_invalid", 0),
            "entries": [] if engine is None else engine.get("entries", []),
        },
        "limitations": [
            "A callback/history sidecar is not a native source-hook call log",
            "Only independent successor-closed native decision prefixes are fixtures",
            "The current importer omits hidden state, relic/potion and some power/turn-counter mechanics",
            "Emulator RNG journaling currently covers only migrated typed provider sites",
            "No original conditional outcome-law or full native cursor parity certification",
        ],
    }


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("trace_dir", type=Path)
    p.add_argument("--fixtures-dir", type=Path)
    p.add_argument("--engine-dll", type=Path,
                   help="Optional existing built Sts2Emulator.Cli.dll; never builds or alters a checkout")
    p.add_argument("--region", choices=["Overgrowth", "Underdocks"], default="Overgrowth")
    p.add_argument("--ascension", type=int, default=0)
    p.add_argument("--out", type=Path)
    args = p.parse_args()
    if not args.trace_dir.is_dir():
        p.error("Trace directory not found: " + str(args.trace_dir))
    fixtures = args.fixtures_dir or args.trace_dir / "fixtures"
    corpus = collect(args.trace_dir)
    strict = triage(args.trace_dir)
    prefixes = extract_dir(args.trace_dir, fixtures)
    engine = None
    error = None
    # Do not accidentally count stale expectations from a previous trace
    # directory as belonging to today's independently closed prefixes.
    expected_fixtures = {
        filename for entry in prefixes.get("traces", [])
        for filename in entry.get("fixtures", [])
    }
    all_fixtures = {path.name for path in fixtures.glob("*.json")}
    foreign = sorted(all_fixtures - expected_fixtures)
    missing = sorted(expected_fixtures - all_fixtures)
    if foreign or missing:
        error = ("Fixture corpus does not match closed-prefix manifest: "
                 f"foreign={foreign[:6]}, missing={missing[:6]}")
    if args.engine_dll is not None and error is None:
        if not args.engine_dll.is_file():
            error = "Engine DLL not found: " + str(args.engine_dll)
        else:
            try:
                proc = subprocess.run(
                    ["dotnet", str(args.engine_dll),
                     "native-engine-fidelity-batch", str(fixtures),
                     args.region, str(args.ascension)],
                    capture_output=True, text=True, timeout=180, check=False)
                if proc.returncode not in (0, 1, 2):
                    error = ("Engine process exit " + str(proc.returncode) +
                             ": " + proc.stderr[-500:])
                else:
                    engine = json.loads(proc.stdout)
                    if engine.get("schema") != "native-engine-fidelity-batch-v1":
                        error = "Unexpected engine batch schema"
                        engine = None
            except (OSError, subprocess.TimeoutExpired, json.JSONDecodeError) as ex:
                error = type(ex).__name__ + ": " + str(ex)[:300]
    report = combine(corpus, strict, prefixes, engine, error)
    output = json.dumps(report, indent=2, sort_keys=True) + "\n"
    print(output)
    if args.out is not None:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return 1 if report["status"] == "REAL_ENGINE_CHECKPOINT_MISMATCH" else (
        2 if report["status"] != "SUPPORTED_CHECKPOINTS_AGREE_NOT_FULL_GAME_PARITY"
        else 0)


if __name__ == "__main__":
    raise SystemExit(main())
