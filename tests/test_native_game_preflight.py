"""Local build fingerprint checks use synthetic bytes, never proprietary binaries."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_game_preflight import audit, file_sha256


class PreflightTests(unittest.TestCase):
    def test_missing_game_assemblies_keep_status_explicit(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = audit(Path(tmp))
            self.assertFalse(output["dependencies_present"])
            self.assertEqual("NOT_VALIDATED", output["reference_hook_status"])
            self.assertEqual("NOT_TESTED", output["native_fidelity_status"])

    def test_synthetic_hashes_are_reproducible(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ("sts2.dll", "0Harmony.dll", "GodotSharp.dll"):
                (root / name).write_bytes(("synthetic-" + name).encode())
            output = audit(root)
            self.assertTrue(output["dependencies_present"])
            self.assertEqual(output["managed_assemblies"]["sts2.dll"]["sha256"],
                             file_sha256(root / "sts2.dll"))
            self.assertEqual(64, len(output["managed_assemblies"]["sts2.dll"]["sha256"]))
            self.assertEqual("NOT_TESTED", output["native_fidelity_status"])


if __name__ == "__main__":
    unittest.main()
