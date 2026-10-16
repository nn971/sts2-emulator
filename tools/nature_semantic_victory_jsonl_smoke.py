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


def begin_combat(session, *, native_policy_baseline=False):
    return session.begin_seeded_semantic_route(
        SEED, region="Overgrowth",
        native_policy_baseline=native_policy_baseline)


def choose_action(combat):
    obs = combat.observe()
    assert "semanticDeck" not in json.dumps(obs)
    assert "rng" not in json.dumps(obs).lower()
    card_ids = {c["instance_id"]: c["card_id"]
                for c in obs["combat"]["hand"]}
    actions = combat.actions()
    strikes = [a for a in actions if a["kind"] == "play_card"
               and card_ids.get(json.loads(
                   a["payloadJson"])["CardInstanceId"]) ==
               "proto.silent.strike"]
    defends = [a for a in actions if a["kind"] == "play_card"
               and card_ids.get(json.loads(
                   a["payloadJson"])["CardInstanceId"]) ==
               "proto.silent.defend"]
    chosen = (strikes or defends or
              [next(a for a in actions if a["kind"] == "end_turn")])[0]
    return chosen, bool(strikes), obs


def without_initial_intents(observation):
    view = json.loads(json.dumps(observation))
    for enemy in view["combat"]["enemies"]:
        for key in list(enemy):
            normalized = key.replace("_", "").lower()
            if normalized in {
                    "moveid", "intentdamage", "intenthits",
                    "intentbasedamage"}:
                enemy.pop(key)
    return json.dumps(view, sort_keys=True)


def play_victory():
    with Session(COMMAND) as session:
        combat = begin_combat(session)
        route = combat.status()
        assert route["sourceRouteProvider"] == (
            "engine-owned-v111-native-weak-route-entry-unverified-v1")
        assert route["sourceRouteActionCount"] > 0
        assert route["sourceRouteProbabilityStatus"] == "UNVERIFIED"
        initial = combat.fingerprint
        strikes = 0
        turns = 0
        transcript = []
        while combat.boundary == "nature":
            event = combat.pending
            assert event["kind"] == "enemy_intent"
            transcript.append(("nature", event["kind"],
                               event["supportFingerprint"]))
            combat = combat.resolve_intent(
                event["semanticCandidates"][0]["intentId"])
        assert combat.boundary == "player", combat.status()
        initial_observation = combat.observe()
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
            chosen, has_strike, _ = choose_action(combat)
            if has_strike:
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
        return (initial, session.call(
            "semantic_status", semantic_handle=combat.handle)["result"][
                "sourceOpeningFingerprint"], tuple(transcript),
                combat.fingerprint, combat.outcome, combat.exit_hp,
                strikes, turns, initial_observation)


def play_native_policy_baseline():
    """Same player policy from an independently rebuilt source snapshot."""
    with Session(COMMAND) as session:
        combat = begin_combat(session, native_policy_baseline=True)
        status = combat.status()
        assert status["sourceRouteProvider"] == (
            "engine-owned-v111-native-weak-route-entry-unverified-v1")
        assert status["sourceRouteProbabilityStatus"] == "UNVERIFIED"
        assert status["controllerMode"] == (
            "engine-owned-native-v111-policy-baseline")
        assert status["nativePolicyProvider"] == (
            "engine-owned-native-v111-comparator-unverified-v1")
        assert status["chanceSites"] == []
        initial = combat.fingerprint
        initial_observation = combat.observe()
        history = []
        strikes = 0
        turns = 0
        for step in range(600):
            if combat.terminal:
                break
            assert combat.boundary == "player", combat.status()
            chosen, has_strike, _ = choose_action(combat)
            if has_strike:
                strikes += 1
            if chosen["kind"] == "end_turn":
                turns += 1
            history.append(("player", chosen["actionId"]))
            combat = combat.step(chosen["actionId"])
        assert combat.terminal, "Native policy baseline did not reach terminal"
        final = combat.status()
        assert final["nativePolicyDecisionCount"] > 0
        assert final["nativePolicyShuffleCount"] > 0, (
            "Native comparator must commit exact shuffle epochs")
        assert final["nativePolicyProvider"] == (
            "engine-owned-native-v111-comparator-unverified-v1")
        assert combat.pending is None
        assert combat.fingerprint == session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint
        return (initial,
                final["sourceOpeningFingerprint"],
                tuple(history), combat.fingerprint, combat.outcome,
                combat.exit_hp, strikes, turns,
                final["nativePolicyDecisionCount"],
                final["nativePolicyShuffleCount"],
                initial_observation)


def play_comparison_pair():
    nature = play_victory()
    native = play_native_policy_baseline()
    assert nature[1] == native[1], (
        "Nature and native branches must start from the same source snapshot")
    assert without_initial_intents(nature[8]) == \
        without_initial_intents(native[10]), (
            "Source-disjoint branches must agree outside the initial intent")
    assert nature[5] is not None and native[5] is not None
    return nature, native


def main():
    baseline = play_comparison_pair()
    with ThreadPoolExecutor(max_workers=15) as pool:
        parallel = list(pool.map(lambda _: play_comparison_pair(), range(15)))
    assert all(result == baseline for result in parallel), (
        "Native/Nature paired replay diverged across independent processes")
    nature, native = baseline
    print("PASS: fair Silent legal-action Act 1 source-routed weak "
          f"paired source-disjoint outcomes Nature={nature[4]}@{nature[5]}, "
          f"native-v111={native[4]}@{native[5]}; "
          f"native decisions={native[8]}, shuffles={native[9]}; "
          "complete 1-vs-15 independent process replay; "
          "source conditional laws remain UNVERIFIED")


if __name__ == "__main__":
    main()
