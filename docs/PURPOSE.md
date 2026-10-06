# Purpose and repository charter

## Purpose

`sts2-emulator` exists to provide one thing exceptionally well:

> **A behaviorally trustworthy, practically fast, forkable emulator of complete Slay the Spire 2 runs.**

The emulator should cover the full mechanical trajectory of a run: map generation and pathing, combats, card/relic/potion rewards, shops, events, rest sites, persistent counters and histories, act transitions, and terminal outcomes.

It is intended to be reused by a larger AI-learning project, but this repository remains an independent simulation component.

## Why it is a separate repository

Game semantics and AI research evolve at different rates.

The emulator needs:

- build-pinned semantic audits;
- parity fixtures;
- RNG reverse engineering;
- deterministic replay;
- performance engineering;
- stable state/action protocols;
- strict regression discipline.

A future AI project needs:

- search algorithms;
- learned models;
- strategic datasets;
- experiment orchestration;
- changing observation encodings;
- evaluation and training infrastructure.

Keeping these concerns separate gives the emulator a stable contract and lets AI experiments change rapidly without modifying game rules.

## Formal target

Model the game as a deterministic transition system once continuation-relevant hidden state is included:

```text
S_(t+1) = T(S_t, a_t)
```

`S` includes every state component required to continue the run exactly, including RNG streams and hidden histories. The central correctness claim is therefore about `T`.

For a pinned game build and a reference transition captured from the native game,

```text
(S, a) -> S_native'
```

the emulator should produce

```text
T_emulator(S, a) = S_native'
```

under the canonical state equivalence defined by this repository.

## Trustworthiness

Trust is established by evidence rather than by nominal feature coverage:

- exact target-build pinning;
- native/live-game state capture;
- canonical snapshots at decision boundaries;
- exact legal-action comparison;
- RNG state/consumption comparison;
- deterministic replay;
- first-divergence diagnostics;
- minimized parity regression fixtures;
- randomized/fuzzed whole-run traces;
- explicit unsupported/weakly verified mechanic tracking.

A mechanic counts as supported only when its semantics are understood well enough to test.

## Practical speed

The emulator is infrastructure for branching workloads, so raw `Step` throughput is only one metric. We also care about:

- fork latency and memory amplification;
- canonical/transposition hashing cost;
- allocation and GC pressure;
- batch stepping and parallel scaling;
- serialization overhead at the consumer boundary;
- replay throughput for large truth corpora.

Correctness has priority over throughput, but the completed milestone must be fast enough to serve as a practical simulation backend rather than merely a reference implementation.

## Stable consumer contract

The long-term public boundary should stay small:

```text
Create/Load state
GetLegalActions(S)
Step(S, a)
Fork(S)
Hash(S)
Serialize(S)
Inspect public/canonical state
BatchStep(...)
```

Bindings may expose additional optimized batch APIs, but parity-critical behavior remains in the canonical engine.

## Explicit non-goals

This repository does not own:

- a strategy policy;
- tree search or MCTS;
- a strategic decision database;
- RL training;
- policy/value/world models;
- experiment tracking;
- a generic LLM controller;
- player-facing automation.

Those belong in the parent AI project and consume this emulator as a dependency.
