"""Persistent JSONL shadow contract: real stepping with no false native parity."""
import copy
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_lockstep_shadow import check_inputs, run_shadow


SOURCE_SEED = "6JK8MD0VQQTA"


def snapshot(seed=SOURCE_SEED, ascension=10, phase="Combat"):
    return json.dumps({
        "schema": "sts2-exact-snapshot-v1",
        "game_build": "v111-fixture",
        "state": {
            "run_seed": seed, "ascension": ascension, "phase": phase,
            "world": {"combat": {} if phase == "Combat" else None},
        },
    })


def crosswalk():
    return {"schema": "native-emulator-id-crosswalk-v1",
            "cards": {"7": 101}, "enemies": {"creature:2": 5}}


def native_state(energy=3, hp=50, hand=True):
    return {
        "player": {"hp": hp, "max_hp": 70, "block": 0},
        "combat": {"energy": energy, "turn": 1,
                   "hand": ([{"instance_id": 7, "model_id": "DEFEND_SILENT"}]
                            if hand else [])},
    }


def emulator_state(energy=3, hp=50, hand=True):
    return {"hp": hp, "max_hp": 70,
            "combat": {
                "energy": energy, "player_block": 0, "turn": 1,
                "hand": ([{"instance_id": 101, "card_id": "Defend_Silent"}]
                         if hand else [])}}


def journal(actions=None):
    commands = actions or [{
        "input_request_id": 1,
        "executor_action_id": 17,
        "request_ordinal": 3,
        "executor_start_ordinal": 4,
        "executor_complete_ordinal": 9,
        "canonical_action": {
            "kind": "play_card",
            "payload": {"card_instance_id": 7, "target_id": None}},
        "pre_request_state": native_state(3),
        "post_execution_state": native_state(2, hand=False),
    }]
    return {
        "schema": "native-source-action-journal-v1",
        "status": "SOURCE_ACTION_CHRONOLOGY_OBSERVED",
        "source_audit": "CALLBACK_TIMELINE_CONSISTENT",
        "run_seed": SOURCE_SEED, "ascension": 10,
        "source_linked_commands": commands,
        "source_unlinked_canonical_executor_actions": [],
        "source_requests_without_executor_start": [],
        "native_choice_results": [],
        "errors": [],
    }


class FakeState:
    def __init__(self, observed, legal, next_state=None):
        self._observation = observed
        self._legal = legal
        self._next = next_state
        self.step_calls = []

    def observe(self):
        return copy.deepcopy(self._observation)

    def actions(self):
        return copy.deepcopy(self._legal)

    def step(self, action_id):
        self.step_calls.append(action_id)
        if self._next is None:
            raise RuntimeError("Unexpected extra step")
        return self._next


class FakeSession:
    def __init__(self, state):
        self.first = state
        self.loaded = []

    def load_snapshot(self, exact):
        self.loaded.append(exact)
        return self.first


def card_action(target=None):
    return [{"kind": "play_card", "actionId": "emu-action-1",
             "payloadJson": json.dumps({
                 "card_instance_id": 101, "target_enemy_id": target})}]


def setup(legal=None, after_energy=2):
    after = FakeState(emulator_state(after_energy, hand=False), [])
    before = FakeState(emulator_state(3), legal or card_action(), after)
    return FakeSession(before), before, after


class NativeShadowTests(unittest.TestCase):
    def test_malformed_journal_fails_closed_without_opening_session(self):
        report = run_shadow([], snapshot(), crosswalk(), None)
        self.assertEqual("BLOCKED_NATIVE_SOURCE", report["status"])
        self.assertEqual("NOT_TESTED", report["native_game_parity"])

    def test_missing_snapshot_blocks_without_starting_emulator(self):
        result = run_shadow(journal(), None, crosswalk(), None)
        self.assertEqual("BLOCKED_EMULATOR_BOOTSTRAP", result["status"])
        self.assertEqual(0, result["emulator_actions_stepped"])
        self.assertEqual("NOT_TESTED", result["native_game_parity"])

    def test_no_source_identity_or_incomplete_capture_is_not_replayed(self):
        j = journal()
        j["status"] = "SOURCE_INPUT_COVERAGE_PARTIAL"
        self.assertEqual("BLOCKED_NATIVE_SOURCE",
                         run_shadow(j, snapshot(), crosswalk(), None)["status"])
        j = journal()
        j["run_seed"] = None
        self.assertEqual("BLOCKED_NATIVE_IDENTITY",
                         run_shadow(j, snapshot(), crosswalk(), None)["status"])

    def test_emulator_bootstrap_must_match_native_run_and_combat(self):
        for s in (snapshot(seed="different"), snapshot(ascension=9),
                  snapshot(phase="MapChoice")):
            self.assertEqual(
                "BLOCKED_EMULATOR_IDENTITY",
                run_shadow(journal(), s, crosswalk(), None)["status"])
        self.assertEqual(
            "BLOCKED_ID_CROSSWALK",
            run_shadow(journal(), snapshot(), None, None)["status"])

    def test_source_card_and_target_ids_must_be_explicitly_mapped(self):
        session, first, _ = setup()
        j = journal()
        bad_mapping = crosswalk()
        bad_mapping["cards"] = {}
        result = run_shadow(j, snapshot(), bad_mapping, session)
        self.assertEqual("BLOCKED_VISIBLE_PRESTATE_ALIGNMENT", result["status"])
        self.assertEqual("CARD_CROSSWALK_INCOMPLETE",
                         result["first_observation"]["difference"]["reason"])
        self.assertEqual([], first.step_calls)

        mapping = crosswalk()
        mapping["enemies"] = {}
        cmd = copy.deepcopy(j)
        cmd["source_linked_commands"][0]["canonical_action"]["payload"]["target_id"] = "creature:2"
        # All visible state matches; target map is incomplete and no step occurs.
        result = run_shadow(cmd, snapshot(), mapping, session)
        self.assertEqual("BLOCKED_ACTION_ALIGNMENT", result["status"])
        self.assertEqual("NATIVE_TARGET_ID_UNMAPPED", result["reason"])

    def test_real_persistent_step_is_only_shadow_visible_evidence(self):
        session, first, _ = setup()
        result = run_shadow(journal(), snapshot(), crosswalk(), session)
        self.assertEqual("SHADOW_STEPPED_VISIBLE_ONLY", result["status"])
        self.assertEqual(1, len(session.loaded))
        self.assertEqual(["emu-action-1"], first.step_calls)
        self.assertEqual(1, result["emulator_actions_stepped"])
        self.assertTrue(result["transitions"][0]["visible_post_projection_agrees"])
        self.assertEqual("NOT_TESTED", result["native_game_parity"])
        self.assertEqual("NOT_TESTED", result["emulator_rng_alignment"])
        self.assertEqual("UNVERIFIED", result["native_boundary_settlement"])
        self.assertFalse(result["verified_native_state_import"])

    def test_prestate_mismatch_blocks_step_and_is_not_certified_divergence(self):
        session, first, _ = setup()
        j = journal()
        j["source_linked_commands"][0]["pre_request_state"]["player"]["hp"] = 47
        result = run_shadow(j, snapshot(), crosswalk(), session)
        self.assertEqual("BLOCKED_VISIBLE_PRESTATE_ALIGNMENT", result["status"])
        self.assertEqual("$.player.hp",
                         result["first_observation"]["difference"]["path"])
        self.assertEqual([], first.step_calls)
        self.assertEqual("NOT_TESTED", result["native_game_parity"])

    def test_legal_action_not_found_stops_without_guessing(self):
        session, first, _ = setup([{"kind": "end_turn", "actionId": "other",
                                   "payloadJson": "{}"}])
        result = run_shadow(journal(), snapshot(), crosswalk(), session)
        self.assertEqual("BLOCKED_ACTION_ALIGNMENT", result["status"])
        self.assertEqual("ACTION_NOT_IN_EMULATOR_LEGAL_MENU", result["reason"])
        self.assertEqual([], first.step_calls)

    def test_post_state_mismatch_is_qualified_not_verified_native_mismatch(self):
        session, first, _ = setup(after_energy=1)
        result = run_shadow(journal(), snapshot(), crosswalk(), session)
        self.assertEqual("SHADOW_VISIBLE_DIFFERENCE_UNCERTIFIED",
                         result["status"])
        self.assertEqual(1, result["emulator_actions_stepped"])
        self.assertEqual("$.combat.energy",
                         result["first_observation"]["difference"]["path"])
        self.assertEqual("NOT_TESTED", result["native_game_parity"])

    def test_survivor_choice_requires_separate_action_semantics(self):
        session, first, _ = setup()
        j = journal()
        j["native_choice_results"] = [{
            "containing_executor_action_id": 17,
            "choice_type": "CombatCard", "choice_id": 4,
            "selected_combat_card_ids": [9]}]
        result = run_shadow(j, snapshot(), crosswalk(), session)
        self.assertEqual("BLOCKED_NESTED_CHOICE_CONTINUATION", result["status"])
        self.assertEqual([], first.step_calls)
        self.assertEqual(4, result["first_observation"]["choice_ids"][0])

    def test_two_source_commands_advance_single_emulator_state(self):
        end = FakeState(emulator_state(1, hand=False), [])
        middle = FakeState(emulator_state(2, hand=False), [
            {"kind": "end_turn", "actionId": "emu-end",
             "payloadJson": "{}"}], end)
        initial = FakeState(emulator_state(3), card_action(), middle)
        session = FakeSession(initial)
        j = journal()
        j["source_linked_commands"].append({
            "input_request_id": 2,
            "executor_action_id": 18,
            "request_ordinal": 10,
            "executor_start_ordinal": 11,
            "executor_complete_ordinal": 12,
            "canonical_action": {"kind": "end_turn", "payload": {}},
            "pre_request_state": native_state(2, hand=False),
            "post_execution_state": native_state(1, hand=False),
        })
        result = run_shadow(j, snapshot(), crosswalk(), session)
        self.assertEqual("SHADOW_STEPPED_VISIBLE_ONLY", result["status"])
        self.assertEqual(2, result["emulator_actions_stepped"])
        self.assertEqual(["emu-action-1"], initial.step_calls)
        self.assertEqual(["emu-end"], middle.step_calls)
        self.assertEqual(1, len(session.loaded))


if __name__ == "__main__":
    unittest.main()
