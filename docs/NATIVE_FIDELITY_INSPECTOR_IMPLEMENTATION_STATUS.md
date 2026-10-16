# Inspector implementation status

> **Chronology note:** The P0–P5 table below is a **foundation-branch snapshot** and is not an exhaustive current integration-branch status. Later October 2026 evidence and native RNG/spectator draft PRs may supersede individual "not tested" / "deferred" entries. The [inspector plan](NATIVE_FIDELITY_INSPECTOR_PLAN.md) and [issue #122](https://github.com/nn971/sts2-emulator/issues/122) are authoritative for scope; the [unified stochastic architecture](UNIFIED_STOCHASTIC_ARCHITECTURE.md) does not make native bridge operation depend on Chance Law or ReferenceNature.

**Scope ownership (2026-10-16):** Native-game fidelity checks,
mechanics/trace differential comparison, exact RNG streams/cursor/shuffle
and source hook-order audits belong to this inspector work and
[inspector issue #122](https://github.com/nn971/sts2-emulator/issues/122).
See the [explicitly transferred fidelity gates](NATIVE_FIDELITY_INSPECTOR_PLAN.md#scope-ownership--fidelity-checks-moved-out-of-nature-issue-143-2026-10-16).
[Nature training issue #143](https://github.com/nn971/sts2-emulator/issues/143)
owns the playable two-site API and emulator-owned ordinary-RNG-vs-Nature
experiment baseline. Original-game parity is not a #143 acceptance gate.
The separate N5 source probability calibration remains distinct.


**Feature branch:** `feature/native-fidelity-inspector-foundation`

| Gate | Status | Evidence / blocker |
|---|---|---|
| P0 | PARTIAL | Pinned binary SHA-256 preflight; game-conditional C# compile, manifest/PCK packaging, and opt-in loader; actual installed-game compile/load test outstanding |
| P1 | PARTIAL | Combat action/executor and history callbacks, stable readiness check, source-audited snapshot adapter, JSONL 0.2 writer, independent event journal and local trace auditor; nested prompts/map/reward support and no-perturbation run outstanding |
| P2 | PARTIAL | Strict offline parity checker, new .NET typed structural combat importer, partial continuation coverage CLI, native Strike/Defend expectation extractor; deterministic emulator stepping and real native fixtures outstanding |
| P3 | PARTIAL | Read-only F8 Godot timeline and offline HTML viewer; action coordinator and optional loopback protocol; native Step/Resume dispatch still deferred |
| P4 | NOT STARTED | Broad game phases/acts, native corpus, first real differential regression |
| P5 | DEFERRED | RNG/Nature and agent playback are independent future milestones |

## Verification and honesty

- Default .NET build/tests and Python synthetic protocol tests run in CI.
- The game-dependent source is compiled only when `Sts2DataDir` points to a real installed game. **Native v111 signatures and run behavior are not yet validated against that build.**
- Recording opt-in requires `STS2_FIDELITY_CAPTURE=1` and matching
  `STS2_FIDELITY_EXPECT_SHA256`. Build-pin data remains local.
- The passive capture begins at the first stable player decision, so earlier
  combat-setup hook order remains outside semantic event coverage.
- Typed native history events are *diagnostic* until exact payload fields are
  audited. Unsupported capture fields and missing RNG state are declared.
- Structural recording validity is reported separately from emulator
  mechanical parity. Synthetic comparator `MATCH` is not a real-game result.
- `CHANCE_UNALIGNED`, `UNSUPPORTED`, `INVALID_SETUP` and
  `INCOMPLETE_CAPTURE` never count as parity passes.

## To satisfy the first acceptance gate

1. On the pinned installed game, run `scripts/build-native-inspector.fish`,
   correct any version-specific native API differences and preserve exact
   SHA-256 and loader/build metadata.
2. Compare opt-in passive recording against a baseline human Silent run for
   observable game outcomes and RNG non-perturbation.
3. Capture Strike and Defend decisions, transition effects, event order, and
   complete supported fields. Audit event scalar extraction against the game.
4. Implement a strict native combat snapshot importer into the emulator,
   including relevant hidden continuation and target/card identities.
5. Run a deterministic native/emulator step and intentionally introduce a
   mechanical defect; require the precise first differing field/event.
6. Wire the native main-thread controller to actual audited player choices
   and enable a synchronized comparison display.

See Issue #122 and PR #121 for active tracking.

## Latest P2 progress

- `Sts2Emulator.Core.NativeCombatSnapshotImporter` reconstructs a typed
  visible-state projection with HP, block, energy, turn, creature identities,
  powers, and complete visible card zones. Malformed or duplicated instance IDs
  and missing fields produce `UNSUPPORTED`. It remains `PARTIAL` and
  `ReadyForDeterministicStep=false` while native continuation is unaudited.
- `tools/native_expectation_fixture.py` extracts native recorded
  Strike/Defend pre-state/action/post-state and diagnostic history, labelled
  `RECORDED_NATIVE_EXPECTATION` and `emulator_parity=NOT_TESTED`.
- `tools/native_parity.py` rejects unclosed decisions, duplicate actions,
  orphaned events, and inconsistent event/hash linkage.
- Archived v0.1xx API references confirm `NPlayerHand` lives in
  `MegaCrit.Sts2.Core.Nodes.Combat` and combat card identity is resolved via
  `NetCombatCard`. The user's pinned v0.111 build remains untested.

## First installed-game crash diagnosis (2026-10-12)

- The native mod was successfully compiled against the user's local game
  and its `NativeV111PassiveBridge.OnFrame` callback ran.
- The observed crash log repeatedly reported
  `FileNotFoundException: Sts2Emulator.Trace, Version=1.0.0.0`.
  The game's mod loader does not reliably probe the sibling protocol DLLs.
- Native builds now compile all Core/Trace protocol `.cs` sources directly
  into `Sts2ReferenceMod.dll` while game-independent builds retain
  ordinary project references; staging removes stale companion DLLs.
- The game also reported a separate
  `UndoAndRedoForkCode` / `NInputManager.remappableKeyboardInputs`
  missing field from another mod. Test with that mod disabled separately.
- The loader scans PCKs, not DLLs: correct game installation must include
  `Sts2ReferenceMod.pck` with root-level `mod_manifest.json`, plus
  `Sts2ReferenceMod.dll`.
- **Pending:** rebuild this revision against the user's installed DLL,
  then retry native load and passive capture. Do not claim crash
  resolved until the new local smoke test succeeds.

## Minimal resource-only PCK (2026-10-12)

The first game-local test produced the corrected single native DLL but no PCK.
This is an incomplete mod package: the game's loader scans recursively for
`*.pck` and reads `res://mod_manifest.json`, then loads the same-basename DLL.
Added `tools/make_inspector_pck.py`, a tested one-file Godot 4.5 PCK v3
packer, and optional Godot-free staging in
`scripts/build-native-inspector.fish GAME_ROOT`. This is source-format
verified, not yet loaded by the user's pinned game.
