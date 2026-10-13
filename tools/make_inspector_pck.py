#!/usr/bin/env python3
"""Build a minimal Godot 4.5 resource-only PCK for STS2's native mod loader.

This is deliberately *not* an export of the game or Godot project. The game's
mod loader discovers .pck files, then reads res://mod_manifest.json and loads
the same-basename .dll next to the pack. Our inspector uses no resource scenes,
so a single unencrypted file suffices.

PCK version 3 layout follows Godot 4.5.1's
core/io/file_access_pack.cpp + editor/export/editor_export_platform.cpp.
Only non-encrypted standalone packs with one manifest are supported.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import tempfile

MAGIC = 0x43504447  # "GDPC" in little endian
PACK_VERSION = 3
GODOT_VERSION = (4, 5, 1)
PACK_REL_FILEBASE = 1 << 1
MANIFEST_NAME = "mod_manifest.json"


def align(value: int, n: int = 16) -> int:
    return ((value + n - 1) // n) * n


def create_pack(manifest: bytes) -> bytes:
    """Pack one res://mod_manifest.json into the Godot PCK v3 format."""
    obj = json.loads(manifest)
    if not isinstance(obj, dict) or not isinstance(obj.get("pck_name"), str):
        raise ValueError("mod_manifest.json needs a string 'pck_name'")
    path = MANIFEST_NAME.encode("utf-8")
    name_pad = (-len(path)) % 4
    padded_name = path + b"\0" * name_pad

    # V3: magic, pack format, major/minor/patch, flags, file base,
    # directory offset, then sixteen reserved uint32 values.
    header_length = 6 * 4 + 2 * 8 + 16 * 4
    file_base = align(header_length)
    directory_base = align(file_base + len(manifest))
    header = struct.pack(
        "<6IQQ",
        MAGIC, PACK_VERSION, *GODOT_VERSION, PACK_REL_FILEBASE,
        file_base, directory_base,
    ) + b"\0" * (16 * 4)
    if len(header) != header_length:
        raise AssertionError("PCK v3 header size changed unexpectedly")

    directory = (
        struct.pack("<I", 1)
        + struct.pack("<I", len(padded_name))
        + padded_name
        + struct.pack("<QQ", 0, len(manifest))
        + hashlib.md5(manifest).digest()
        + struct.pack("<I", 0)
    )
    output = (
        header
        + b"\0" * (file_base - len(header))
        + manifest
        + b"\0" * (directory_base - file_base - len(manifest))
        + directory
    )
    verify_pack(output, manifest)
    return output


def verify_pack(data: bytes, expected_manifest: bytes) -> None:
    """Check offsets, directory, hash, bounds and exact manifest contents."""
    if len(data) < 104:
        raise ValueError("Truncated PCK header")
    magic, version, major, minor, patch, flags, base, directory = struct.unpack_from(
        "<6IQQ", data, 0
    )
    if (magic, version, major, minor, patch, flags) != (
        MAGIC, PACK_VERSION, *GODOT_VERSION, PACK_REL_FILEBASE
    ):
        raise ValueError("Unsupported PCK header")
    if not (104 <= base <= directory <= len(data)):
        raise ValueError("Invalid PCK offsets")
    if struct.unpack_from("<I", data, directory)[0] != 1:
        raise ValueError("Expected exactly one packaged file")
    name_len = struct.unpack_from("<I", data, directory + 4)[0]
    if name_len > 4096 or directory + 8 + name_len + 36 > len(data):
        raise ValueError("Invalid file path")
    name = data[directory + 8: directory + 8 + name_len].rstrip(b"\0")
    if name != MANIFEST_NAME.encode("utf-8"):
        raise ValueError("Missing res://mod_manifest.json")
    offset = directory + 8 + name_len
    file_offset, file_size = struct.unpack_from("<QQ", data, offset)
    md5 = data[offset + 16:offset + 32]
    file_flags = struct.unpack_from("<I", data, offset + 32)[0]
    if file_flags or file_offset + file_size > directory - base:
        raise ValueError("Invalid PCK file range or flags")
    content = data[base + file_offset:base + file_offset + file_size]
    if content != expected_manifest or hashlib.md5(content).digest() != md5:
        raise ValueError("Manifest content/digest mismatch")


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--manifest", type=Path,
                   default=Path("native/Sts2ReferenceMod/mod_manifest.json"))
    p.add_argument("--out", type=Path, required=True)
    args = p.parse_args()
    manifest = args.manifest.read_bytes()
    data = create_pack(manifest)
    name = json.loads(manifest)["pck_name"]
    if args.out.name != name + ".pck":
        p.error("PCK basename must match manifest pck_name: " + name)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    # Write atomically so a failure never leaves a partially valid PCK.
    fd, temp_name = tempfile.mkstemp(prefix=".pck-", suffix=".tmp", dir=args.out.parent)
    try:
        with os.fdopen(fd, "wb") as target:
            target.write(data)
            target.flush()
            os.fsync(target.fileno())
        os.replace(temp_name, args.out)
    finally:
        if os.path.exists(temp_name):
            os.unlink(temp_name)
    print(f"Created {args.out} ({len(data)} bytes; one manifest, Godot 4.5 PCK v3)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
