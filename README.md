# STS2 Emulator

A deterministic, forkable, headless Slay the Spire 2 emulator for whole-run simulation, search and learning. Game mechanics, persistent state, chance resolution, native fidelity tooling and the consumer interface belong here; policies, strategic search and training belong in [sts2-ai](https://github.com/nn971/sts2-ai).

## Current milestone: frozen Silent v0.111 gameplay baseline

**Single-player Silent supports configured end-to-end Overgrowth / Underdocks → Hive → Glory gameplay**, with persisted state, regional encounters, events, Ancients, rewards, shops, rest sites and endgame routing. The baseline is broadly integrated, with explicit known omissions, approximate generation and mechanics requiring further tests. It is an **implementation checkpoint**, not a native-fidelity release.

Start with the [Silent all-Act freeze notes](docs/SILENT_V111_FROZEN_BASELINE.md) for scope, supported mode, current coverage, important test failures and unresolved mechanics. See [implementation status](docs/V111_IMPLEMENTATION_STATUS.md) for the cumulative mechanics audit.

The next active tracks are:

1. **Native Inspector and fidelity comparisons:** collect reliable native action, state, and RNG evidence and compare real-game transitions against the emulator.
2. **Desired RNG architecture:** source-ordered, typed random-choice requests, swappable native/controlled providers, deterministic continuations and explicit chance distributions.

Read the [desired RNG plan](docs/DESIRED_RNG_IMPLEMENTATION.md), [native engine fidelity notes](docs/V111_NATIVE_ENGINE_FIDELITY.md) and [current next steps](docs/NEXT_STEPS.md). The native-game source/binary pin is v0.111.0, game commit `41cef1ea`; mechanics support remains separately classified from native parity.

## Supported execution and contracts

- Stable whole-run state, deterministic `Step`, legal actions, forks and exact snapshot/restore.
- Opt-in Silent full-route fidelity ID: `v111-experimental-silent-three-act-route-v1`.
- Versioned JSONL consumer protocol and Python binding for external search/training.
- Historical restrictive prototype remains supported via its versioned adapters.
- Single-player only. Multiplayer behavior and other-character mechanics are postponed.

See [v111 interface](docs/V111_INTERFACE.md), [AI adapter](docs/AI_INTERFACE.md), and [subrepo integration](docs/SUBREPO_INTEGRATION.md). Registered content and supported execution are distinguished in the [coverage inventory](data/coverage/v111-baseline.json).

## Build and diagnostics

Requires .NET 9 SDK:

```fish
dotnet test Sts2Emulator.sln -c Release
dotnet run --project src/Sts2Emulator.Cli -- v111-coverage
python tools/check_v111_coverage.py
./scripts/bench.fish
```

The all-Act gameplay baseline has **known global test failures** (296 identified in the initial Inspector integration report); the full test suite and coverage checker are not yet certified green. See the [freeze limitations](docs/SILENT_V111_FROZEN_BASELINE.md) before interpreting a CI result.

The JSONL interface is launched with:

```fish
dotnet run --project src/Sts2Emulator.Cli -- prototype-ai-jsonl
```

Consult [the contract](docs/V111_INTERFACE.md) for `reset_v111`, legal actions, `step`, `fork`, `observe` and save/load.

## Documentation map

Start at the [documentation index](docs/INDEX.md) for the organized current contracts, proposed designs, reference evidence and historical plans; see the [development history](docs/HISTORY.md) for the chronological project narrative.

- [Freeze baseline](docs/SILENT_V111_FROZEN_BASELINE.md) — current stable gameplay reference and open gaps.
- [Next steps](docs/NEXT_STEPS.md) — native fidelity and RNG priorities.
- [Implementation status](docs/V111_IMPLEMENTATION_STATUS.md) — cumulative source-backed gameplay change ledger.
- [Full implementation roadmap](docs/FULL_IMPLEMENTATION_ROADMAP.md) — longer-term all-content/all-character ambition.
- [Emulator roadmap](docs/ROADMAP.md) — archived prototype milestones and later release gates.
- [RNG](docs/RNG.md) and [desired RNG implementation](docs/DESIRED_RNG_IMPLEMENTATION.md).
- [Parity testing](docs/PARITY_TESTING.md), [native reference bridge](docs/REFERENCE_BRIDGE.md), [fidelity contract](docs/FIDELITY_CONTRACT.md).
- [Project status](PROJECT_STATUS.md) — concise current index; the old rolling notes are archived.
- [Reference note index](docs/reference-builds/INDEX.md) — all pinned source audits and numbered mechanics passes.
- [Historical archive](docs/archive/INDEX.md) — previous implementation plans and progress logs.

## Repository responsibility and data policy

The emulator is independent of Mega Crit. The game installation, proprietary assets, redistributed assemblies and copied/decompiled source code remain local and uncommitted. Only separately audited metadata, implementation, test fixtures and documentation belong in this repository. See [legal and data policy](docs/LEGAL_AND_DATA_POLICY.md).
