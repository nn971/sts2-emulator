#!/usr/bin/env python3
"""Reject changed pins, inventories and executable coverage claims in CI.

Run --update after reviewing an intentional inventory/content change. The
baseline records claims, never a substitute for native replay evidence.
"""
import argparse
import hashlib
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASELINE = ROOT / "data/coverage/v111-baseline.json"


def checked_report(dotnet):
    inventory_path = ROOT / "data/coverage/v111-inventory.json"
    inventory = json.loads(inventory_path.read_text())
    pin = json.loads((ROOT / "data/reference_builds/v0.111.0-41cef1ea.json").read_text())
    assert inventory["game_version"] == pin["game_version"]
    assert inventory["game_commit"] == pin["commit"]
    assert inventory["build_fingerprint"] == pin["build_fingerprint"]
    blobs = dict(line.split() for line in (ROOT / "tools/reference_source/spire_codex_v0.111.0_git_blobs.tsv").read_text().splitlines()
                 if line and not line.startswith("#"))
    assert inventory["corpus_blobs"] == blobs, "Corpus blob pin changed"
    keys = [(item["kind"], item["id"]) for item in inventory["items"]]
    assert len(keys) == len(set(keys)), "Duplicate inventory entries"
    report = json.loads(subprocess.check_output([
        dotnet, str(ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"),
        "v111-coverage"], text=True))
    assert report["schema"] == "sts2-v111-coverage-v1"
    assert report["game_build"] == f"{pin['game_version']}/{pin['commit']}/{pin['build_fingerprint']}"
    assert {(item["kind"], item["native_id"]) for item in report["items"]} == set(keys)
    for item in report["items"]:
        if item["scope"] == "multiplayer-only":
            assert item["implementation"] == item["integration"] == "excluded"
        if item["validation"] == "native-parity":
            raise AssertionError("Native parity claims require a reviewed fixture ledger; none is installed yet")
    # This first baseline deliberately blocks every final release claim while
    # the real-game release corpus and exhaustive branch inventory are absent.
    assert report["release_ready"] is False
    return {
        "schema": "sts2-v111-coverage-baseline-v1",
        "inventory_file_sha256": hashlib.sha256(inventory_path.read_bytes()).hexdigest(),
        "report": report,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--update", action="store_true")
    args = parser.parse_args()
    result = checked_report(args.dotnet)
    if args.update:
        BASELINE.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
        print("Updated reviewed v111 coverage baseline")
    else:
        assert json.loads(BASELINE.read_text()) == result, (
            "Inventory or executable coverage changed; review the diff and run tools/check_v111_coverage.py --update")
        print("Pinned inventory and executable v111 coverage match the reviewed baseline")


if __name__ == "__main__":
    main()
