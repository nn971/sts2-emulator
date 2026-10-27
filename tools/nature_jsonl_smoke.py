#!/usr/bin/env python3
"""End-to-end opt-in Nature JSONL smoke; no native-game parity is claimed."""
from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"


def main() -> None:
    with subprocess.Popen(
        ["dotnet", str(DLL), "prototype-ai-jsonl"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, bufsize=1,
    ) as proc:
        def call(**kwargs):
            assert proc.stdin is not None and proc.stdout is not None
            proc.stdin.write(json.dumps(kwargs) + "\n")
            proc.stdin.flush()
            line = proc.stdout.readline()
            assert line, f"CLI exited early with {proc.poll()}"
            return json.loads(line)

        hello = call(op="hello")
        assert hello["ok"], hello
        assert hello["natureBoundarySchemaId"] == "sts2-v111-nature-isolated-boundary-v1"
        assert hello["natureBoundaryScope"] == "isolated-combat-entry-only"
        assert hello["naturePlayable"] is False
        assert hello["natureEventMetadataSchemaId"] == (
            "sts2-v111-nature-event-metadata-v1")

        hp = call(op="nature_begin", kind="initial_hp",
                  seed="nature-jsonl-hp",
                  act=2, encounter_id="proto.native.hive.encounter.mytes_normal",
                  native_entry="MytesNormal", total_floor=21,
                  map_column=3, map_row=5)
        assert hp["ok"] and hp["kind"] == "initial_hp" and hp["pending"], hp
        assert hp["pending"]["operationKind"] == "choose_index", hp
        assert hp["pending"]["ordinal"] == 0, hp
        parent_fingerprint = hp["fingerprint"]
        hp_event = call(op="nature_inspect", nature_handle=hp["natureHandle"])
        assert hp_event["ok"] and hp_event["kind"] == "initial_hp", hp_event
        assert hp_event["schemaId"] == hello["natureEventMetadataSchemaId"]
        assert hp_event["pending"]["siteId"] == "combat.initial_unique_hp"
        # The Native bounded-index transform is source-verified for the
        # declared HP support; live-pool completeness and source parity remain
        # separate evidence.
        assert hp_event["pending"]["probabilityStatus"] == "SOURCE_VERIFIED"
        law_id = hp_event["pending"]["referenceLawId"]
        assert isinstance(law_id, str) and len(law_id) == 64
        first_event_id = hp_event["pending"]["eventId"]
        assert hp_event["transcript"] == []

        bad = call(op="nature_resolve", nature_handle=hp["natureHandle"],
                   fingerprint=parent_fingerprint, selected_index=-1)
        assert not bad["ok"], bad
        bad = call(op="nature_resolve", nature_handle=hp["natureHandle"],
                   fingerprint="stale", selected_index=0)
        assert not bad["ok"], bad

        hp_saved = call(op="nature_save", nature_handle=hp["natureHandle"])
        assert hp_saved["ok"] and hp_saved["kind"] == "initial_hp", hp_saved
        hp_loaded = call(op="nature_load", kind="initial_hp",
                         snapshot=hp_saved["snapshot"])
        assert hp_loaded["ok"] and hp_loaded["fingerprint"] == parent_fingerprint
        loaded_event = call(op="nature_inspect", nature_handle=hp_loaded["natureHandle"])
        assert loaded_event["pending"]["eventId"] == first_event_id

        hp_fork = call(op="nature_fork", nature_handle=hp["natureHandle"])
        assert hp_fork["ok"] and hp_fork["fingerprint"] == parent_fingerprint
        fork_event = call(op="nature_inspect", nature_handle=hp_fork["natureHandle"])
        assert fork_event["pending"]["eventId"] == first_event_id
        hp_next = call(op="nature_resolve", nature_handle=hp["natureHandle"],
                       fingerprint=parent_fingerprint, selected_index=0)
        assert hp_next["ok"] and hp_next["pending"] and not hp_next["complete"], hp_next
        assert hp_next["pending"]["ordinal"] == 1, hp_next
        hp_next_event = call(op="nature_inspect", nature_handle=hp_next["natureHandle"])
        assert hp_next_event["transcript"][0]["eventId"] == first_event_id
        assert hp_next_event["transcript"][0]["selectedIndex"] == 0
        assert hp_next_event["pending"]["eventId"] != first_event_id
        hp_terminal = call(op="nature_resolve", nature_handle=hp_next["natureHandle"],
                           fingerprint=hp_next["fingerprint"], selected_index=0)
        assert hp_terminal["ok"] and hp_terminal["complete"], hp_terminal
        assert hp_terminal["pending"] is None, hp_terminal
        hp_terminal_save = call(op="nature_save", nature_handle=hp_terminal["natureHandle"])
        hp_terminal_restore = call(op="nature_load", kind="initial_hp",
                                   snapshot=hp_terminal_save["snapshot"])
        assert hp_terminal_restore["fingerprint"] == hp_terminal["fingerprint"]

        premature = call(op="nature_advance", nature_handle=hp_next["natureHandle"])
        assert not premature["ok"], premature
        composed = call(op="nature_advance", nature_handle=hp_terminal["natureHandle"])
        assert composed["ok"] and composed["kind"] == "intents", composed
        assert composed["natureHandle"] != hp_terminal["natureHandle"]
        composed_view = call(op="nature_inspect", nature_handle=composed["natureHandle"])
        assert len(composed_view["transcript"]) == 2
        assert composed_view["transcript"][0]["eventId"] == first_event_id
        composed_saved = call(op="nature_save", nature_handle=composed["natureHandle"])
        assert composed_saved["ok"] and composed_saved["kind"] == "intents"
        composed_loaded = call(op="nature_load", kind="intents",
                               snapshot=composed_saved["snapshot"])
        assert composed_loaded["ok"]
        assert composed_loaded["fingerprint"] == composed["fingerprint"]
        while composed["pending"] is not None:
            composed = call(op="nature_resolve",
                            nature_handle=composed["natureHandle"],
                            fingerprint=composed["fingerprint"], selected_index=0)
            assert composed["ok"], composed
        assert composed["complete"], composed

        # The intent selector commits deterministic first enemies and stops
        # at the actual native RNG callback of the last Exoskeleton.
        intents = call(op="nature_begin", kind="intents",
                       seed="nature-jsonl-intents", act=2,
                       encounter_id="proto.native.hive.encounter.exoskeletons_normal",
                       native_entry="ExoskeletonsNormal", total_floor=20,
                       map_column=3, map_row=5)
        assert intents["ok"] and intents["pending"], intents
        assert intents["pending"]["operationKind"] == "bounded_integer", intents
        assert intents["pending"]["boundedCount"] > 1, intents
        intent_view = call(op="nature_inspect", nature_handle=intents["natureHandle"])
        assert intent_view["pending"]["operationKind"] == "bounded_integer"
        assert intent_view["pending"]["boundedCount"] > 1
        assert intent_view["pending"]["orderedCandidates"] == []
        original = intents["fingerprint"]
        intent_saved = call(op="nature_save", nature_handle=intents["natureHandle"])
        intent_restored = call(op="nature_load", kind="intents",
                               snapshot=intent_saved["snapshot"])
        assert intent_restored["ok"] and intent_restored["fingerprint"] == original
        intent_fork = call(op="nature_fork", nature_handle=intents["natureHandle"])
        assert intent_fork["ok"] and intent_fork["fingerprint"] == original
        invalid = call(op="nature_resolve", nature_handle=intents["natureHandle"],
                       fingerprint=original,
                       selected_index=intents["pending"]["boundedCount"])
        assert not invalid["ok"], invalid
        intent_a = call(op="nature_resolve", nature_handle=intents["natureHandle"],
                        fingerprint=original, selected_index=0)
        intent_b = call(op="nature_resolve", nature_handle=intent_fork["natureHandle"],
                        fingerprint=original,
                        selected_index=intents["pending"]["boundedCount"] - 1)
        assert intent_a["ok"] and intent_a["complete"], intent_a
        assert intent_b["ok"] and intent_b["complete"], intent_b
        assert intent_a["fingerprint"] != intent_b["fingerprint"]
        assert intent_a["kind"] == intent_b["kind"] == "intents"
        status = call(op="close")
        assert status["ok"], status
        proc.stdin.close()
        assert proc.wait(timeout=20) == 0

    sys.path.insert(0, str(ROOT / "bindings/python"))
    from sts2_emulator import EmulatorError, Session
    command = ["dotnet", str(DLL), "prototype-ai-jsonl"]
    with Session(command) as session:
        hp = session.begin_nature("nature-python-hp", kind="initial_hp",
                                  act=2,
                                  encounter_id="proto.native.hive.encounter.mytes_normal",
                                  native_entry="MytesNormal", total_floor=21)
        inspected = hp.inspect()
        assert inspected["pending"]["siteId"] == "combat.initial_unique_hp"
        saved = hp.save_snapshot()
        restored = session.load_nature_snapshot(saved, kind="initial_hp")
        assert restored.fingerprint == hp.fingerprint
        assert restored.inspect()["pending"]["eventId"] == inspected["pending"]["eventId"]
        first = hp.resolve(0)
        second = first.resolve(0)
        assert second.complete and second.pending is None
        intents = second.advance()
        while not intents.complete:
            intents = intents.resolve(0)
        assert intents.complete
        assert session.release_nature_many([hp, restored, first, second, intents]) == 5
        try:
            hp.resolve(0)
        except EmulatorError:
            pass
        else:
            raise AssertionError("Released Nature parent handle resolved")
    print("Isolated v111 Nature HP/intent JSONL and Python binding smoke passed")


if __name__ == "__main__":
    main()
