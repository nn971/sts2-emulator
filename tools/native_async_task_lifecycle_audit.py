"""Validate passive native Task-reference lifecycle observations.

Five pinned v0.111 entry points are instrumented only when the optional
native task hook is enabled. Registers and completion are sampled on frames;
this audit never interprets an empty *covered* set as global settlement.
"""
from __future__ import annotations

from collections import Counter

FAMILIES = frozenset((
    "GAME_ACTION_BODY", "GAME_ACTION_STEP", "HOOK_TASK_BODY",
    "HOOK_PAUSE_WAIT", "HOOK_COMPLETION_BARRIER", "TURN_LOOP",
))
STATUSES = frozenset(("PENDING", "FAULTED", "CANCELED", "RAN_TO_COMPLETION"))
COVERAGE = "NAMED_ASYNC_ENTRYPOINTS_ONLY"
BASIS = "PASSIVE_TASK_REFERENCE_POLLED_AT_FRAME"


def audit_native_task_lifecycle(records: list[dict]) -> dict:
    result = {
        "status": "NOT_CAPTURED_OPTIONAL",
        "task_hook_installed": False,
        "task_references_registered": 0,
        "task_references_observed_complete": 0,
        "pending_at_teardown": None,
        "player_checkpoint_count": 0,
        "player_checkpoint_covered_counts": [],
        "registration_overflow": 0,
        "source_task_inventory_exhaustive": False,
        "native_settlement_certified": False,
        "errors": [],
    }
    if not records or not isinstance(records[0], dict):
        return result
    header = records[0].get("data")
    installed = isinstance(header, dict) and (
        header.get("native_async_task_hook_installed") is True)
    result["task_hook_installed"] = installed
    if not installed:
        if any(isinstance(r, dict) and r.get("type") in
               ("native_async_task_lifecycle", "native_async_task_checkpoint")
               for r in records):
            result["errors"].append("Task events present without installed hook provenance")
            result["status"] = "INVALID_NATIVE_TASK_LIFECYCLE"
        return result

    opened: dict[int, tuple[str, str]] = {}
    registered = 0
    completed = 0
    last_overflow = 0
    last_ordinal = 0
    last_player_turn = None
    observed_frames: set[int] = set()
    expected_frames = {
        (r.get("data") or {}).get("turn")
        for r in records if isinstance(r, dict)
        and r.get("type") == "native_turn_checkpoint"
        and isinstance(r.get("data"), dict)
        and r["data"].get("kind") == "PLAYER_TURN_START"
        and type(r["data"].get("turn")) is int
    }
    prior: dict | None = None
    teardown_seen = False
    for row in records:
        if not isinstance(row, dict):
            result["errors"].append("Non-object task source record")
            continue
        typ = row.get("type")
        data = row.get("data")
        if typ not in ("native_async_task_lifecycle", "native_async_task_checkpoint"):
            prior = row
            continue
        ordinal = row.get("ordinal")
        if (type(ordinal) is not int or ordinal <= last_ordinal
            or not isinstance(data, dict)
            or data.get("source_version") != "v0.111.0"
            or data.get("observation_basis") != BASIS
            or data.get("native_settlement_certified") is not False
            or data.get("native_task_mutations") != 0):
            result["errors"].append(f"Invalid task event provenance at {ordinal}")
            prior = row
            continue
        last_ordinal = ordinal
        if teardown_seen:
            result["errors"].append(f"Task record after teardown at {ordinal}")
        if typ == "native_async_task_lifecycle":
            task_id = data.get("task_id")
            family = data.get("family")
            source = data.get("source")
            phase = data.get("phase")
            status = data.get("task_status")
            if (type(task_id) is not int or task_id <= 0
                or family not in FAMILIES
                or not isinstance(source, str) or not source
                or phase not in ("registered", "completed")
                or status not in STATUSES):
                result["errors"].append(f"Invalid task lifecycle payload at {ordinal}")
            elif phase == "registered":
                if task_id != registered + 1 or task_id in opened:
                    result["errors"].append(f"Non-monotone task register at {ordinal}")
                else:
                    opened[task_id] = (family, source)
                    registered += 1
            elif task_id not in opened:
                result["errors"].append(f"Orphan task completion at {ordinal}")
            elif (family, source) != opened[task_id] or status == "PENDING":
                result["errors"].append(f"Invalid task completion at {ordinal}")
            else:
                del opened[task_id]
                completed += 1
        else:
            site = data.get("site")
            turn = data.get("turn")
            pending = data.get("tracked_pending_task_count")
            by_family = data.get("tracked_pending_by_family")
            ids = data.get("pending_task_ids")
            totals = (data.get("registered_task_count"),
                      data.get("observed_completed_task_count"))
            overflow = data.get("registration_overflow")
            if (site not in ("PLAYER_TURN_START", "COMBAT_TEARDOWN")
                or data.get("coverage") != COVERAGE
                or data.get("exhaustive_native_task_inventory") is not False
                or type(pending) is not int or pending < 0
                or not isinstance(by_family, dict)
                or not isinstance(ids, list)
                or any(type(x) is not int for x in ids)
                or ids != sorted(opened)
                or len(ids) != pending
                or totals != (registered, completed)
                or type(overflow) is not int or overflow < last_overflow
                or any(f not in FAMILIES or type(v) is not int or v < 0
                       for f,v in by_family.items())
                or by_family != dict(Counter(f for f, _ in opened.values()))):
                result["errors"].append(f"Invalid task checkpoint counts at {ordinal}")
            if type(overflow) is int and overflow >= 0:
                last_overflow = overflow
            if site == "PLAYER_TURN_START":
                if (type(turn) is not int or turn < 1
                    or prior is None
                    or prior.get("type") != "native_turn_checkpoint"
                    or (prior.get("data") or {}).get("kind") != "PLAYER_TURN_START"
                    or (prior.get("data") or {}).get("turn") != turn
                    or type(prior.get("ordinal")) is not int
                    or prior["ordinal"] + 1 != ordinal
                    or turn in observed_frames):
                    result["errors"].append(
                        f"Task checkpoint not paired with player frame at {ordinal}")
                else:
                    observed_frames.add(turn)
                    result["player_checkpoint_covered_counts"].append({
                        "turn": turn,
                        "ordinal": ordinal,
                        "tracked_pending_task_count": pending,
                        "observed_covered_task_quiescence": pending == 0
                            and overflow == 0,
                        "native_settlement_certified": False,
                    })
            elif turn is not None:
                result["errors"].append(f"Teardown task checkpoint has turn at {ordinal}")
            else:
                teardown_seen = True
                result["pending_at_teardown"] = pending
        prior = row

    if expected_frames != observed_frames:
        result["errors"].append(
            "Missing or unpaired player turn task checkpoint")
    if (any(r.get("type") == "terminal" for r in records
            if isinstance(r, dict)) and not teardown_seen):
        result["errors"].append("Missing task checkpoint at combat teardown")
    result["task_references_registered"] = registered
    result["task_references_observed_complete"] = completed
    result["player_checkpoint_count"] = len(observed_frames)
    result["registration_overflow"] = last_overflow
    result["status"] = (
        "INVALID_NATIVE_TASK_LIFECYCLE" if result["errors"]
        else "SOURCE_TRACKED_TASK_LIFECYCLE_PARTIAL"
        if last_overflow else "SOURCE_TRACKED_TASK_LIFECYCLE_OBSERVED_UNCERTIFIED"
    )
    return result
