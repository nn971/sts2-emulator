#!/usr/bin/env python3
"""Extract a compact, explicitly non-replayed oracle combat action ledger.

Input: passive reference probe v4 JSONL. Incomplete asynchronous boundaries
are observational evidence, not exact emulator step boundaries.
"""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path


def runtime_leaf(obj):
    return str((obj or {}).get("runtime_type") or
               (obj or {}).get("type") or "").rsplit(".", 1)[-1]


def model_id(obj):
    return ((obj or {}).get("id") or {}).get("entry")


def creature(obj):
    return (obj or {}).get("creature") or {}


def summarize(state):
    combat = state.get("combat") or {}
    players = state.get("players") or []
    player = players[0] if players else {}
    body = creature(player)
    enemies = state.get("enemies") or []
    return {
        "round": combat.get("round_number"),
        "side": combat.get("current_side"),
        "player": {
            "hp": body.get("current_hp"),
            "block": body.get("block"),
            "energy": (player.get("combat") or {}).get("energy"),
            "hand": (player.get("hand") or {}).get("count"),
            "draw": (player.get("draw_pile") or {}).get("count"),
            "discard": (player.get("discard_pile") or {}).get("count"),
            "powers": [
                runtime_leaf(p) for p in (body.get("powers") or {}).get("items", [])
            ],
        },
        "enemies": [
            {
                "combat_id": e.get("combat_id"),
                # Passive enemy snapshots may not include monster names.
                "name": runtime_leaf(e.get("monster")) if e.get("monster") else None,
                "hp": e.get("current_hp"),
                "block": e.get("block"),
                "slot": e.get("slot_name"),
                "powers": [
                    runtime_leaf(p) for p in (e.get("powers") or {}).get("items", [])
                ],
            }
            for e in enemies
        ],
    }


def event_of(record):
    h = record.get("history_entry") or {}
    name = runtime_leaf(h)
    if name == "CardPlayStartedEntry":
        cp = h.get("card_play") or {}
        card = cp.get("card") or {}
        return {"kind": "card_started", "card": runtime_leaf(card),
                "target": ((cp.get("target") or {}).get("combat_id"))}
    if name == "CardPlayFinishedEntry":
        cp = h.get("card_play") or {}
        return {"kind": "card_finished", "card": runtime_leaf(cp.get("card") or {})}
    if name == "MonsterPerformedMoveEntry":
        return {"kind": "enemy_move", "monster": runtime_leaf(h.get("monster") or {}),
                "move": (h.get("move") or {}).get("state_id") or
                        (h.get("move") or {}).get("id") or
                        (h.get("move") or {}).get("name")}
    if name == "CardGeneratedEntry":
        return {"kind": "card_generated", "card": runtime_leaf(h.get("card") or {})}
    if name == "DamageReceivedEntry":
        result = h.get("result") or {}
        return {"kind": "damage", "unblocked": result.get("unblocked_damage"),
                "blocked": result.get("blocked_damage"),
                "total": result.get("total_damage"),
                "target": ((result.get("receiver") or {}).get("combat_id"))}
    if name == "PowerReceivedEntry":
        return {"kind": "power_received", "power": runtime_leaf(h.get("power") or {}),
                "amount": h.get("amount")}
    if name == "PotionUsedEntry":
        return {"kind": "potion_used", "potion": runtime_leaf(h.get("potion") or {})}
    if name == "BlockGainedEntry":
        return {"kind": "block_gained", "amount": h.get("amount")}
    if name == "EnergySpentEntry":
        return {"kind": "energy_spent", "amount": h.get("amount")}
    if record.get("boundary") == "combat_manager.PlayerEndedTurn":
        return {"kind": "player_ended_turn"}
    if record.get("boundary") == "combat_manager.TurnStarted":
        return {"kind": "turn_started"}
    return None


def audit(path, combats):
    records = {}
    session = None
    with path.open(encoding="utf-8") as source:
        for line_number, line in enumerate(source, 1):
            if not line.strip():
                continue
            record = json.loads(line)
            if record.get("type") == "session":
                session = record
            if record.get("type") != "boundary":
                continue
            index = record.get("recorder_combat_index")
            if index not in combats:
                continue
            state = record.get("state") or {}
            meta = (state.get("combat") or {}).get("encounter") or {}
            existing = records.setdefault(index, {"encounter": model_id(meta),
                "events": [], "snapshots": 0})
            if meta and not existing["encounter"]:
                existing["encounter"] = model_id(meta)
            existing["snapshots"] += 1
            event = event_of(record)
            if event is None:
                continue
            summary = summarize(state)
            existing["events"].append({
                "sequence": record.get("sequence"),
                "boundary": record.get("boundary"),
                "evidence": event,
                "snapshot": summary,
                "confidence": (
                    "lifecycle-marker; snapshot may be pre/post effect"
                    if record.get("boundary") == "combat_history.Changed"
                    else "boundary snapshot; not necessarily decision state"
                )
            })

    if not session:
        raise ValueError("No probe session header present")
    if session.get("schema") != "sts2-reference-probe-v4":
        raise ValueError("Requires passive reference probe v4")
    return {
        "schema": "native-overgrowth-action-ledger-v1",
        "source": {"probe_schema": session["schema"],
                   "build_fingerprint": session.get("build_fingerprint"),
                   "game_version": session.get("expected_game_version"),
                   "game_commit": session.get("expected_game_commit")},
        "limits": [
            "Passive history callbacks are not synchronous step checkpoints",
            "No emulator replay or RNG equality is claimed",
            "HP/block/powers may reflect intermediate effects",
            "Only selected combat indices are included",
        ],
        "combats": records,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("trace", type=Path)
    p.add_argument("--combats", nargs="+", type=int, default=[5, 6])
    p.add_argument("-o", "--output", type=Path)
    args = p.parse_args()
    result = audit(args.trace, set(args.combats))
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + "\n")
    for number, info in sorted(result["combats"].items()):
        counts = Counter(x["evidence"]["kind"] for x in info["events"])
        print(f"Combat {number}: {info['encounter']}, "
              f"{info['snapshots']} boundaries, {len(info['events'])} events")
        print("  " + ", ".join(f"{k}={v}" for k, v in sorted(counts.items())))
        for e in info["events"]:
            if e["evidence"]["kind"] == "enemy_move":
                print(f"  seq={e['sequence']} round={e['snapshot']['round']} "
                      f"move={e['evidence']['move']}")
    if not result["combats"]:
        raise SystemExit("No matching combat boundaries")


if __name__ == "__main__":
    main()
