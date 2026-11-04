"""Source-only continuity windows between completed native card actions.

An ActionExecutor.AfterActionExecuted callback observes the completed GameAction
body. Equality with a later BeforeActionExecuted snapshot shows that no visible
projection changed *between those two observations*. It is evidence for an
additional comparison boundary, not a guarantee that all asynchronous tasks
in the original game have completed.
"""
from __future__ import annotations


def _first_difference(left, right, path="$"):
    """Return the first differing JSON path without copying private state."""
    if type(left) is not type(right):
        return path
    if isinstance(left, dict):
        for key in sorted(left.keys() | right.keys()):
            child = path + "." + key
            if key not in left or key not in right:
                return child
            found = _first_difference(left[key], right[key], child)
            if found is not None:
                return found
    elif isinstance(left, list):
        if len(left) != len(right):
            return path + ".length"
        for index, (a, b) in enumerate(zip(left, right)):
            found = _first_difference(a, b, path + f"[{index}]")
            if found is not None:
                return found
    elif left != right:
        return path
    return None


def audit_action_windows(records: list[dict]) -> dict:
    """Summarize native play-card post/next-action pre observation windows.

    These are source/source comparisons only; the emulator is not consulted.
    A changed window may represent legitimate automatic effects and is
    reported as such. Incomplete and cross-turn windows remain deferred.
    """
    result = {
        "status": "NOT_CAPTURED",
        "source_completed_card_actions": 0,
        "stable_post_card_windows": 0,
        "changed_post_card_windows": 0,
        "unpaired_post_card_windows": 0,
        "unverifiable_post_card_windows": 0,
        "nested_choice_stable_windows": 0,
        "source_windows": [],
        "native_settlement_certified": False,
        "emulator_action_projection_parity": "NOT_TESTED",
    }
    if not isinstance(records, list):
        return result
    starts = [r for r in records if isinstance(r, dict)
              and r.get("type") == "action_started"]
    ends = {}
    for row in records:
        if (isinstance(row, dict) and row.get("type") == "action_completed"
            and type(row.get("action_id")) is int):
            ends.setdefault(row["action_id"], []).append(row)
    for index, start in enumerate(starts):
        action = start.get("canonical_action")
        if not isinstance(action, dict) or action.get("kind") != "play_card":
            continue
        action_id = start.get("action_id")
        completion = ends.get(action_id, [])
        if len(completion) != 1:
            result["unverifiable_post_card_windows"] += 1
            continue
        finished = completion[0]
        result["source_completed_card_actions"] += 1
        next_start = starts[index + 1] if index + 1 < len(starts) else None
        if next_start is None:
            result["unpaired_post_card_windows"] += 1
            continue
        a, b = finished.get("state"), next_start.get("state")
        start_ordinal, end_ordinal, next_ordinal = (
            start.get("ordinal"), finished.get("ordinal"),
            next_start.get("ordinal"))
        if (not all(type(k) is int for k in
                    (start_ordinal, end_ordinal, next_ordinal))
            or not start_ordinal < end_ordinal < next_ordinal
            or not isinstance(a, dict) or not isinstance(b, dict)):
            result["unverifiable_post_card_windows"] += 1
            continue
        # There may be a turn-loop callback between actions, even when
        # their two visible snapshots happen to agree. Such a window is
        # not a same-phase action-completion observation.
        if any(row.get("type") == "native_phase_handoff"
               and type(row.get("ordinal")) is int
               and end_ordinal < row["ordinal"] < next_ordinal
               for row in records if isinstance(row, dict)):
            result["unpaired_post_card_windows"] += 1
            continue
        turn_a = (a.get("combat") or {}).get("turn")
        turn_b = (b.get("combat") or {}).get("turn")
        if type(turn_a) is not int or turn_a != turn_b:
            result["unpaired_post_card_windows"] += 1
            continue
        difference = _first_difference(a, b)
        window = {
            "card_action_id": action_id,
            "completion_ordinal": end_ordinal,
            "next_action_id": next_start.get("action_id"),
            "next_start_ordinal": next_ordinal,
            "turn": turn_a,
            "native_projection": (
                "UNCHANGED_BETWEEN_SOURCE_CALLBACKS"
                if difference is None else "CHANGED_BETWEEN_SOURCE_CALLBACKS"),
            "first_source_difference_path": difference,
            "native_settlement": "SETTLEMENT_UNVERIFIED",
        }
        result["source_windows"].append(window)
        if difference is None:
            result["stable_post_card_windows"] += 1
            if any(row.get("type") == "nested_choice_pause"
                   and row.get("action_id") == action_id
                   and start_ordinal < row.get("ordinal", -1) < end_ordinal
                   for row in records if isinstance(row, dict)):
                result["nested_choice_stable_windows"] += 1
        else:
            result["changed_post_card_windows"] += 1
    if result["source_completed_card_actions"]:
        result["status"] = "NATIVE_POST_CARD_WINDOWS_OBSERVED_UNCERTIFIED"
    return result
