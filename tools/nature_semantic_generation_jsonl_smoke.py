#!/usr/bin/env python3
"""Source-ordered generated-card pools through independent semantic workers."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session

DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
COMMAND = ["dotnet", str(DLL), "prototype-ai-jsonl"]
SEED = "nature-semantic-generated-card-identity"


def rollout(worker):
    with Session(COMMAND) as session:
        assert session.capabilities["semanticGeneratedCardChoiceSchemaId"] == (
            "sts2-v111-semantic-generation-choice-v1")
        assert "generated_card_pool" in session.capabilities[
            "semanticExtendedControlledSites"]
        combat = session.begin_seeded_semantic_combat(
            SEED, act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20,
            fixture_generation_potion_id="proto.potion.orobic_acid")
        assert combat.status()["chanceSites"] == [
            "enemy_intent", "draw_card", "generated_card"]
        assert combat.status()["controllerMode"] == "nature-extended-fixture"
        initial = combat.fingerprint
        action = next(action for action in combat.actions()
                      if action["kind"] == "use_potion")
        combat = combat.step(action["actionId"])

        transcript = []
        while combat.boundary == "nature":
            event = combat.pending
            assert event["kind"] == "generated_card"
            assert event["siteId"] == "combat.generated_card.select"
            assert event["streamId"] == "prototype.combat"
            assert event["operationKind"] == "choose_card_model"
            candidates = event["candidates"]
            assert len(candidates) > 3
            if not transcript:
                try:
                    combat.resolve_generated_card("proto.card.not_real")
                except ValueError:
                    pass
                else:
                    raise AssertionError("Out-of-support card model was accepted")
            selected = candidates[worker % len(candidates)]["cardModelId"]
            transcript.append((event["eventId"], selected))
            combat = combat.resolve_generated_card(selected)

        assert len(transcript) == 3
        assert len({event_id for event_id, _ in transcript}) == 3
        assert combat.boundary == "player"
        assert len(combat.observe()["combat"]["hand"]) == 8
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        return initial, tuple(transcript), combat.fingerprint


def main():
    serial = [rollout(worker) for worker in range(15)]
    with ThreadPoolExecutor(max_workers=15) as workers:
        parallel = list(workers.map(rollout, range(15)))
    assert serial == parallel
    assert len({result[1] for result in serial}) > 1
    print("PASS: source-timed Orobic Acid generation pools, ordered card-model "
          "identity, typed resolution, snapshot replay and 1-vs-15 independent "
          "workers; source law UNVERIFIED")


if __name__ == "__main__":
    main()
