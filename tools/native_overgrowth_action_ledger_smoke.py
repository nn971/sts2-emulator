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

        # A second tiny capture exercises newly covered public card events,
        # source-bound Ringing and the all-combats selection policy.
        extra = [
            {"type": "boundary", "recorder_combat_index": 0,
             "sequence": 1, "boundary": "combat_history.Changed",
             "state": {"combat": None}},
            boundary(102, {
                "runtime_type": "CardDrawnEntry",
                "card": {"runtime_type": "MegaCrit.Sts2.Core.Models.Cards.Backflip",
                         "current_upgrade_level": 1},
            }),
            boundary(103, {
                "runtime_type": "CardAfflictedEntry",
                "card": {
                    "runtime_type": "MegaCrit.Sts2.Core.Models.Cards.Backflip",
                    "current_upgrade_level": 1,
                    "affliction": {
                        "runtime_type":
                        "MegaCrit.Sts2.Core.Models.Afflictions.Ringing"
                    },
                },
            }),
            boundary(104, {
                "runtime_type": "BlockGainedEntry",
                "receiver": {"combat_id": 0},
                "amount": 8,
            }),
            boundary(105, {}),
        ]
        extra[-1]["boundary"] = "combat_manager.CombatEnded"
        extra[2]["state"]["enemies"][0]["powers"]["items"] = [
            {"runtime_type": "MegaCrit.Sts2.Core.Models.Powers.StrengthPower",
             "amount": 10}
        ]
        path.write_text(
            "\\n".join(json.dumps(record) for record in records + extra)
            + "\\n",
            encoding="utf-8",
        )
        full = audit(path, None)
        assert set(full["combats"]) == {6}
        kinds = [e["evidence"]["kind"] for e in full["combats"][6]["events"]]
        assert kinds == ["enemy_move", "damage", "card_drawn",
                         "card_afflicted", "block_gained", "combat_ended"]
        aff = full["combats"][6]["events"][3]
        assert aff["evidence"] == {
            "kind": "card_afflicted", "card": "Backflip",
            "upgrade": 1, "affliction": "Ringing"
        }
        assert aff["snapshot"]["enemies"][0]["power_amounts"] == [
            {"power": "StrengthPower", "amount": 10}
        ]
        assert full["combats"][6]["events"][4]["evidence"]["target"] == 0
        assert "All positive combat indices" in full["limits"][-1]
    print("PASS: native action-ledger all-combats/card-affliction/power evidence")



if __name__ == "__main__":
    main()
