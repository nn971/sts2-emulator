"""Real loopback HTTP contracts for the safe pre-native-run bridge probe."""
from __future__ import annotations

import json
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_live_bridge_preflight import inspect_bridge


PUBLIC = "public-live-bridge-probe-token-001"
FIDELITY = "private-live-bridge-probe-token-002"


class FakeInspector(BaseHTTPRequestHandler):
    requests = []
    redirect = False

    def log_message(self, *args):
        pass

    def do_GET(self):
        self.requests.append((self.path, self.headers.get("Authorization")))
        if self.redirect and self.path == "/v1/agent/status":
            self.send_response(302)
            self.send_header("Location", "http://example.invalid/")
            self.end_headers()
            return
        if self.path == "/v1/agent/status":
            if self.headers.get("Authorization") == "Bearer " + PUBLIC:
                code, value = 200, {"readiness": "NO_COMBAT"}
            else:
                code, value = 401, {"error": "UNAUTHORIZED"}
        elif self.path == "/v1/fidelity/stream?after_seq=-1":
            if self.headers.get("Authorization") != "Bearer " + FIDELITY:
                code, value = 401, {"error": "UNAUTHORIZED"}
            else:
                code, value = 400, {"error": "INVALID_EVENT_CURSOR"}
        else:
            code, value = 404, {"error": "UNKNOWN_ENDPOINT"}
        body = json.dumps(value).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


class BridgePreflightTests(unittest.TestCase):
    def setUp(self):
        FakeInspector.requests = []
        FakeInspector.redirect = False
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), FakeInspector)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.base = "http://127.0.0.1:" + str(self.server.server_port)

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)

    def test_real_http_authentication_preflight_sends_four_gets_only(self):
        report = inspect_bridge(self.base, PUBLIC, FIDELITY)
        self.assertEqual("LIVE_BRIDGE_AUTHENTICATION_READY", report["status"])
        self.assertEqual(4, report["verified_requests"])
        self.assertFalse(report["native_gameplay_mutated"])
        self.assertFalse(report["native_game_launched_by_probe"])
        self.assertEqual(4, len(FakeInspector.requests))
        self.assertNotIn(PUBLIC, json.dumps(report))
        self.assertNotIn(FIDELITY, json.dumps(report))

    def test_bad_private_token_is_rejected_without_leaking(self):
        invalid = "wrong-private-token-of-adequate-length"
        report = inspect_bridge(self.base, PUBLIC, invalid)
        self.assertEqual("BLOCKED_BRIDGE_PREFLIGHT", report["status"])
        self.assertEqual("fidelity_endpoint", report["failed_probe"])
        self.assertEqual(401, report["observed_http_status"])
        self.assertNotIn(invalid, json.dumps(report))

    def test_nonloopback_rejected_before_network_and_missing_tokens(self):
        self.assertEqual("BLOCKED_BRIDGE_PREFLIGHT", inspect_bridge(
            "http://192.168.1.9:39221", PUBLIC, FIDELITY)["status"])
        self.assertEqual("BLOCKED_BRIDGE_PREFLIGHT", inspect_bridge(
            self.base, None, FIDELITY)["status"])
        self.assertEqual("BLOCKED_BRIDGE_PREFLIGHT", inspect_bridge(
            self.base, PUBLIC, PUBLIC)["status"])
        self.assertEqual([], FakeInspector.requests)

    def test_redirect_refused_and_not_followed(self):
        FakeInspector.redirect = True
        result = inspect_bridge(self.base, PUBLIC, FIDELITY)
        self.assertEqual("BLOCKED_BRIDGE_PREFLIGHT", result["status"])
        self.assertEqual("public_endpoint", result["failed_probe"])
        self.assertEqual(302, result["observed_http_status"])
        self.assertEqual(1, len(FakeInspector.requests))


if __name__ == "__main__":
    unittest.main()
