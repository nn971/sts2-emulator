#!/usr/bin/env python3
"""Act 1 source-pinned weak combat victory with fair player actions.

Protocol/semantic mechanics acceptance, not a model training benchmark.
All processes have separate emulator handles, RNG copies and observations.
"""
from concurrent.futures import ThreadPoolExecutor
import argparse
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
from jsonl_privacy_assertions import assert_public_stochastic_view
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session

DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
COMMAND = ["dotnet", str(DLL), "prototype-ai-jsonl"]
SEED = "nature-p0-weak-real-victory"


def begin_combat(session, *, seed=SEED, region="Overgrowth",
                 native_policy_baseline=False):
    return session.begin_seeded_semantic_route(
        seed, region=region,
        native_policy_baseline=native_policy_baseline)


def choose_action(combat):
    obs = combat.observe()
    assert_public_stochastic_view(obs)
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


def validate_intent_support(event):
    assert event["semanticIntentSupportSchemaId"] == (
        "sts2-v111-semantic-weighted-intent-v1")
    candidates = event["semanticCandidates"]
    weights = event["rawEligibleWeights"]
    assert candidates and weights == [candidate["rawWeight"]
                                      for candidate in candidates]
    assert event["totalRawWeight"] == event["supportCount"]
    assert event["probabilityStatus"] == "UNVERIFIED"
    assert event["weightsVerified"] is False
    ticket_start = 0
    for candidate in candidates:
        assert candidate["rawWeight"] > 0
        assert candidate["ticketStart"] == ticket_start
        assert candidate["intentId"] and candidate["moveId"]
        ticket_start += candidate["rawWeight"]
    assert ticket_start == event["totalRawWeight"]
    return len(candidates), ticket_start


def play_victory(seed=SEED, region="Overgrowth"):
    with Session(COMMAND) as session:
        combat = begin_combat(session, seed=seed, region=region)
        route = combat.status()
        assert route["sourceRouteProvider"] == (
            "engine-owned-v111-native-weak-route-entry-unverified-v1")
        assert route["sourceRouteActionCount"] > 0
        assert route["sourceRouteProbabilityStatus"] == "UNVERIFIED"
        initial = combat.fingerprint
        strikes = 0
        turns = 0
        intent_events = 0
        intent_candidates = 0
        transcript = []
        while combat.boundary == "nature":
            event = combat.pending
            assert event["kind"] == "enemy_intent"
            transcript.append(("nature", event["kind"],
                               event["supportFingerprint"]))
            candidate_count, _ = validate_intent_support(event)
            intent_events += 1
            intent_candidates += candidate_count
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
                    candidate_count, _ = validate_intent_support(event)
                    intent_events += 1
                    intent_candidates += candidate_count
                    combat = combat.resolve_intent(
                        event["semanticCandidates"][0]["intentId"])
                else:
                    combat = combat.resolve_draw_card(
                        event["candidates"][0]["cardInstanceId"])
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
                strikes, turns, initial_observation, intent_events,
                intent_candidates)


def play_native_policy_baseline(seed=SEED, region="Overgrowth"):
    """Same player policy from an independently rebuilt source snapshot."""
    with Session(COMMAND) as session:
        combat = begin_combat(session, seed=seed, region=region,
                              native_policy_baseline=True)
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


def play_comparison_pair(seed=SEED, region="Overgrowth"):
    nature = play_victory(seed=seed, region=region)
    native = play_native_policy_baseline(seed=seed, region=region)
    assert nature[1] == native[1], (
        "Nature and native branches must start from the same source snapshot")
    assert without_initial_intents(nature[8]) == \
        without_initial_intents(native[10]), (
            "Source-disjoint branches must agree outside the initial intent")
    assert nature[5] is not None and native[5] is not None
    return nature, native


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--worker-repeats", type=int, default=15,
        help="independent paired reruns per route seed (default: 15; 0 disables)")
    args = parser.parse_args()
    if args.worker_repeats < 0:
        parser.error("--worker-repeats must be zero or greater")

    route_cases = [
        ("canonical Overgrowth", SEED, "Overgrowth"),
        ("Overgrowth/Shrinker Beetle", "route-audit-01", "Overgrowth"),
        ("Overgrowth/Fuzzy Wurm Crawler", "route-audit-02", "Overgrowth"),
        ("Underdocks/Toadpoles", "route-audit-01", "Underdocks"),
        ("Underdocks/Sludge Spinner", "route-audit-02", "Underdocks"),
    ]
    # Check deterministic paired replay at every included route seed, not
    # just the canonical encounter.
    with ThreadPoolExecutor(max_workers=max(1, args.worker_repeats)) as pool:
        for label, seed, region in route_cases:
            baseline = play_comparison_pair(seed=seed, region=region)
            parallel = list(pool.map(
                lambda _: play_comparison_pair(seed=seed, region=region),
                range(args.worker_repeats)))
            assert all(result == baseline for result in parallel), (
                f"Native/Nature paired replay diverged for {label} "
                "across independent processes")
            nature, native = baseline
            print(f"PASS: fair Silent legal-action Act 1 source-routed "
                  f"paired case {label}: Nature={nature[4]}@{nature[5]}, "
                  f"native-v111={native[4]}@{native[5]}; "
                  f"weighted intent events={nature[9]} "
                  f"(candidates={nature[10]}); "
                  f"native decisions={native[8]}, shuffles={native[9]}; "
                  f"independent paired reruns={args.worker_repeats}")
    print("Source conditional laws remain UNVERIFIED")


if __name__ == "__main__":
    main()
