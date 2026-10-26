#!/usr/bin/env python3
"""Isolated playable Nature fixture smoke: controlled HP→intent→shuffle→player."""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
from jsonl_privacy_assertions import assert_public_stochastic_view
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session, EmulatorError

SEED = "nature-143-playable-jsonl"


def main():
    with Session(["dotnet", str(DLL), "prototype-ai-jsonl"]) as session:
        capabilities = session.capabilities
        assert capabilities["natureBoundaryScope"] == "isolated-combat-entry-only"
        assert capabilities["naturePlayable"] is False
        assert capabilities["controlledChanceSchemaId"] == (
            "sts2-v111-controlled-playable-fixture-v1")
        hp = session.begin_nature(
            SEED, kind="initial_hp", act=2,
            encounter_id="proto.native.hive.encounter.mytes_normal",
            native_entry="MytesNormal", total_floor=21)
        while not hp.complete:
            hp = hp.resolve(0)
        intents = hp.advance()
        while not intents.complete:
            intents = intents.resolve(0)
        entry = session.begin_controlled_combat(intents, seed=SEED)
        assert entry.trace()["pending"]["siteId"] == (
            "controlled.fixture.opening_shuffle.swap_index")
        try:
            session.call("controlled_observe",
                         controlled_handle=entry.handle,
                         policy_id="prototype-fair-v0")
        except EmulatorError as error:
            assert "shuffle is pending" in str(error)
        else:
            raise AssertionError("Pending shuffle exposed a player observation")
        fork = entry.fork()
        saved = entry.save_snapshot()
        restored = session.load_controlled_snapshot(saved)
        assert restored.fingerprint == entry.fingerprint
        assert fork.trace()["pending"]["eventId"] == (
            entry.trace()["pending"]["eventId"])
        while entry.trace()["pending"] is not None:
            before = entry.trace()
            entry = entry.resolve(0)
            after = entry.trace()
            assert after["transcript"][-1]["eventId"] == (
                before["pending"]["eventId"])
        assert entry.trace()["kind"] == "player"
        player = entry.observe()
        # Fair observation uses canonical snake_case JSON and numeric RunPhase.
        assert player["phase"] == 3  # RunPhase.Combat
        assert player["combat"] is not None
        assert player["combat"]["hand"]
        actions = entry.actions()
        assert actions and any(a["kind"] == "end_turn" for a in actions)
        assert_public_stochastic_view(player)
        assert entry.fingerprint == session.load_controlled_snapshot(
            entry.save_snapshot()).fingerprint
        assert session.release_controlled_many([fork, restored, entry]) == 3
        try:
            fork.trace()
        except EmulatorError:
            pass
        else:
            raise AssertionError("Released controlled handle was still accessible")
    print("Nature playable entry: HP/intents, shuffle, fair observation, replay/fork passed")


if __name__ == "__main__":
    main()
