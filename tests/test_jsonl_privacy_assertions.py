import unittest

from tools.jsonl_privacy_assertions import (
    assert_public_stochastic_view,
    contains_private_stochastic_field,
)


class TestJsonlPrivacyAssertions(unittest.TestCase):
    def test_public_identifier_may_contain_rng_text(self):
        view = {"map_generation_profile_id": "overgrowth-native-rng-v1"}
        self.assertFalse(contains_private_stochastic_field(view))
        assert_public_stochastic_view(view)

    def test_private_fields_are_detected_recursively(self):
        view = {"combat": {"provider": {"native_rng_cursor": 12}}}
        self.assertTrue(contains_private_stochastic_field(view))
        with self.assertRaises(AssertionError):
            assert_public_stochastic_view(view)

    def test_semantic_deck_field_is_private(self):
        self.assertTrue(contains_private_stochastic_field(
            {"combat": {"semantic_deck": {"draw_order": []}}}))


if __name__ == "__main__":
    unittest.main()
