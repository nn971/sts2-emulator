#!/usr/bin/env python3
"""Produce an auditable import-coverage manifest for a native decision snapshot.

The current mainline emulator has no general native-combat state loader.
Accordingly every import is PARTIAL or UNSUPPORTED until a real continuation
adapter can independently attest to required fields.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_parity import load_trace

REQUIRED_COMBAT_FIELDS = (
    "$.player.hp", "$.player.max_hp", "$.combat.energy",
    "$.combat.hand", "$.combat.draw_pile", "$.combat.discard_pile",
    "$.combat.exhaust_pile", "$.combat.creatures", "$.player.powers",
    "$.combat.turn", "$.combat.max_energy", "$.player.block",
)


def import_manifest(records: list[dict], decision_id: int) -> dict:
    decision = next((r for r in records if r.get("type") == "decision"
                     and r.get("decision_id") == decision_id), None)
    if decision is None:
        return {"status": "UNSUPPORTED", "reason": "Unknown decision ID",
                "decision_id": decision_id, "missing_fields": list(REQUIRED_COMBAT_FIELDS)}
    coverage = decision.get("coverage", {})
    verified = set(coverage.get("verified_fields", []))
    missing = sorted(set(REQUIRED_COMBAT_FIELDS) - verified)
    phase = decision.get("phase")
    unsupported = list(coverage.get("unsupported_mechanics", []))
    if phase != "Combat":
        unsupported.append("NON_COMBAT_IMPORT")
    unsupported.append("EMULATOR_NATIVE_CONTINUATION_STEPPER_UNIMPLEMENTED")
    return {
        "status": "PARTIAL" if phase == "Combat" else "UNSUPPORTED",
        "reason": "Visible native combat state can be structurally imported by the .NET core; deterministic continuation is still unverified",
        "decision_id": decision_id,
        "phase": phase,
        "state_hash": decision.get("state_hash"),
        "verified_fields": sorted(verified),
        "missing_fields": missing,
        "unsupported_mechanics": unsupported,
        "rng_status": coverage.get("rng_status", "RNG_UNAUDITED"),
        "legal_actions": decision.get("legal_actions", []),
        "visible_snapshot_importer": "Sts2Emulator.Core.NativeCombatSnapshotImporter",
        "verified_continuation": False,
        "next_step_ready": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_trace", type=Path)
    parser.add_argument("--decision", type=int, required=True)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    records, error = load_trace(args.native_trace)
    if error:
        result = {"status": "UNSUPPORTED", "reason": error, "next_step_ready": False}
    else:
        result = import_manifest(records, args.decision)
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if result["status"] == "PARTIAL" else 2


if __name__ == "__main__":
    raise SystemExit(main())
