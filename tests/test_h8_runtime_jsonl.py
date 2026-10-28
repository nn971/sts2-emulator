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
    def test_inline_chance_law_matches_standalone_across_real_draws(self):
        with Session([
            "dotnet", os.environ["STS2_EMULATOR_DLL"], "prototype-ai-jsonl"
        ]) as session:
            state = session.call(
                "h8_begin_seeded", seed="h8-jsonl-inline-event-law-a10",
                act=1, encounter_id="proto.encounter.mawler_normal",
                native_entry="MawlerNormal", total_floor=2,
                ascension=10, first_monster_combat_id=1,
                map_column=3, map_row=1,
                replay_opening_draw_hooks=True,
                include_reference_law=True,
            )["result"]
            handle = state["h8Handle"]
            seen_laws = 0
            for _ in range(160):
                kind = state["kind"]
                if kind == "terminal":
                    break
                token = {
                    "h8_handle": handle,
                    "runtime_id": state["runtimeId"],
                    "boundary_version": state["boundaryVersion"],
                }
                if kind in ("draw_card", "enemy_intent"):
                    reference = session.call(
                        "h8_reference_law", **token)["pending"]
                    self.assertEqual(state["referenceLaw"], reference)
                    seen_laws += 1
                    if kind == "draw_card":
                        response = session.call(
                            "h8_resolve_draw_card", **token,
                            card_instance_id=state["cardInstanceIds"][0],
                            provider_id="h8-inline-provider-regression",
                            include_reference_law=True,
                        )
                    else:
                        response = session.call(
                            "h8_resolve_intent", **token,
                            intent_id=state["intentIds"][0],
                            provider_id="h8-inline-provider-regression",
                            include_reference_law=True,
                        )
                elif kind == "player":
                    self.assertIsNone(state["referenceLaw"])
                    response = session.call(
                        "h8_step", **token,
                        action_id=state["endTurnActionId"],
                        include_reference_law=True,
                    )
                else:
                    self.fail("Unsupported inline Chance Law boundary " + kind)
                nxt = response["result"]
                self.assertEqual(nxt["boundaryVersion"],
                                 state["boundaryVersion"] + 1)
                state = nxt
            self.assertGreaterEqual(seen_laws, 3)
            audit = session.call("h8_replay_audit", h8_handle=handle)
            self.assertTrue(audit["replayVerified"])
            session.call("h8_release_many", h8_handles=[handle])

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
                    law = session.call("h8_reference_law", **token)["pending"]
                    self.assertEqual(law["kind"], "enemy_intent")
                    self.assertEqual(
                        [c["intentId"] for c in law["semanticCandidates"]],
                        state["intentIds"])
                    self.assertEqual(
                        [c["rawWeight"] for c in law["semanticCandidates"]],
                        state["intentRawWeights"])
                    self.assertEqual(
                        [c["outcomeId"] for c in law["chanceLaw"]["outcomes"]],
                        state["intentIds"])
                    self.assertEqual(
                        law["eventId"], law["chanceLaw"]["eventId"])
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
            law = session.call("h8_reference_law", **token)["pending"]
            self.assertEqual(law["kind"], "draw_card")
            self.assertEqual(
                [c["cardInstanceId"] for c in law["candidates"]],
                state["cardInstanceIds"])
            self.assertEqual(
                [c["outcomeId"] for c in law["chanceLaw"]["outcomes"]],
                ["card_instance:" + str(x) for x in state["cardInstanceIds"]])
            self.assertEqual(law["eventId"], law["chanceLaw"]["eventId"])
            self.assertEqual(
                law["constraintFingerprint"],
                law["chanceLaw"]["conditioningFingerprint"])
            with self.assertRaises(EmulatorError):
                session.call(
                    "h8_reference_law", **{
                        **token, "boundary_version":
                            state["boundaryVersion"] + 1})
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

    def test_opt_in_profile_attributes_h8_player_mechanics_without_changing_game(self):
        with Session([
            "dotnet", os.environ["STS2_EMULATOR_DLL"], "prototype-ai-jsonl"
        ]) as session:
            reset = session.call("h8_perf_reset")
            self.assertEqual(
                reset["schemaId"], "sts2-h8-nested-hotpath-performance-v1")
            state = session.call(
                "h8_begin_seeded",
                seed="h8-jsonl-profile-player-a10",
                act=1, encounter_id="proto.encounter.mawler_normal",
                native_entry="MawlerNormal", total_floor=2,
                ascension=10, first_monster_combat_id=1,
                map_column=3, map_row=1,
                replay_opening_draw_hooks=True,
            )["result"]
            handle = state["h8Handle"]
            for _ in range(20):
                token = dict(
                    h8_handle=handle, runtime_id=state["runtimeId"],
                    boundary_version=state["boundaryVersion"])
                if state["kind"] == "player":
                    session.call(
                        "h8_observe", **token, policy_id="prototype-fair-v0")
                    choice = state["endTurnActionId"]
                    state = session.call(
                        "h8_step", **token, action_id=choice)["result"]
                    break
                self.assertEqual(state["kind"], "enemy_intent")
                state = session.call(
                    "h8_resolve_intent", **token,
                    intent_id=state["intentIds"][0])["result"]
            else:
                self.fail("Expected a real player action to profile")
            profile = session.call("h8_perf_read")["profile"]
            self.assertTrue(profile["enabled"])
            self.assertTrue(profile["inclusiveNestedOperations"])
            operations = profile["operations"]
            self.assertIn("h8.player.total", operations)
            self.assertIn("h8.fork.run_total", operations)
            self.assertIn("h8.status.project", operations)
            self.assertIn("h8.jsonl.response", operations)
            self.assertEqual(1, operations["h8.player.total"]["count"])
            self.assertGreater(
                operations["h8.player.total"]["allocatedBytes"], 0)
            stopped = session.call("h8_perf_stop")["profile"]
            self.assertFalse(stopped["enabled"])
            self.assertEqual(
                stopped["operations"]["h8.player.total"],
                operations["h8.player.total"])
            resumed = session.call("h8_perf_resume")
            self.assertEqual(
                resumed["schemaId"],
                "sts2-h8-nested-hotpath-performance-v1")
            second_stop = session.call("h8_perf_stop")["profile"]
            self.assertEqual(
                second_stop["operations"]["h8.player.total"],
                operations["h8.player.total"])
            self.assertTrue(session.call(
                "h8_replay_audit", h8_handle=handle)["replayVerified"])
            session.call("h8_release_many", h8_handles=[handle])


if __name__ == "__main__":
    unittest.main()
