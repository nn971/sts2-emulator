#!/usr/bin/env python3
"""Real emulator JSONL-process smoke for the M1 incremental live coordinator.

All source events here are EMULATOR-DERIVED SYNTHETIC DATA, generated from a
separate emulator state. The coordinator independently reloads that exact
emulator snapshot and steps via the JSONL protocol. This validates the live
coordinator's action transport and first-divergence behavior but supplies
zero independent original-game native parity evidence.
"""
from __future__ import annotations

import io
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))

from sts2_emulator import Session
from native_live_fidelity_coordinator import (
    IncrementalFidelityCoordinator, parse_sse)
from native_live_offline_replay import simulate_sidecar, _dump_json
from native_turn_checkpoint_jsonl_smoke import source_projection


def native_source_records(run_seed, ascension, initial, final):
    return [
        {"type": "header", "ordinal": 1, "data": {
            "run_id": "emulator-derived-smoke",
            "game_build": "EMULATOR_DERIVED_SYNTHETIC"}},
        {"type": "native_run_identity", "ordinal": 2, "data": {
            "seed": run_seed, "ascension": ascension}},
        {"type": "native_turn_checkpoint", "ordinal": 3,
         "state": source_projection(initial), "data": {
             "kind": "PLAYER_TURN_START",
             "turn": initial["state"]["world"]["combat"]["turn"],
             "settlement_status": "SETTLEMENT_UNVERIFIED"}},
        {"type": "action_started", "ordinal": 4, "action_id": 1,
         "canonical_action": {"kind": "end_turn", "payload": {}}},
        {"type": "action_completed", "ordinal": 5, "action_id": 1},
        {"type": "native_turn_checkpoint", "ordinal": 6,
         "state": source_projection(final), "data": {
             "kind": "PLAYER_TURN_START",
             "turn": final["state"]["world"]["combat"]["turn"],
             "settlement_status": "SETTLEMENT_UNVERIFIED"}},
    ]


def smoke(session):
    for seed_index in range(10):
        state = session.reset("native-live-jsonl-synthetic-" + str(seed_index),
                              ascension=10, first_act="Overgrowth")
        for _ in range(50):
            if state.observe().get("combat") is not None:
                break
            actions = state.actions()
            if not actions:
                break
            state = state.step(actions[0]["actionId"])
        if state.observe().get("combat") is None:
            continue
        end_turn = [a for a in state.actions()
                    if a.get("kind") == "end_turn"
                    and json.loads(a["payloadJson"]) == {}]
        if len(end_turn) != 1:
            continue
        continued = state.step(end_turn[0]["actionId"])
        if continued.observe().get("combat") is None:
            continue
        initial = json.loads(state.save_snapshot())
        final = json.loads(continued.save_snapshot())
        if final["state"]["world"]["combat"]["turn"] <= (
                initial["state"]["world"]["combat"]["turn"]):
            continue

        seed = initial["state"]["run_seed"]
        ascension = initial["state"]["ascension"]
        prelude = {
            "status": "RUN_PRELUDE_SOURCE_OBSERVED",
            "run_identity": {
                "game_build": "EMULATOR_DERIVED_SYNTHETIC",
                "seed": seed, "ascension": ascension},
            "first_combat_link": {"combat_run_id": "emulator-derived-smoke"},
        }
        def make_coordinator():
            return IncrementalFidelityCoordinator(
                session, prelude, bootstrap=lambda _p, _s: (
                    {"status": "CANDIDATE_COMBAT_REACHED_UNCERTIFIED"},
                    json.dumps(initial)))
        rows = native_source_records(seed, ascension, initial, final)
        envelopes = simulate_sidecar(rows)
        frames = "".join(
            f"id: {row['sequence']}\nevent: native\ndata: "
            + _dump_json(row, sort_keys=False) + "\n\n"
            for row in envelopes)
        parsed = list(parse_sse(io.StringIO(frames)))
        if len(parsed) != len(envelopes):
            raise AssertionError("SSE transport lost emulator-derived events")

        live = make_coordinator()
        for event in parsed:
            live.consume(event)
            if live.stopped:
                raise AssertionError("Healthy synthetic coordinator stopped: " +
                                     live.status + ": " + live.reason)
        if (live.status != "PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED"
            or len(live.matched) != 2 or live.stepped != 1):
            raise AssertionError("Synthetic live coordinator failed: " + str(live._receipt()))

        damaged_rows = native_source_records(seed, ascension, initial, final)
        damaged_rows[-1]["state"]["player"]["hp"] += 1
        damaged = make_coordinator()
        for row in simulate_sidecar(damaged_rows):
            damaged.consume(row)
            if damaged.stopped:
                break
        if (damaged.status != "SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED"
            or damaged.first_difference["difference"]["path"] != "$.player.hp"
            or len(damaged.matched) != 1 or damaged.stepped != 1):
            raise AssertionError("Synthetic first divergence not frozen: " +
                                 str(damaged._receipt()))
        return {
            "status": "EMULATOR_DERIVED_NATIVE_LIVE_COORDINATOR_JSONL_SMOKE_PASS",
            "real_emulator_jsonl_process": True,
            "source_evidence": "EMULATOR_DERIVED_SYNTHETIC_ONLY",
            "projected_player_turns_matched": len(live.matched),
            "emulator_actions_stepped": live.stepped,
            "mismatch_detected_at": damaged.first_difference["difference"]["path"],
            "native_live_lockstep": False,
            "native_settlement_certified": False,
        }
    raise AssertionError("No reproducible nonterminal end-turn fixture in 10 seeds")


def main():
    command = ["dotnet", str(ROOT / "src" / "Sts2Emulator.Cli" /
                              "bin" / "Release" / "net9.0" /
                              "Sts2Emulator.Cli.dll"), "prototype-ai-jsonl"]
    with Session(command) as session:
        print(json.dumps(smoke(session), sort_keys=True))


if __name__ == "__main__":
    main()
