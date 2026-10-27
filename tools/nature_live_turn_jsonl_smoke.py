#!/usr/bin/env python3
"""Live end-turn intent Nature boundary JSONL smoke for issue #143."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import Session, EmulatorError

SEED = "nature-143-live-jsonl"


def main():
    with Session(["dotnet", str(DLL), "prototype-ai-jsonl"]) as s:
        hp = s.begin_nature(
            SEED, kind="initial_hp", act=2,
            encounter_id="proto.native.hive.encounter.exoskeletons_normal",
            native_entry="ExoskeletonsNormal", total_floor=20)
        while not hp.complete:
            hp = hp.resolve(0)
        intents = hp.advance()
        while not intents.complete:
            intents = intents.resolve(0)
        state = s.begin_controlled_combat(intents, seed=SEED)
        while state.trace()["pending"]:
            state = state.resolve(0)

        first_obs = state.observe()
        assert first_obs["combat"]["turn"] == 1
        end = next(action for action in state.actions()
                   if action["kind"] == "end_turn")
        next_state = state.step(end["actionId"])
        chance = next_state.trace()
        assert chance["kind"] == "next_turn_intent", chance
        assert chance["pending"]["siteId"] == (
            "combat.next_turn_intent.index_draw")
        assert chance["pending"]["streamId"] == "run.monster_ai"
        assert chance["playerActionIds"] == [end["actionId"]]
        # The canonical Native monster-AI stream has a source-verified
        # NextInt grid law for this declared index support. This law metadata
        # does not assert source-call or whole-combat parity.
        assert chance["pending"]["probabilityStatus"] == "SOURCE_VERIFIED"
        law_id = chance["pending"]["referenceLawId"]
        assert isinstance(law_id, str) and len(law_id) == 64

        try:
            next_state.observe()
        except EmulatorError as error:
            assert "player boundary" in str(error)
        else:
            raise AssertionError("Player observed mid-action state")

        suspended = s.load_controlled_snapshot(next_state.save_snapshot())
        assert suspended.fingerprint == next_state.fingerprint
        assert suspended.trace()["pending"]["eventId"] == (
            chance["pending"]["eventId"])
        try:
            s.call("controlled_resolve",
                   controlled_handle=next_state.handle,
                   fingerprint="stale",
                   selected_index=0)
        except EmulatorError:
            pass
        else:
            raise AssertionError("Stale Nature fingerprint accepted")
        assert next_state.fingerprint == suspended.fingerprint
        assert next_state.trace()["pending"]["eventId"] == (
            suspended.trace()["pending"]["eventId"])

        while next_state.trace()["pending"]:
            old = next_state.trace()
            next_state = next_state.resolve(0)
            assert next_state.trace()["transcript"][-1]["eventId"] == (
                old["pending"]["eventId"])
        after = next_state.observe()
        assert after["combat"]["turn"] == 2
        assert next_state.trace()["playerActionIds"] == [end["actionId"]]
        assert next_state.fingerprint == s.load_controlled_snapshot(
            next_state.save_snapshot()).fingerprint
        assert s.release_controlled_many([suspended, next_state]) == 2
    print("Live Nature end-turn intent continuation smoke passed")


if __name__ == "__main__":
    main()
