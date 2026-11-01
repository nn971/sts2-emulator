"""Offline regression of independent run-start -> first combat prelude replay.

Synthetic source is explicitly constructed from emulator choices; never
interpreted as parity evidence from the installed game.
"""
import base64
import copy
import json
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_run_bootstrap import (
    _neow_native_id, _source_preflight, _emulator_opening_profile,
    run_bootstrap, _native_upfront_diagnosis,
    _native_upfront_room_prefix_diagnosis,
    _native_upfront_full_queue_diagnosis, SCHEMA,
)
from native_neow_rng_verify import neow_event_rng, verify_neow_event_rng, xxhash64


def fixture(seed="synthetic-bootstrap", choice="take_proto.native.neow.heftytablet"):
    def native_option(cls, index):
        names = {
            "arcane_scroll": "ARCANE_SCROLL",
            "heftytablet": "HEFTY_TABLET",
            "wingedboots": "WINGED_BOOTS",
        }
        rid = names[cls]
        return {"index": index, "text_key": "NEOW.pages.INITIAL.options." + rid,
                "relic_model_id": rid, "is_locked": False, "is_proceed": False}

    opts = [native_option("arcane_scroll", 0),
            native_option("wingedboots", 1),
            native_option("heftytablet", 2)]
    return {
        "schema": "native-run-prelude-audit-v1",
        "status": "RUN_PRELUDE_SOURCE_OBSERVED",
        "run_identity": {"seed": seed, "ascension": 10,
                         "game_build": "EMULATOR-DERIVED-SYNTHETIC-NOT-NATIVE",
                         "character_id": "SILENT",
                         "first_act_id": "OVERGROWTH", "run_id": "synthetic"},
        "source_run_rng_snapshots": [{"site": "run_started"}],
        "source_opaque_queue_requests": [],
        "source_precombat_player_choices": [],
        "source_event_choice_requests": [{
            "ordinal": 3, "request_id": 1, "event_model_id": "NEOW",
            "selected_index": 2, "options": opts,
            "event_rng": neow_event_rng(seed, 8)}],
        "source_event_choice_returns": [{"ordinal": 4, "request_id": 1}],
        "source_room_arrivals": [
            {"ordinal": 5, "act_index": 0, "act_floor": 1,
             "map_column": 3, "map_row": 0, "room_type": "Monster",
             "player_hp": 56, "player_gold": 99}],
        "first_combat_link": {"act_index": 0, "map_column": 3, "map_row": 0},
    }


class NativeUpFrontBoundaryTests(unittest.TestCase):
    def test_real_source_run_started_counter_is_reported_as_first_rng_gap(self):
        source = {"source_run_rng_snapshots": [{
            "site": "run_started", "streams": [{
                "scope": "run", "stream_id": "UpFront",
                "counter": 412, "s0": 10, "s1": 20, "s2": 30, "s3": 40,
            }]}]}
        snapshot = {"state": {"native_run_rng": {"streams": [{
            "stream_id": "run.up_front", "call_count": 229,
            "state_bytes": base64.b64encode(struct.pack(
                "<QQQQ", 11, 21, 31, 41)).decode(),
        }]}}}
        result = _native_upfront_diagnosis(source, snapshot)
        self.assertEqual("SOURCE_UPFRONT_DIVERGED", result["status"])
        self.assertEqual(412, result["source_counter"])
        self.assertEqual(229, result["emulator_counter"])
        self.assertEqual(183, result["remaining_source_draws"])
        self.assertEqual("$.native_run_rng.run.up_front.counter",
                         result["first_difference"]["path"])
        self.assertFalse(result["source_cursor_words_match"])

    def test_source_cursor_requires_exact_words_and_encoding(self):
        sample = {"source_run_rng_snapshots": [{
            "site": "run_started", "streams": [{
                "scope": "run", "stream_id": "UpFront",
                "counter": 229, "s0": 11, "s1": 21, "s2": 31, "s3": 41,
            }]}]}
        snap = {"state": {"native_run_rng": {"streams": [{
            "stream_id": "run.up_front", "call_count": 229,
            "state_bytes": base64.b64encode(struct.pack(
                "<QQQQ", 11, 21, 31, 41)).decode(),
        }]}}}
        self.assertEqual("SOURCE_UPFRONT_MATCHED",
                         _native_upfront_diagnosis(sample, snap)["status"])
        self.assertFalse(_native_upfront_diagnosis(sample, snap)[
            "source_upfront_all_phases_reconstructed"])
        snap["state"]["native_startup_rooms"] = {"acts": [
            {"act": index, "events": ["Event"],
             "normal_encounters": ["Weak"], "elite_encounters": ["Elite"],
             "boss_encounter": "Boss", "ancient": "Ancient"}
            for index in (1, 2, 3)]}
        self.assertTrue(_native_upfront_diagnosis(sample, snap)[
            "source_upfront_all_phases_reconstructed"])
        self.assertEqual("OBSERVED_PREFIX_COMPARED_SEPARATELY",
            _native_upfront_diagnosis(sample, snap)[
                "source_upfront_room_identity_parity"])
        snap["state"]["native_run_rng"]["streams"][0]["state_bytes"] = "bogus"
        self.assertEqual("BLOCKED_UPFRONT_CURSOR_ENCODING",
                         _native_upfront_diagnosis(sample, snap)["status"])


    def test_native_rng_parity_does_not_hide_source_room_queue_mismatch(self):
        # Observed native first combat: FUZZY_WURM_CRAWLER_WEAK.
        # Independently reconstructed UpFront source queue currently starts
        # with SlimesWeak, even though its 412-call RNG cursor is identical.
        source = {"source_room_arrivals": [{
            "ordinal": 12, "act_index": 0, "room_type": "Monster",
            "room_model_id": "FUZZY_WURM_CRAWLER_WEAK"}]}
        snapshot = {"state": {"native_startup_rooms": {"acts": [
            {"act": 1, "normal_encounters":
                ["SlimesWeak", "FuzzyWurmCrawlerWeak"],
             "elite_encounters": ["ByrdonisElite"], "boss_encounter": "TheKinBoss"},
            {"act": 2, "normal_encounters": ["BowlbugsWeak"],
             "elite_encounters": [], "boss_encounter": "KaiserCrabBoss"},
            {"act": 3, "normal_encounters": ["TurretOperatorWeak"],
             "elite_encounters": [], "boss_encounter": "TestSubjectBoss"}]}}}
        result = _native_upfront_room_prefix_diagnosis(source, snapshot)
        self.assertEqual("SOURCE_ROOM_QUEUE_FIRST_DIFFERENCE", result["status"])
        self.assertEqual("FUZZY_WURM_CRAWLER_WEAK",
                         result["first_difference"]["native"])
        self.assertEqual("SlimesWeak",
                         result["first_difference"]["emulator_source_queue"])
        self.assertEqual("$.native_startup_rooms.acts[0].normal_encounters[0]",
                         result["first_difference"]["path"])
        self.assertEqual("NOT_VERIFIED", result["full_queue_identity_parity"])

        # A matching observed prefix is still not proof of the unobserved
        # remainder of the source-generated queues.
        snapshot["state"]["native_startup_rooms"]["acts"][0][
            "normal_encounters"] = ["FuzzyWurmCrawlerWeak", "SlimesWeak"]
        source["source_room_arrivals"].append({
            "ordinal": 21, "act_index": 0, "room_type": "Monster",
            "room_model_id": "SLIMES_WEAK"})
        matched = _native_upfront_room_prefix_diagnosis(source, snapshot)
        self.assertEqual("SOURCE_ROOM_QUEUE_OBSERVED_PREFIX_MATCHED",
                         matched["status"])
        self.assertEqual(2, matched["source_observed_prefix_length"])
        self.assertEqual("NOT_VERIFIED", matched["full_queue_identity_parity"])

        source["source_room_arrivals"][0]["room_model_id"] = ""
        self.assertEqual("BLOCKED_NATIVE_ROOM_IDENTITY",
            _native_upfront_room_prefix_diagnosis(source, snapshot)["status"])


    def test_complete_native_startup_queue_comparison_is_separate(self):
        acts = []
        for i in range(3):
            acts.append({
                "act_index": i, "events": ["A", "B"],
                "normal_encounters": ["FuzzyWurmCrawlerWeak"],
                "elite_encounters": ["ByrdonisElite"],
                "boss_encounter": "TheKinBoss",
                "second_boss_encounter": None,
                "ancient": "Neow"})
        native = {"source_startup_room_queues": {"acts": acts}}
        snap = {"state": {"native_startup_rooms": {"acts": [
            {"act": i + 1, "events": ["A", "B"],
             "normal_encounters": ["SlimesWeak"],
             "elite_encounters": ["ByrdonisElite"],
             "boss_encounter": "TheKinBoss",
             "second_boss_encounter": None,
             "ancient": "Neow"}
            for i in range(3)]}}}
        result = _native_upfront_full_queue_diagnosis(native, snap)
        self.assertEqual("SOURCE_FULL_ROOM_QUEUES_DIVERGED", result["status"])
        self.assertEqual(
            "$.native_startup_rooms.acts[0].normal_encounters[0]",
            result["first_difference"]["path"])
        snap["state"]["native_startup_rooms"]["acts"][0][
            "normal_encounters"] = ["FuzzyWurmCrawlerWeak"]
        for act in snap["state"]["native_startup_rooms"]["acts"][1:]:
            act["normal_encounters"] = ["FuzzyWurmCrawlerWeak"]
        self.assertEqual("SOURCE_FULL_ROOM_QUEUES_MATCHED",
                         _native_upfront_full_queue_diagnosis(native, snap)["status"])
        self.assertEqual("SOURCE_FULL_ROOM_QUEUES_NOT_CAPTURED",
                         _native_upfront_full_queue_diagnosis({}, snap)["status"])


class FakeState:
    def __init__(self, stage, root=None):
        self.stage = stage
        self.root = root or {}
    def save_snapshot(self):
        return json.dumps({"schema": "sts2-exact-snapshot-v1", "state": {
            "run_seed": self.root["seed"], "ascension": self.root["ascension"]}})
    def actions(self):
        if self.stage == 0:
            return [{"kind": "start_run", "payloadJson": "{}",
                     "actionId": "start"}]
        if self.stage == 1:
            return [{"kind": "event_choice",
                     "payloadJson": json.dumps({"ChoiceId": x}),
                     "actionId": "neow-" + str(i)}
                    for i, x in enumerate(self.root["offers"])]
        if self.stage == 2:
            return [{"kind": "continue_event",
                     "payloadJson": "{}", "actionId": "proceed"}]
        if self.stage == 3:
            return [{"kind": "choose_map_node",
                     "payloadJson": '{"NodeId":"1:1:3"}',
                     "actionId": "map"}]
        return []
    def observe(self):
        if self.stage == 1:
            return {"event_id": "proto.native.event.neow",
                    "event": {"offered_choice_ids": self.root["offers"]}}
        if self.stage == 2:
            return {"event_id": "proto.native.event.neow", "phase": "event",
                    "event": {"terminal_page": True}}
        if self.stage == 3:
            return {"event_id": None, "phase": "map_choice",
                    "map": [{"node_id": "1:1:3", "act": 1, "floor": 1}]}
        return {"combat": {"turn": 1}, "current_map_node_id": "1:1:3",
                "hp": self.root.get("hp", 56),
                "gold": self.root.get("gold", 99)}
    def step(self, action_id):
        if (self.stage == 0 and action_id == "start"
            or self.stage == 1 and action_id.startswith("neow-")
            or self.stage == 2 and action_id == "proceed"
            or self.stage == 3 and action_id == "map"):
            return FakeState(self.stage + 1, self.root)
        raise ValueError("Unexpected or invented emulator action")


class FakeSession:
    def __init__(self, *, offers=None, hp=56):
        self.offers = offers or [
            "take_proto.native.neow.arcanescroll",
            "take_proto.native.neow.wingedboots",
            "take_proto.native.neow.heftytablet"]
        self.hp = hp
        self.reset_count = 0
    def reset(self, seed, **kwargs):
        self.reset_count += 1
        if kwargs != {"mode": "v111", "character": "silent",
                      "first_act": "Overgrowth", "ascension": 10,
                      "fidelity_id": "v111-experimental-native-neow-opening-v1"}:
            raise AssertionError(kwargs)
        return FakeState(0, {"seed": seed, "ascension": 10,
                             "offers": self.offers, "hp": self.hp})


class BootstrapTests(unittest.TestCase):
    def test_source_build_uses_native_map_only_for_pinned_game(self):
        real = {"game_build": "assembly=v0.111.0;mvid=" + "a" * 32
                + ";sha256=" + "b" * 64}
        synthetic = fixture()["run_identity"]
        self.assertEqual("v111-experimental-native-standard-map-opening-v1",
                         _emulator_opening_profile(real))
        self.assertEqual("v111-experimental-native-neow-opening-v1",
                         _emulator_opening_profile(synthetic))

    def test_source_neow_mapping_requires_original_text_and_relic_id(self):
        item = fixture()["source_event_choice_requests"][0]["options"][2]
        self.assertEqual("take_proto.native.neow.heftytablet",
                         _neow_native_id(item))
        wrong = item.copy()
        wrong["text_key"] = "NEOW.pages.INITIAL.options.WINGED_BOOTS"
        with self.assertRaises(ValueError):
            _neow_native_id(wrong)
        unknown = item.copy()
        unknown["relic_model_id"] = "UNKNOWN"
        unknown["text_key"] = "NEOW.pages.INITIAL.options.UNKNOWN"
        with self.assertRaises(ValueError):
            _neow_native_id(unknown)

    def test_source_observation_must_be_audited_first(self):
        source = fixture()
        source["status"] = "INVALID_PRELUDE"
        session = FakeSession()
        result, snap = run_bootstrap(source, session)
        self.assertEqual("BLOCKED_UNAUDITED_SOURCE", result["status"])
        self.assertIsNone(snap)
        self.assertEqual(0, session.reset_count)

    def test_independent_bootstrap_steps_only_exact_legal_actions(self):
        session = FakeSession()
        result, snapshot = run_bootstrap(fixture(), session)
        self.assertEqual("CANDIDATE_COMBAT_REACHED_UNCERTIFIED", result["status"])
        self.assertEqual(["start_run", "event_choice", "continue_event",
                          "choose_map_node"],
                         [x["kind"] for x in result["emulator_steps"]])
        self.assertEqual(1, session.reset_count)
        self.assertIsInstance(snapshot, str)
        self.assertEqual("PRODUCED_UNCERTIFIED", result["candidate_emulator_snapshot"])
        self.assertEqual("NOT_TESTED", result["native_rng_alignment"])
        self.assertFalse(result["ready_for_native_lockstep"])

    def test_native_seed_hash_and_event_checkpoint(self):
        self.assertEqual(0xef46db3751d8e999, xxhash64(b""))
        self.assertEqual(0xd24ec4f1a98c6e5b, xxhash64(b"a"))
        self.assertEqual(0x44bc2cf5ad770999, xxhash64(b"abc"))
        state = neow_event_rng("native-checkpoint-seed", 10)
        self.assertEqual("SOURCE_EVENT_RNG_RECONSTRUCTED",
                         verify_neow_event_rng("native-checkpoint-seed", state)["status"])
        changed = dict(state, s3=state["s3"] ^ 1)
        report = verify_neow_event_rng("native-checkpoint-seed", changed)
        self.assertEqual("SOURCE_EVENT_RNG_DIFFERENCE", report["status"])
        self.assertEqual("$.event_rng.s3", report["first_difference"]["path"])

    def test_inconsistent_native_event_rng_blocks_before_emulator_reset(self):
        source = fixture()
        source["source_event_choice_requests"][0]["event_rng"]["s0"] ^= 1
        session = FakeSession()
        report, candidate = run_bootstrap(source, session)
        self.assertEqual("BLOCKED_NATIVE_NEOW_RNG_RECONSTRUCTION", report["status"])
        self.assertEqual("$.event_rng.s0",
                         report["first_difference_or_blocker"]["path"])
        self.assertIsNone(candidate)
        self.assertEqual(0, session.reset_count)

    def test_missing_legacy_source_rng_is_explicit_blocker(self):
        source = fixture()
        source["run_identity"]["seed"] = "oldlegacy"
        session = FakeSession()
        report, _ = run_bootstrap(source, session)
        self.assertEqual("BLOCKED_NATIVE_NEOW_RNG_RECONSTRUCTION", report["status"])
        self.assertEqual(0, session.reset_count)

    def test_first_neow_offer_difference_stops_before_macro_action(self):
        session = FakeSession(offers=[
            "take_proto.native.neow.arcanescroll",
            "take_proto.native.neow.lavarock",
            "take_proto.native.neow.heftytablet"])
        result, snapshot = run_bootstrap(fixture(), session)
        self.assertEqual("SOURCE_NEOW_OFFER_DIFFERENCE", result["status"])
        self.assertEqual("$.neow.offers[1]",
                         result["first_difference_or_blocker"]["path"])
        self.assertEqual(1, len(result["emulator_steps"]))
        self.assertIsNone(snapshot)

    def test_missing_original_map_position_fails_closed(self):
        source = fixture()
        source["source_room_arrivals"][0]["map_column"] = 4
        source["first_combat_link"]["map_column"] = 4
        result, snap = run_bootstrap(source, FakeSession())
        self.assertEqual("SOURCE_MAP_NODE_DIFFERENCE", result["status"])
        self.assertIsNone(snap)

    def test_unsupported_nested_macro_choices_are_not_guessed(self):
        source = fixture()
        source["source_precombat_player_choices"] = [{"choice_id": 2}]
        result, snap = run_bootstrap(source, FakeSession())
        self.assertEqual("BLOCKED_NESTED_PRECOMBAT_CHOICES", result["status"])
        self.assertIsNone(snap)

    def test_opaque_queue_action_blocks_before_reset(self):
        source = fixture()
        source["source_opaque_queue_requests"] = [{"native_action_type": "Other"}]
        session = FakeSession()
        result, _ = run_bootstrap(source, session)
        self.assertEqual("BLOCKED_OPAQUE_PRECOMBAT_ACTIONS", result["status"])
        self.assertEqual(0, session.reset_count)

    def test_unobserved_extra_room_blocks_complex_routes(self):
        source = fixture()
        source["source_room_arrivals"].append(dict(
            source["source_room_arrivals"][0], map_column=2, ordinal=8))
        self.assertEqual("BLOCKED_COMPLEX_PRECOMBAT_ROUTE",
                         _source_preflight(source)[0])

    def test_uncertified_hp_difference_is_exposed_with_candidate(self):
        result, snap = run_bootstrap(fixture(), FakeSession(hp=55))
        self.assertEqual("CANDIDATE_VISIBLE_DIFFERENCE_UNCERTIFIED",
                         result["status"])
        self.assertEqual("$.hp", result["first_difference_or_blocker"]["path"])
        self.assertIsNotNone(snap)

    def test_first_floor_scope_is_explicit(self):
        source = fixture()
        source["source_room_arrivals"][0]["act_floor"] = 2
        result, snap = run_bootstrap(source, FakeSession())
        self.assertEqual("BLOCKED_COMPLEX_PRECOMBAT_ROUTE", result["status"])
        self.assertIsNone(snap)


if __name__ == "__main__":
    unittest.main()
