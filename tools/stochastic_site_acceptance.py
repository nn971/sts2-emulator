#!/usr/bin/env python3
"""Produce an auditable *declaration* ledger for every registered RNG site.

Presence of a SiteId or a law is NOT evidence that the operation has executable
Nature coverage or validated mechanics. This ledger intentionally records
these as separate statuses and never upgrades UNVERIFIED probabilities.
"""
import argparse
from collections import Counter
import json
from pathlib import Path

import rng_callsite_inventory as census


def build_report(inventory: dict, rows: list[dict]) -> dict:
    coverage = inventory["registry_matrix_coverage"]
    evidence = {
        item["site_id"]: item
        for item in inventory["site_matrix_contract_evidence"]["rows"]
    }
    sites = []
    for row in sorted(rows, key=lambda x: x["site_id"]):
        contract = evidence.get(row["site_id"])
        if contract is None:
            raise ValueError(f"Missing registered site evidence: {row['site_id']}")
        law_status = census._law_status_kind(row["reference_law_status"])
        controlled = row["controlled_implemented"]
        mechanics = row["controlled_mechanics"]
        explicitly_unsupported = controlled.strip().lower().startswith("no")
        focused_mechanics_test_declared = not mechanics.strip().lower().startswith(
            "no:")
        sites.append({
            "site_id": row["site_id"],
            "engine_owner": row["owner"],
            "law_provenance": law_status,
            "law_explanation": row["reference_law_status"],
            "native_provider_implementation": row["native_implemented"],
            "nature_or_controlled_implementation": controlled,
            "controlled_mechanics_declaration": mechanics,
            "focused_mechanics_test_status": (
                "DECLARED" if focused_mechanics_test_declared
                else "NOT_ESTABLISHED"
            ),
            "native_parity_declaration": row["native_parity"],
            "contract_facets_located": contract["evidence_presence"],
            "missing_contract_facets": contract["missing_facets"],
            "linked_documents": contract["site_specific_contract_docs"],
            "deliberately_not_accepted": (
                "unsupported site, requires explicit fail-closed runtime test"
                if explicitly_unsupported else
                "full mechanics-entry, legal-support, provider, law, "
                "save/restore, and fail-closed acceptance is not established"
            ),
            "runtime_continuation_accepted": False,
            "source_law_calibration_accepted": law_status == "calibrated",
            "note": (
                "A calibrated mathematical transform does not prove eligibility "
                "of the actual source pool or original-game RNG parity."
            ),
        })
    ids = [site["site_id"] for site in sites]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate site IDs")
    missing = coverage["site_ids_without_matrix_rows"]
    return {
        "schema": "sts2-source-independent-site-acceptance-declarations-v1",
        "registered_site_count": coverage["registered_site_count"],
        "ledger_site_count": len(sites),
        "registry_to_matrix_complete": not missing,
        "contract_facets_located": sum(
            not s["missing_contract_facets"] for s in sites
        ),
        "law_provenance_counts": dict(sorted(Counter(
            s["law_provenance"] for s in sites).items())),
        "unsupported_site_count": sum(s["nature_or_controlled_implementation"]
                                       .strip().lower().startswith("no")
                                       for s in sites),
        "focused_mechanics_test_declared_count": sum(
            s["focused_mechanics_test_status"] == "DECLARED" for s in sites
        ),
        "focused_mechanics_test_not_established_count": sum(
            s["focused_mechanics_test_status"] == "NOT_ESTABLISHED"
            for s in sites
        ),
        "executably_accepted_site_count": 0,
        "acceptance_status": "DECLARATIONS_ONLY__RUNTIME_ACCEPTANCE_OPEN",
        "important_limit": (
            "Static source/matrix coverage is not executable per-site proof. "
            "Runtime legal-support, continuations, save/replay and fail-closed "
            "tests must be linked by site before any accepted count is raised."
        ),
        "sites": sites,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--inventory", required=True, type=Path)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--strict", action="store_true",
                   help="Fail on registry/matrix or missing contract-declaration facets")
    a = p.parse_args()
    inventory = json.loads(a.inventory.read_text(encoding="utf-8"))
    report = build_report(inventory, census._migration_matrix_site_rows())
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(report, sort_keys=True, indent=2) + "\n",
                        encoding="utf-8")
    print("RNG site declarations:", report["ledger_site_count"])
    print("Law provenance:", report["law_provenance_counts"])
    print("Contract facets located:", report["contract_facets_located"])
    print("Executable acceptance:", report["acceptance_status"])
    if a.strict and (
        not report["registry_to_matrix_complete"]
        or report["registered_site_count"] != report["ledger_site_count"]
        or report["contract_facets_located"] != report["ledger_site_count"]
    ):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
