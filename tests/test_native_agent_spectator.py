"""The local native agent reader has no command or non-loopback mode."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))

from native_agent_spectator import SCHEMA, base_url, fetch


class NativeAgentSpectatorTests(unittest.TestCase):
    def test_loopback_only_and_fixed_read_endpoints(self):
        self.assertEqual("http://127.0.0.1:39221",
                         base_url("127.0.0.1", 39221))
        self.assertEqual("http://[::1]:39221", base_url("::1", 39221))
        for host in ("0.0.0.0", "localhost.evil.com", "192.168.1.2", ""):
            with self.assertRaises(ValueError):
                base_url(host, 39221)
        for port in (1, -1, 65536):
            with self.assertRaises(ValueError):
                base_url("localhost", port)
        with self.assertRaises(ValueError):
            fetch("http://127.0.0.1:39221", "long-enough-test-token-to-use",
                  "/v1/actions")
        with self.assertRaises(ValueError):
            fetch("http://127.0.0.1:39221", "short", "/v1/agent/status")


if __name__ == "__main__":
    unittest.main()
