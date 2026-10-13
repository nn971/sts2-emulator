import json
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from make_inspector_pck import create_pack, verify_pack

MANIFEST = json.dumps({
    "pck_name": "Sts2ReferenceMod",
    "name": "Sts2 Reference Inspector",
    "author": "test",
    "version": "0.1.0",
}, separators=(",", ":")).encode()


class MinimalPckTests(unittest.TestCase):
    def test_roundtrip_single_root_manifest(self):
        pack = create_pack(MANIFEST)
        verify_pack(pack, MANIFEST)
        self.assertEqual(b"GDPC", pack[:4])
        self.assertEqual(3, struct.unpack_from("<I", pack, 4)[0])
        self.assertEqual((4, 5, 1), struct.unpack_from("<III", pack, 8))
        self.assertEqual(2, struct.unpack_from("<I", pack, 20)[0])

        base, directory = struct.unpack_from("<QQ", pack, 24)
        self.assertEqual(MANIFEST, pack[base: base + len(MANIFEST)])
        self.assertEqual(1, struct.unpack_from("<I", pack, directory)[0])

    def test_tampered_manifest_fails(self):
        pack = bytearray(create_pack(MANIFEST))
        base = struct.unpack_from("<Q", pack, 24)[0]
        pack[base + 8] ^= 1
        with self.assertRaisesRegex(ValueError, "content/digest"):
            verify_pack(bytes(pack), MANIFEST)

    def test_bad_manifest_rejected(self):
        with self.assertRaises(ValueError):
            create_pack(b'{"name":"wrong"}')

    def test_real_manifest_builds_and_is_named_consistently(self):
        root = Path(__file__).resolve().parents[1]
        contents = (root / "native" / "Sts2ReferenceMod" / "mod_manifest.json").read_bytes()
        self.assertEqual("Sts2ReferenceMod", json.loads(contents)["pck_name"])
        verify_pack(create_pack(contents), contents)


if __name__ == "__main__":
    unittest.main()
