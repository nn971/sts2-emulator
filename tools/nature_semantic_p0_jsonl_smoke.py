#!/usr/bin/env python3
"""P0 restricted playable semantic JSONL bridge: two-site Nature only.

This is an *interface smoke*, not a full combat / training evidence report.
The conditioned opening is precommitted by the separate legacy fixture
and is not counted as a Nature draw.
"""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
from jsonl_privacy_assertions import assert_public_stochastic_view
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session, EmulatorError

SEED = "nature-143-p0-semantic-jsonl"


def main():
    with Session(["dotnet", str(DLL), "prototype-ai-jsonl"]) as session:
        caps = session.capabilities
        assert caps["semanticChanceSchemaId"] == (
            "sts2-v111-semantic-playable-p0-v1")
        assert caps["semanticDrawChoiceSchemaId"] == (
            "sts2-v111-semantic-deck-v1")
        assert caps["semanticControlledSites"] == ["enemy_intent", "draw_card"]
        assert caps["semanticProbabilityStatus"] == "UNVERIFIED"
        assert caps["controlledChanceSchemaId"] == (
            "sts2-v111-controlled-playable-fixture-v1")

        hp = session.begin_nature(
            SEED, kind="initial_hp", act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20)
        while not hp.complete:
            hp = hp.resolve(0)
        intent = hp.advance()
        while not intent.complete:
            intent = intent.resolve(0)
        legacy = session.begin_controlled_combat(intent, seed=SEED)
        while legacy.trace()["pending"]:
            legacy = legacy.resolve(0)

        combat = session.begin_semantic_combat(legacy)
        assert combat.boundary == "player"
        assert combat.pending is None
        assert not combat.terminal
        visible = combat.observe()
        assert visible["combat"]["hand"]
        assert_public_stochastic_view(visible)
        before = combat.fingerprint
        saved = combat.save_snapshot()
        restored = session.load_semantic_snapshot(saved)
        assert restored.fingerprint == before
        duplicate = combat.fork()
        assert duplicate.fingerprint == before

        end = next(a for a in combat.actions() if a["kind"] == "end_turn")
        advanced = combat.step(end["actionId"])
        assert advanced.fingerprint != before
        assert advanced.boundary == "nature", advanced.status()
        try:
            combat.resolve(0)
        except EmulatorError:
            pass
        else:
            raise AssertionError("Player boundary incorrectly resolved a chance")

        try:
            session.call("semantic_resolve", semantic_handle=advanced.handle,
                         fingerprint="stale", selected_index=0)
        except EmulatorError:
            pass
        else:
            raise AssertionError("Stale Nature ticket accepted")

        n_intents = n_draws = 0
        for _ in range(100):
            if advanced.boundary in ("player", "terminal"):
                break
            event = advanced.pending
            assert event and event["kind"] in ("enemy_intent", "draw_card")
            assert event["probabilityStatus"] == "UNVERIFIED"
            if event["kind"] == "enemy_intent":
                n_intents += 1
                assert event["ownerId"].startswith("creature:")
                assert event["supportCount"] >= 1
                assert event["semanticIntentSupportSchemaId"] == (
                    "sts2-v111-semantic-weighted-intent-v1")
                assert event["semanticCandidates"]
                assert event["totalRawWeight"] == event["supportCount"]
                assert sum(event["rawEligibleWeights"]) == event["totalRawWeight"]
                assert all(c["rawWeight"] > 0 and c["intentId"] and c["moveId"]
                           for c in event["semanticCandidates"])
                assert not event["weightsVerified"]
                selected = event["semanticCandidates"][0]["intentId"]
            else:
                n_draws += 1
                assert event["candidates"]
                assert all(isinstance(c["cardInstanceId"], int) and
                           c["cardModelId"].startswith("proto.")
                           for c in event["candidates"])
                assert event["shuffleEpoch"] >= 0
            prior = advanced.fingerprint
            advanced = (advanced.resolve_intent(selected)
                        if event["kind"] == "enemy_intent"
                        else advanced.resolve_draw_card(
                            event["candidates"][0]["cardInstanceId"]))
            assert advanced.fingerprint != prior
            assert advanced.fingerprint == session.load_semantic_snapshot(
                advanced.save_snapshot()).fingerprint
        assert advanced.boundary in ("player", "terminal")
        assert n_intents > 0
        if advanced.boundary == "player":
            assert advanced.actions()
            assert_public_stochastic_view(advanced.observe())
        assert advanced.status()["chanceSites"] == ["enemy_intent", "draw_card"]
        assert session.release_semantic_many(
            [combat, restored, duplicate, advanced]) == 4

    print("P0 semantic JSONL: restricted playable source, fair view, "
          "two-site chance, replay/fork/handles passed; "
          f"intents={n_intents}, draws={n_draws}")


if __name__ == "__main__":
    main()
