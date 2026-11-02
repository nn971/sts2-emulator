"""Private, read-only native feedback inspection contracts."""
from __future__ import annotations

import json
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_live_fidelity_status import read_status


PRIVATE = "native-private-fidelity-status-token-971"


class StatusHandler(BaseHTTPRequestHandler):
    requests = []

    def log_message(self, *args):
        pass

    def do_GET(self):
        self.requests.append((self.path, self.headers.get("Authorization")))
        if self.path != "/v1/fidelity/status":
            self.send_response(404)
            self.end_headers()
            return
        if self.headers.get("Authorization") != "Bearer " + PRIVATE:
            code, result = 401, {"error": "UNAUTHORIZED"}
        else:
            code, result = 200, {
                "schema": "sts2-native-fidelity-status-v1",
                "journal_failure": None,
                "last_source_sequence": 910,
                "latest_result": {
                    "status": "SOURCE_TERMINAL_OBSERVED_UNCERTIFIED",
                    "player_checkpoints_matched": 6,
                    "emulator_actions_stepped": 22},
                "native_settlement_certified": False,
                "native_live_lockstep": False}
        data = json.dumps(result).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


class FidelityStatusTests(unittest.TestCase):
    def setUp(self):
        StatusHandler.requests = []
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), StatusHandler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.url = "http://127.0.0.1:" + str(self.server.server_port)

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)

    def test_private_receipt_read_only_and_token_absent_from_report(self):
        value = read_status(self.url, PRIVATE)
        self.assertEqual("NATIVE_FIDELITY_FEEDBACK_OBSERVED", value["status"])
        self.assertEqual(6, value["latest_result"]["player_checkpoints_matched"])
        self.assertEqual(910, value["last_source_sequence"])
        self.assertFalse(value["native_gameplay_mutated"])
        self.assertFalse(value["native_settlement_certified"])
        self.assertEqual([("/v1/fidelity/status", "Bearer " + PRIVATE)],
                         StatusHandler.requests)
        self.assertNotIn(PRIVATE, json.dumps(value))

    def test_wrong_private_token_and_missing_public_rejected(self):
        wrong = "private-invalid-credential-971"
        report = read_status(self.url, wrong)
        self.assertEqual("BLOCKED_FIDELITY_STATUS_PROBE", report["status"])
        self.assertEqual(401, report["http_status"])
        self.assertNotIn(wrong, json.dumps(report))
        self.assertEqual("BLOCKED_FIDELITY_STATUS_PROBE",
                         read_status(self.url, None)["status"])

    def test_nonloopback_refused_before_any_request(self):
        report = read_status("http://192.168.1.2:39221", PRIVATE)
        self.assertEqual("BLOCKED_FIDELITY_STATUS_PROBE", report["status"])
        self.assertEqual([], StatusHandler.requests)


if __name__ == "__main__":
    unittest.main()
