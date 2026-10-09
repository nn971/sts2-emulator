#!/usr/bin/env python3
"""Offline regression checks for one-run native capture audits."""

import importlib.util
import json
from pathlib import Path
from tempfile import TemporaryDirectory

script_path = Path(__file__).with_name("native_one_run_audit.py")
spec = importlib.util.spec_from_file_location("native_one_run_audit", script_path)
assert spec is not None and spec.loader is not None
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

session = {
    "type": "session",
    "schema": mod.EXPECTED_SCHEMA,
    "corpus_mode": mod.EXPECTED_MODE,
    "build_fingerprint": "a" * 64,
    "build": {"version": "v0.111.0"},
    "expected_game_version": "v0.111.0",
    "expected_game_commit": "41cef1ea",
}
run_attached = {
    "type": "diagnostic",
    "code": "run_recorder_attached",
}
combat_attached = {
    "type": "diagnostic",
    "code": "recorder_attached",
}
assert mod.assess([session, run_attached, combat_attached],
                  preflight=True)["ok"]
assert not mod.assess([session, combat_attached],
                      preflight=True)["ok"]
assert not mod.assess(
    [session | {"corpus_mode": "overgrowth-silent-act1"},
     run_attached, combat_attached], preflight=True)["ok"]
assert not mod.assess(
    [session, run_attached, combat_attached,
     {"type": "diagnostic", "code": "capture_failed"}],
    preflight=True)["ok"]

boundary = {
    "type": "boundary",
    "boundary": "run_manager.RoomEntered",
    "run_observation": {
        "available": True,
        "run_rng": {"native": 1},
        "map": {"points": [{"coord": [0, 1]}]},
        "current_map_point_history": {"rooms": []},
        "players": [{"deck": {"items": [{"type": "Strike"}]}}],
        "events": [{
            "options": [{"text_key": "TEST.options.OBSERVE"}]
        }],
    },
}
report = mod.assess(
    [session, run_attached, combat_attached,
     boundary, {"type": "boundary",
                "boundary": "combat_history.Changed"}]
)
assert report["ok"], report
assert report["map_snapshots"] == 1
assert report["snapshots_with_persistent_deck"] == 1
assert report["event_option_pages_observed"] == 1
assert report["snapshots_with_run_rng"] == 1
assert report["snapshots_with_map_history"] == 1
assert report["boundaries"]["combat_history.Changed"] == 1

with TemporaryDirectory() as temp:
    file_path = Path(temp) / "probe.jsonl"
    file_path.write_text(
        "\n".join(json.dumps(row) for row in
                  [session, run_attached, combat_attached, boundary]) + "\n",
        encoding="utf-8",
    )
    rows = mod.read_jsonl(file_path)
    assert len(rows) == 4
    with file_path.open("a", encoding="utf-8") as stream:
        stream.write("{bad-json}\n")
    try:
        mod.read_jsonl(file_path)
    except ValueError as exc:
        assert "Line 5" in str(exc), exc
    else:
        raise AssertionError("Malformed JSONL not detected")

print("native one-run audit smoke: passed")
