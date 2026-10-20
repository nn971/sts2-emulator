#!/usr/bin/env python3
"""External two-policy Silent Shrinker Beetle gate: real engine, laws and replay.

An intentionally tiny controller tests mechanics only; it is not trained Nature.
Each worker starts its own independent emulator JSONL subprocess.
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import random
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session  # noqa: E402

COMMAND = ["dotnet", str(ROOT /
    "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"),
    "prototype-ai-jsonl"]
SEED = "nature-p0-weak-real-victory"


def choose_player(combat):
    public = combat.observe()
    encoded = json.dumps(public).lower()
    assert "semanticdeck" not in encoded and '"rng"' not in encoded
    hand = {c["instance_id"]: c["card_id"] for c in public["combat"]["hand"]}
    actions = combat.actions()
    for card_id in ("proto.silent.strike", "proto.silent.defend"):
        for action in actions:
            if (action["kind"] == "play_card" and
                hand.get(json.loads(action["payloadJson"])["CardInstanceId"])
                    == card_id):
                return action["actionId"]
    return next(action["actionId"] for action in actions
                if action["kind"] == "end_turn")


def inspect_law(event):
    assert event["kind"] in ("enemy_intent", "draw_card"), event
    law = event["chanceLaw"]
    assert law["schemaId"] == "sts2-v111-chance-law-v1"
    assert law["probabilityStatus"] == event["probabilityStatus"] == "UNVERIFIED"
    assert law["eventId"] == event["eventId"]
    assert law["ownerId"] == event["ownerId"]
    assert law["ordinal"] == event["ordinal"]
    outcomes = law["outcomes"]
    assert outcomes and all(o["weight"] > 0 for o in outcomes)
    assert len({o["outcomeId"] for o in outcomes}) == len(outcomes)
    assert law["totalWeight"] == sum(o["weight"] for o in outcomes)
    if event["kind"] == "enemy_intent":
        candidates = event["semanticCandidates"]
        assert [o["outcomeId"] for o in outcomes] == [
            x["intentId"] for x in candidates]
        assert [o["weight"] for o in outcomes] == [
            x["rawWeight"] for x in candidates]
    else:
        assert [o["outcomeId"] for o in outcomes] == [
            "card_instance:" + str(x["cardInstanceId"])
            for x in event["candidates"]]
        assert law["conditioningFingerprint"] == event["constraintFingerprint"]
    return outcomes


def run_one(policy):
    with Session(COMMAND) as session:
        def play():
            state = session.begin_seeded_semantic_combat(
                SEED, act=1,
                encounter_id="proto.encounter.shrinker_beetle_weak",
                native_entry="ShrinkerBeetleWeak", total_floor=2,
                ascension=0, first_monster_combat_id=1,
                map_column=3, map_row=1,
                replay_opening_draw_hooks=True,
                native_policy_baseline=False)
            opening = state.status()["sourceOpeningFingerprint"]
            initial = state.fingerprint
            intent_rng = random.Random(20261021)
            draw_rng = random.Random(718)
            transcript = []
            draws = nontrivial = intents = 0
            for _ in range(600):
                if state.terminal:
                    break
                if state.boundary == "player":
                    action = choose_player(state)
                    child = state.step(action)
                    transcript.append(("player", action, child.fingerprint))
                elif state.boundary == "nature":
                    event = state.pending
                    outcomes = inspect_law(event)
                    if event["kind"] == "enemy_intent":
                        selected = intent_rng.choices(
                            outcomes, weights=[o["weight"] for o in outcomes])[0]
                        child = state.resolve_intent(selected["outcomeId"])
                        intents += 1
                    else:
                        selected = (
                            outcomes[-1] if policy == "learned-draw-stub"
                            else draw_rng.choices(
                                outcomes,
                                weights=[o["weight"] for o in outcomes])[0])
                        child = state.resolve_draw_card(
                            int(selected["outcomeId"].split(":")[1]))
                        draws += 1
                        nontrivial += len(outcomes) > 1
                    transcript.append((
                        event["kind"], event["chanceLaw"]["lawId"],
                        selected["outcomeId"], child.fingerprint))
                else:
                    raise AssertionError("Unknown semantic boundary: " + state.boundary)
                state = child
            assert state.terminal and state.boundary == "terminal"
            assert state.outcome in ("victory", "defeat")
            assert state.pending is None
            saved = state.save_snapshot()
            assert session.load_semantic_snapshot(saved).fingerprint == state.fingerprint
            assert intents > 0 and draws > 0 and nontrivial > 0, (
                policy, intents, draws, nontrivial)
            return (opening, initial, tuple(transcript), state.fingerprint,
                    state.outcome, state.exit_hp, intents, draws, nontrivial)
        first = play()
        assert play() == first, "Independent replay and terminal snapshot differ"
        return first


def paired():
    learned = run_one("learned-draw-stub")
    reference = run_one("reference")
    assert learned[:2] == reference[:2], "Different conditioned openings"
    return learned, reference


def main():
    baseline = paired()
    with ThreadPoolExecutor(max_workers=15) as pool:
        results = list(pool.map(lambda _: paired(), range(15)))
    assert all(result == baseline for result in results), (
        "1-worker and 15-process full action/chance/snapshot transcripts diverged")
    for name, row in zip(("learned-draw-stub", "reference"), baseline):
        print("PASS", name, "outcome", row[4], "HP", row[5],
              "intent_events", row[6], "physical_draws", row[7],
              "nontrivial_draws", row[8])
    print("PASS 1-versus-15 independent external emulator processes; "
          "laws UNVERIFIED; no learned-policy effectiveness claim")


if __name__ == "__main__":
    main()
