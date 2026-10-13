#!/usr/bin/env python3
"""Generate exhaustive *testability* inventory from the executable v111 report.

A native action sidecar observes limited visible combat fields and native RNG
calls. Neither a registered mechanic nor one observed callback certifies a
native/emulator differential for that mechanic. This report marks each item
explicitly and distinguishes unsupported test interfaces from absent evidence.
"""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

from native_action_envelope_batch import collect

COMBAT_VISIBLE = frozenset(("cards", "monsters", "powers", "encounters"))
NATIVE_ENGINE_IMPORTER = frozenset(("cards", "monsters", "powers", "encounters"))
TESTABLE_STATUS = "CONDITIONAL_VISIBLE_CHECKPOINT_IMPORT_ONLY"


def matrix(coverage: dict, corpus: dict | None = None,
           source: str = "executable-v111-coverage") -> dict:
    report = coverage.get("report", coverage)
    if report.get("schema") != "sts2-v111-coverage-v1":
        raise ValueError("Require the full executable v111-coverage report")
    items = report.get("items")
    if not isinstance(items, list) or not items:
        raise ValueError("Coverage items are missing")
    result = []
    by_kind = {}
    for original in items:
        kind = original["kind"]
        implemented = original.get("implementation") == "implemented"
        integrated = original.get("integration") == "integrated"
        eligible = original.get("scope") == "single-player"
        validation = original.get("validation")
        missing = len(original.get("missing_variants", [])) + len(
            original.get("missing_branches", []))
        if not eligible:
            state = "OUT_OF_SINGLE_PLAYER_SCOPE"
        elif not implemented:
            state = "IMPLEMENTATION_INCOMPLETE"
        elif not integrated:
            state = "NOT_INTEGRATED_IN_GAME_PATH"
        elif validation == "native-parity" and missing == 0:
            state = "REPORTED_NATIVE_VALIDATED"
        elif kind in NATIVE_ENGINE_IMPORTER:
            state = TESTABLE_STATUS
        else:
            state = "NATIVE_DIFFERENTIAL_INTERFACE_NOT_ESTABLISHED"
        item = {
            "kind": kind,
            "native_id": original["native_id"],
            "scope": original.get("scope"),
            "implementation": original.get("implementation"),
            "integration": original.get("integration"),
            "registry_ids": original.get("engine_ids", []),
            "native_validation": validation,
            "unverified_variant_count": len(original.get("missing_variants", [])),
            "unverified_branch_count": len(original.get("missing_branches", [])),
            "visible_in_combat_sidecar": kind in COMBAT_VISIBLE,
            "full_native_mechanic_observation": False,
            "testability": state,
            "native_fixture_count": "NOT_ESTABLISHED_PER_ITEM",
            "exact_rng_parity": "NOT_TESTED",
        }
        result.append(item)
        group = by_kind.setdefault(kind, Counter())
        group["eligible"] += bool(eligible)
        group["implemented"] += bool(eligible and implemented)
        group["integrated"] += bool(eligible and integrated)
        group["conditionally_importable"] += state == TESTABLE_STATUS
        group["native_differential_interface_missing"] += (
            state == "NATIVE_DIFFERENTIAL_INTERFACE_NOT_ESTABLISHED")
        group["missing_variants_and_branches"] += missing
        group["native_validated"] += (
            state == "REPORTED_NATIVE_VALIDATED")
    total_implemented_integrated = sum(v["integrated"] for v in by_kind.values())
    return {
        "schema": "native-v111-implemented-mechanics-testability-v1",
        "inventory_source": source,
        "coverage_inventory_hash": report.get("inventory_hash"),
        "coverage_game_build": report.get("game_build"),
        "content_item_count": len(items),
        "implemented_integrated_items": total_implemented_integrated,
        "native_game_validated_items": sum(
            v["native_validated"] for v in by_kind.values()),
        "mechanics_exhaustively_testable_now": False,
        "all_native_rng_streams_match_game_mechanics": "NOT_TESTED",
        "all_implemented_mechanics_have_native_parity": "NOT_TESTED",
        "source_hook_order_parity": "NOT_TESTED",
        "conditional_chance_law": "NOT_CALIBRATED",
        "sidecar_corpus_status": (
            "NOT_SUPPLIED" if corpus is None else corpus.get("status")),
        "native_rng_checkpoint_count": (
            0 if corpus is None else corpus.get("native_rng_checkpoint_count", 0)),
        "native_rng_checkpoint_unobserved_advances": (
            0 if corpus is None else corpus.get(
                "native_rng_checkpoint_unobserved_advances", 0)),
        "native_rng_source_calls": (
            0 if corpus is None else corpus.get("native_rng_source_calls", 0)),
        "by_kind": {k: dict(v) for k, v in sorted(by_kind.items())},
        "items": result,
        "acceptance": [
            "Game-local DLL builds and native source hooks install on the exact pinned build",
            "Observed 12+3 persistent streams have valid immutable cursor checkpoints",
            "All native source RNG operations used by the test are observed and ordered",
            "Context/map/event/reward/shop RNG ownership and original candidate laws are audited",
            "Each implemented integrated mechanic has an independent native pre-state and post-state differential, including variants and branches",
            "Missing importer fields, hidden mechanics and unsupported prompts are counted, never silently passed",
            "Native/emulator action and RNG first-divergence reports are checked across independent seeded runs",
        ],
    }


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("coverage_json", type=Path,
                   help="Output from dotnet run ... -- v111-coverage; not --missing")
    p.add_argument("--trace-dir", type=Path)
    p.add_argument("--out", type=Path)
    args = p.parse_args()
    coverage = json.loads(args.coverage_json.read_text(encoding="utf-8"))
    corpus = collect(args.trace_dir) if args.trace_dir else None
    result = matrix(coverage, corpus, args.coverage_json.name)
    output = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(output)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return 2  # An inventory report is a to-do list, not a parity success.


if __name__ == "__main__":
    raise SystemExit(main())
