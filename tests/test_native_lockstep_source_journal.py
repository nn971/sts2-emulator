"""Source-only action journal: exact native identity without emulator parity claims."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_lockstep_source_journal import build


def source_capture():
    action = {"kind": "play_card",
              "payload": {"card_instance_id": 7, "target_id": None}}
    pre = {"player": {"hp": 50}, "combat": {"energy": 3}}
    exec_state = {"player": {"hp": 50}, "combat": {"energy": 2}}
    final = {"player": {"hp": 50}, "combat": {"energy": 1}}
    rows = [
        {"type": "header", "data": {
            "schema": "native-action-envelope-v1",
            "game_build": "v0.111.0;sha256=pin",
            "run_id": "source-run"}},
        {"type": "native_run_identity", "data": {
            "seed": "6JK8MD0VQQTA", "ascension": 10,
            "source": "RunState.Rng.StringSeed",
            "rng_draws_performed": 0}},
        {"type": "native_input_request",
         "native_type": "PlayCardAction",
         "canonical_action": action, "state": pre,
         "data": {
             "input_request_id": 1,
             "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
             "native_acceptance": "UNVERIFIED",
             "human_origin": "UNVERIFIED", "input_injected": False}},
        {"type": "action_started", "action_id": 1,
         "native_type": "PlayCardAction",
         "canonical_action": action,
         "state": exec_state,
         "data": {"input_request_id": 1,
                  "request_execution_link": "SAME_NATIVE_ACTION_OBJECT",
                  "human_origin": "UNVERIFIED"}},
        {"type": "native_input_outcome", "native_type": "PlayCardAction",
         "data": {"input_request_id": 1,
                  "native_action_state": "Executing",
                  "native_action_id": 7,
                  "queue_result":
                      "ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN",
                  "source": "ActionQueueSynchronizer.RequestEnqueue.postfix",
                  "human_origin": "UNVERIFIED",
                  "legal_menu_verified": False}},
        {"type": "native_hand_menu", "action_id": 1, "data": {
            "menu_id": 1, "mode": "SimpleSelect",
            "source_model_id": "SURVIVOR",
            "min_select": 1, "max_select": 1,
            "require_manual_confirmation": False,
            "native_hand_holder_count": 2, "missing_card_ids": 0,
            "options": [
                {"combat_card_id": 8, "card_model_id": "DEFEND_SILENT",
                 "ui_hand_index": 0},
                {"combat_card_id": 9, "card_model_id": "STRIKE_SILENT",
                 "ui_hand_index": 1}],
            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "selection_owner_id": 1, "selection_choice_id": 4,
            "predicate_re_evaluated_by_inspector": False,
            "source": "NPlayerHand.SelectCards.postfix_after_native_visibility_update",
            "emulator_parity": "NOT_TESTED"}},
        {"type": "native_hand_menu_closed", "action_id": 1, "data": {
            "menu_id": 1, "final_revision": 1,
            "selection_owner_id": 1, "selection_choice_id": 4,
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "native_reason": "AfterCardsSelected",
            "source": "NPlayerHand.AfterCardsSelected.postfix",
            "native_choice_result_verified": False,
            "complete_menu_history": "NOT_VERIFIED"}},
        {"type": "native_player_choice", "action_id": 1, "data": {
            "owner_id": 1, "choice_id": 4, "choice_type": "CombatCard",
            "selected_combat_card_ids": [9],
            "selection_projection": "EXACT_COMBAT_CARD_IDS",
            "origin": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
            "accepted_choice_menu": "NOT_CAPTURED",
            "human_origin": "UNVERIFIED"}},
        {"type": "action_completed", "action_id": 1, "state": final},
        {"type": "terminal", "data": {
            "status": "OBSERVED", "open_actions": [],
            "native_game_parity": "NOT_TESTED"}},
    ]
    for i, row in enumerate(rows, 1):
        row["ordinal"] = i
    return rows


class NativeSourceJournalTests(unittest.TestCase):
    def test_request_can_execute_before_queue_postfix_and_choice_is_bound(self):
        report = build(source_capture())
        self.assertEqual("SOURCE_ACTION_CHRONOLOGY_OBSERVED", report["status"])
        self.assertEqual("6JK8MD0VQQTA", report["run_seed"])
        self.assertEqual(10, report["ascension"])
        self.assertEqual(1, len(report["source_linked_commands"]))
        cmd = report["source_linked_commands"][0]
        self.assertEqual(1, cmd["input_request_id"])
        self.assertLess(cmd["request_ordinal"], cmd["executor_start_ordinal"])
        self.assertGreater(cmd["native_queue_return_ordinal"],
                           cmd["executor_start_ordinal"])
        self.assertEqual(3, cmd["pre_request_state"]["combat"]["energy"])
        self.assertEqual(1, cmd["post_execution_state"]["combat"]["energy"])
        self.assertFalse(cmd["emulator_applied"])
        self.assertEqual("SOURCE_OBSERVED_DIFFERENT",
                         cmd["request_to_execution_state_agreement"])
        self.assertIsNone(cmd["last_native_completion_before_request"])
        self.assertEqual("UNOBSERVED",
                         cmd["last_native_completion_to_request_state_agreement"])
        self.assertEqual("NATIVE_CALLBACKS_NOT_FULL_LEGAL_MENU",
                         cmd["source_boundary"])
        observed = report["source_executor_observed_transitions"]
        self.assertEqual(1, len(observed))
        self.assertEqual(1, observed[0]["executor_action_id"])
        self.assertEqual({"player": {"hp": 50}, "combat": {"energy": 2}},
                         observed[0]["pre_execution_state"])
        self.assertEqual({"player": {"hp": 50}, "combat": {"energy": 1}},
                         observed[0]["post_execution_state"])
        self.assertEqual("NATIVE_EXECUTOR_CALLBACKS_NOT_SETTLED",
                         observed[0]["source_boundary"])
        self.assertEqual("SOURCE_OBSERVED",
                         cmd["native_executor_object_identity"])
        self.assertEqual("UNVERIFIED", cmd["originating_human_input"])
        self.assertEqual(1, len(report["native_choice_results"]))
        choice = report["native_choice_results"][0]
        self.assertEqual(4, choice["choice_id"])
        self.assertEqual(1, choice["containing_executor_action_id"])
        self.assertEqual(1, choice["native_initial_hand_menu_id"])
        self.assertEqual([9], choice["selected_combat_card_ids"])
        self.assertEqual("NOT_EXECUTED", report["emulator_replay"])
        self.assertEqual("NOT_TESTED", report["emulator_parity"])

    def test_deferred_or_canceled_request_stays_unexecuted(self):
        rows = source_capture()
        request = copy.deepcopy(rows[2])
        request["data"]["input_request_id"] = 2
        outcome = copy.deepcopy(rows[4])
        outcome["data"].update(input_request_id=2, native_action_state="None",
                               native_action_id=None,
                               queue_result="DEFERRED_OR_UNENQUEUED")
        rows[-1:-1] = [request, outcome]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = build(rows)
        self.assertEqual("SOURCE_ACTION_CHRONOLOGY_OBSERVED", report["status"])
        self.assertEqual(1, len(report["source_linked_commands"]))
        pending = report["source_requests_without_executor_start"]
        self.assertEqual(1, len(pending))
        self.assertEqual(2, pending[0]["input_request_id"])
        self.assertEqual("DEFERRED_OR_UNENQUEUED",
                         pending[0]["native_queue_return"])
        self.assertFalse(pending[0]["execution_observed"])

    def test_unlinked_executor_action_is_partial_without_fabricated_request(self):
        rows = source_capture()
        action = {"kind": "play_card",
                  "payload": {"card_instance_id": 9, "target_id": None}}
        rows[-1:-1] = [
            {"type": "action_started", "action_id": 2,
             "native_type": "PlayCardAction",
             "canonical_action": action, "state": {}},
            {"type": "action_completed", "action_id": 2, "state": {}},
        ]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = build(rows)
        self.assertEqual("SOURCE_INPUT_COVERAGE_PARTIAL", report["status"])
        self.assertEqual(1, len(report["source_linked_commands"]))
        self.assertEqual(1, len(report[
            "source_unlinked_canonical_executor_actions"]))
        self.assertEqual("NATIVE_SOURCE_REQUEST_LINK_MISSING",
                         report["source_unlinked_canonical_executor_actions"][0][
                             "reason"])

    def test_automatic_executor_transition_is_preserved_without_human_command(self):
        rows = source_capture()
        state = {"player": {"hp": 50}, "combat": {"energy": 1}}
        rows[-1:-1] = [
            {"type": "action_started", "action_id": 2,
             "native_type": "ReadyToBeginEnemyTurnAction",
             "canonical_action": None, "state": state},
            {"type": "action_completed", "action_id": 2, "state": state},
        ]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = build(rows)
        self.assertEqual("SOURCE_ACTION_CHRONOLOGY_OBSERVED",
                         report["status"])
        self.assertEqual(1, len(report["source_linked_commands"]))
        self.assertEqual(2, len(report[
            "source_executor_observed_transitions"]))
        auto = report["source_executor_observed_transitions"][1]
        self.assertEqual(2, auto["executor_action_id"])
        self.assertIsNone(auto["canonical_action_candidate"])
        self.assertFalse(auto["emulator_applied"])
        self.assertEqual("NATIVE_EXECUTOR_CALLBACKS_NOT_SETTLED",
                         auto["source_boundary"])

    def test_missing_true_run_identity_remains_explicit(self):
        rows = [r for r in source_capture() if r["type"] != "native_run_identity"]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = build(rows)
        self.assertEqual("SOURCE_IDENTITY_MISSING", report["status"])
        self.assertIsNone(report["run_seed"])

    def test_invalid_source_sidecar_never_produces_ready_journal(self):
        rows = source_capture()
        rows[-1]["data"]["status"] = "INCOMPLETE_CAPTURE"
        report = build(rows)
        self.assertEqual("INVALID_SOURCE_CAPTURE", report["status"])
        self.assertEqual([], report["source_linked_commands"])
        self.assertEqual("NOT_EXECUTED", report["emulator_replay"])
        rows = source_capture()
        rows[-1]["ordinal"] = 100
        self.assertEqual("INVALID_SOURCE_CAPTURE", build(rows)["status"])


if __name__ == "__main__":
    unittest.main()
