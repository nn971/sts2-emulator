"""Validate pinned-v0.111 CombatManager turn callback chronology.

TurnEnded is fired AFTER switching CurrentSide. TurnStarted is fired after
start-of-turn hooks (and after PlayPhase is enabled for the player), while
AboutToSwitchToEnemyTurn is fired BEFORE phase-two cleanup. This audit never
infers complete automatic-effect settlement from the callback sequence.
"""
from __future__ import annotations


CALLBACKS = {
    "ABOUT_TO_SWITCH_TO_ENEMY_TURN": "CombatManager.AboutToSwitchToEnemyTurn",
    "SIDE_SWITCHED": "CombatManager.TurnEnded",
    "TURN_STARTED": "CombatManager.TurnStarted",
}
BOOLEANS = (
    "ending_player_turn_phase_one",
    "ending_player_turn_phase_two",
    "enemy_turn_started",
)


def audit_phase_handoffs(records: list[dict]) -> dict:
    """Return explicit observed phase chronology, accepting older sidecars."""
    result = {
        "status": "NOT_CAPTURED_LEGACY",
        "source_events": 0,
        "completed_handoffs": 0,
        "extra_player_turn_handoffs": 0,
        "awaited_source_milestones": [],
        "player_start_hook_pauses_observed": 0,
        "player_start_hook_resumes_observed": 0,
        "player_start_hook_resolutions_without_frame": 0,
        "pending": None,
        "errors": [],
    }
    pending = None
    pending_turn = None
    pending_extra = False
    last_event = None
    for record in records:
        if record.get("type") == "native_turn_checkpoint":
            data = record.get("data") or {}
            if (pending == "AWAIT_PLAYER_START_HOOK_COMPLETION"
                and data.get("kind") == "PLAYER_TURN_START"
                and data.get("turn") == pending_turn
                and data.get("player_phase") == "Play"):
                result["player_start_hook_resumes_observed"] += 1
                if last_event is not None:
                    result["completed_handoffs"] += 1
                    if pending_extra:
                        result["extra_player_turn_handoffs"] += 1
                result["awaited_source_milestones"].append({
                    "ordinal": record.get("ordinal"), "turn": pending_turn,
                    "milestone": "PLAYER_AUTO_PREPLAY_REACHED_PLAY_AT_FRAME",
                    "settlement_status": "SETTLEMENT_UNVERIFIED",
                })
                pending, pending_turn, pending_extra = None, None, False
            continue
        if record.get("type") != "native_phase_handoff":
            continue
        result["source_events"] += 1
        ordinal = record.get("ordinal")
        data = record.get("data")
        failure = None
        milestone = None
        if not isinstance(data, dict):
            failure = "missing callback data"
        else:
            event = data.get("source_event")
            side = data.get("native_side")
            phase = data.get("player_phase")
            sync = data.get("synchronizer_state")
            turn = data.get("turn")
            one = data.get("ending_player_turn_phase_one")
            two = data.get("ending_player_turn_phase_two")
            enemy = data.get("enemy_turn_started")
            if (event not in CALLBACKS
                or data.get("source_callback") != CALLBACKS.get(event)
                or data.get("source_version") != "v0.111.0"
                or data.get("observation_basis") != "NATIVE_COMBAT_MANAGER_CALLBACK"
                or data.get("settlement_status") != "SETTLEMENT_UNVERIFIED"
                or data.get("native_phase_handoff_order_verified") is not False
                or data.get("rng_draws_performed_by_inspector") != 0
                or type(turn) is not int or turn <= 0
                or side not in ("Player", "Enemy")
                or phase not in ("None", "Start", "AutoPrePlay", "Play",
                                  "AutoPostPlay", "End")
                or sync not in ("PlayPhase", "NotPlayPhase", "EndTurnPhaseOne")
                or any(type(data.get(key)) is not bool for key in BOOLEANS)
                or record.get("action_id") is not None
                or record.get("state") is not None):
                failure = "invalid callback provenance or payload"
            elif one:
                failure = "phase-one flag still set at handoff callback"
            elif event == "ABOUT_TO_SWITCH_TO_ENEMY_TURN":
                # StartTurn can signal TurnStarted while a HookPlayerChoiceContext
                # is paused for a nested choice. If no later playable frame was
                # sampled, End-phase source flags can still advance chronology,
                # but the missing resolution evidence remains counted.
                if (pending == "AWAIT_PLAYER_START_HOOK_COMPLETION"
                    and turn == pending_turn):
                    result["player_start_hook_resolutions_without_frame"] += 1
                    pending, pending_turn, pending_extra = None, None, False
                if (side != "Player" or phase != "End" or sync != "NotPlayPhase"
                    or not two or enemy):
                    failure = "invalid pre-handoff source phase"
                elif pending is not None:
                    failure = "handoff began before previous callback chain completed"
                else:
                    pending, pending_turn = "AWAIT_SIDE_SWITCH", turn
                    milestone = "PLAYER_PHASE_ONE_AWAITED_HOOKS_RETURNED"
            elif event == "SIDE_SWITCHED":
                if sync != "NotPlayPhase":
                    failure = "side switched outside NotPlayPhase"
                elif pending == "AWAIT_SIDE_SWITCH":
                    if (side == "Enemy" and turn == pending_turn
                        and two and not enemy and phase == "End"):
                        pending = "AWAIT_ENEMY_START"
                        milestone = "PLAYER_PHASE_TWO_AWAITED_HOOKS_RETURNED"
                    elif (side == "Player" and turn == pending_turn + 1
                          and two and not enemy and phase == "End"):
                        pending, pending_turn = "AWAIT_EXTRA_PLAYER_START", turn
                        pending_extra = True
                        milestone = "PLAYER_PHASE_TWO_RETURNED_EXTRA_TURN_HOOK_PENDING"
                    else:
                        failure = "invalid player-to-next-side transition"
                elif (pending == "AWAIT_PLAYER_SWITCH"
                      and side == "Player" and not two and enemy
                      and phase == "None" and turn == pending_turn + 1):
                    pending, pending_turn = "AWAIT_PLAYER_START", turn
                    pending_extra = False
                    milestone = "ENEMY_ACTIONS_AND_END_HOOKS_AWAITED_RETURNED"
                else:
                    failure = "unpaired or out-of-order side switch"
            elif event == "TURN_STARTED":
                if side == "Enemy":
                    if (phase != "None" or sync != "NotPlayPhase" or not enemy
                        or two or pending != "AWAIT_ENEMY_START"
                        or turn != pending_turn):
                        failure = "unexpected enemy turn start"
                    else:
                        pending = "AWAIT_PLAYER_SWITCH"
                        milestone = "ENEMY_START_HOOKS_AWAITED_RETURNED"
                else:
                    allowed_predecessor = (
                        pending in ("AWAIT_PLAYER_START", "AWAIT_EXTRA_PLAYER_START")
                        and turn == pending_turn)
                    opening_callback = pending is None and last_event is None
                    if sync != "PlayPhase" or enemy or two:
                        failure = "player start outside synchronized play state"
                    elif not (allowed_predecessor or opening_callback):
                        failure = "unexpected player turn start"
                    elif phase in ("Start", "AutoPrePlay"):
                        # HookPlayerChoiceContext can return on pause before
                        # its task completes; TurnStarted does not await it.
                        result["player_start_hook_pauses_observed"] += 1
                        pending_extra = pending == "AWAIT_EXTRA_PLAYER_START"
                        pending, pending_turn = "AWAIT_PLAYER_START_HOOK_COMPLETION", turn
                    elif phase == "Play":
                        if allowed_predecessor:
                            result["completed_handoffs"] += 1
                            if pending == "AWAIT_EXTRA_PLAYER_START":
                                result["extra_player_turn_handoffs"] += 1
                        pending, pending_turn, pending_extra = None, None, False
                        milestone = "PLAYER_SETUP_AND_AUTO_PREPLAY_AWAITED_RETURNED"
                    else:
                        failure = "player start in unsupported phase"
            if failure is None:
                last_event = event
                if milestone is not None:
                    result["awaited_source_milestones"].append({
                        "ordinal": ordinal, "turn": turn, "milestone": milestone,
                        "settlement_status": "SETTLEMENT_UNVERIFIED",
                    })
        if failure is not None:
            result["errors"].append(f"ordinal {ordinal}: {failure}")
    result["pending"] = pending
    if result["errors"]:
        result["status"] = "INVALID_PHASE_HANDOFF_CHRONOLOGY"
    elif result["source_events"]:
        result["status"] = ("SOURCE_PHASE_HANDOFF_ORDER_OBSERVED"
                            if result["completed_handoffs"]
                            else "SOURCE_PHASE_CALLBACKS_PARTIAL")
    return result
