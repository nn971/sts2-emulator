# Next development phase — Native fidelity and RNG

**Effective after the Silent v0.111 gameplay freeze (2026-10-13).**

> **2026-10-31 fidelity priority update:** The [live lockstep Silent A10 plan](LIVE_LOCKSTEP_NATIVE_FIDELITY_PLAN.md) is the canonical ordered implementation plan for native fidelity (issue #122): installed-game run + reference-mod action capture + persistent independent emulator Native-RNG continuation + in-mod first-difference Inspector. The earlier priorities below remain a historical baseline and general checklist; they must not be interpreted as postponing exact Native RNG until after an unrelated native step controller.

The frozen [all-Act Silent gameplay baseline](SILENT_V111_FROZEN_BASELINE.md) is the comparison target. Preserve the existing gameplay implementation and consumer contracts while the Inspector/RNG work progresses on isolated branches.

## 1. Establish the reference baseline

- **Completed (2026-10-13):** the Silent gameplay freeze was merged through [PR #141](https://github.com/nn971/sts2-emulator/pull/141); exact reference SHA is [`d6d65cc2a588cdd569e393cf1c7d8a299a43461c`](https://github.com/nn971/sts2-emulator/commit/d6d65cc2a588cdd569e393cf1c7d8a299a43461c) and the matching snapshot branch is `snapshot/silent-v111-all-act-20261013`.
- Keep the [baseline record](SILENT_V111_FROZEN_BASELINE.md) and [coverage inventory](../data/coverage/v111-baseline.json) linked to the exact frozen revision, including the reported 296 global test failures and eight synthetic full-route cases.
- Keep a reproducer for any unexpected baseline regression. Gameplay fixes go into scoped PRs, never retrospectively change the frozen reference.

## 2. Complete native Inspector acceptance

- Compile/load the Inspector mod locally against the pinned real-game DLL and confirm its opt-in instrumentation works during gameplay.
- Collect representative action envelopes, nested choices, enemy turns, potion/relic interactions and full-combat observations; audit provenance, recording completeness and source hooks.
- Compare actual native engine actions/states with the emulator on independently importable checkpoints; classify unsupported observations instead of promoting partial evidence to full parity.
- Apply the [native-fidelity plan](V111_NATIVE_ENGINE_FIDELITY.md) and the dedicated review PR stack, without mixing gameplay modifications.

## 3. Implement the desired RNG abstraction

- Follow [desired RNG implementation](DESIRED_RNG_IMPLEMENTATION.md): mechanics issue typed chance requests at source-authentic call sites, and the native / scripted / externally controlled Nature providers share the continuation.
- Preserve per-stream ownership, ordering, native primitive draw behavior and exact snapshots in native mode.
- Use game-local native stream and action evidence to test call order and eligibility. Calibrate chance distributions separately from mechanical behavior.
- Keep controlled Nature interfaces available for downstream AI, with algorithm/training experiments owned by `sts2-ai`.

## 4. Completion and handoff gates

- Check full-test regressions against the frozen SHA, and maintain a provenance-aware list of observed mismatch classes.
- Run repeatable game-local Inspector corpora and a representative local emulator replay suite.
- Merge Inspector/RNG changes only after focused checks and documented behavior changes, while retaining a stable baseline for comparisons.

## Deliberate postponements

Full cross-character mechanics, multiplayer behavior, release-grade optimization and all-character native parity remain outside this phase. The [full implementation roadmap](FULL_IMPLEMENTATION_ROADMAP.md) records those longer-term targets.
