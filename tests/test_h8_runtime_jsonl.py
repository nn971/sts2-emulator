"""H8 versioned JSONL runtime: source-valid A10 draw, stale guard, fork, audit."""
from __future__ import annotations

import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "bindings" / "python"))
from sts2_emulator import EmulatorError, Session  # noqa: E402


@unittest.skipUnless(os.environ.get("STS2_EMULATOR_DLL"),
                     "Set STS2_EMULATOR_DLL to test the real H8 runtime")
class H8JsonlRuntimeTests(unittest.TestCase):
    def test_real_a10_versioned_combat_draw_and_replay(self):
        with Session([
            "dotnet", os.environ["STS2_EMULATOR_DLL"], "prototype-ai-jsonl"
        ]) as session:
            state = session.call(
                "h8_begin_seeded",
                seed="h8-jsonl-a10-mawler-replay",
                act=1, encounter_id="proto.encounter.mawler_normal",
                native_entry="MawlerNormal", total_floor=2,
                ascension=10, first_monster_combat_id=1,
                map_column=3, map_row=1,
                replay_opening_draw_hooks=True,
            )["result"]
            seen = [state["h8Handle"]]

            for _ in range(200):
                if state["kind"] == "draw_card":
                    break
                token = {
                    "h8_handle": state["h8Handle"],
                    "runtime_id": state["runtimeId"],
                    "boundary_version": state["boundaryVersion"],
                }
                if state["kind"] == "enemy_intent":
                    action = session.call(
                        "h8_resolve_intent", **token,
                        intent_id=state["intentIds"][0],
                        provider_id="scripted-h8-regression",
                    )
                elif state["kind"] == "player":
                    self.assertIn(
                        state["endTurnActionId"], state["playerActionIds"])
                    observed = session.call(
                        "h8_observe", **token,
                        policy_id="prototype-fair-v0")
                    import json
                    public = json.loads(observed["payloadJson"])
                    self.assertIsInstance(public, dict)
                    self.assertNotIn("semanticDeck", public)
                    self.assertNotIn("pending", public)
                    self.assertEqual(
                        [x["actionId"] for x in observed["actions"]],
                        state["playerActionIds"])
                    self.assertEqual(observed["policyId"], "prototype-fair-v0")
                    with self.assertRaises(EmulatorError):
                        session.call("h8_observe", **{
                            **token, "boundary_version":
                            state["boundaryVersion"] + 1,
                        }, policy_id="prototype-fair-v0")
                    action = session.call(
                        "h8_step", **token,
                        action_id=state["endTurnActionId"],
                    )
                else:
                    self.fail(f"Unexpected H8 boundary {state['kind']}")
                next_state = action["result"]
                self.assertEqual(state["h8Handle"], next_state["h8Handle"])
                self.assertEqual(
                    state["boundaryVersion"] + 1,
                    next_state["boundaryVersion"])
                state = next_state

            self.assertEqual(state["kind"], "draw_card")
            self.assertTrue(state["cardInstanceIds"])
            token = {
                "h8_handle": state["h8Handle"],
                "runtime_id": state["runtimeId"],
                "boundary_version": state["boundaryVersion"],
            }
            original = session.call(
                "h8_status", h8_handle=state["h8Handle"])["result"]
            self.assertEqual(original, state)
            with self.assertRaises(EmulatorError):
                session.call(
                    "h8_resolve_draw_card", **{
                        **token, "runtime_id":
                            "00000000-0000-0000-0000-000000000000",
                    },
                    card_instance_id=state["cardInstanceIds"][0])
            with self.assertRaises(EmulatorError):
                session.call(
                    "h8_resolve_draw_card", **token,
                    card_instance_id=9223372036854775807)
            self.assertEqual(
                state, session.call(
                    "h8_status", h8_handle=state["h8Handle"])["result"])

            fork = session.call(
                "h8_fork", h8_handle=state["h8Handle"])["result"]
            seen.append(fork["h8Handle"])
            self.assertNotEqual(state["runtimeId"], fork["runtimeId"])
            self.assertEqual(state["boundaryVersion"], fork["boundaryVersion"])

            child = session.call(
                "h8_resolve_draw_card", **token,
                card_instance_id=state["cardInstanceIds"][-1],
                provider_id="scripted-h8-regression",
            )["result"]
            self.assertEqual(
                child["boundaryVersion"], state["boundaryVersion"] + 1)
            branch = session.call(
                "h8_resolve_draw_card", h8_handle=fork["h8Handle"],
                runtime_id=fork["runtimeId"],
                boundary_version=fork["boundaryVersion"],
                card_instance_id=fork["cardInstanceIds"][0],
                provider_id="reference-nature",
            )["result"]
            self.assertEqual(
                branch["boundaryVersion"], fork["boundaryVersion"] + 1)
            for handle in seen:
                audit = session.call("h8_replay_audit", h8_handle=handle)
                self.assertTrue(audit["replayVerified"])
                history = session.call(
                    "h8_decision_history", h8_handle=handle)["decisions"]
                self.assertEqual(
                    len(history),
                    session.call(
                        "h8_status", h8_handle=handle
                    )["result"]["decisionCount"])
                self.assertEqual(history[-1]["kind"], "draw_card")
            released = session.call("h8_release_many", h8_handles=seen)
            self.assertEqual(released["released"], 2)


if __name__ == "__main__":
    unittest.main()
