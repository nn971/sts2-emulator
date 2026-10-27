#!/usr/bin/env python3
"""Random-target semantic event and replay smoke through independent JSONL workers."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session

DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
COMMAND = ["dotnet", str(DLL), "prototype-ai-jsonl"]
SEED = "nature-semantic-random-target-identity"


def rollout(worker):
    with Session(COMMAND) as session:
        assert session.capabilities["semanticTargetChoiceSchemaId"] == (
            "sts2-v111-semantic-target-choice-v1")
        extended_sites = session.capabilities[
            "semanticExtendedControlledSites"]
        assert isinstance(extended_sites, list)
        assert len(extended_sites) == len(set(extended_sites))
        assert {"random_enemy_target", "generated_card_pool"} <= set(
            extended_sites)
        combat = session.begin_seeded_semantic_combat(
            SEED, act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20,
            fixture_random_target_card_id="proto.silent.ricochet")
        assert combat.boundary == "player"
        assert combat.status()["chanceSites"] == [
            "enemy_intent", "draw_card", "random_target"]
        assert combat.status()["controllerMode"] == (
            "nature-extended-fixture")
        observation = combat.observe()
        assert any(card["card_id"] == "proto.silent.ricochet"
                   for card in observation["combat"]["hand"])
        initial = combat.fingerprint
        actions = combat.actions()
        action = next(action for action in actions
                      if action["kind"] == "play_card" and
                      json.loads(action["payloadJson"])["CardInstanceId"] ==
                      observation["combat"]["hand"][0]["instance_id"])
        combat = combat.step(action["actionId"])

        transcript = []
        while combat.boundary == "nature":
            event = combat.pending
            assert event["kind"] == "random_target"
            assert event["siteId"] == "combat.effect.random_enemy"
            assert event["streamId"] == "run.combat_targets"
            assert event["operationKind"] == "choose_enemy_instance"
            candidates = event["candidates"]
            assert len(candidates) > 1
            if not transcript:
                try:
                    combat.resolve_random_target(2147483647)
                except ValueError:
                    pass
                else:
                    raise AssertionError("Out-of-support enemy ID was accepted")
            selected = candidates[worker % len(candidates)]["enemyInstanceId"]
            transcript.append((event["eventId"], selected))
            combat = combat.resolve_random_target(selected)

        assert len(transcript) == 4
        assert len({event_id for event_id, _ in transcript}) == 4
        assert combat.boundary == "player"
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        return initial, tuple(transcript), combat.fingerprint


def main():
    serial = [rollout(worker) for worker in range(15)]
    parallel = [None] * 15
    with ThreadPoolExecutor(max_workers=15) as workers:
        futures = [workers.submit(rollout, worker) for worker in range(15)]
        parallel = [future.result() for future in futures]
    assert serial == parallel
    assert len({result[1] for result in serial}) > 1
    print("PASS: source-timed Ricochet random-target events, stable enemy IDs, "
          "typed resolution, snapshot replay and 1-vs-15 independent workers; "
          "source law UNVERIFIED")


if __name__ == "__main__":
    main()
