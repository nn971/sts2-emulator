"""Assertions for keeping private stochastic state out of public JSONL views."""


def contains_private_stochastic_field(value):
    """Return whether a JSON value contains a private-state field name.

    Public values may legitimately contain identifiers such as
    ``native-rng-overgrowth-v1``. Privacy checks therefore inspect object keys,
    not serialized text values.
    """
    if isinstance(value, dict):
        return any(
            "rng" in str(key).lower()
            or "semanticdeck" in str(key).replace("_", "").lower()
            or contains_private_stochastic_field(child)
            for key, child in value.items()
        )
    if isinstance(value, list):
        return any(contains_private_stochastic_field(child) for child in value)
    return False


def assert_public_stochastic_view(value):
    if contains_private_stochastic_field(value):
        raise AssertionError("Public JSONL view contains a private stochastic field")
