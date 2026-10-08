#!/usr/bin/env python3
"""Black-box contract test for the live native-Overgrowth JSONL reset.

Run after building the CLI in Release mode, e.g. from repository root:
    python tools/native_overgrowth_reset_smoke.py
"""
from __future__ import annotations

import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
RESET_SCHEMA = "prototype-native-overgrowth-reset-v1"
MAP_PROFILE = "native-overgrowth-map-structure-v0.111.0-v1"
POLICY = "prototype-fair-v0"


def verify_start(payload: dict) -> None:
    assert payload["act"] == 1
    assert payload["floor"] == 0
    assert payload["map_generation_profile_id"] == MAP_PROFILE
    assert payload["event_id"] == "proto.native.event.neow"
    assert any(point["floor"] == 16 for point in payload["map"])
    assert len(payload["deck"]) == 13
    assert sum(
        card["card_id"] == "proto.common.restlessness"
        for card in payload["deck"]
    ) == 1
    assert sum(card["card_id"] == "proto.silent.strike" for card in payload["deck"]) == 5
    assert sum(card["card_id"] == "proto.silent.defend" for card in payload["deck"]) == 5


def main() -> None:
    process = subprocess.Popen(
        ["dotnet", str(DLL), "prototype-ai-jsonl"],
        cwd=ROOT,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    assert process.stdin is not None
    assert process.stdout is not None
    handles: list[str] = []
    sequence = 0

    def request(op: str, **arguments: object) -> dict:
        nonlocal sequence
        sequence += 1
        request_id = f"native-{sequence}"
        body = {"request_id": request_id, "op": op, **arguments}
        process.stdin.write(json.dumps(body) + "\n")
        process.stdin.flush()
        line = process.stdout.readline()
        if not line:
            raise AssertionError(
                f"Bridge terminated during {op}; stderr={process.stderr.read()!r}"
            )
        response = json.loads(line)
        assert response["requestId"] == request_id, response
        assert response["ok"], response
        return response

    def observe(handle: str) -> tuple[dict, str]:
        response = request(
            "observe", state_handle=handle, policy_id=POLICY
        )
        assert response["schemaId"] == "prototype-ai-v0"
        assert response["policyId"] == POLICY
        return json.loads(response["payloadJson"]), response["observationHash"]

    try:
        hello = request("hello")
        assert hello["nativeOvergrowthResetId"] == RESET_SCHEMA
        assert hello["wireSchemaId"] == "prototype-ai-jsonl-v0"
        assert hello["fairPolicyId"] == POLICY

        # The legacy op is unchanged and MUST NOT silently start the native map.
        legacy = request("reset", seed="native-bridge-seed")["stateHandle"]
        handles.append(legacy)
        legacy_payload, _ = observe(legacy)
        assert legacy_payload["map_generation_profile_id"] != MAP_PROFILE

        # A missing ascension means zero; repeated seeds/actions replay exactly.
        first = request("reset_native_overgrowth", seed="native-bridge-seed")
        second = request(
            "reset_native_overgrowth",
            seed="native-bridge-seed",
            ascension=0,
        )
        for response in (first, second):
            assert response["schemaId"] == RESET_SCHEMA
            assert isinstance(response["stateHandle"], str)
            assert "hypothetical" not in response
            handles.append(response["stateHandle"])

        left, right = first["stateHandle"], second["stateHandle"]
        before_left, left_hash = observe(left)
        before_right, right_hash = observe(right)
        verify_start(before_left)
        assert before_left == before_right
        assert left_hash == right_hash

        left_actions = request("legal_actions", state_handle=left)["actions"]
        right_actions = request("legal_actions", state_handle=right)["actions"]
        assert left_actions == right_actions
        assert len(left_actions) == 3
        assert all(action["kind"] == "event_choice" for action in left_actions)
        assert request("is_terminal", state_handle=left)["terminal"] is False

        # The factory creates a genuine live handle, not a synthetic one.
        sequence += 1
        process.stdin.write(json.dumps({
            "request_id": f"native-{sequence}",
            "op": "hypothetical_draw_order",
            "state_handle": left,
            "ordered_cards": [],
        }) + "\n")
        process.stdin.flush()
        rejected = json.loads(process.stdout.readline())
        assert rejected["requestId"] == f"native-{sequence}"
        assert rejected["ok"] is False
        assert "hypothetical" in rejected["error"].lower()

        chosen = left_actions[0]["actionId"]
        left_step = request("step", state_handle=left, action_id=chosen)
        right_step = request("step", state_handle=right, action_id=chosen)
        assert left_step["parent"] == left
        assert right_step["parent"] == right
        assert left_step["action"] == right_step["action"]
        assert left_step["exactHash"] == right_step["exactHash"]
        handles.extend((left_step["child"], right_step["child"]))
        assert observe(left_step["child"])[1] == observe(right_step["child"])[1]
        assert request(
            "is_terminal", state_handle=left_step["child"]
        )["terminal"] is False

        # A higher Ascension value exercises the same native factory path.
        high = request(
            "reset_native_overgrowth", seed="native-a10", ascension=10
        )
        assert high["schemaId"] == RESET_SCHEMA
        handles.append(high["stateHandle"])
        verify_start(observe(high["stateHandle"])[0])

        released = request("release_many", state_handles=handles)
        assert released["released"] == len(set(handles))
        handles.clear()
        sequence += 1
        process.stdin.write(json.dumps({
            "request_id": f"native-{sequence}", "op": "observe",
            "state_handle": left, "policy_id": POLICY,
        }) + "\n")
        process.stdin.flush()
        invalid = json.loads(process.stdout.readline())
        assert invalid["ok"] is False
        assert "Unknown state handle" in invalid["error"]

        request("close")
        process.wait(timeout=10)
        assert process.returncode == 0
        print("PASS native Overgrowth JSONL capability, reset, public "
              "state, replay, live-handle guard and release")
    finally:
        if process.poll() is None:
            process.kill()
        process.wait(timeout=10)


if __name__ == "__main__":
    main()
