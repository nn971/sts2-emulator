#!/usr/bin/env python3
"""Controlled Nature live end-turn recycle: one source-ordered shuffle in combat."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import EmulatorError, Session


def main():
    seed = "nature-jsonl-live-recycle"
    with Session(["dotnet", str(DLL), "prototype-ai-jsonl"]) as session:
        hp = session.begin_nature(
            seed, kind="initial_hp", act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20)
        while not hp.complete:
            hp = hp.resolve(0)
        intents = hp.advance()
        while not intents.complete:
            intents = intents.resolve(0)
        combat = session.begin_controlled_combat(intents, seed=seed)
        while combat.trace()["pending"]:
            combat = combat.resolve(0)

        for _ in range(5):
            end = next(action for action in combat.actions()
                       if action["kind"] == "end_turn")
            combat = combat.step(end["actionId"])
            while combat.trace()["kind"] == "next_turn_intent":
                combat = combat.resolve(0)
            if combat.trace()["kind"] == "draw_recycle":
                break
        else:
            raise AssertionError("Live recycle never occurred")

        first = combat.trace()
        assert first["pending"]["siteId"] == (
            "combat.draw_pile_recycle.swap_index")
        assert first["pending"]["streamId"] == "run.shuffle"
        # The Native Fisher-Yates bounded-index transform is source-verified
        # for this declared support; pile and continuation parity remain
        # separate evidence.
        assert first["pending"]["probabilityStatus"] == "SOURCE_VERIFIED"
        law_id = first["pending"]["referenceLawId"]
        assert isinstance(law_id, str) and len(law_id) == 64
        assert first["pending"]["boundedCount"] >= 2
        event_id = first["pending"]["eventId"]
        original = combat.fingerprint
        saved = combat.save_snapshot()
        restored = session.load_controlled_snapshot(saved)
        assert restored.fingerprint == original
        assert restored.trace()["pending"]["eventId"] == event_id
        try:
            combat.observe()
        except EmulatorError:
            pass
        else:
            raise AssertionError("Player observation leaked during Nature recycle")
        try:
            session.call("controlled_resolve",
                         controlled_handle=combat.handle,
                         fingerprint="stale",
                         selected_index=0)
        except EmulatorError:
            pass
        else:
            raise AssertionError("Stale shuffle decision was accepted")

        while combat.trace()["kind"] == "draw_recycle":
            prior = combat.trace()
            combat = combat.resolve(0)
            assert combat.trace()["transcript"][-1]["eventId"] == (
                prior["pending"]["eventId"])
        view = combat.observe()
        assert view["combat"]["hand"]
        assert combat.trace()["kind"] == "player"
        assert combat.fingerprint == session.load_controlled_snapshot(
            combat.save_snapshot()).fingerprint
        assert session.release_controlled_many([restored, combat]) == 2
    print("Live Nature draw-recycle continuation JSONL smoke passed")


if __name__ == "__main__":
    main()
