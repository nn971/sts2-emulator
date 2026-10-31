#!/usr/bin/env python3
"""Actual JSONL-process smoke for the persistent shadow stepper.

The 'native' source journal is deliberately generated from emulator-visible
observations in this test. It verifies transport/step mechanics ONLY and makes
NO claim about parity with the real game or source snapshot import.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings" / "python"))

from sts2_emulator import Session
from native_lockstep_shadow import run_shadow


def projection(frame: dict) -> dict:
    combat = frame["combat"]
    if not isinstance(combat, dict):
        raise ValueError("Expected combat for shadow smoke")
    return {
        "player": {
            "hp": frame["hp"], "max_hp": frame["max_hp"],
            "block": combat["player_block"],
        },
        "combat": {
            "energy": combat["energy"],
            "turn": combat["turn"],
            "hand": [
                {"instance_id": card["instance_id"],
                 "model_id": card["card_id"]}
                for card in combat["hand"]
            ],
        },
    }


def test_real_jsonl(command: list[str]) -> dict:
    with Session(command) as session:
        root = session.reset("native-shadow-jsonl-synthetic-smoke-v1",
                             ascension=10, first_act="Overgrowth")
        # Advance a real *emulator* run, always using legal actions. The
        # synthetic test needs only one combat and never reads native data.
        state = root
        for _ in range(45):
            observed = state.observe()
            if observed.get("combat") is not None:
                break
            legal = state.actions()
            if not legal:
                raise AssertionError("No emulator legal action before first combat")
            state = state.step(legal[0]["actionId"])
        else:
            raise AssertionError("Emulator did not enter combat in 45 legal steps")

        first = state.observe()
        legal = state.actions()
        # End-turn is always a valid simple combat action and carries no
        # special card ID mapping, nested hand choice, or selected target.
        turn = next((a for a in legal if a["kind"] == "end_turn"
                     and json.loads(a["payloadJson"]) == {}), None)
        if turn is None:
            raise AssertionError("Emulator first combat has no end_turn action")
        emulated_child = state.step(turn["actionId"])
        final = emulated_child.observe()
        if final.get("combat") is None:
            raise AssertionError("Fixture first turn must preserve a live combat")
        raw_snapshot = state.save_snapshot()
        owned = json.loads(raw_snapshot)
        seed = owned["state"]["run_seed"]
        ascension = owned["state"]["ascension"]
        # The fake native IDs in this synthetic smoke are intentionally equal
        # to emulator IDs; a real capture must supply independent crosswalk.
        all_cards = first["combat"]["hand"] + final["combat"]["hand"]
        crosswalk = {
            "schema": "native-emulator-id-crosswalk-v1",
            "cards": {str(card["instance_id"]): card["instance_id"]
                      for card in all_cards},
            "enemies": {},
        }
        journal = {
            "schema": "native-source-action-journal-v1",
            "status": "SOURCE_ACTION_CHRONOLOGY_OBSERVED",
            "source_audit": "CALLBACK_TIMELINE_CONSISTENT",
            "run_seed": seed,
            "ascension": ascension,
            "source_unlinked_canonical_executor_actions": [],
            "errors": [],
            "native_choice_results": [],
            "source_linked_commands": [{
                "input_request_id": 1,
                "request_ordinal": 3,
                "executor_action_id": 17,
                "executor_start_ordinal": 4,
                "executor_complete_ordinal": 9,
                "canonical_action": {"kind": "end_turn", "payload": {}},
                "pre_request_state": projection(first),
                "post_execution_state": projection(final),
            }],
        }
        report = run_shadow(journal, raw_snapshot, crosswalk, session)
        if report["status"] != "SHADOW_STEPPED_VISIBLE_ONLY":
            raise AssertionError(
                "Real JSONL session shadow smoke failed: " +
                json.dumps(report, sort_keys=True))
        if report["emulator_actions_stepped"] != 1:
            raise AssertionError("Expected one actual emulator transition")
        if (report["native_game_parity"] != "NOT_TESTED"
            or report["emulator_rng_alignment"] != "NOT_TESTED"):
            raise AssertionError("Synthetic fixture claimed real-game parity")
        return {
            "status": "SYNTHETIC_EMULATOR_JSONL_SHADOW_SMOKE_PASS",
            "real_emulator_session": True,
            "source_data": "EMULATOR_DERIVED_SYNTHETIC_ONLY",
            "shadow_actions_stepped": report["emulator_actions_stepped"],
            "native_game_parity": report["native_game_parity"],
        }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--cli-dll", type=Path, default=(
        ROOT / "src" / "Sts2Emulator.Cli" / "bin" /
        "Release" / "net9.0" / "Sts2Emulator.Cli.dll"))
    args = parser.parse_args()
    report = test_real_jsonl([
        args.dotnet, str(args.cli_dll), "prototype-ai-jsonl"])
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
