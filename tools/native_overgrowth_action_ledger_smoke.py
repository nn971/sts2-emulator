#!/usr/bin/env python3
"""Self-contained fixture checks for the conservative native combat ledger."""
from __future__ import annotations

import json
import tempfile
from pathlib import Path

from native_overgrowth_action_ledger import audit


def boundary(sequence: int, history: dict) -> dict:
    return {
        "type": "boundary",
        "recorder_combat_index": 6,
        "sequence": sequence,
        "boundary": "combat_history.Changed",
        "state": {
            "combat": {
                "round_number": 2,
                "current_side": "Enemy",
                "encounter": {"id": {"entry": "BYGONE_EFFIGY_ELITE"}},
            },
            "players": [
                {
                    "creature": {
                        "current_hp": 34,
                        "max_hp": 70,
                        "block": 7,
                        "powers": {"items": []},
                    },
                    "combat": {"energy": 0},
                    "hand": {"count": 0},
                    "draw_pile": {"count": 10},
                    "discard_pile": {"count": 4},
                }
            ],
            "enemies": [
                {
                    "type": "MegaCrit.Sts2.Core.Entities.Creatures.Creature",
                    "combat_id": 1,
                    "current_hp": 104,
                    "block": 0,
                    "powers": {"items": []},
                }
            ],
        },
        "history_entry": history,
    }


def main() -> None:
    records = [
        {"type": "session", "schema": "sts2-reference-probe-v4",
         "expected_game_version": "v0.111.0",
         "expected_game_commit": "41cef1ea",
         "build_fingerprint": "fixture"},
        boundary(100, {
            "runtime_type":
                "MegaCrit.Sts2.Core.Combat.History.Entries.MonsterPerformedMoveEntry",
            "monster": {
                "runtime_type":
                    "MegaCrit.Sts2.Core.Models.Monsters.BygoneEffigy"
            },
            "move": {"state_id": "WAKE_MOVE"},
        }),
        boundary(101, {
            "runtime_type":
                "MegaCrit.Sts2.Core.Combat.History.Entries.DamageReceivedEntry",
            "result": {
                "receiver": {"combat_id": 0},
                "blocked_damage": 17,
                "unblocked_damage": 1,
                "total_damage": 18,
            },
        }),
    ]
    with tempfile.TemporaryDirectory() as temp:
        path = Path(temp) / "oracle.jsonl"
        path.write_text(
            "\n".join(json.dumps(record) for record in records) + "\n",
            encoding="utf-8",
        )
        result = audit(path, {6})
        enemy = result["combats"][6]
        assert enemy["encounter"] == "BYGONE_EFFIGY_ELITE"
        assert enemy["snapshots"] == 2
        first, second = enemy["events"]
        assert first["evidence"] == {
            "kind": "enemy_move",
            "monster": "BygoneEffigy",
            "move": "WAKE_MOVE",
        }
        assert first["snapshot"]["enemies"][0]["combat_id"] == 1
        assert first["snapshot"]["enemies"][0]["hp"] == 104
        assert first["snapshot"]["enemies"][0]["name"] is None
        assert second["evidence"]["total"] == 18
        assert second["evidence"]["unblocked"] == 1
        assert second["evidence"]["blocked"] == 17
        assert second["evidence"]["target"] == 0
        assert result["source"]["game_version"] == "v0.111.0"
    print("PASS: native action-ledger move IDs, damage, snapshots, provenance")


if __name__ == "__main__":
    main()
