# Silent v0.111 all-Act gameplay baseline (2026-10-13)

**Status: frozen gameplay checkpoint, merged into `main` on 2026-10-13.** This document identifies the gameplay implementation to preserve while development focuses on the integrated native fidelity Inspector and desired RNG architecture. It is a reproducible **implementation baseline**, not a statement of full mechanics coverage, native parity, or a passing complete test suite.

## Scope

- Pinned game: STS2 v0.111.0, source commit `41cef1ea`, pinned build fingerprint `3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e`.
- Single-player Silent, fully unlocked profile, with generated routes `Overgrowth` or `Underdocks` → `Hive` → `Glory`, including the Architect conclusion.
- Opt-in configured run mode: `v111-experimental-silent-three-act-route-v1` (see [v111 interface](V111_INTERFACE.md)). Preserve the legacy prototype adapter and previously versioned fidelity modes.
- The separate native-Inspector/RNG integration review stack is **not part of this gameplay snapshot**. Its branches/PRs should be tested and integrated independently.
- Cross-character mechanics and multiplayer-only content are out of scope for this freeze.

## Delivered gameplay baseline

Both Act 1 variants and both later acts have implemented and integrated route definitions, regional combat/event dispatch, persisted encounter history and act entry, shared rewards/shops/rests, Ancient opening choices and normal terminal outcomes. A0 and A10 synthetic full-route tests exercise both Act 1 starts, including Glory's double-boss progression. Source-backed Silent card implementation is broadly complete; event, relic, potion, affliction and modifier support is substantial.

The committed `data/coverage/v111-baseline.json` currently records **4/4 acts**, **89/90 encounters implemented/integrated** (`TUNNELER_NORMAL` partial), **63/66 events implemented** and **65/66 integrated**, and **8/8 rest options**. These are inventory bookkeeping counts: some recently added relics remain stale in that file, some entries refer to foreign-character content, and implementation/registration is distinct from full branch or interaction testing. Regenerate the coverage report before relying on the exact totals.

## Known limitations carried into the snapshot

1. **Global test baseline:** the fidelity integration PR [#137](https://github.com/nn971/sts2-emulator/pull/137) reports 296 existing failing tests from this all-Act gameplay base. Focused suites have passed in subsequent integration work, but the full suite has not been certified green for the freeze.
2. **Incomplete gameplay evidence:** the reported eight full-route tests use synthetic combat finishers. A 256-seed natural legal-choice sweep terminated without engine blockers but did not reach Glory with its baseline policy. Repeated natural full-Act victories are not yet established.
3. **Remaining registered gaps:** `TUNNELER_NORMAL` partial, `NEOW` and `OROBAS` partial in the coverage ledger, unenumerated/untested event choices, modifier variants and specialized interactions. `COLORFUL_PHILOSOPHERS` requires postponed foreign-character pools.
4. **Generation and probability:** some eligible-pool ordering, restrictions, act/Ancient assignment, context RNG and gameplay sampling still use prototype behavior. Exact native RNG ownership and call order are the next development track.
5. **Validation:** exact action/choice timing, legal-action and state parity against the installed native game remain unverified in the full run corpus. The ordinary gameplay baseline can still be used for deterministic simulation and testing within its declared limitations.

## Rules for working from the freeze

- **Immutable reference:** Git commit [`d6d65cc2a588cdd569e393cf1c7d8a299a43461c`](https://github.com/nn971/sts2-emulator/commit/d6d65cc2a588cdd569e393cf1c7d8a299a43461c), merged via [PR #141](https://github.com/nn971/sts2-emulator/pull/141), with the matching `snapshot/silent-v111-all-act-20261013` branch. Pin downstream work to this exact SHA for reproducibility.
- **Gameplay changes:** fix mechanics only on a separate, explicitly named branch, with tests and a recorded baseline diff; preserve this frozen revision for regression comparison.
- **Fidelity/RNG development:** apply isolated changes on the pre-existing dedicated branches and their review chain; measure against the snapshot and record divergence without silently redefining expected mechanics.
- **Documentation:** this concise page is the freeze entrypoint. [Implementation status](V111_IMPLEMENTATION_STATUS.md) is the cumulative dated mechanical ledger; [full roadmap](FULL_IMPLEMENTATION_ROADMAP.md) defines the larger eventual project. [Next steps](NEXT_STEPS.md) tracks the fidelity/RNG phase.

## Reproducing local diagnostics

Requires .NET 9 SDK.

```fish
dotnet test Sts2Emulator.sln -c Release
dotnet run --project src/Sts2Emulator.Cli -- v111-coverage
python tools/check_v111_coverage.py
```

Classify full-suite failures and missing coverage honestly; expected nonzero results are part of the recorded baseline. Native game attachment requires the local pinned Steam build, outside remote CI.

## Development history

Read the [chronological history](HISTORY.md) and [archived mechanics ledger](archive/status/2026-10-13-v111-implementation-ledger.md) for dated implementation details. The [document index](INDEX.md) separates active, historical and proposed work.

## Deferred targets

Full real-game fidelity corpus, exact native RNG reproduction, multi-character/shared foreign-card behavior, and release-grade performance/API stabilization are separate milestones.
