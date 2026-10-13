#!/usr/bin/env python3
"""Offline validation of actual pinned native monster initial HP source rolls.

The source Creature.SetUniqueMonsterHpValue computes ordered unused HP values
from a HashSet, selects one via run.niche and then attaches the creature to
combat. These pre-frame observations prove one primitive choice and pool
consistency; stable creature identities and game/emulator matching need a
later combat-creation correlation.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_rng_boundary_verify import verify_call


def validate(data: dict) -> str | None:
    if (data.get("source_version") != "v0.111.0"
        or data.get("source_callsite_observed") is not True
        or data.get("native_creature_combat_id_status")
        != "PRE_ATTACH_CREATURE_ID_UNVERIFIED"
        or type(data.get("creation_ordinal")) is not int
        or data["creation_ordinal"] <= 0
        or not isinstance(data.get("monster_model_id"), str)
        or not data["monster_model_id"]):
        return "UNVERIFIED_INITIAL_HP_SOURCE"
    eligible = data.get("ordered_eligible_hp")
    chosen = data.get("selected_index")
    hp = data.get("selected_hp")
    if (not isinstance(eligible, list) or not eligible
        or any(type(x) is not int for x in eligible)
        or len(eligible) != len(set(eligible))
        or type(chosen) is not int
        or not 0 <= chosen < len(eligible)
        or type(hp) is not int or eligible[chosen] != hp):
        return "INVALID_INITIAL_HP_ELIGIBILITY"
    error = verify_call({
        "source_version": "v0.111.0",
        "origin": "source_primitive_hook",
        "operation_kind": "bounded_integer",
        "source_hook_observed": True,
        "bounded_count": len(eligible),
        "selected_index": chosen,
        "cursor_before": data.get("cursor_before"),
        "cursor_after": data.get("cursor_after")
    })
    return ("INITIAL_HP_" + error) if error else None


def audit(records: list[dict]) -> dict:
    count = 0
    errors = []
    ordinals: set[int] = set()
    for line, row in enumerate(records, 1):
        if row.get("type") != "native_initial_hp_roll":
            continue
        count += 1
        data = row.get("data") or {}
        error = validate(data)
        ordinal = data.get("creation_ordinal")
        if ordinal in ordinals:
            error = "DUPLICATE_CREATION_ORDINAL"
        ordinals.add(ordinal)
        if error:
            errors.append({"line": line, "reason": error})
    return {
        "schema": "native-initial-hp-verification-v1",
        "status": ("NO_NATIVE_INITIAL_HP_SOURCE_EVENTS" if not count
                   else "NATIVE_INITIAL_HP_SOURCE_MISMATCH" if errors
                   else "OBSERVED_NATIVE_INITIAL_HP_ROLLS_VALID"),
        "source_rolls": count,
        "validated_rolls": count - len(errors),
        "errors": errors[:20],
        "native_to_emulator_creature_identity": "NOT_TESTED",
        "complete_combat_creation_parity": "NOT_TESTED",
    }


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("sidecar", type=Path)
    p.add_argument("--out", type=Path)
    args = p.parse_args()
    records = [json.loads(line) for line in
               args.sidecar.read_text(encoding="utf-8").splitlines()
               if line.strip()]
    result = audit(records)
    output = json.dumps(result, indent=2, sort_keys=True) + "\n"
    print(output)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return (0 if result["status"] == "OBSERVED_NATIVE_INITIAL_HP_ROLLS_VALID"
            else 1 if result["errors"] else 2)


if __name__ == "__main__":
    raise SystemExit(main())
