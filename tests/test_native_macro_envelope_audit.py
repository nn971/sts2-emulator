"""Source-only regression fixtures for the run-wide macro envelope."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_macro_envelope_audit import audit


def sample():
    run = "native-run-a"
    def row(kind, data):
        return {"type": kind, "run_id": run, "data": data}
    rows = [
        row("header", {
            "schema": "native-macro-envelope-v1",
            "run_id": run, "seed": "6JK8MD0VQQTA",
            "observation_only": True, "native_gameplay_mutated": False,
            "native_rng_consumed_by_inspector": False,
            "native_emulator_parity": "NOT_TESTED",
        }),
        row("map_snapshot", {
            "act_index": 0, "act_id": "Overgrowth",
            "source": "RunState.Map; StandardActMap._rng.ToSerializable",
            "legal_destination_basis": "SOURCE_MAP_EDGES",
            "map_ui_eligibility": "NOT_VERIFIED",
            "emulator_parity": "NOT_TESTED",
            "map_rng": {"counter": 8},
            "points": [
                {"column": 3, "row": 0, "point_type": "Ancient",
                 "children": [{"column": 2, "row": 1}]},
                {"column": 2, "row": 1, "point_type": "Shop",
                 "children": []},
            ]
        }),
        row("room_entered", {
            "act_index": 0, "total_floor": 0, "column": 3, "row": 0,
            "room_type": "Ancient", "player_hp": 70, "player_gold": 99,
            "source": "RunManager.RoomEntered",
            "outgoing_map_edges": [{"column": 2, "row": 1}],
            "actual_map_screen_actions": "NOT_CAPTURED",
            "emulator_parity": "NOT_TESTED",
        }),
        row("map_request", {
            "request_id": 1, "kind": "map_move",
            "from_column": 3, "from_row": 0,
            "to_column": 2, "to_row": 1,
            "queue_acceptance": "UNVERIFIED",
            "actual_legal_menu": "NOT_CAPTURED",
        }),
        row("map_request_outcome", {
            "request_id": 1, "native_action_state": "WaitingForExecution",
            "native_action_id": 5, "execution_completed": False,
            "emulator_parity": "NOT_TESTED",
        }),
        row("room_entered", {
            "act_index": 0, "total_floor": 1, "column": 2, "row": 1,
            "room_type": "Shop", "player_hp": 70, "player_gold": 99,
            "source": "RunManager.RoomEntered",
            "outgoing_map_edges": [],
            "actual_map_screen_actions": "NOT_CAPTURED",
            "emulator_parity": "NOT_TESTED",
        }),
        row("shop_snapshot", {
            "act_index": 0, "total_floor": 1, "player_gold": 99,
            "cause": "ROOM_ENTERED",
            "offers": [{"slot": 0, "kind": "MerchantCardEntry",
                        "model_id": "CardA", "is_stocked": True,
                        "base_cost": 80, "on_sale": False,
                        "removal_used": False,
                        "displayed_price": None,
                        "displayed_price_coverage":
                            "NOT_CAPTURED_FROM_NATIVE_UI",
                        "purchase_eligible": "NOT_VERIFIED"}],
            "price_modifiers_evaluated_by_inspector": False,
            "native_rng_consumed_by_inspector": False,
            "emulator_parity": "NOT_TESTED",
        }),
        row("shop_snapshot", {
            "act_index": 0, "total_floor": 1, "player_gold": 19,
            "cause": "SOURCE_PURCHASE_COMPLETED",
            "offers": [{"slot": 0, "kind": "MerchantCardEntry",
                        "model_id": None, "is_stocked": False,
                        "base_cost": 80, "on_sale": False,
                        "removal_used": False,
                        "displayed_price": None,
                        "displayed_price_coverage":
                            "NOT_CAPTURED_FROM_NATIVE_UI",
                        "purchase_eligible": "NOT_VERIFIED"}],
            "price_modifiers_evaluated_by_inspector": False,
            "native_rng_consumed_by_inspector": False,
            "emulator_parity": "NOT_TESTED",
        }),
        row("reward_offer", {
            "offer_id": 1, "native_set_id": 0, "player_id": 1,
            "can_skip": True,
            "native_choice_contents_complete": False,
            "emulator_parity": "NOT_TESTED",
            "rewards": [{"index": 0, "native_type": "CardReward",
                         "populated": True, "contents": "NOT_CAPTURED"}],
        }),
        row("reward_selection_requested", {
            "offer_id": 1, "selected_index": 0,
            "choice_link": "SAME_NATIVE_REWARD_OBJECT",
            "effect_completed": False,
        }),
        row("reward_skip_requested", {
            "skipped_set_identity": "UNVERIFIED",
            "effect_completed": False,
        }),
        row("event_choice_request", {
            "request_id": 1, "event_model_id": "EventA",
            "selected_index": 0, "source_menu_coverage":
                "SOURCE_CURRENT_OPTIONS",
            "effect_completed": False,
            "options": [{"index": 0, "text_key": "choice_A",
                         "is_locked": False, "is_proceed": True}],
        }),
        row("event_choice_return", {
            "request_id": 1, "effect_completed": False,
        }),
        row("combat_link", {
            "combat_run_id": "combat-a",
            "emulator_parity": "NOT_TESTED",
        }),
        row("terminal", {
            "status": "SOURCE_OBSERVED", "open_map_requests": 0,
            "open_event_requests": 0,
            "native_game_parity": "NOT_TESTED",
            "whole_run_certified": False,
        }),
    ]
    for i, entry in enumerate(rows, 1):
        entry["ordinal"] = i
    return rows


class NativeMacroEnvelopeAuditTests(unittest.TestCase):
    def test_source_only_trace_passes_without_claiming_emulator_parity(self):
        report = audit(sample())
        self.assertEqual("SOURCE_MACRO_TIMELINE_CONSISTENT", report["status"])
        self.assertEqual(1, report["maps"])
        self.assertEqual(2, report["rooms"])
        self.assertEqual(2, report["shop_snapshots"])
        self.assertEqual(1, report["source_map_request_links"])
        self.assertEqual(1, report["source_reward_object_links"])
        self.assertFalse(report["whole_run_certified"])

    def test_map_edges_and_room_legal_destinations_are_exact(self):
        for corruption in ("missing_child", "changed_target", "wrong_edges"):
            rows = sample()
            if corruption == "missing_child":
                rows[1]["data"]["points"].pop()
            elif corruption == "changed_target":
                rows[1]["data"]["points"][0]["children"][0]["column"] = 4
            else:
                rows[2]["data"]["outgoing_map_edges"] = []
            self.assertEqual("INCOMPLETE_CAPTURE", audit(rows)["status"],
                             corruption)

    def test_queue_identity_and_choice_links_are_fail_closed(self):
        for index, key, value in (
            (4, "request_id", 999),
            (9, "selected_index", 88),
            (11, "request_id", 100),
            (12, "request_id", 100),
        ):
            rows = sample()
            rows[index]["data"][key] = value
            self.assertEqual("INCOMPLETE_CAPTURE", audit(rows)["status"],
                             (index, key))

    def test_forged_shop_price_and_parity_claim_fail(self):
        rows = sample()
        rows[6]["data"]["offers"][0]["displayed_price"] = 80
        self.assertEqual("INCOMPLETE_CAPTURE", audit(rows)["status"])
        rows = sample()
        rows[7]["data"]["emulator_parity"] = "MATCHED"
        self.assertEqual("INCOMPLETE_CAPTURE", audit(rows)["status"])

    def test_partial_capture_and_native_failure_are_distinct(self):
        self.assertEqual("SOURCE_CAPTURE_IN_PROGRESS",
                         audit(sample()[:-1])["status"])
        rows = sample()
        rows.insert(-1, {"type": "observation_failure",
                         "run_id": "native-run-a",
                         "data": {"stage": "shop", "reason": "MISSING"}})
        rows[-1]["data"]["status"] = "INCOMPLETE_CAPTURE"
        for i, entry in enumerate(rows, 1):
            entry["ordinal"] = i
        report = audit(rows)
        self.assertEqual("INCOMPLETE_CAPTURE", report["status"])
        self.assertTrue(report["observational_failures"])

    def test_orphan_and_duplicate_event_requests_fail(self):
        rows = sample()
        rows[11]["data"]["request_id"] = 10
        self.assertEqual("INCOMPLETE_CAPTURE", audit(rows)["status"])
        rows = sample()
        rows.insert(12, copy.deepcopy(rows[11]))
        for i, entry in enumerate(rows, 1):
            entry["ordinal"] = i
        self.assertEqual("INCOMPLETE_CAPTURE", audit(rows)["status"])


if __name__ == "__main__":
    unittest.main()
