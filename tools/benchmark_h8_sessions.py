#!/usr/bin/env python3
"""Paired H8 A10 versioned-session throughput benchmark (scripted actor only).

This measures emulator+JSONL execution of the *implemented H8 surface*.
It DOES NOT run the 14.66M-parameter H7 Transformer, cover the 16-cell H7
curriculum, or measure gradient updates. Unsupported and capped partial
sessions are explicitly reported, never counted as complete combats.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
from collections import Counter
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from typing import Any


def canonical_hash(value: Any) -> str:
    return hashlib.sha256(json.dumps(
        value, sort_keys=True, separators=(",", ":"),
        ensure_ascii=True).encode()).hexdigest()


def split_shards(seeds: list[str], workers: int) -> list[list[str]]:
    """One persistent emulator subprocess per nonempty shard."""
    return [seeds[i::workers] for i in range(min(workers, len(seeds)))]


def _choose(seed: str, ordinal: int, kind: str, choices: list[Any]) -> Any:
    if not choices:
        raise RuntimeError(f"Empty {kind} support at {seed}:{ordinal}")
    ticket = int.from_bytes(hashlib.sha256(
        f"{seed}:{ordinal}:{kind}".encode()).digest()[:8], "little")
    return choices[ticket % len(choices)]


def _run_shard(
    seeds: list[str], dll: str, bindings: str, mode: str,
    max_boundaries: int, audit_every: int,
) -> list[dict[str, Any]]:
    # Import in the child: the parent process never touches a live .NET
    # session and each worker exclusively owns its one JSONL subprocess.
    sys.path.insert(0, bindings)
    from sts2_emulator import Session

    results = []
    with Session(["dotnet", dll, "prototype-ai-jsonl"]) as session:
        for index, seed in enumerate(seeds):
            native = mode == "native"
            begin = time.perf_counter()
            state = session.call(
                "h8_begin_seeded",
                seed=seed, act=1,
                encounter_id="proto.encounter.mawler_normal",
                native_entry="MawlerNormal", total_floor=2,
                ascension=10, first_monster_combat_id=1,
                map_column=3, map_row=1,
                replay_opening_draw_hooks=True,
                use_m2_automatic_draw=native,
                use_native_draw_transition=native,
                use_native_player_end_turn=native,
                use_trusted_intent=native,
            )["result"]
            handle = state["h8Handle"]
            kinds: Counter[str] = Counter()
            actions: list[tuple[str, Any]] = []
            status = "capped"
            try:
                for ordinal in range(max_boundaries):
                    kind = state["kind"]
                    if kind in ("terminal", "unsupported"):
                        status = kind
                        break
                    token = dict(
                        h8_handle=handle,
                        runtime_id=state["runtimeId"],
                        boundary_version=state["boundaryVersion"],
                    )
                    if kind == "enemy_intent":
                        choice = _choose(
                            seed, ordinal, kind, state["intentIds"])
                        nxt = session.call(
                            "h8_resolve_intent", **token,
                            intent_id=choice,
                            provider_id="scripted-h8-throughput",
                        )["result"]
                    elif kind == "draw_card":
                        choice = _choose(
                            seed, ordinal, kind, state["cardInstanceIds"])
                        nxt = session.call(
                            "h8_resolve_draw_card", **token,
                            card_instance_id=choice,
                            provider_id="scripted-h8-throughput",
                        )["result"]
                    elif kind == "player":
                        choice = state["endTurnActionId"]
                        if choice not in state["playerActionIds"]:
                            raise RuntimeError(
                                f"Missing legal end-turn action: {seed}")
                        nxt = session.call(
                            "h8_step", **token, action_id=choice,
                        )["result"]
                    else:
                        raise RuntimeError(
                            f"Unknown H8 boundary {kind!r} in {seed}")
                    if nxt["boundaryVersion"] != state["boundaryVersion"] + 1:
                        raise RuntimeError("Lost or duplicated H8 boundary")
                    actions.append((kind, choice))
                    kinds[kind] += 1
                    state = nxt
                else:
                    if state["kind"] in ("terminal", "unsupported"):
                        status = state["kind"]
                execution_seconds = time.perf_counter() - begin
                history = session.call(
                    "h8_decision_history", h8_handle=handle)["decisions"]
                mechanics_sha = session.call(
                    "h8_audit_state_fingerprint",
                    h8_handle=handle)["fingerprint"]
                if len(history) != len(actions):
                    raise RuntimeError("H8 decision journal count diverged")
                should_audit = index % audit_every == 0
                audit_start = time.perf_counter()
                if should_audit:
                    result = session.call(
                        "h8_replay_audit", h8_handle=handle)
                    if not result["replayVerified"]:
                        raise RuntimeError("H8 audit did not verify")
                audit_seconds = time.perf_counter() - audit_start
                results.append(dict(
                    seed=seed, status=status, decisions=len(actions),
                    kinds=dict(kinds),
                    trace_digest=canonical_hash(actions),
                    journal_digest=canonical_hash(history),
                    mechanics_sha256=mechanics_sha,
                    execution_seconds=execution_seconds,
                    replay_verified=should_audit,
                    audit_seconds=audit_seconds if should_audit else 0.0,
                ))
            finally:
                released = session.call(
                    "h8_release_many", h8_handles=[handle])
                if released["released"] != 1:
                    raise RuntimeError("H8 session handle leaked")
    return results


def run_cohort(
    seeds: list[str], dll: str, bindings: str,
    mode: str, workers: int, max_boundaries: int, audit_every: int,
) -> dict[str, Any]:
    began = time.perf_counter()
    shards = split_shards(seeds, workers)
    with ProcessPoolExecutor(max_workers=len(shards)) as pool:
        futures = [
            pool.submit(
                _run_shard, shard, dll, bindings, mode,
                max_boundaries, audit_every)
            for shard in shards
        ]
        rows = [row for future in futures for row in future.result()]
    wall = time.perf_counter() - began
    by_seed = {r["seed"]: r for r in rows}
    if set(by_seed) != set(seeds) or len(rows) != len(seeds):
        raise RuntimeError("H8 benchmark lost or duplicated source sessions")
    counts = Counter(r["status"] for r in rows)
    kinds = Counter()
    for row in rows:
        kinds.update(row["kinds"])
    total = sum(r["decisions"] for r in rows)
    if not total:
        raise RuntimeError("No H8 decisions advanced")
    return dict(
        mode=mode, workers_requested=workers,
        worker_processes_observed=len(shards),
        episodes=len(rows), statuses=dict(counts),
        supported_decisions=total, decisions_by_kind=dict(kinds),
        replay_verified=sum(r["replay_verified"] for r in rows),
        wall_seconds=wall,
        supported_decisions_per_second=total / wall,
        sessions_per_second=len(rows) / wall,
        audit_seconds_sum=sum(r["audit_seconds"] for r in rows),
        source_digest=canonical_hash(seeds),
        trajectories=by_seed,
    )


def validate_pair(a: dict[str, Any], b: dict[str, Any]) -> None:
    """Compare the same real source seeds under checked/native resolution."""
    if a["source_digest"] != b["source_digest"]:
        raise RuntimeError("Paired benchmark source seeds differ")
    for seed, left in a["trajectories"].items():
        right = b["trajectories"][seed]
        for key in ("status", "decisions", "kinds",
                    "trace_digest", "journal_digest", "mechanics_sha256"):
            if left[key] != right[key]:
                raise RuntimeError(
                    f"H8 {seed} diverged on {key}: "
                    f"{left[key]!r} vs {right[key]!r}")


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--emulator-dll", type=Path, required=True)
    p.add_argument("--emulator-bindings", type=Path, required=True)
    p.add_argument("--emulator-revision", required=True)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--workers", type=int, default=15)
    p.add_argument("--episodes", type=int, default=128)
    p.add_argument("--max-boundaries", type=int, default=80)
    p.add_argument("--replay-every", type=int, default=8)
    p.add_argument("--order", choices=("ab", "abba"), default="abba")
    p.add_argument("--seed-prefix", default="h8-a10-paired-mawler-v1")
    args = p.parse_args()
    if (args.workers < 1 or args.episodes < 1 or
            args.max_boundaries < 1 or args.replay_every < 1):
        p.error("Require positive workers, episodes, boundaries and replay rate")
    if not args.emulator_dll.is_file():
        p.error("Emulator DLL does not exist")
    if not (args.emulator_bindings / "sts2_emulator" / "__init__.py").is_file():
        p.error("Emulator Python bindings do not exist")

    from pathlib import Path as _Path
    dll = str(args.emulator_dll.resolve())
    bindings = str(args.emulator_bindings.resolve())
    sha = hashlib.sha256(_Path(dll).read_bytes()).hexdigest()
    seeds = [f"{args.seed_prefix}:{i:05d}" for i in range(args.episodes)]
    order = ["checked", "native"]
    if args.order == "abba":
        order = ["checked", "native", "native", "checked"]
    cohorts: list[dict[str, Any]] = []
    for index, mode in enumerate(order):
        print(
            f"[h8-benchmark] cohort={index + 1}/{len(order)} "
            f"mode={mode} workers={args.workers} episodes={args.episodes}",
            flush=True)
        cohort = run_cohort(
            seeds, dll, bindings, mode, args.workers,
            args.max_boundaries, args.replay_every)
        cohorts.append(cohort)
        print(
            f"[h8-benchmark] {mode} {cohort['supported_decisions_per_second']:.2f} "
            f"decisions/s, terminal={cohort['statuses'].get('terminal', 0)}, "
            f"unsupported={cohort['statuses'].get('unsupported', 0)}, "
            f"capped={cohort['statuses'].get('capped', 0)}",
            flush=True)
    for cohort in cohorts[1:]:
        validate_pair(cohorts[0], cohort)
    output = dict(
        schema="sts2-h8-scripted-session-ab-v1",
        evidence="H8-versioned-JSONL-scripted-A10-Mawler-only-not-H7-Transformer",
        emulator_revision=args.emulator_revision,
        emulator_dll_sha256=sha,
        source_digest=canonical_hash(seeds),
        workers=args.workers,
        episodes=args.episodes,
        max_boundaries=args.max_boundaries,
        replay_every=args.replay_every,
        order=order,
        exact_paired_journals=True,
        cohorts=cohorts,
    )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(output, indent=2) + "\n", encoding="utf-8")
    print(
        "[h8-benchmark] paired journals exact; report=" + str(args.output),
        flush=True)


if __name__ == "__main__":
    main()
