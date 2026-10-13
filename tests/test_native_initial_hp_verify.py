"""Native pre-frame initial HP source value/eligibility verification."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from native_rng_stream_audit import MASK, advance, initial_state, rotate
from native_initial_hp_verify import audit, validate


def roll():
    before = initial_state(42)
    random64 = (rotate((before[1] * 5) & MASK, 7) * 9) & MASK
    eligible = [28, 29, 31, 32]
    index = int(((random64 >> 11) * (1 / (1 << 53))) * len(eligible))
    after = advance(before)
    def cursor(counter, state):
        return {"Counter": counter, **{
            "State" + str(i): x for i, x in enumerate(state)}}
    return {
        "source_version": "v0.111.0", "creation_ordinal": 1,
        "monster_model_id": "SludgeSpinner",
        "ordered_eligible_hp": eligible, "selected_index": index,
        "selected_hp": eligible[index],
        "source_eligible_values_exhausted": False,
        "cursor_before": cursor(2, before),
        "cursor_after": cursor(3, after),
        "source_callsite_observed": True,
        "native_creature_combat_id_status": "PRE_ATTACH_CREATURE_ID_UNVERIFIED"
    }


class NativeInitialHpRollTests(unittest.TestCase):
    def test_valid_real_rng_and_selected_hp(self):
        self.assertIsNone(validate(roll()))
        r = audit([{"type": "native_initial_hp_roll", "data": roll()}])
        self.assertEqual("OBSERVED_NATIVE_INITIAL_HP_ROLLS_VALID", r["status"])
        self.assertEqual(1, r["validated_rolls"])
        self.assertEqual("NOT_TESTED",
                         r["native_to_emulator_creature_identity"])

    def test_wrong_source_eligibility_and_value_rejected(self):
        d = roll()
        d["selected_hp"] += 1
        self.assertEqual("INVALID_INITIAL_HP_ELIGIBILITY", validate(d))
        d = roll()
        d["ordered_eligible_hp"].append(d["ordered_eligible_hp"][0])
        self.assertEqual("INVALID_INITIAL_HP_ELIGIBILITY", validate(d))

    def test_native_rng_mutation_detected(self):
        d = roll()
        d["cursor_after"]["State2"] ^= 1
        self.assertEqual("INITIAL_HP_XOSHIRO_STATE_TRANSITION_MISMATCH",
                         validate(d))

    def test_preframe_absence_is_not_success(self):
        self.assertEqual("NO_NATIVE_INITIAL_HP_SOURCE_EVENTS",
                         audit([{"type": "header"}])["status"])

    def test_duplicate_source_creation_id_rejected(self):
        d = roll()
        self.assertEqual("NATIVE_INITIAL_HP_SOURCE_MISMATCH",
                         audit([
                            {"type": "native_initial_hp_roll", "data": d},
                            {"type": "native_initial_hp_roll", "data": d}
                         ])["status"])


if __name__ == "__main__":
    unittest.main()
