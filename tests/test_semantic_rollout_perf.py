"""H7-compatible semantic observation and opt-in JSONL profiling regression."""
from __future__ import annotations

import json
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "bindings" / "python"))
from sts2_emulator import EmulatorError, SemanticCombat, Session  # noqa: E402


class MockSession:
    def __init__(self):
        self.calls = []

    def call(self, operation, **payload):
        self.calls.append((operation, payload))
        return {
            "payloadJson": json.dumps({"combat": {"hand": []}, "hp": 55}),
            "actions": [{"actionId": "end_turn", "kind": "end_turn", "payloadJson": "{}"}],
        }


class SemanticFrameTests(unittest.TestCase):
    @staticmethod
    def state(boundary="player"):
        fake = MockSession()
        state = SemanticCombat(
            fake, "m7", "hash7", boundary, None,
            boundary == "terminal", None, None,
        )
        return fake, state

    def test_joint_frame_one_request_same_menu(self):
        fake, state = self.state()
        frame, actions = state.observe_frame()
        self.assertEqual(frame["hp"], 55)
        self.assertEqual(actions[0]["actionId"], "end_turn")
        self.assertEqual(len(fake.calls), 1)
        self.assertEqual(fake.calls[0], ("semantic_observe", {
            "semantic_handle": "m7", "policy_id": "prototype-fair-v0",
        }))

    def test_legacy_interfaces_keep_values(self):
        fake, state = self.state()
        self.assertEqual(state.observe()["hp"], 55)
        self.assertEqual(state.actions()[0]["kind"], "end_turn")
        self.assertEqual(len(fake.calls), 1)

    def test_actions_alone_still_calls_emulator(self):
        fake, state = self.state()
        self.assertEqual(state.actions()[0]["actionId"], "end_turn")
        self.assertEqual(len(fake.calls), 1)

    def test_one_shot_cache_is_scoped_to_handle(self):
        fake, state = self.state()
        state.observe()
        other = SemanticCombat(fake, "m8", "hash8", "player", None, False, None, None)
        self.assertEqual(other.actions()[0]["actionId"], "end_turn")
        self.assertEqual(len(fake.calls), 2)

    def test_non_player_boundaries_cannot_observe(self):
        fake, state = self.state("nature")
        with self.assertRaises(EmulatorError):
            state.observe_frame()
        self.assertEqual(fake.calls, [])


@unittest.skipUnless(os.environ.get("STS2_EMULATOR_DLL"), "Set STS2_EMULATOR_DLL for live semantic smoke")
class SemanticProfilerIntegrationTests(unittest.TestCase):
    def test_real_cli_fair_frame_and_profile_snapshot(self):
        cmd = [
            "dotnet", os.environ["STS2_EMULATOR_DLL"], "prototype-ai-jsonl",
        ]
        with Session(cmd) as session:
            schema = session.reset_semantic_profile()
            self.assertEqual(schema, "sts2-semantic-jsonl-performance-v1")
            state = session.begin_seeded_semantic_combat(
                "h7-semantic-perf-regression", act=1,
                encounter_id="proto.encounter.mawler_normal",
                native_entry="MawlerNormal", total_floor=2,
                ascension=10, first_monster_combat_id=1,
                map_column=3, map_row=1,
                replay_opening_draw_hooks=True,
                fixture_deck_preset_id="silent-starter-v1",
            )
            self.assertIn(state.boundary, ("player", "player_choice"))
            observation, actions = state.observe_frame()
            self.assertIsInstance(observation, dict)
            self.assertTrue(actions)
            self.assertEqual(state.observe(), observation)
            self.assertEqual(state.actions(), actions)
            self.assertEqual(state.fingerprint, state.status()["fingerprint"])
            child = state.step(actions[0]["actionId"])
            snapshot = child.save_snapshot()
            restored = session.load_semantic_snapshot(snapshot)
            self.assertEqual(child.fingerprint, restored.fingerprint)
            profile = session.read_semantic_profile()
            self.assertEqual(profile["schemaId"], schema)
            measurements = profile["operations"]
            self.assertEqual(measurements["op.semantic_observe"]["count"], 2)
            self.assertEqual(measurements["op.semantic_step"]["count"], 1)
            self.assertGreaterEqual(measurements["op.semantic_load"]["count"], 1)
            self.assertGreaterEqual(measurements["sub.semantic_store_fork"]["count"], 3)
            self.assertGreater(measurements["sub.semantic_result_fingerprint"]["count"], 0)
            self.assertGreater(measurements["sub.semantic_result_pending"]["count"], 0)
            for item in measurements.values():
                self.assertGreaterEqual(item["totalSeconds"], 0.0)
                self.assertGreaterEqual(item["allocatedBytes"], 0)
            session.release_semantic_many([state, child, restored])
