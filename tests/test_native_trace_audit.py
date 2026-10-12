"""Checks that a passive trace can be audited without any native game binary."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_trace_audit import audit
from test_native_parity import sample


def passive_fixture():
    records = copy.deepcopy(sample())
    records.pop(9)  # Remove the dangling synthetic third boundary.
    records[0]["capture_mode"] = "passive"
    records[1]["observation_revision"] = 1
    records[1]["event_seq"] = 0
    records[5]["observation_revision"] = 2
    records[5]["event_seq"] = 1
    records[4]["first_event_seq"] = 1
    records[4]["last_event_seq"] = 1
    records[8]["first_event_seq"] = 2
    records[8]["last_event_seq"] = 2
    records[-1]["state"] = {"player": {"hp": 38}}
    return records


class PassiveAuditTests(unittest.TestCase):
    def test_complete_decision_lifecycle_is_structurally_valid(self):
        result = audit(passive_fixture())
        self.assertEqual("STRUCTURALLY_VALID", result["status"])
        self.assertEqual(2, result["decisions"])
        self.assertEqual("NOT_TESTED", result["native_parity"])
        self.assertFalse(result["game_build_pinned"])

    def test_mismatched_successor_hash_is_incomplete(self):
        records = passive_fixture()
        records[4]["after_hash"] = "WRONG"
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_event_cursor_gap_is_incomplete(self):
        records = passive_fixture()
        records[8]["first_event_seq"] = 10
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_rejected_native_action_is_incomplete(self):
        records = passive_fixture()
        records[2]["accepted"] = False
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_unfinished_capture_is_incomplete(self):
        records = passive_fixture()
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records, "Writer overflow")["status"])


if __name__ == "__main__":
    unittest.main()
