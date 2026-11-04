"""Check tracked native persistent RNG stream stability at source action windows.

The native v0.111 action sidecar captures 15 persistent streams immediately
after action completion and immediately after the next action begins. These
are source observations, not an exhaustive RNG inventory or a settlement
certificate. In particular, an unchanged public combat projection alone
does not establish that the RNG states are equal.
"""
from __future__ import annotations

from native_action_window_audit import audit_action_windows

CURSOR_KEYS = ("Counter", "State0", "State1", "State2", "State3")
TRACKED_STREAM_COUNT = 15
EXPECTED_SOURCE = "native_rng_object_to_serializable"
EXPECTED_SCOPE = "15_persistent_streams_in_observed_combat"


def _validated_streams(record: dict | None, *, phase: str,
                       ordinal: int, action_id: int | None) -> dict | None:
    if not isinstance(record, dict) or record.get("type") != "native_rng_stream_checkpoint":
        return None
    if record.get("ordinal") != ordinal:
        return None
    # After-action snapshots are generated from the action-executor
    # completion callback and do not carry an action_id. Before-action
    # snapshots carry the newly starting action's ID.
    if phase == "before_action" and record.get("action_id") != action_id:
        return None
    data = record.get("data")
    if not isinstance(data, dict) or (
            data.get("phase") != phase
            or data.get("source") != EXPECTED_SOURCE
            or data.get("scope") != EXPECTED_SCOPE
            or data.get("rng_draws_performed") != 0):
        return None
    streams = data.get("streams")
    if not isinstance(streams, list) or len(streams) != TRACKED_STREAM_COUNT:
        return None
    result = {}
    for entry in streams:
        if not isinstance(entry, dict):
            return None
        sid = entry.get("stream_id")
        cursor = entry.get("cursor")
        if (not isinstance(sid, str) or not sid or sid in result
            or not isinstance(cursor, dict)
            or set(cursor) != set(CURSOR_KEYS)
            or any(type(cursor[k]) is not int
                   or cursor[k] < 0 or cursor[k] >= 2**64
                   for k in CURSOR_KEYS)):
            return None
        result[sid] = cursor
    return result


def compare_source_rng_window(post: dict | None, pre: dict | None, *,
                              completion_ordinal: int,
                              next_start_ordinal: int,
                              next_action_id: int) -> tuple[str, dict | None]:
    """Return a source-only status and a verified matching 15-stream cursor.

    Exact native callback ordinals matter: nearest neighboring snapshots
    could straddle additional effects and cannot be substituted.
    """
    if (type(completion_ordinal) is not int
        or type(next_start_ordinal) is not int
        or type(next_action_id) is not int
        or completion_ordinal >= next_start_ordinal):
        return "SOURCE_RNG_WINDOW_INVALID", None
    a = _validated_streams(
        post, phase="after_action", ordinal=completion_ordinal + 1,
        action_id=None)
    b = _validated_streams(
        pre, phase="before_action", ordinal=next_start_ordinal + 1,
        action_id=next_action_id)
    if a is None or b is None:
        return "SOURCE_RNG_WINDOW_UNVERIFIED", None
    if set(a) != set(b):
        return "SOURCE_RNG_TRACKED_STREAM_IDS_CHANGED", None
    if a != b:
        return "SOURCE_RNG_CHANGED_BETWEEN_CALLBACKS", None
    return "SOURCE_15_TRACKED_RNG_STREAMS_STABLE", a


def audit_native_rng_windows(records: list[dict], *, source_windows=None) -> dict:
    """Audit 15 tracked streams at every public-projection-stable card window."""
    windows = (source_windows if source_windows is not None
               else audit_action_windows(records).get("source_windows", []))
    by_ordinal = {r.get("ordinal"): r for r in records
                  if isinstance(r, dict)
                  and r.get("type") == "native_rng_stream_checkpoint"
                  and type(r.get("ordinal")) is int}
    summary = {
        "status": "NOT_CAPTURED",
        "source_projection_stable_windows": 0,
        "source_15_streams_stable_windows": 0,
        "source_rng_changed_windows": 0,
        "source_rng_unverifiable_windows": 0,
        "source_rng_streams_exhaustive": False,
        "emulator_rng_parity": "NOT_TESTED",
        "native_settlement_certified": False,
        "windows": [],
    }
    for w in windows:
        if w.get("native_projection") != "UNCHANGED_BETWEEN_SOURCE_CALLBACKS":
            continue
        summary["source_projection_stable_windows"] += 1
        end, nxt = w["completion_ordinal"], w["next_start_ordinal"]
        status, _ = compare_source_rng_window(
            by_ordinal.get(end + 1), by_ordinal.get(nxt + 1),
            completion_ordinal=end, next_start_ordinal=nxt,
            next_action_id=w["next_action_id"])
        summary["windows"].append({
            "card_action_id": w["card_action_id"],
            "completion_ordinal": end,
            "next_start_ordinal": nxt,
            "rng_status": status,
        })
        if status == "SOURCE_15_TRACKED_RNG_STREAMS_STABLE":
            summary["source_15_streams_stable_windows"] += 1
        elif status == "SOURCE_RNG_CHANGED_BETWEEN_CALLBACKS":
            summary["source_rng_changed_windows"] += 1
        else:
            summary["source_rng_unverifiable_windows"] += 1
    if summary["source_projection_stable_windows"]:
        summary["status"] = "SOURCE_POST_CARD_TRACKED_RNG_WINDOWS_OBSERVED_UNCERTIFIED"
    return summary
