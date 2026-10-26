#!/usr/bin/env python3
"""Matched two-site Nature/control rollouts over the fair semantic API.

This is an emulator experiment harness, not a learned-policy trainer. The
same player policy instance contract and seed are used for each paired arm;
the Nature arm resolves only events visible through its Nature boundary, and
the control arm leaves ordinary randomness with the engine-owned baseline.
By default, a supported source-route seed for the selected region is replayed
in fifteen isolated process pairs. Pass one or more ``--seed`` values to
select another cohort.

An optional policy module may export ``make_policy(seed)``. The returned
object must implement:

* ``choose_player_action(observation, legal_actions) -> action_id``
* ``choose_nature_outcome(event) -> outcome``

For ``enemy_intent``, outcome is an intent ID. For ``draw_card``, it is a
physical card instance ID. Other Nature event kinds accept an outcome ID;
``enemy_action_chance`` may also return a saved candidate label.
"""

from __future__ import annotations

import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
from pathlib import Path
import statistics
import sys
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"
COMMAND = ["dotnet", str(DLL), "prototype-ai-jsonl"]
MAX_ACTION_STEPS = 1200
MAX_NATURE_EVENTS_PER_TURN = 100


class FirstLegalPolicy:
    """Small deterministic policy for exercising the paired harness."""

    def choose_player_action(self, observation: dict,
                             legal_actions: list[dict]) -> str:
        hand = observation.get("combat", {}).get("hand", [])
        card_ids = {card.get("instance_id"): card.get("card_id")
                    for card in hand}
        plays = [action for action in legal_actions
                 if action.get("kind") == "play_card"]
        strikes = [action for action in plays
                   if card_ids.get(_action_card_instance(action)) ==
                   "proto.silent.strike"]
        defends = [action for action in plays
                   if card_ids.get(_action_card_instance(action)) ==
                   "proto.silent.defend"]
        chosen = (strikes or defends or
                  [action for action in legal_actions
                   if action.get("kind") == "end_turn"] or legal_actions)
        if not chosen:
            raise RuntimeError("Player boundary has no legal action")
        return chosen[0]["actionId"]

    def choose_nature_outcome(self, event: dict) -> Any:
        kind = event.get("kind")
        if kind == "enemy_intent":
            candidates = event.get("semanticCandidates") or []
            if not candidates:
                raise RuntimeError("Nature intent event has no candidates")
            return candidates[0]["intentId"]
        if kind == "draw_card":
            candidates = event.get("candidates") or []
            if not candidates:
                raise RuntimeError("Nature draw event has no candidates")
            return candidates[0]["cardInstanceId"]
        semantic_ids = event.get("semanticOutcomeIds") or []
        if semantic_ids:
            return semantic_ids[0]
        labels = event.get("candidateLabels") or []
        if labels:
            return labels[0]
        raise RuntimeError(
            f"No default outcome policy for Nature event {kind!r}")


def _action_card_instance(action: dict) -> int | None:
    try:
        payload = json.loads(action.get("payloadJson", "{}"))
        return payload.get("CardInstanceId")
    except (TypeError, ValueError):
        return None


def _load_policy_factory(path: str | None):
    if path is None:
        return lambda _seed: FirstLegalPolicy()
    policy_path = Path(path).expanduser().resolve()
    if not policy_path.is_file():
        raise FileNotFoundError(f"Policy module does not exist: {policy_path}")
    spec = importlib.util.spec_from_file_location(
        "nature_semantic_ab_policy", policy_path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Cannot load policy module: {policy_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    factory = getattr(module, "make_policy", None)
    if not callable(factory):
        raise TypeError("Policy module must define make_policy(seed)")
    return factory


def _resolve_nature_event(combat, event: dict, outcome: Any):
    kind = event.get("kind")
    if kind == "enemy_intent":
        return combat.resolve_intent(str(outcome))
    if kind == "draw_card":
        if isinstance(outcome, bool) or not isinstance(outcome, int):
            raise TypeError("draw_card policy outcome must be an integer ID")
        return combat.resolve_draw_card(outcome)
    if kind == "enemy_action_chance" and isinstance(outcome, str) and \
            outcome in (event.get("candidateLabels") or []):
        return combat.resolve_candidate_label(outcome)
    if isinstance(outcome, str):
        return combat.resolve_outcome_id(outcome)
    raise TypeError(
        f"Policy outcome for {kind!r} must be an event-specific ID")


def _run_arm(seed: str, region: str, *, native_policy_baseline: bool,
             policy_factory):
    # Delayed import keeps --help and static checks independent of the Python
    # binding's optional package setup.
    sys.path.insert(0, str(ROOT / "bindings/python"))
    from sts2_emulator import Session

    policy = policy_factory(seed)
    required = ("choose_player_action", "choose_nature_outcome")
    if any(not callable(getattr(policy, name, None)) for name in required):
        raise TypeError("Policy object is missing a required decision method")

    with Session(COMMAND) as session:
        combat = session.begin_seeded_semantic_route(
            seed, region=region,
            native_policy_baseline=native_policy_baseline)
        initial_status = combat.status()
        opening_fingerprint = initial_status["sourceOpeningFingerprint"]
        initial_route_fingerprint = initial_status.get(
            "sourceRouteFingerprint")
        initial_observation = None
        initial_public_observation = None
        action_ids: list[str] = []
        nature_kinds: Counter[str] = Counter()
        max_pending_events_in_turn = 0
        pending_events_in_turn = 0

        for _ in range(MAX_ACTION_STEPS):
            if combat.terminal:
                break
            if combat.boundary == "nature":
                event = combat.pending
                if not isinstance(event, dict):
                    raise RuntimeError("Nature boundary has no pending event")
                pending_events_in_turn += 1
                if pending_events_in_turn > MAX_NATURE_EVENTS_PER_TURN:
                    raise RuntimeError("Nature event chain exceeded its guard")
                nature_kinds[str(event.get("kind"))] += 1
                outcome = policy.choose_nature_outcome(event)
                combat = _resolve_nature_event(combat, event, outcome)
                continue

            if combat.boundary in ("player", "player_choice"):
                pending_events_in_turn = 0
                observation = combat.observe()
                if initial_observation is None:
                    initial_observation = observation
                    initial_public_observation = _without_enemy_intents(
                        observation)
                legal_actions = combat.actions()
                if combat.boundary == "player_choice":
                    event = combat.pending or {}
                    if event.get("kind") != "player_card_selection":
                        raise RuntimeError(
                            f"Unsupported player-choice boundary: {event!r}")
                action_id = policy.choose_player_action(
                    observation, legal_actions)
                if not isinstance(action_id, str) or action_id not in {
                        action.get("actionId") for action in legal_actions}:
                    raise ValueError(
                        "Player policy returned an action outside the fair "
                        "legal-action menu")
                action_ids.append(action_id)
                combat = (combat.resolve_card_selection(action_id)
                          if combat.boundary == "player_choice"
                          else combat.step(action_id))
                continue

            raise RuntimeError(
                f"Unsupported semantic boundary: {combat.boundary!r}")

        if not combat.terminal:
            raise RuntimeError(
                f"Run did not terminate within {MAX_ACTION_STEPS} actions")
        final_status = combat.status()
        if combat.pending is not None:
            raise RuntimeError("Terminal run retained a pending choice")
        if initial_observation is None:
            raise RuntimeError("Run never reached a fair player boundary")
        if session.load_semantic_snapshot(
                combat.save_snapshot()).fingerprint != combat.fingerprint:
            raise RuntimeError("Terminal semantic snapshot failed replay")
        return {
            "seed": seed,
            "region": region,
            "arm": "engine_owned_control" if native_policy_baseline
                else "two_site_nature",
            "sourceOpeningFingerprint": opening_fingerprint,
            "sourceRouteFingerprint": initial_route_fingerprint,
            "initialPublicObservation": initial_public_observation,
            "actionIds": action_ids,
            "natureEventCounts": dict(sorted(nature_kinds.items())),
            "outcome": combat.outcome,
            "exitHp": combat.exit_hp,
            "playerActionCount": len(action_ids),
            "nativePolicyDecisionCount": final_status.get(
                "nativePolicyDecisionCount", 0),
            "otherChanceDecisionCount": final_status.get(
                "otherChanceDecisionCount", 0),
            "fingerprint": combat.fingerprint,
        }


def _without_enemy_intents(observation: dict) -> str:
    public = json.loads(json.dumps(observation))
    for enemy in public.get("combat", {}).get("enemies", []):
        for key in list(enemy):
            if key.replace("_", "").lower() in {
                    "moveid", "intentdamage", "intenthits",
                    "intentbasedamage"}:
                enemy.pop(key)
    return json.dumps(public, sort_keys=True, separators=(",", ":"))


def _run_pair(seed: str, region: str, policy_factory):
    nature = _run_arm(seed, region, native_policy_baseline=False,
                      policy_factory=policy_factory)
    control = _run_arm(seed, region, native_policy_baseline=True,
                       policy_factory=policy_factory)
    if nature["sourceOpeningFingerprint"] != \
            control["sourceOpeningFingerprint"]:
        raise AssertionError(
            f"Paired arms have different source openings for {seed!r}")
    if nature["initialPublicObservation"] != \
            control["initialPublicObservation"]:
        raise AssertionError(
            f"Paired fair openings differ for {seed!r}")
    return {"seed": seed, "region": region,
            "nature": nature, "control": control}


def _summarize(pairs: list[dict]) -> dict:
    result = {}
    for arm in ("nature", "control"):
        rows = [pair[arm] for pair in pairs]
        outcomes = Counter(row["outcome"] for row in rows)
        hp_values = [row["exitHp"] for row in rows
                     if isinstance(row["exitHp"], int)]
        result[arm] = {
            "outcomes": dict(sorted(outcomes.items())),
            "meanExitHp": statistics.fmean(hp_values) if hp_values else None,
            "meanPlayerActions": statistics.fmean(
                row["playerActionCount"] for row in rows),
            "meanNatureEvents": statistics.fmean(
                sum(row["natureEventCounts"].values()) for row in rows),
            "meanNativePolicyDecisions": statistics.fmean(
                row["nativePolicyDecisionCount"] for row in rows),
            "meanSemanticOtherChanceDecisions": statistics.fmean(
                row["otherChanceDecisionCount"] for row in rows),
        }
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", dest="seeds", action="append",
                        help="source-route seed; may be repeated")
    parser.add_argument("--repeats", type=int, default=15,
                        help="isolated paired replays per seed (default: 15)")
    parser.add_argument("--region", choices=("Overgrowth", "Underdocks"),
                        default="Overgrowth")
    parser.add_argument("--workers", type=int, default=15)
    parser.add_argument("--policy-module", help="Python file exporting make_policy(seed)")
    args = parser.parse_args()
    default_seeds = {
        "Overgrowth": "nature-control-ab-000",
        "Underdocks": "route-audit-01",
    }
    seeds = args.seeds or [default_seeds[args.region]]
    if any(not seed.strip() for seed in seeds):
        parser.error("--seed must not be empty")
    if len(set(seeds)) != len(seeds):
        parser.error("--seed values must be unique")
    if args.repeats < 1:
        parser.error("--repeats must be positive")
    if args.workers < 1 or args.workers > 15:
        parser.error("--workers must be between 1 and 15")
    if not DLL.is_file():
        parser.error(
            f"Missing Release CLI build: {DLL}. Build Sts2Emulator.Cli first.")

    policy_factory = _load_policy_factory(args.policy_module)
    task_seeds = [seed for seed in seeds for _ in range(args.repeats)]
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        repeated_pairs = list(pool.map(
            lambda seed: _run_pair(seed, args.region, policy_factory),
            task_seeds))

    # Repeated processes with the same source seed must reproduce both
    # provider-specific trajectories exactly. Aggregate each seed once so
    # isolated replay count is not mistaken for a larger sample size.
    pairs_by_seed: dict[str, dict] = {}
    replay_checks = []
    for pair in repeated_pairs:
        baseline = pairs_by_seed.setdefault(pair["seed"], pair)
        if pair != baseline:
            raise AssertionError(
                f"Independent paired replay diverged for {pair['seed']!r}")
        replay_checks.append({
            "seed": pair["seed"],
            "natureFingerprint": pair["nature"]["fingerprint"],
            "controlFingerprint": pair["control"]["fingerprint"],
            "matchedBaseline": True,
        })
    pairs = list(pairs_by_seed.values())

    # The report excludes raw observations and private provider transcripts;
    # it retains only paired source identity and aggregate outcomes.
    report = {
        "schemaId": "sts2-v111-nature-control-ab-report-v1",
        "experiment": "matched_two_site_nature_vs_engine_owned_control",
        "playerPolicy": ("first_legal_strike_defend_end_turn"
                         if args.policy_module is None
                         else str(Path(args.policy_module).resolve())),
        "naturePolicy": "policy_module" if args.policy_module else
            "first_supported_outcome",
        "seeds": seeds,
        "region": args.region,
        "workerCount": args.workers,
        "repetitionsPerSeed": args.repeats,
        "isolatedProcessPairCount": len(repeated_pairs),
        "pairedRuns": len(pairs),
        "summary": _summarize(pairs),
        "runs": [{
            "seed": pair["seed"],
            "region": pair["region"],
            "sourceOpeningFingerprint": pair["nature"][
                "sourceOpeningFingerprint"],
            "nature": {key: value for key, value in pair["nature"].items()
                       if key not in {"initialPublicObservation", "actionIds"}},
            "control": {key: value for key, value in pair["control"].items()
                        if key not in {"initialPublicObservation", "actionIds"}},
        } for pair in pairs],
        "replayChecks": replay_checks,
        "probabilityStatus": "UNVERIFIED",
        "decisionCountScope": (
            "NativePolicyAudit and SemanticOtherChance transcripts only; "
            "engine-owned legacy reward fallback draws are not counted"),
        "trainingEvidence": False,
    }
    print(json.dumps(report, sort_keys=True, separators=(",", ":")))


if __name__ == "__main__":
    main()
