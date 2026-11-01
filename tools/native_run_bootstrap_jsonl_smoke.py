#!/usr/bin/env python3
"""Real emulator JSONL run-start bootstrap smoke with EMULATOR-DERIVED source.

The journal below is synthetic by construction and MUST NOT be used as
independent evidence of native RNG, event offers or mechanics parity.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))
from sts2_emulator import Session
from native_run_bootstrap import _RELIC_TO_EMULATOR, run_bootstrap
from native_run_prelude_audit import audit
from native_neow_rng_verify import neow_event_rng


def synthetic_prelude(seed, offers, selected, node, final):
    name_for_choice = {v: k for k, v in _RELIC_TO_EMULATOR.items()}
    options = []
    for index, cid in enumerate(offers):
        rid = name_for_choice[cid]
        options.append({
            "index": index, "relic_model_id": rid,
            "text_key": "NEOW.pages.INITIAL.options." + rid,
            "is_locked": False, "is_proceed": False
        })
    _, floor, col = node.split(":")
    floor, col = int(floor), int(col)
    assert floor == 1
    run_id = "EMULATOR-DERIVED-SYNTHETIC-ONLY"
    data = []
    def add(typ, entry):
        data.append({"type": typ, "run_id": run_id,
                     "ordinal": len(data) + 1, "data": entry})
    add("run_started", {
        "schema": "native-run-prelude-v1",
        "game_build": "EMULATOR-DERIVED-SYNTHETIC-NOT-NATIVE",
        "run_id": run_id, "seed": seed, "ascension": 10,
        "character_id": "SILENT", "first_act_id": "OVERGROWTH",
        "player_count": 1, "source": "RunManager.RunStarted(RunState)",
        "source_seed": "RunState.Rng.StringSeed",
        "source_macro_choices_complete": False,
        "source_rng_consumed_by_inspector": False,
        "emulator_bootstrap": "NOT_TESTED"
    })
    streams = [
        {"scope": "run", "stream_id": "UpFront", "counter": 0,
         "s0": 1, "s1": 2, "s2": 3, "s3": 4},
        {"scope": "player", "stream_id": "Rewards", "counter": 0,
         "s0": 5, "s1": 6, "s2": 7, "s3": 8}
    ]
    streams.sort(key=lambda x: (x["scope"], x["stream_id"]))
    def rng(site):
        add("run_rng_snapshot", {
            "site": site, "player_rng_seed": 123,
            "streams": streams,
            "source": "RunRngSet.ToSerializable+PlayerRngSet.ToSerializable",
            "source_rng_consumed_by_inspector": False,
            "native_rng_precombat_history_complete": False,
            "emulator_rng_alignment": "NOT_TESTED"
        })
    rng("run_started")
    add("event_choice_request", {
        "request_id": 1, "event_model_id": "NEOW",
        "owner_id": 9, "selected_index": selected,
        "selected_text_key": options[selected]["text_key"],
        "options": options, "event_rng": neow_event_rng(seed, 6),
        "event_rng_source": "EventModel.Rng.ToSerializable",
        "source_event_rng_consumed": False,
        "event_rng_alignment": "NOT_TESTED",
        "source": "EventSynchronizer.ChooseLocalOption.prefix",
        "selection_origin": "LOCAL_SYNCHRONIZER_CALL",
        "option_execution": "NOT_OBSERVED", "effects_settled": False
    })
    add("event_choice_return", {
        "request_id": 1, "source": "EventSynchronizer.ChooseLocalOption.postfix",
        "handler_returned": True, "option_effects_settled": False
    })
    add("room_entered", {
        "act_index": 0, "act_id": "OVERGROWTH",
        "act_floor": floor, "total_floor": floor,
        "map_column": col, "map_row": floor - 1,
        "room_type": "Monster", "room_model_id": "SYNTHETIC_MONSTER",
        "player_hp": final["hp"], "player_gold": final["gold"],
        "source": "RunManager.RoomEntered",
        "visited_destination": "SOURCE_CURRENT_MAP_COORD",
        "command_provenance": "UNVERIFIED",
        "emulator_parity": "NOT_TESTED"
    })
    rng("room_entered")
    rng("combat_attach")
    add("combat_link", {
        "combat_run_id": "synthetic-combat",
        "combat_trace_file": "synthetic-combat.jsonl",
        "act_index": 0, "map_column": col, "map_row": floor - 1,
        "source": "NativeV111PassiveBridge.OpenCombat",
        "combat_initial_rng_alignment": "NOT_TESTED",
        "full_macro_action_transcript": "NOT_CAPTURED"
    })
    add("terminal", {
        "status": "SOURCE_PRELUDE_OBSERVED",
        "reason": "EMULATOR-DERIVED-SYNTHETIC-ONLY",
        "combat_link_recorded": True,
        "linked_combat_room_arrival_observed": True,
        "map_requests_missing_postfix": 0,
        "opaque_requests_missing_postfix": 0,
        "event_choice_requests_missing_postfix": 0,
        "source_macro_choices_complete": False,
        "native_rng_parity": "NOT_TESTED",
        "emulator_bootstrap": "NOT_TESTED"
    })
    result = audit(data)
    if result["status"] != "RUN_PRELUDE_SOURCE_OBSERVED":
        raise AssertionError("Synthetic fixture failed source structure audit: "
                             + repr(result["errors"]))
    return result


def main():
    command = ["dotnet", str(ROOT / "src" / "Sts2Emulator.Cli" / "bin" /
                            "Release" / "net9.0" / "Sts2Emulator.Cli.dll"),
               "prototype-ai-jsonl"]
    with Session(command) as session:
        chosen = None
        for index in range(12):
            seed = "native-bootstrap-emulator-synthetic-" + str(index)
            first = session.reset(seed, ascension=10, first_act="Overgrowth",
                                  fidelity_id="v111-experimental-native-neow-opening-v1")
            start = first.actions()
            if len(start) != 1 or start[0]["kind"] != "start_run":
                raise AssertionError("Native v111 start_run wire changed")
            neow = first.step(start[0]["actionId"])
            frame = neow.observe()
            offers = frame["event"]["offered_choice_ids"]
            for k, action in enumerate(neow.actions()):
                if action["kind"] != "event_choice":
                    continue
                next_state = neow.step(action["actionId"])
                continuation = next_state.actions()
                if (len(continuation) == 1
                    and continuation[0]["kind"] == "continue_event"):
                    next_state = next_state.step(continuation[0]["actionId"])
                if not any(x["kind"] == "choose_map_node"
                           for x in next_state.actions()):
                    continue
                for move in next_state.actions():
                    if move["kind"] != "choose_map_node":
                        continue
                    node = json.loads(move["payloadJson"]).get("NodeId")
                    if node is None:
                        node = json.loads(move["payloadJson"]).get("node_id")
                    if not isinstance(node, str) or not node.startswith("1:1:"):
                        continue
                    candidate = next_state.step(move["actionId"])
                    final = candidate.observe()
                    if final.get("combat") is not None:
                        choice_id = json.loads(action["payloadJson"])
                        choice_id = choice_id.get("ChoiceId", choice_id.get("choice_id"))
                        selected = offers.index(choice_id)
                        chosen = (seed, offers, selected, node, final)
                        break
                if chosen is not None:
                    break
            if chosen is not None:
                break
        if chosen is None:
            raise AssertionError("No direct first-floor combat in 12 synthetic seeds")
        seed, offers, selected, node, final = chosen
        source = synthetic_prelude(seed, offers, selected, node, final)
        result, snapshot = run_bootstrap(source, session)
        if result["status"] != "CANDIDATE_COMBAT_REACHED_UNCERTIFIED":
            raise AssertionError("Synthetic actual emulator bootstrap failed: "
                                 + json.dumps(result, sort_keys=True))
        if snapshot is None:
            raise AssertionError("Missing candidate emulator snapshot")
        state = json.loads(snapshot)["state"]
        if (state["run_seed"] != seed or state["ascension"] != 10
            or not isinstance(state["world"]["combat"], dict)):
            raise AssertionError("Bootstrap candidate is not a real emulator combat continuation")
        assert result["native_mechanics_parity"] == "NOT_TESTED"
        assert result["native_rng_alignment"] == "NOT_TESTED"
        assert result["ready_for_native_lockstep"] is False
        print(json.dumps({
            "status": "EMULATOR_DERIVED_SYNTHETIC_FIRST_COMBAT_BOOTSTRAP_PASS",
            "source_data": "EMULATOR_DERIVED_SYNTHETIC_ONLY",
            "real_jsonl_emulator_process": True,
            "steps": [s["kind"] for s in result["emulator_steps"]],
            "native_mechanics_parity": result["native_mechanics_parity"],
            "native_rng_alignment": result["native_rng_alignment"]
        }, sort_keys=True))


if __name__ == "__main__":
    main()
