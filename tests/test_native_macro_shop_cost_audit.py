"""Check source Cost getter receipts remain attached to native stock slots."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_macro_envelope_audit import audit
from test_native_macro_envelope_audit import sample


class ShopCostEvidenceTests(unittest.TestCase):
    def test_source_cost_receipt_is_qualified_not_displayed_price(self):
        receipt = {"type": "shop_source_cost", "run_id": "native-run-a",
                   "data": {"slot": 0, "native_cost": 71,
                            "source": "MerchantEntry.get_Cost.postfix",
                            "observation_basis": "EXACT_NATIVE_GETTER_RETURN_VALUE",
                            "displayed_price_coverage": "UNVERIFIED_UI_USAGE",
                            "source_price_hook_evaluated_by_inspector": False,
                            "emulator_parity": "NOT_TESTED"}}
        rows = sample()
        rows.insert(7, receipt)
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        self.assertEqual("SOURCE_MACRO_TIMELINE_CONSISTENT",
                         audit(rows)["status"])
        for field, changed in (
                ("slot", 99), ("native_cost", -1),
                ("displayed_price_coverage", "VERIFIED_UI"),
                ("source_price_hook_evaluated_by_inspector", True)):
            altered = copy.deepcopy(rows)
            altered[7]["data"][field] = changed
            self.assertEqual("INCOMPLETE_CAPTURE", audit(altered)["status"],
                             field)


if __name__ == "__main__":
    unittest.main()
