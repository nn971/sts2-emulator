#!/usr/bin/env python3
"""Independent P0 seeded semantic entry: no external HP/shuffle Nature tickets.

This tests transport, reproducibility and one real action->Nature->player
transition. Full source-native entry parity and terminal-training coverage
remain separate issue #143 gates.
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session

COMMAND = ["dotnet", str(DLL), "prototype-ai-jsonl"]
SEED = "semantic-seeded-143-independent-workers"


def rollout():
    # Every invocation owns an independent external dotnet JSONL process.
    with Session(COMMAND) as session:
        assert session.capabilities["semanticControlledSites"] == [
            "enemy_intent", "draw_card"]
        assert session.capabilities["semanticSeededOpeningProvider"] == (
            "engine-owned-seeded-source-shaped-unverified-v1")
        combat = session.begin_seeded_semantic_combat(
            SEED, act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20,
            map_column=3, map_row=5)
        assert combat.boundary == "player"
        assert combat.status()["sourceOpeningProvider"] == (
            "engine-owned-seeded-source-shaped-unverified-v1")
        assert combat.pending is None
        public = combat.observe()
        assert "semanticDeck" not in json.dumps(public)
        assert "rng" not in json.dumps(public).lower()
        initial = combat.fingerprint
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == initial
        assert combat.fork().fingerprint == initial

        end = next(a for a in combat.actions() if a["kind"] == "end_turn")
        combat = combat.step(end["actionId"])
        count = 0
        transcript = []
        while combat.boundary == "nature":
            assert count < 120, "Timed out resolving a single real end turn"
            event = combat.pending
            assert event["kind"] in ("enemy_intent", "draw_card")
            assert event["probabilityStatus"] == "UNVERIFIED"
            transcript.append((event["kind"], event["supportFingerprint"]))
            combat = combat.resolve(0)
            count += 1
        assert any(kind == "enemy_intent" for kind, _ in transcript)
        assert combat.boundary in ("player", "terminal")
        assert combat.status()["chanceSites"] == ["enemy_intent", "draw_card"]
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        result = initial, tuple(transcript), combat.fingerprint, combat.boundary
        return result


def main():
    baseline = rollout()
    with ThreadPoolExecutor(max_workers=15) as pool:
        independent = list(pool.map(lambda _: rollout(), range(15)))
    assert all(value == baseline for value in independent), (
        "Seeded Nature session differs across independent JSONL processes")
    print("PASS: seeded Nature opening + action/chance resume; "
          "1 vs 15 independent process transcript equality; replay; "
          "external Nature sites only intent/draw; source law UNVERIFIED")


if __name__ == "__main__":
    main()
