# Development history

A chronological guide to the emulator's development. It complements Git commits, the [source-audit index](reference-builds/INDEX.md) and the [archived cumulative mechanics ledger](archive/status/2026-10-13-v111-implementation-ledger.md). Early milestone documents reflect the state **at that time**, not the native game's verified behavior today.

## October 6, 2026 — Repository foundation and restrictive Silent prototype

The [initial repository commit](https://github.com/nn971/sts2-emulator/commit/ac8943686e9838a8df0f5ae49294a6ee543242d8) established a separate C# emulator and deterministic simulation/trace scaffolding. The early whole-run Silent prototype added persistent acts, maps, rewards, encounters, shops, rests, events, powers and suspendable player choices. See [prototype scope](archive/roadmaps/2026-10-13-prototype-scope.md) and [original roadmap](archive/roadmaps/2026-10-13-prototype-roadmap.md).

## October 6–10 — Pinned v0.111 mechanics, Silent cards and Act 1

The project pinned the v0.111.0 game build (game commit `41cef1ea`) and source/structured reference data. Source-backed mechanics expanded across Silent cards, colorless cards, enemy hooks, reward generation, Overgrowth and Underdocks. The [content-expansion decision](archive/roadmaps/2026-10-13-scope-expansion.md) and [Act 1 combat audit](archive/roadmaps/2026-10-13-act1-combat-completeness.md) document earlier priorities and phased integration.

## October 10–13 — Hive, Glory and all-Act execution

The [full implementation roadmap](FULL_IMPLEMENTATION_ROADMAP.md) defined the eventual single-player destination. The Silent gameplay branch added later-act encounter queues, events, act-entry Ancients, history persistence, Glory double bosses (A10), and the Architect ending. Eight full-route cases use synthetic combat finishers; a 256-seed natural legal-choice sweep reached Hive with no engine blockers but produced no Glory entry under its baseline policy. See [regional integration](reference-builds/v0.111.0-regional-integration-bags.md), [natural run sweep](reference-builds/v0.111.0-unassisted-silent-run-sweep.md), and the [regional development plan](archive/roadmaps/2026-10-13-regional-integrity-plan.md).

## October 12–13 — Native Inspector and RNG work branched separately

Native combat recordings and recovered checkpoints motivated the [first actual-engine comparison](V111_NATIVE_ENGINE_FIDELITY.md) and [recovered checkpoint batch](V111_NATIVE_PREFIX_BATCH.md). The separate fidelity/RNG integration review chain begins at [PR #137](https://github.com/nn971/sts2-emulator/pull/137) and continues through [#138](https://github.com/nn971/sts2-emulator/pull/138), [#139](https://github.com/nn971/sts2-emulator/pull/139), and [#140](https://github.com/nn971/sts2-emulator/pull/140). Verify their individual CI and merge states before attributing them to `main`. The [proposed RNG architecture](DESIRED_RNG_IMPLEMENTATION.md) and [Inspector design](NATIVE_FIDELITY_INSPECTOR_PLAN.md) remain unchanged by this documentation cleanup.

## October 13 — Gameplay freeze merged into main

[PR #141](https://github.com/nn971/sts2-emulator/pull/141) published the Silent all-Act baseline on `main` as [`d6d65cc2a588cdd569e393cf1c7d8a299a43461c`](https://github.com/nn971/sts2-emulator/commit/d6d65cc2a588cdd569e393cf1c7d8a299a43461c). The [snapshot branch](https://github.com/nn971/sts2-emulator/tree/snapshot/silent-v111-all-act-20261013) was created at the same commit. The frozen revision is suitable for reproducible comparison; the [baseline report](SILENT_V111_FROZEN_BASELINE.md) explicitly records incomplete mechanical evidence and the reported 296 failing global tests.

## October 14 — Documentation consolidation

Superseded plans and the long mechanics log were archived; their original locations became compatibility pointers. The [documentation index](INDEX.md), [reference-audit index](reference-builds/INDEX.md), and [archive inventory](archive/INDEX.md) make the record navigable, while current development proceeds via [Next steps](NEXT_STEPS.md).

## Reading paths

For **current capability**, start with the [frozen baseline](SILENT_V111_FROZEN_BASELINE.md). For **why a mechanic was introduced**, use the [chronological mechanics ledger](archive/status/2026-10-13-v111-implementation-ledger.md) and [reference audits](reference-builds/INDEX.md). For **future work**, use [next steps](NEXT_STEPS.md). For **longer-term scope**, use the [full roadmap](FULL_IMPLEMENTATION_ROADMAP.md).
