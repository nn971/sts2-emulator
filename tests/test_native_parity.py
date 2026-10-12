"""Synthetic protocol checks; these do not prove STS2 native parity."""
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_parity import compare, load_trace
from native_scenario_import import import_manifest


def sample():
    coverage = {"verified_fields": ["$.player.hp", "$.combat.energy"],
                "missing_fields": ["$.combat.draw_pile"],
                "unsupported_mechanics": [], "verified_events": ["damage"],
                "rng_status": "RNG_UNAUDITED",
                "required_preconditions": ["$.player.hp", "$.combat.energy"]}
    def decision(n, hp):
        return {"type": "decision", "decision_id": n, "phase": "Combat",
                "state": {"player": {"hp": hp}, "combat": {"energy": 3}},
                "state_hash": f"state-{n}",
                "legal_actions": [{"kind": "play_card", "payload": {"card_instance_id": 1}}],
                "coverage": copy.deepcopy(coverage)}
    def action(n):
        return {"type": "action", "decision_id": n, "accepted": True,
                "action": {"kind": "play_card", "payload": {"card_instance_id": 1}}}
    def event(n):
        return {"type": "event", "decision_id": n, "event_seq": n, "kind": "damage",
                "actor_id": "card:1", "target_id": "creature:1", "payload": {"amount": 6}}
    def transition(n):
        return {"type": "transition", "decision_id": n, "before_hash": f"state-{n}",
                "after_hash": f"state-{n+1}", "status": "COMPLETE"}
    return [
        {"type": "header", "trace_schema": "0.2", "game_build": "SYNTHETIC",
         "bridge_version": "contract", "run_id": "synthetic", "run_seed": "0"},
        decision(1, 50), action(1), event(1), transition(1),
        decision(2, 44), action(2), event(2), transition(2), decision(3, 38),
        {"type": "terminal", "status": "COMPLETE", "reason": "fixture"},
    ]


class NativeParityContractTests(unittest.TestCase):
    def test_synthetic_step_matching(self):
        result = compare(sample(), copy.deepcopy(sample()), decision_limit=1)
        self.assertEqual("MATCH", result["status"])
        self.assertEqual(2, len(result["coverage"]["verified_fields"]))

    def test_precise_post_state_difference(self):
        candidate = sample()
        candidate[5]["state"]["player"]["hp"] = 45
        result = compare(sample(), candidate, decision_limit=1)
        self.assertEqual("MISMATCH", result["status"])
        self.assertEqual("$.player.hp", result["first_difference"]["path"])

    def test_precondition_difference_is_invalid_setup(self):
        candidate = sample()
        candidate[1]["state"]["player"]["hp"] = 51
        self.assertEqual("INVALID_SETUP", compare(sample(), candidate, decision_limit=1)["status"])

    def test_menu_mismatch(self):
        candidate = sample()
        candidate[1]["legal_actions"] = []
        self.assertEqual("MISMATCH", compare(sample(), candidate, decision_limit=1)["status"])

    def test_audited_event_mismatch(self):
        candidate = sample()
        candidate[3]["payload"]["amount"] = 7
        result = compare(sample(), candidate, decision_limit=1)
        self.assertEqual("MISMATCH", result["status"])
        self.assertIn("events", result["first_difference"]["path"])

    def test_field_coverage_is_explicit(self):
        candidate = sample()
        candidate[1]["coverage"]["verified_fields"] = []
        self.assertEqual("UNSUPPORTED", compare(sample(), candidate, decision_limit=1)["status"])

    def test_proven_chance_site_is_unaligned(self):
        reference = sample()
        candidate = sample()
        reference[4]["chance_unaligned_at"] = "shuffle_1"
        candidate[4]["chance_unaligned_at"] = "shuffle_1"
        self.assertEqual("CHANCE_UNALIGNED", compare(reference, candidate, decision_limit=1)["status"])

    def test_unaudited_preconditions_cannot_match(self):
        candidate = sample()
        candidate[1]["coverage"].pop("required_preconditions")
        self.assertEqual("INVALID_SETUP",
                         compare(sample(), candidate, decision_limit=1)["status"])

    def test_gapped_event_sequence_is_incomplete(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trace.jsonl"
            records = sample()
            records[3]["event_seq"] = 2
            path.write_text("\n".join(map(json.dumps, records)) + "\n", encoding="utf-8")
            _, error = load_trace(path)
            self.assertIn("event_seq", error)

    def test_partial_jsonl_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trace.jsonl"
            path.write_text("\n".join(map(json.dumps, sample()[:-1])) + "\n",
                            encoding="utf-8")
            _, error = load_trace(path)
            self.assertIn("terminal", error.lower())

    def test_importer_does_not_claim_continuation(self):
        manifest = import_manifest(sample(), 1)
        self.assertEqual("PARTIAL", manifest["status"])
        self.assertFalse(manifest["next_step_ready"])
        self.assertIn("$.combat.draw_pile", manifest["missing_fields"])


if __name__ == "__main__":
    unittest.main()
