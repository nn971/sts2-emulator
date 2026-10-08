#!/usr/bin/env python3
"""Build evidence-level player action frames from a passive STS2 probe v4.

The source contains asynchronous CombatHistory callbacks. Frame snapshots
are callback-time observations, *not* exact simulator pre/post-step states.
Recorder card IDs are available in new captures; older v4 logs work with
explicitly weaker, card-class-only identification.
"""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

from native_overgrowth_action_ledger import event_of, model_id, runtime_leaf


def visible_snapshot(state):
    """Decision-relevant PUBLIC projection, intentionally omitting RNG/draw order."""
    combat = state.get("combat") or {}
    players = state.get("players") or []
    player = players[0] if players else {}
    body = player.get("creature") or {}
    combat_player = player.get("combat") or {}

    def powers(creature):
        items = ((creature.get("powers") or {}).get("items") or [])
        return [
            {"id": ((power.get("id") or {}).get("entry")),
             "amount": power.get("amount")}
            for power in items
        ]

    def card_model(card):
        return {
            "recorder_card_id": card.get("recorder_card_id"),
            "id": ((card.get("id") or {}).get("entry")),
            "upgraded": card.get("is_upgraded"),
            "affliction": (runtime_leaf(card["affliction"])
                           if card.get("affliction") else None),
            "energy_cost": card.get("canonical_energy_cost"),
            "playable_observed": card.get("is_playable"),
        }

    def public_pile(name):
        pile = player.get(name) or {}
        return [card_model(c) for c in (pile.get("items") or [])]

    return {
        "round": combat.get("round_number"),
        "side": combat.get("current_side"),
        "player": {
            "hp": body.get("current_hp"),
            "block": body.get("block"),
            "energy": combat_player.get("energy"),
            "hand": public_pile("hand"),
            "draw_count": (player.get("draw_pile") or {}).get("count"),
            "discard": public_pile("discard_pile"),
            "exhaust": public_pile("exhaust_pile"),
            "powers": powers(body),
        },
        "enemies": [
            {"combat_id": enemy.get("combat_id"),
             "hp": enemy.get("current_hp"),
             "block": enemy.get("block"),
             "is_dead": enemy.get("is_dead"),
             "powers": powers(enemy)}
            for enemy in (state.get("enemies") or [])
        ],
    }


def card_from_play(entry):
    play = entry.get("card_play") or {}
    card = play.get("card") or {}
    target = play.get("target") or {}
    return {
        "native_type": runtime_leaf(card),
        "recorder_card_id": card.get("recorder_card_id"),
        "target_combat_id": target.get("combat_id"),
    }


def action_frames(trace: Path, combats: set[int] | None = None):
    session = None
    data = {}
    stacks: dict[int, list[dict]] = {}
    for line_number, line in enumerate(trace.open(encoding="utf-8"), 1):
        if not line.strip():
            continue
        try:
            record = json.loads(line)
        except json.JSONDecodeError as exc:
            raise ValueError(f"Invalid JSONL record at line {line_number}") from exc
        if record.get("type") == "session":
            session = record
            continue
        if record.get("type") != "boundary":
            continue
        index = record.get("recorder_combat_index")
        if not isinstance(index, int) or index <= 0:
            continue
        if combats is not None and index not in combats:
            continue
        state = record.get("state") or {}
        encounter = model_id((state.get("combat") or {}).get("encounter"))
        info = data.setdefault(index, {
            "encounter": None, "actions": [], "warnings": [],
            "history_events_outside_card_plays": Counter()
        })
        if encounter:
            info["encounter"] = encounter
        stack = stacks.setdefault(index, [])
        seq = record.get("sequence")
        h = record.get("history_entry") or {}
        kind = runtime_leaf(h)
        boundary = record.get("boundary")

        if boundary == "combat_history.Changed" and kind == "CardPlayStartedEntry":
            action = {
                "kind": "card_play",
                "start_sequence": seq,
                "finish_sequence": None,
                "card": card_from_play(h),
                "at_start_callback": visible_snapshot(state),
                "at_finish_callback": None,
                "effects_during_callback_interval": [],
                "evidence": "object_identity" if
                    card_from_play(h)["recorder_card_id"] is not None
                    else "card_class_only",
                "warnings": []
            }
            if stack:
                stack[-1]["warnings"].append(
                    f"Nested card play starts at sequence {seq}; effects may overlap")
            stack.append(action)
            continue

        if boundary == "combat_history.Changed" and kind == "CardPlayFinishedEntry":
            if not stack:
                info["warnings"].append(f"Unpaired card finish at sequence {seq}")
                continue
            action = stack.pop()
            end = card_from_play(h)
            start = action["card"]
            if (end["native_type"] != start["native_type"] or
                (end["recorder_card_id"] is not None and
                 start["recorder_card_id"] is not None and
                 end["recorder_card_id"] != start["recorder_card_id"])):
                action["warnings"].append(
                    f"Card start/finish mismatch at sequence {seq}: {end}")
            action["finish_sequence"] = seq
            action["at_finish_callback"] = visible_snapshot(state)
            info["actions"].append(action)
            continue

        if boundary == "combat_history.Changed":
            evidence = event_of(record)
            if evidence is not None:
                observed = {"sequence": seq, **evidence}
                if stack:
                    stack[-1]["effects_during_callback_interval"].append(observed)
                else:
                    info["history_events_outside_card_plays"][evidence["kind"]] += 1
            continue

        # These markers delimit observable user activity, but occur after
        # native asynchronous turn/potion effects, not at simulator step entry.
        if boundary == "combat_manager.PlayerEndedTurn":
            info["actions"].append({
                "kind": "end_turn_marker",
                "sequence": seq,
                "at_callback": visible_snapshot(state),
                "evidence": "passive_boundary"
            })

    if session is None:
        raise ValueError("Missing probe session header")
    if session.get("schema") != "sts2-reference-probe-v4":
        raise ValueError("Expected passive reference probe v4")
    for index, info in data.items():
        for action in stacks.get(index, []):
            info["warnings"].append(
                f"Unfinished card play at sequence {action['start_sequence']}")
        info["actions"].sort(
            key=lambda action: action.get("start_sequence",
                                          action.get("sequence", -1)))
        info["history_events_outside_card_plays"] = dict(
            sorted(info["history_events_outside_card_plays"].items()))
        info["card_play_count"] = sum(
            a["kind"] == "card_play" for a in info["actions"])
        info["stable_id_card_play_count"] = sum(
            a["kind"] == "card_play" and a["evidence"] == "object_identity"
            for a in info["actions"])
    return {
        "schema": "native-combat-action-frames-v1",
        "source": {
            "probe_schema": session.get("schema"),
            "game_version": session.get("expected_game_version"),
            "game_commit": session.get("expected_game_commit"),
            "build_fingerprint": session.get("build_fingerprint"),
        },
        "limitations": [
            "Card start and finish snapshots are asynchronous callback observations",
            "Intermediate changes can be caused by queued effects or unrelated hooks",
            "The extractor supplies no native RNG-to-emulator alignment",
            "Unseen player decisions and non-combat choices are outside the stream",
            "The native recorder's card IDs are combat-local object identities",
            "Private draw-pile order and RNG state are intentionally excluded",
        ],
        "combats": data,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path)
    parser.add_argument("--combats", nargs="+", type=int)
    parser.add_argument("--output", "-o", type=Path)
    args = parser.parse_args()
    result = action_frames(args.trace, set(args.combats) if args.combats else None)
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + "\n",
                               encoding="utf-8")
    for index, info in sorted(result["combats"].items()):
        print(f"Combat {index}: {info['encounter']}, "
              f"{info['card_play_count']} paired card plays, "
              f"{info['stable_id_card_play_count']} with stable IDs, "
              f"{len(info['warnings'])} pairing warnings")


if __name__ == "__main__":
    main()
