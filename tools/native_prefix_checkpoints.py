#!/usr/bin/env python3
"""Extract *closed decision prefixes* from otherwise incomplete native traces.

Only records strictly BEFORE a subsequent independent stable decision are
eligible. A terminal state at a nested-choice abort is never accepted as
an independent post-state. This checks internal trace linkage, not emulator
parity, source hook order, native RNG equivalence, or hidden continuation.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from native_parity import load_trace
from native_expectation_fixture import get_card, compact_state


def closed_prefixes(records: list[dict]) -> list[dict]:
    if not records or records[0].get("trace_schema") != "0.2":
        return []
    indexed = [(i, r) for i, r in enumerate(records) if r.get("type") == "decision"]
    prefixes: list[dict] = []
    for (start, before), (finish, after) in zip(indexed, indexed[1:]):
        decision_id = before.get("decision_id")
        if (type(decision_id) is not int or after.get("decision_id") != decision_id + 1):
            break
        between = records[start + 1:finish]
        actions = [x for x in between if x.get("type") == "action"
                   and x.get("decision_id") == decision_id]
        transitions = [x for x in between if x.get("type") == "transition"
                       and x.get("decision_id") == decision_id]
        events = [x for x in between if x.get("type") == "event"
                  and x.get("decision_id") == decision_id]
        if len(actions) != 1 or len(transitions) != 1:
            break
        if any(x.get("type") not in ("action", "event", "transition") or
               x.get("decision_id") != decision_id for x in between):
            break
        action, transition = actions[0], transitions[0]
        positions = [(i, x.get("type")) for i, x in enumerate(between)]
        ai = next(i for i, k in positions if k == "action")
        ti = next(i for i, k in positions if k == "transition")
        if ai >= ti or any(i >= ti for i, k in positions if k == "event"):
            break
        chosen = action.get("action")
        if (action.get("accepted") is not True or
                not isinstance(chosen, dict) or
                chosen not in before.get("legal_actions", []) or
                transition.get("status") != "COMPLETE" or
                transition.get("before_hash") != before.get("state_hash") or
                transition.get("after_hash") != after.get("state_hash") or
                not isinstance(before.get("state"), dict) or
                not isinstance(after.get("state"), dict)):
            break
        last_seq = before.get("event_seq")
        if type(last_seq) is not int:
            break
        for event in events:
            if event.get("event_seq") != last_seq + 1:
                break
            last_seq += 1
        else:
            if (transition.get("first_event_seq") != before["event_seq"] + 1 or
                    transition.get("last_event_seq") != last_seq or
                    after.get("event_seq") != last_seq):
                break
            prefixes.append({
                "decision_id": decision_id,
                "action": chosen,
                "before": before,
                "after": after,
                "transition": transition,
                "events": events,
            })
            continue
        break
    return prefixes


def expectation(prefix: dict, header: dict, raw_status: str) -> dict | None:
    action = prefix["action"]
    if action.get("kind") != "play_card":
        return None
    card_id = (action.get("payload") or {}).get("card_instance_id")
    if type(card_id) is not int:
        return None
    pre = prefix["before"]["state"]
    card = get_card(pre, card_id)
    if card is None:
        return None
    coverage = prefix["before"].get("coverage") or {}
    required = {"$.player.hp", "$.player.block", "$.combat.energy",
                "$.combat.creatures", "$.combat.hand"}
    verified = coverage.get("verified_fields", [])
    return {
        "fixture_schema": "native-expectation-v1",
        "status": "RECORDED_NATIVE_EXPECTATION",
        "reason": "Closed native decision prefix with independent successor checkpoint; full trace may be incomplete",
        "prefix_only": True,
        "raw_trace_terminal_status": raw_status,
        "native_capture": "RECORDED_NATIVE_PREFIX; SOURCE_HOOK_ORDER_NOT_TESTED",
        "emulator_parity": "NOT_TESTED",
        "ready_for_emulator_step": False,
        "native_trace_header": header,
        "decision_id": prefix["decision_id"],
        "card_family": str(card.get("model_id", "")).casefold(),
        "selected_card": card,
        "selected_action": action,
        "pre_state": compact_state(pre),
        "post_state": compact_state(prefix["after"]["state"]),
        "pre_hash": prefix["before"].get("state_hash"),
        "post_hash": prefix["after"].get("state_hash"),
        "legal_actions": prefix["before"].get("legal_actions", []),
        "native_events": [
            {"event_seq": e.get("event_seq"), "kind": e.get("kind"),
             "actor_id": e.get("actor_id"), "target_id": e.get("target_id"),
             "payload": e.get("payload"), "visibility": e.get("visibility")}
            for e in prefix["events"]
        ],
        "coverage": {
            "verified_fields": verified,
            "required_initial_fields": sorted(required),
            "missing_initial_fields": sorted(required - set(verified)),
            "missing_continuation_fields": coverage.get("missing_fields", []),
            "unsupported_mechanics": coverage.get("unsupported_mechanics", []),
            "legal_actions_complete": coverage.get("legal_actions_complete") is True,
            "audited_events": coverage.get("verified_events", []),
            "rng_status": coverage.get("rng_status", "RNG_UNAUDITED"),
        },
        "next_check": {
            "state_differences_visible": True,
            "emulator_import": "NOT_TESTED",
            "parity": "NOT_TESTED",
            "native_source_hook_order": "NOT_TESTED",
            "native_prefix_hashes": "LINKED_NOT_INDEPENDENTLY_RECOMPUTED",
        },
    }


def extract_dir(directory: Path, fixture_dir: Path | None = None) -> dict:
    results: list[dict] = []
    total = 0
    written = 0
    for path in sorted(directory.glob("passive-*.jsonl")):
        if path.name.endswith(".actions.jsonl"):
            continue
        # load_trace returns parsed records EVEN when it reports the last
        # decision incomplete. Never recover malformed or dropped data.
        records, error = load_trace(path)
        recoverable_error = (error is None or
            error == "Native trace reports an incomplete capture" or
            error.startswith("Unfinished transition for decision ") or
            error.startswith("Transition for decision "))
        if not recoverable_error or not records or records[-1].get("type") != "terminal":
            results.append({"file": path.name, "status": "REJECTED",
                            "reason": error or "Missing terminal",
                            "closed_prefix_count": 0, "fixtures": []})
            continue
        valid = closed_prefixes(records)
        total += len(valid)
        raw_terminal = records[-1].get("status", "UNKNOWN")
        files = []
        for prefix in valid:
            fixture = expectation(prefix, records[0], raw_terminal)
            if fixture is not None and fixture_dir is not None:
                fixture_dir.mkdir(parents=True, exist_ok=True)
                filename = path.stem + f"-prefix-decision-{prefix['decision_id']}.json"
                (fixture_dir / filename).write_text(
                    json.dumps(fixture, indent=2, sort_keys=True) + "\n", encoding="utf-8")
                files.append(filename)
                written += 1
        results.append({
            "file": path.name,
            "status": "CLOSED_PREFIXES_ONLY" if valid else "NO_CLOSED_PREFIX",
            "full_trace_status": "INCOMPLETE_CAPTURE" if error else "STRUCTURALLY_VALID",
            "closed_prefix_count": len(valid),
            "actions": [{"decision_id": x["decision_id"],
                         "kind": x["action"].get("kind"),
                         "card_model": (expectation(x, records[0], raw_terminal) or {})
                                         .get("selected_card", {}).get("model_id")}
                        for x in valid],
            "fixtures": files,
        })
    return {
        "schema": "native-closed-prefixes-v1",
        "status": "NATIVE_CHECKPOINT_EVIDENCE_ONLY",
        "emulator_parity": "NOT_TESTED",
        "exact_rng_parity": "NOT_TESTED",
        "source_hook_order_parity": "NOT_TESTED",
        "recovered_closed_prefix_count": total,
        "play_card_fixtures_written": written,
        "traces": results,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--fixture-dir", type=Path)
    args = parser.parse_args()
    if not args.directory.is_dir():
        parser.error("Directory does not exist: " + str(args.directory))
    result = extract_dir(args.directory, args.fixture_dir)
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
