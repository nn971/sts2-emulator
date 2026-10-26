#!/usr/bin/env python3
"""Fail-closed inventory consistency check, not a source-fidelity certificate.

Checks that every *registered* stochastic site has an inventory row, a
provenance status and five linked-contract locator fields. It deliberately
does not infer supported executable continuations from a row count.
"""
import argparse
import json
import re
from pathlib import Path


def extract(pattern, text, name):
    match = re.search(pattern, text)
    if match is None:
        raise ValueError("Missing required census field: " + name)
    return [int(s) for s in match.groups()]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--census", type=Path, default=Path("docs/RNG_DIRECT_CALLSITE_CENSUS.md"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    text = args.census.read_text(encoding="utf-8")
    mapped, registered = extract(
        r"Registered stochastic site IDs mapped to migration-matrix rows:\s*\*\*(\d+)/(\d+)\*\*",
        text, "registered site coverage")
    missing, duplicates, unknown = extract(
        r"IDs without rows:\s*\*\*(\d+)\*\*; duplicate matrix IDs:\s*\*\*(\d+)\*\*; unresolved registry references:\s*\*\*(\d+)\*\*",
        text, "mapping integrity")
    unverified, conditional, calibrated, unspecified = extract(
        r"Reference-law column status:\s*\*\*(\d+) UNVERIFIED\*\*;\s*\*\*(\d+) conditional\*\*;\s*\*\*(\d+) calibrated\*\*;\s*\*\*(\d+) unspecified\*\*",
        text, "provenance classification")
    locator, locator_total = extract(
        r"S0 contract-evidence locator:\s*\*\*(\d+)/(\d+) rows\*\*",
        text, "contract evidence")
    report = {
        "registered_sites": registered,
        "mapped_sites": mapped,
        "row_ids_without_mapping": missing,
        "duplicate_matrix_ids": duplicates,
        "unresolved_registry_references": unknown,
        "contract_locator_matched": locator,
        "law_status_counts": {
            "UNVERIFIED": unverified,
            "conditional": conditional,
            "calibrated": calibrated,
            "unspecified": unspecified,
        },
        "source_callsite_completeness": "NOT_ESTABLISHED",
        "full_continuation_acceptance": "NOT_ESTABLISHED",
        "original_game_fidelity": "OUT_OF_SCOPE",
    }
    good = (registered > 0 and mapped == registered
            and not (missing or duplicates or unknown or unspecified)
            and locator == locator_total == registered
            and unverified + conditional + calibrated == registered)
    report["inventory_gate"] = "PASS" if good else "FAIL"
    serialized = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(serialized, encoding="utf-8")
    print(serialized)
    return 0 if good else 1


if __name__ == "__main__":
    raise SystemExit(main())
