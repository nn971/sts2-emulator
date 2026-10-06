# Milestone 1 — Trustworthy, practically fast whole-run emulator

Milestone 1 is the first substantive release target.

## Acceptance criteria

### Fidelity

A version-pinned corpus containing at least 1,000 complete or terminally interrupted real-game runs can be replayed with:

- no unexplained canonical-state divergence at any decision boundary;
- exact agreement on legal-action sets for covered phases;
- exact agreement on RNG stream state/consumption for audited streams;
- explicit categorization of any intentionally ignored presentation-only state;
- deterministic repeatability of emulator replay.

The corpus must cover every top-level phase that is declared supported: combat, rewards, map choice, shops, rests, events, relic/potion/card acquisition, act transitions, and terminal outcomes.

### Fork semantics

For any canonical state `S`:

- `Fork(S)` is independent: mutating/advancing one branch cannot alter a sibling;
- canonical equality is preserved immediately after fork;
- RNG state is branch-local;
- model/object references inside a branch refer to the branch-local graph;
- fork behavior has dedicated invariant tests.

### Performance

Performance is measured on declared reference hardware. Initial targets are intentionally broad:

- **single-core lightweight transitions:** aim for `>= 50k/s` once semantics stabilize;
- **aggregate workstation throughput:** aim for `>= 500k/s`, then `>= 1M/s` where workloads parallelize;
- **fork latency:** low enough to create search branches at high frequency without full deep-copy cost;
- **state hashing:** cheap enough to use on essentially every search node when transpositions matter;
- **memory:** bounded enough that large search frontiers do not become allocation/GC dominated.

Failing a speed target does not permit weakening parity. It opens an optimization task.

### Tooling

Milestone 1 includes:

- native/reference trace capture protocol;
- canonical trace schema;
- replay runner;
- first-divergence diff;
- trace corpus manifest with build/version provenance;
- performance benchmark suite;
- CI regression suite.

## Non-goals for Milestone 1

- implementing downstream strategy/search/training systems;
- designing final observation embeddings;
- solving all combats optimally;
- producing a polished player-facing mod;
- reproducing UI/animation/audio state;
- speculative mechanics implemented only to increase a coverage counter.

## Exit question

Milestone 1 is complete when we can answer “yes” to:

> If a downstream consumer observes a surprising long-horizon consequence, do we trust the emulator enough to investigate the game logic rather than first suspect simulation drift?
