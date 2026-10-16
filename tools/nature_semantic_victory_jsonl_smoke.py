#!/usr/bin/env python3
"""Act 1 source-pinned weak combat victory with fair player actions.

Protocol/semantic mechanics acceptance, not a model training benchmark.
All processes have separate emulator handles, RNG copies and observations.
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session

DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
COMMAND = ["dotnet", str(DLL), "prototype-ai-jsonl"]
SEED = "nature-p0-weak-real-victory"


def play_victory():
    with Session(COMMAND) as session:
        combat = session.begin_seeded_semantic_combat(
            SEED, act=1,
            encounter_id="proto.encounter.shrinker_beetle_weak",
            native_entry="ShrinkerBeetleWeak", total_floor=2,
            map_column=3, map_row=1,
            replay_opening_draw_hooks=True)
        assert combat.boundary == "player"
        assert len(combat.observe()["combat"]["enemies"]) == 1
        initial = combat.fingerprint
        strikes = 0
        turns = 0
        transcript = []
        for step in range(600):
            if combat.terminal:
                break
            if combat.boundary == "nature":
                event = combat.pending
                assert event["kind"] in ("enemy_intent", "draw_card")
                transcript.append(("nature", event["kind"],
                                   event["supportFingerprint"]))
                if event["kind"] == "enemy_intent":
                    combat = combat.resolve_intent(
                        event["semanticCandidates"][0]["intentId"])
                else:
                    combat = combat.resolve(0)
                continue
            assert combat.boundary == "player"
            obs = combat.observe()
            assert "semanticDeck" not in json.dumps(obs)
            assert "rng" not in json.dumps(obs).lower()
            card_ids = {c["instance_id"]: c["card_id"]
                        for c in obs["combat"]["hand"]}
            actions = combat.actions()
            strikes_here = [a for a in actions if a["kind"] == "play_card"
                            and card_ids.get(json.loads(
                                a["payloadJson"])["cardInstanceId"]) ==
                            "proto.silent.strike"]
            defends_here = [a for a in actions if a["kind"] == "play_card"
                            and card_ids.get(json.loads(
                                a["payloadJson"])["cardInstanceId"]) ==
                            "proto.silent.defend"]
            chosen = (strikes_here or defends_here or
                      [next(a for a in actions if a["kind"] == "end_turn")])[0]
            if strikes_here:
                strikes += 1
            if chosen["kind"] == "end_turn":
                turns += 1
            transcript.append(("player", chosen["actionId"]))
            combat = combat.step(chosen["actionId"])
        assert combat.terminal, "Supported starter combat never reached terminal"
        assert combat.outcome == "victory", combat.status()
        result = combat.status()
        assert result["rewardProvider"] == (
            "engine-owned-prototype-reward-unverified-v1")
        assert result["rewardDrawCount"] > 0
        assert result["probabilityStatus"] == "UNVERIFIED"
        assert combat.exit_hp is not None and combat.exit_hp > 0
        assert strikes >= 6
        assert combat.pending is None
        assert combat.fingerprint == session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint
        return initial, tuple(transcript), combat.fingerprint, combat.exit_hp, strikes, turns


def main():
    baseline = play_victory()
    with ThreadPoolExecutor(max_workers=15) as pool:
        parallel = list(pool.map(lambda _: play_victory(), range(15)))
    assert all(result == baseline for result in parallel), (
        "Act 1 terminal victory diverged across independent emulator processes")
    print("PASS: fair Silent legal-action Act 1 source-pinned weak "
          f"terminal victory; exitHp={baseline[3]}, "
          f"strikes={baseline[4]}, endTurns={baseline[5]}; "
          "complete 1-vs-15 independent process action/Nature replay; "
          "source conditional laws remain UNVERIFIED")


if __name__ == "__main__":
    main()
