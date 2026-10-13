#!/usr/bin/env python3
"""Protocol check for local conditional combat-stream rejection.

The positive exact-state/cursor behavior is tested with a C# deterministic
fixture; this smoke checks user-facing handle provenance and fail-closed JSONL.
"""
from __future__ import annotations

import json
import pathlib
import subprocess


def main() -> None:
    executable = (
        pathlib.Path("src/Sts2Emulator.Cli/bin/Release/net9.0")
        / "Sts2Emulator.Cli.dll"
    )
    with subprocess.Popen(
        ["dotnet", str(executable), "prototype-ai-jsonl"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        text=True,
    ) as process:
        assert process.stdin is not None and process.stdout is not None

        def call(request: dict, *, expected_ok: bool = True) -> dict:
            process.stdin.write(json.dumps(request) + "\n")
            process.stdin.flush()
            response = json.loads(process.stdout.readline())
            assert response["ok"] is expected_ok, response
            return response

        hello = call({"op": "hello"})
        assert hello["conditionalCombatEntryId"] == (
            "prototype-local-combat-stream-condition-v1"
        )
        live = call({"op": "reset", "seed": "live-private-run-seed"})["stateHandle"]
        synthetic = call({
            "op": "reset_hypothetical",
            "search_seed": "0000000000000000000000000000000f",
        })["stateHandle"]
        handles = [live, synthetic]
        try:
            for label, root in (("live", live), ("hypothetical", synthetic)):
                start = call({
                    "op": "legal_actions", "state_handle": root,
                })["actions"][0]["actionId"]
                mapped = call({
                    "op": "step", "state_handle": root, "action_id": start,
                })["child"]
                handles.append(mapped)
                frame = call({
                    "op": "observe", "state_handle": mapped,
                    "policy_id": "prototype-fair-v0",
                })
                actions = call({
                    "op": "legal_actions", "state_handle": mapped,
                })["actions"]
                combat_choice = next(
                    item for item in actions if item["kind"] == "choose_map_node"
                )
                response = call({
                    "op": "condition_combat_entry",
                    "state_handle": mapped,
                    "action_id": combat_choice["actionId"],
                    "expected_observation_hash": "intentionally-no-match",
                    "expected_legal_action_ids": [combat_choice["actionId"]],
                    "search_seed": "0000000000000000000000000000002a",
                    "max_candidates": 2,
                }, expected_ok=False)
                assert "child" not in response
                if label == "live":
                    assert "hypothetical" in response["error"]
                else:
                    assert "exhausted" in response["error"]

                again = call({
                    "op": "observe", "state_handle": mapped,
                    "policy_id": "prototype-fair-v0",
                })
                assert again == frame
        finally:
            released = call({"op": "release_many", "state_handles": handles})
            assert released["released"] == len(handles)
        call({"op": "close"})
        assert process.wait(timeout=5) == 0
        print("Local combat-stream entry: hypothetical gate and rejection checks passed")


if __name__ == "__main__":
    main()
