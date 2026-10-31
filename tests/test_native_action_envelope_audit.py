"""Non-game regression tests for the all-action native capture sidecar."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_action_envelope_audit import audit, diff_scalars, infer_choice_discard


def sample():
    return [
        {"type": "header", "ordinal": 1,
         "data": {"schema": "native-action-envelope-v1"}},
        {"type": "history_baseline", "ordinal": 2,
         "data": {"existing_entry_count": 3}},
        {"type": "action_started", "ordinal": 3,
         "action_id": 1, "parent_action_id": None,
         "native_type": "PlayCardAction",
         "canonical_action": {"kind": "play_card", "payload": {"card_instance_id": 0}},
         "state": {"player": {"hp": 50, "block": 0},
                   "combat": {"energy": 3, "creatures": [],
                              "hand": [{"instance_id": 0}],
                              "draw_pile": [], "discard_pile": [], "exhaust_pile": []}}},
        {"type": "history_entry", "ordinal": 4, "action_id": 1,
         "data": {"history_index": 3, "missing_fields": [], "payload": {"amount": 1}}},
        {"type": "nested_choice_pause", "ordinal": 5, "action_id": 1,
         "state": {"player": {"hp": 50}, "combat": {"energy": 2}}},
        {"type": "action_started", "ordinal": 6, "action_id": 2,
         "parent_action_id": 1, "native_type": "AutomaticAction", "state": {}},
        {"type": "action_completed", "ordinal": 7, "action_id": 2,
         "state": {}},
        {"type": "action_completed", "ordinal": 8, "action_id": 1,
         "state": {"player": {"hp": 50, "block": 0},
                   "combat": {"energy": 2, "creatures": [],
                              "hand": [], "draw_pile": [],
                              "discard_pile": [{"instance_id": 0}],
                              "exhaust_pile": []}}},
        {"type": "terminal", "ordinal": 9, "data": {
            "status": "OBSERVED", "open_actions": [], "native_game_parity": "NOT_TESTED"}},
    ]


class EnvelopeAuditTests(unittest.TestCase):
    def test_disabled_and_installed_rng_hooks_remain_distinct(self):
        default = audit(sample())
        self.assertFalse(default["native_rng_hook_installed"])
        self.assertEqual("SOURCE_RNG_UNINSTRUMENTED",
                         default["native_rng_source_coverage"])
        records = sample()
        records[0]["data"]["native_rng_hook_installed"] = True
        records[0]["data"]["native_monster_ai_scope_installed"] = True
        installed = audit(records)
        self.assertTrue(installed["native_rng_hook_installed"])
        self.assertTrue(installed["native_monster_ai_scope_installed"])
        self.assertEqual("HOOK_INSTALLED_NO_SOURCE_CALLS_OBSERVED",
                         installed["native_rng_source_coverage"])
        self.assertEqual(0, installed["native_rng_source_calls"])
        self.assertEqual("NOT_TESTED", installed["exact_rng_parity"])

    def test_source_request_seed_and_choice_are_audited_without_acceptance_claim(self):
        records = sample()
        records[0]["data"]["native_input_hook_installed"] = True
        identity = {"type": "native_run_identity", "data": {
            "seed": "6JK8MD0VQQTA", "ascension": 10,
            "source": "RunState.Rng.StringSeed",
            "rng_draws_performed": 0,
            "emulator_run_identity_parity": "NOT_TESTED"}}
        request = {"type": "native_input_request",
            "native_type": "PlayCardAction",
            "canonical_action": records[2]["canonical_action"],
            "data": {"input_request_id": 1,
                "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
                "native_acceptance": "UNVERIFIED",
                "human_origin": "UNVERIFIED",
                "input_injected": False}}
        records[2]["data"] = {
            "input_request_id": 1,
            "request_execution_link": "SAME_NATIVE_ACTION_OBJECT",
            "human_origin": "UNVERIFIED"}
        selection = {"type": "native_player_choice",
            "action_id": 1,
            "data": {"owner_id": 1, "choice_id": 0,
                "choice_type": "CombatCard",
                "selected_combat_card_ids": [5],
                "selected_indexes": None,
                "selected_canonical_card_models": None,
                "selected_player_id": None,
                "selection_projection": "EXACT_COMBAT_CARD_IDS",
                "origin": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
                "accepted_choice_menu": "NOT_CAPTURED",
                "human_origin": "UNVERIFIED"}}
        records.insert(1, identity)
        records.insert(3, request)
        records.insert(7, selection)
        for i, record in enumerate(records):
            record["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertTrue(result["native_input_hook_installed"])
        self.assertEqual("6JK8MD0VQQTA",
                         result["native_run_identity"]["seed"])
        self.assertEqual(1, result["source_request_execution_links"])
        self.assertEqual(1, len(result["native_input_requests"]))
        self.assertEqual("UNVERIFIED",
                         result["native_input_requests"][0]["acceptance"])
        self.assertEqual([5],
                         result["native_player_choices"][0]["selected_combat_card_ids"])
        self.assertEqual("NOT_TESTED", result["native_game_parity"])

        forged = copy.deepcopy(records)
        forged[4]["data"]["input_request_id"] = 999
        self.assertEqual("INCOMPLETE_CAPTURE", audit(forged)["status"])
        forged = copy.deepcopy(records)
        forged[7]["data"]["accepted_choice_menu"] = "EXACT"
        self.assertEqual("INCOMPLETE_CAPTURE", audit(forged)["status"])

    def test_canceled_or_deferred_request_never_becomes_accepted_by_auditor(self):
        records = sample()
        request = {"type": "native_input_request", "native_type": "PlayCardAction",
            "canonical_action": records[2]["canonical_action"],
            "data": {"input_request_id": 1,
                "source": "ActionQueueSynchronizer.RequestEnqueue.prefix",
                "native_acceptance": "UNVERIFIED",
                "human_origin": "UNVERIFIED",
                "input_injected": False}}
        records.insert(2, request)
        for i, row in enumerate(records):
            row["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(0, result["source_request_execution_links"])
        self.assertEqual("UNVERIFIED",
                         result["native_input_requests"][0]["acceptance"])

    def test_native_request_postfix_distinguishes_queue_and_cancellation(self):
        records = sample()
        source = "ActionQueueSynchronizer.RequestEnqueue."
        request = {"type": "native_input_request",
            "native_type": "PlayCardAction",
            "canonical_action": records[2]["canonical_action"],
            "data": {"input_request_id": 1,
                "source": source + "prefix",
                "native_acceptance": "UNVERIFIED",
                "human_origin": "UNVERIFIED", "input_injected": False}}
        accepted = {"type": "native_input_outcome",
            "native_type": "PlayCardAction",
            "data": {"input_request_id": 1,
                "native_action_state": "Executing", "native_action_id": 8,
                "queue_result": "ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN",
                "source": source + "postfix",
                "human_origin": "UNVERIFIED",
                "legal_menu_verified": False}}
        records.insert(2, request)
        records[3]["data"] = {
            "input_request_id": 1,
            "request_execution_link": "SAME_NATIVE_ACTION_OBJECT",
            "human_origin": "UNVERIFIED"}
        records.insert(4, accepted)
        for i, row in enumerate(records):
            row["ordinal"] = i + 1
        report = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual(1, report["source_request_execution_links"])
        self.assertEqual(1, len(report["native_input_outcomes"]))
        self.assertFalse(report["native_input_outcomes"][0]["menu_verified"])
        self.assertEqual("ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN",
                         report["native_input_outcomes"][0]["queue_result"])

        canceled = copy.deepcopy(records)
        canceled[4]["data"].update(
            native_action_state="Canceled", native_action_id=8,
            queue_result="CANCELED_AT_RETURN")
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT",
                         audit(canceled)["status"])
        # A state of None with no queue ID has no acceptance evidence.
        deferred = copy.deepcopy(records)
        deferred[4]["data"].update(
            native_action_state="None", native_action_id=None,
            queue_result="DEFERRED_OR_UNENQUEUED")
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT",
                         audit(deferred)["status"])
        forged = copy.deepcopy(records)
        forged[4]["data"].update(
            native_action_state="None", native_action_id=None,
            queue_result="ASSIGNED_QUEUE_ID_AND_NOT_CANCELED_AT_RETURN")
        self.assertEqual("INCOMPLETE_CAPTURE", audit(forged)["status"])

    def test_native_hand_menu_is_ui_evidence_without_choice_id_reconstruction(self):
        rows = sample()
        menu = {"type": "native_hand_menu", "action_id": 1, "data": {
            "menu_id": 1, "mode": "SimpleSelect",
            "source_model_id": "SURVIVOR",
            "min_select": 1, "max_select": 1,
            "require_manual_confirmation": False,
            "native_hand_holder_count": 4, "missing_card_ids": 0,
            "options": [
                {"combat_card_id": 7, "card_model_id": "DEFEND_SILENT",
                 "ui_hand_index": 0},
                {"combat_card_id": 9, "card_model_id": "STRIKE_SILENT",
                 "ui_hand_index": 2}],
            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            "selection_id_binding": "UNVERIFIED",
            "predicate_re_evaluated_by_inspector": False,
            "source": "NPlayerHand.SelectCards.postfix_after_native_visibility_update",
            "emulator_parity": "NOT_TESTED"}}
        rows.insert(5, menu)
        for i, r in enumerate(rows):
            r["ordinal"] = i + 1
        result = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, len(result["native_hand_menus"]))
        self.assertEqual([7, 9], [o["combat_card_id"] for o in
                         result["native_hand_menus"][0]["options"]])
        self.assertEqual("UNVERIFIED",
                         result["native_hand_menus"][0]["choice_id_binding"])
        self.assertEqual("NOT_TESTED", result["native_game_parity"])
        forged = copy.deepcopy(rows)
        forged[5]["data"]["predicate_re_evaluated_by_inspector"] = True
        self.assertEqual("INCOMPLETE_CAPTURE", audit(forged)["status"])
        duplicate = copy.deepcopy(rows)
        duplicate[5]["data"]["options"][1]["combat_card_id"] = 7
        self.assertEqual("INCOMPLETE_CAPTURE", audit(duplicate)["status"])
        missing = copy.deepcopy(rows)
        missing[5]["data"]["missing_card_ids"] = 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(missing)["status"])

    def test_native_ui_card_interaction_is_before_optional_manual_acceptance(self):
        rows = sample()
        ui = {"type": "native_ui_play_begin", "data": {
            "combat_card_id": 19, "card_model_id": "SHIV",
            "started_via_shortcut": True,
            "source": "NPlayerHand.StartCardPlay.prefix",
            "native_ui_gate": "SOURCE_CALLSITE_PASSED",
            "manual_play_accepted": "NOT_TESTED",
            "queue_acceptance": "NOT_TESTED",
            "human_origin": "UNVERIFIED",
            "full_legal_menu": "NOT_TESTED",
            "emulator_parity": "NOT_TESTED"}}
        rows.insert(3, ui)
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        result = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, len(result["native_ui_play_begins"]))
        self.assertTrue(
            result["native_ui_play_begins"][0]["started_via_shortcut"])
        forged = copy.deepcopy(rows)
        forged[3]["data"]["full_legal_menu"] = "VERIFIED"
        self.assertEqual("INCOMPLETE_CAPTURE", audit(forged)["status"])

    def test_source_reserved_hand_choice_id_links_initial_menu_to_selection(self):
        rows = sample()
        menu = {"type": "native_hand_menu", "action_id": 1, "data": {
            "menu_id": 1, "mode": "SimpleSelect",
            "source_model_id": "SURVIVOR",
            "min_select": 1, "max_select": 1,
            "require_manual_confirmation": False,
            "native_hand_holder_count": 2, "missing_card_ids": 0,
            "options": [
                {"combat_card_id": 7, "card_model_id": "DEFEND_SILENT",
                 "ui_hand_index": 0},
                {"combat_card_id": 9, "card_model_id": "STRIKE_SILENT",
                 "ui_hand_index": 1}],
            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "selection_owner_id": 1,
            "selection_choice_id": 4,
            "predicate_re_evaluated_by_inspector": False,
            "source": "NPlayerHand.SelectCards.postfix_after_native_visibility_update",
            "emulator_parity": "NOT_TESTED"}}
        selection = {"type": "native_player_choice", "action_id": 1, "data": {
            "owner_id": 1, "choice_id": 4, "choice_type": "CombatCard",
            "selected_combat_card_ids": [9],
            "selection_projection": "EXACT_COMBAT_CARD_IDS",
            "origin": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
            "accepted_choice_menu": "NOT_CAPTURED",
            "human_origin": "UNVERIFIED"}}
        rows.insert(5, menu)
        rows.insert(6, selection)
        for i, r in enumerate(rows, 1):
            r["ordinal"] = i
        result = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual("SOURCE_ASYNC_FLOW_RESERVED_ID",
                         result["native_hand_menus"][0]["choice_id_binding"])
        self.assertEqual(4, result["native_hand_menus"][0]["choice_id"])
        matches = result["native_hand_menu_choice_matches"]
        self.assertEqual(1, len(matches))
        self.assertEqual(1, matches[0]["menu_id"])
        self.assertTrue(matches[0]["selection_in_initial_visible_options"])
        self.assertEqual("NOT_VERIFIED", matches[0]["complete_menu_history"])

        wrong_id = copy.deepcopy(rows)
        wrong_id[5]["data"]["selection_choice_id"] = 5
        result = audit(wrong_id)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual([], result["native_hand_menu_choice_matches"])

        ambiguous = copy.deepcopy(rows)
        ambiguous[5]["data"]["selection_id_binding"] = "AMBIGUOUS_SOURCE_RESERVATION"
        self.assertEqual("INCOMPLETE_CAPTURE", audit(ambiguous)["status"])

        changed = copy.deepcopy(rows)
        changed[6]["data"]["selected_combat_card_ids"] = [12]
        result = audit(changed)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertFalse(result["native_hand_menu_choice_matches"][0][
            "selection_in_initial_visible_options"])

        duplicated = copy.deepcopy(rows)
        second = copy.deepcopy(menu)
        second["data"]["menu_id"] = 2
        duplicated.insert(6, second)
        for i, r in enumerate(duplicated, 1):
            r["ordinal"] = i
        self.assertEqual("INCOMPLETE_CAPTURE", audit(duplicated)["status"])

    def test_source_menu_lifetime_revisions_and_peek_never_invent_eligibility(self):
        rows = sample()
        original = {"type": "native_hand_menu", "action_id": 1, "data": {
            "menu_id": 1, "mode": "SimpleSelect",
            "source_model_id": "SURVIVOR",
            "min_select": 1, "max_select": 1,
            "require_manual_confirmation": False,
            "native_hand_holder_count": 2, "missing_card_ids": 0,
            "options": [
                {"combat_card_id": 7, "card_model_id": "DEFEND_SILENT",
                 "ui_hand_index": 0}],
            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "selection_owner_id": 1, "selection_choice_id": 4,
            "predicate_re_evaluated_by_inspector": False,
            "source": "NPlayerHand.SelectCards.postfix_after_native_visibility_update",
            "emulator_parity": "NOT_TESTED"}}
        def revision(number, option_ids, peek=False):
            options = [
                {"combat_card_id": cid,
                 "card_model_id": "STRIKE_SILENT" if cid == 9 else "DEFEND_SILENT",
                 "ui_hand_index": i} for i, cid in enumerate(option_ids)]
            return {"type": "native_hand_menu_revision", "action_id": 1,
                "data": {
                    "menu_id": 1, "revision": number,
                    "selection_owner_id": 1,
                    "selection_choice_id": 4,
                    "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
                    "mode": "SimpleSelect", "peek_mode": peek,
                    "native_hand_holder_count": 2,
                    "missing_card_ids": 0, "options": options,
                    "coverage": ("NATIVE_PEEK_VIEW_NOT_SELECTABLE" if peek
                        else "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS"),
                    "source": "NPlayerHand.UpdateSelectModeCardVisibility.postfix",
                    "predicate_re_evaluated_by_inspector": False,
                    "complete_menu_history": "NOT_VERIFIED",
                    "emulator_parity": "NOT_TESTED"}}
        closed = {"type": "native_hand_menu_closed", "action_id": 1,
                  "data": {"menu_id": 1, "final_revision": 4,
                    "selection_owner_id": 1, "selection_choice_id": 4,
                    "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
                    "native_reason": "AfterCardsSelected",
                    "source": "NPlayerHand.AfterCardsSelected.postfix",
                    "native_choice_result_verified": False,
                    "complete_menu_history": "NOT_VERIFIED"}}
        selection = {"type": "native_player_choice", "action_id": 1, "data": {
            "owner_id": 1, "choice_id": 4,
            "choice_type": "CombatCard",
            "selected_combat_card_ids": [9],
            "selection_projection": "EXACT_COMBAT_CARD_IDS",
            "origin": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
            "accepted_choice_menu": "NOT_CAPTURED",
            "human_origin": "UNVERIFIED"}}
        # The original visible offer contained only 7; after state change it
        # contains 9. Native peek never establishes a selectable option.
        rows[5:5] = [original, revision(2, [9]), revision(3, [], True),
                     revision(4, [9]), closed, selection]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        report = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual(3, len(report["native_hand_menu_revisions"]))
        self.assertEqual(1, len(report["native_hand_menu_closures"]))
        self.assertEqual([], report["native_hand_menu_open_lifetimes"])
        self.assertEqual("NATIVE_PEEK_VIEW_NOT_SELECTABLE",
                         report["native_hand_menu_revisions"][1]["coverage"])
        link = report["native_hand_menu_choice_matches"][0]
        self.assertFalse(link["selection_in_initial_visible_options"])
        self.assertTrue(link["selection_in_latest_captured_options"])
        self.assertTrue(link["native_hand_menu_closed_before_choice"])
        self.assertEqual(4, link["latest_captured_revision"])
        self.assertEqual("SOURCE_REVISIONS_OBSERVED",
                         link["selection_menu_revalidation"])
        self.assertEqual("NOT_VERIFIED", link["complete_menu_history"])

        altered = copy.deepcopy(rows)
        altered[7]["data"]["options"] = [
            {"combat_card_id": 9, "card_model_id": "STRIKE_SILENT",
             "ui_hand_index": 0}]
        self.assertEqual("INCOMPLETE_CAPTURE", audit(altered)["status"])
        altered = copy.deepcopy(rows)
        altered[8]["data"]["revision"] = 7
        self.assertEqual("INCOMPLETE_CAPTURE", audit(altered)["status"])
        altered = copy.deepcopy(rows)
        altered[9]["data"]["final_revision"] = 3
        self.assertEqual("INCOMPLETE_CAPTURE", audit(altered)["status"])
        altered = copy.deepcopy(rows)
        altered[9]["data"]["selection_choice_id"] = 5
        self.assertEqual("INCOMPLETE_CAPTURE", audit(altered)["status"])

    def test_source_peek_mode_has_no_selectable_options_at_choice(self):
        rows = sample()
        initial = {"type": "native_hand_menu", "data": {
            "menu_id": 1, "mode": "SimpleSelect",
            "source_model_id": "SURVIVOR", "min_select": 1, "max_select": 1,
            "require_manual_confirmation": False,
            "native_hand_holder_count": 1, "missing_card_ids": 0,
            "options": [{"combat_card_id": 7, "card_model_id": "DEFEND_SILENT",
                         "ui_hand_index": 0}],
            "coverage": "SOURCE_NATIVE_UI_VISIBLE_HAND_OPTIONS",
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "selection_owner_id": 1, "selection_choice_id": 4,
            "predicate_re_evaluated_by_inspector": False,
            "source": "NPlayerHand.SelectCards.postfix_after_native_visibility_update",
            "emulator_parity": "NOT_TESTED"}}
        peek = {"type": "native_hand_menu_revision", "data": {
            "menu_id": 1, "revision": 2,
            "selection_owner_id": 1, "selection_choice_id": 4,
            "selection_id_binding": "SOURCE_ASYNC_FLOW_RESERVED_ID",
            "mode": "SimpleSelect", "peek_mode": True,
            "native_hand_holder_count": 1, "missing_card_ids": 0,
            "options": [], "coverage": "NATIVE_PEEK_VIEW_NOT_SELECTABLE",
            "source": "NPlayerHand.UpdateSelectModeCardVisibility.postfix",
            "predicate_re_evaluated_by_inspector": False,
            "complete_menu_history": "NOT_VERIFIED",
            "emulator_parity": "NOT_TESTED"}}
        selection = {"type": "native_player_choice", "data": {
            "owner_id": 1, "choice_id": 4,
            "choice_type": "CombatCard", "selected_combat_card_ids": [7],
            "selection_projection": "EXACT_COMBAT_CARD_IDS",
            "origin": "PlayerChoiceSynchronizer.PlayerChoiceReceived",
            "accepted_choice_menu": "NOT_CAPTURED",
            "human_origin": "UNVERIFIED"}}
        rows[5:5] = [initial, peek, selection]
        for i, row in enumerate(rows, 1):
            row["ordinal"] = i
        result = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        link = result["native_hand_menu_choice_matches"][0]
        self.assertIsNone(link["selection_in_latest_captured_options"])
        self.assertTrue(link["latest_captured_peek_mode"])
        self.assertEqual([1], result["native_hand_menu_open_lifetimes"])

    def test_native_manual_play_result_does_not_prove_queue_acceptance(self):
        rows = sample()
        manual = {"type": "native_manual_play_result", "data": {
            "combat_card_id": 19, "card_model_id": "SHIV",
            "target_id": "creature:1", "returned_success": True,
            "source": "CardModel.TryManualPlay.postfix",
            "queue_acceptance": "UNVERIFIED",
            "human_origin": "UNVERIFIED",
            "emulator_parity": "NOT_TESTED"}}
        rows.insert(3, manual)
        for i, r in enumerate(rows):
            r["ordinal"] = i + 1
        result = audit(rows)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, len(result["native_manual_play_results"]))
        self.assertTrue(result["native_manual_play_results"][0]["returned_success"])
        self.assertEqual("UNVERIFIED",
                         result["native_manual_play_results"][0]["queue_acceptance"])
        forged = copy.deepcopy(rows)
        forged[3]["data"]["queue_acceptance"] = "VERIFIED"
        self.assertEqual("INCOMPLETE_CAPTURE", audit(forged)["status"])

    def test_valid_nested_action_window_and_measured_delta(self):
        report = audit(sample())
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual("NOT_TESTED", report["native_game_parity"])
        self.assertEqual(1, report["nested_choice_pauses"])
        self.assertEqual(2, len(report["actions"]))
        action = report["actions"][0]
        self.assertEqual({"before": 3, "after": 2},
                         action["observed_delta"]["energy"])
        self.assertEqual({"before": [0], "after": []},
                         action["observed_delta"]["zones"]["hand"])

    def test_true_action_resumption_and_terminal_cleanup_are_valid(self):
        records = sample()
        records.insert(5, {"type": "action_resumed", "action_id": 1,
                           "native_type": "PlayCardAction",
                           "state": {"combat": {"energy": 2}}})
        records.insert(-1, {"type": "history_reset", "data": {
            "previous_count": 5, "new_count": 0}})
        records[-1]["data"]["reason"] = "Combat ended"
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["action_resumptions"])
        self.assertEqual(1, result["terminal_history_clears"])

    def test_legacy_choice_resume_is_qualified_not_rewritten(self):
        records = sample()
        records.insert(5, {"type": "anomaly", "data": {
            "reason": "duplicate_action_start", "detail": "PlayCardAction"}})
        records.insert(-1, {"type": "history_reset", "data": {
            "previous_count": 5, "new_count": 0}})
        records[-1]["data"].update({"status": "INCOMPLETE_CAPTURE",
                                     "reason": "Combat ended"})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("LEGACY_CALLBACK_SEQUENCE_RECOVERABLE", result["status"])
        self.assertEqual("INCOMPLETE_CAPTURE", result["raw_terminal_status"])
        self.assertEqual(1, result["legacy_resume_candidates"])
        self.assertEqual(1, result["terminal_history_clears"])

    def test_duplicate_start_without_choice_cannot_be_recovered(self):
        records = sample()
        records.insert(3, {"type": "anomaly", "data": {
            "reason": "duplicate_action_start", "detail": "PlayCardAction"}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_history_reset_during_live_action_is_incomplete(self):
        records = sample()
        records.insert(4, {"type": "history_reset", "data": {
            "previous_count": 4, "new_count": 0}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_source_observed_strict_boundary_miss_preserves_executor_timeline(self):
        records = sample()
        records.insert(3, {"type": "strict_input_gap", "action_id": 1,
                           "data": {
                               "reason": "executor_action_without_unique_strict_decision",
                               "last_reported_gate": "WAITING_FOR_DECISION:CARD_PLAY_ANIMATION",
                               "decision_generation": 15,
                               "last_captured_generation": 14,
                               "provenance": "native_executor_callback_and_cached_frame_gate",
                               "human_input_origin_verified": False,
                               "legal_menu_reconstructed": False}})
        for i, row in enumerate(records):
            row["ordinal"] = i + 1
        report = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual(1, len(report["strict_input_gaps"]))
        gap = report["strict_input_gaps"][0]
        self.assertEqual(1, gap["action_id"])
        self.assertEqual(15, gap["decision_generation"])
        self.assertEqual("NOT_TESTED", report["native_game_parity"])
        records[3]["data"]["legal_menu_reconstructed"] = True
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_frame_hp_delta_records_enemy_damage_between_callbacks(self):
        records = sample()
        state1 = {"player": {"hp": 45}, "combat": {"energy": 2}}
        state2 = {"player": {"hp": 40}, "combat": {"energy": 2}}
        records.insert(-1, {"type": "frame_state", "state": state1,
                            "data": {"frame_index": 1, "state_hash": "a"}})
        records.insert(-1, {"type": "frame_state", "state": state2,
                            "data": {"frame_index": 2, "state_hash": "b"}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["observed_mechanic_indicators"]
                         ["player_hp_change"]["count"])

    def test_real_native_rng_source_boundary_is_audited(self):
        records = sample()
        records.insert(4, {"type": "native_rng_boundary", "action_id": 1,
            "native_type": "Rng.NextInt",
            "data": {"site_id": "combat.committed_intent.index_draw",
                     "stream_id": "run.monster_ai",
                     "owner_id": "creature:1",
                     "call_ordinal": 0, "operation_kind": "bounded_integer",
                     "bounded_count": 2, "selected_index": 1,
                     "cursor_before": {"Counter": 4},
                     "cursor_after": {"Counter": 5},
                     "source_version": "v0.111.0",
                     "origin": "source_primitive_hook",
                     "attributed": True,
                     "source_hook_observed": True}})
        for i, row in enumerate(records):
            row["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["native_rng_source_calls"])
        self.assertEqual("SOURCE_RNG_CALLS_OBSERVED",
                         result["native_rng_source_coverage"])

    def test_frame_inferred_rng_call_cannot_masquerade_as_source(self):
        records = sample()
        records.insert(-1, {"type": "native_rng_boundary",
            "data": {"origin": "rendered_frame_poll"}})
        for i, row in enumerate(records):
            row["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("INCOMPLETE_CAPTURE", result["status"])
        self.assertIn("Invalid native RNG source call", result["reason"])

    def test_missing_action_end_is_fail_closed(self):
        records = sample()
        records.pop(7)
        records[-1]["data"]["open_actions"] = [1]
        for i, x in enumerate(records):
            x["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_unmatched_action_end_is_rejected(self):
        records = sample()
        records[6]["action_id"] = 999
        self.assertIn("no action start", audit(records)["reason"])

    def test_nonmonotone_history_detected(self):
        records = sample()
        records[3]["data"]["history_index"] = 7
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_missing_observation_explicitly_incomplete(self):
        records = sample()
        records.insert(5, {"type": "observation_failure",
                           "data": {"stage": "before_action"}})
        for i, x in enumerate(records):
            x["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])

    def test_nested_discard_candidate_remains_explicitly_unverified(self):
        before = {"combat": {"hand": [
            {"instance_id": 2}, {"instance_id": 5}],
            "discard_pile": [{"instance_id": 0}]}}
        after = {"combat": {"hand": [{"instance_id": 5}],
            "discard_pile": [{"instance_id": 0}, {"instance_id": 2}]}}
        inference = infer_choice_discard(before, after)
        self.assertEqual(2, inference["candidate_discarded_card_instance_id"])
        self.assertFalse(inference["semantic_selection_verified"])
        self.assertEqual("INFERRED_FROM_CARD_ZONE_DIFFERENCE",
                         inference["provenance"])

    def test_observed_mechanics_never_claim_parity(self):
        result = audit(sample())
        indicators = result["observed_mechanic_indicators"]
        self.assertEqual(1, indicators["card_play"]["count"])
        self.assertEqual(1, indicators["energy_change"]["count"])
        self.assertEqual("UNOBSERVED", indicators["potion_history_entry"]["status"])
        self.assertEqual("NOT_TESTED", result["native_source_hook_order"])

    def test_action_and_frame_deltas_are_not_double_counted(self):
        records = sample()
        state1 = {"player": {"hp": 50}, "combat": {"energy": 3}}
        state2 = {"player": {"hp": 50}, "combat": {"energy": 2}}
        records.insert(-1, {"type": "frame_state", "state": state1,
                            "data": {"frame_index": 1, "state_hash": "a"}})
        records.insert(-1, {"type": "frame_state", "state": state2,
                            "data": {"frame_index": 2, "state_hash": "b"}})
        for i, entry in enumerate(records):
            entry["ordinal"] = i + 1
        result = audit(records)
        energy = result["observed_mechanic_indicators"]["energy_change"]
        self.assertEqual(1, energy["count"])
        self.assertEqual(1, energy["counts_by_observer"]["action"])
        self.assertEqual(1, energy["counts_by_observer"]["frame"])

    def test_effect_delta_is_independent_of_native_rng_seed(self):
        pre = {"player": {"hp": 50}, "combat": {"energy": 3, "creatures": [],
               "hand": [], "draw_pile": [], "discard_pile": [],
               "exhaust_pile": []}}
        post = copy.deepcopy(pre)
        post["combat"]["energy"] = 2
        self.assertEqual({"energy": {"before": 3, "after": 2}},
                         diff_scalars(pre, post))

    def test_observed_scalar_source_call_is_typed_and_audited(self):
        records = sample()
        records[0]["data"].update(
            native_rng_hook_installed=True,
            native_rng_double_hook_installed=True,
            native_rng_bool_hook_installed=True)
        records.insert(-1, {"type": "native_rng_scalar_boundary",
            "action_id": None,
            "data": {
                "site_id": "UNATTRIBUTED", "stream_id": "run.monster_ai",
                "owner_id": "run", "call_ordinal": 0,
                "operation_kind": "unit_double",
                "selected_double": 0.25, "selected_bool": None,
                "cursor_before": {"Counter": 4},
                "cursor_after": {"Counter": 5},
                "source_version": "v0.111.0",
                "origin": "source_primitive_hook",
                "attributed": False,
                "source_hook_observed": True
            }})
        for i, record in enumerate(records):
            record["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["native_rng_source_calls"])
        self.assertEqual(1, result["native_rng_scalar_source_calls"])
        self.assertTrue(result["native_rng_double_hook_installed"])
        self.assertTrue(result["native_rng_bool_hook_installed"])

    def test_real_nextfloat_header_and_source_evidence_accepted(self):
        records = sample()
        records[0]["data"].update(native_rng_float_hook_installed=True)
        records.insert(-1, {"type": "native_rng_scalar_boundary",
            "action_id": None,
            "data": {
                "site_id": "UNATTRIBUTED", "stream_id": "run.monster_ai",
                "owner_id": "creature:1", "call_ordinal": 0,
                "operation_kind": "range_float",
                "selected_double": None, "selected_bool": None,
                "selected_float": 2.5, "float_min": 2.5, "float_max": 2.5,
                "cursor_before": {"Counter": 4},
                "cursor_after": {"Counter": 5},
                "source_version": "v0.111.0",
                "origin": "source_primitive_hook", "attributed": False,
                "source_hook_observed": True
            }})
        for i, record in enumerate(records):
            record["ordinal"] = i + 1
        result = audit(records)
        self.assertEqual("CALLBACK_TIMELINE_CONSISTENT", result["status"])
        self.assertEqual(1, result["native_rng_float_source_calls"])
        self.assertTrue(result["native_rng_float_hook_installed"])

    def test_ranged_float_missing_bounds_and_nonfinite_values_fail(self):
        records = sample()
        base = {
            "type": "native_rng_scalar_boundary",
            "data": {"source_version": "v0.111.0",
                     "origin": "source_primitive_hook",
                     "source_hook_observed": True,
                     "operation_kind": "range_float",
                     "selected_double": None, "selected_bool": None,
                     "selected_float": 0.5,
                     "float_min": 0.0, "float_max": 1.0,
                     "cursor_before": {"Counter": 0},
                     "cursor_after": {"Counter": 1}}
        }
        for invalid in ({"float_max": None},
                        {"selected_float": float("inf")},
                        {"float_min": 2.0}):
            trial = [dict(row) for row in records]
            altered = {"type": base["type"], "data": {**base["data"], **invalid}}
            trial.insert(-1, altered)
            for i, row in enumerate(trial):
                row["ordinal"] = i + 1
            self.assertEqual("INCOMPLETE_CAPTURE", audit(trial)["status"])

    def test_frame_derived_scalar_source_is_rejected(self):
        records = sample()
        records.insert(-1, {"type": "native_rng_scalar_boundary",
            "data": {"origin": "frame_cursor_difference"}})
        for i, row in enumerate(records):
            row["ordinal"] = i + 1
        self.assertEqual("INCOMPLETE_CAPTURE", audit(records)["status"])


if __name__ == "__main__":
    unittest.main()
