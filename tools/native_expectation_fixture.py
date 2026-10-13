#!/usr/bin/env python3
"""Extract an authentic *native expectation* from a passive combat trace.

A recorded expectation is an oracle observation, not a reconstructed emulator
run. Only a verified emulator continuation can upgrade it to parity evidence.
No game binaries or privileged memory are included in the result.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from native_parity import load_trace, transition_data

ALLOWED_CARD_IDS = {"strike", "defend"}


def get_card(state: dict[str, Any], instance_id: int) -> dict[str, Any] | None:
    hand = state.get("combat", {}).get("hand", [])
    matches = [c for c in hand if c.get("instance_id") == instance_id]
    return matches[0] if len(matches) == 1 else None


def card_family(model_id: str) -> str | None:
    """STS2 mod/source IDs have character-specific prefixes/suffixes."""
    words = model_id.casefold().replace("-", "_").split("_")
    for family in ALLOWED_CARD_IDS:
        if family in words or model_id.casefold() == family:
            return family
    return None


def compact_state(state: dict[str, Any]) -> dict[str, Any]:
    # Keep all original canonical fields. Do not silently turn a partial
    # observation into a complete emulator continuation snapshot.
    return json.loads(json.dumps(state))


def extract(records: list[dict[str, Any]], decision_id: int) -> dict[str, Any]:
    base = {
        "fixture_schema": "native-expectation-v1",
        "status": "UNSUPPORTED",
        "decision_id": decision_id,
        "native_capture": "UNVERIFIED",
        "emulator_parity": "NOT_TESTED",
        "ready_for_emulator_step": False,
        "reason": "",
    }
    if not records or records[0].get("trace_schema") != "0.2":
        return {**base, "reason": "Trace schema 0.2 required"}
    if records[0].get("capture_mode") != "passive":
        return {**base, "reason": "Native passive recording required"}
    try:
        steps = transition_data(records)
    except ValueError as error:
        return {**base, "reason": "Invalid native trace structure: " + str(error)}
    current = steps.get(decision_id)
    if current is None or current.get("action") is None:
        return {**base, "reason": "Decision/action not recorded"}
    if "transition" not in current or current["transition"].get("status") != "COMPLETE":
        return {**base, "reason": "Decision transition incomplete"}
    if current["before"].get("phase") != "Combat":
        return {**base, "reason": "Only ordinary combat currently supported"}
    action = current["action"].get("action") or {}
    if not current["action"].get("accepted") or action.get("kind") != "play_card":
        return {**base, "reason": "A recorded accepted play_card is required"}
    payload = action.get("payload") or {}
    card_id = payload.get("card_instance_id")
    if type(card_id) is not int:
        return {**base, "reason": "Missing native card instance ID"}
    pre = current["before"].get("state") or {}
    card = get_card(pre, card_id)
    if card is None:
        return {**base, "reason": "Card instance absent or duplicated in original hand"}
    family = card_family(str(card.get("model_id", "")))
    if family is None:
        return {**base, "reason": "First native fixture restricted to Strike and Defend"}
    next_steps = [x for idx, x in steps.items() if idx > decision_id]
    if next_steps:
        post_boundary = next_steps[0]["before"]
        post = post_boundary.get("state")
        post_hash = post_boundary.get("state_hash")
    else:
        terminal = records[-1]
        post = terminal.get("state") if terminal.get("type") == "terminal" else None
        post_hash = current["transition"].get("after_hash")
    if not isinstance(post, dict):
        return {**base, "reason": "Next decision or terminal post-state unavailable"}
    if current["transition"].get("after_hash") != post_hash:
        return {**base, "reason": "Native transition hash disagrees with post checkpoint"}
    verified = current["before"].get("coverage", {}).get("verified_fields", [])
    required = {"$.player.hp", "$.player.block", "$.combat.energy",
                "$.combat.creatures", "$.combat.hand"}
    coverage = current["before"].get("coverage", {})
    unsupported = sorted(coverage.get("unsupported_mechanics", []))
    missing = sorted(set(required) - set(verified))
    if not current["action"]["action"] in current["before"].get("legal_actions", []):
        return {**base, "reason": "Recorded action not contained in advertised legal choices"}

    return {
        **base,
        "status": "RECORDED_NATIVE_EXPECTATION",
        "reason": "Recorded native before/action/after and chronological diagnostic history",
        "native_capture": "SOURCE_AUDITED_LOCAL_VALIDATION_PENDING",
        "native_trace_header": records[0],
        "decision_id": decision_id,
        "card_family": family,
        "selected_card": card,
        "selected_action": action,
        "pre_state": compact_state(pre),
        "post_state": compact_state(post),
        "pre_hash": current["before"].get("state_hash"),
        "post_hash": post_hash,
        "legal_actions": current["before"].get("legal_actions", []),
        "native_events": [
            {
                "event_seq": event.get("event_seq"),
                "kind": event.get("kind"),
                "actor_id": event.get("actor_id"),
                "target_id": event.get("target_id"),
                "payload": event.get("payload"),
                "visibility": event.get("visibility"),
            } for event in current["events"]
        ],
        "coverage": {
            "verified_fields": verified,
            "required_initial_fields": sorted(required),
            "missing_initial_fields": missing,
            "missing_continuation_fields": coverage.get("missing_fields", []),
            "unsupported_mechanics": unsupported,
            "legal_actions_complete": coverage.get("legal_actions_complete") is True,
            "audited_events": coverage.get("verified_events", []),
            "rng_status": coverage.get("rng_status", "RNG_UNAUDITED"),
        },
        "next_check": {
            "state_differences_visible": True,
            "emulator_import": "UNIMPLEMENTED",
            "parity": "NOT_TESTED",
            "human_hook_origin": "UNAUDITED",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_trace", type=Path)
    parser.add_argument("--decision", type=int, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    records, error = load_trace(args.native_trace)
    if error:
        result = {"status": "INCOMPLETE_CAPTURE", "reason": error,
                  "emulator_parity": "NOT_TESTED"}
    else:
        result = extract(records, args.decision)
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0 if result["status"] == "RECORDED_NATIVE_EXPECTATION" else 2


if __name__ == "__main__":
    raise SystemExit(main())
