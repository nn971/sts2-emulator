#!/usr/bin/env python3
"""Pin a local installed STS2 build without importing/distributing game code.

The output is provenance for manual native smoke tests, not evidence that
Harmony hooks or semantic parity have been validated.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
from pathlib import Path


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as src:
        for block in iter(lambda: src.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def audit(data_dir: Path) -> dict:
    data_dir = data_dir.resolve()
    required = ("sts2.dll", "0Harmony.dll", "GodotSharp.dll")
    required_files = {
        name: {"present": (data_dir / name).is_file(),
               "sha256": file_sha256(data_dir / name)
               if (data_dir / name).is_file() else None}
        for name in required
    }
    release_json = data_dir.parent / "release_info.json"
    runtime_json = data_dir / "sts2.runtimeconfig.json"
    output = {
        "audit_schema": "1",
        "game_data_directory": str(data_dir),
        "platform": platform.platform(),
        "managed_assemblies": required_files,
        "game_release": {
            "file": str(release_json),
            "present": release_json.is_file(),
            "sha256": file_sha256(release_json) if release_json.is_file() else None,
        },
        "runtime": {
            "file": str(runtime_json),
            "present": runtime_json.is_file(),
            "sha256": file_sha256(runtime_json) if runtime_json.is_file() else None,
        },
        "reference_hook_status": "NOT_VALIDATED",
        "passive_no_perturbation_status": "NOT_TESTED",
        "native_fidelity_status": "NOT_TESTED",
    }
    output["dependencies_present"] = all(v["present"] for v in required_files.values())
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data_dir", type=Path, help="Path containing the original sts2.dll")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    result = audit(args.data_dir)
    output = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    print(output)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output, encoding="utf-8")
    return 0 if result["dependencies_present"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
