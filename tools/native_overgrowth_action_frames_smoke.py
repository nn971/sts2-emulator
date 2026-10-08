#!/usr/bin/env python3
"""Contract smoke tests for native callback action frames (no game binary)."""
from __future__ import annotations

import json
import tempfile
from pathlib import Path

from native_overgrowth_action_frames import action_frames


def state(hand_ids=(1, 2), hp=50, energy=3):
    def card(idx):
        return {
            "type": "MegaCrit.Sts2.Core.Models.Cards.StrikeSilent",
            "recorder_card_id": idx,
            "id": {"entry": "STRIKE_SILENT"},
            "is_upgraded": False,
            "is_playable": True,
            "canonical_energy_cost": 1,
        }
    return {
        "combat": {
            "round_number": 1, "current_side": "Player",
            "encounter": {"id": {"entry": "BYGONE_EFFIGY_ELITE"}},
        },
        "players": [{
            "creature": {
                "current_hp": hp, "block": 0,
                "powers": {"items": [{"id": {"entry": "DEXTERITY_POWER"},
                                      "amount": 2}]},
            },
            "combat": {"energy": energy},
            "hand": {"count": len(hand_ids),
                     "items": [card(idx) for idx in hand_ids]},
            "draw_pile": {"count": 7, "items": []},
            "discard_pile": {"items": []},
            "exhaust_pile": {"items": []},
        }],
        "enemies": [{
            "combat_id": 1, "current_hp": 100, "block": 0,
            "powers": {"items": []}
        }],
        "run_rng": {"secret": "must never be exported"},
    }


def history(sequence, entry, snapshot=None, index=6):
    return {
        "type": "boundary", "boundary": "combat_history.Changed",
        "recorder_combat_index": index, "sequence": sequence,
        "state": snapshot if snapshot is not None else state(),
        "history_entry": entry,
    }


def card_play(kind, idx, target=1):
    return {
        "runtime_type": "MegaCrit.Sts2.Core.Combat.History.Entries." + kind,
        "card_play": {
            "card": {
                "runtime_type": "MegaCrit.Sts2.Core.Models.Cards.StrikeSilent",
                "recorder_card_id": idx,
            },
            "target": {"combat_id": target} if target else None,
        }
    }


def main():
    records = [
        {"type": "session", "schema": "sts2-reference-probe-v4",
         "expected_game_version": "v0.111.0",
         "expected_game_commit": "41cef1ea"},
        history(1, card_play("CardPlayStartedEntry", 2)),
        history(2, {"runtime_type": "DamageReceivedEntry",
                    "result": {"unblocked_damage": 7, "blocked_damage": 0,
                               "total_damage": 7,
                               "receiver": {"combat_id": 1}}}),
        history(3, card_play("CardPlayFinishedEntry", 2),
                state((1,), energy=2)),
        history(4, card_play("CardPlayStartedEntry", 1),
                state((1,), energy=2)),
        history(5, card_play("CardPlayFinishedEntry", 1),
                state((), energy=1)),
        {
            "type": "boundary", "boundary": "combat_manager.PlayerEndedTurn",
            "recorder_combat_index": 6, "sequence": 6, "state": state(()),
        },
    ]
    with tempfile.TemporaryDirectory() as root:
        trace = Path(root) / "capture.jsonl"
        trace.write_text(
            "\n".join(json.dumps(item) for item in records) + "\n",
            encoding="utf-8",
        )
        extracted = action_frames(trace)
        assert list(extracted["combats"]) == [6]
        info = extracted["combats"][6]
        assert info["card_play_count"] == 2
        assert info["stable_id_card_play_count"] == 2
        assert info["warnings"] == []
        first, second, ending = info["actions"]
        assert first["card"]["recorder_card_id"] == 2
        assert second["card"]["recorder_card_id"] == 1
        assert first["card"]["target_combat_id"] == 1
        assert first["start_sequence"] == 1
        assert first["finish_sequence"] == 3
        assert first["evidence"] == "object_identity"
        assert first["effects_during_callback_interval"] == [{
            "sequence": 2, "kind": "damage", "unblocked": 7,
            "blocked": 0, "total": 7, "target": 1
        }]
        assert [c["recorder_card_id"] for c in
                first["at_start_callback"]["player"]["hand"]] == [1, 2]
        assert first["at_finish_callback"]["player"]["energy"] == 2
        assert first["at_start_callback"]["player"]["draw_count"] == 7
        assert first["at_start_callback"]["player"]["powers"] == [
            {"id": "DEXTERITY_POWER", "amount": 2}
        ]
        assert "run_rng" not in json.dumps(extracted)
        assert ending["kind"] == "end_turn_marker"

        # Old v4 remains readable with a clear loss of identity evidence.
        for record in records:
            if record.get("type") != "boundary":
                continue
            card = ((record.get("history_entry") or {})
                    .get("card_play") or {}).get("card")
            if card:
                card.pop("recorder_card_id", None)
        trace.write_text(
            "\n".join(json.dumps(item) for item in records) + "\n",
            encoding="utf-8",
        )
        legacy = action_frames(trace, {6})["combats"][6]
        assert legacy["card_play_count"] == 2
        assert legacy["stable_id_card_play_count"] == 0
        assert legacy["actions"][0]["evidence"] == "card_class_only"
        assert action_frames(trace, {5})["combats"] == {}

        # Mismatch is reported, never silently paired as an exact action.
        records[3]["history_entry"]["card_play"]["card"][
            "runtime_type"] = "MegaCrit.Sts2.Core.Models.Cards.DefendSilent"
        trace.write_text(
            "\n".join(json.dumps(item) for item in records) + "\n",
            encoding="utf-8",
        )
        mismatch = action_frames(trace)["combats"][6]["actions"][0]
        assert len(mismatch["warnings"]) == 1

    print("PASS: native action frames, duplicate-card IDs, legacy v4, "
          "visible-only snapshots, callback evidence, mismatch reporting")


if __name__ == "__main__":
    main()
