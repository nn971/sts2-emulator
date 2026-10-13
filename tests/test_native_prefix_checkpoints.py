"""Native prefix salvage must require an independent *successor decision*."""
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_prefix_checkpoints import closed_prefixes, expectation, extract_dir
from test_native_trace_triage import valid_trace


class NativePrefixTests(unittest.TestCase):
    def test_closed_prefix_survives_later_unsupported_choice(self):
        records = valid_trace()
        records[-1]["status"] = "INCOMPLETE_CAPTURE"
        first = closed_prefixes(records)
        self.assertEqual(1, len(first))
        f = expectation(first[0], records[0], "INCOMPLETE_CAPTURE")
        self.assertEqual("RECORDED_NATIVE_EXPECTATION", f["status"])
        self.assertTrue(f["prefix_only"])
        self.assertEqual("NOT_TESTED", f["emulator_parity"])
        self.assertEqual("strike_silent", f["selected_card"]["model_id"])

    def test_missing_or_corrupted_next_checkpoint_never_produces_prefix(self):
        records = valid_trace()
        del records[4]  # delete independent successor decision
        self.assertEqual([], closed_prefixes(records))
        records = valid_trace()
        records[4]["state_hash"] = "TAMPERED"
        self.assertEqual([], closed_prefixes(records))

    def test_rejected_or_unlisted_action_is_not_recovered(self):
        records = valid_trace()
        records[2]["accepted"] = False
        self.assertEqual([], closed_prefixes(records))
        records = valid_trace()
        records[1]["legal_actions"] = []
        self.assertEqual([], closed_prefixes(records))

    def test_bad_event_cursor_is_not_recovered(self):
        records = valid_trace()
        records[3]["last_event_seq"] = 1
        self.assertEqual([], closed_prefixes(records))

    def test_writes_only_closed_prefix_card_fixtures(self):
        records = valid_trace()
        records[-1]["status"] = "INCOMPLETE_CAPTURE"
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            trace = root / "passive-test.jsonl"
            trace.write_text("\n".join(json.dumps(x) for x in records)+"\n",encoding="utf-8")
            report = extract_dir(root, root / "fixtures")
            self.assertEqual(1, report["recovered_closed_prefix_count"])
            self.assertEqual(1, report["play_card_fixtures_written"])
            fixture = json.loads(next((root / "fixtures").glob("*.json")).read_text())
            self.assertEqual("INCOMPLETE_CAPTURE", fixture["raw_trace_terminal_status"])


if __name__ == "__main__":
    unittest.main()
