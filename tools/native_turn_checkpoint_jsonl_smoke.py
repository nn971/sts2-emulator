#!/usr/bin/env python3
"""Actual emulator JSONL process smoke with EMULATOR-DERIVED synthetic source.

Checks exact snapshot/card mapping/turn-boundary replay mechanics. No native
sidecar is used and this test supplies ZERO independent native fidelity evidence.
"""
from __future__ import annotations

import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))
from sts2_emulator import Session
from native_turn_checkpoint_replay import replay, _token


def source_projection(snapshot: dict) -> dict:
    root = snapshot["state"]
    combat = root["world"]["combat"]
    if not isinstance(combat, dict):
        raise ValueError("Synthetic projection needs a live combat")
    by_id = {c["instance_id"]: c for c in combat["cards"]}
    def card_list(zone):
        ids = combat[zone]
        if zone == "draw_pile":
            ids = list(reversed(ids))
        return [
            {"instance_id": x, "model_id": _token(by_id[x]["card_id"])}
            for x in ids
        ]
    creatures = [{
        "id": "creature:0", "model_id": "SILENT",
        "hp": root["player"]["hp"], "block": combat["player_block"],
        "powers": [],
    }]
    for enemy in combat["enemies"]:
        creatures.append({
            "id": "creature:" + str(enemy["instance_id"]),
            "model_id": _token(enemy["enemy_id"]),
            "hp": enemy["hp"], "block": enemy["block"],
            "powers": [
                {"model_id": _token(x["power_id"]) + "_POWER",
                 "amount": x["stacks"]}
                for x in enemy["power_states"]
            ],
        })
    return {
        "player": {
            "hp": root["player"]["hp"],
            "max_hp": root["player"]["max_hp"],
            "gold": root["player"]["gold"],
            "block": combat["player_block"],
        },
        "combat": {
            "turn": combat["turn"], "energy": combat["energy"],
            "creatures": creatures,
            **{zone: card_list(zone) for zone in (
                "hand", "draw_pile", "discard_pile", "exhaust_pile")}
        },
    }


def smoke(session):
    # Need an emulator-internal first combat, then one nonterminal end turn.
    # A run-specific native fidelity profile is deliberately unnecessary:
    # this exercises only JSONL transport and canonical exact-state comparison.
    for i in range(10):
        state = session.reset("checkpoint-jsonl-emulator-derived-" + str(i),
                              ascension=10, first_act="Overgrowth")
        for _ in range(50):
            if state.observe().get("combat") is not None:
                break
            legal = state.actions()
            if not legal:
                break
            state = state.step(legal[0]["actionId"])
        if state.observe().get("combat") is None:
            continue
        choices = [x for x in state.actions()
                   if x.get("kind") == "end_turn"
                   and json.loads(x["payloadJson"]) == {}]
        if len(choices) != 1:
            continue
        next_state = state.step(choices[0]["actionId"])
        if next_state.observe().get("combat") is None:
            continue
        initial = json.loads(state.save_snapshot())
        final = json.loads(next_state.save_snapshot())
        if final["state"]["world"]["combat"]["turn"] <= (
                initial["state"]["world"]["combat"]["turn"]):
            continue
        j = {
            "schema": "native-source-action-journal-v1",
            "status": "SOURCE_ACTION_CHRONOLOGY_OBSERVED",
            "source_audit": "CALLBACK_TIMELINE_CONSISTENT",
            "run_seed": initial["state"]["run_seed"],
            "ascension": initial["state"]["ascension"],
            "errors": [], "source_requests_without_executor_start": [],
            "source_unlinked_canonical_executor_actions": [],
            "native_choice_results": [],
            "source_turn_checkpoint_candidates": [
                {"kind": "PLAYER_TURN_START", "turn":
                     initial["state"]["world"]["combat"]["turn"],
                 "ordinal": 2, "source_state": source_projection(initial)},
                {"kind": "ENEMY_TURN_START", "turn":
                     initial["state"]["world"]["combat"]["turn"],
                 "ordinal": 12},
                {"kind": "PLAYER_TURN_START", "turn":
                     final["state"]["world"]["combat"]["turn"],
                 "ordinal": 20, "source_state": source_projection(final)},
            ],
            "source_linked_commands": [{
                "executor_action_id": 1,
                "executor_start_ordinal": 5,
                "executor_complete_ordinal": 9,
                "canonical_action": {"kind": "end_turn", "payload": {}},
            }],
        }
        report = replay(j, state.save_snapshot(), session)
        if report["status"] != (
                "SOURCE_PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED"):
            raise AssertionError(json.dumps(report, sort_keys=True))
        if (len(report["player_checkpoints"]) != 2
            or len(report["enemy_checkpoint_candidates"]) != 1
            or report["native_live_lockstep"] is not False
            or report["native_full_state_parity"] != "NOT_TESTED"):
            raise AssertionError("Synthetic JSONL smoke promoted source as native parity")
        return {"status": "EMULATOR_DERIVED_TURN_CHECKPOINT_JSONL_SMOKE_PASS",
                "real_jsonl_process": True,
                "source_evidence": "EMULATOR_DERIVED_SYNTHETIC_ONLY",
                "emulator_steps": report["emulator_canonical_actions_stepped"],
                "compared_player_checkpoints": len(report["player_checkpoints"]),
                "enemy_checkpoints_deferred": len(report["enemy_checkpoint_candidates"]),
                "native_live_lockstep": False}
    raise AssertionError("No reproducible nonlethal synthetic first turn in 10 seeds")


def main():
    command = ["dotnet", str(ROOT / "src" / "Sts2Emulator.Cli" /
                             "bin" / "Release" / "net9.0" /
                             "Sts2Emulator.Cli.dll"), "prototype-ai-jsonl"]
    with Session(command) as session:
        print(json.dumps(smoke(session), sort_keys=True))


if __name__ == "__main__":
    main()
