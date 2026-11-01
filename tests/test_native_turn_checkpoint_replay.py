"""Source checkpoint replay: independent emulator state, never parity certification."""
import base64
import copy
import json
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_turn_checkpoint_replay import (
    derive_crosswalk, first_difference, replay, _native_rng_window,
    _selected_choice, _check_rng,
)


def source(turn, hand, draw, discard, hp=40, strength=0):
    def cards(ids):
        return [{"instance_id": i, "model_id": "STRIKE_SILENT" if i == 0
                 else "DEFEND_SILENT"} for i in ids]
    return {
        "player": {"hp": 50, "max_hp": 70, "gold": 99, "block": 0},
        "combat": {
            "turn": turn, "energy": 3, "max_energy": 3,
            "creatures": [
                {"id": "creature:0", "model_id": "SILENT",
                 "hp": 50, "block": 0, "powers": []},
                {"id": "creature:1", "model_id": "FUZZY_WURM_CRAWLER",
                 "hp": hp, "block": 0, "powers": (
                     [{"model_id": "STRENGTH_POWER", "amount": strength}]
                     if strength else [])},
            ],
            "hand": cards(hand), "draw_pile": cards(draw),
            "discard_pile": cards(discard), "exhaust_pile": [],
        },
    }


def exact(turn, hand, draw, discard, hp=40, strength=0):
    combat = {
        "turn": turn, "energy": 3, "max_energy": 3, "player_block": 0,
        "cards": [
            {"instance_id": 100, "card_id": "proto.silent.strike"},
            {"instance_id": 101, "card_id": "proto.silent.defend"},
        ],
        "hand": hand, "draw_pile": list(reversed(draw)),
        "discard_pile": discard, "exhaust_pile": [],
        "enemies": [
            {"instance_id": 5, "enemy_id": "Fuzzy_Wurm_Crawler",
             "hp": hp, "block": 0,
             "power_states": ([{"power_id": "proto.power.strength",
                               "stacks": strength}] if strength else [])}
        ],
    }
    return {
        "schema": "sts2-exact-snapshot-v1",
        "state": {"run_seed": "TEST-SEED", "ascension": 10,
                  "player": {"hp": 50, "max_hp": 70, "gold": 99},
                  "world": {"combat": combat}},
    }


class FakeState:
    def __init__(self, snapshot, legal=None, child=None):
        self.snapshot = snapshot
        self.legal = legal or []
        self.child = child
        self.stepped = []

    def actions(self):
        return copy.deepcopy(self.legal)

    def save_snapshot(self):
        return json.dumps(self.snapshot)

    def step(self, action_id):
        self.stepped.append(action_id)
        if self.child is None:
            raise RuntimeError("Unexpected emulator step")
        return self.child


class FakeSession:
    def __init__(self, root):
        self.root = root
        self.loaded = 0

    def load_snapshot(self, value):
        self.loaded += 1
        return self.root


def checkpoint(turn, ordinal, state):
    return {"kind": "PLAYER_TURN_START", "turn": turn,
            "ordinal": ordinal, "source_state": state,
            "settlement_status": "SETTLEMENT_UNVERIFIED"}


def journal(a, b):
    return {
        "schema": "native-source-action-journal-v1",
        "status": "SOURCE_ACTION_CHRONOLOGY_OBSERVED",
        "source_audit": "CALLBACK_TIMELINE_CONSISTENT",
        "run_seed": "TEST-SEED", "ascension": 10, "errors": [],
        "source_requests_without_executor_start": [],
        "source_unlinked_canonical_executor_actions": [],
        "native_choice_results": [],
        "source_turn_checkpoint_candidates": [
            checkpoint(1, 2, a),
            {"kind": "ENEMY_TURN_START", "turn": 1, "ordinal": 15,
             "settlement_status": "SETTLEMENT_UNVERIFIED"},
            checkpoint(2, 20, b)
        ],
        "source_linked_commands": [
            {"executor_action_id": 1, "executor_start_ordinal": 5,
             "executor_complete_ordinal": 8,
             "canonical_action": {
                 "kind": "play_card",
                 "payload": {"card_instance_id": 0, "target_id": "creature:1"}}},
            {"executor_action_id": 2, "executor_start_ordinal": 10,
             "executor_complete_ordinal": 13,
             "canonical_action": {"kind": "end_turn", "payload": {}}}
        ],
    }


def fixture():
    a = source(1, [0], [1], [])
    b = source(2, [1], [], [0], hp=34)
    after_play = exact(1, [], [101], [100], hp=34)
    after_play["state"]["world"]["combat"]["energy"] = 2
    finish = FakeState(exact(2, [101], [], [100], hp=34))
    end = FakeState(after_play, [
        {"kind": "end_turn", "actionId": "end",
         "payloadJson": "{}"},
    ], finish)
    start = FakeState(exact(1, [100], [101], []), [
        {"kind": "play_card", "actionId": "strike",
         "payloadJson": json.dumps({
             "CardInstanceId": 100, "TargetEnemyId": 5})},
    ], end)
    return journal(a, b), start, end, finish


class NativeTurnCheckpointReplayTests(unittest.TestCase):
    def test_independent_persistent_emulator_matches_only_player_starts(self):
        j, start, end, _ = fixture()
        session = FakeSession(start)
        result = replay(j, start.save_snapshot(), session)
        self.assertEqual(
            "SOURCE_PLAYER_CHECKPOINTS_PROJECTED_AGREEMENT_UNCERTIFIED",
            result["status"], result)
        self.assertEqual([1, 2], [x["turn"] for x in result["player_checkpoints"]])
        self.assertEqual(1, len(result["enemy_checkpoint_candidates"]))
        self.assertEqual("DEFERRED_NO_PRE_ENEMY_EFFECT_BOUNDARY",
                         result["enemy_checkpoint_candidates"][0]["emulator_comparison"])
        self.assertEqual(["strike"], start.stepped)
        self.assertEqual(["end"], end.stepped)
        self.assertEqual(1, session.loaded)
        self.assertEqual(2, result["emulator_canonical_actions_stepped"])
        self.assertEqual("NOT_TESTED", result["native_full_state_parity"])

    def test_initial_mapping_requires_ordered_card_identity(self):
        j, start, _, _ = fixture()
        native = copy.deepcopy(j["source_turn_checkpoint_candidates"][0]["source_state"])
        initial = start.snapshot
        walk = derive_crosswalk(native, initial)
        self.assertEqual({"0": 100, "1": 101}, walk["cards"])
        self.assertEqual({"creature:1": 5}, walk["enemies"])
        native["combat"]["draw_pile"][0]["model_id"] = "SURVIVOR"
        with self.assertRaisesRegex(ValueError, "identity mismatch"):
            derive_crosswalk(native, initial)
        native = copy.deepcopy(j["source_turn_checkpoint_candidates"][0]["source_state"])
        native["combat"]["hand"].append(copy.deepcopy(native["combat"]["hand"][0]))
        with self.assertRaisesRegex(ValueError, "size mismatch"):
            derive_crosswalk(native, initial)

    def test_first_difference_reports_ordered_zone_path(self):
        j, start, _, _ = fixture()
        source_first = j["source_turn_checkpoint_candidates"][0]["source_state"]
        mapping = derive_crosswalk(source_first, start.snapshot)
        bad = copy.deepcopy(start.snapshot)
        bad["state"]["world"]["combat"]["hand"] = [101]
        bad["state"]["world"]["combat"]["draw_pile"] = [100]
        first = first_difference(source_first, bad, mapping)
        self.assertEqual("$.combat.hand", first["path"])
        self.assertEqual("ORDERED_ZONE_DIFFERENCE", first["reason"])

    def test_later_strength_difference_is_first_source_mismatch(self):
        j, start, _, _ = fixture()
        later = j["source_turn_checkpoint_candidates"][2]["source_state"]
        later["combat"]["creatures"][1]["powers"] = [
            {"model_id": "STRENGTH_POWER", "amount": 7}]
        result = replay(j, start.save_snapshot(), FakeSession(start))
        self.assertEqual("SOURCE_PLAYER_CHECKPOINT_DIFFERENCE_UNCERTIFIED",
                         result["status"])
        self.assertEqual(1, len(result["player_checkpoints"]))
        self.assertEqual("$.combat.creatures.creature:1.strength",
                         result["first_difference"]["difference"]["path"])

    def test_wrong_seed_or_unmapped_enemy_blocks_before_any_step(self):
        j, start, _, _ = fixture()
        wrong = copy.deepcopy(start.snapshot)
        wrong["state"]["run_seed"] = "OTHER"
        self.assertEqual("BLOCKED_EMULATOR_BOOTSTRAP",
                         replay(j, json.dumps(wrong), FakeSession(start))["status"])
        j["source_turn_checkpoint_candidates"][0]["source_state"][
            "combat"]["creatures"][1]["model_id"] = "ANOTHER"
        result = replay(j, start.save_snapshot(), FakeSession(start))
        self.assertEqual("BLOCKED_EMULATOR_BOOTSTRAP", result["status"])
        self.assertEqual([], start.stepped)

    def test_source_rng_window_requires_unchanged_bracket(self):
        def row(i, value):
            return {"ordinal": i, "type": "native_rng_stream_checkpoint",
                    "data": {"streams": [
                        {"stream_id": "run.shuffle", "cursor": {
                            "Counter": value, "State0": 1, "State1": 2,
                            "State2": 3, "State3": 4}}]}}
        rng, status = _native_rng_window([row(1, 4), row(4, 4)], 2)
        self.assertIsNone(rng)
        self.assertEqual("SOURCE_RNG_STREAM_INVENTORY_INCOMPLETE", status)
        rng, status = _native_rng_window([row(1, 4), row(4, 5)], 2)
        self.assertIsNone(rng)
        self.assertEqual("SOURCE_RNG_BRACKET_CHANGED", status)

    def test_exact_xoshiro_words_and_counter_are_compared(self):
        names = ["run.stream_" + str(i) for i in range(15)]
        cursors = {
            name: {"Counter": 7, "State0": 11, "State1": 13,
                   "State2": 17, "State3": 19}
            for name in names}
        emu = {"state": {"native_run_rng": {"streams": [
            {"stream_id": name,
             "codec": "sts2-v111-xoshiro256-starstar-v1",
             "state_bytes": base64.b64encode(
                 struct.pack("<QQQQ", 11, 13, 17, 19)).decode("ascii"),
             "call_count": 7}
            for name in names]}}}
        self.assertEqual(
            "SOURCE_BRACKETED_15_STREAMS_AGREE",
            _check_rng(cursors, emu)["status"])
        emu["state"]["native_run_rng"]["streams"][4]["call_count"] = 8
        delta = _check_rng(cursors, emu)
        self.assertEqual("RNG_STREAM_DIFFERENCE", delta["status"])
        self.assertEqual(names[4], delta["stream_id"])

    def test_nested_choice_requires_exactly_one_emulator_selection(self):
        crosswalk = {"cards": {"10": 101}}
        native = [{"containing_executor_action_id": 17,
                   "choice_type": "CombatCard",
                   "selected_combat_card_ids": [10]}]
        legal = [{"kind": "select_cards", "actionId": "discard-101",
                  "payloadJson": json.dumps({"CardInstanceIds": [101]})}]
        action, error = _selected_choice(native, 17, crosswalk, legal)
        self.assertEqual(("discard-101", None), (action, error))
        action, error = _selected_choice(native, 17, crosswalk, [])
        self.assertIsNone(action)
        self.assertEqual("NESTED_CHOICE_NOT_UNIQUELY_LEGAL", error)
        forged = copy.deepcopy(native)
        forged[0]["selected_combat_card_ids"] = [11]
        action, error = _selected_choice(forged, 17, crosswalk, legal)
        self.assertEqual("NESTED_CHOICE_CARD_UNMAPPED", error)


if __name__ == "__main__":
    unittest.main()
