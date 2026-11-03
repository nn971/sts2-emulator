"""Source-only player-turn awaited-hook/queue-frame evidence, not settlement.

Pinned v0.111 CombatManager phase callbacks occur after *specific* awaited
hooks return. A later PLAY frame independently samples the native action queue
and three tracked effect flags. This audit checks the entire ordered source
chain before describing the combination as a bounded witness. Neither the
callbacks nor an idle frame proves future async effects cannot enqueue work.
"""
from __future__ import annotations

from native_phase_handoff_audit import audit_phase_handoffs

_IDLE_QUEUE = "SOURCE_QUEUE_AND_EXECUTOR_QUIESCENT_AT_FRAME"
_IDLE_EFFECT = "SOURCE_TRACKED_EFFECT_BODY_INACTIVE_AT_FRAME"
_PLAYER_SETUP = "PLAYER_SETUP_AND_AUTO_PREPLAY_AWAITED_RETURNED"
_PLAY_AFTER_PAUSE = "PLAYER_AUTO_PREPLAY_REACHED_PLAY_AT_FRAME"


def audit_awaited_turn_witnesses(records: list[dict],
                                turn_candidates: list[dict],
                                phase_report: dict | None = None) -> dict:
    phase = phase_report if phase_report is not None else audit_phase_handoffs(records)
    milestones = phase.get("awaited_source_milestones", [])
    starts = {
        (row.get("data") or {}).get("turn"): row.get("ordinal")
        for row in records if isinstance(row, dict)
        and row.get("type") == "native_phase_handoff"
        and (row.get("data") or {}).get("source_event") == "TURN_STARTED"
        and (row.get("data") or {}).get("native_side") == "Player"
    }

    result = {
        "status": "NOT_CAPTURED",
        "player_turns_observed": 0,
        "bounded_awaited_and_idle_frame_witnesses": 0,
        "normal_enemy_turn_chains": 0,
        "extra_player_turn_chains": 0,
        "turns_without_full_witness": 0,
        "native_settlement_certified": False,
        "arbitrary_delayed_effects_excluded": False,
        "witnesses": [],
    }
    for cp in turn_candidates:
        if cp.get("kind") != "PLAYER_TURN_START":
            continue
        result["player_turns_observed"] += 1
        turn, end = cp.get("turn"), cp.get("ordinal")
        reasons = []
        callback = starts.get(turn)
        if (type(turn) is not int or type(end) is not int
            or type(callback) is not int or callback >= end):
            reasons.append("PLAYER_TURN_STARTED_CALLBACK_NOT_OBSERVED_BEFORE_FRAME")

        # Each milestone has precise source ordinal and semantics. In
        # particular, a TurnStarted callback observed during AutoPrePlay
        # does not imply its nested hook task is already finished.
        def occurrence(name, t, *, before):
            matches = [
                x["ordinal"] for x in milestones
                if x.get("milestone") == name and x.get("turn") == t
                and type(x.get("ordinal")) is int
                and x["ordinal"] <= before
            ]
            return max(matches) if matches else None

        setup = occurrence(_PLAYER_SETUP, turn, before=end)
        resumed = occurrence(_PLAY_AFTER_PAUSE, turn, before=end)
        player_ready = setup if setup is not None else resumed
        if player_ready is None or (
            callback is not None and player_ready < callback):
            reasons.append("PLAYER_PLAYABLE_POST_HOOK_MILESTONE_UNOBSERVED")
        if (cp.get("queue_quiescence_status") != _IDLE_QUEUE
            or cp.get("automatic_effect_evidence_status") != _IDLE_EFFECT):
            reasons.append("FRAME_QUEUE_OR_TRACKED_EFFECT_NOT_IDLE")
        if phase.get("errors"):
            reasons.append("SOURCE_PHASE_HANDOFF_AUDIT_INVALID")

        chain = "INITIAL_PLAYER_TURN"
        if type(turn) is int and turn > 1:
            prior = turn - 1
            one = occurrence("PLAYER_PHASE_ONE_AWAITED_HOOKS_RETURNED",
                             prior, before=end)
            two = occurrence("PLAYER_PHASE_TWO_AWAITED_HOOKS_RETURNED",
                             prior, before=end)
            enemy = occurrence("ENEMY_START_HOOKS_AWAITED_RETURNED",
                               prior, before=end)
            returned = occurrence("ENEMY_ACTIONS_AND_END_HOOKS_AWAITED_RETURNED",
                                  turn, before=end)
            extra = occurrence("PLAYER_PHASE_TWO_RETURNED_EXTRA_TURN_HOOK_PENDING",
                               turn, before=end)
            if (all(type(x) is int for x in
                    (one, two, enemy, returned, player_ready))
                and one < two < enemy < returned <= player_ready):
                chain = "NORMAL_ENEMY_TURN_AWAITED_CHAIN"
            elif (all(type(x) is int for x in (one, extra, player_ready))
                  and one < extra <= player_ready):
                chain = "EXTRA_PLAYER_TURN_AWAITED_CHAIN"
            else:
                chain = "TURN_HANDOFF_AWAIT_CHAIN_UNVERIFIED"
                reasons.append(chain)

        observed = not reasons
        if observed:
            result["bounded_awaited_and_idle_frame_witnesses"] += 1
            if chain == "NORMAL_ENEMY_TURN_AWAITED_CHAIN":
                result["normal_enemy_turn_chains"] += 1
            elif chain == "EXTRA_PLAYER_TURN_AWAITED_CHAIN":
                result["extra_player_turn_chains"] += 1
        else:
            result["turns_without_full_witness"] += 1
        result["witnesses"].append({
            "turn": turn, "player_checkpoint_ordinal": end,
            "turn_started_callback_ordinal": callback,
            "player_playable_milestone_ordinal": player_ready,
            "handoff_kind": chain,
            "observation": ("AWAITED_CHAIN_AND_TRACKED_IDLE_FRAME_OBSERVED"
                            if observed else "BOUNDARY_WITNESS_INCOMPLETE"),
            "unverified_reasons": reasons,
            "native_settlement": "SETTLEMENT_UNVERIFIED",
        })
    if result["player_turns_observed"]:
        result["status"] = ("SOURCE_BOUNDED_TURN_WITNESSES_OBSERVED_UNCERTIFIED"
                            if not result["turns_without_full_witness"]
                            else "SOURCE_BOUNDED_TURN_WITNESSES_PARTIAL")
    return result
