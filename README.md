# STS2 Emulator

A **trustworthy, practically fast, whole-run emulator for Slay the Spire 2**.

This repository is intended to stand on its own as emulator infrastructure and to be embedded as a **subrepository/submodule** inside a future AI-learning project. It owns game semantics, state transitions, RNG reproduction, parity tooling, and performance-critical simulation. It deliberately does **not** own strategy search, policy/value models, training loops, experiment management, or decision databases.

The first milestone is the whole point of this repository:

The active target is the [complete v111 single-player roadmap](docs/FULL_IMPLEMENTATION_ROADMAP.md).
See [current implementation and remaining gates](docs/V111_IMPLEMENTATION_STATUS.md)
and the [versioned consumer contract](docs/V111_INTERFACE.md). Configured v111
execution currently supports Silent Act 1 with explicit prototype RNG fidelity;
native Hive/Glory and other characters fail explicitly. The legacy complete
prototype remains available through its original API.

> **Make complete seeded runs simulatable end to end first, then iteratively replace prototype content, RNG, and timing with native STS2 semantics and optimize representative AI workloads.**

## Scope boundary

This repository owns:

- canonical whole-run state;
- deterministic `Step(state, action)` semantics;
- legal-action generation;
- complete continuation-relevant RNG state;
- cheap independent `Fork()`;
- combat, map, rewards, shops, events, rests, relics, potions, and act transitions;
- native/live-game reference capture;
- differential replay and parity tests;
- stable serialization, hashing, and language bindings;
- performance benchmarks for simulation consumers.

A parent AI repository should own:

- search algorithms;
- strategic experience/decision databases;
- training datasets derived from emulator output;
- policy/value/world models;
- RL or expert-iteration loops;
- experiment configuration and evaluation;
- player-facing automation or control.

See [docs/SUBREPO_INTEGRATION.md](docs/SUBREPO_INTEGRATION.md).

## Design principles

1. **Whole-run functionality first.** A restrictive complete game loop is more useful early than isolated high-fidelity subsystems.
2. **Replaceable approximations.** Prototype content, RNG, generation rules, and timing are explicitly versioned so native semantics can replace them cleanly.
3. **Whole-run state.** Combat is one subsystem of a persistent run state.
4. **Deterministic core.** Hidden RNG state belongs to engine state, so `Step(S, a)` is deterministic.
5. **Forking is first-class.** Counterfactual branches must be cheap and isolated.
6. **Version-pinned truth.** Every trace and parity result names the target game build and emulator revision.
7. **Explicit uncertainty.** Unsupported or weakly verified mechanics fail loudly or carry explicit status.
8. **Stable consumer boundary.** AI/search code consumes the emulator through documented APIs and bindings instead of importing internal mechanics.
9. **No presentation baggage.** UI, animation, audio, and rendering are outside canonical simulation state unless they affect future mechanics.

## Milestone 1: trustworthy + practically fast emulator

Milestone 1 succeeds when a large corpus of real seeded runs can be replayed with canonical agreement at every decision boundary, and the emulator has measured throughput/fork/memory characteristics suitable for background search and learning workloads.

Initial acceptance target:

- 1,000+ version-pinned real-game runs with diverse/random legal decisions;
- no unexplained canonical-state divergence from run start to terminal state;
- exact legal-action agreement for supported phases;
- reproducible RNG-stream state and consumption;
- cheap isolated full-run forks;
- reproducible benchmark suite and regression budgets;
- a stable native/C ABI or equivalent binding suitable for a parent research project.

See [docs/MILESTONE_1.md](docs/MILESTONE_1.md).

## Repository layout

```text
.
├── src/
│   ├── Sts2Emulator.Core/       # Canonical deterministic state + engine contracts
│   ├── Sts2Emulator.Trace/      # Versioned trace/state protocol
│   └── Sts2Emulator.Cli/        # Developer CLI; no hidden game rules
├── tests/
│   └── Sts2Emulator.Core.Tests/ # Fork/hash/protocol invariants and later parity fixtures
├── benchmarks/
│   └── Sts2Emulator.Microbench/ # Fork/hash/transition microbenchmarks
├── bindings/
│   └── python/                   # Thin standard-library JSONL consumer binding
├── schemas/                      # Language-neutral state/trace schemas
├── tools/                        # Replay/parity utilities
├── scripts/                      # Fish-shell developer helpers
├── data/                         # Local traces/extractions; proprietary data is not committed
├── docs/                         # Fidelity, RNG, architecture, performance, integration, roadmap
└── .github/workflows/ci.yml
```

## Build

Requires .NET 9 SDK.

```fish
./scripts/test.fish
./scripts/bench.fish
```

Equivalent direct commands:

```text
dotnet test Sts2Emulator.sln -c Release
dotnet run --project benchmarks/Sts2Emulator.Microbench/Sts2Emulator.Microbench.csproj -c Release
```

The current development branch contains a **restrictive Silent whole-run prototype**. Its `proto.*` content, development RNG, room generation, and timing model are intentionally provisional; they exist to exercise the complete run architecture before native-fidelity replacement.

Useful developer commands:

```fish
dotnet run --project src/Sts2Emulator.Cli -- prototype-run my-seed
dotnet run --project src/Sts2Emulator.Cli -- prototype-sweep 100
dotnet run --project src/Sts2Emulator.Cli -- prototype-manifest
```

The prototype also exposes `PrototypeAiEnvironment` with the versioned `prototype-ai-v0`
observation/action boundary, stable action IDs, deterministic reset/step/fork, and sibling
expansion for search consumers.

## Start here

1. [docs/PURPOSE.md](docs/PURPOSE.md) — emulator charter and repository boundary.
2. [docs/SUBREPO_INTEGRATION.md](docs/SUBREPO_INTEGRATION.md) — how the future AI project should consume this repo.
3. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — native oracle → exact emulator → stable consumer API.
4. [docs/FIDELITY_CONTRACT.md](docs/FIDELITY_CONTRACT.md) — what “trustworthy” means.
5. [docs/STATE_MODEL.md](docs/STATE_MODEL.md) — canonical exact state and public projections.
6. [docs/RNG.md](docs/RNG.md) — RNG as explicit forkable state.
7. [docs/PARITY_TESTING.md](docs/PARITY_TESTING.md) — trace capture and first-divergence workflow.
8. [docs/PERFORMANCE.md](docs/PERFORMANCE.md) — speed, memory, fork, and batch metrics.
9. [docs/REFERENCE_BRIDGE.md](docs/REFERENCE_BRIDGE.md) — native-game oracle responsibilities.
10. [docs/PROTOTYPE_SCOPE.md](docs/PROTOTYPE_SCOPE.md) — current Silent whole-run prototype and replacement boundaries.
11. [docs/AI_INTERFACE.md](docs/AI_INTERFACE.md) — prototype observation/action boundary for search and learning.
12. [docs/NEXT_STEPS.md](docs/NEXT_STEPS.md) — concrete next implementation sprints.
13. [docs/ROADMAP.md](docs/ROADMAP.md) — emulator-only roadmap.
14. [docs/REFERENCES.md](docs/REFERENCES.md) — relevant STS2 emulator/prediction prior art.

## Current status

**AI-ready whole-run prototype.** A restrictive Silent ruleset supports seeded runs through constrained maps, varied encounters, combat, rewards, shops, events, richer rests, bosses, act transitions, and terminal victory/death. A versioned prototype AI adapter and capability manifest are available. Prototype semantics still carry no native-fidelity claim.

## Independence

This is an independent research/interoperability project and is not affiliated with Mega Crit. Do not commit proprietary game assets, redistributed assemblies, or copied/decompiled source text. See [docs/LEGAL_AND_DATA_POLICY.md](docs/LEGAL_AND_DATA_POLICY.md).
