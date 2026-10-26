"""Self-tests for the source-independent per-site declaration ledger."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from stochastic_site_acceptance import build_report


def site(site_id, mechanics):
    return {
        "site_id": site_id,
        "owner": "test-owner",
        "reference_law_status": "UNVERIFIED: test model",
        "native_implemented": "Yes: native boundary",
        "controlled_implemented": "Yes: saved continuation",
        "controlled_mechanics": mechanics,
        "native_parity": "No: pending",
    }


class TestStochasticSiteAcceptance(unittest.TestCase):
    def test_focused_test_declarations_do_not_accept_full_site_runtime(self):
        rows = [
            site("shop.offer", "Yes: replayed and tested"),
            site("reward.offer", "No: focused mechanics regression pending"),
        ]
        evidence = [
            {
                "site_id": row["site_id"],
                "evidence_presence": {},
                "missing_facets": [],
                "site_specific_contract_docs": [],
            }
            for row in rows
        ]
        inventory = {
            "registry_matrix_coverage": {
                "registered_site_count": 2,
                "site_ids_without_matrix_rows": [],
            },
            "site_matrix_contract_evidence": {"rows": evidence},
        }

        report = build_report(inventory, rows)

        self.assertEqual(1, report["focused_mechanics_test_declared_count"])
        self.assertEqual(
            1, report["focused_mechanics_test_not_established_count"])
        self.assertEqual(0, report["executably_accepted_site_count"])
        by_id = {row["site_id"]: row for row in report["sites"]}
        self.assertEqual("DECLARED",
                         by_id["shop.offer"]["focused_mechanics_test_status"])
        self.assertFalse(by_id["shop.offer"]["runtime_continuation_accepted"])


if __name__ == "__main__":
    unittest.main()
