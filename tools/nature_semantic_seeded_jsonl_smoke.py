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
from jsonl_privacy_assertions import assert_public_stochastic_view
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
            map_column=3, map_row=5,
            replay_opening_draw_hooks=True)
        assert combat.boundary == "player"
        assert combat.status()["sourceOpeningProvider"] == (
            "engine-owned-seeded-source-shaped-draw-hooks-unverified-v1")
        assert combat.pending is None
        public = combat.observe()
        assert_public_stochastic_view(public)
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
            if event["kind"] == "enemy_intent":
                candidates = event["semanticCandidates"]
                assert event["semanticIntentSupportSchemaId"] == (
                    "sts2-v111-semantic-weighted-intent-v1")
                assert candidates and event["weightsVerified"] is False
                assert event["totalRawWeight"] == event["supportCount"]
                assert sum(c["rawWeight"] for c in candidates) == (
                    event["totalRawWeight"])
                combat = combat.resolve_intent(candidates[0]["intentId"])
            else:
                combat = combat.resolve_draw_card(
                    event["candidates"][0]["cardInstanceId"])
            count += 1
        assert any(kind == "enemy_intent" for kind, _ in transcript)
        assert combat.boundary in ("player", "terminal")
        assert combat.status()["chanceSites"] == ["enemy_intent", "draw_card"]
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        result = initial, tuple(transcript), combat.fingerprint, combat.boundary
        return result


def terminal_rollout():
    # A complete real combat, with only legal player end_turn actions and
    # legal source-filtered intent or physical-card draw Nature choices.
    # This is mechanical terminal coverage, not a learning evaluation.
    with Session(COMMAND) as session:
        combat = session.begin_seeded_semantic_combat(
            SEED + "-defeat", act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20,
            map_column=3, map_row=5)
        source_opening = combat.status()["sourceOpeningFingerprint"]
        initial_observation = without_initial_intents(combat.observe())
        turns = events = 0
        trace = []
        while not combat.terminal:
            assert combat.boundary == "player"
            assert turns < 40, "Real combat did not terminate"
            end = next(a for a in combat.actions() if a["kind"] == "end_turn")
            combat = combat.step(end["actionId"])
            turns += 1
            while combat.boundary == "nature":
                assert events < 1200, "Unfinished terminal combat chance events"
                event = combat.pending
                assert event["kind"] in ("enemy_intent", "draw_card")
                assert event["probabilityStatus"] == "UNVERIFIED"
                trace.append((event["kind"], event["supportFingerprint"]))
                if event["kind"] == "enemy_intent":
                    combat = combat.resolve_intent(
                        event["semanticCandidates"][0]["intentId"])
                else:
                    combat = combat.resolve_draw_card(
                        event["candidates"][0]["cardInstanceId"])
                events += 1
        assert combat.boundary == "terminal"
        assert combat.outcome == "defeat", combat.status()
        assert combat.exit_hp is not None and combat.exit_hp <= 0
        assert combat.pending is None
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        status = combat.status()
        assert status["terminal"] and status["exitHp"] == combat.exit_hp
        return (source_opening, initial_observation, turns, tuple(trace),
                combat.outcome, combat.exit_hp, combat.fingerprint,
                status.get("nativePolicyDecisionCount", 0),
                status.get("nativePolicyShuffleCount", 0))


def terminal_native_policy_rollout():
    with Session(COMMAND) as session:
        combat = session.begin_seeded_semantic_combat(
            SEED + "-defeat", act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20,
            map_column=3, map_row=5, native_policy_baseline=True)
        source_opening = combat.status()["sourceOpeningFingerprint"]
        initial_observation = without_initial_intents(combat.observe())
        turns = 0
        while not combat.terminal:
            assert turns < 40, "Native-policy normal combat did not terminate"
            assert combat.boundary == "player", combat.status()
            end = next(a for a in combat.actions()
                       if a["kind"] == "end_turn")
            combat = combat.step(end["actionId"])
            turns += 1
        assert combat.boundary == "terminal"
        assert combat.outcome == "defeat", combat.status()
        assert combat.exit_hp is not None and combat.exit_hp <= 0
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        status = combat.status()
        assert status["nativePolicyProvider"] == (
            "engine-owned-native-v111-comparator-unverified-v1")
        assert status["nativePolicyDecisionCount"] > 0
        assert status["nativePolicyShuffleCount"] > 0
        return (source_opening, initial_observation, turns, (),
                combat.outcome, combat.exit_hp, combat.fingerprint,
                status["nativePolicyDecisionCount"],
                status["nativePolicyShuffleCount"])


def without_initial_intents(observation):
    view = json.loads(json.dumps(observation))
    for enemy in view["combat"]["enemies"]:
        for key in list(enemy):
            if key.replace("_", "").lower() in {
                    "moveid", "intentdamage", "intenthits",
                    "intentbasedamage"}:
                enemy.pop(key)
    return json.dumps(view, sort_keys=True)


def terminal_comparison_pair():
    nature = terminal_rollout()
    native = terminal_native_policy_rollout()
    assert nature[0] == native[0], (
        "Normal encounter branches must share a source opening")
    assert nature[1] == native[1], (
        "Normal encounter fair openings differ outside initial intent")
    assert nature[4] == native[4] == "defeat"
    assert nature[5] is not None and native[5] is not None
    return nature, native


def engine_cost_rollout():
    with Session(COMMAND) as session:
        provider = "engine-owned-native-v111-cost-stream-unverified-v1"
        assert provider in session.capabilities["semanticOtherChanceProviders"]
        combat = session.begin_seeded_semantic_combat(
            SEED + "-snecko-native-cost", act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20,
            map_column=3, map_row=5,
            other_chance_provider_id=provider,
            fixture_draw_cost_relic_id="proto.relic.snecko_eye",
            replay_opening_draw_hooks=True)
        assert combat.status()["otherChanceProvider"] == provider
        assert combat.status()["otherChanceDecisionCount"] == 0
        assert_public_stochastic_view(combat.observe())
        action = next(a for a in combat.actions() if a["kind"] == "end_turn")
        combat = combat.step(action["actionId"])
        trace = []
        while combat.boundary == "nature":
            assert len(trace) < 150
            event = combat.pending
            assert event["kind"] in ("enemy_intent", "draw_card")
            trace.append((event["kind"], event["supportFingerprint"]))
            if event["kind"] == "enemy_intent":
                combat = combat.resolve_intent(
                    event["semanticCandidates"][0]["intentId"])
            else:
                combat = combat.resolve_draw_card(
                    event["candidates"][0]["cardInstanceId"])
        assert combat.boundary in ("player", "terminal")
        state = combat.status()
        assert state["otherChanceProvider"] == provider
        assert state["otherChanceDecisionCount"] > 0
        assert state["probabilityStatus"] == "UNVERIFIED"
        assert session.load_semantic_snapshot(
            combat.save_snapshot()).fingerprint == combat.fingerprint
        return tuple(trace), combat.fingerprint, state["otherChanceDecisionCount"]


def main():
    baseline = rollout()
    with ThreadPoolExecutor(max_workers=15) as pool:
        independent = list(pool.map(lambda _: rollout(), range(15)))
    assert all(value == baseline for value in independent), (
        "Seeded Nature session differs across independent JSONL processes")
    cost = engine_cost_rollout()
    with ThreadPoolExecutor(max_workers=15) as pool:
        cost_workers = list(pool.map(
            lambda _: engine_cost_rollout(), range(15)))
    assert all(value == cost for value in cost_workers), (
        "Engine-owned native-codec cost rolls differ by process")
    terminal = terminal_comparison_pair()
    with ThreadPoolExecutor(max_workers=15) as pool:
        terminal_workers = list(pool.map(
            lambda _: terminal_comparison_pair(), range(15)))
    assert all(value == terminal for value in terminal_workers), (
        "Paired normal terminal combat differs across independent processes")
    print("PASS: seeded Nature opening, 1 vs 15 independent process "
          "first-turn and full-terminal transcript equality; "
          f"seeded Hive normal defeat after Nature/native turns="
          f"{terminal[0][2]}/{terminal[1][2]}, "
          f"exit HP={terminal[0][5]}/{terminal[1][5]}; "
          f"replay; engine-owned cost rolls={cost[2]} per worker; "
          "only intent/draw Nature; source law UNVERIFIED")


if __name__ == "__main__":
    main()
